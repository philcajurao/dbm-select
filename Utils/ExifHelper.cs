using System;
using System.IO;

namespace dbm_select.Utils
{
    public static class ExifHelper
    {
        public static double GetOrientationAngle(string filePath)
        {
            try
            {
                using (var stream = File.OpenRead(filePath))
                {
                    using (var reader = new BinaryReader(stream))
                    {
                        // 1. Verify JPEG Marker (FF D8)
                        if (reader.ReadByte() != 0xFF || reader.ReadByte() != 0xD8) return 0;

                        while (stream.Position < stream.Length)
                        {
                            // 2. Read next Marker
                            byte marker = 0;
                            while (stream.Position < stream.Length)
                            {
                                if (reader.ReadByte() == 0xFF)
                                {
                                    marker = reader.ReadByte();
                                    if (marker != 0xFF && marker != 0x00) break; // Found a valid marker
                                }
                            }

                            if (marker == 0) break;

                            // Read Length of the segment
                            ushort length = ReadBigEndianUInt16(reader);
                            long nextSegmentStart = stream.Position + length - 2;

                            // 3. Check for APP1 Marker (FF E1) which contains Exif
                            if (marker == 0xE1)
                            {
                                // 4. Check "Exif\0\0" header (6 bytes)
                                if (length >= 8 &&
                                    reader.ReadByte() == 0x45 && reader.ReadByte() == 0x78 &&
                                    reader.ReadByte() == 0x69 && reader.ReadByte() == 0x66 &&
                                    reader.ReadByte() == 0x00 && reader.ReadByte() == 0x00)
                                {
                                    // 5. Read TIFF Header
                                    long tiffStart = stream.Position;

                                    // Byte Order: II (0x4949) = Little Endian, MM (0x4D4D) = Big Endian
                                    ushort byteOrder = ReadBigEndianUInt16(reader);
                                    bool isLittleEndian = byteOrder == 0x4949;

                                    // Magic Number (42)
                                    ushort magic = isLittleEndian ? ReadLittleEndianUInt16(reader) : ReadBigEndianUInt16(reader);

                                    // Offset to First IFD
                                    uint offsetToIFD = isLittleEndian ? ReadLittleEndianUInt32(reader) : ReadBigEndianUInt32(reader);

                                    // Jump to IFD
                                    if (offsetToIFD > 0)
                                    {
                                        stream.Position = tiffStart + offsetToIFD;

                                        // 6. Read Number of Entries
                                        ushort entries = isLittleEndian ? ReadLittleEndianUInt16(reader) : ReadBigEndianUInt16(reader);

                                        for (int i = 0; i < entries; i++)
                                        {
                                            // Tag (2), Type (2), Count (4), Value/Offset (4)
                                            ushort tag = isLittleEndian ? ReadLittleEndianUInt16(reader) : ReadBigEndianUInt16(reader);
                                            ushort type = isLittleEndian ? ReadLittleEndianUInt16(reader) : ReadBigEndianUInt16(reader);
                                            uint count = isLittleEndian ? ReadLittleEndianUInt32(reader) : ReadBigEndianUInt32(reader);

                                            // 7. Check for Orientation Tag (0x0112)
                                            if (tag == 0x0112)
                                            {
                                                ushort orientation = isLittleEndian ? ReadLittleEndianUInt16(reader) : ReadBigEndianUInt16(reader);

                                                // 1=0, 3=180, 6=90 CW, 8=270 CW (90 CCW)
                                                switch (orientation)
                                                {
                                                    case 3: return 180;
                                                    case 6: return 90;
                                                    case 8: return 270;
                                                    default: return 0;
                                                }
                                            }
                                            else
                                            {
                                                // Skip the value/offset (4 bytes) - we already read tag(2)+type(2)+count(4) = 8 bytes
                                                reader.ReadUInt32();
                                            }
                                        }
                                    }
                                }
                                // If we found APP1 but parsed it and didn't find orientation, just stop looking
                                break;
                            }

                            // Move to next segment
                            stream.Position = nextSegmentStart;
                        }
                    }
                }
            }
            catch
            {
                // Fail silently to 0 rotation
            }
            return 0;
        }

        private static ushort ReadBigEndianUInt16(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(2);
            if (bytes.Length < 2) return 0;
            return (ushort)((bytes[0] << 8) | bytes[1]);
        }

        private static ushort ReadLittleEndianUInt16(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(2);
            if (bytes.Length < 2) return 0;
            return (ushort)((bytes[1] << 8) | bytes[0]);
        }

        private static uint ReadBigEndianUInt32(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(4);
            if (bytes.Length < 4) return 0;
            return (uint)((bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3]);
        }

        private static uint ReadLittleEndianUInt32(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(4);
            if (bytes.Length < 4) return 0;
            return (uint)((bytes[3] << 24) | (bytes[2] << 16) | (bytes[1] << 8) | bytes[0]);
        }
    }
}