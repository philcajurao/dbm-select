using CommunityToolkit.Mvvm.ComponentModel; // Required for [ObservableProperty]
using dbm_select.Models;
using System.Collections.ObjectModel;
using System.IO;
using Avalonia.Media.Imaging;

namespace dbm_select.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        // This constructor loads images immediately for testing purposes
        public MainWindowViewModel()
        {
            // Replace this path with a real folder on your PC for testing
            LoadImages(@"C:\Users\Phil\Pictures");
        }

        // ✅ 1. Add the SelectedImage property
        // The Toolkit automatically generates "SelectedImage" property from this field
        // and handles the notification to the View.
        [ObservableProperty]
        private ImageItem? _selectedImage;

        // ✅ NEW: Properties to store the images dropped into the specific boxes
        // We name them based on the box they belong to.
        [ObservableProperty] private ImageItem? _image8x16;
        [ObservableProperty] private ImageItem? _imageBarong;
        [ObservableProperty] private ImageItem? _imageToga;
        [ObservableProperty] private ImageItem? _imageAnyPhoto;

        // ✅ NEW: Helper method to assign the image based on the Category string
        public void SetPackageImage(string category, ImageItem image)
        {
            switch (category)
            {
                case "8x16": Image8x16 = image; break;
                case "Barong": ImageBarong = image; break;
                case "Toga": ImageToga = image; break;
                case "Any Photo": ImageAnyPhoto = image; break;
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
                        // Note: Loading full bitmaps here is heavy on memory. 
                        // In production, consider loading thumbnails only.
                        var bitmap = new Bitmap(file);
                        Images.Add(new ImageItem
                        {
                            Bitmap = bitmap,
                            FileName = Path.GetFileName(file)
                        });
                    }
                    catch
                    {
                        // Ignore errors
                    }
                }
            }
        }
    }
}