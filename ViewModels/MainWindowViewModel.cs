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
using System.Collections.Generic; // Added for List<>
using System.Linq;

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

            LoadImages(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)));

            if (!LoadSettings())
            {
                OutputFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "DBM_Select");
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

        [ObservableProperty] private string _excelFileName = "Order_Log";
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
        [ObservableProperty] private string _selectedPackage = "Basic";

        [ObservableProperty] private bool _isBasicSelected = true;
        [ObservableProperty] private bool _isPkgASelected;
        [ObservableProperty] private bool _isPkgBSelected;
        [ObservableProperty] private bool _isPkgCSelected;
        [ObservableProperty] private bool _isPkgDSelected;

        [ObservableProperty] private ImageItem? _selectedImage;

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

        [ObservableProperty] private bool _isImportantNotesDialogVisible;

        // ✅ NEW: Acknowledgement Dialog Properties
        [ObservableProperty] private bool _isAcknowledgementDialogVisible;
        [ObservableProperty] private bool _isImportantNotesChecked; // Checkbox state

        [ObservableProperty] private string _errorMessage = "Please check your inputs.";

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
                        if (!string.IsNullOrEmpty(settings.LastOutputFolder))
                            OutputFolderPath = settings.LastOutputFolder;

                        if (!string.IsNullOrEmpty(settings.LastExcelFolder))
                            ExcelFolderPath = settings.LastExcelFolder;
                        else
                            ExcelFolderPath = OutputFolderPath;

                        if (!string.IsNullOrEmpty(settings.LastExcelFileName))
                            ExcelFileName = settings.LastExcelFileName;

                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
            }
            return false;
        }

        private void SaveSettings()
        {
            try
            {
                string? dir = Path.GetDirectoryName(_settingsFilePath);
                if (dir != null && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var settings = new AppSettings
                {
                    LastOutputFolder = OutputFolderPath,
                    LastExcelFolder = ExcelFolderPath,
                    LastExcelFileName = ExcelFileName
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
        }

        // --- LOGIC COMMANDS ---

        [RelayCommand]
        public void UpdatePackage(string packageName)
        {
            SelectedPackage = packageName;
            UpdateVisibility(packageName);
        }

        private void UpdateVisibility(string pkg)
        {
            IsBarongVisible = false;
            IsCreativeVisible = false;
            IsAnyVisible = false;
            IsInstaxVisible = false;

            if (pkg == "A" || pkg == "B")
            {
                IsBarongVisible = true;
            }
            else if (pkg == "C")
            {
                IsBarongVisible = true;
                IsCreativeVisible = true;
                IsAnyVisible = true;
            }
            else if (pkg == "D")
            {
                IsBarongVisible = true;
                IsCreativeVisible = true;
                IsAnyVisible = true;
                IsInstaxVisible = true;
            }
        }

        public void SetPackageImage(string category, ImageItem image)
        {
            switch (category)
            {
                case "8x10": Image8x10 = image; break;
                case "Barong": ImageBarong = image; break;
                case "Creative": ImageCreative = image; break;
                case "Any": ImageAny = image; break;
                case "Instax": ImageInstax = image; break;
            }
        }

        [RelayCommand]
        public void ClearSlot(string category)
        {
            switch (category)
            {
                case "8x10": Image8x10 = null; break;
                case "Barong": ImageBarong = null; break;
                case "Creative": ImageCreative = null; break;
                case "Any": ImageAny = null; break;
                case "Instax": ImageInstax = null; break;
            }
        }

        [RelayCommand]
        public void ClearAll()
        {
            IsClearConfirmationVisible = true;
        }

        [RelayCommand]
        public void ConfirmClear()
        {
            ResetData();
            IsClearConfirmationVisible = false;
        }

        [RelayCommand]
        public void CancelClear()
        {
            IsClearConfirmationVisible = false;
        }

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

        [RelayCommand]
        public void SaveAndCloseSettings()
        {
            SaveSettings();
            IsSettingsDialogVisible = false;
        }

        // --- Submit Logic ---

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

            bool isMissingImages = false;
            if (Image8x10 == null) isMissingImages = true;
            else if (IsBarongVisible && ImageBarong == null) isMissingImages = true;
            else if (IsCreativeVisible && ImageCreative == null) isMissingImages = true;
            else if (IsAnyVisible && ImageAny == null) isMissingImages = true;
            else if (IsInstaxVisible && ImageInstax == null) isMissingImages = true;

            if (isMissingImages)
            {
                ErrorMessage = $"Your selected package ({SelectedPackage}) requires all photo slots to be filled.";
                IsErrorDialogVisible = true;
                return;
            }

            IsSubmitConfirmationVisible = true;
        }

        // Step 1: Confirm Submit -> Open Notes
        [RelayCommand]
        public void ConfirmSubmit()
        {
            IsSubmitConfirmationVisible = false;
            IsImportantNotesDialogVisible = true;
        }

        // ✅ NEW: Step 2: Continue from Notes -> Open Acknowledgement
        [RelayCommand]
        public void ContinueFromNotes()
        {
            IsImportantNotesDialogVisible = false;
            IsImportantNotesChecked = false; // Reset Checkbox
            IsAcknowledgementDialogVisible = true;
        }

        [RelayCommand]
        public void CancelNotes()
        {
            IsImportantNotesDialogVisible = false;
        }

        // ✅ NEW: Cancel Acknowledgement
        [RelayCommand]
        public void CancelAcknowledgement()
        {
            IsAcknowledgementDialogVisible = false;
        }

        // ✅ NEW: Step 3: Proceed from Acknowledgement -> Save Data
        [RelayCommand]
        public void ProceedFromAcknowledgement()
        {
            string outputFolder = OutputFolderPath;
            IsAcknowledgementDialogVisible = false;

            try
            {
                if (!Directory.Exists(outputFolder)) Directory.CreateDirectory(outputFolder);

                SaveImageToFile(Image8x10, " 8x10 ", outputFolder);
                if (IsBarongVisible) SaveImageToFile(ImageBarong, " Barong ", outputFolder);
                if (IsCreativeVisible) SaveImageToFile(ImageCreative, " Creative ", outputFolder);
                if (IsAnyVisible) SaveImageToFile(ImageAny, " Any ", outputFolder);
                if (IsInstaxVisible) SaveImageToFile(ImageInstax, " Instax ", outputFolder);

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

                if (File.Exists(excelPath))
                {
                    try { allRows.AddRange(MiniExcel.Query<OrderLogItem>(excelPath)); }
                    catch { }
                }

                allRows.Add(newItem);

                if (File.Exists(excelPath)) File.Delete(excelPath);

                MiniExcel.SaveAs(excelPath, allRows);

                System.Diagnostics.Debug.WriteLine($"Saved images to {outputFolder} and log to {excelPath}");

                // Show Thank You Dialog
                IsThankYouDialogVisible = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
                ErrorMessage = $"An error occurred while saving.\n\nIf the Excel file is open, please CLOSE it and try again.\n\nDetails: {ex.Message}";
                IsErrorDialogVisible = true;
            }
        }

        [RelayCommand]
        public void CloseThankYouDialog()
        {
            IsThankYouDialogVisible = false;
            ResetData();
        }

        [RelayCommand]
        public void CloseSettings()
        {
            CancelSettings();
        }

        [RelayCommand]
        public void CancelSubmit()
        {
            IsSubmitConfirmationVisible = false;
        }

        [RelayCommand]
        public void CloseErrorDialog()
        {
            IsErrorDialogVisible = false;
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                return Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase);
            }
            catch { return false; }
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
            Image8x10 = null;
            ImageBarong = null;
            ImageCreative = null;
            ImageAny = null;
            ImageInstax = null;

            UpdateVisibility("Basic");
        }

        private void SaveImageToFile(ImageItem? image, string sizeCategory, string folderPath)
        {
            if (image == null || string.IsNullOrEmpty(image.FullPath)) return;

            string pkg = SelectedPackage;
            string name = (ClientName ?? "No name").ToUpper();
            string originalName = Path.GetFileNameWithoutExtension(image.FileName);
            string extension = Path.GetExtension(image.FileName);

            string newFileName = $"({pkg}) {name}{sizeCategory}{originalName}{extension}";

            foreach (char c in Path.GetInvalidFileNameChars())
                newFileName = newFileName.Replace(c, '_');

            File.Copy(image.FullPath, Path.Combine(folderPath, newFileName), true);
        }

        public ObservableCollection<ImageItem> Images { get; } = new();

        public void LoadImages(string folderPath)
        {
            Images.Clear();
            if (!Directory.Exists(folderPath)) return;

            var supportedExtensions = new[] { "*.jpg", "*.jpeg", "*.png" };
            foreach (var ext in supportedExtensions)
            {
                foreach (var file in Directory.GetFiles(folderPath, ext))
                {
                    try
                    {
                        Images.Add(new ImageItem
                        {
                            Bitmap = new Bitmap(file),
                            FileName = Path.GetFileName(file),
                            FullPath = file
                        });
                    }
                    catch { }
                }
            }
        }
    }
}