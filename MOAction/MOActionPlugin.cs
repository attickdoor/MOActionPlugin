using Dalamud.Game.Command;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.Remoting.Messaging;
using Dalamud.Game.Chat;
using ImGuiNET;
using Serilog;

namespace MOActionPlugin
{
    internal class MOAssistPlugin : IDalamudPlugin
    {
        public string Name => "Mouseover Action Plugin";

        public MOActionConfiguration Configuration;

        private DalamudPluginInterface pluginInterface;
        private MOAction moAction;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;

            this.pluginInterface.CommandManager.AddHandler("/pmoaction", new CommandInfo(OnCommandDebugCombo)
            {
                HelpMessage = "Open a window to edit custom combo settings.",
                ShowInHelp = true
            });

            Configuration = pluginInterface.GetPluginConfig() as MOActionConfiguration ?? new MOActionConfiguration();

            moAction = new MOAction(pluginInterface.TargetModuleScanner, pluginInterface.ClientState, Configuration);

            SetNewConfig();

            moAction.Enable();

            this.pluginInterface.UiBuilder.OnBuildUi += UiBuilder_OnBuildUi;
        }

        private string ClassJobToName(byte key)
        {
            switch (key)
            {
                default: return "Unknown";
                case 0: return "General";
                case 1: return "Gladiator";
                case 2: return "Pugilist";
                case 3: return "Marauder";
                case 4: return "Lancer";
                case 5: return "Archer";
                case 6: return "Conjurer";
                case 7: return "Thaumaturge";
                case 8: return "Carpenter";
                case 9: return "Blacksmith";
                case 10: return "Armorer";
                case 11: return "Goldsmith";
                case 12: return "Leatherworker";
                case 13: return "Weaver";
                case 14: return "Alchemist";
                case 15: return "Culinarian";
                case 16: return "Miner";
                case 17: return "Botanist";
                case 18: return "Fisher";
                case 19: return "Paladin";
                case 20: return "Monk";
                case 21: return "Warrior";
                case 22: return "Dragoon";
                case 23: return "Bard";
                case 24: return "White Mage";
                case 25: return "Black Mage";
                case 26: return "Arcanist";
                case 27: return "Summoner";
                case 28: return "Scholar";
                case 29: return "Rogue";
                case 30: return "Ninja";
                case 31: return "Machinist";
                case 32: return "Dark Knight";
                case 33: return "Astrologian";
                case 34: return "Samurai";
                case 35: return "Red Mage";
                case 36: return "Blue Mage";
                case 37: return "Gunbreaker";
                case 38: return "Dancer";
            }
        }

        private bool isImguiMoSetupOpen = true;

        private void UiBuilder_OnBuildUi()
        {
            if (!isImguiMoSetupOpen)
                return;

            var values = Enum.GetValues(typeof(MOActionPreset)).Cast<MOActionPreset>();
            var orderedByClassJob = values
                .Where(x => x != MOActionPreset.None && x.GetAttribute<MoActionInfoAttribute>() != null)
                .OrderBy(x => x.GetAttribute<MoActionInfoAttribute>().ClassJob).ToArray();

            var flagsSelected = new bool[orderedByClassJob.Length];
            for (var i = 0; i < orderedByClassJob.Length; i++)
                flagsSelected[i] = Configuration.MoPresets.HasFlag(orderedByClassJob[i]);

            ImGui.SetNextWindowSize(new Vector2(740, 490));

            ImGui.Begin("MouseOver action setup", ref isImguiMoSetupOpen,
                ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar);

            ImGui.Text("This window allows you to enable and disable actions which will affect your mouse over targets.");
            ImGui.Separator();

            ImGui.BeginChild("scrolling", new Vector2(0, 400), true, ImGuiWindowFlags.HorizontalScrollbar);

            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 3));

            var lastClassJob = -1;

            for (var i = 0; i < orderedByClassJob.Length; i++)
            {
                var flag = orderedByClassJob[i];
                var flagInfo = flag.GetAttribute<MoActionInfoAttribute>();

                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 14));

                if (lastClassJob != flagInfo.ClassJob)
                {
                    ImGui.Separator();

                    lastClassJob = flagInfo.ClassJob;
                    ImGui.TextColored(new Vector4(0.0f, 0.4f, 0.7f, 1.0f), ClassJobToName(flagInfo.ClassJob));
                }

                ImGui.PopStyleVar();

                ImGui.PushID("moCheck" + i);

                ImGui.Checkbox(flagInfo.Name, ref flagsSelected[i]);
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(1f, 0, 0, 1.0f),
                    flagInfo.IsPvP ? " PvP" : string.Empty);

                ImGui.PopID();

                ImGui.Spacing();
            }

            ImGui.PopStyleVar();

            ImGui.EndChild();

            ImGui.Separator();

            if (ImGui.Button("Enable All"))
            {
                for (var i = 0; i < flagsSelected.Length; i++)
                {
                    flagsSelected[i] = true;
                }
            }

            ImGui.SameLine();

            if (ImGui.Button("Disable All"))
            {
                for (var i = 0; i < flagsSelected.Length; i++)
                {
                    flagsSelected[i] = false;
                }
            }

            ImGui.Spacing();

            for (var i = 0; i < orderedByClassJob.Length; i++)
                if (flagsSelected[i])
                    Configuration.MoPresets |= orderedByClassJob[i];
                else
                    Configuration.MoPresets &= ~orderedByClassJob[i];

            if (ImGui.Button("Save and Close"))
            {
                pluginInterface.SavePluginConfig(Configuration);
                SetNewConfig();
                isImguiMoSetupOpen = false;
            }

            ImGui.End();
        }

        private void SetNewConfig()
        {
            var values = Enum.GetValues(typeof(MOActionPreset)).Cast<MOActionPreset>();
            var orderedByClassJob = values
                .Where(x => x != MOActionPreset.None && x.GetAttribute<MoActionInfoAttribute>() != null)
                .OrderBy(x => x.GetAttribute<MoActionInfoAttribute>().ClassJob).ToArray();

            var eligibleActionList = (from t in orderedByClassJob where Configuration.MoPresets.HasFlag(t) select t.GetAttribute<MoActionInfoAttribute>().ActionId).ToArray();

            this.moAction.EligibleActionList = eligibleActionList;
        }

        public void Dispose()
        {
            moAction.Dispose();

            pluginInterface.CommandManager.RemoveHandler("/pmoaction");

            pluginInterface.Dispose();
        }

        private void OnCommandDebugCombo(string command, string arguments)
        {
            isImguiMoSetupOpen = true;
        }
    }
}
