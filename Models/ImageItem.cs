using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace dbm_select.Models
{
    public partial class ImageItem : ObservableObject
    {
        [ObservableProperty]
        private Bitmap? _bitmap;

        public required string FileName { get; set; }
        public required string FullPath { get; set; }

        // ✅ NEW: Property to store correction angle (0, 90, 180, 270)
        public double RotationAngle { get; set; } = 0;
    }
}