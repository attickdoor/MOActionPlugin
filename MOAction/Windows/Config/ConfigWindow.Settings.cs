using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using MOAction.Configuration;
using Newtonsoft.Json;

namespace MOAction.Windows.Config;

public partial class ConfigWindow
{
    private int Settings()
    {
        using var tabItem = ImRaii.TabItem("Settings");
        if (!tabItem.Success)
            return 0;

        ImGui.Text("This window allows you to set up your action stacks.");
        ImGui.Text("What is an action stack? ");
        ImGui.SameLine();
        if (ImGui.Button("Click me to learn!"))
            Dalamud.Utility.Util.OpenLink("https://youtu.be/pm4eCxD90gs");

        ImGui.Checkbox("Stack entry fails if target is out of range.", ref Plugin.Configuration.RangeCheck);
        if (ImGui.Button("Copy all stacks to clipboard"))
            Plugin.CopyToClipboard(Plugin.MoAction.Stacks);

        ImGui.SameLine();
        if (ImGui.Button("Import stacks from clipboard"))
        {
            // TODO: don't wipe all existing stacks
            try
            {
                var tempStacks = Plugin.SortStacks(Plugin.RebuildStacks(JsonConvert.DeserializeObject<List<ConfigurationEntry>>(Encoding.UTF8.GetString(Convert.FromBase64String(ImGui.GetClipboardText())))));
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

        using var child = ImRaii.Child("scrolling", Vector2.Zero, true);
        if (!child.Success)
            return 1;

        // sorted stacks are grouped by job.
        using (ImRaii.PushId("Sorted Stacks"))
        {
            foreach (var x in Plugin.JobAbbreviations)
            {
                var jobName = x.Abbreviation.ExtractText();
                var entries = Plugin.SavedStacks[jobName];
                if (entries.Count == 0)
                    continue;

                using var innerId = ImRaii.PushId(jobName);
                ImGui.SetNextItemWidth(300);
                if (ImGui.CollapsingHeader(jobName))
                {
                    if (ImGui.Button("Copy All to Clipboard"))
                        Plugin.CopyToClipboard(entries.ToList());

                    using var indent = ImRaii.PushIndent();
                    DrawConfigForList(Plugin.SortedStacks[jobName]);
                }
            }
        }

        using (ImRaii.PushId("Unsorted Stacks"))
        {
            // Unsorted stacks are created when "Add stack" is clicked.
            DrawConfigForList(Plugin.NewStacks);
        }

        return 1;
    }

    private void DrawSettingsButtons()
    {
        if (ImGui.Button("Save"))
        {
            Plugin.SaveStacks();
        }

        ImGui.SameLine();
        if (ImGui.Button("Save and Close"))
        {
            IsOpen = false;
            Plugin.SaveStacks();
        }

        ImGui.SameLine();
        if (ImGui.Button("New Stack"))
        {
            if (Plugin.ClientState.LocalPlayer != null)
            {
                MoActionStack stack = new(default, null);
                var job = Plugin.ClientState.LocalPlayer.ClassJob.RowId.ToString();
                stack.Job = job;
                Plugin.NewStacks.Add(stack);
                Plugin.PluginLog.Debug($"Localplayer job was {job}");
            }
            else
            {
                Plugin.NewStacks.Add(new MoActionStack(default, []));
            }
        }
    }

    private void DrawConfigForList(ICollection<MoActionStack> list)
    {
        for (var i = 0; i < list.Count; i++)
        {
            using var id = ImRaii.PushId(i);

            var entry = list.ElementAt(i);
            if (!ImGui.CollapsingHeader(entry.BaseAction.RowId == 0 ? "Unset Action###" : $"{entry.BaseAction.Name.ExtractText()}###"))
                continue;

            // Require user to select a job, filtering actions down.
            ImGui.SetNextItemWidth(100);
            using (var combo = ImRaii.Combo("Job", entry.GetJob()))
            {
                if (combo.Success)
                {
                    foreach (var x in Plugin.JobAbbreviations)
                    {
                        var job = x.Abbreviation.ExtractText();
                        if (!ImGui.Selectable(job))
                            continue;

                        if (entry.GetJob() != null && entry.GetJob() != job)
                        {
                            entry.BaseAction = default;
                            foreach (var stackentry in entry.Entries)
                                stackentry.Action = default;
                        }

                        entry.Job = x.RowId.ToString();
                    }
                }
            }

            ImGui.SameLine();
            ImGui.SetNextItemWidth(100);
            using (var combo = ImRaii.Combo("Held Modifier Key", entry.Modifier.ToString()))
            {
                if (combo.Success)
                {
                    foreach (var vk in MoActionStack.AllKeys)
                        if (ImGui.Selectable(vk.ToString().Replace("MENU", "ALT")))
                            entry.Modifier = vk;
                }
            }

            if (entry.GetJob() != "Unset Job" || entry.GetJob() != "ADV")
            {
                using var indent = ImRaii.PushIndent();

                // Select base action.
                ImGui.SetNextItemWidth(200);
                using (var combo = ImRaii.Combo("Base Action", entry.BaseAction.RowId == 0 ? "" : entry.BaseAction.Name.ExtractText()))
                {
                    if (combo.Success)
                    {
                        foreach (var actionEntry in Plugin.JobActions[entry.GetJob()])
                        {
                            if (!ImGui.Selectable(actionEntry.Name.ExtractText()))
                                continue;

                            // By default, add UI mouseover as the first TargetType
                            entry.BaseAction = actionEntry;
                            if (entry.Entries.Count == 0)
                            {
                                entry.Entries.Add(new StackEntry(actionEntry, Plugin.TargetTypes[0]));
                            }
                            else
                            {
                                entry.Entries[0].Action = actionEntry;
                            }
                        }
                    }
                }

                if (entry.BaseAction.RowId != 0)
                {
                    using (ImRaii.PushIndent())
                    {
                        for (int j = 0; j < entry.Entries.Count; j++)
                        {
                            var stackEntry = entry.Entries[j];
                            using var innerId = ImRaii.PushId(j); // push stack entry number

                            ImGui.Text($"Ability #{entry.Entries.IndexOf(stackEntry) + 1}");
                            ImGui.SetNextItemWidth(200);
                            using (var innerCombo = ImRaii.Combo("Target", stackEntry.Target == null ? "" : stackEntry.Target.TargetName))
                            {
                                if (innerCombo.Success)
                                {
                                    foreach (var target in Plugin.TargetTypes)
                                    {
                                        if (ImGui.Selectable(target.TargetName))
                                            stackEntry.Target = target;
                                    }

                                    if (stackEntry.Action.TargetArea)
                                    {
                                        foreach (var target in Plugin.GroundTargetTypes)
                                            if (ImGui.Selectable(target.TargetName))
                                                stackEntry.Target = target;
                                    }
                                }
                            }

                            ImGui.SameLine();
                            ImGui.SetNextItemWidth(200);
                            using (var innerCombo = ImRaii.Combo("Ability", stackEntry.Action.Name.ExtractText()))
                            {
                                if (innerCombo.Success)
                                {
                                    foreach (var ability in Plugin.JobActions[entry.GetJob()])
                                    {
                                        if (!ImGui.Selectable(ability.Name.ExtractText()))
                                            continue;

                                        stackEntry.Action = ability;
                                        if (ability.TargetArea && Plugin.GroundTargetTypes.Contains(stackEntry.Target))
                                            stackEntry.Target = null;
                                    }
                                }
                            }

                            // TODO: foreach makes lists immutable, use for-loop
                            if (entry.Entries.Count > 1)
                            {
                                ImGui.SameLine();
                                if (ImGui.Button("Delete Entry"))
                                {
                                    entry.Entries.Remove(stackEntry);
                                    j--;
                                }
                            }
                        }
                    }

                    // Add new entry to bottom of stack.
                    if (ImGui.Button("Add new stack entry"))
                        entry.Entries.Add(new StackEntry(entry.BaseAction, null));

                    ImGui.SameLine();
                    if (ImGui.Button("Copy stack to clipboard"))
                        Plugin.CopyToClipboard([entry]);

                    ImGui.SameLine();
                    if (ImGui.Button("Delete Stack"))
                    {
                        list.Remove(entry);
                        Plugin.SavedStacks[entry.GetJob()].Remove(entry);
                        i--;
                    }
                }
            }
        }
    }
}
