using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel; // Required for [ObservableProperty]
using CommunityToolkit.Mvvm.Input;          // Required for [RelayCommand]
using dbm_select.Models;
using System;
using System.Collections.ObjectModel;
using System.IO;

namespace dbm_select.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        public MainWindowViewModel()
        {
            LoadImages(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)));

            // Initialize visibility
            UpdateVisibility("Basic Package");
        }

        // Client Data Inputs
        [ObservableProperty] private string? _clientName;
        [ObservableProperty] private string? _clientEmail;

        // Track selected package (Default to Basic)
        [ObservableProperty] private string _selectedPackage = "Basic Package";

        // Selection Preview
        [ObservableProperty] private ImageItem? _selectedImage;

        // ✅ RENAMED: Image Slots to match your specific list
        [ObservableProperty] private ImageItem? _image8x10;      // Was 8x16
        [ObservableProperty] private ImageItem? _imageBarong;
        [ObservableProperty] private ImageItem? _imageCreative;  // Was Toga
        [ObservableProperty] private ImageItem? _imageAny;       // Was Any Photo
        [ObservableProperty] private ImageItem? _imageInstax;

        // ✅ RENAMED: Visibility Flags
        [ObservableProperty] private bool _isBarongVisible;
        [ObservableProperty] private bool _isCreativeVisible; // Was Toga
        [ObservableProperty] private bool _isAnyVisible;
        [ObservableProperty] private bool _isInstaxVisible;

        // Command to update SelectedPackage when a RadioButton is clicked
        [RelayCommand]
        public void UpdatePackage(string packageName)
        {
            SelectedPackage = packageName;
            UpdateVisibility(packageName);
        }

        // ✅ LOGIC UPDATE: Separated C and D logic
        private void UpdateVisibility(string pkg)
        {
            // 1. Reset all to Hidden
            IsBarongVisible = false;
            IsCreativeVisible = false;
            IsAnyVisible = false;
            IsInstaxVisible = false;

            // 2. Configure based on Package List

            // Basic: 8x10 Only (Default)

            // Package A & B: 8x10 + Barong
            if (pkg == "Package A" || pkg == "Package B")
            {
                IsBarongVisible = true;
            }
            // Package C: 8x10 + Barong + Creative + Any (NO INSTAX)
            else if (pkg == "Package C")
            {
                IsBarongVisible = true;
                IsCreativeVisible = true;
                IsAnyVisible = true;
                IsInstaxVisible = false;
            }
            // Package D: 8x10 + Barong + Creative + Any + INSTAX
            else if (pkg == "Package D")
            {
                IsBarongVisible = true;
                IsCreativeVisible = true;
                IsAnyVisible = true;
                IsInstaxVisible = true; // ✅ Instax only for D
            }
        }

        // Helper method to assign the image based on the Category string
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

        // Clear Slot Command
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

        // Clear All Command
        [RelayCommand]
        public void ClearAll()
        {
            ClientName = string.Empty;
            ClientEmail = string.Empty;
            SelectedPackage = "Basic Package";

            SelectedImage = null;

            // Reset Slots
            Image8x10 = null;
            ImageBarong = null;
            ImageCreative = null;
            ImageAny = null;
            ImageInstax = null;

            // Reset visibility to Basic
            UpdateVisibility("Basic Package");
        }

        // Submit Command
        [RelayCommand]
        public void Submit()
        {
            string outputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "DBM_Select");

            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            // Save Images with updated labels
            SaveImageToFile(Image8x10, " 8x10 ", outputFolder);
            SaveImageToFile(ImageBarong, " Barong ", outputFolder);
            SaveImageToFile(ImageCreative, " Creative ", outputFolder);
            SaveImageToFile(ImageAny, " Any ", outputFolder);
            SaveImageToFile(ImageInstax, " Instax ", outputFolder);

            System.Diagnostics.Debug.WriteLine($"Saved images to {outputFolder}");
            ClearAll();
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
                {
                    newFileName = newFileName.Replace(c, '_');
                }

                string destPath = Path.Combine(folderPath, newFileName);
                File.Copy(image.FullPath, destPath, true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving image: {ex.Message}");
            }
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
                        var bitmap = new Bitmap(file);
                        Images.Add(new ImageItem
                        {
                            Bitmap = bitmap,
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