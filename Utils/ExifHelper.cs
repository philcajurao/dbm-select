using System;
using System.IO;
using System.Diagnostics;

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
                        if (stream.Length < 64) return 0; // Too small
                        if (reader.ReadByte() != 0xFF || reader.ReadByte() != 0xD8) return 0;

                        while (stream.Position < stream.Length)
                        {
                            // 2. Scan for 0xFF byte indicating start of marker
                            if (reader.ReadByte() != 0xFF) continue;

                            // 3. Skip padding 0xFF bytes
                            byte marker = reader.ReadByte();
                            while (marker == 0xFF && stream.Position < stream.Length)
                            {
                                marker = reader.ReadByte();
                            }

                            // 4. Valid markers are 0xE1 (APP1) through 0xEF (APP15)
                            // 0x00 is escaped FF, 0xD0-D7 are restart markers (no length), 0xD8/D9 are ROI/EOI
                            if (marker == 0x00 || (marker >= 0xD0 && marker <= 0xD7)) continue;
                            if (marker == 0xD9) break; // EOI (End of Image)

                            // Read segment length
                            long segmentLength = ReadBigEndianUInt16(reader);
                            long nextSegmentStart = stream.Position + segmentLength - 2;

                            // 5. Handle APP1 (Exif)
                            if (marker == 0xE1)
                            {
                                // Minimum header size check (Exif\0\0)
                                if (segmentLength >= 8)
                                {
                                    // Check for "Exif\0\0"
                                    if (reader.ReadByte() == 0x45 && reader.ReadByte() == 0x78 &&
                                        reader.ReadByte() == 0x69 && reader.ReadByte() == 0x66 &&
                                        reader.ReadByte() == 0x00 && reader.ReadByte() == 0x00)
                                    {
                                        return ReadOrientationFromTiff(reader, stream.Position);
                                    }
                                }
                            }

                            // Skip to next segment
                            if (nextSegmentStart > stream.Length) break;
                            stream.Position = nextSegmentStart;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exif Parsing Error for {filePath}: {ex.Message}");
            }
            return 0;
        }

        private static double ReadOrientationFromTiff(BinaryReader reader, long tiffStart)
        {
            // Byte Order
            ushort byteOrder = ReadBigEndianUInt16(reader);
            bool isLittleEndian = byteOrder == 0x4949; // "II"

            // Magic Number (42)
            ushort magic = isLittleEndian ? ReadLittleEndianUInt16(reader) : ReadBigEndianUInt16(reader);

            // Offset to 0th IFD
            uint offsetToIFD = isLittleEndian ? ReadLittleEndianUInt32(reader) : ReadBigEndianUInt32(reader);

            if (offsetToIFD > 0)
            {
                reader.BaseStream.Position = tiffStart + offsetToIFD;

                // Entry Count
                ushort entries = isLittleEndian ? ReadLittleEndianUInt16(reader) : ReadBigEndianUInt16(reader);

                for (int i = 0; i < entries; i++)
                {
                    // Tag (2), Type (2), Count (4), Value/Offset (4)
                    ushort tag = isLittleEndian ? ReadLittleEndianUInt16(reader) : ReadBigEndianUInt16(reader);

                    // Skip Type (2) and Count (4)
                    reader.ReadBytes(6);

                    // Value/Offset (4 bytes)
                    // For Orientation (Short, Type 3), the value is stored in the first 2 bytes of this field.
                    if (tag == 0x0112)
                    {
                        ushort orientation = isLittleEndian ? ReadLittleEndianUInt16(reader) : ReadBigEndianUInt16(reader);

                        // Consume the remaining 2 bytes of the 4-byte field to maintain stream alignment if we were reading consecutively,
                        // but since we return immediately, it doesn't matter.

                        switch (orientation)
                        {
                            case 3: return 180;
                            case 6: return 90;
                            case 8: return 270; // 90 CCW
                            default: return 0;
                        }
                    }
                    else
                    {
                        // Skip Value/Offset bytes
                        reader.ReadBytes(4);
                    }
                }
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