using System;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB.PointClouds;

namespace RvtLoadLaz
{
    public class Application : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                // Register Point Cloud Engine
                try
                {
                    PointCloudEngineRegistry.RegisterPointCloudEngine("LAZ_PointCloud", new RvtLoadLaz.PointCloud.LazPointCloudEngine(), false);
                }
                catch (Exception ex)
                {
                    // Ignore if already registered (e.g. during regeneration)
                    System.Diagnostics.Trace.WriteLine($"Engine registration warning: {ex.Message}");
                }

                // Create a custom ribbon tab
                string tabName = "LAZ Loader";
                
                try
                {
                    application.CreateRibbonTab(tabName);
                }
                catch
                {
                    // Tab might already exist, that's okay
                }

                // Create ribbon panel
                RibbonPanel panel = application.CreateRibbonPanel(tabName, "Point Clouds");

                // Get assembly path
                string assemblyPath = Assembly.GetExecutingAssembly().Location;

                // Create "Load LAZ" button
                PushButtonData loadButtonData = new PushButtonData(
                    "LoadLazButton",
                    "Load LAZ",
                    assemblyPath,
                    "RvtLoadLaz.Commands.LoadLazCommand"
                );

                loadButtonData.ToolTip = "Load LAZ/LAS point cloud files into Revit";
                loadButtonData.LongDescription = "Load LAZ or LAS point cloud files with CloudCompare-style coordinate transformation. " +
                                                "Uses safe LAStools approach - no crashes!";

                // Add Load button to panel (Restored)
                PushButton loadButton = panel.AddItem(loadButtonData) as PushButton;

                // Create "About" button
                PushButtonData aboutButtonData = new PushButtonData(
                    "AboutButton",
                    "About",
                    assemblyPath,
                    "RvtLoadLaz.Commands.AboutCommand"
                );

                aboutButtonData.ToolTip = "Show About & Developer Info";
                
                // Set Icon (if exists)
                string iconPath = Path.Combine(Path.GetDirectoryName(assemblyPath), "icon_about.png");
                if (File.Exists(iconPath))
                {
                    Uri uriImage = new Uri(iconPath);
                    BitmapImage largeImage = new BitmapImage(uriImage);
                    aboutButtonData.LargeImage = largeImage;
                }
                
                // Add About button
                panel.AddItem(aboutButtonData);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", $"Failed to init application: {ex.Message}");
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}
