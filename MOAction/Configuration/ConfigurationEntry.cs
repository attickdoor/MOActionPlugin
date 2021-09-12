using Dalamud.Game.ClientState.Keys;
using System;
using System.Collections.Generic;


namespace MOAction.Configuration
{
    [Serializable]
    public class ConfigurationEntry
    {
        public uint BaseId;

        public List<(string, uint)> Stack;

        public VirtualKey Modifier;
        public string Job;

        public ConfigurationEntry(uint baseid, List<(string, uint)> stack, VirtualKey modifier, string job)
        {
            BaseId = baseid;
            Stack = stack;
            Modifier = modifier;
            Job = job;
        }
    }
}
