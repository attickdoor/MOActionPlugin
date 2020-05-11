using System;
using System.Collections.Generic;
using System.IO;
using Dalamud.Configuration;
using Dalamud.Plugin;
using MOAction.Configuration;
using MOAction.Target;
using Newtonsoft.Json;

namespace MOAction
{
    [Serializable]
    public class MOActionConfiguration : IPluginConfiguration
    {
        //public List<(uint, List<StackEntry>)> Stacks { get; private set; }
        public int Version { get; set; } = 0;
        public List<GuiSettings> StackFlags { get; private set; }
        int IPluginConfiguration.Version { get; set; }

        public bool OldConfigActive { get; set; }
        public bool[] OldFlags { get; set; }
        public bool oldMO { get; set; }
        public bool oldField { get; set; }
        public bool RangeCheck { get; set; }

        public MOActionConfiguration()
        {
            //Stacks = new List<(uint key, List<StackEntry> value)>();
            StackFlags = new List<GuiSettings>();
            OldFlags = new bool[1];
        }

        public void SetStackFlags(List<GuiSettings> flags)
        {
            StackFlags = flags;
        }

        public void SetStacks(List<(uint, List<StackEntry>)> stack)
        {
            //Stacks = stack;
        }
        
        public void SetOldFlags(bool[] flags)
        {
            OldFlags = flags;
        }

        public void SetWindowVersion(bool old)
        {
            OldConfigActive = old;
        }

        public void SetOldMO(bool old)
        {
            oldMO = old;
        }
        public void SetOldField(bool old)
        {
            oldField = old;
        }
        public void SetRangeCheck(bool range)
        {
            RangeCheck = range;
        }
    }
}
