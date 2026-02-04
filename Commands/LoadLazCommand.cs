using System;
using System.IO;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.PointClouds;
using Autodesk.Revit.UI;
using RvtLoadLaz.Core;
using RvtLoadLaz.PointCloud;
using RvtLoadLaz.UI;
using System.Linq; // Added for Take() method 

namespace RvtLoadLaz.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class LoadLazCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            string tempLasFile = null;

            try
            {
                // Step 1: File picker
                var openFileDialog = new OpenFileDialog
                {
                    Filter = "LAZ Files (*.laz)|*.laz|LAS Files (*.las)|*.las|All Files (*.*)|*.*",
                    Title = "Select LAZ/LAS Point Cloud File",
                    CheckFileExists = true
                };

                if (openFileDialog.ShowDialog() != DialogResult.OK)
                    return Result.Cancelled;

                string inputFile = openFileDialog.FileName;
                bool isLaz = Path.GetExtension(inputFile).ToLower() == ".laz";

                System.Diagnostics.Trace.WriteLine($"📂 Selected file: {inputFile}");

                // Step 2: Read header to check if coordinate transformation is needed
                var fileInfo = LasReader.ReadHeader(inputFile);

                // Get assembly version/timestamp for verification
                string version = "v3.4 (Section Box Fix)";
                
                // Step 3: Show file info
                string info = $"LAZ File Information ({version}):\n\n" +
                             $"Version: {fileInfo.VersionMajor}.{fileInfo.VersionMinor}\n" +
                             $"Point Format: {fileInfo.PointDataFormatId}\n" +
                             $"Total Points: {fileInfo.TotalPoints:N0}\n\n" +
                             $"Bounds:\n" +
                             $"X: [{fileInfo.MinX:F2}, {fileInfo.MaxX:F2}]\n" +
                             $"Y: [{fileInfo.MinY:F2}, {fileInfo.MaxY:F2}]\n" +
                             $"Z: [{fileInfo.MinZ:F2}, {fileInfo.MaxZ:F2}]";

                TaskDialog.Show("File Info", info);

                // Step 4: Check for global shift need (CloudCompare style)
                CoordinateTransform transform = null;

                if (CoordinateTransform.NeedsGlobalShift(fileInfo))
                {
                    // Try to get Revit Survey Point
                    double? surveyX = null, surveyY = null, surveyZ = null;
                    try
                    {
                        var surveyPoint = new FilteredElementCollector(doc)
                            .OfClass(typeof(BasePoint))
                            .Cast<BasePoint>()
                            .FirstOrDefault(bp => bp.IsShared);

                        if (surveyPoint != null)
                        {
                            // Survey Point values are in internal feet. Convert to Meters for LAS comparison.
                            // 1 ft = 0.3048 m
                            double toMeters = 0.3048;
                            
                            // Get Shared Position (East/North/Elev)
                            // Use the position properties depending on Revit version/clip state, 
                            // but generally Position property or parameters give the internal coords relative to startup.
                            // What we want is the "Shared Coordinate" values.
                            
                            // Method 1: Parameters (safest for "Values shown in UI")
                            double ew = surveyPoint.get_Parameter(BuiltInParameter.BASEPOINT_EASTWEST_PARAM).AsDouble();
                            double ns = surveyPoint.get_Parameter(BuiltInParameter.BASEPOINT_NORTHSOUTH_PARAM).AsDouble();
                            double el = surveyPoint.get_Parameter(BuiltInParameter.BASEPOINT_ELEVATION_PARAM).AsDouble();

                            surveyX = ew * toMeters;
                            surveyY = ns * toMeters;
                            surveyZ = el * toMeters;

                            System.Diagnostics.Trace.WriteLine($"📍 Found Survey Point (Meters): {surveyX:F2}, {surveyY:F2}, {surveyZ:F2}");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.WriteLine($"⚠️ Could not read Survey Point: {ex.Message}");
                    }

                    var suggestedTransform = CoordinateTransform.SuggestAutoShift(fileInfo, surveyX, surveyY, surveyZ);

                    var shiftDialog = new GlobalShiftDialog(fileInfo, suggestedTransform);
                    if (shiftDialog.ShowDialog() == true && shiftDialog.ApplyShift)
                    {
                        transform = new CoordinateTransform(
                            shiftDialog.ShiftX,
                            shiftDialog.ShiftY,
                            shiftDialog.ShiftZ,
                            shiftDialog.Scale
                        );

                        System.Diagnostics.Trace.WriteLine($"✓ Applying coordinate transform: {transform}");
                    }
                }
                else
                {
                     // Force check if user wants to shift anyway? 
                     // For now, let's stick to the logic but maybe the logic is flawed for this file.
                     // Add a note in debug if skipped.
                }

                // Step 5: Ask about point limit for large files
                int maxPoints = int.MaxValue;
                if (fileInfo.TotalPoints > 500000)
                {
                    var result = TaskDialog.Show("Point Limit",
                        $"File contains {fileInfo.TotalPoints:N0} points.\n\n" +
                        $"Load first 500,000 points for faster loading?",
                        TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);

                    if (result == TaskDialogResult.Yes)
                    {
                        maxPoints = 500000;
                    }
                }

                // Step 6: Convert LAZ to LAS if needed
                string lasFileToRead = inputFile;

                if (isLaz)
                {
                    tempLasFile = Path.Combine(Path.GetTempPath(), $"temp_{Guid.NewGuid()}.las");

                    System.Diagnostics.Trace.WriteLine($"🔄 Converting LAZ to LAS...");

                    if (!LasToolsWrapper.ConvertLazToLas(inputFile, tempLasFile, out string errorMsg))
                    {
                        TaskDialog.Show("Error", $"Failed to convert LAZ file:\n\n{errorMsg}");
                        return Result.Failed;
                    }

                    lasFileToRead = tempLasFile;
                }

                // Step 7: Read points from uncompressed LAS
                System.Diagnostics.Trace.WriteLine($"📖 Loading points...");
                var points = LasReader.ReadPoints(lasFileToRead, maxPoints, transform);

                if (points == null || points.Count == 0)
                {
                    TaskDialog.Show("Error", "No points were read from the file.");
                    return Result.Failed;
                }

                System.Diagnostics.Trace.WriteLine($"✓ Loaded {points.Count:N0} points");

                // Check bounds of loaded points
                double debugMinX = double.MaxValue, debugMaxX = double.MinValue;
                foreach(var p in points.Take(1000))
                {
                    if (p.X < debugMinX) debugMinX = p.X;
                    if (p.X > debugMaxX) debugMaxX = p.X;
                }

                // Step 8: Create point cloud in Revit
                using (Transaction trans = new Transaction(doc, "Load LAZ Point Cloud"))
                {
                    trans.Start();

                    string engineIdentifier = "LAZ_PointCloud";
                    string filePath = inputFile; // Use original path as identifier

                    // 1. Create the access object
                    var pointCloudAccess = new LazPointCloudAccess(Path.GetFileName(filePath), points);

                    // 2. Stash it so the Engine can find it given the filePath
                    LazDataStash.Add(filePath, pointCloudAccess);

                    // 3. Create point cloud type
                    // This will trigger LazPointCloudEngine.CreatePointCloudAccess(filePath)
                    PointCloudType pointCloudType = PointCloudType.Create(doc, engineIdentifier, filePath);

                    // 4. Create point cloud instance
                    var instance = PointCloudInstance.Create(doc, pointCloudType.Id, Transform.Identity);
                    
                    trans.Commit();

                    // ZOOM TO ELEMENT
                    uiDoc.ShowElements(instance.Id);
                    // refresh view
                    uiDoc.RefreshActiveView();

                    // Prepare Detailed Info
                    string transformInfo = transform != null ? 
                        $"\n\n[TRANSFORM APPLIED]\nShift X: {transform.ShiftX:F2}\nShift Y: {transform.ShiftY:F2}\nShift Z: {transform.ShiftZ:F2}" : 
                        "\n\n[NO TRANSFORM]";
                    
                    // Calculate final bounds (local)
                    double minX = points.Min(p => p.X);
                    double maxX = points.Max(p => p.X);
                    double minY = points.Min(p => p.Y);
                    double maxY = points.Max(p => p.Y);
                    double minZ = points.Min(p => p.Z);
                    double maxZ = points.Max(p => p.Z);

                    // Calculate RGB and Intensity Stats
                    ushort minR = points.Min(p => p.Red);
                    ushort maxR = points.Max(p => p.Red);
                    ushort minG = points.Min(p => p.Green);
                    ushort maxG = points.Max(p => p.Green);
                    ushort minB = points.Min(p => p.Blue);
                    ushort maxB = points.Max(p => p.Blue);
                    ushort minI = points.Min(p => p.Intensity);
                    ushort maxI = points.Max(p => p.Intensity);

                    string details = $"Loaded {points.Count:N0} points (v1.9)\n\n" +
                                     $"[ORIGINAL GLOBAL COORDINATES]\n" +
                                     $"X: {fileInfo.MinX:F2} to {fileInfo.MaxX:F2}\n" +
                                     $"Y: {fileInfo.MinY:F2} to {fileInfo.MaxY:F2}\n" +
                                     $"Z: {fileInfo.MinZ:F2} to {fileInfo.MaxZ:F2}\n\n" +
                                     $"{transformInfo}\n\n" +
                                     $"[TRANSFORMED LOCAL COORDINATES]\n" +
                                     $"X: {minX:F2} to {maxX:F2} (Size: {(maxX-minX):F2})\n" +
                                     $"Y: {minY:F2} to {maxY:F2} (Size: {(maxY-minY):F2})\n" +
                                     $"Z: {minZ:F2} to {maxZ:F2} (Size: {(maxZ-minZ):F2})\n\n" +
                                     $"[COLOR & INTENSITY]\n" +
                                     $"Red: {minR} - {maxR}\n" +
                                     $"Green: {minG} - {maxG}\n" +
                                     $"Blue: {minB} - {maxB}\n" +
                                     $"Intensity: {minI} - {maxI}";
                    
                    TaskDialog.Show("Success - Point Cloud Loaded", details);
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                System.Diagnostics.Trace.WriteLine($"❌ Error: {ex.Message}");
                System.Diagnostics.Trace.WriteLine($"   Stack: {ex.StackTrace}");

                TaskDialog.Show("Error", $"Failed to load LAZ file:\n\n{ex.Message}");
                return Result.Failed;
            }
            finally
            {
                // Clean up temp file
                if (tempLasFile != null && File.Exists(tempLasFile))
                {
                    try
                    {
                        File.Delete(tempLasFile);
                        System.Diagnostics.Trace.WriteLine($"🗑 Cleaned up temp file");
                    }
                    catch { }
                }
            }
        }
    }
}
