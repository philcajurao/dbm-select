using Avalonia.Controls;
using Avalonia.Layout; // Required for Orientation
using Avalonia.Media; // Required for Stretch
using Avalonia.Media.Imaging;
using Avalonia.Controls.Primitives; // Required for ScrollBarVisibility
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using dbm_select.Models;
using MiniExcelLibs;
using SkiaSharp; // Required for Image Manipulation
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading; // Required for Cancellation
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

        // Layout Control Properties
        [ObservableProperty] private bool _isSingleLargeLayout; // Basic (320x480)
        [ObservableProperty] private bool _isDoubleLargeLayout; // A & B (210x270)
        [ObservableProperty] private bool _isQuadLayout;        // C (155x210)
        [ObservableProperty] private bool _isFiveLayout;        // D (155x210)

        [ObservableProperty] private Orientation _slotsOrientation = Orientation.Vertical;

        // Responsive Scaling Controls
        [ObservableProperty] private Stretch _layoutStretch = Stretch.None;
        [ObservableProperty] private ScrollBarVisibility _scrollVisibility = ScrollBarVisibility.Auto;

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
        // Optimized & Robust: Handles Orientation, Subsampling, and Fallbacks
        private Bitmap? LoadBitmapWithOrientation(string path, int? targetWidth)
        {
            FileStream? stream = null;
            try
            {
                // 1. Open Stream (ReadWrite share to prevent locking issues)
                stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                using var codec = SKCodec.Create(stream);

                // If Skia fails to read the header, or codec is null -> Fallback
                if (codec == null) throw new Exception("Skia Codec failed");

                var orientation = codec.EncodedOrigin;

                // 2. Calculate Dimensions
                SKImageInfo info = codec.Info;
                SKSizeI supportedDimensions = info.Size;

                if (targetWidth.HasValue && info.Width > targetWidth.Value)
                {
                    float scale = (float)targetWidth.Value / info.Width;
                    supportedDimensions = codec.GetScaledDimensions(scale);
                }

                // 3. Decode
                var bitmapInfo = new SKImageInfo(supportedDimensions.Width, supportedDimensions.Height, SKColorType.Bgra8888);
                using var bitmap = new SKBitmap(bitmapInfo);

                var result = codec.GetPixels(bitmapInfo, bitmap.GetPixels());

                if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput)
                {
                    throw new Exception("Skia GetPixels failed");
                }

                SKBitmap finalBitmap = bitmap;
                bool needsDispose = false;

                // 4. Handle Rotation
                if (orientation != SKEncodedOrigin.TopLeft)
                {
                    finalBitmap = RotateBitmap(bitmap, orientation);
                    needsDispose = true;
                }

                // 5. Precise Resize (if needed)
                if (targetWidth.HasValue && finalBitmap.Width > targetWidth.Value)
                {
                    int height = (int)((double)targetWidth.Value / finalBitmap.Width * finalBitmap.Height);
                    var resizeInfo = new SKImageInfo(targetWidth.Value, height, SKColorType.Bgra8888);
                    var resized = finalBitmap.Resize(resizeInfo, SKFilterQuality.Medium);

                    if (resized != finalBitmap)
                    {
                        if (needsDispose) finalBitmap.Dispose();
                        finalBitmap = resized;
                        needsDispose = true;
                    }
                }

                // 6. Copy to Avalonia
                var pixelSize = new Avalonia.PixelSize(finalBitmap.Width, finalBitmap.Height);
                var vector = new Avalonia.Vector(96, 96);

                var writeableBitmap = new Avalonia.Media.Imaging.WriteableBitmap(
                    pixelSize,
                    vector,
                    Avalonia.Platform.PixelFormat.Bgra8888,
                    Avalonia.Platform.AlphaFormat.Premul);

                using (var buffer = writeableBitmap.Lock())
                {
                    var dstInfo = new SKImageInfo(finalBitmap.Width, finalBitmap.Height, SKColorType.Bgra8888, SKAlphaType.Premul);

                    using (var surface = SKSurface.Create(dstInfo, buffer.Address, buffer.RowBytes))
                    {
                        if (surface != null)
                        {
                            surface.Canvas.DrawBitmap(finalBitmap, 0, 0);
                        }
                        else
                        {
                            throw new Exception("SKSurface creation failed");
                        }
                    }
                }

                if (needsDispose) finalBitmap.Dispose();
                return writeableBitmap;
            }
            catch (Exception)
            {
                // Robust Fallback:
                // If smart loading fails, close stream and let Avalonia standard load try.
                stream?.Dispose();
                stream = null;

                try
                {
                    return new Bitmap(path);
                }
                catch
                {
                    return null; // File is truly corrupted/unreadable
                }
            }
            finally
            {
                stream?.Dispose();
            }
        }

        private SKBitmap RotateBitmap(SKBitmap bitmap, SKEncodedOrigin orientation)
        {
            SKBitmap rotated;
            switch (orientation)
            {
                case SKEncodedOrigin.BottomRight:
                    rotated = new SKBitmap(bitmap.Width, bitmap.Height, bitmap.ColorType, bitmap.AlphaType);
                    using (var canvas = new SKCanvas(rotated))
                    {
                        canvas.RotateDegrees(180, bitmap.Width / 2, bitmap.Height / 2);
                        canvas.DrawBitmap(bitmap, 0, 0);
                    }
                    break;
                case SKEncodedOrigin.RightTop:
                    rotated = new SKBitmap(bitmap.Height, bitmap.Width, bitmap.ColorType, bitmap.AlphaType);
                    using (var canvas = new SKCanvas(rotated))
                    {
                        canvas.Translate(rotated.Width, 0);
                        canvas.RotateDegrees(90);
                        canvas.DrawBitmap(bitmap, 0, 0);
                    }
                    break;
                case SKEncodedOrigin.LeftBottom:
                    rotated = new SKBitmap(bitmap.Height, bitmap.Width, bitmap.ColorType, bitmap.AlphaType);
                    using (var canvas = new SKCanvas(rotated))
                    {
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
        private CancellationTokenSource? _loadImagesCts;

        public async Task LoadImages(string folderPath)
        {
            _loadImagesCts?.Cancel();
            _loadImagesCts = new CancellationTokenSource();
            var token = _loadImagesCts.Token;

            Images.Clear();
            if (!Directory.Exists(folderPath)) return;

            _currentBrowseFolderPath = folderPath;
            SaveSettings();

            IsLoadingImages = true;
            HasNoImages = false;

            try
            {
                var supportedExtensions = new[] { ".jpg", ".jpeg", ".png" };

                // 1. FAST SCAN
                var files = await Task.Run(() =>
                    Directory.EnumerateFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly)
                             .Where(s => supportedExtensions.Contains(Path.GetExtension(s).ToLower()))
                             .OrderBy(f => f)
                             .ToList(), token);

                if (files.Count == 0)
                {
                    HasNoImages = true;
                    IsLoadingImages = false;
                    return;
                }

                IsLoadingImages = false; // Hide spinner immediately

                // 3. BACKGROUND FILL (Sequential Processing Fix)
                await Task.Run(() =>
                {
                    int processedCount = 0;
                    foreach (var file in files)
                    {
                        if (token.IsCancellationRequested) break;

                        try
                        {
                            // Load 100px Thumbnail (Robust method)
                            var bmp = LoadBitmapWithOrientation(file, 100);

                            if (bmp != null)
                            {
                                var item = new ImageItem
                                {
                                    FileName = Path.GetFileName(file) ?? "Unknown",
                                    FullPath = file,
                                    Bitmap = bmp
                                };

                                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                                {
                                    Images.Add(item);
                                }, Avalonia.Threading.DispatcherPriority.Background);
                            }

                            // Aggressive GC
                            processedCount++;
                            if (processedCount % 10 == 0)
                            {
                                GC.Collect();
                            }
                        }
                        catch { }
                    }
                    GC.Collect();
                }, token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}"); }
            finally
            {
                if (!token.IsCancellationRequested && Images.Count == 0) HasNoImages = true;
                if (IsLoadingImages) IsLoadingImages = false;
            }
        }

        // Updated: Seamless Transition (No Clearing Old Image)
        private async Task UpdatePreviewAsync(ImageItem? thumbnailItem)
        {
            if (thumbnailItem == null)
            {
                PreviewImage = null;
                return;
            }

            try
            {
                var highResItem = await Task.Run(() =>
                {
                    try
                    {
                        // Load Full Size
                        var bitmap = LoadBitmapWithOrientation(thumbnailItem.FullPath, null);
                        if (bitmap == null) return null;

                        return new ImageItem
                        {
                            Bitmap = bitmap,
                            FileName = thumbnailItem.FileName ?? string.Empty,
                            FullPath = thumbnailItem.FullPath ?? string.Empty
                        };
                    }
                    catch { return null; }
                });

                // Swap to new image only when ready
                if (highResItem != null)
                {
                    PreviewImage = highResItem;
                }
                else
                {
                    // Fallback to thumbnail to avoid blank space
                    PreviewImage = thumbnailItem;
                }
            }
            catch { }
        }

        // --- SLOT MANAGEMENT ---
        public void SetPackageImage(string category, ImageItem sourceItem)
        {
            ClearSlot(category);
            ImageItem newSlotItem = sourceItem;
            try
            {
                // Load 300px Medium Quality for slots
                var mediumBitmap = LoadBitmapWithOrientation(sourceItem.FullPath, 300);
                if (mediumBitmap != null)
                {
                    newSlotItem = new ImageItem { FileName = sourceItem.FileName, FullPath = sourceItem.FullPath, Bitmap = mediumBitmap };
                }
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
            DisposePreviewImages();

            try
            {
                var results = await Task.Run(() =>
                {
                    ImageItem? Load(ImageItem? src)
                    {
                        if (src == null) return null;
                        var bmp = LoadBitmapWithOrientation(src.FullPath, null);
                        if (bmp == null) return null;

                        return new ImageItem
                        {
                            Bitmap = bmp,
                            FileName = src.FileName ?? string.Empty,
                            FullPath = src.FullPath ?? string.Empty
                        };
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

        [RelayCommand] public void ClosePreviewPackage() { IsPreviewPackageDialogVisible = false; DisposePreviewImages(); }
        [RelayCommand] public void OpenHelp() { IsHelpDialogVisible = true; }
        [RelayCommand] public void CloseHelp() { IsHelpDialogVisible = false; }

        [RelayCommand]
        public void Submit()
        {
            if (string.IsNullOrWhiteSpace(ClientName) || string.IsNullOrWhiteSpace(ClientEmail))
            {
                ErrorMessage = "Please enter both the Client Name and Email Address.";
                IsErrorDialogVisible = true;
                return;
            }
            if (!IsValidEmail(ClientEmail))
            {
                ErrorMessage = "The Email Address format is invalid.\n(e.g., user@example.com)";
                IsErrorDialogVisible = true;
                return;
            }

            bool isMissing = false;
            if (Image8x10 == null) isMissing = true;
            else if (IsBarongVisible && ImageBarong == null) isMissing = true;
            else if (IsCreativeVisible && ImageCreative == null) isMissing = true;
            else if (IsAnyVisible && ImageAny == null) isMissing = true;
            else if (IsInstaxVisible && ImageInstax == null) isMissing = true;

            if (isMissing)
            {
                ErrorMessage = $"Your selected package ({SelectedPackage}) requires all photo slots to be filled.";
                IsErrorDialogVisible = true;
                return;
            }
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
        [RelayCommand] public void UpdatePackage(string packageName) { SelectedPackage = packageName; UpdateVisibility(packageName); }
        [RelayCommand] public void ClearAll() { IsClearConfirmationVisible = true; }
        [RelayCommand] public void ConfirmClear() { ResetData(); IsClearConfirmationVisible = false; }
        [RelayCommand] public void CancelClear() { IsClearConfirmationVisible = false; }

        [RelayCommand]
        public void OpenSettings()
        {
            _snapOutputFolder = OutputFolderPath;
            _snapExcelFolder = ExcelFolderPath;
            _snapExcelFileName = ExcelFileName;
            IsSettingsDirty = false;
            IsSettingsDialogVisible = true;
        }

        [RelayCommand]
        public void CancelSettings()
        {
            OutputFolderPath = _snapOutputFolder;
            ExcelFolderPath = _snapExcelFolder;
            ExcelFileName = _snapExcelFileName;
            IsSettingsDialogVisible = false;
        }

        [RelayCommand] public void SaveAndCloseSettings() { SaveSettings(); IsSettingsDialogVisible = false; }
        [RelayCommand] public void OpenAbout() { IsAboutDialogVisible = true; }
        [RelayCommand] public void CloseAbout() { IsAboutDialogVisible = false; }

        [RelayCommand]
        public void ClearSlot(string category)
        {
            switch (category)
            {
                case "8x10": Image8x10?.Bitmap?.Dispose(); Image8x10 = null; break;
                case "Barong": ImageBarong?.Bitmap?.Dispose(); ImageBarong = null; break;
                case "Creative": ImageCreative?.Bitmap?.Dispose(); ImageCreative = null; break;
                case "Any": ImageAny?.Bitmap?.Dispose(); ImageAny = null; break;
                case "Instax": ImageInstax?.Bitmap?.Dispose(); ImageInstax = null; break;
            }
            GC.Collect();
        }

        [RelayCommand]
        public async Task ProceedFromAcknowledgement()
        {
            IsAcknowledgementDialogVisible = false;
            IsLoadingSubmit = true;
            await Task.Delay(3000);
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

                    var newItem = new OrderLogItem
                    {
                        Status = "DONE CHOOSING",
                        Name = ClientName?.ToUpper() ?? "UNKNOWN",
                        Email = ClientEmail ?? string.Empty,
                        Package = SelectedPackage,
                        Box_8x10 = Image8x10?.FileName ?? "Empty",
                        Box_Barong = IsBarongVisible ? ImageBarong?.FileName ?? "Empty" : "N/A",
                        Box_Creative = IsCreativeVisible ? ImageCreative?.FileName ?? "Empty" : "N/A",
                        Box_Any = IsAnyVisible ? ImageAny?.FileName ?? "Empty" : "N/A",
                        Box_Instax = IsInstaxVisible ? ImageInstax?.FileName ?? "Empty" : "N/A",
                        TimeStamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    };

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

        private bool LoadSettings()
        {
            try { if (File.Exists(_settingsFilePath)) { var json = File.ReadAllText(_settingsFilePath); var settings = JsonSerializer.Deserialize<AppSettings>(json); if (settings != null) { if (!string.IsNullOrEmpty(settings.LastOutputFolder)) OutputFolderPath = settings.LastOutputFolder; if (!string.IsNullOrEmpty(settings.LastExcelFolder)) ExcelFolderPath = settings.LastExcelFolder; else ExcelFolderPath = OutputFolderPath; if (!string.IsNullOrEmpty(settings.LastExcelFileName)) ExcelFileName = settings.LastExcelFileName; if (!string.IsNullOrEmpty(settings.LastBrowseFolder)) _currentBrowseFolderPath = settings.LastBrowseFolder; return true; } } } catch { }
            return false;
        }
        private void SaveSettings()
        {
            try { string? dir = Path.GetDirectoryName(_settingsFilePath); if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir); var settings = new AppSettings { LastOutputFolder = OutputFolderPath, LastExcelFolder = ExcelFolderPath, LastExcelFileName = ExcelFileName, LastBrowseFolder = _currentBrowseFolderPath }; File.WriteAllText(_settingsFilePath, JsonSerializer.Serialize(settings)); } catch { }
        }
        public class AppSettings { public string? LastOutputFolder { get; set; } public string? LastExcelFolder { get; set; } public string? LastExcelFileName { get; set; } public string? LastBrowseFolder { get; set; } }

        private void UpdateVisibility(string pkg)
        {
            IsBarongVisible = false;
            IsCreativeVisible = false;
            IsAnyVisible = false;
            IsInstaxVisible = false;

            // Default: Scrollable, standard size
            IsSingleLargeLayout = false;
            IsDoubleLargeLayout = false;
            IsQuadLayout = false;
            IsFiveLayout = false;
            LayoutStretch = Stretch.None;
            ScrollVisibility = ScrollBarVisibility.Auto;

            if (pkg == "Basic")
            {
                IsSingleLargeLayout = true;
                SlotsOrientation = Orientation.Vertical;

                // Responsive Scaling On
                LayoutStretch = Stretch.Uniform;
                ScrollVisibility = ScrollBarVisibility.Disabled;
            }
            else if (pkg == "A" || pkg == "B")
            {
                IsBarongVisible = true;
                IsDoubleLargeLayout = true;
                SlotsOrientation = Orientation.Vertical;

                // Responsive Scaling On
                LayoutStretch = Stretch.Uniform;
                ScrollVisibility = ScrollBarVisibility.Disabled;
            }
            else if (pkg == "C")
            {
                IsBarongVisible = true;
                IsCreativeVisible = true;
                IsAnyVisible = true;

                // Quad Layout: Scaled Up, Responsive
                IsQuadLayout = true;
                SlotsOrientation = Orientation.Horizontal;

                LayoutStretch = Stretch.Uniform;
                ScrollVisibility = ScrollBarVisibility.Disabled;
            }
            else if (pkg == "D")
            {
                IsBarongVisible = true;
                IsCreativeVisible = true;
                IsAnyVisible = true;
                IsInstaxVisible = true;

                // Five Layout: Scaled Up, Responsive
                IsFiveLayout = true;
                SlotsOrientation = Orientation.Horizontal;

                LayoutStretch = Stretch.Uniform;
                ScrollVisibility = ScrollBarVisibility.Disabled;
            }
        }

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
        private void ClearBrowserImages() { foreach (var i in Images) i.Bitmap?.Dispose(); Images.Clear(); GC.Collect(); HasNoImages = true; }
    }
}