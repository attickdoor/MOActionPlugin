using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using MOAction.Configuration;

namespace MOAction
{
    [Serializable]
    public class MOActionConfiguration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;
        public List<ConfigurationEntry> Stacks { get; private set; }
        int IPluginConfiguration.Version { get; set; }

        public bool RangeCheck { get; set; }
        public bool MouseClamp { get; set; }
        public bool OtherGroundClamp { get; set; }

        public MOActionConfiguration()
        {
            Stacks = new();
            RangeCheck = false;
            MouseClamp = false;
            OtherGroundClamp = false;
        }

    }
}
