using System;
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
                // 1. Read all metadata directories from the file
                var directories = ImageMetadataReader.ReadMetadata(filePath);

                // 2. Find the IFD0 directory (where Orientation usually lives)
                var subIfdDirectory = directories.OfType<ExifIfd0Directory>().FirstOrDefault();

                if (subIfdDirectory != null && subIfdDirectory.TryGetInt32(ExifDirectoryBase.TagOrientation, out int orientation))
                {
                    // 3. Map EXIF orientation tag to rotation degrees
                    // 1 = Normal (0)
                    // 3 = Rotated 180
                    // 6 = Rotated 90 CW (The most common for Portrait)
                    // 8 = Rotated 270 CW (90 CCW)
                    switch (orientation)
                    {
                        case 3: return 180;
                        case 6: return 90;
                        case 8: return 270;
                        default: return 0;
                    }
                }
            }
            catch (Exception)
            {
                // If reading fails or tag is missing, assume 0
            }
            return 0;
        }
    }
}