using System;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Common.Math;
using ImGuiNET;

namespace MOAction.Windows.Config;

[Flags]
public enum Tabs
{
    None,

    Settings,

    About,
}

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
        var open = Tabs.None;
        var bottomContentHeight = ImGui.GetFrameHeightWithSpacing() + ImGui.GetStyle().WindowPadding.Y + GetSeparatorPaddingHeight;
        using (var contentChild = ImRaii.Child("ConfigContent", new Vector2(0, -bottomContentHeight)))
        {
            if (contentChild.Success)
            {
                using var tabBar = ImRaii.TabBar("##ConfigTabBar");
                if (tabBar.Success)
                {
                    open |= Settings();

                    open |= About();
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
            case Tabs.Settings:
                DrawSettingsButtons();
                break;
            case Tabs.About:
                DrawAboutButtons();
                break;
        }
    }
}
