using Avalonia.Media.Imaging;

namespace dbm_select.Models
{
    public class ImageItem
    {
        public required Bitmap Bitmap { get; set; }
        public required string FileName { get; set; }
        public required string FullPath { get; set; }

        // ✅ NEW: Property to store correction angle (0, 90, 180, 270)
        public double RotationAngle { get; set; } = 0;
    }
}