using Autodesk.Revit.DB.PointClouds;
using RvtLoadLaz.Core;

namespace RvtLoadLaz.PointCloud
{
    public class LazPointCloudEngine : IPointCloudEngine
    {
        public IPointCloudAccess CreatePointCloudAccess(string identifier)
        {
            // Try to find pre-loaded data in stash
            var access = LazDataStash.Get(identifier);
            if (access != null)
            {
                return access;
            }

            // If not in stash, we currently don't support loading directly from file 
            // without the command's pre-processing (LAZ conversion, UI, etc.)
            // In a full implementation, we would replicate the loading logic here,
            // but for this plugin, we rely on the Command to prepare the data.
            return new LazPointCloudAccess(identifier, null);
        }

        public void Free()
        {
            LazDataStash.Clear();
        }
    }
}
