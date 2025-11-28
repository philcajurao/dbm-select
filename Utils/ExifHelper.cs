using System;
using System.IO; // ✅ Added for FileStream
using System.Linq;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace dbm_select.Utils
{
    public static class ExifHelper
    {
        public static double GetOrientationAngle(string filePath)
        {
            try
            {
                // ✅ Use explicit FileStream with FileShare.ReadWrite to prevent file locking issues
                // during parallel processing. This ensures the handle is released immediately.
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    // 1. Read all metadata directories from the stream
                    var directories = ImageMetadataReader.ReadMetadata(stream);

                    // 2. Find the IFD0 directory (where Orientation usually lives)
                    var subIfdDirectory = directories.OfType<ExifIfd0Directory>().FirstOrDefault();

                    if (subIfdDirectory != null && subIfdDirectory.TryGetInt32(ExifDirectoryBase.TagOrientation, out int orientation))
                    {
                        // 3. Map EXIF orientation tag to rotation degrees
                        switch (orientation)
                        {
                            case 3: return 180;
                            case 6: return 90;
                            case 8: return 270;
                            default: return 0;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // If reading fails or tag is missing, assume 0 so the image still loads (just unrotated)
            }
            return 0;
        }
    }
}