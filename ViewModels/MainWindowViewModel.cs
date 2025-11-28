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

            // Initial load
            _ = LoadImages(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)));

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

            UpdateVisibility("Basic");
        }

        // --- PATH PROPERTIES ---
        private string _snapOutputFolder = string.Empty;
        private string _snapExcelFolder = string.Empty;
        private string _snapExcelFileName = string.Empty;

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
                var settings = new AppSettings { LastOutputFolder = OutputFolderPath, LastExcelFolder = ExcelFolderPath, LastExcelFileName = ExcelFileName };
                string json = JsonSerializer.Serialize(settings);
                File.WriteAllText(_settingsFilePath, json);
            }
            catch (Exception) { }
        }

        public class AppSettings { public string? LastOutputFolder { get; set; } public string? LastExcelFolder { get; set; } public string? LastExcelFileName { get; set; } }

        // --- LOGIC COMMANDS ---
        [RelayCommand] public void UpdatePackage(string packageName) { SelectedPackage = packageName; UpdateVisibility(packageName); }
        private void UpdateVisibility(string pkg)
        {
            IsBarongVisible = false; IsCreativeVisible = false; IsAnyVisible = false; IsInstaxVisible = false;
            if (pkg == "A" || pkg == "B") { IsBarongVisible = true; }
            else if (pkg == "C") { IsBarongVisible = true; IsCreativeVisible = true; IsAnyVisible = true; }
            else if (pkg == "D") { IsBarongVisible = true; IsCreativeVisible = true; IsAnyVisible = true; IsInstaxVisible = true; }
        }
        public void SetPackageImage(string category, ImageItem image)
        {
            switch (category) { case "8x10": Image8x10 = image; break; case "Barong": ImageBarong = image; break; case "Creative": ImageCreative = image; break; case "Any": ImageAny = image; break; case "Instax": ImageInstax = image; break; }
        }
        [RelayCommand]
        public void ClearSlot(string category)
        {
            switch (category) { case "8x10": Image8x10 = null; break; case "Barong": ImageBarong = null; break; case "Creative": ImageCreative = null; break; case "Any": ImageAny = null; break; case "Instax": ImageInstax = null; break; }
        }
        [RelayCommand] public void ClearAll() { IsClearConfirmationVisible = true; }
        [RelayCommand] public void ConfirmClear() { ResetData(); IsClearConfirmationVisible = false; }
        [RelayCommand] public void CancelClear() { IsClearConfirmationVisible = false; }
        [RelayCommand] public void OpenSettings() { _snapOutputFolder = OutputFolderPath; _snapExcelFolder = ExcelFolderPath; _snapExcelFileName = ExcelFileName; IsSettingsDirty = false; IsSettingsDialogVisible = true; }
        [RelayCommand] public void CancelSettings() { OutputFolderPath = _snapOutputFolder; ExcelFolderPath = _snapExcelFolder; ExcelFileName = _snapExcelFileName; IsSettingsDialogVisible = false; }
        [RelayCommand] public void SaveAndCloseSettings() { SaveSettings(); IsSettingsDialogVisible = false; }
        [RelayCommand] public void OpenAbout() { IsAboutDialogVisible = true; }
        [RelayCommand] public void CloseAbout() { IsAboutDialogVisible = false; }
        [RelayCommand] public void OpenPreviewPackage() { IsPreviewPackageDialogVisible = true; }
        [RelayCommand] public void ClosePreviewPackage() { IsPreviewPackageDialogVisible = false; }
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
        [RelayCommand]
        public void ProceedFromAcknowledgement()
        {
            string outputFolder = OutputFolderPath; IsAcknowledgementDialogVisible = false;
            try
            {
                if (!Directory.Exists(outputFolder)) Directory.CreateDirectory(outputFolder);
                SaveImageToFile(Image8x10, " 8x10 ", outputFolder); if (IsBarongVisible) SaveImageToFile(ImageBarong, " Barong ", outputFolder); if (IsCreativeVisible) SaveImageToFile(ImageCreative, " Creative ", outputFolder); if (IsAnyVisible) SaveImageToFile(ImageAny, " Any ", outputFolder); if (IsInstaxVisible) SaveImageToFile(ImageInstax, " Instax ", outputFolder);
                string excelPath = Path.Combine(ExcelFolderPath, ExcelFileName); if (!excelPath.EndsWith(".xlsx")) excelPath += ".xlsx";
                string? excelDir = Path.GetDirectoryName(excelPath); if (!string.IsNullOrEmpty(excelDir) && !Directory.Exists(excelDir)) { Directory.CreateDirectory(excelDir); }
                var newItem = new OrderLogItem { Status = "DONE CHOOSING", Name = ClientName?.ToUpper() ?? "UNKNOWN", Email = ClientEmail ?? "", Package = SelectedPackage, Box_8x10 = Image8x10?.FileName ?? "Empty", Box_Barong = IsBarongVisible ? (ImageBarong?.FileName ?? "Empty") : "N/A", Box_Creative = IsCreativeVisible ? (ImageCreative?.FileName ?? "Empty") : "N/A", Box_Any = IsAnyVisible ? (ImageAny?.FileName ?? "Empty") : "N/A", Box_Instax = IsInstaxVisible ? (ImageInstax?.FileName ?? "Empty") : "N/A", TimeStamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") };
                var allRows = new List<OrderLogItem>();
                if (File.Exists(excelPath)) { try { allRows.AddRange(MiniExcel.Query<OrderLogItem>(excelPath)); } catch { } }
                allRows.Add(newItem); if (File.Exists(excelPath)) File.Delete(excelPath); MiniExcel.SaveAs(excelPath, allRows);
                System.Diagnostics.Debug.WriteLine($"Saved images to {outputFolder} and log to {excelPath}"); IsThankYouDialogVisible = true;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}"); ErrorMessage = $"An error occurred while saving.\n\nIf the Excel file is open, please CLOSE it and try again.\n\nDetails: {ex.Message}"; IsErrorDialogVisible = true; }
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
        public async Task LoadImages(string folderPath)
        {
            Images.Clear();
            if (!Directory.Exists(folderPath)) return;

            IsLoadingImages = true;
            HasNoImages = false;

            try
            {
                var supportedExtensions = new[] { "*.jpg", "*.jpeg", "*.png" };
                var loadedImages = await Task.Run(() =>
                {
                    var list = new List<ImageItem>();
                    foreach (var ext in supportedExtensions)
                    {
                        foreach (var file in Directory.GetFiles(folderPath, ext))
                        {
                            try
                            {
                                using (var stream = File.OpenRead(file))
                                {
                                    // ✅ KEY FIX: Reduced decode width to 100px (was 200px)
                                    // This drastically lowers memory usage and speeds up loading.
                                    // Since your tiles are about 80px wide, 100px is perfect.
                                    var thumbBitmap = Bitmap.DecodeToWidth(stream, 100);

                                    list.Add(new ImageItem
                                    {
                                        Bitmap = thumbBitmap,
                                        FileName = Path.GetFileName(file) ?? "Unknown",
                                        FullPath = file
                                    });
                                }
                            }
                            catch { }
                        }
                    }
                    return list;
                });

                foreach (var img in loadedImages)
                {
                    Images.Add(img);
                }
            }
            finally
            {
                IsLoadingImages = false;
                HasNoImages = Images.Count == 0;
            }
        }
    }
}