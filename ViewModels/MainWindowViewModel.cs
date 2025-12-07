using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using dbm_select.Models;
using MiniExcelLibs;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace dbm_select.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private readonly string _settingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DBM_Select",
            "settings.json");

        // --- CACHING VARIABLES ---
        private const int MaxCacheSize = 20;
        private readonly Dictionary<string, ImageItem> _highResCache = new();
        private readonly List<string> _cacheOrder = new();
        private readonly object _cacheLock = new();
        private ImageItem? _lastHighResPreview; 

        public MainWindowViewModel()
        {
            if (Design.IsDesignMode) return;

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

            string folderToLoad = !string.IsNullOrEmpty(_currentBrowseFolderPath) && Directory.Exists(_currentBrowseFolderPath)
                ? _currentBrowseFolderPath
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));

            _ = LoadImages(folderToLoad);

            UpdateVisibility("Basic");
        }

        // --- PROPERTIES ---
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

        private CancellationTokenSource? _previewCts;

        partial void OnSelectedImageChanged(ImageItem? value)
        {
            _previewCts?.Cancel();
            _previewCts = new CancellationTokenSource();
            _ = UpdatePreviewAsync(value, _previewCts.Token);
        }

        [ObservableProperty] private ImageItem? _previewImage;
        [ObservableProperty] private bool _isLoadingPreview;

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

        [ObservableProperty] private bool _isSingleLargeLayout;
        [ObservableProperty] private bool _isDoubleLargeLayout;
        [ObservableProperty] private bool _isQuadLayout;
        [ObservableProperty] private bool _isFiveLayout;

        [ObservableProperty] private Orientation _slotsOrientation = Orientation.Vertical;
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

        // ---------------------------------------------------------
        // IMAGE LOADING LOGIC (Smart Cache-First Strategy)
        // ---------------------------------------------------------
        private CancellationTokenSource? _loadImagesCts;

        public async Task LoadImages(string folderPath)
        {
            _loadImagesCts?.Cancel();
            _loadImagesCts = new CancellationTokenSource();
            var token = _loadImagesCts.Token;

            Images.Clear();
            ClearCache();

            if (!Directory.Exists(folderPath)) return;

            _currentBrowseFolderPath = folderPath;
            SaveSettings();

            IsLoadingImages = true;
            HasNoImages = false;

            await Task.Run(async () =>
            {
                var supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
                    { ".jpg", ".jpeg", ".png" };

                var files = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly)
                                     .Where(s => supportedExtensions.Contains(Path.GetExtension(s)))
                                     .OrderBy(f => f)
                                     .ToList();

                if (files.Count == 0)
                {
                    HasNoImages = true;
                    IsLoadingImages = false;
                    return;
                }

                IsLoadingImages = false;

                // --- PHASE 1: FILL THE GRID ---
                // Strategy: Try loading from Cache (150px) first.
                // If Cache exists -> We show high quality instantly.
                // If Cache missing -> We load tiny 50px placeholder instantly.
                var batch = new List<ImageItem>();
                int counter = 0;

                foreach (var file in files)
                {
                    if (token.IsCancellationRequested) break;

                    try
                    {
                        // 1. Try Cached Version (High Quality)
                        var bmp = LoadBitmapWithOrientation(file, 150, onlyLoadFromCache: true);

                        // 2. If no cache, load Tiny Placeholder (Low Quality)
                        if (bmp == null)
                        {
                            bmp = LoadBitmapWithOrientation(file, 50);
                        }

                        if (bmp != null)
                        {
                            var item = new ImageItem 
                            { 
                                FileName = Path.GetFileName(file) ?? "Unknown", 
                                FullPath = file, 
                                Bitmap = bmp 
                            };
                            batch.Add(item);
                        }
                    }
                    catch { }

                    // Add to UI in batches of 20
                    if (batch.Count >= 20)
                    {
                        var itemsToAdd = batch.ToList();
                        batch.Clear();
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => 
                        {
                            foreach (var i in itemsToAdd) Images.Add(i);
                        }, Avalonia.Threading.DispatcherPriority.Background);
                        await Task.Delay(1);
                    }
                    counter++;
                    if (counter % 100 == 0) GC.Collect();
                }

                // Add remaining items
                if (batch.Count > 0)
                {
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => 
                    {
                        foreach (var i in batch) Images.Add(i);
                    });
                }

                // --- PHASE 2: UPGRADE PLACEHOLDERS ---
                // Only upgrade images that are currently small (width <= 50).
                // If we loaded from cache in Phase 1, they are already 150px, so we skip them.
                
                var itemsToUpgrade = Images.ToList(); // Safety copy
                
                foreach (var item in itemsToUpgrade)
                {
                    if (token.IsCancellationRequested) break;

                    // Skip if already high quality (loaded from cache in Phase 1)
                    if (item.Bitmap != null && item.Bitmap.PixelSize.Width > 100) continue;

                    try 
                    {
                        // Load Standard 150px (This will also save to disk cache for next time)
                        var betterBmp = LoadBitmapWithOrientation(item.FullPath, 150);
                        
                        if (betterBmp != null)
                        {
                            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                item.Bitmap = betterBmp;
                            }, Avalonia.Threading.DispatcherPriority.Background);
                        }
                    }
                    catch { }
                    
                    await Task.Delay(5); 
                }

                GC.Collect();

            }, token);
        }

        // ---------------------------------------------------------
        // PREVIEW LOGIC
        // ---------------------------------------------------------
        private async Task UpdatePreviewAsync(ImageItem? selectedItem, CancellationToken token)
        {
            if (_lastHighResPreview != null)
            {
                bool isInCache = false;
                lock(_cacheLock) { isInCache = _highResCache.ContainsValue(_lastHighResPreview); }
                if (!isInCache) _lastHighResPreview.Bitmap?.Dispose();
                _lastHighResPreview = null;
            }

            if (selectedItem == null)
            {
                PreviewImage = null;
                IsLoadingPreview = false;
                return;
            }

            var cachedItem = GetFromCache(selectedItem.FullPath);
            if (cachedItem != null)
            {
                PreviewImage = cachedItem;
                IsLoadingPreview = false;
                return;
            }

            PreviewImage = new ImageItem 
            { 
                Bitmap = selectedItem.Bitmap, 
                FileName = selectedItem.FileName,
                FullPath = selectedItem.FullPath
            };

            IsLoadingPreview = true;

            try
            {
                var highResItem = await Task.Run(() => 
                    GenerateHighQualityPreview(selectedItem.FullPath, token), token);

                if (token.IsCancellationRequested)
                {
                    highResItem?.Bitmap?.Dispose();
                    return;
                }

                if (highResItem != null)
                {
                    AddToCache(selectedItem.FullPath, highResItem);
                    _lastHighResPreview = highResItem;
                    PreviewImage = highResItem;
                }
            }
            catch { }
            finally
            {
                IsLoadingPreview = false;
            }
        }

        // --- CACHE HELPERS ---
        private void AddToCache(string path, ImageItem item)
        {
            lock (_cacheLock)
            {
                if (_highResCache.ContainsKey(path)) { _cacheOrder.Remove(path); _cacheOrder.Add(path); return; }
                if (_cacheOrder.Count >= MaxCacheSize)
                {
                    string oldest = _cacheOrder[0];
                    _cacheOrder.RemoveAt(0);
                    if (_highResCache.TryGetValue(oldest, out var old)) { if (old != PreviewImage) old.Bitmap?.Dispose(); _highResCache.Remove(oldest); }
                }
                _highResCache[path] = item; _cacheOrder.Add(path);
            }
        }

        private ImageItem? GetFromCache(string path)
        {
            lock (_cacheLock)
            {
                if (_highResCache.TryGetValue(path, out var item)) { _cacheOrder.Remove(path); _cacheOrder.Add(path); return item; }
                return null;
            }
        }

        private void ClearCache()
        {
            lock (_cacheLock) { foreach (var i in _highResCache.Values) i.Bitmap?.Dispose(); _highResCache.Clear(); _cacheOrder.Clear(); }
            GC.Collect();
        }

        // ---------------------------------------------------------
        // BITMAP HELPERS (SkiaSharp Optimized + Disk Cache)
        // ---------------------------------------------------------
        private Bitmap? LoadBitmapWithOrientation(string path, int? targetWidth, bool onlyLoadFromCache = false)
        {
            bool useDiskCache = targetWidth.HasValue && targetWidth.Value >= 100 && targetWidth.Value <= 200;
            string cachePath = "";

            if (useDiskCache)
            {
                try
                {
                    var dir = Path.GetDirectoryName(path);
                    var name = Path.GetFileNameWithoutExtension(path);
                    if (dir != null)
                    {
                        var cacheDir = Path.Combine(dir, ".dbm_thumbs");
                        cachePath = Path.Combine(cacheDir, name + ".dbm");
                        
                        // IF CACHE HIT: Return it immediately
                        if (File.Exists(cachePath)) return new Bitmap(cachePath);
                    }
                }
                catch { }
            }

            // IF ONLY CACHE WAS REQUESTED AND WE MISSED: Return null (don't load raw)
            if (onlyLoadFromCache) return null;

            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var codec = SKCodec.Create(stream);
                if (codec == null) return null;

                SKImageInfo info = codec.Info;
                SKSizeI supportedDimensions = info.Size;

                if (targetWidth.HasValue && info.Width > targetWidth.Value)
                {
                    float scale = (float)targetWidth.Value / info.Width;
                    supportedDimensions = codec.GetScaledDimensions(scale);
                }

                var bitmapInfo = new SKImageInfo(supportedDimensions.Width, supportedDimensions.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
                using var bitmap = new SKBitmap(bitmapInfo);
                
                var result = codec.GetPixels(bitmapInfo, bitmap.GetPixels());
                if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput) return null;

                SKBitmap finalBitmap = bitmap;
                bool needsDispose = false;

                if (codec.EncodedOrigin != SKEncodedOrigin.TopLeft)
                {
                    finalBitmap = RotateBitmap(bitmap, codec.EncodedOrigin);
                    needsDispose = true;
                }

                // SAVE TO DISK CACHE (Only for 150px size)
                if (useDiskCache && !string.IsNullOrEmpty(cachePath))
                {
                    try
                    {
                        var dir = Path.GetDirectoryName(cachePath);
                        if (dir != null && !Directory.Exists(dir))
                        {
                            var di = Directory.CreateDirectory(dir);
                            di.Attributes |= FileAttributes.Hidden;
                        }
                        
                        using var image = SKImage.FromBitmap(finalBitmap);
                        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 80);
                        using var cacheStream = File.OpenWrite(cachePath);
                        data.SaveTo(cacheStream);
                    }
                    catch { }
                }

                var writeableBitmap = CreateAvaloniaBitmap(finalBitmap);
                if (needsDispose) finalBitmap.Dispose();
                
                return writeableBitmap;
            }
            catch { return null; }
        }

        private ImageItem? GenerateHighQualityPreview(string path, CancellationToken token)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var codec = SKCodec.Create(stream);
                if (codec == null) return null;

                int targetWidth = 1500; 
                SKSizeI supportedDimensions = codec.Info.Size;
                if (codec.Info.Width > targetWidth)
                {
                    float scale = (float)targetWidth / codec.Info.Width;
                    supportedDimensions = codec.GetScaledDimensions(scale);
                }

                var info = new SKImageInfo(supportedDimensions.Width, supportedDimensions.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
                using var rawBitmap = new SKBitmap(info);
                
                if (codec.GetPixels(info, rawBitmap.GetPixels()) != SKCodecResult.Success) return null;
                if (token.IsCancellationRequested) return null;

                SKBitmap workingBitmap = rawBitmap;
                bool needsDispose = false;

                double angle = dbm_select.Utils.ExifHelper.GetOrientationAngle(path);
                if (angle != 0)
                {
                    workingBitmap = RotateSkBitmap(rawBitmap, angle);
                    needsDispose = true;
                }

                var finalInfo = new SKImageInfo(workingBitmap.Width, workingBitmap.Height);
                using var surface = SKSurface.Create(finalInfo);
                using var canvas = surface.Canvas;
                
                var kernel = new float[] { -0.5f, -0.5f, -0.5f, -0.5f, 5.0f, -0.5f, -0.5f, -0.5f, -0.5f };
                using var paint = new SKPaint { FilterQuality = SKFilterQuality.High };
                paint.ImageFilter = SKImageFilter.CreateMatrixConvolution(new SKSizeI(3, 3), kernel, 1.0f, 0.0f, new SKPointI(1, 1), SKShaderTileMode.Clamp, false, null, null);

                canvas.DrawBitmap(workingBitmap, 0, 0, paint);
                canvas.Flush();

                if (needsDispose) workingBitmap.Dispose();

                using var image = surface.Snapshot();
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var ms = new MemoryStream();
                data.SaveTo(ms);
                ms.Seek(0, SeekOrigin.Begin);

                return new ImageItem { Bitmap = new Bitmap(ms), FileName = Path.GetFileName(path), FullPath = path };
            }
            catch { return null; }
        }

        // --- SKIASHARP HELPERS ---
        private SKBitmap RotateBitmap(SKBitmap bitmap, SKEncodedOrigin orientation)
        {
            SKBitmap rotated;
            var info = new SKImageInfo(
                orientation == SKEncodedOrigin.RightTop || orientation == SKEncodedOrigin.LeftBottom ? bitmap.Height : bitmap.Width,
                orientation == SKEncodedOrigin.RightTop || orientation == SKEncodedOrigin.LeftBottom ? bitmap.Width : bitmap.Height,
                bitmap.ColorType, bitmap.AlphaType);

            switch (orientation)
            {
                case SKEncodedOrigin.BottomRight:
                    rotated = new SKBitmap(info); using (var c = new SKCanvas(rotated)) { c.RotateDegrees(180, bitmap.Width/2, bitmap.Height/2); c.DrawBitmap(bitmap, 0, 0); } break;
                case SKEncodedOrigin.RightTop:
                    rotated = new SKBitmap(info); using (var c = new SKCanvas(rotated)) { c.Translate(rotated.Width, 0); c.RotateDegrees(90); c.DrawBitmap(bitmap, 0, 0); } break;
                case SKEncodedOrigin.LeftBottom:
                    rotated = new SKBitmap(info); using (var c = new SKCanvas(rotated)) { c.Translate(0, rotated.Height); c.RotateDegrees(270); c.DrawBitmap(bitmap, 0, 0); } break;
                default: return bitmap;
            }
            return rotated;
        }

        private SKBitmap RotateSkBitmap(SKBitmap bitmap, double angle)
        {
            bool isRotated90 = angle % 180 != 0;
            int newW = isRotated90 ? bitmap.Height : bitmap.Width;
            int newH = isRotated90 ? bitmap.Width : bitmap.Height;
            var rotated = new SKBitmap(newW, newH, bitmap.ColorType, bitmap.AlphaType);
            using (var c = new SKCanvas(rotated)) 
            { 
                c.Translate(newW / 2f, newH / 2f); 
                c.RotateDegrees((float)angle); 
                c.Translate(-bitmap.Width / 2f, -bitmap.Height / 2f); 
                c.DrawBitmap(bitmap, 0, 0); 
            }
            return rotated;
        }

        private Bitmap CreateAvaloniaBitmap(SKBitmap skBitmap)
        {
            var px = new Avalonia.PixelSize(skBitmap.Width, skBitmap.Height);
            var wb = new Avalonia.Media.Imaging.WriteableBitmap(px, new Avalonia.Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Premul);
            using (var buf = wb.Lock())
            {
                var info = new SKImageInfo(skBitmap.Width, skBitmap.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
                using (var surface = SKSurface.Create(info, buf.Address, buf.RowBytes)) surface.Canvas.DrawBitmap(skBitmap, 0, 0);
            }
            return wb;
        }

        // --- COMMANDS ---
        public void SetPackageImage(string category, ImageItem sourceItem)
        {
            ClearSlot(category);
            ImageItem newSlotItem = sourceItem;
            // Load medium quality for slot (300px) - uses cache if available
            var mediumBitmap = LoadBitmapWithOrientation(sourceItem.FullPath, 300);
            if (mediumBitmap != null)
                newSlotItem = new ImageItem { FileName = sourceItem.FileName, FullPath = sourceItem.FullPath, Bitmap = mediumBitmap };

            switch (category)
            {
                case "8x10": Image8x10 = newSlotItem; break;
                case "Barong": ImageBarong = newSlotItem; break;
                case "Creative": ImageCreative = newSlotItem; break;
                case "Any": ImageAny = newSlotItem; break;
                case "Instax": ImageInstax = newSlotItem; break;
            }
        }

        [RelayCommand]
        public async Task OpenPreviewPackage()
        {
            IsPreviewPackageDialogVisible = true;
            IsModalLoading = true;
            
            PreviewImage8x10?.Bitmap?.Dispose(); PreviewImage8x10 = null;
            PreviewImageBarong?.Bitmap?.Dispose(); PreviewImageBarong = null;
            PreviewImageCreative?.Bitmap?.Dispose(); PreviewImageCreative = null;
            PreviewImageAny?.Bitmap?.Dispose(); PreviewImageAny = null;
            PreviewImageInstax?.Bitmap?.Dispose(); PreviewImageInstax = null;

            try
            {
                var results = await Task.Run(() =>
                {
                    ImageItem? Load(ImageItem? src)
                    {
                        if (src == null) return null;
                        var bmp = LoadBitmapWithOrientation(src.FullPath, 500); 
                        if (bmp == null) return null;
                        return new ImageItem { Bitmap = bmp, FileName = src.FileName ?? "", FullPath = src.FullPath ?? "" };
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

        [RelayCommand] public void ClosePreviewPackage() { IsPreviewPackageDialogVisible = false; }
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
            await Task.Delay(2000); 
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
                    if (IsBarongVisible) SaveImageToFile(ImageBarong, " Barong Filipiniana ", specificFolder);
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
                _ = LoadImages(_currentBrowseFolderPath);
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
                LayoutStretch = Stretch.Uniform;
                ScrollVisibility = ScrollBarVisibility.Disabled;
            }
            else if (pkg == "A" || pkg == "B")
            {
                IsBarongVisible = true;
                IsDoubleLargeLayout = true;
                SlotsOrientation = Orientation.Vertical;
                LayoutStretch = Stretch.Uniform;
                ScrollVisibility = ScrollBarVisibility.Disabled;
            }
            else if (pkg == "C")
            {
                IsBarongVisible = true;
                IsCreativeVisible = true;
                IsAnyVisible = true;
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
                IsFiveLayout = true;
                SlotsOrientation = Orientation.Horizontal;
                LayoutStretch = Stretch.Uniform;
                ScrollVisibility = ScrollBarVisibility.Disabled;
            }
        }

        private void ResetData()
        {
            ClearCache();
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
        private void ClearBrowserImages() 
        { 
            ClearCache();
            foreach (var i in Images) i.Bitmap?.Dispose(); 
            Images.Clear(); 
            GC.Collect(); 
            HasNoImages = true; 
        }
    }
}