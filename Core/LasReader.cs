using System;
using System.Collections.Generic;
using System.IO;

namespace RvtLoadLaz.Core
{
    /// <summary>
    /// Point data from LAS file
    /// </summary>
    public class LasPoint
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public ushort Intensity { get; set; }
        public byte Classification { get; set; }
        public ushort Red { get; set; }
        public ushort Green { get; set; }
        public ushort Blue { get; set; }
    }

    /// <summary>
    /// LAS file header information
    /// </summary>
    public class LasFileInfo
    {
        public byte VersionMajor { get; set; }
        public byte VersionMinor { get; set; }
        public byte PointDataFormatId { get; set; }
        public ushort PointDataRecordLength { get; set; }
        public uint NumberOfPointRecords { get; set; }
        public ulong ExtendedNumberOfPointRecords { get; set; }
        
        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MinZ { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }
        public double MaxZ { get; set; }
        
        public double ScaleX { get; set; }
        public double ScaleY { get; set; }
        public double ScaleZ { get; set; }
        public double OffsetX { get; set; }
        public double OffsetY { get; set; }
        public double OffsetZ { get; set; }

        public uint OffsetToPointData { get; set; }

        public ulong TotalPoints => NumberOfPointRecords > 0 ? NumberOfPointRecords : ExtendedNumberOfPointRecords;
    }

    /// <summary>
    /// Simple reader for uncompressed LAS files
    /// No P/Invoke, no crashes - just pure C# binary reading
    /// </summary>
    public static class LasReader
    {
        /// <summary>
        /// Read LAS file header
        /// </summary>
        public static LasFileInfo ReadHeader(string filePath)
        {
            using (var stream = File.OpenRead(filePath))
            using (var reader = new BinaryReader(stream))
            {
                var info = new LasFileInfo();

                // Read file signature - should be "LASF"
                byte[] signature = reader.ReadBytes(4);
                string sigString = System.Text.Encoding.ASCII.GetString(signature);
                if (sigString != "LASF")
                {
                    throw new InvalidDataException($"Not a valid LAS file - signature '{sigString}' mismatch");
                }

                // Skip file source ID (2), global encoding (2), GUID (16) -> 20 bytes total
                // Current pos: 4. Target pos: 24. Skip: 20.
                reader.ReadBytes(20);

                // Read version
                info.VersionMajor = reader.ReadByte(); // offset 24
                info.VersionMinor = reader.ReadByte(); // offset 25

                // Skip system identifier (32) and generating software (32) -> 64 bytes
                reader.ReadBytes(64);

                // Skip creation day/year (4 bytes)
                reader.ReadBytes(4);

                // Read header size
                ushort headerSize = reader.ReadUInt16(); // offset 94

                // Read offset to point data
                info.OffsetToPointData = reader.ReadUInt32(); // offset 96

                // Skip number of VLRs
                reader.ReadUInt32();

                // Read point data format and record length
                info.PointDataFormatId = reader.ReadByte(); // offset 104
                info.PointDataRecordLength = reader.ReadUInt16(); // offset 105

                // Read legacy number of point records
                info.NumberOfPointRecords = reader.ReadUInt32(); // offset 107

                // Skip number of points by return (5 * 4 bytes = 20 bytes)
                reader.ReadBytes(20);

                // Read scale factors
                info.ScaleX = reader.ReadDouble(); // offset 131
                info.ScaleY = reader.ReadDouble();
                info.ScaleZ = reader.ReadDouble();

                // Read offsets
                info.OffsetX = reader.ReadDouble(); // offset 155
                info.OffsetY = reader.ReadDouble();
                info.OffsetZ = reader.ReadDouble();

                // Read bounds
                info.MaxX = reader.ReadDouble(); // offset 179
                info.MinX = reader.ReadDouble();
                info.MaxY = reader.ReadDouble();
                info.MinY = reader.ReadDouble();
                info.MaxZ = reader.ReadDouble();
                info.MinZ = reader.ReadDouble();

                // For LAS 1.4, read extended number of points if available
                // Current pos should be 227
                // Extended Num Points is at 247. Start of Waveform Data Packet Record is 227.
                // We need to skip to 247. Difference: 20 bytes.
                if (info.VersionMajor == 1 && info.VersionMinor >= 4 && headerSize >= 255)
                {
                    // Skip to extended number of points
                    // Current position is 227. Target is 247.
                    reader.ReadBytes(20);
                    info.ExtendedNumberOfPointRecords = reader.ReadUInt64();
                }

                System.Diagnostics.Trace.WriteLine($"📄 LAS Header Info:");
                System.Diagnostics.Trace.WriteLine($"   Version: {info.VersionMajor}.{info.VersionMinor}");
                System.Diagnostics.Trace.WriteLine($"   Point Format: {info.PointDataFormatId}");
                System.Diagnostics.Trace.WriteLine($"   Total Points: {info.TotalPoints:N0}");
                System.Diagnostics.Trace.WriteLine($"   Offset to Data: {info.OffsetToPointData}");

                return info;
            }
        }

        /// <summary>
        /// Read points from uncompressed LAS file
        /// </summary>
        public static List<LasPoint> ReadPoints(
            string filePath,
            int maxPoints = int.MaxValue,
            CoordinateTransform transform = null)
        {
            var points = new List<LasPoint>();
            var header = ReadHeader(filePath);

            int pointsToRead = (int)Math.Min(maxPoints, (long)header.TotalPoints);

            System.Diagnostics.Trace.WriteLine($"📖 Reading {pointsToRead:N0} points from LAS file...");

            using (var stream = File.OpenRead(filePath))
            using (var reader = new BinaryReader(stream))
            {
                // Align stream to start of point data
                // We can use stream.Seek here before we start reading with BinaryReader
                stream.Seek(header.OffsetToPointData, SeekOrigin.Begin);

                bool hasRgb = HasRGB(header.PointDataFormatId);
                int rgbOffset = GetRGBOffset(header.PointDataFormatId);
                int recordLength = header.PointDataRecordLength;

                for (int i = 0; i < pointsToRead; i++)
                {
                    int bytesRead = 0;

                    // Read XYZ (always at offset 0, 4, 8 for all formats) -> 12 bytes
                    int rawX = reader.ReadInt32();
                    int rawY = reader.ReadInt32();
                    int rawZ = reader.ReadInt32();
                    bytesRead += 12;

                    // Read intensity (offset 12) -> 2 bytes
                    ushort intensity = reader.ReadUInt16();
                    bytesRead += 2;

                    // Skip return info byte (offset 14) -> 1 byte
                    reader.ReadByte();
                    bytesRead += 1;

                    // Read classification (offset 15) -> 1 byte
                    byte classification = reader.ReadByte();
                    bytesRead += 1;

                    // Total so far: 16 bytes

                    // Read RGB if present
                    ushort red = 0, green = 0, blue = 0;
                    if (hasRgb && rgbOffset > 0)
                    {
                        // Calculate how many bytes to skip to reach RGB
                        // Current bytesRead should be 16.
                        int skip = rgbOffset - bytesRead;
                        if (skip > 0) 
                        {
                            reader.ReadBytes(skip);
                            bytesRead += skip;
                        }

                        red = reader.ReadUInt16();
                        green = reader.ReadUInt16();
                        blue = reader.ReadUInt16();
                        bytesRead += 6; // 2+2+2
                    }

                    // Move to next point by reading remaining bytes in this record
                    // Do NOT use Stream.Seek as it desyncs with BinaryReader buffering
                    int bytesRemaining = recordLength - bytesRead;
                    
                    if (bytesRemaining > 0)
                    {
                        // Safely consume remaining bytes of this record
                        reader.ReadBytes(bytesRemaining);
                    }
                    else if (bytesRemaining < 0)
                    {
                        // This implies our read logic exceeded the record length!
                        // This shouldn't happen for standard formats, but good to check
                        System.Diagnostics.Trace.WriteLine($"⚠️ Warning: Point record read overflow by {-bytesRemaining} bytes at point {i}");
                    }

                    // Apply scale and offset to get real coordinates
                    double x = rawX * header.ScaleX + header.OffsetX;
                    double y = rawY * header.ScaleY + header.OffsetY;
                    double z = rawZ * header.ScaleZ + header.OffsetZ;

                    // Apply coordinate transformation if provided
                    if (transform != null)
                    {
                        x = (x - transform.ShiftX) * transform.Scale;
                        y = (y - transform.ShiftY) * transform.Scale;
                        z = (z - transform.ShiftZ) * transform.Scale;
                    }

                    points.Add(new LasPoint
                    {
                        X = x,
                        Y = y,
                        Z = z,
                        Intensity = intensity,
                        Classification = classification,
                        Red = red,
                        Green = green,
                        Blue = blue
                    });
                }
            }

            System.Diagnostics.Trace.WriteLine($"✓ Successfully read {points.Count:N0} points");
            return points;
        }

        private static bool HasRGB(byte pointFormat)
        {
            // Mask compression bit (0x80) and other flags to get raw format ID
            // Format 135 = 128 (Compressed) + 7
            // Use 0x3F to be safe (ignoring top 2 bits, typically 0x80 is compression)
            byte id = (byte)(pointFormat & 0x7F); // 0x7F keeps lower 7 bits. 0x3F keeps lower 6.
            // LAS spec: "The most significant bit (bit 7) is the Compressed bit"
            
            return id == 2 || id == 3 || id == 5 ||
                   id == 7 || id == 8 || id == 10;
        }

        private static int GetRGBOffset(byte pointFormat)
        {
            byte id = (byte)(pointFormat & 0x7F);

            // Formats 2, 3, 5: RGB at offset 20 (Legacy)
            if (id == 2 || id == 3 || id == 5)
                return 20;

            // Formats 6, 7, 8, 9, 10: RGB at offset 30 (LAS 1.4)
            // Base size for Format 6 is 30 bytes. RGB follows immediately in Format 7/8/10.
            if (id >= 6 && id <= 10)
                return 30;

            return -1;
        }
    }
}
