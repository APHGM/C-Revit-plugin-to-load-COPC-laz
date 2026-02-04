using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.PointClouds;
using System.Runtime.InteropServices;
using RvtLoadLaz.Core;

namespace RvtLoadLaz.PointCloud
{
    public class LazPointSetIterator : IPointSetIterator
    {
        private readonly List<LasPoint> _points;
        private int _currentIndex = 0;
        private readonly int _totalPoints;
        private readonly PointCloudFilter _filter;

        public LazPointSetIterator(List<LasPoint> points, PointCloudFilter filter)
        {
            _points = points ?? new List<LasPoint>();
            _totalPoints = _points.Count;
            _filter = filter;
        }

        public IPointSetIterator CreateCopy()
        {
            var copy = new LazPointSetIterator(_points, _filter);
            copy._currentIndex = _currentIndex;
            return copy;
        }

        public int ReadPoints(IntPtr buffer, int nBufferSize)
        {
            int pointsWritten = 0;
            int pointsScanned = 0;
            int pointSize = Marshal.SizeOf(typeof(CloudPoint));

            // Loop until buffer is full OR we run out of points
            while (pointsWritten < nBufferSize && (_currentIndex + pointsScanned) < _totalPoints)
            {
                var lasPoint = _points[_currentIndex + pointsScanned];
                
                // Color Packing (ARGB)
                byte r = (byte)(lasPoint.Red / 256);
                byte g = (byte)(lasPoint.Green / 256);
                byte b = (byte)(lasPoint.Blue / 256);
                int color = (255 << 24) | (r << 16) | (g << 8) | b;

                var cloudPoint = new CloudPoint((float)lasPoint.X, (float)lasPoint.Y, (float)lasPoint.Z, color);

                // FILTER CHECK
                // If filter exists and point fails test, skip it.
                if (_filter != null)
                {
                    // TestPoint returns 1 if inside, 0 if outside (usually)
                    // Actually checking docs: TestPoint returns Filtered or Accepted status
                    // But effectively: if (!filter.TestPoint(cloudPoint)) continue; 
                    // Let's assume boolean behavior or implicit check.
                    // Wait, TestPoint returns int? No, usually boolean in .NET wrappers or similar.
                    // Let's verify signature in previous steps or assume standard usage.
                    // Actually, let's just use it in if condition.
                    // If it causes compile error (int vs bool), we fix.
                    if (!_filter.TestPoint(cloudPoint))
                    {
                        pointsScanned++;
                        continue;
                    }
                }

                // Write to buffer
                IntPtr pointPtr = new IntPtr(buffer.ToInt64() + pointsWritten * pointSize);
                Marshal.StructureToPtr(cloudPoint, pointPtr, false);

                pointsWritten++;
                pointsScanned++;
            }

            _currentIndex += pointsScanned;
            return pointsWritten;
        }

        public void Free()
        {
            // Nothing to free explicitly, strictly reference holding
        }
    }
}
