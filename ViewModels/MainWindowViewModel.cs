using Avalonia.Controls;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using dbm_select.Models;
using MiniExcelLibs; // ✅ REQUIRED
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Text.Json;
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

            // ✅ UPDATED: Use "Basic"
            UpdateVisibility("Basic");
        }

        [ObservableProperty] private string _outputFolderPath = string.Empty;

        partial void OnOutputFolderPathChanged(string value)
        {
            SaveSettings();
        }

        [ObservableProperty] private string? _clientName;
        [ObservableProperty] private string? _clientEmail;

        // ✅ UPDATED: Default to "Basic"
        [ObservableProperty] private string _selectedPackage = "Basic";

        // Radio Button Selection States
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

        [ObservableProperty] private bool _isClearConfirmationVisible;
        [ObservableProperty] private bool _isSubmitConfirmationVisible;
        [ObservableProperty] private bool _isErrorDialogVisible;
        [ObservableProperty] private bool _isSettingsDialogVisible;
        [ObservableProperty] private bool _isThankYouDialogVisible;

        [ObservableProperty] private string _errorMessage = "Please check your inputs.";

        private bool LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);

                    if (settings != null && !string.IsNullOrEmpty(settings.LastOutputFolder))
                    {
                        OutputFolderPath = settings.LastOutputFolder;
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
                    LastOutputFolder = OutputFolderPath
                };

                string json = JsonSerializer.Serialize(settings);
                File.WriteAllText(_settingsFilePath, json);
            }
            catch (Exception) { }
        }

        public class AppSettings
        {
            public string? LastOutputFolder { get; set; }
        }

        [RelayCommand]
        public void UpdatePackage(string packageName)
        {
            SelectedPackage = packageName;
            UpdateVisibility(packageName);
        }

        // ✅ UPDATED: Check for short names "A", "B", "C", "D"
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
            IsSettingsDialogVisible = true;
        }

        [RelayCommand]
        public void CloseSettings()
        {
            IsSettingsDialogVisible = false;
        }

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

        [RelayCommand]
        public void ConfirmSubmit()
        {
            string outputFolder = OutputFolderPath;
            IsSubmitConfirmationVisible = false;

            try
            {
                if (!Directory.Exists(outputFolder)) Directory.CreateDirectory(outputFolder);

                // 1. Save Image Files
                SaveImageToFile(Image8x10, " 8x10 ", outputFolder);
                if (IsBarongVisible) SaveImageToFile(ImageBarong, " Barong ", outputFolder);
                if (IsCreativeVisible) SaveImageToFile(ImageCreative, " Creative ", outputFolder);
                if (IsAnyVisible) SaveImageToFile(ImageAny, " Any ", outputFolder);
                if (IsInstaxVisible) SaveImageToFile(ImageInstax, " Instax ", outputFolder);

                // 2. SAVE TO EXCEL LOGIC
                string excelPath = Path.Combine(outputFolder, "Order_Log.xlsx");

                // Create new item
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

                // Read existing data if file exists
                if (File.Exists(excelPath))
                {
                    try
                    {
                        allRows.AddRange(MiniExcel.Query<OrderLogItem>(excelPath));
                    }
                    catch
                    {
                        // Ignore read errors, we will just overwrite 
                    }
                }

                // Add new row
                allRows.Add(newItem);

                // Delete old file to ensure overwrite
                if (File.Exists(excelPath))
                {
                    File.Delete(excelPath);
                }

                // Save fresh file with headers
                MiniExcel.SaveAs(excelPath, allRows);

                System.Diagnostics.Debug.WriteLine($"Saved images and log to {outputFolder}");

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
            // ✅ UPDATED: Use "Basic"
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

            // ✅ UPDATED: Use "Basic"
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