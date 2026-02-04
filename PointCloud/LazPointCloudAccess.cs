using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.PointClouds;
using RvtLoadLaz.Core;

namespace RvtLoadLaz.PointCloud
{
    /// <summary>
    /// Provides point cloud data to Revit from LAZ file
    /// </summary>
    public class LazPointCloudAccess : IPointCloudAccess
    {
        private readonly List<LasPoint> _points;
        private readonly string _identifier;
        private XYZ _min;
        private XYZ _max;

        public LazPointCloudAccess(string identifier, List<LasPoint> points)
        {
            _identifier = identifier;
            var inputPoints = points ?? new List<LasPoint>(); // Source
            
            // 1. Data Thickening (Safe Mode)
            // Create a NEW list to avoid modification errors
            int originalCount = inputPoints.Count;
            int targetCount = originalCount + (originalCount / 2); // 1.5x
            
            _points = new List<LasPoint>(targetCount);
            
            // Copy originals first
            _points.AddRange(inputPoints);

            Random rng = new Random();

            // Add 50% Clones with Jitter
            int limit = originalCount / 2; 
            for (int i = 0; i < limit; i++)
            {
                var source = inputPoints[i]; 
                
                // Jitter ~1.5cm (assuming units are meters/feet, 0.015 is safe small noise)
                double jitterX = (rng.NextDouble() - 0.5) * 0.03; 
                double jitterY = (rng.NextDouble() - 0.5) * 0.03;
                double jitterZ = (rng.NextDouble() - 0.5) * 0.03;

                var clone = new LasPoint
                {
                    X = source.X + jitterX,
                    Y = source.Y + jitterY,
                    Z = source.Z + jitterZ,
                    Red = source.Red,
                    Green = source.Green,
                    Blue = source.Blue,
                    Intensity = source.Intensity
                };

                _points.Add(clone);
            }
            
            // 2. Randomize points (Fisher-Yates Shuffle).
            // Apply to the FINAL list (_points)
            int n = _points.Count;
            while (n > 1)
            {
                n--; // Correct decrement
                int k = rng.Next(n + 1); // Random 0 to n
                var value = _points[k];
                _points[k] = _points[n];
                _points[n] = value;
            }

            // Calculate bounds
            CalculateBounds();
        }

        private void CalculateBounds()
        {
            if (_points == null || _points.Count == 0)
            {
                _min = new XYZ(0, 0, 0);
                _max = new XYZ(0, 0, 0);
                return;
            }

            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

            foreach (var pt in _points)
            {
                if (pt.X < minX) minX = pt.X;
                if (pt.Y < minY) minY = pt.Y;
                if (pt.Z < minZ) minZ = pt.Z;
                if (pt.X > maxX) maxX = pt.X;
                if (pt.Y > maxY) maxY = pt.Y;
                if (pt.Z > maxZ) maxZ = pt.Z;
            }

            _min = new XYZ(minX, minY, minZ);
            _max = new XYZ(maxX, maxY, maxZ);
        }

        public int ReadPoints(PointCloudFilter filter, ElementId viewId, IntPtr buffer, int nBufferSize)
        {
            try
            {
                int count = Math.Min(_points.Count, nBufferSize);
                int pointSize = Marshal.SizeOf(typeof(CloudPoint));

                for (int i = 0; i < count; i++)
                {
                    var lasPoint = _points[i]; // Restored missing declaration

                    // Revert to Real Colors (ARGB) - v2.9
                    // The Green Test failed, implying the Iterator was the missing link.
                    // We now use proper ARGB packing for both ReadPoints and the Iterator.
                    
                    byte r = (byte)(lasPoint.Red / 256);
                    byte g = (byte)(lasPoint.Green / 256);
                    byte b = (byte)(lasPoint.Blue / 256);
                    
                    // Pack: Alpha (24-31) | Red (16-23) | Green (8-15) | Blue (0-7)
                    // Note: Ensure Alpha is 255 (Opaque)
                    int color = (255 << 24) | (r << 16) | (g << 8) | b;

                    var cloudPoint = new CloudPoint((float)lasPoint.X, (float)lasPoint.Y, (float)lasPoint.Z, color);

                    // Write to buffer
                    IntPtr pointPtr = new IntPtr(buffer.ToInt64() + i * pointSize);
                    Marshal.StructureToPtr(cloudPoint, pointPtr, false);
                }

                return count;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"❌ Error reading points: {ex.Message}");
                return 0;
            }
        }

        public IPointSetIterator CreatePointSetIterator(PointCloudFilter filter, ElementId viewId)
        {
            return new LazPointSetIterator(_points, filter);
        }

        public IPointSetIterator CreatePointSetIterator(PointCloudFilter filter, double pixelSizeInFeet, ElementId viewId)
        {
            return new LazPointSetIterator(_points, filter);
        }

        public void GetExtent(out XYZ min, out XYZ max)
        {
            min = _min;
            max = _max;
        }

        public Outline GetExtent()
        {
            return new Outline(_min, _max);
        }

        public XYZ GetOffset()
        {
            // No offset needed - coordinates already transformed
            return XYZ.Zero;
        }

        public string GetName()
        {
            return _identifier;
        }

        public string GetUnitsName()
        {
            return "Meters"; // LAS files typically use meters
        }

        public double GetUnitsToFeetConversionFactor()
        {
            return 3.28084; // 1 meter = 3.28084 feet
        }

        public string GetColorEncodingName()
        {
            return "RGB";
        }

        public PointCloudColorEncoding GetColorEncoding()
        {
            return PointCloudColorEncoding.ABGR; // Reverted to ABGR
        }

        public string GetIdentifier()
        {
            return _identifier;
        }

        public void Free()
        {
            // Cleanup if needed
            _points?.Clear();
        }
    }
}
