using Avalonia.Controls;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using dbm_select.Models;
using MiniExcelLibs;
using SkiaSharp; // ✅ Required for Image Manipulation
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace dbm_select.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private readonly string _settingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DBM_Select",
            "settings.json");

        public MainWindowViewModel()
        {
            if (Design.IsDesignMode) return;

            // 1. Load Settings
            if (!LoadSettings())
            {
                string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string baseFolder = Path.Combine(documentsPath, "DBM_Select");
                string logsFolder = Path.Combine(baseFolder, "Logs");

                if (!Directory.Exists(baseFolder)) Directory.CreateDirectory(baseFolder);
                if (!Directory.Exists(logsFolder)) Directory.CreateDirectory(logsFolder);

                OutputFolderPath = baseFolder;
                ExcelFolderPath = logsFolder;
                ExcelFileName = "Client_Logs";
            }

            // 2. Determine Load Path
            string folderToLoad = !string.IsNullOrEmpty(_currentBrowseFolderPath) && Directory.Exists(_currentBrowseFolderPath)
                ? _currentBrowseFolderPath
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));

            // 3. Load Images Async
            _ = LoadImages(folderToLoad);

            UpdateVisibility("Basic");
        }

        // --- PATH PROPERTIES ---
        private string _snapOutputFolder = string.Empty;
        private string _snapExcelFolder = string.Empty;
        private string _snapExcelFileName = string.Empty;
        private string _currentBrowseFolderPath = string.Empty;

        [ObservableProperty] private bool _isSettingsDirty;

        [ObservableProperty] private string _outputFolderPath = string.Empty;
        partial void OnOutputFolderPathChanged(string value) => CheckSettingsDirty();

        [ObservableProperty] private string _excelFolderPath = string.Empty;
        partial void OnExcelFolderPathChanged(string value) => CheckSettingsDirty();

        [ObservableProperty] private string _excelFileName = "Client_Logs";
        partial void OnExcelFileNameChanged(string value) => CheckSettingsDirty();

        private void CheckSettingsDirty()
        {
            IsSettingsDirty = OutputFolderPath != _snapOutputFolder || ExcelFolderPath != _snapExcelFolder || ExcelFileName != _snapExcelFileName;
        }

        // --- APP STATE PROPERTIES ---
        [ObservableProperty] private string? _clientName;
        [ObservableProperty] private string? _clientEmail;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SelectedPackageDisplayName))]
        private string _selectedPackage = "Basic";

        public string SelectedPackageDisplayName => SelectedPackage == "Basic" ? "Basic Package" : $"Package {SelectedPackage}";

        [ObservableProperty] private bool _isBasicSelected = true;
        [ObservableProperty] private bool _isPkgASelected;
        [ObservableProperty] private bool _isPkgBSelected;
        [ObservableProperty] private bool _isPkgCSelected;
        [ObservableProperty] private bool _isPkgDSelected;

        [ObservableProperty] private ImageItem? _selectedImage;

        // Trigger async preview loading when selection changes
        partial void OnSelectedImageChanged(ImageItem? value)
        {
            _ = UpdatePreviewAsync(value);
        }

        [ObservableProperty] private ImageItem? _previewImage;
        [ObservableProperty] private bool _isLoadingPreview;

        // Modal Preview Images
        [ObservableProperty] private ImageItem? _previewImage8x10;
        [ObservableProperty] private ImageItem? _previewImageBarong;
        [ObservableProperty] private ImageItem? _previewImageCreative;
        [ObservableProperty] private ImageItem? _previewImageAny;
        [ObservableProperty] private ImageItem? _previewImageInstax;
        [ObservableProperty] private bool _isModalLoading;

        [ObservableProperty] private ImageItem? _image8x10;
        [ObservableProperty] private ImageItem? _imageBarong;
        [ObservableProperty] private ImageItem? _imageCreative;
        [ObservableProperty] private ImageItem? _imageAny;
        [ObservableProperty] private ImageItem? _imageInstax;

        [ObservableProperty] private bool _isBarongVisible;
        [ObservableProperty] private bool _isCreativeVisible;
        [ObservableProperty] private bool _isAnyVisible;
        [ObservableProperty] private bool _isInstaxVisible;

        [ObservableProperty] private bool _isClearConfirmationVisible;
        [ObservableProperty] private bool _isSubmitConfirmationVisible;
        [ObservableProperty] private bool _isErrorDialogVisible;
        [ObservableProperty] private bool _isSettingsDialogVisible;
        [ObservableProperty] private bool _isThankYouDialogVisible;
        [ObservableProperty] private bool _isAboutDialogVisible;
        [ObservableProperty] private bool _isPreviewPackageDialogVisible;
        [ObservableProperty] private bool _isHelpDialogVisible;
        [ObservableProperty] private bool _isImportantNotesDialogVisible;
        [ObservableProperty] private bool _isAcknowledgementDialogVisible;
        [ObservableProperty] private bool _isImportantNotesChecked;
        [ObservableProperty] private bool _isLoadingSubmit;
        [ObservableProperty] private string _errorMessage = "Please check your inputs.";
        [ObservableProperty] private bool _hasNoImages = true;
        [ObservableProperty] private bool _isLoadingImages;

        public ObservableCollection<ImageItem> Images { get; } = new();

        // --- SKIASHARP HELPER ---
        private Bitmap LoadBitmapWithOrientation(string path, int? targetWidth)
        {
            try
            {
                using var stream = File.OpenRead(path);
                using var codec = SKCodec.Create(stream);
                if (codec == null) return new Bitmap(path);

                var orientation = codec.EncodedOrigin;

                // Optimization: If orientation is correct and targetWidth exists, use Avalonia fast path
                if (orientation == SKEncodedOrigin.TopLeft && targetWidth.HasValue)
                {
                    stream.Position = 0;
                    return Bitmap.DecodeToWidth(stream, targetWidth.Value);
                }

                // Slow path: Use Skia to fix rotation
                using var bitmap = SKBitmap.Decode(codec);
                if (bitmap == null) return new Bitmap(path);

                SKBitmap finalBitmap = bitmap;
                bool needsDispose = false;

                if (orientation != SKEncodedOrigin.TopLeft)
                {
                    finalBitmap = RotateBitmap(bitmap, orientation);
                    needsDispose = true;
                }

                if (targetWidth.HasValue && finalBitmap.Width > targetWidth.Value)
                {
                    int height = (int)((double)targetWidth.Value / finalBitmap.Width * finalBitmap.Height);
                    var info = new SKImageInfo(targetWidth.Value, height);
                    var resized = finalBitmap.Resize(info, SKFilterQuality.Medium);
                    
                    if (needsDispose && finalBitmap != bitmap) finalBitmap.Dispose();
                    finalBitmap = resized;
                    needsDispose = true;
                }

                using var image = SKImage.FromBitmap(finalBitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var ms = new MemoryStream();
                data.SaveTo(ms);
                ms.Seek(0, SeekOrigin.Begin);
                
                if (needsDispose) finalBitmap.Dispose();
                return new Bitmap(ms);
            }
            catch
            {
                return new Bitmap(path); // Fallback
            }
        }

        private SKBitmap RotateBitmap(SKBitmap bitmap, SKEncodedOrigin orientation)
        {
            SKBitmap rotated;
            switch (orientation)
            {
                case SKEncodedOrigin.BottomRight: 
                    rotated = new SKBitmap(bitmap.Width, bitmap.Height);
                    using (var canvas = new SKCanvas(rotated)) {
                        canvas.RotateDegrees(180, bitmap.Width / 2, bitmap.Height / 2);
                        canvas.DrawBitmap(bitmap, 0, 0);
                    }
                    break;
                case SKEncodedOrigin.RightTop: 
                    rotated = new SKBitmap(bitmap.Height, bitmap.Width);
                    using (var canvas = new SKCanvas(rotated)) {
                        canvas.Translate(rotated.Width, 0);
                        canvas.RotateDegrees(90);
                        canvas.DrawBitmap(bitmap, 0, 0);
                    }
                    break;
                case SKEncodedOrigin.LeftBottom: 
                    rotated = new SKBitmap(bitmap.Height, bitmap.Width);
                    using (var canvas = new SKCanvas(rotated)) {
                        canvas.Translate(0, rotated.Height);
                        canvas.RotateDegrees(270);
                        canvas.DrawBitmap(bitmap, 0, 0);
                    }
                    break;
                default: return bitmap;
            }
            return rotated;
        }

        // --- LOADING METHODS ---
        public async Task LoadImages(string folderPath)
        {
            Images.Clear();
            if (!Directory.Exists(folderPath)) return;

            _currentBrowseFolderPath = folderPath;
            SaveSettings();

            IsLoadingImages = true;
            HasNoImages = false;

            try
            {
                var supportedExtensions = new[] { ".jpg", ".jpeg", ".png" };
                var files = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly)
                                     .Where(s => supportedExtensions.Contains(Path.GetExtension(s).ToLower()))
                                     .ToList();

                if (files.Count == 0) { HasNoImages = true; IsLoadingImages = false; return; }

                await Task.Run(() =>
                {
                    var chunks = files.Chunk(20); 
                    foreach (var chunk in chunks)
                    {
                        var batch = new System.Collections.Concurrent.ConcurrentBag<ImageItem>();
                        Parallel.ForEach(chunk, file =>
                        {
                            try
                            {
                                // 100px Thumbnail + Rotation Fix
                                var bmp = LoadBitmapWithOrientation(file, 100);
                                batch.Add(new ImageItem { Bitmap = bmp, FileName = Path.GetFileName(file), FullPath = file });
                            }
                            catch { }
                        });

                        if (!batch.IsEmpty)
                        {
                            var sorted = batch.OrderBy(x => x.FileName).ToList();
                            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                            {
                                foreach (var item in sorted) Images.Add(item);
                            }, Avalonia.Threading.DispatcherPriority.Background);
                        }
                    }
                });
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error loading images: {ex.Message}"); }
            finally { IsLoadingImages = false; HasNoImages = Images.Count == 0; }
        }

        private async Task UpdatePreviewAsync(ImageItem? thumbnailItem)
        {
            if (thumbnailItem == null) { PreviewImage = null; IsLoadingPreview = false; return; }
            
            IsLoadingPreview = true;
            PreviewImage = null;

            try
            {
                var highResItem = await Task.Run(() =>
                {
                    try
                    {
                        // Full Size + Rotation Fix
                        var bitmap = LoadBitmapWithOrientation(thumbnailItem.FullPath, null);
                        return new ImageItem { Bitmap = bitmap, FileName = thumbnailItem.FileName, FullPath = thumbnailItem.FullPath };
                    }
                    catch { return null; }
                });
                PreviewImage = highResItem;
            }
            catch { PreviewImage = thumbnailItem; }
            finally { IsLoadingPreview = false; }
        }

        // --- SLOT MANAGEMENT ---
        public void SetPackageImage(string category, ImageItem sourceItem)
        {
            ClearSlot(category); // Dispose old first

            ImageItem newSlotItem = sourceItem;
            try
            {
                // 300px Medium + Rotation Fix
                var mediumBitmap = LoadBitmapWithOrientation(sourceItem.FullPath, 300);
                newSlotItem = new ImageItem { FileName = sourceItem.FileName, FullPath = sourceItem.FullPath, Bitmap = mediumBitmap };
            }
            catch { }

            switch (category)
            {
                case "8x10": Image8x10 = newSlotItem; break;
                case "Barong": ImageBarong = newSlotItem; break;
                case "Creative": ImageCreative = newSlotItem; break;
                case "Any": ImageAny = newSlotItem; break;
                case "Instax": ImageInstax = newSlotItem; break;
            }
        }

        // --- PREVIEW MODAL ---
        [RelayCommand]
        public async Task OpenPreviewPackage()
        {
            IsPreviewPackageDialogVisible = true;
            IsModalLoading = true;
            await Task.Delay(50);
            DisposePreviewImages();

            try
            {
                var results = await Task.Run(() =>
                {
                    ImageItem? Load(ImageItem? src) 
                    {
                         if (src == null) return null;
                         try {
                             var bmp = LoadBitmapWithOrientation(src.FullPath, null);
                             return new ImageItem { Bitmap = bmp, FileName = src.FileName, FullPath = src.FullPath };
                         } catch { return null; }
                    }

                    return new Dictionary<string, ImageItem?>
                    {
                        { "8x10", Load(Image8x10) },
                        { "Barong", IsBarongVisible ? Load(ImageBarong) : null },
                        { "Creative", IsCreativeVisible ? Load(ImageCreative) : null },
                        { "Any", IsAnyVisible ? Load(ImageAny) : null },
                        { "Instax", IsInstaxVisible ? Load(ImageInstax) : null }
                    };
                });

                PreviewImage8x10 = results["8x10"];
                PreviewImageBarong = results["Barong"];
                PreviewImageCreative = results["Creative"];
                PreviewImageAny = results["Any"];
                PreviewImageInstax = results["Instax"];
            }
            catch { IsPreviewPackageDialogVisible = false; }
            finally { IsModalLoading = false; }
        }

        private void DisposePreviewImages()
        {
            PreviewImage8x10?.Bitmap?.Dispose(); PreviewImage8x10 = null;
            PreviewImageBarong?.Bitmap?.Dispose(); PreviewImageBarong = null;
            PreviewImageCreative?.Bitmap?.Dispose(); PreviewImageCreative = null;
            PreviewImageAny?.Bitmap?.Dispose(); PreviewImageAny = null;
            PreviewImageInstax?.Bitmap?.Dispose(); PreviewImageInstax = null;
            GC.Collect();
        }

        [RelayCommand]
        public void ClosePreviewPackage()
        {
            IsPreviewPackageDialogVisible = false;
            DisposePreviewImages();
        }

        // --- SETTINGS & DATA ---
        private bool LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    var json = File.ReadAllText(_settingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                    {
                        if (!string.IsNullOrEmpty(settings.LastOutputFolder)) OutputFolderPath = settings.LastOutputFolder;
                        if (!string.IsNullOrEmpty(settings.LastExcelFolder)) ExcelFolderPath = settings.LastExcelFolder;
                        else ExcelFolderPath = OutputFolderPath;
                        if (!string.IsNullOrEmpty(settings.LastExcelFileName)) ExcelFileName = settings.LastExcelFileName;
                        if (!string.IsNullOrEmpty(settings.LastBrowseFolder)) _currentBrowseFolderPath = settings.LastBrowseFolder;
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private void SaveSettings()
        {
            try
            {
                string? dir = Path.GetDirectoryName(_settingsFilePath);
                if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var settings = new AppSettings { LastOutputFolder = OutputFolderPath, LastExcelFolder = ExcelFolderPath, LastExcelFileName = ExcelFileName, LastBrowseFolder = _currentBrowseFolderPath };
                var json = JsonSerializer.Serialize(settings);
                File.WriteAllText(_settingsFilePath, json);
            }
            catch { }
        }
        
        public class AppSettings 
        { 
            public string? LastOutputFolder { get; set; } 
            public string? LastExcelFolder { get; set; } 
            public string? LastExcelFileName { get; set; } 
            public string? LastBrowseFolder { get; set; } 
        }

        // --- DIALOGS & SUBMIT ---
        [RelayCommand] public void ClearSlot(string category) 
        { 
            switch (category) { case "8x10": Image8x10?.Bitmap?.Dispose(); Image8x10 = null; break; case "Barong": ImageBarong?.Bitmap?.Dispose(); ImageBarong = null; break; case "Creative": ImageCreative?.Bitmap?.Dispose(); ImageCreative = null; break; case "Any": ImageAny?.Bitmap?.Dispose(); ImageAny = null; break; case "Instax": ImageInstax?.Bitmap?.Dispose(); ImageInstax = null; break; } 
            GC.Collect();
        }
        
        [RelayCommand] public void UpdatePackage(string packageName) { SelectedPackage = packageName; UpdateVisibility(packageName); }
        
        private void UpdateVisibility(string pkg)
        {
            IsBarongVisible = false; IsCreativeVisible = false; IsAnyVisible = false; IsInstaxVisible = false;
            if (pkg == "A" || pkg == "B") { IsBarongVisible = true; }
            else if (pkg == "C") { IsBarongVisible = true; IsCreativeVisible = true; IsAnyVisible = true; }
            else if (pkg == "D") { IsBarongVisible = true; IsCreativeVisible = true; IsAnyVisible = true; IsInstaxVisible = true; }
        }

        [RelayCommand] public void ClearAll() { IsClearConfirmationVisible = true; }
        [RelayCommand] public void ConfirmClear() { ResetData(); IsClearConfirmationVisible = false; }
        [RelayCommand] public void CancelClear() { IsClearConfirmationVisible = false; }
        [RelayCommand] public void OpenSettings() { _snapOutputFolder = OutputFolderPath; _snapExcelFolder = ExcelFolderPath; _snapExcelFileName = ExcelFileName; IsSettingsDirty = false; IsSettingsDialogVisible = true; }
        [RelayCommand] public void CancelSettings() { OutputFolderPath = _snapOutputFolder; ExcelFolderPath = _snapExcelFolder; ExcelFileName = _snapExcelFileName; IsSettingsDialogVisible = false; }
        [RelayCommand] public void SaveAndCloseSettings() { SaveSettings(); IsSettingsDialogVisible = false; }
        [RelayCommand] public void OpenAbout() { IsAboutDialogVisible = true; }
        [RelayCommand] public void CloseAbout() { IsAboutDialogVisible = false; }
        [RelayCommand] public void OpenHelp() { IsHelpDialogVisible = true; }
        [RelayCommand] public void CloseHelp() { IsHelpDialogVisible = false; }
        
        [RelayCommand] public void Submit() 
        { 
            if (string.IsNullOrWhiteSpace(ClientName) || string.IsNullOrWhiteSpace(ClientEmail)) { ErrorMessage = "Please enter both the Client Name and Email Address."; IsErrorDialogVisible = true; return; } 
            if (!IsValidEmail(ClientEmail)) { ErrorMessage = "The Email Address format is invalid.\n(e.g., user@example.com)"; IsErrorDialogVisible = true; return; } 
            bool isMissing = false; 
            if (Image8x10 == null) isMissing = true; 
            else if (IsBarongVisible && ImageBarong == null) isMissing = true; 
            else if (IsCreativeVisible && ImageCreative == null) isMissing = true; 
            else if (IsAnyVisible && ImageAny == null) isMissing = true; 
            else if (IsInstaxVisible && ImageInstax == null) isMissing = true; 
            if (isMissing) { ErrorMessage = $"Your selected package ({SelectedPackage}) requires all photo slots to be filled."; IsErrorDialogVisible = true; return; } 
            IsSubmitConfirmationVisible = true; 
        }
        [RelayCommand] public void ConfirmSubmit() { IsSubmitConfirmationVisible = false; IsImportantNotesDialogVisible = true; }
        [RelayCommand] public void ContinueFromNotes() { IsImportantNotesDialogVisible = false; IsImportantNotesChecked = false; IsAcknowledgementDialogVisible = true; }
        [RelayCommand] public void CancelNotes() { IsImportantNotesDialogVisible = false; }
        [RelayCommand] public void CancelAcknowledgement() { IsAcknowledgementDialogVisible = false; }
        [RelayCommand] public void CloseThankYouDialog() { IsThankYouDialogVisible = false; ResetData(); }
        [RelayCommand] public void CloseSettings() { CancelSettings(); }
        [RelayCommand] public void CancelSubmit() { IsSubmitConfirmationVisible = false; }
        [RelayCommand] public void CloseErrorDialog() { IsErrorDialogVisible = false; }

        [RelayCommand]
        public async Task ProceedFromAcknowledgement()
        {
            IsAcknowledgementDialogVisible = false;
            IsLoadingSubmit = true;
            await Task.Delay(50);
            try
            {
                await Task.Run(() =>
                {
                    if (!Directory.Exists(OutputFolderPath)) Directory.CreateDirectory(OutputFolderPath);
                    string safeClientName = (ClientName ?? "Unknown").ToUpper();
                    foreach (char c in Path.GetInvalidFileNameChars()) safeClientName = safeClientName.Replace(c, '_');
                    string specificFolder = Path.Combine(OutputFolderPath, $"{SelectedPackage}-{safeClientName}");
                    if (!Directory.Exists(specificFolder)) Directory.CreateDirectory(specificFolder);

                    SaveImageToFile(Image8x10, " 8x10 ", specificFolder);
                    if (IsBarongVisible) SaveImageToFile(ImageBarong, " Barong ", specificFolder);
                    if (IsCreativeVisible) SaveImageToFile(ImageCreative, " Creative ", specificFolder);
                    if (IsAnyVisible) SaveImageToFile(ImageAny, " Any ", specificFolder);
                    if (IsInstaxVisible) SaveImageToFile(ImageInstax, " Instax ", specificFolder);

                    string excelPath = Path.Combine(ExcelFolderPath, ExcelFileName + ".xlsx");
                    string? excelDir = Path.GetDirectoryName(excelPath);
                    if (!string.IsNullOrEmpty(excelDir) && !Directory.Exists(excelDir)) Directory.CreateDirectory(excelDir);

                    var newItem = new OrderLogItem { Status = "DONE CHOOSING", Name = ClientName?.ToUpper(), Email = ClientEmail, Package = SelectedPackage, 
                        Box_8x10 = Image8x10?.FileName ?? "Empty", 
                        Box_Barong = IsBarongVisible ? ImageBarong?.FileName ?? "Empty" : "N/A",
                        Box_Creative = IsCreativeVisible ? ImageCreative?.FileName ?? "Empty" : "N/A",
                        Box_Any = IsAnyVisible ? ImageAny?.FileName ?? "Empty" : "N/A",
                        Box_Instax = IsInstaxVisible ? ImageInstax?.FileName ?? "Empty" : "N/A",
                        TimeStamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") };

                    var allRows = new List<OrderLogItem>();
                    if (File.Exists(excelPath)) try { allRows.AddRange(MiniExcel.Query<OrderLogItem>(excelPath)); } catch { }
                    allRows.Add(newItem);
                    if (File.Exists(excelPath)) File.Delete(excelPath); MiniExcel.SaveAs(excelPath, allRows);
                });

                ResetData();
                ClearBrowserImages();
                IsLoadingSubmit = false;
                IsThankYouDialogVisible = true;
                await LoadImages(_currentBrowseFolderPath);
            }
            catch (Exception ex) { IsLoadingSubmit = false; ErrorMessage = $"Saving Error: {ex.Message}"; IsErrorDialogVisible = true; }
        }

        private void SaveImageToFile(ImageItem? image, string cat, string folder)
        {
            if (image == null) return;
            string ext = Path.GetExtension(image.FileName);
            string newName = $"({SelectedPackage}) {(ClientName ?? "Unknown").ToUpper()}{cat}{Path.GetFileNameWithoutExtension(image.FileName)}{ext}";
            foreach (char c in Path.GetInvalidFileNameChars()) newName = newName.Replace(c, '_');
            File.Copy(image.FullPath, Path.Combine(folder, newName), true);
        }

        private void ClearBrowserImages() { foreach(var i in Images) i.Bitmap?.Dispose(); Images.Clear(); GC.Collect(); HasNoImages = true; }
        
        private void ResetData() 
        { 
            ClientName = string.Empty; 
            ClientEmail = string.Empty; 
            SelectedPackage = "Basic"; 
            IsBasicSelected = true; 
            IsPkgASelected = false; 
            IsPkgBSelected = false; 
            IsPkgCSelected = false; 
            IsPkgDSelected = false; 
            SelectedImage = null; 
            PreviewImage = null; 
            Image8x10 = null; 
            ImageBarong = null; 
            ImageCreative = null; 
            ImageAny = null; 
            ImageInstax = null; 
            UpdateVisibility("Basic"); 
        }
        
        private bool IsValidEmail(string email) { try { return Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase); } catch { return false; } }
    }
}