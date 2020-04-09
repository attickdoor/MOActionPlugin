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
        public List<GuiSettings> StackFlags { get; private set; }
        int IPluginConfiguration.Version { get; set; }

        public MOActionConfiguration()
        {
            //Stacks = new List<(uint key, List<StackEntry> value)>();
            StackFlags = new List<GuiSettings>();
        }

        public void SetStackFlags(List<GuiSettings> flags)
        {
            StackFlags = flags;
        }

        public void SetStacks(List<(uint, List<StackEntry>)> stack)
        {
            //Stacks = stack;
        }
    }
}
