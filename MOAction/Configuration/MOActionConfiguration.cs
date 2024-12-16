using System;
using System.Collections.Generic;
using Dalamud.Configuration;

namespace MOAction.Configuration;

[Serializable]
public class MOActionConfiguration : IPluginConfiguration
{
    public int Version { get; set; } = 6;
    public List<ConfigurationEntry> Stacks { get; private set; }

    public int CrosshairWidth;
    public int CrosshairHeight;

    public bool RangeCheck;

    public MOActionConfiguration()
    {
        Stacks = [];
        RangeCheck = false;
        InitializeCrosshairLocation();
    }

    private unsafe void InitializeCrosshairLocation(){
        var dev = FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Device.Instance();
        CrosshairWidth = (int)dev->Width/2;
        CrosshairHeight = (int)dev->Height/2;
    }
}