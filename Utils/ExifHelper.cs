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
                        // Check for JPEG Header
                        if (stream.Length < 32) return 0;
                        if (reader.ReadByte() != 0xFF || reader.ReadByte() != 0xD8) return 0;

                        while (stream.Position < stream.Length)
                        {
                            // Find next marker (0xFF followed by non-0xFF)
                            if (reader.ReadByte() != 0xFF) continue;
                            byte marker = reader.ReadByte();

                            // Skip padding
                            while (marker == 0xFF) marker = reader.ReadByte();

                            // Markers with no data
                            if (marker == 0x00 || (marker >= 0xD0 && marker <= 0xD7)) continue;
                            if (marker == 0xD9) break; // EOI

                            // Read Length
                            byte[] lenBytes = reader.ReadBytes(2);
                            if (lenBytes.Length < 2) break;
                            Array.Reverse(lenBytes); // JPEG markers are Big Endian
                            ushort length = BitConverter.ToUInt16(lenBytes, 0);

                            long nextSegment = stream.Position + length - 2;

                            // APP1 Marker (Exif)
                            if (marker == 0xE1 && length >= 8)
                            {
                                // Check "Exif\0\0"
                                byte[] header = reader.ReadBytes(6);
                                if (header[0] == 'E' && header[1] == 'x' && header[2] == 'i' && header[3] == 'f')
                                {
                                    // TIFF Header Start
                                    long tiffStart = stream.Position;

                                    // Byte Order
                                    byte[] order = reader.ReadBytes(2);
                                    bool isLittleEndian = order[0] == 0x49 && order[1] == 0x49;

                                    // Magic 42
                                    reader.ReadBytes(2);

                                    // Offset to IFD
                                    uint offsetToIFD = ReadUInt32(reader, isLittleEndian);

                                    // Jump to IFD
                                    stream.Position = tiffStart + offsetToIFD;

                                    // Count
                                    ushort entries = ReadUInt16(reader, isLittleEndian);

                                    for (int i = 0; i < entries; i++)
                                    {
                                        ushort tag = ReadUInt16(reader, isLittleEndian);
                                        ushort type = ReadUInt16(reader, isLittleEndian);
                                        uint count = ReadUInt32(reader, isLittleEndian);

                                        // Orientation Tag = 0x0112
                                        if (tag == 0x0112)
                                        {
                                            ushort orientation = ReadUInt16(reader, isLittleEndian);
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
                                            // Skip value offset (4 bytes total, we read 2 bytes of tag + 2 type + 4 count = 8. Need to read 4 more to finish entry = 12)
                                            // Actually standard is 12 bytes: Tag(2), Type(2), Count(4), Value/Offset(4).
                                            // We read Tag, Type, Count. Need to skip 4 bytes.
                                            reader.ReadBytes(4);
                                        }
                                    }
                                }
                            }

                            stream.Position = nextSegment;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exif Error: {ex.Message}");
            }
            return 0;
        }

        private static ushort ReadUInt16(BinaryReader reader, bool isLittleEndian)
        {
            byte[] data = reader.ReadBytes(2);
            if (data.Length < 2) return 0;
            if (!isLittleEndian) Array.Reverse(data);
            return BitConverter.ToUInt16(data, 0);
        }

        private static uint ReadUInt32(BinaryReader reader, bool isLittleEndian)
        {
            byte[] data = reader.ReadBytes(4);
            if (data.Length < 4) return 0;
            if (!isLittleEndian) Array.Reverse(data);
            return BitConverter.ToUInt32(data, 0);
        }
    }
}