using System;
using System.Collections.Generic;
using System.IO;
using Dalamud.Configuration;
using Dalamud.Plugin;
using Newtonsoft.Json;

namespace MOActionPlugin
{
    [Serializable]
    public class MOActionConfiguration : IPluginConfiguration
    {
        public bool IsGuiMO { get; set; }
        public bool IsFieldMO { get; set; }
        public HashSet<ulong> ActiveIDs { get; set; }
        int IPluginConfiguration.Version { get; set; }

        public MOActionConfiguration()
        {
            ActiveIDs = new HashSet<ulong>();
        }
    }
}
