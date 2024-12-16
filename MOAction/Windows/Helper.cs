using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;

namespace MOAction.Windows;

public static class Helper
{
    public static bool DrawArrows(ref int selected, int length, int id = 0)
    {
        var changed = false;

        // Prevents changing values from triggering EndDisable
        var isMin = selected == 0;
        var isMax = selected + 1 == length;

        ImGui.SameLine(0, 2);

        using (ImRaii.Disabled(isMin))
        {
            if (ImGuiComponents.IconButton(id, FontAwesomeIcon.ArrowUp))
            {
                selected--;
                changed = true;
            }
        }

        ImGui.SameLine(0, 2);

        using (ImRaii.Disabled(isMax))
        {
            if (ImGuiComponents.IconButton(id + 1, FontAwesomeIcon.ArrowDown))
            {
                selected++;
                changed = true;
            }
        }

        return changed;
    }

    public static void Tooltip(string tooltip)
    {
        using (ImRaii.Tooltip())
        using (ImRaii.TextWrapPos(ImGui.GetFontSize() * 35.0f))
        {
            ImGui.TextUnformatted(tooltip);
        }
    }

    public static bool CtrlShiftButton(string label, string tooltip = "")
    {
        var ctrlShiftHeld = ImGui.GetIO() is { KeyCtrl: true, KeyShift: true };

        bool ret;
        using (ImRaii.Disabled(!ctrlShiftHeld))
            ret = ImGui.Button(label) && ctrlShiftHeld;

        if (!string.IsNullOrEmpty(tooltip) && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            Tooltip(tooltip);

        return ret;
    }
}
