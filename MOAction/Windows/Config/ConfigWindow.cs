using System;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Common.Math;
using ImGuiNET;
using System.Collections.Generic;
using System.Text;
using MOAction.Configuration;
using Newtonsoft.Json;

namespace MOAction.Windows.Config;

public partial class ConfigWindow : Window, IDisposable
{
    private readonly Plugin Plugin;

    private const float SeparatorPadding = 1.0f;
    private static float GetSeparatorPaddingHeight => SeparatorPadding * ImGuiHelpers.GlobalScale;

    public ConfigWindow(Plugin plugin) : base("Action Stack Setup##MOAction")
    {
        Plugin = plugin;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(800, 800),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public void Dispose() { }

    public override void Draw()
    {
        // TODO Find better solution to draw bottom bar depend on open tab
        var open = 0;
        var bottomContentHeight = ImGui.GetFrameHeightWithSpacing() + ImGui.GetStyle().WindowPadding.Y + GetSeparatorPaddingHeight;
        using (var contentChild = ImRaii.Child("ConfigContent", new Vector2(0, -bottomContentHeight)))
        {
            if (contentChild.Success)
            {
                using var tabBar = ImRaii.TabBar("##ConfigTabBar");
                if (tabBar.Success)
                {
                    open = Math.Max(Settings(), open);

                    open = Math.Max(About(), open);

                    open = Math.Max(Wizard(), open);
                }
            }
        }

        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(SeparatorPadding);

        using var bottomChild = ImRaii.Child("ConfigBottomBar", new Vector2(0, 0), false, 0);
        if (!bottomChild.Success)
            return;

        switch (open)
        {
            case 1:
                DrawSettingsButtons();
                break;
            case 2:
                DrawAboutButtons();
                break;
        }
    }

        private void ImportStringToMouseOverActions(String import)
    {
        try
        {
            var tempStacks = Plugin.SortStacks(Plugin.RebuildStacks(JsonConvert.DeserializeObject<List<ConfigurationEntry>>(Encoding.UTF8.GetString(Convert.FromBase64String(import)))));
            foreach (var (k, v) in tempStacks)
            {
                if (Plugin.SavedStacks.TryGetValue(k, out var value))
                    value.UnionWith(v);
                else
                    Plugin.SavedStacks[k] = v;
            }
        }
        catch (Exception e)
        {
            Plugin.PluginLog.Error(e, "Importing stacks from clipboard failed.");
        }
    }
}
