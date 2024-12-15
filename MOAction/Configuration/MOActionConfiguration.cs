using System;
using System.Collections.Generic;
using Dalamud.Configuration;

namespace MOAction.Configuration;

[Serializable]
public class MOActionConfiguration : IPluginConfiguration
{
    public int Version { get; set; } = 6;
    public List<ConfigurationEntry> Stacks { get; private set; }

    public bool RangeCheck;

    public MOActionConfiguration()
    {
        Stacks = [];
        RangeCheck = false;
    }
}