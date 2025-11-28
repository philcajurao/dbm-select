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
                            // 2. Read Marker
                            if (reader.ReadByte() != 0xFF) break;
                            byte marker = reader.ReadByte();
                            ushort length = ReadBigEndianUInt16(reader); // Length includes the 2 bytes for length itself

                            // 3. Check for APP1 Marker (FF E1) which contains Exif
                            if (marker == 0xE1)
                            {
                                long nextTagStart = stream.Position + length - 2;

                                // 4. Check "Exif\0\0" header
                                if (reader.ReadByte() == 0x45 && reader.ReadByte() == 0x78 &&
                                    reader.ReadByte() == 0x69 && reader.ReadByte() == 0x66 &&
                                    reader.ReadByte() == 0x00 && reader.ReadByte() == 0x00)
                                {
                                    // 5. Read TIFF Header
                                    long tiffStart = stream.Position;
                                    ushort byteOrder = reader.ReadUInt16(); // II (0x4949) or MM (0x4D4D)
                                    bool isLittleEndian = byteOrder == 0x4949;

                                    reader.ReadUInt16(); // 42 (Magic number)
                                    uint offsetToIFD = isLittleEndian ? reader.ReadUInt32() : ReadBigEndianUInt32(reader);

                                    // Jump to IFD
                                    stream.Position = tiffStart + offsetToIFD;

                                    // 6. Read Number of Entries
                                    ushort entries = isLittleEndian ? reader.ReadUInt16() : ReadBigEndianUInt16(reader);

                                    for (int i = 0; i < entries; i++)
                                    {
                                        ushort tag = isLittleEndian ? reader.ReadUInt16() : ReadBigEndianUInt16(reader);
                                        ushort type = isLittleEndian ? reader.ReadUInt16() : ReadBigEndianUInt16(reader);
                                        uint count = isLittleEndian ? reader.ReadUInt32() : ReadBigEndianUInt32(reader);
                                        // Value offset (4 bytes) - for Short(3), it contains the value directly

                                        // 7. Check for Orientation Tag (0x0112)
                                        if (tag == 0x0112)
                                        {
                                            ushort orientation = isLittleEndian ? reader.ReadUInt16() : ReadBigEndianUInt16(reader);

                                            // Convert Orientation to degrees
                                            // 1 = 0, 3 = 180, 6 = 90, 8 = 270
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
                                            reader.ReadUInt32(); // Skip value offset
                                        }
                                    }
                                }
                                break; // Found APP1 but no orientation, stop
                            }

                            // Skip to next marker
                            stream.Position += length - 2;
                        }
                    }
                }
            }
            catch
            {
                // If anything fails (not a JPEG, corrupted, etc.), assume 0 rotation
            }
            return 0;
        }

        private static ushort ReadBigEndianUInt16(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(2);
            Array.Reverse(bytes);
            return BitConverter.ToUInt16(bytes, 0);
        }

        private static uint ReadBigEndianUInt32(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(4);
            Array.Reverse(bytes);
            return BitConverter.ToUInt32(bytes, 0);
        }
    }
}