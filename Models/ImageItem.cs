using Avalonia.Media.Imaging;

namespace dbm_select.Models
{
    public class ImageItem
    {
        public required Bitmap Bitmap { get; set; }
        public required string FileName { get; set; }
        public required string FullPath { get; set; }
    }
}