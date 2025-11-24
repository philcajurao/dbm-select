using Avalonia.Controls;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using dbm_select.Models;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Text.Json; // ✅ NEW: Required for saving settings

namespace dbm_select.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        // Path to store the settings file (e.g., AppData/Roaming/DBM_Select/settings.json)
        private readonly string _settingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DBM_Select",
            "settings.json");

        public MainWindowViewModel()
        {
            if (Design.IsDesignMode) return;

            LoadImages(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)));

            // ✅ CHANGED: Try to load saved settings first. If fails, use default.
            if (!LoadSettings())
            {
                OutputFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "DBM_Select");
            }

            UpdateVisibility("Basic Package");
        }

        // Output Folder Path
        [ObservableProperty] private string _outputFolderPath;

        // ✅ NEW: Auto-save whenever the OutputFolderPath property changes
        partial void OnOutputFolderPathChanged(string value)
        {
            SaveSettings();
        }

        // Client Data Inputs
        [ObservableProperty] private string? _clientName;
        [ObservableProperty] private string? _clientEmail;

        // Track selected package
        [ObservableProperty] private string _selectedPackage = "Basic Package";

        // Radio Button Selection States
        [ObservableProperty] private bool _isBasicSelected = true;
        [ObservableProperty] private bool _isPkgASelected;
        [ObservableProperty] private bool _isPkgBSelected;
        [ObservableProperty] private bool _isPkgCSelected;
        [ObservableProperty] private bool _isPkgDSelected;

        // Selection Preview
        [ObservableProperty] private ImageItem? _selectedImage;

        // Image Slots
        [ObservableProperty] private ImageItem? _image8x10;
        [ObservableProperty] private ImageItem? _imageBarong;
        [ObservableProperty] private ImageItem? _imageCreative;
        [ObservableProperty] private ImageItem? _imageAny;
        [ObservableProperty] private ImageItem? _imageInstax;

        // Visibility Flags
        [ObservableProperty] private bool _isBarongVisible;
        [ObservableProperty] private bool _isCreativeVisible;
        [ObservableProperty] private bool _isAnyVisible;
        [ObservableProperty] private bool _isInstaxVisible;

        // Confirmation Dialog Flags
        [ObservableProperty] private bool _isClearConfirmationVisible;
        [ObservableProperty] private bool _isSubmitConfirmationVisible;
        [ObservableProperty] private bool _isErrorDialogVisible;

        // Settings Dialog Flag
        [ObservableProperty] private bool _isSettingsDialogVisible;

        [ObservableProperty] private string _errorMessage = "Please check your inputs.";

        // --- Settings Persistence Logic ---

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
                // Ensure directory exists
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
                System.Diagnostics.Debug.WriteLine($"Settings saved to {_settingsFilePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }

        // Simple internal class to hold settings data
        public class AppSettings
        {
            public string? LastOutputFolder { get; set; }
        }

        // --- End Persistence Logic ---

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

            if (pkg == "Package A" || pkg == "Package B")
            {
                IsBarongVisible = true;
            }
            else if (pkg == "Package C")
            {
                IsBarongVisible = true;
                IsCreativeVisible = true;
                IsAnyVisible = true;
            }
            else if (pkg == "Package D")
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

        // --- Clear All Logic ---
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

        // --- Settings Logic ---
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

        [RelayCommand]
        public void ConfirmSubmit()
        {
            string outputFolder = OutputFolderPath;

            if (!Directory.Exists(outputFolder)) Directory.CreateDirectory(outputFolder);

            SaveImageToFile(Image8x10, " 8x10 ", outputFolder);
            if (IsBarongVisible) SaveImageToFile(ImageBarong, " Barong ", outputFolder);
            if (IsCreativeVisible) SaveImageToFile(ImageCreative, " Creative ", outputFolder);
            if (IsAnyVisible) SaveImageToFile(ImageAny, " Any ", outputFolder);
            if (IsInstaxVisible) SaveImageToFile(ImageInstax, " Instax ", outputFolder);

            System.Diagnostics.Debug.WriteLine($"Saved images to {outputFolder}");

            IsSubmitConfirmationVisible = false;
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
            SelectedPackage = "Basic Package";

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

            UpdateVisibility("Basic Package");
        }

        private void SaveImageToFile(ImageItem? image, string sizeCategory, string folderPath)
        {
            if (image == null || string.IsNullOrEmpty(image.FullPath)) return;

            try
            {
                string pkg = SelectedPackage;
                string name = ClientName ?? "No name";
                string originalName = Path.GetFileNameWithoutExtension(image.FileName);
                string extension = Path.GetExtension(image.FileName);

                string newFileName = $"({pkg}) {name}{sizeCategory}{originalName}{extension}";

                foreach (char c in Path.GetInvalidFileNameChars())
                    newFileName = newFileName.Replace(c, '_');

                File.Copy(image.FullPath, Path.Combine(folderPath, newFileName), true);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}"); }
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