using dbm_select.Utils;
using System.Collections.ObjectModel;
using System.IO;
using dbm_select.Models;
using Avalonia.Media.Imaging;

namespace dbm_select.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {

        // Existing file list (e.g., for non-image files)
        public ObservableCollection<string> Files { get; } = new();

        public void LoadFiles(string folderPath)
        {
            Files.Clear();
            foreach (var file in GetFiles.FromFolder(folderPath))
                Files.Add(Path.GetFileName(file));
        }

        // ✅ New image list for displaying thumbnails
        public ObservableCollection<ImageItem> Images { get; } = new();

        public void LoadImages(string folderPath)
        {
            Images.Clear();

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
                            FileName = Path.GetFileName(file)
                        });
                    }
                    catch
                    {
                        // Optional: log or skip unreadable files
                    }
                }
            }
        }


    }
}
