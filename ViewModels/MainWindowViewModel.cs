using Avalonia.Controls;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using dbm_select.Models;
using MiniExcelLibs;
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

    // 1. Load Settings FIRST to get the saved paths
    if (!LoadSettings())
    {
        // First run defaults
        string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string baseFolder = Path.Combine(documentsPath, "DBM_Select");
        string logsFolder = Path.Combine(baseFolder, "Logs");

        if (!Directory.Exists(baseFolder)) Directory.CreateDirectory(baseFolder);
        if (!Directory.Exists(logsFolder)) Directory.CreateDirectory(logsFolder);

        OutputFolderPath = baseFolder;
        ExcelFolderPath = logsFolder;
        ExcelFileName = "Client_Logs";
    }

    // 2. Decide which folder to load
    string folderToLoad;

    if (!string.IsNullOrEmpty(_currentBrowseFolderPath) && Directory.Exists(_currentBrowseFolderPath))
    {
        // Use the saved folder from last session
        folderToLoad = _currentBrowseFolderPath;
    }
    else
    {
        // Fallback to Pictures folder
        folderToLoad = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
    }

    // 3. Load the images
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
            IsSettingsDirty =
                OutputFolderPath != _snapOutputFolder ||
                ExcelFolderPath != _snapExcelFolder ||
                ExcelFileName != _snapExcelFileName;
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
            // Don't await here to avoid blocking UI; method handles its own lifecycle
            _ = UpdatePreviewAsync(value);
        }

        // Separate property for the HD Preview Image
        [ObservableProperty] private ImageItem? _previewImage;
        [ObservableProperty] private bool _isLoadingPreview;

        // High-Resolution copies specifically for the Package Preview Modal
[ObservableProperty] private ImageItem? _previewImage8x10;
[ObservableProperty] private ImageItem? _previewImageBarong;
[ObservableProperty] private ImageItem? _previewImageCreative;
[ObservableProperty] private ImageItem? _previewImageAny;
[ObservableProperty] private ImageItem? _previewImageInstax;

// Property to manage the loading state of the modal's images
[ObservableProperty] private bool _isModalLoading;

        // ✅ UPDATED: Load High Quality Image on Demand
        private async Task UpdatePreviewAsync(ImageItem? thumbnailItem)
        {
            if (thumbnailItem == null)
            {
                PreviewImage = null;
                IsLoadingPreview = false;
                return;
            }

            // Show loading indicator immediately
            IsLoadingPreview = true;

            // Optional: Keep showing the thumbnail while loading the HD version? 
            // Or clear it? Let's clear it to show the spinner clearly.
            PreviewImage = null;

            try
            {
                // Load the FULL resolution image from disk in background
                var highResItem = await Task.Run(() =>
                {
                    try
                    {
                        // Load full quality
                        var bitmap = new Bitmap(thumbnailItem.FullPath);
                        return new ImageItem
                        {
                            Bitmap = bitmap,
                            FileName = thumbnailItem.FileName,
                            FullPath = thumbnailItem.FullPath
                        };
                    }
                    catch
                    {
                        return null;
                    }
                });

                PreviewImage = highResItem;
            }
            catch
            {
                // Fallback to thumbnail if HD load fails
                PreviewImage = thumbnailItem;
            }
            finally
            {
                IsLoadingPreview = false;
            }
        }

        [ObservableProperty] private ImageItem? _image8x10;
        [ObservableProperty] private ImageItem? _imageBarong;
        [ObservableProperty] private ImageItem? _imageCreative;
        [ObservableProperty] private ImageItem? _imageAny;
        [ObservableProperty] private ImageItem? _imageInstax;

        [ObservableProperty] private bool _isBarongVisible;
        [ObservableProperty] private bool _isCreativeVisible;
        [ObservableProperty] private bool _isAnyVisible;
        [ObservableProperty] private bool _isInstaxVisible;

        // Dialog Flags
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

        // --- SETTINGS PERSISTENCE ---
        private bool LoadSettings()
{
    try
    {
        if (File.Exists(_settingsFilePath))
        {
            string json = File.ReadAllText(_settingsFilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json);

            if (settings != null)
            {
                if (!string.IsNullOrEmpty(settings.LastOutputFolder)) OutputFolderPath = settings.LastOutputFolder;
                
                if (!string.IsNullOrEmpty(settings.LastExcelFolder)) ExcelFolderPath = settings.LastExcelFolder;
                else ExcelFolderPath = OutputFolderPath;
                
                if (!string.IsNullOrEmpty(settings.LastExcelFileName)) ExcelFileName = settings.LastExcelFileName;

                // ✅ NEW: Load the browse folder
                if (!string.IsNullOrEmpty(settings.LastBrowseFolder)) _currentBrowseFolderPath = settings.LastBrowseFolder;

                return true;
            }
        }
    }
    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}"); }
    return false;
}

private void SaveSettings()
{
    try
    {
        string? dir = Path.GetDirectoryName(_settingsFilePath);
        if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

        var settings = new AppSettings 
        { 
            LastOutputFolder = OutputFolderPath, 
            LastExcelFolder = ExcelFolderPath, 
            LastExcelFileName = ExcelFileName,
            LastBrowseFolder = _currentBrowseFolderPath // ✅ NEW: Save the browse folder
        };

        string json = JsonSerializer.Serialize(settings);
        File.WriteAllText(_settingsFilePath, json);
    }
    catch (Exception) { }
}

        public class AppSettings 
{ 
    public string? LastOutputFolder { get; set; } 
    public string? LastExcelFolder { get; set; } 
    public string? LastExcelFileName { get; set; }
    public string? LastBrowseFolder { get; set; } // ✅ NEW
}

        // --- LOGIC COMMANDS ---
        [RelayCommand] public void UpdatePackage(string packageName) { SelectedPackage = packageName; UpdateVisibility(packageName); }
        private void UpdateVisibility(string pkg)
        {
            IsBarongVisible = false; IsCreativeVisible = false; IsAnyVisible = false; IsInstaxVisible = false;
            if (pkg == "A" || pkg == "B") { IsBarongVisible = true; }
            else if (pkg == "C") { IsBarongVisible = true; IsCreativeVisible = true; IsAnyVisible = true; }
            else if (pkg == "D") { IsBarongVisible = true; IsCreativeVisible = true; IsAnyVisible = true; IsInstaxVisible = true; }
        }


// In MainWindowViewModel.cs

public void SetPackageImage(string category, ImageItem sourceItem)
{
    // 1. Dispose OLD IMAGE: Always dispose the image currently in the slot first.
    switch (category)
    {
        case "8x10": Image8x10?.Bitmap?.Dispose(); break;
        case "Barong": ImageBarong?.Bitmap?.Dispose(); break;
        case "Creative": ImageCreative?.Bitmap?.Dispose(); break;
        case "Any": ImageAny?.Bitmap?.Dispose(); break;
        case "Instax": ImageInstax?.Bitmap?.Dispose(); break;
    }

    // Default to the low-res thumbnail as a resilient fallback.
    ImageItem newSlotItem = sourceItem;

    // ✅ UPDATED: Set the target decoding width to 300 pixels. 
    // This is still high quality for the UI but significantly lowers memory usage.
    const int MEDIUM_PREVIEW_WIDTH = 300; 

    try
    {
        // 2. LOAD MEDIUM QUALITY: Attempt to load the image capped at 300px width.
        Bitmap mediumQualityBitmap;
        using (var stream = File.OpenRead(sourceItem.FullPath))
        {
            // Use DecodeToWidth to cap the resolution.
            mediumQualityBitmap = Bitmap.DecodeToWidth(stream, MEDIUM_PREVIEW_WIDTH);
        }

        // 3. SUCCESS: Create the new medium-res item.
        newSlotItem = new ImageItem
        {
            FileName = sourceItem.FileName,
            FullPath = sourceItem.FullPath,
            Bitmap = mediumQualityBitmap
        };
    }
    catch (Exception ex)
    {
        // 4. FAILURE: If any file operation fails, the newSlotItem remains the low-res thumbnail.
        System.Diagnostics.Debug.WriteLine($"Error loading medium quality image for slot: {ex.Message}");
    }

    // 5. ASSIGNMENT: Assign the resulting item.
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
public void ClearSlot(string category)
{
    // 1. Explicitly dispose the Bitmap object before setting the property to null.
    //    This immediately frees the large image memory from the RAM.
    switch (category) 
    { 
        case "8x10": Image8x10?.Bitmap?.Dispose(); Image8x10 = null; break; 
        case "Barong": ImageBarong?.Bitmap?.Dispose(); ImageBarong = null; break; 
        case "Creative": ImageCreative?.Bitmap?.Dispose(); ImageCreative = null; break; 
        case "Any": ImageAny?.Bitmap?.Dispose(); ImageAny = null; break; 
        case "Instax": ImageInstax?.Bitmap?.Dispose(); ImageInstax = null; break; 
    }

    // 2. Force garbage collection.
    //    While usually unnecessary, forcing GC helps reclaim the unmanaged memory (from Dispose)
    //    faster on lower-spec machines or during long sessions with large images.
    GC.Collect();
}



        [RelayCommand] public void ClearAll() { IsClearConfirmationVisible = true; }
        [RelayCommand] public void ConfirmClear() { ResetData(); IsClearConfirmationVisible = false; }
        [RelayCommand] public void CancelClear() { IsClearConfirmationVisible = false; }
        [RelayCommand] public void OpenSettings() { _snapOutputFolder = OutputFolderPath; _snapExcelFolder = ExcelFolderPath; _snapExcelFileName = ExcelFileName; IsSettingsDirty = false; IsSettingsDialogVisible = true; }
        [RelayCommand] public void CancelSettings() { OutputFolderPath = _snapOutputFolder; ExcelFolderPath = _snapExcelFolder; ExcelFileName = _snapExcelFileName; IsSettingsDialogVisible = false; }
        [RelayCommand] public void SaveAndCloseSettings() { SaveSettings(); IsSettingsDialogVisible = false; }
        [RelayCommand] public void OpenAbout() { IsAboutDialogVisible = true; }
        [RelayCommand] public void CloseAbout() { IsAboutDialogVisible = false; }

private ImageItem? LoadHighResImageFromFile(ImageItem? source)
{
    if (source == null || string.IsNullOrEmpty(source.FullPath))
    {
        return null;
    }

    try
    {
        // Loads the image at its ORIGINAL, full quality
        Bitmap originalQualityBitmap;
        using (var stream = File.OpenRead(source.FullPath))
        {
            // Note: No DecodeToWidth() is used here for the preview modal.
            originalQualityBitmap = new Bitmap(stream);
        }

        // Create the new high-res item
        return new ImageItem
        {
            FileName = source.FileName,
            FullPath = source.FullPath,
            Bitmap = originalQualityBitmap
        };
    }
    catch (Exception ex)
    {
        // Debug output is crucial here to find file lock/permission errors!
        System.Diagnostics.Debug.WriteLine($"CRITICAL ERROR loading ORIGINAL image for preview: {ex.Message}");
        // Fallback to the optimized (300px) slot image if original load fails
        return source; 
    }
}

        [RelayCommand]
public async Task OpenPreviewPackage()
{
    // CRITICAL: We set the visibility and loading state FIRST.
    IsPreviewPackageDialogVisible = true;
    IsModalLoading = true;
    
    // Allows the UI thread to acknowledge the visibility change before blocking.
    await Task.Delay(50); 
    
    // Clear previous high-res images and memory (We call ClosePreviewPackage without setting IsPreviewPackageDialogVisible=false)
    // We modify the close method to prevent it from hiding the dialog early.
    DisposePreviewImages(); 

    try
    {
        // ... (Task.Run block remains the same as before to load images) ...
        var results = await Task.Run(() =>
        {
            return new Dictionary<string, ImageItem?>
            {
                { "8x10", LoadHighResImageFromFile(Image8x10) },
                { "Barong", IsBarongVisible ? LoadHighResImageFromFile(ImageBarong) : null },
                { "Creative", IsCreativeVisible ? LoadHighResImageFromFile(ImageCreative) : null },
                { "Any", IsAnyVisible ? LoadHighResImageFromFile(ImageAny) : null },
                { "Instax", IsInstaxVisible ? LoadHighResImageFromFile(ImageInstax) : null }
            };
        });

        // Assign results back on the UI Thread
        PreviewImage8x10 = results["8x10"];
        PreviewImageBarong = results["Barong"];
        PreviewImageCreative = results["Creative"];
        PreviewImageAny = results["Any"];
        PreviewImageInstax = results["Instax"];
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"Fatal error in OpenPreviewPackage command: {ex.Message}");
        // If loading fails completely, ensure the modal is dismissed
        IsPreviewPackageDialogVisible = false;
    }
    finally
    {
        // Always hide the loading spinner
        IsModalLoading = false;
    }
}

// Resets high-res images when closing the modal
private void DisposePreviewImages()
{
    // FIX MVVMTK0034: Use public properties (PreviewImage8x10, etc.) instead of private fields (_previewImage8x10).
    PreviewImage8x10?.Bitmap?.Dispose(); PreviewImage8x10 = null;
    PreviewImageBarong?.Bitmap?.Dispose(); PreviewImageBarong = null;
    PreviewImageCreative?.Bitmap?.Dispose(); PreviewImageCreative = null;
    PreviewImageAny?.Bitmap?.Dispose(); PreviewImageAny = null;
    PreviewImageInstax?.Bitmap?.Dispose(); PreviewImageInstax = null;
    
    GC.Collect();
}


// In MainWindowViewModel.cs (Replace existing ClosePreviewPackage command)

[RelayCommand] 
public void ClosePreviewPackage() 
{ 
    IsPreviewPackageDialogVisible = false; 
    DisposePreviewImages();
}


        [RelayCommand] public void OpenHelp() { IsHelpDialogVisible = true; }
        [RelayCommand] public void CloseHelp() { IsHelpDialogVisible = false; }
        [RelayCommand]
        public void Submit()
        {
            if (string.IsNullOrWhiteSpace(ClientName) || string.IsNullOrWhiteSpace(ClientEmail)) { ErrorMessage = "Please enter both the Client Name and Email Address."; IsErrorDialogVisible = true; return; }
            if (!IsValidEmail(ClientEmail)) { ErrorMessage = "The Email Address format is invalid.\n(e.g., user@example.com)"; IsErrorDialogVisible = true; return; }
            bool isMissingImages = false;
            if (Image8x10 == null) isMissingImages = true; else if (IsBarongVisible && ImageBarong == null) isMissingImages = true; else if (IsCreativeVisible && ImageCreative == null) isMissingImages = true; else if (IsAnyVisible && ImageAny == null) isMissingImages = true; else if (IsInstaxVisible && ImageInstax == null) isMissingImages = true;
            if (isMissingImages) { ErrorMessage = $"Your selected package ({SelectedPackage}) requires all photo slots to be filled."; IsErrorDialogVisible = true; return; }
            IsSubmitConfirmationVisible = true;
        }
        [RelayCommand] public void ConfirmSubmit() { IsSubmitConfirmationVisible = false; IsImportantNotesDialogVisible = true; }
        [RelayCommand] public void ContinueFromNotes() { IsImportantNotesDialogVisible = false; IsImportantNotesChecked = false; IsAcknowledgementDialogVisible = true; }
        [RelayCommand] public void CancelNotes() { IsImportantNotesDialogVisible = false; }
        [RelayCommand] public void CancelAcknowledgement() { IsAcknowledgementDialogVisible = false; }
        // In MainWindowViewModel.cs

[RelayCommand]
public async Task ProceedFromAcknowledgement()
{
    // 1. Hide the Ack Dialog immediately
    IsAcknowledgementDialogVisible = false;
    
    // 2. Show the Loading Overlay
    IsLoadingSubmit = true;

    // Small delay to ensure UI renders the loader before heavy lifting starts
    await Task.Delay(3000);

    try
    {
        // 3. Run heavy file operations in the background
        await Task.Run(() =>
        {
            // --- FOLDER SETUP ---
            string baseFolder = OutputFolderPath;
            string safeClientName = (ClientName ?? "Unknown").ToUpper();
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                safeClientName = safeClientName.Replace(c, '_');
            }
            string subFolderName = $"{SelectedPackage}-{safeClientName}";
            string specificSubmissionFolder = Path.Combine(baseFolder, subFolderName);

            if (!Directory.Exists(specificSubmissionFolder))
                Directory.CreateDirectory(specificSubmissionFolder);

            // --- SAVE IMAGES ---
            // Note: Accessing ViewModel properties (like Image8x10) from this background thread 
            // is generally safe for reading, provided they aren't changing concurrently.
            SaveImageToFile(Image8x10, " 8x10 ", specificSubmissionFolder);
            
            if (IsBarongVisible) SaveImageToFile(ImageBarong, " Barong ", specificSubmissionFolder);
            if (IsCreativeVisible) SaveImageToFile(ImageCreative, " Creative ", specificSubmissionFolder);
            if (IsAnyVisible) SaveImageToFile(ImageAny, " Any ", specificSubmissionFolder);
            if (IsInstaxVisible) SaveImageToFile(ImageInstax, " Instax ", specificSubmissionFolder);

            // --- EXCEL LOGGING ---
            string excelPath = Path.Combine(ExcelFolderPath, ExcelFileName);
            if (!excelPath.EndsWith(".xlsx")) excelPath += ".xlsx";

            string? excelDir = Path.GetDirectoryName(excelPath);
            if (!string.IsNullOrEmpty(excelDir) && !Directory.Exists(excelDir))
            {
                Directory.CreateDirectory(excelDir);
            }

            var newItem = new OrderLogItem
            {
                Status = "DONE CHOOSING",
                Name = ClientName?.ToUpper() ?? "UNKNOWN",
                Email = ClientEmail ?? "",
                Package = SelectedPackage,
                Box_8x10 = Image8x10?.FileName ?? "Empty",
                Box_Barong = IsBarongVisible ? (ImageBarong?.FileName ?? "Empty") : "N/A",
                Box_Creative = IsCreativeVisible ? (ImageCreative?.FileName ?? "Empty") : "N/A",
                Box_Any = IsAnyVisible ? (ImageAny?.FileName ?? "Empty") : "N/A",
                Box_Instax = IsInstaxVisible ? (ImageInstax?.FileName ?? "Empty") : "N/A",
                TimeStamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            var allRows = new List<OrderLogItem>();
            
            // Need a lock or try-catch here usually, but for single user app it's fine
            if (File.Exists(excelPath))
            {
                try { allRows.AddRange(MiniExcel.Query<OrderLogItem>(excelPath)); } catch { }
            }

            allRows.Add(newItem);

            if (File.Exists(excelPath)) File.Delete(excelPath);
            MiniExcel.SaveAs(excelPath, allRows);

            System.Diagnostics.Debug.WriteLine($"Saved to: {specificSubmissionFolder}");
        });

        // 4. Hide Loading and Show Thank You (Back on UI Thread)
        IsLoadingSubmit = false;
        IsThankYouDialogVisible = true;
    }
    catch (Exception ex)
    {
        IsLoadingSubmit = false;
        System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
        ErrorMessage = $"An error occurred while saving.\n\nDetails: {ex.Message}";
        IsErrorDialogVisible = true;
    }
}
        
        [RelayCommand] public void CloseThankYouDialog() { IsThankYouDialogVisible = false; ResetData(); }
        [RelayCommand] public void CloseSettings() { CancelSettings(); }
        [RelayCommand] public void CancelSubmit() { IsSubmitConfirmationVisible = false; }
        [RelayCommand] public void CloseErrorDialog() { IsErrorDialogVisible = false; }
        private bool IsValidEmail(string email) { try { return Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase); } catch { return false; } }
        private void ResetData() { ClientName = string.Empty; ClientEmail = string.Empty; SelectedPackage = "Basic"; IsBasicSelected = true; IsPkgASelected = false; IsPkgBSelected = false; IsPkgCSelected = false; IsPkgDSelected = false; SelectedImage = null; PreviewImage = null; Image8x10 = null; ImageBarong = null; ImageCreative = null; ImageAny = null; ImageInstax = null; UpdateVisibility("Basic"); }
        private void SaveImageToFile(ImageItem? image, string sizeCategory, string folderPath) { if (image == null || string.IsNullOrEmpty(image.FullPath)) return; string pkg = SelectedPackage; string name = (ClientName ?? "No name").ToUpper(); string originalName = Path.GetFileNameWithoutExtension(image.FileName); string extension = Path.GetExtension(image.FileName); string newFileName = $"({pkg}) {name}{sizeCategory}{originalName}{extension}"; foreach (char c in Path.GetInvalidFileNameChars()) newFileName = newFileName.Replace(c, '_'); File.Copy(image.FullPath, Path.Combine(folderPath, newFileName), true); }

        public ObservableCollection<ImageItem> Images { get; } = new();

        // ✅ UPDATED: Load Thumbnails only for browsing area
        // In MainWindowViewModel.cs

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
        
        // 1. Get ALL file paths first (This is extremely fast)
        var files = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly)
                             .Where(s => supportedExtensions.Contains(Path.GetExtension(s).ToLower()))
                             .ToList();

        if (files.Count == 0)
        {
            HasNoImages = true;
            IsLoadingImages = false;
            return;
        }

        // 2. Hide loading spinner immediately so user sees the list start populating
        IsLoadingImages = false;

        // 3. Process images in background
        await Task.Run(() =>
        {
            // Process in chunks to avoid freezing UI (e.g., 20 images at a time)
            int batchSize = 20;
            
            // Use Parallel.ForEach for faster decoding on multi-core Intel CPUs
            // We partition the list into chunks first
            var chunks = files.Chunk(batchSize);

            foreach (var chunk in chunks)
            {
                var processedBatch = new System.Collections.Concurrent.ConcurrentBag<ImageItem>();

                // Decode this chunk in parallel
                Parallel.ForEach(chunk, file =>
                {
                    try
                    {
                        using (var stream = File.OpenRead(file))
                        {
                            // Keep Quality Low (90px) for thumbnails
                            var thumbBitmap = Bitmap.DecodeToWidth(stream, 90);
                            
                            processedBatch.Add(new ImageItem
                            {
                                Bitmap = thumbBitmap,
                                FileName = Path.GetFileName(file),
                                FullPath = file
                            });
                        }
                    }
                    catch { /* Skip corrupted files */ }
                });

                // 4. Update UI Thread in batches
                if (!processedBatch.IsEmpty)
                {
                    // Sort to maintain order (since Parallel might scramble them slightly)
                    var sortedBatch = processedBatch.OrderBy(x => x.FileName).ToList();

                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        foreach (var item in sortedBatch)
                        {
                            Images.Add(item);
                        }
                    });
                }
            }
        });
    }
    catch (Exception ex)
    {
        IsLoadingImages = false;
        System.Diagnostics.Debug.WriteLine($"Error loading images: {ex.Message}");
    }
}
    
    }
}