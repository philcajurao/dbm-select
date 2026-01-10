using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using dbm_select.Models;
using dbmselect.Models;
using MiniExcelLibs;
using SkiaSharp;
using System;
using Avalonia.Threading;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Utils;

namespace dbm_select.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private readonly AppSettings _appsettings = new();

        // --- CACHING VARIABLES ---
        private const int MaxCacheSize = 20;
        private readonly Dictionary<string, ImageItem> _highResCache = [];
        private readonly List<string> _cacheOrder = [];
        private readonly object _cacheLock = new();
        private ImageItem? _lastHighResPreview;

        // --- FOLDER CACHE (Instant Load) ---
        private readonly Dictionary<string, List<ImageItem>> _folderCache = new();
        private readonly List<string> _folderCacheOrder = new();
        private const int MaxFolderCacheCount = 5;

        // --- WORKER QUEUE ---
        // Instead of firing 200 tasks, we queue items here and 1 worker processes them.
        private readonly ConcurrentQueue<ImageItem> _upgradeQueue = new();
        private CancellationTokenSource? _loadImagesCts;

        public MainWindowViewModel()
        {
            if (Design.IsDesignMode) return;

            if (!LoadSettings())
            {
                string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string baseFolder = Path.Combine(documentsPath, FolderNameConstants.DBM_SELECT);
                string logsFolder = Path.Combine(baseFolder, FolderNameConstants.LOGS);

                if (!Directory.Exists(baseFolder)) Directory.CreateDirectory(baseFolder);
                if (!Directory.Exists(logsFolder)) Directory.CreateDirectory(logsFolder);

                OutputFolderPath = baseFolder;
                ExcelFolderPath = logsFolder;
                ExcelFileName = FileNameConstants.CLIENT_LOGS;
            }

            string folderToLoad = !string.IsNullOrEmpty(_currentBrowseFolderPath) && Directory.Exists(_currentBrowseFolderPath)
                ? _currentBrowseFolderPath
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));

            _ = LoadImages(folderToLoad);

            UpdateVisibility(PackgeConstants.Basic);
        }

        // --- PROPERTIES (Unchanged) ---
        private string _snapOutputFolder = string.Empty;
        private string _snapExcelFolder = string.Empty;
        private string _snapExcelFileName = string.Empty;
        private string _currentBrowseFolderPath = string.Empty;

        [ObservableProperty] private bool _isSettingsDirty;
        [ObservableProperty] private string _outputFolderPath = string.Empty;
        partial void OnOutputFolderPathChanged(string value) => CheckSettingsDirty();
        [ObservableProperty] private string _excelFolderPath = string.Empty;
        partial void OnExcelFolderPathChanged(string value) => CheckSettingsDirty();
        [ObservableProperty] private string _excelFileName = FileNameConstants.CLIENT_LOGS;
        partial void OnExcelFileNameChanged(string value) => CheckSettingsDirty();

        private void CheckSettingsDirty()
        {
            IsSettingsDirty = OutputFolderPath != _snapOutputFolder || ExcelFolderPath != _snapExcelFolder || ExcelFileName != _snapExcelFileName;
        }

        [ObservableProperty] private string? _clientName;
        [ObservableProperty] private string? _clientEmail;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SelectedPackageDisplayName))]
        private string _selectedPackage = PackgeConstants.Basic;
        public string SelectedPackageDisplayName => SelectedPackage == PackgeConstants.Basic ? PackgeConstants.BasicPackage : $"Package {SelectedPackage}";

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
        [ObservableProperty] private ImageItem? _previewImageSoloGroup;
        [ObservableProperty] private bool _isModalLoading;

        [ObservableProperty] private ImageItem? _image8x10;
        [ObservableProperty] private ImageItem? _imageBarong;
        [ObservableProperty] private ImageItem? _imageCreative;
        [ObservableProperty] private ImageItem? _imageAny;
        [ObservableProperty] private ImageItem? _imageSoloGroup;

        [ObservableProperty] private bool _isBarongVisible;
        [ObservableProperty] private bool _isCreativeVisible;
        [ObservableProperty] private bool _isAnyVisible;
        [ObservableProperty] private bool _isSoloGroupVisible;

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
        // STABLE IMAGE LOADING LOGIC (Worker Pattern)
        // ---------------------------------------------------------
        public async Task LoadImages(string folderPath)
        {
            // 1. HARD STOP previous loading
            if (_loadImagesCts != null)
            {
                _loadImagesCts.Cancel();
                _loadImagesCts.Dispose();
                _loadImagesCts = null;
            }

            _loadImagesCts = new CancellationTokenSource();
            var token = _loadImagesCts.Token;

            // 2. Cleanup Memory & Queue
            _upgradeQueue.Clear();
            Images.Clear();
            HasNoImages = false;
            IsLoadingImages = true;

            // Force GC to clear previous folder's bitmaps before loading new ones
            GC.Collect();
            GC.WaitForPendingFinalizers();

            if (!Directory.Exists(folderPath)) return;

            _currentBrowseFolderPath = folderPath;
            SaveSettings();

            // 3. INSTANT CACHE CHECK
            if (_folderCache.ContainsKey(folderPath))
            {
                var cachedImages = _folderCache[folderPath];

                // Refresh LRU
                _folderCacheOrder.Remove(folderPath);
                _folderCacheOrder.Add(folderPath);

                if (cachedImages.Count == 0) HasNoImages = true;

                // Add all to UI instantly
                foreach (var img in cachedImages) Images.Add(img);

                IsLoadingImages = false;
                return;
            }

            // 4. LOAD FROM DISK
            await Task.Run(async () =>
            {
                var supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png" };

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

                var currentFolderItems = new List<ImageItem>();
                var batch = new List<ImageItem>();

                // PHASE 1: Fast Load Loop
                foreach (var file in files)
                {
                    if (token.IsCancellationRequested) return;

                    try
                    {
                        // Tiny load (40px)
                        var tinyBmp = LoadBitmapWithOrientation(file, 40);

                        if (tinyBmp != null)
                        {
                            var item = new ImageItem
                            {
                                FileName = Path.GetFileName(file) ?? "Unknown",
                                FullPath = file,
                                Bitmap = tinyBmp
                            };

                            currentFolderItems.Add(item);
                            batch.Add(item);

                            // Enqueue for background worker to upgrade later
                            _upgradeQueue.Enqueue(item);
                        }
                    }
                    catch { }

                    // Update UI in batches of 10 to prevent freezing
                    if (batch.Count >= 10)
                    {
                        var chunk = batch.ToList();
                        batch.Clear();
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            foreach (var i in chunk) Images.Add(i);
                        }, DispatcherPriority.Background);

                        // Start the upgrade worker if not running
                        _ = ProcessUpgradeQueue(token);
                    }
                }

                // Add remaining items
                if (batch.Count > 0)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        foreach (var i in batch) Images.Add(i);
                    });
                    _ = ProcessUpgradeQueue(token);
                }

                // PHASE 2: Cache Result
                if (!token.IsCancellationRequested)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (_folderCache.ContainsKey(folderPath)) return;

                        if (_folderCacheOrder.Count >= MaxFolderCacheCount)
                        {
                            var oldest = _folderCacheOrder[0];
                            if (_folderCache.TryGetValue(oldest, out var oldList))
                            {
                                foreach (var img in oldList) img.Bitmap?.Dispose();
                            }
                            _folderCache.Remove(oldest);
                            _folderCacheOrder.RemoveAt(0);
                        }

                        _folderCache[folderPath] = currentFolderItems;
                        _folderCacheOrder.Add(folderPath);
                    });
                }

            }, token);
        }

        // --- BACKGROUND WORKER FOR UPGRADES ---
        // This runs sequentially on a background thread, ensuring we don't overload the system
        private async Task ProcessUpgradeQueue(CancellationToken token)
        {
            // Simple locking mechanism to ensure only one worker runs per token
            // Note: In a real producer/consumer, we'd use Channels, but this is sufficient here.

            while (!_upgradeQueue.IsEmpty)
            {
                if (token.IsCancellationRequested) return;

                if (_upgradeQueue.TryDequeue(out var item))
                {
                    try
                    {
                        // Check if already high quality (e.g. from previous run)
                        if (item.Bitmap != null && item.Bitmap.PixelSize.Width > 100) continue;

                        // Heavy Load
                        var highResBmp = await Task.Run(() => LoadBitmapWithOrientation(item.FullPath, 200), token);

                        if (highResBmp != null && !token.IsCancellationRequested)
                        {
                            await Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                var old = item.Bitmap;
                                item.Bitmap = highResBmp; // UI Snaps here
                                old?.Dispose();
                            }, DispatcherPriority.Background);
                        }
                    }
                    catch { }
                }

                // Small breathe between heavy decodes
                await Task.Delay(5, token);
            }
        }

        // ---------------------------------------------------------
        // PREVIEW LOGIC
        // ---------------------------------------------------------
        private async Task UpdatePreviewAsync(ImageItem? selectedItem, CancellationToken token)
        {
            if (_lastHighResPreview != null)
            {
                bool isInCache = false;
                lock (_cacheLock) { isInCache = _highResCache.ContainsValue(_lastHighResPreview); }
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
            // Note: We do NOT clear _folderCache here, that is persistent across browsing.
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

                        if (File.Exists(cachePath)) return new Bitmap(cachePath);
                    }
                }
                catch { }
            }

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

                // SAVE TO DISK CACHE
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

        // --- SKIASHARP HELPERS (Unchanged) ---
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
                    rotated = new SKBitmap(info); using (var c = new SKCanvas(rotated)) { c.RotateDegrees(180, bitmap.Width / 2, bitmap.Height / 2); c.DrawBitmap(bitmap, 0, 0); }
                    break;
                case SKEncodedOrigin.RightTop:
                    rotated = new SKBitmap(info); using (var c = new SKCanvas(rotated)) { c.Translate(rotated.Width, 0); c.RotateDegrees(90); c.DrawBitmap(bitmap, 0, 0); }
                    break;
                case SKEncodedOrigin.LeftBottom:
                    rotated = new SKBitmap(info); using (var c = new SKCanvas(rotated)) { c.Translate(0, rotated.Height); c.RotateDegrees(270); c.DrawBitmap(bitmap, 0, 0); }
                    break;
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
            var mediumBitmap = LoadBitmapWithOrientation(sourceItem.FullPath, 300);
            if (mediumBitmap != null)
                newSlotItem = new ImageItem { FileName = sourceItem.FileName, FullPath = sourceItem.FullPath, Bitmap = mediumBitmap };

            switch (category)
            {
                case CategoryConstants.EightByTen: Image8x10 = newSlotItem; break;
                case CategoryConstants.Barong: ImageBarong = newSlotItem; break;
                case CategoryConstants.Creative: ImageCreative = newSlotItem; break;
                case CategoryConstants.Any: ImageAny = newSlotItem; break;
                // UPDATED CASE
                case CategoryConstants.SoloGroup: ImageSoloGroup = newSlotItem; break;
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
            // UPDATED DISPOSE
            PreviewImageSoloGroup?.Bitmap?.Dispose(); PreviewImageSoloGroup = null;

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
                        { CategoryConstants.EightByTen, Load(Image8x10) },
                        { CategoryConstants.Barong, IsBarongVisible ? Load(ImageBarong) : null },
                        { CategoryConstants.Creative, IsCreativeVisible ? Load(ImageCreative) : null },
                        { CategoryConstants.Any, IsAnyVisible ? Load(ImageAny) : null },
                        // UPDATED KEY AND PROPERTY
                        { CategoryConstants.SoloGroup, IsSoloGroupVisible ? Load(ImageSoloGroup) : null }
                    };
                });

                PreviewImage8x10 = results[CategoryConstants.EightByTen];
                PreviewImageBarong = results[CategoryConstants.Barong];
                PreviewImageCreative = results[CategoryConstants.Creative];
                PreviewImageAny = results[CategoryConstants.Any];
                // UPDATED ASSIGNMENT
                PreviewImageSoloGroup = results[CategoryConstants.SoloGroup];
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
            // UPDATED CHECK
            else if (IsSoloGroupVisible && ImageSoloGroup == null) isMissing = true;

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
                case CategoryConstants.EightByTen: Image8x10?.Bitmap?.Dispose(); Image8x10 = null; break;
                case CategoryConstants.Barong: ImageBarong?.Bitmap?.Dispose(); ImageBarong = null; break;
                case CategoryConstants.Creative: ImageCreative?.Bitmap?.Dispose(); ImageCreative = null; break;
                case CategoryConstants.Any: ImageAny?.Bitmap?.Dispose(); ImageAny = null; break;
                // UPDATED CASE
                case CategoryConstants.SoloGroup: ImageSoloGroup?.Bitmap?.Dispose(); ImageSoloGroup = null; break;
            }
            GC.Collect();
        }

        [RelayCommand]
        public async Task ProceedFromAcknowledgement()
        {
            // 1. Close the dialog and show loading spinner
            IsAcknowledgementDialogVisible = false;
            IsLoadingSubmit = true;

            // 2. STOP any background image loading immediately to free up resources/files
            _loadImagesCts?.Cancel();

            // Small delay to ensure UI updates and previous tasks stop
            await Task.Delay(500);

            try
            {
                await Task.Run(() =>
                {
                    // --- A. VALIDATION & SETUP ---
                    if (string.IsNullOrWhiteSpace(OutputFolderPath))
                        throw new DirectoryNotFoundException("The output folder path is not set in Settings.");

                    if (!Directory.Exists(OutputFolderPath))
                        Directory.CreateDirectory(OutputFolderPath);

                    // Sanitize Client Name for Folder Creation
                    string safeClientName = (ClientName ?? "Unknown").ToUpper();
                    foreach (char c in Path.GetInvalidFileNameChars())
                        safeClientName = safeClientName.Replace(c, '_');

                    string specificFolder = Path.Combine(OutputFolderPath, $"{SelectedPackage}-{safeClientName}");
                    if (!Directory.Exists(specificFolder))
                        Directory.CreateDirectory(specificFolder);

                    // --- B. SAVE IMAGES ---
                    SaveImageToFile(Image8x10, " 8x10 ", specificFolder);

                    if (IsBarongVisible)
                        SaveImageToFile(ImageBarong, " Barong Filipiniana ", specificFolder);

                    if (IsCreativeVisible)
                        SaveImageToFile(ImageCreative, " Creative ", specificFolder);

                    if (IsAnyVisible)
                        SaveImageToFile(ImageAny, " Any ", specificFolder);

                    if (IsSoloGroupVisible)
                        SaveImageToFile(ImageSoloGroup, " Solo or Group ", specificFolder);

                    // --- C. EXCEL LOGGING ---
                    string excelPath = Path.Combine(ExcelFolderPath, ExcelFileName + ".xlsx");
                    string? excelDir = Path.GetDirectoryName(excelPath);

                    if (!string.IsNullOrEmpty(excelDir) && !Directory.Exists(excelDir))
                        Directory.CreateDirectory(excelDir);

                    // Create new log item
                    // (Note: I condensed the logic here to match your previous flow properly)
                    var allRows = new List<OrderLogItem>();

                    // 1. Try reading existing file
                    if (File.Exists(excelPath))
                    {
                        try
                        {
                            allRows.AddRange(MiniExcel.Query<OrderLogItem>(excelPath));
                        }
                        catch (IOException)
                        {
                            // If reading fails, we just proceed with the new list
                        }
                    }

                    var newItem = new OrderLogItem
                    {
                        Status = "DONE CHOOSING",
                        Name = safeClientName,
                        Email = ClientEmail ?? string.Empty,
                        Package = SelectedPackage,
                        Box_8x10 = Image8x10?.FileName ?? "Empty",
                        Box_Barong = IsBarongVisible ? ImageBarong?.FileName ?? "Empty" : "N/A",
                        Box_Creative = IsCreativeVisible ? ImageCreative?.FileName ?? "Empty" : "N/A",
                        Box_Any = IsAnyVisible ? ImageAny?.FileName ?? "Empty" : "N/A",
                        Box_SoloGroup = IsSoloGroupVisible ? ImageSoloGroup?.FileName ?? "Empty" : "N/A",
                        TimeStamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    };

                    allRows.Add(newItem);

                    // 2. Save Excel (Manual Overwrite Safety)
                    try
                    {
                        if (File.Exists(excelPath))
                        {
                            File.Delete(excelPath);
                        }

                        MiniExcel.SaveAs(excelPath, allRows);
                    }
                    catch (IOException)
                    {
                        throw new IOException($"Could not save to Excel. Please ensure '{ExcelFileName}.xlsx' is closed.");
                    }
                }); // <--- THIS WAS MISSING (Closes Task.Run)

                // --- D. CLEANUP & RESET (Main Thread) ---
                ResetData();

                // Use the new safe ClearBrowserImages method
                ClearBrowserImages();

                IsLoadingSubmit = false;
                IsThankYouDialogVisible = true;

                // Reload folder images fresh
                _ = LoadImages(_currentBrowseFolderPath);
            } // <--- THIS WAS MISSING (Closes the Try block)
            catch (IOException ioEx)
            {
                // Handle file locking specifically
                IsLoadingSubmit = false;
                ErrorMessage = ioEx.Message;
                IsErrorDialogVisible = true;
            }
            catch (Exception ex)
            {
                // Handle any other crashes
                IsLoadingSubmit = false;
                ErrorMessage = $"An unexpected error occurred: {ex.Message}";
                IsErrorDialogVisible = true;
            }
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
            if (!_appsettings.LoadSettings())
                return false;

            if (!string.IsNullOrEmpty(_appsettings.LastOutputFolder))
                OutputFolderPath = _appsettings.LastOutputFolder;

            if (!string.IsNullOrEmpty(_appsettings.LastExcelFolder))
                ExcelFolderPath = _appsettings.LastExcelFolder;
            else ExcelFolderPath = OutputFolderPath;

            if (!string.IsNullOrEmpty(_appsettings.LastExcelFileName))
                ExcelFileName = _appsettings.LastExcelFileName;

            if (!string.IsNullOrEmpty(_appsettings.LastBrowseFolder))
                _currentBrowseFolderPath = _appsettings.LastBrowseFolder;

            return true;
        }
        private void SaveSettings()
        {
            _appsettings.SaveSettings(
                OutputFolderPath,
                ExcelFolderPath,
                ExcelFileName,
                _currentBrowseFolderPath);
        }

        [ObservableProperty] private string _anySlotLabel = "Any";
        [ObservableProperty] private bool _isBarongHorizontal; // For C & D (Top Row)
[ObservableProperty] private bool _isBarongVertical;   // For A & B (Bottom Row)
[ObservableProperty] private bool _isSecondaryQuad; // Controls if bottom row items are small

private void UpdateVisibility(string pkg)
{
    // 1. Reset all flags
    IsBarongVisible = false;
    IsBarongHorizontal = false;
    IsBarongVertical = false;
    IsCreativeVisible = false;
    IsAnyVisible = false;
    IsSoloGroupVisible = false;
    IsSingleLargeLayout = false;
    IsDoubleLargeLayout = false;
    IsQuadLayout = false;
    IsFiveLayout = false;
    
    // New Flags Reset
    IsSecondaryQuad = false;

    LayoutStretch = Stretch.None;
    ScrollVisibility = ScrollBarVisibility.Auto;
    AnySlotLabel = "Any";

    if (pkg == "Basic")
    {
        IsSingleLargeLayout = true;
        LayoutStretch = Stretch.Uniform;
        ScrollVisibility = ScrollBarVisibility.Disabled;
    }
    else if (pkg == "A")
    {
        // 2 Rows: 8x10 (Big) + Barong (Big)
        IsBarongVisible = true;
        IsBarongVertical = true;
        IsDoubleLargeLayout = true; // Makes 8x10 Big
        IsSecondaryQuad = false;    // Makes Barong Big
        LayoutStretch = Stretch.Uniform;
        ScrollVisibility = ScrollBarVisibility.Disabled;
    }
   else if (pkg == "B")
    {
        // 2 Rows: 
        // Row 1: 8x10 (Normal/Quad Size)
        // Row 2: Barong + Barkada (Quad Size)
        IsBarongVisible = true;
        IsBarongVertical = true;
        IsAnyVisible = true;
        AnySlotLabel = "Barkada Shot";
        
        IsDoubleLargeLayout = false; // CHANGED: Set to false (was true) to remove huge size
        IsQuadLayout = true;         // ADDED: Set to true so 8x10 matches the size of bottom slots
        IsSecondaryQuad = true;      // Bottom row remains Quad/Small
        
        LayoutStretch = Stretch.Uniform;
        ScrollVisibility = ScrollBarVisibility.Disabled;
    }
    else if (pkg == "C")
    {
        // 2 Rows: [8x10 + Barong] + [Creative + Barkada]
        IsBarongVisible = true;
        IsBarongHorizontal = true;
        IsCreativeVisible = true;
        IsAnyVisible = true;
        AnySlotLabel = "Barkada Shot";
        
        IsQuadLayout = true;
        IsSecondaryQuad = true; 
        
        LayoutStretch = Stretch.Uniform;
        ScrollVisibility = ScrollBarVisibility.Disabled;
    }
    else if (pkg == "D")
    {
        // 3 Rows: [8x10 + Barong] + [Creative + Any] + [Solo]
        IsBarongVisible = true;
        IsBarongHorizontal = true;
        IsCreativeVisible = true;
        IsAnyVisible = true;
        IsSoloGroupVisible = true;
        
        IsFiveLayout = true;
        IsSecondaryQuad = true;
        
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
            ImageSoloGroup = null;
            UpdateVisibility("Basic");
        }

        private bool IsValidEmail(string email) { try { return Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase); } catch { return false; } }
        private void ClearBrowserImages()
        {
            // 1. Cancel any active loading background tasks first
            _loadImagesCts?.Cancel();

            // 2. Remove the current folder from Memory Cache
            // This forces the app to reload fresh from Disk Cache (safe) instead of using disposed RAM images (unsafe)
            if (!string.IsNullOrEmpty(_currentBrowseFolderPath))
            {
                if (_folderCache.ContainsKey(_currentBrowseFolderPath))
                {
                    _folderCache.Remove(_currentBrowseFolderPath);
                    _folderCacheOrder.Remove(_currentBrowseFolderPath);
                }
            }

            // 3. Snapshot the images currently in the list
            var imagesToDispose = Images.ToList();

            // 4. Clear the observable collection (This updates the UI)
            Images.Clear();
            HasNoImages = true;

            // 5. Clear the internal High-Res Preview cache
            ClearCache();

            // 6. Dispose the bitmaps on a background thread to avoid UI lag/locking
            Task.Run(async () =>
            {
                // Wait 1 second to ensure the UI has fully removed the images from the Visual Tree
                await Task.Delay(1000);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    foreach (var i in imagesToDispose)
                    {
                        i.Bitmap?.Dispose();
                    }
                    // Force a GC collection to reclaim the memory
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                });
            });
        }
    }
}