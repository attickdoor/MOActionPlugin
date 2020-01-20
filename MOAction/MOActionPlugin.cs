using Dalamud.Game.Command;
using Dalamud.Plugin;
using System;
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
    class MOAssistPlugin : IDalamudPlugin
    {
        public string Name => "Mouseover Action Plugin";

        public MOActionConfiguration Configuration;

        private DalamudPluginInterface pluginInterface;
        private MOAction moAction;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
            /*
            this.pluginInterface.CommandManager.AddHandler("/pmoaction", new CommandInfo(OnCommandDebugCombo)
            {
                HelpMessage = "Open a window to edit custom combo settings.",
                ShowInHelp = true
            });
            */
            this.Configuration = pluginInterface.GetPluginConfig() as MOActionConfiguration ?? new MOActionConfiguration();

            this.moAction = new MOAction(pluginInterface.TargetModuleScanner, pluginInterface.ClientState, this.Configuration);

            this.moAction.Enable();

            //this.pluginInterface.UiBuilder.OnBuildUi += UiBuilder_OnBuildUi;
        }
        /*
        private bool isImguiComboSetupOpen = false;

                private void UiBuilder_OnBuildUi()
        {
            if (!isImguiComboSetupOpen)
                return;

            var values = Enum.GetValues(typeof(MOAssistPreset)).Cast<MOAssistPreset>();
            var orderedByClassJob = values.Where(x => x != MOAssistPreset.None && x.GetAttribute<CustomComboInfoAttribute>() != null).OrderBy(x => x.GetAttribute<CustomComboInfoAttribute>().ClassJob).ToArray();

            var flagsSelected = new bool[orderedByClassJob.Length];
            for (var i = 0; i < orderedByClassJob.Length; i++)
            {
                flagsSelected[i] = Configuration.ComboPresets.HasFlag(orderedByClassJob[i]);
            }
            
            ImGui.SetNextWindowSize(new Vector2(740, 490));

            ImGui.Begin("Custom Combo Setup", ref isImguiComboSetupOpen, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar);

            ImGui.Text("This window allows you to enable and disable custom combos to your liking.");
            ImGui.Separator();

            ImGui.BeginChild("scrolling", new Vector2(0, 400), true, ImGuiWindowFlags.HorizontalScrollbar);

            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 5));

            var lastClassJob = 0;

            for (var i = 0; i < orderedByClassJob.Length; i++)
            {
                var flag = orderedByClassJob[i];
                var flagInfo = flag.GetAttribute<CustomComboInfoAttribute>();

                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 14));
                if (lastClassJob != flagInfo.ClassJob)
                {
                    ImGui.Separator();

                    lastClassJob = flagInfo.ClassJob;
                    ImGui.TextColored(new Vector4(0.0f, 0.4f, 0.7f, 1.0f), ClassJobToName(flagInfo.ClassJob));
                }
                ImGui.PopStyleVar();

                ImGui.Checkbox(flagInfo.FancyName, ref flagsSelected[i]);
                ImGui.TextColored(new Vector4(0.68f, 0.68f, 0.68f, 1.0f), $"#{i}:" + flagInfo.Description);
                ImGui.Spacing();
            }

            for (var i = 0; i < orderedByClassJob.Length; i++)
            {
                if (flagsSelected[i])
                {
                    Configuration.ComboPresets |= orderedByClassJob[i];
                }
                else
                {
                    Configuration.ComboPresets &= ~orderedByClassJob[i];
                }
            }

            ImGui.PopStyleVar();

            ImGui.EndChild();

            ImGui.Separator();

            if (ImGui.Button("Save and Close"))
            {
                this.pluginInterface.SavePluginConfig(Configuration);
                this.isImguiComboSetupOpen = false;
            }

            ImGui.End();
        }
        */
        public void Dispose()
        {
            this.moAction.Dispose();

            //this.pluginInterface.CommandManager.RemoveHandler("/pmoaction");

            this.pluginInterface.Dispose();
        }
        /*
        private void OnCommandDebugCombo(string command, string arguments)
        {
            var argumentsParts = arguments.Split();

            switch (argumentsParts[0])
            {
                case "setall":
                    {
                        foreach (var value in Enum.GetValues(typeof(MOAssistPreset)).Cast<MOAssistPreset>())
                        {
                            if (value == MOAssistPreset.None)
                                continue;

                            this.Configuration.ComboPresets |= value;
                        }

                        this.pluginInterface.Framework.Gui.Chat.Print("all SET");
                    }
                    break;
                case "unsetall":
                    {
                        foreach (var value in Enum.GetValues(typeof(MOAssistPreset)).Cast<MOAssistPreset>())
                        {
                            this.Configuration.ComboPresets &= value;
                        }

                        this.pluginInterface.Framework.Gui.Chat.Print("all UNSET");
                    }
                    break;
                case "set":
                    {
                        foreach (var value in Enum.GetValues(typeof(MOAssistPreset)).Cast<MOAssistPreset>())
                        {
                            if (value.ToString().ToLower() != argumentsParts[1].ToLower())
                                continue;

                            this.Configuration.ComboPresets |= value;
                        }
                    }
                    break;
                case "toggle":
                    {
                        foreach (var value in Enum.GetValues(typeof(MOAssistPreset)).Cast<MOAssistPreset>())
                        {
                            if (value.ToString().ToLower() != argumentsParts[1].ToLower())
                                continue;

                            this.Configuration.ComboPresets ^= value;
                        }
                    }
                    break;

                case "unset":
                    {
                        foreach (var value in Enum.GetValues(typeof(MOAssistPreset)).Cast<MOAssistPreset>())
                        {
                            if (value.ToString().ToLower() != argumentsParts[1].ToLower())
                                continue;

                            this.Configuration.ComboPresets &= ~value;
                        }
                    }
                    break;

                case "list":
                    {
                        foreach (var value in Enum.GetValues(typeof(MOAssistPreset)).Cast<MOAssistPreset>())
                        {
                            if (this.Configuration.ComboPresets.HasFlag(value))
                                this.pluginInterface.Framework.Gui.Chat.Print(value.ToString());
                        }
                    }
                    break;

                default:
                    this.isImguiComboSetupOpen = true;
                    break;
            }

            this.pluginInterface.SavePluginConfig(this.Configuration);
        }
    }*/
    }
}
