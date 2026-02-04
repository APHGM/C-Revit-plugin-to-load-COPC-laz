using System;
using System.Linq;

namespace RvtLoadLaz.Core
{
    /// <summary>
    /// CloudCompare-style coordinate transformation
    /// Handles global shift and scale for large coordinates
    /// </summary>
    public class CoordinateTransform
    {
        // Revit limit is around 20 miles (approx 32,000 meters or 100,000 feet)
        // Set threshold lower to ensure safety
        private const double GlobalShiftThreshold = 100.0; // Changed to 100 to force check always for testing
        public double ShiftX { get; set; }
        public double ShiftY { get; set; }
        public double ShiftZ { get; set; }
        public double Scale { get; set; } = 1.0;

        public CoordinateTransform()
        {
        }

        public CoordinateTransform(double shiftX, double shiftY, double shiftZ, double scale = 1.0)
        {
            ShiftX = shiftX;
            ShiftY = shiftY;
            ShiftZ = shiftZ;
            Scale = scale;
        }

        /// <summary>
        /// Check if coordinates need global shift (CloudCompare style)
        /// </summary>
        public static bool NeedsGlobalShift(LasFileInfo fileInfo, double threshold = 1_000_000)
        {
            double maxCoord = Math.Max(
                Math.Max(Math.Abs(fileInfo.MaxX), Math.Abs(fileInfo.MinX)),
                Math.Max(
                    Math.Max(Math.Abs(fileInfo.MaxY), Math.Abs(fileInfo.MinY)),
                    Math.Max(Math.Abs(fileInfo.MaxZ), Math.Abs(fileInfo.MinZ))
                )
            );

            return maxCoord > GlobalShiftThreshold;
        }

        /// <summary>
        /// Suggest automatic global shift based on bounds center or Survey Point
        /// </summary>
        public static CoordinateTransform SuggestAutoShift(LasFileInfo fileInfo, double? surveyX = null, double? surveyY = null, double? surveyZ = null)
        {
            // Goal: Match Survey Point to File Coordinates using any means necessary (Swap, Scale).
            // User requirement: "Take exact value to dialog".

            double targetX = fileInfo.MinX;
            double targetY = fileInfo.MinY;
            double targetZ = fileInfo.MinZ;

            // Default Fallback: Auto-Floor (if no survey point match)
            double defaultX = Math.Floor(targetX / 1000.0) * 1000.0;
            double defaultY = Math.Floor(targetY / 1000.0) * 1000.0;
            double defaultZ = Math.Floor(targetZ / 1000.0) * 1000.0;

            if (surveyX.HasValue && surveyY.HasValue)
            {
                double sx = surveyX.Value;
                double sy = surveyY.Value;
                
                // If inputs are essentially zero, skip logic and return default
                if (Math.Abs(sx) < 1.0 && Math.Abs(sy) < 1.0)
                {
                     return new CoordinateTransform(defaultX, defaultY, defaultZ, 1.0);
                }

                // Prepare candidates
                var candidates = new[]
                {
                    new { Name="Direct", X=sx, Y=sy, ScaledZ=surveyZ ?? 0 },
                    new { Name="Swap", X=sy, Y=sx, ScaledZ=surveyZ ?? 0 },
                    new { Name="ScaleDirect (MM)", X=sx*1000.0, Y=sy*1000.0, ScaledZ=(surveyZ??0)*1000.0 },
                    new { Name="ScaleSwap (MM)", X=sy*1000.0, Y=sx*1000.0, ScaledZ=(surveyZ??0)*1000.0 }
                };

                // Find candidate closely matching File Min (within 20,000 units)
                var best = candidates
                    .Select(c => new 
                    { 
                        c.Name, 
                        c.X, 
                        c.Y, 
                        c.ScaledZ,
                        Dist = Math.Abs(targetX - c.X) + Math.Abs(targetY - c.Y) 
                    })
                    .OrderBy(c => c.Dist)
                    .First();

                System.Diagnostics.Trace.WriteLine($"🎯 Best Match: {best.Name} (Diff: {best.Dist:F0}) -> X:{best.X}, Y:{best.Y}");

                // Threshold: If the survey point is within 50km of the file, we assume it's the intended coordinate system.
                // We return the EXACT values (best.X, best.Y) as requested.
                if (best.Dist < 50000) 
                {
                    return new CoordinateTransform(
                        best.X, // Return EXACT value (e.g. 327900.0)
                        best.Y,
                        best.ScaledZ,
                        1.0
                    );
                }
                else
                {
                     System.Diagnostics.Trace.WriteLine($"⚠️ Survey Point too far ({best.Dist:F0}). Using Auto-Floor.");
                }
            }

            // Fallback
            return new CoordinateTransform(
                defaultX,
                defaultY,
                defaultZ,
                1.0
            );
        }

        /// <summary>
        /// Apply transformation to a point
        /// </summary>
        public void ApplyTo(ref double x, ref double y, ref double z)
        {
            x = (x - ShiftX) * Scale;
            y = (y - ShiftY) * Scale;
            z = (z - ShiftZ) * Scale;
        }

        /// <summary>
        /// Get preview of transformed coordinate
        /// </summary>
        public (double x, double y, double z) GetTransformed(double origX, double origY, double origZ)
        {
            return (
                (origX - ShiftX) * Scale,
                (origY - ShiftY) * Scale,
                (origZ - ShiftZ) * Scale
            );
        }

        public override string ToString()
        {
            return $"Shift: ({ShiftX:F2}, {ShiftY:F2}, {ShiftZ:F2}), Scale: {Scale:F4}";
        }
    }
}
