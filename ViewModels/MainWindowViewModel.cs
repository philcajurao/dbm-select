using Avalonia.Controls;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using dbm_select.Models;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions; // ✅ Required for Email Validation

namespace dbm_select.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        public MainWindowViewModel()
        {
            if (Design.IsDesignMode) return;

            LoadImages(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)));
            UpdateVisibility("Basic Package");
        }

        // Client Data Inputs
        [ObservableProperty] private string? _clientName;
        [ObservableProperty] private string? _clientEmail;

        // Track selected package (Default to Basic)
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

        // Validation Error Dialog
        [ObservableProperty] private bool _isErrorDialogVisible;
        [ObservableProperty] private string _errorMessage = "Please check your inputs.";

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

        // --- Submit Logic ---

        [RelayCommand]
        public void Submit()
        {
            // 1. VALIDATE: Check Name and Email Existence
            if (string.IsNullOrWhiteSpace(ClientName) || string.IsNullOrWhiteSpace(ClientEmail))
            {
                ErrorMessage = "Please enter both the Client Name and Email Address.";
                IsErrorDialogVisible = true;
                return;
            }

            // ✅ 2. VALIDATE: Check Email Format
            if (!IsValidEmail(ClientEmail))
            {
                ErrorMessage = "The Email Address format is invalid.\n(e.g., user@example.com)";
                IsErrorDialogVisible = true;
                return;
            }

            // 3. VALIDATE: Check if required image slots are filled
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

            // If all checks pass, show the confirm dialog
            IsSubmitConfirmationVisible = true;
        }

        // ✅ Helper function for Email Regex
        private bool IsValidEmail(string email)
        {
            try
            {
                // Simple Regex for Email Validation
                return Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        [RelayCommand]
        public void ConfirmSubmit()
        {
            string outputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "DBM_Select");

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