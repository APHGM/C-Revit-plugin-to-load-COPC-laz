using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;

namespace RvtLoadLaz.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class AboutCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Task Dialog to show About Info
            TaskDialog aboutDialog = new TaskDialog("About RvtLoadLaz");
            aboutDialog.MainInstruction = "RvtLoadLaz Plugin";
            aboutDialog.MainContent = "Version: 3.4 (Section Box Fix)\n\n" +
                                      "A custom point cloud import engine for Revit.\n\n" +
                                      "Developer Info:\n" +
                                      "Name: [Arun Prashad.Hariharan]\n" +
                                      "Email: [arunprashadh@gmail.com]\n" +
                                      "Website: https://www.linkedin.com/in/arun-prashad-hariharan/overlay/about-this-profile/?lipi=urn%3Ali%3Apage%3Ad_flagship3_profile_view_base%3BXEypR%2FMBRk6auQ%2BSQ0d9dQ%3D%3D]\n\n" +
                                      "Powered by: Antigravity AI & LasZip";
            
            aboutDialog.TitleAutoPrefix = false;
            aboutDialog.CommonButtons = TaskDialogCommonButtons.Close;
            
            // Add a footer
            aboutDialog.FooterText = "© 2026 Custom Logic Inc.";

            aboutDialog.Show();

            return Result.Succeeded;
        }
    }
}
