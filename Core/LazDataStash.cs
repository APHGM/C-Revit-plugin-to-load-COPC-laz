using System.Collections.Generic;
using RvtLoadLaz.PointCloud;

namespace RvtLoadLaz.Core
{
    public static class LazDataStash
    {
        private static readonly Dictionary<string, LazPointCloudAccess> _stash = new Dictionary<string, LazPointCloudAccess>();

        public static void Add(string key, LazPointCloudAccess access)
        {
            if (_stash.ContainsKey(key))
                _stash[key] = access;
            else
                _stash.Add(key, access);
        }

        public static LazPointCloudAccess Get(string key)
        {
            if (_stash.ContainsKey(key))
                return _stash[key];
            return null;
        }

        public static void Remove(string key)
        {
            if (_stash.ContainsKey(key))
                _stash.Remove(key);
        }

        public static void Clear()
        {
            _stash.Clear();
        }
    }
}
