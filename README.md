# C-Revit-plugin-to-load-COPC-laz
# RvtLoadLaz - Revit LAZ Point Cloud Loader

**RvtLoadLaz** is a custom Revit plugin designed to import **LAZ** and **LAS** point cloud files directly into Autodesk Revit. 

It provides a robust alternative to native indexing, featuring **CloudCompare-style coordinate transformation** to handle large coordinate systems correctly and ensuring stability during the import process.

## Features

- **Direct LAZ/LAS Import**: Load compressed LAZ and standard LAS point cloud files directly.
- **Custom Point Cloud Engine**: Registers a dedicated `LAZ_PointCloud` engine within Revit for optimized rendering.
- **Smart Coordinate Transformation**: Automatically handles large coordinates to prevent "jitter" and display issues, similar to CloudCompare's "Apply Global Shift/Scale".
- **Stability**: Built to handle large datasets effectively avoiding common crashes associated with some point cloud importers.
- **Simple UI**: Adds a dedicated "LAZ Loader" tab in the Revit Ribbon for easy access.

## Installation

1. Build the solution using Visual Studio (ensure you have the Revit API references linked).
2. Copy the resulting `.dll` files and the `.addin` manifest file to your Revit Addins folder:
   - Usually located at: `%AppData%\Autodesk\Revit\Addins\<Year>\`
3. Restart Revit.

## Usage

1. Open Autodesk Revit.
2. Navigate to the **LAZ Loader** tab in the Ribbon.
3. Click the **Load LAZ** button.
4. Select your `.laz` or `.las` file from the file dialog.
5. The point cloud will be processed and indexed into the current view.

## Requirements

- Autodesk Revit (compatible with the API version targeted in the build).
- .NET Framework (as required by your specific Revit version).

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
