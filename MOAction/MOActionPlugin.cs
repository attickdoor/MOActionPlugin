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

        private List<ApplicableAction> applicableActions;

        private string[] soloJobNames = { "AST", "WHM", "SCH", "SMN", "ACN", "BLM", "RDM", "BLU", "BRD", "MCH", "DNC", "DRK", "GNB", "WAR", "PLD", "DRG", "MNK", "SAM", "NIN" };
        private string[] roleActionNames = { /*"Disciple of Magic", "Disciple of War",*/ "Caster", "Ranged",  "Melee", "Tank", "Healer"};

        private bool[] flagsSelected;




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

            this.applicableActions = new List<ApplicableAction>();

            PopulateActions();
            flagsSelected = new bool[applicableActions.Count()];
            for (var i = 0; i < flagsSelected.Length; i++)
                flagsSelected[i] = false;
        }

        private bool isImguiMoSetupOpen = true;

        private void UiBuilder_OnBuildUi()
        {
            if (!isImguiMoSetupOpen)
                return;
            /*
            var values = Enum.GetValues(typeof(MOActionPreset)).Cast<MOActionPreset>();
            var orderedByClassJob = values
                .Where(x => x != MOActionPreset.None && x.GetAttribute<MoActionInfoAttribute>() != null)
                .OrderBy(x => x.GetAttribute<MoActionInfoAttribute>().ClassJob).ToArray();
            */

            ImGui.SetNextWindowSize(new Vector2(740, 490));

            ImGui.Begin("MouseOver action setup", ref isImguiMoSetupOpen,
                ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar);

            ImGui.Text("This window allows you to enable and disable actions which will affect your mouse over targets.");
            ImGui.Separator();

            ImGui.BeginChild("scrolling", new Vector2(0, 400), true, ImGuiWindowFlags.HorizontalScrollbar);

            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 3));

            ImGui.Checkbox("Enable mouseover on UI elements", ref moAction.IsGuiMOEnabled);
            ImGui.Checkbox("Enable mouseover on field entities", ref moAction.IsFieldMOEnabled);

            string lastClassJob = "";

            // Support actions first
            if (ImGui.CollapsingHeader("Support Actions"))
            {
                ImGui.Indent();
                for (var i = 0; i < applicableActions.Count(); i++)
                {
                    var action = applicableActions.ElementAt(i);
                    
                    if (action.CanTargetParty && !action.IsPvP && !action.IsRoleAction)
                    {
                        if (!lastClassJob.Equals(ClassJobCategoryToName(action.ClassJobCategory)))
                        {
                            lastClassJob = ClassJobCategoryToName(action.ClassJobCategory);
                            if (ImGui.CollapsingHeader(lastClassJob + "##Support"))
                            {
                                for (int j = i; j < applicableActions.Count(); j++)
                                {
                                    action = applicableActions.ElementAt(j);

                                    if (!lastClassJob.Equals(ClassJobCategoryToName(action.ClassJobCategory)))
                                    {
                                        break;
                                    }
                                    if (action.CanTargetParty && !action.IsPvP && !action.IsRoleAction)
                                    {
                                        ImGui.Checkbox(action.AbilityName, ref flagsSelected[j]);
                                    }
                                }

                            }
                        }
                    }
                }
                ImGui.Unindent();
            }
            lastClassJob = "";
            // Then combat actions
            if (ImGui.CollapsingHeader("Damage Actions"))
            {
                ImGui.Indent();
                for (var i = 0; i < applicableActions.Count(); i++)
                {
                    var action = applicableActions.ElementAt(i);

                    if (action.CanTargetHostile && !action.IsPvP && !action.IsRoleAction)
                    {
                        if (!lastClassJob.Equals(ClassJobCategoryToName(action.ClassJobCategory)))
                        {
                            lastClassJob = ClassJobCategoryToName(action.ClassJobCategory);
                            if (ImGui.CollapsingHeader(lastClassJob + "##Damage"))
                            {
                                for (int j = i; j < applicableActions.Count(); j++)
                                {
                                    action = applicableActions.ElementAt(j);
                                    if (!lastClassJob.Equals(ClassJobCategoryToName(action.ClassJobCategory)))
                                    {
                                        break;
                                    }
                                    if (action.CanTargetHostile && !action.IsPvP && !action.IsRoleAction)
                                    {
                                        ImGui.Checkbox(action.AbilityName, ref flagsSelected[j]);
                                    }
                                }

                            }
                        }
                    }
                }
                ImGui.Unindent();
            }
            lastClassJob = "";
            // Role Actions
            if (ImGui.CollapsingHeader("Role Actions"))
            {
                ImGui.Indent();
                for (var i = 0; i < applicableActions.Count(); i++)
                {
                    var action = applicableActions.ElementAt(i);
                    if (action.IsRoleAction)
                    {
                        if (!lastClassJob.Equals(ClassJobCategoryToName(action.ClassJobCategory)))
                        {
                            lastClassJob = ClassJobCategoryToName(action.ClassJobCategory);
                            if (ImGui.CollapsingHeader(lastClassJob))
                            {
                                for (int j = i; j < applicableActions.Count(); j++)
                                {
                                    action = applicableActions.ElementAt(j);
                                    if (!lastClassJob.Equals(ClassJobCategoryToName(action.ClassJobCategory)))
                                    {
                                        break;
                                    }
                                    if (!action.IsPvP && (action.CanTargetHostile || action.CanTargetParty))
                                        ImGui.Checkbox(action.AbilityName, ref flagsSelected[j]);
                                }

                            }
                        }
                    }
                }
                ImGui.Unindent();
            }
            lastClassJob = "";
            // Repeat for PvP
            if (ImGui.CollapsingHeader("PvP Support"))
            {
                ImGui.Indent();
                for (var i = 0; i < applicableActions.Count(); i++)
                {
                    var action = applicableActions.ElementAt(i);
                    if (action.CanTargetParty && action.IsPvP)
                    {
                        if (!lastClassJob.Equals(ClassJobCategoryToName(action.ClassJobCategory)))
                        {
                            lastClassJob = ClassJobCategoryToName(action.ClassJobCategory);
                            if (ImGui.CollapsingHeader(lastClassJob + "##PVP Support"))
                            {
                                for (int j = i; j < applicableActions.Count(); j++)
                                {
                                    action = applicableActions.ElementAt(j);
                                    if (!lastClassJob.Equals(ClassJobCategoryToName(action.ClassJobCategory)))
                                    {
                                        break;
                                    }
                                    if (action.CanTargetParty && action.IsPvP)
                                    {
                                        ImGui.Checkbox(action.AbilityName, ref flagsSelected[j]);
                                    }
                                }

                            }
                        }
                    }
                }
                ImGui.Unindent();
            }
            lastClassJob = "";
            if (ImGui.CollapsingHeader("PvP Damage"))
            {
                ImGui.Indent();
                for (var i = 0; i < applicableActions.Count(); i++)
                {
                    var action = applicableActions.ElementAt(i);
                    if (action.CanTargetHostile && action.IsPvP)
                    {
                        if (!lastClassJob.Equals(ClassJobCategoryToName(action.ClassJobCategory)))
                        {
                            lastClassJob = ClassJobCategoryToName(action.ClassJobCategory);
                            if (ImGui.CollapsingHeader(lastClassJob + "##PVP Damage"))
                            {

                                for (int j = i; j < applicableActions.Count(); j++)
                                {
                                    action = applicableActions.ElementAt(j);
                                    if (!lastClassJob.Equals(ClassJobCategoryToName(action.ClassJobCategory)))
                                    {
                                        break;
                                    }
                                    if (action.CanTargetHostile && action.IsPvP)
                                    {
                                        ImGui.Checkbox(action.AbilityName, ref flagsSelected[j]);
                                    }
                                }

                            }
                        }
                    }
                }
                ImGui.Unindent();
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

            ImGui.SameLine();

            if (ImGui.Button("Save and Close"))
            {
                pluginInterface.SavePluginConfig(Configuration);
                UpdateList(flagsSelected);
                SetNewConfig();
                isImguiMoSetupOpen = false;
            }

            ImGui.Spacing();
            /*
            for (var i = 0; i < orderedByClassJob.Length; i++)
                if (flagsSelected[i])
                    Configuration.MoPresets |= orderedByClassJob[i];
                else
                    Configuration.MoPresets &= ~orderedByClassJob[i];
            */
            ImGui.End();
        }

        private void UpdateList(bool[] flags)
        {
            for (int i = 0; i < applicableActions.Count(); i++)
            {
                if (flags[i] == true)
                {
                    moAction.enableAction(applicableActions.ElementAt(i).ID);
                }
                else
                {
                    moAction.removeAction(applicableActions.ElementAt(i).ID);
                }
            }
        }

        private void SetNewConfig()
        {
            var values = Enum.GetValues(typeof(MOActionPreset)).Cast<MOActionPreset>();
            var orderedByClassJob = values
                .Where(x => x != MOActionPreset.None && x.GetAttribute<MoActionInfoAttribute>() != null)
                .OrderBy(x => x.GetAttribute<MoActionInfoAttribute>().ClassJob).ToArray();

            var eligibleActionList = (from t in orderedByClassJob where Configuration.MoPresets.HasFlag(t) select t.GetAttribute<MoActionInfoAttribute>().ActionId).ToArray();

            //this.moAction.enabledActions = eligibleActionList;
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

        private string ClassJobCategoryToName(byte key)
        {
            switch (key)
            {
                case 1: return "All Classes";
                case 2: return "GLA";
                case 3: return "PGL";
                case 4: return "MRD";
                case 5: return "LNC";
                case 6: return "ARC";
                case 7: return "CNJ";
                case 8: return "THM";
                case 9: return "CRP";
                case 10: return "BSM";
                case 11: return "ARM";
                case 12: return "GSM";
                case 13: return "LTW";
                case 14: return "WVR";
                case 15: return "ALC";
                case 16: return "CUL";
                case 17: return "MIN";
                case 18: return "BTN";
                case 19: return "FSH";
                case 20: return "PLD";
                case 21: return "MNK";
                case 22: return "WAR";
                case 23: return "DRG";
                case 24: return "BRD";
                case 25: return "WHM";
                case 26: return "BLM";
                case 27: return "ACN";
                case 28: return "SMN";
                case 29: return "SCH";
                case 30: return "Disciple of War";
                case 31: return "Disciple of Magic";
                case 32: return "Disciple of the Land";
                case 33: return "Disciple of the Hand";
                case 34: return "Disciples of War or Magic";
                case 35: return "Disciples of the Land or Hand";
                case 36: return "Any Disciple of War excluding gladiators";
                case 37: return "PLD WAR DRK";
                case 38: return "PLD";
                case 39: return "PLD WAR DRK";
                case 40: return "MNK WAR DRG BRD NIN";
                case 41: return "MNK";
                case 42: return "MNK WAR DRG BRD NIN";
                case 43: return "PLD MNK WAR DRG DRK";
                case 44: return "WAR";
                case 45: return "PLD MNK WAR DRG DRK";
                case 46: return "MNK DRG BRD NIN MCH";
                case 47: return "DRG";
                case 48: return "MNK DRG BRD NIN MCH";
                case 49: return "BRD BLM SMN MCH";
                case 50: return "BRD";
                case 51: return "BRD MCH";
                case 52: return "PLD WHM SCH AST";
                case 53: return "WHM";
                case 54: return "WHM BLM SMN SCH AST";
                case 55: return "BLM";
                case 56: return "PLD WHM BLM";
                case 57: return "PLD BLM";
                case 58: return "PLD WHM";
                case 59: return "Tank";
                case 60: return "PLD WAR DRG DRK GNB";
                case 61: return "Healer";
                case 62: return "WHM BLM SMN SCH AST";
                case 63: return "Caster";
                case 64: return "Healer";
                case 65: return "MNK SAM";
                case 66: return "Ranged";
                case 67: return "MNK DRG NIN";
                case 68: return "ACN";
                case 69: return "SMN";
                case 70: return "Any Disciple of the Hand excluding culinarians";
                case 71: return "WHM BLM SMN SCH";
                case 72: return "WHM BLM SMN SCH";
                case 73: return "Healer";
                case 84: return "MNK DRG SAM";
                case 85: return "Jobs of the Disciples of War or Magic";
                case 86: return "PLD WAR DRK GNB MNK DRG NIN SAM";
                case 87: return "BRD MCH DNC BLM SMN RDM WHM SCH AST";
                case 88: return "PLD MNK WAR DRG BRD NIN MCH";
                case 89: return "Caster";
                case 90: return "WHM BRD BLM SMN SCH MCH AST";
                case 91: return "ROG";
                case 92: return "NIN";
                case 93: return "NIN";
                case 94: return "NIN";
                case 95: return "NIN";
                case 96: return "MCH";
                case 97: return "MNK DRG BRD NIN MCH";
                case 98: return "DRK";
                case 99: return "AST";
                case 100: return "BRD NIN MCH";
                case 101: return "MNK DRG NIN";
                case 102: return "MNK NIN SAM";
                case 103: return "NIN";
                case 105: return "BRD NIN MCH DNC";
                case 106: return "BRD";
                case 107: return "PLD MNK WAR DRG BRD WHM BLM SMN SCH NIN MCH DRK AST";
                case 108: return "Disciples of War or Magic";
                case 110: return "Jobs of the Disciples of War or Magic";
                case 111: return "SAM";
                case 112: return "RDM";
                case 113: return "Tank";
                case 114: return "Melee";
                case 115: return "Ranged";
                case 116: return "Caster";
                case 117: return "Healer";
                case 118: return "Disciple of War";
                case 119: return "MNK DRG BLM SMN NIN SAM RDM BLU";
                case 120: return "Disciple of Magic";
                case 121: return "Tank";
                case 122: return "Melee";
                case 123: return "Ranged";
                case 124: return "BLM SMN RDM BLU";
                case 125: return "Healer";
                case 126: return "Disciple of War";
                case 127: return "MNK DRG BLM SMN NIN SAM RDM BLU";
                case 128: return "Disciple of Magic";
                case 129: return "BLU";
                case 130: return "All classes and jobs excluding limited jobs";
                case 131: return "MNK DRG BRD BLM SMN NIN MCH SAM RDM DNC";
                case 132: return "MNK DRG BRD WHM BLM SMN SCH NIN MCH AST SAM RDM DNC";
                case 133: return "Healer";
                case 134: return "Tank";
                case 135: return "PLD MNK WAR DRG BRD BLM SMN NIN MCH DRK SAM RDM GNB DNC";
                case 136: return "PLD WAR WHM SCH DRK AST GNB";
                case 137: return "PLD MNK WAR DRG BRD WHM BLM SMN SCH NIN MCH DRK AST SAM RDM GNB DNC";
                case 138: return "PLD MNK WAR DRG NIN DRK SAM GNB";
                case 139: return "Ranged";
                case 140: return "Disciple of Magic";
                case 141: return "PLD MNK WAR DRG BRD WHM BLM SMN SCH MCH DRK AST SAM RDM GNB DNC";
                case 142: return "Any Disciple of War or Magic excluding limited jobs";
                case 143: return "Disciples of War excluding limited jobs";
                case 144: return "Any Disciple of Magic excluding limited jobs";
                case 145: return "Disciple of War";
                case 146: return "Any job of the Disciples of War or Magic excluding limited jobs";
                case 147: return "Caster";
                case 148: return "Melee";
                case 149: return "GNB";
                case 150: return "DNC";
                case 156: return "Tank excluding limited jobs";
                case 157: return "Healer excluding limited jobs";
                case 158: return "Physical DPS excluding limited jobs";
                case 159: return "Magical DPS excluding limited jobs";
                case 160: return "SCH";
                case 161: return "Disciple of War";
                default: return "Unknown";

            }
        }

        private void SortActions()
        {
            List<ApplicableAction> tmp = new List<ApplicableAction>();
            foreach (string elem in soloJobNames)
            {
                foreach (ApplicableAction action in applicableActions)
                {
                    if (ClassJobCategoryToName(action.ClassJobCategory).Contains(elem) && !action.IsRoleAction)
                        tmp.Add(action);
                }
            }

            foreach (string elem in roleActionNames)
            {
                foreach (ApplicableAction action in applicableActions)
                {
                    if (ClassJobCategoryToName(action.ClassJobCategory).Contains(elem) && action.IsRoleAction)
                        tmp.Add(action);
                }
            }
            applicableActions.Clear();
            foreach (ApplicableAction elem in tmp)
            {
                applicableActions.Add(elem);
            }
        }

        private void PopulateActions()
        {
            applicableActions.Add(new ApplicableAction(9, "Fast Blade", false, false, false, false, true, 38, false));
            applicableActions.Add(new ApplicableAction(15, "Riot Blade", false, false, false, false, true, 38, false));
            applicableActions.Add(new ApplicableAction(16, "Shield Bash", false, false, false, false, true, 38, false));
            applicableActions.Add(new ApplicableAction(21, "Rage of Halone", false, false, false, false, true, 38, false));
            applicableActions.Add(new ApplicableAction(24, "Shield Lob", false, false, false, false, true, 38, false));
            applicableActions.Add(new ApplicableAction(27, "Cover", false, false, true, false, false, 20, false));
            applicableActions.Add(new ApplicableAction(29, "Spirits Within", false, false, false, false, true, 20, false));
            applicableActions.Add(new ApplicableAction(31, "Heavy Swing", false, false, false, false, true, 44, false));
            applicableActions.Add(new ApplicableAction(37, "Maim", false, false, false, false, true, 44, false));
            applicableActions.Add(new ApplicableAction(41, "Overpower", false, false, false, false, true, 44, false));
            applicableActions.Add(new ApplicableAction(42, "Storm's Path", false, false, false, false, true, 44, false));
            applicableActions.Add(new ApplicableAction(43, "Holmgang", false, true, false, false, true, 44, false));
            applicableActions.Add(new ApplicableAction(45, "Storm's Eye", false, false, false, false, true, 44, false));
            applicableActions.Add(new ApplicableAction(46, "Tomahawk", false, false, false, false, true, 44, false));
            applicableActions.Add(new ApplicableAction(49, "Inner Beast", false, false, false, false, true, 22, false));
            applicableActions.Add(new ApplicableAction(53, "Bootshine", false, false, false, false, true, 41, false));
            applicableActions.Add(new ApplicableAction(54, "True Strike", false, false, false, false, true, 41, false));
            applicableActions.Add(new ApplicableAction(56, "Snap Punch", false, false, false, false, true, 41, false));
            applicableActions.Add(new ApplicableAction(61, "Twin Snakes", false, false, false, false, true, 41, false));
            applicableActions.Add(new ApplicableAction(66, "Demolish", false, false, false, false, true, 41, false));
            applicableActions.Add(new ApplicableAction(71, "Shoulder Tackle", false, false, false, false, true, 21, false));
            applicableActions.Add(new ApplicableAction(74, "Dragon Kick", false, false, false, false, true, 21, false));
            applicableActions.Add(new ApplicableAction(75, "True Thrust", false, false, false, false, true, 47, false));
            applicableActions.Add(new ApplicableAction(78, "Vorpal Thrust", false, false, false, false, true, 47, false));
            applicableActions.Add(new ApplicableAction(84, "Full Thrust", false, false, false, false, true, 47, false));
            applicableActions.Add(new ApplicableAction(86, "Doom Spike", false, false, false, false, true, 23, false));
            applicableActions.Add(new ApplicableAction(87, "Disembowel", false, false, false, false, true, 47, false));
            applicableActions.Add(new ApplicableAction(88, "Chaos Thrust", false, false, false, false, true, 47, false));
            applicableActions.Add(new ApplicableAction(90, "Piercing Talon", false, false, false, false, true, 47, false));
            applicableActions.Add(new ApplicableAction(92, "Jump", false, false, false, false, true, 23, false));
            applicableActions.Add(new ApplicableAction(95, "Spineshatter Dive", false, false, false, false, true, 23, false));
            applicableActions.Add(new ApplicableAction(96, "Dragonfire Dive", false, false, false, false, true, 23, false));
            applicableActions.Add(new ApplicableAction(97, "Heavy Shot", false, false, false, false, true, 50, false));
            applicableActions.Add(new ApplicableAction(98, "Straight Shot", false, false, false, false, true, 50, false));
            applicableActions.Add(new ApplicableAction(100, "Venomous Bite", false, false, false, false, true, 50, false));
            applicableActions.Add(new ApplicableAction(106, "Quick Nock", false, false, false, false, true, 50, false));
            applicableActions.Add(new ApplicableAction(110, "Bloodletter", false, false, false, false, true, 50, false));
            applicableActions.Add(new ApplicableAction(112, "Repelling Shot", false, false, false, false, true, 50, false));
            applicableActions.Add(new ApplicableAction(113, "Windbite", false, false, false, false, true, 50, false));
            applicableActions.Add(new ApplicableAction(114, "Mage's Ballad", false, false, false, false, true, 24, false));
            applicableActions.Add(new ApplicableAction(116, "Army's Paeon", false, false, false, false, true, 24, false));
            applicableActions.Add(new ApplicableAction(117, "Rain of Death", false, false, false, false, true, 24, false));
            applicableActions.Add(new ApplicableAction(119, "Stone", false, false, false, false, true, 53, false));
            applicableActions.Add(new ApplicableAction(120, "Cure", false, true, true, true, false, 53, false));
            applicableActions.Add(new ApplicableAction(121, "Aero", false, false, false, false, true, 53, false));
            applicableActions.Add(new ApplicableAction(125, "Raise", false, false, true, true, false, 53, false));
            applicableActions.Add(new ApplicableAction(127, "Stone II", false, false, false, false, true, 53, false));
            applicableActions.Add(new ApplicableAction(131, "Cure III", false, true, true, false, false, 25, false));
            applicableActions.Add(new ApplicableAction(132, "Aero II", false, false, false, false, true, 53, false));
            applicableActions.Add(new ApplicableAction(134, "Fluid Aura", false, false, false, false, true, 53, false));
            applicableActions.Add(new ApplicableAction(135, "Cure II", false, true, true, true, false, 53, false));
            applicableActions.Add(new ApplicableAction(137, "Regen", false, true, true, true, false, 25, false));
            applicableActions.Add(new ApplicableAction(140, "Benediction", false, true, true, true, false, 25, false));
            applicableActions.Add(new ApplicableAction(141, "Fire", false, false, false, false, true, 55, false));
            applicableActions.Add(new ApplicableAction(142, "Blizzard", false, false, false, false, true, 55, false));
            applicableActions.Add(new ApplicableAction(144, "Thunder", false, false, false, false, true, 55, false));
            applicableActions.Add(new ApplicableAction(145, "Sleep", false, false, false, false, true, 55, false));
            applicableActions.Add(new ApplicableAction(147, "Fire II", false, false, false, false, true, 55, false));
            applicableActions.Add(new ApplicableAction(152, "Fire III", false, false, false, false, true, 55, false));
            applicableActions.Add(new ApplicableAction(153, "Thunder III", false, false, false, false, true, 26, false));
            applicableActions.Add(new ApplicableAction(154, "Blizzard III", false, false, false, false, true, 26, false));
            applicableActions.Add(new ApplicableAction(155, "Aetherial Manipulation", false, false, true, false, false, 55, false));
            applicableActions.Add(new ApplicableAction(156, "Scathe", false, false, false, false, true, 55, false));
            applicableActions.Add(new ApplicableAction(159, "Freeze", false, false, false, false, true, 26, false));
            applicableActions.Add(new ApplicableAction(162, "Flare", false, false, false, false, true, 26, false));
            applicableActions.Add(new ApplicableAction(163, "Ruin", false, false, false, false, true, 69, false));
            applicableActions.Add(new ApplicableAction(164, "Bio", false, false, false, false, true, 69, false));
            applicableActions.Add(new ApplicableAction(167, "Energy Drain", false, false, false, false, true, 29, false));
            applicableActions.Add(new ApplicableAction(168, "Miasma", false, false, false, false, true, 69, false));
            applicableActions.Add(new ApplicableAction(172, "Ruin II", false, false, false, false, true, 69, false));
            applicableActions.Add(new ApplicableAction(173, "Resurrection", false, false, true, true, false, 68, false));
            applicableActions.Add(new ApplicableAction(174, "Bane", false, false, false, false, true, 69, false));
            applicableActions.Add(new ApplicableAction(178, "Bio II", false, false, false, false, true, 69, false));
            applicableActions.Add(new ApplicableAction(181, "Fester", false, false, false, false, true, 69, false));
            applicableActions.Add(new ApplicableAction(184, "Enkindle", false, false, false, false, true, 28, false));
            applicableActions.Add(new ApplicableAction(185, "Adloquium", false, true, true, true, false, 29, false));
            applicableActions.Add(new ApplicableAction(188, "Sacred Soil", false, false, false, false, false, 29, false));
            applicableActions.Add(new ApplicableAction(189, "Lustrate", false, true, true, true, false, 29, false));
            applicableActions.Add(new ApplicableAction(190, "Physick", false, true, true, true, false, 29, false));
            applicableActions.Add(new ApplicableAction(1584, "Purify", false, true, true, false, false, 125, true));
            applicableActions.Add(new ApplicableAction(2240, "Spinning Edge", false, false, false, false, true, 93, false));
            applicableActions.Add(new ApplicableAction(2242, "Gust Slash", false, false, false, false, true, 93, false));
            applicableActions.Add(new ApplicableAction(2246, "Assassinate", false, false, false, false, true, 92, false));
            applicableActions.Add(new ApplicableAction(2247, "Throwing Dagger", false, false, false, false, true, 93, false));
            applicableActions.Add(new ApplicableAction(2248, "Mug", false, false, false, false, true, 93, false));
            applicableActions.Add(new ApplicableAction(2255, "Aeolian Edge", false, false, false, false, true, 93, false));
            applicableActions.Add(new ApplicableAction(2257, "Shadow Fang", false, false, false, false, true, 93, false));
            applicableActions.Add(new ApplicableAction(2258, "Trick Attack", false, false, false, false, true, 93, false));
            applicableActions.Add(new ApplicableAction(2262, "Shukuchi", false, false, false, false, false, 92, false));
            applicableActions.Add(new ApplicableAction(2866, "Split Shot", false, false, false, false, true, 96, false));
            applicableActions.Add(new ApplicableAction(2868, "Slug Shot", false, false, false, false, true, 96, false));
            applicableActions.Add(new ApplicableAction(2870, "Spread Shot", false, false, false, false, true, 96, false));
            applicableActions.Add(new ApplicableAction(2872, "Hot Shot", false, false, false, false, true, 96, false));
            applicableActions.Add(new ApplicableAction(2873, "Clean Shot", false, false, false, false, true, 96, false));
            applicableActions.Add(new ApplicableAction(2874, "Gauss Round", false, false, false, false, true, 96, false));
            applicableActions.Add(new ApplicableAction(2878, "Wildfire", false, false, false, false, true, 96, false));
            applicableActions.Add(new ApplicableAction(2890, "Ricochet", false, false, false, false, true, 96, false));
            applicableActions.Add(new ApplicableAction(3360, "Raw Destruction", false, false, false, false, true, 122, true));
            applicableActions.Add(new ApplicableAction(3361, "Cometeor", false, false, false, false, false, 89, true));
            applicableActions.Add(new ApplicableAction(3538, "Goring Blade", false, false, false, false, true, 20, false));
            applicableActions.Add(new ApplicableAction(3539, "Royal Authority", false, false, false, false, true, 20, false));
            applicableActions.Add(new ApplicableAction(3541, "Clemency", false, true, true, true, false, 20, false));
            applicableActions.Add(new ApplicableAction(3543, "Tornado Kick", false, false, false, false, true, 21, false));
            applicableActions.Add(new ApplicableAction(3549, "Fell Cleave", false, false, false, false, true, 22, false));
            applicableActions.Add(new ApplicableAction(3554, "Fang and Claw", false, false, false, false, true, 23, false));
            applicableActions.Add(new ApplicableAction(3555, "Geirskogul", false, false, false, false, true, 23, false));
            applicableActions.Add(new ApplicableAction(3556, "Wheeling Thrust", false, false, false, false, true, 23, false));
            applicableActions.Add(new ApplicableAction(3558, "Empyreal Arrow", false, false, false, false, true, 24, false));
            applicableActions.Add(new ApplicableAction(3559, "the Wanderer's Minuet", false, false, false, false, true, 24, false));
            applicableActions.Add(new ApplicableAction(3560, "Iron Jaws", false, false, false, false, true, 24, false));
            applicableActions.Add(new ApplicableAction(3561, "the Warden's Paean", false, true, true, false, false, 24, false));
            applicableActions.Add(new ApplicableAction(3562, "Sidewinder", false, false, false, false, true, 24, false));
            applicableActions.Add(new ApplicableAction(3563, "Armor Crush", false, false, false, false, true, 92, false));
            applicableActions.Add(new ApplicableAction(3566, "Dream Within a Dream", false, false, false, false, true, 92, false));
            applicableActions.Add(new ApplicableAction(3568, "Stone III", false, false, false, false, true, 25, false));
            applicableActions.Add(new ApplicableAction(3569, "Asylum", false, false, false, false, false, 25, false));
            applicableActions.Add(new ApplicableAction(3570, "Tetragrammaton", false, true, true, true, false, 25, false));
            applicableActions.Add(new ApplicableAction(3573, "Ley Lines", false, false, false, false, false, 26, false));
            applicableActions.Add(new ApplicableAction(3576, "Blizzard IV", false, false, false, false, true, 26, false));
            applicableActions.Add(new ApplicableAction(3577, "Fire IV", false, false, false, false, true, 26, false));
            applicableActions.Add(new ApplicableAction(3578, "Painflare", false, false, false, false, true, 28, false));
            applicableActions.Add(new ApplicableAction(3579, "Ruin III", false, false, false, false, true, 28, false));
            applicableActions.Add(new ApplicableAction(3580, "Tri-disaster", false, false, false, false, true, 28, false));
            applicableActions.Add(new ApplicableAction(3582, "Deathflare", false, false, false, false, true, 28, false));
            applicableActions.Add(new ApplicableAction(3584, "Broil", false, false, false, false, true, 29, false));
            applicableActions.Add(new ApplicableAction(3585, "Deployment Tactics", false, true, true, false, false, 29, false));
            applicableActions.Add(new ApplicableAction(3594, "Benefic", false, true, true, true, false, 99, false));
            applicableActions.Add(new ApplicableAction(3595, "Aspected Benefic", false, true, true, true, false, 99, false));
            applicableActions.Add(new ApplicableAction(3596, "Malefic", false, false, false, false, true, 99, false));
            applicableActions.Add(new ApplicableAction(3598, "Malefic II", false, false, false, false, true, 99, false));
            applicableActions.Add(new ApplicableAction(3599, "Combust", false, false, false, false, true, 99, false));
            applicableActions.Add(new ApplicableAction(3603, "Ascend", false, false, true, true, false, 99, false));
            applicableActions.Add(new ApplicableAction(3608, "Combust II", false, false, false, false, true, 99, false));
            applicableActions.Add(new ApplicableAction(3610, "Benefic II", false, true, true, true, false, 99, false));
            applicableActions.Add(new ApplicableAction(3612, "Synastry", false, false, true, false, false, 99, false));
            applicableActions.Add(new ApplicableAction(3614, "Essential Dignity", false, true, true, true, false, 99, false));
            applicableActions.Add(new ApplicableAction(3615, "Gravity", false, false, false, false, true, 99, false));
            applicableActions.Add(new ApplicableAction(3617, "Hard Slash", false, false, false, false, true, 98, false));
            applicableActions.Add(new ApplicableAction(3623, "Syphon Strike", false, false, false, false, true, 98, false));
            applicableActions.Add(new ApplicableAction(3624, "Unmend", false, false, false, false, true, 98, false));
            applicableActions.Add(new ApplicableAction(3632, "Souleater", false, false, false, false, true, 98, false));
            applicableActions.Add(new ApplicableAction(3639, "Salted Earth", false, false, false, false, false, 98, false));
            applicableActions.Add(new ApplicableAction(3640, "Plunge", false, false, false, false, true, 98, false));
            applicableActions.Add(new ApplicableAction(3641, "Abyssal Drain", false, false, false, false, true, 98, false));
            applicableActions.Add(new ApplicableAction(3643, "Carve and Spit", false, false, false, false, true, 98, false));
            applicableActions.Add(new ApplicableAction(4249, "Terminal Velocity", false, false, false, false, true, 123, true));
            applicableActions.Add(new ApplicableAction(7382, "Intervention", false, false, true, false, false, 20, false));
            applicableActions.Add(new ApplicableAction(7383, "Requiescat", false, false, false, false, true, 20, false));
            applicableActions.Add(new ApplicableAction(7384, "Holy Spirit", false, false, false, false, true, 20, false));
            applicableActions.Add(new ApplicableAction(7386, "Onslaught", false, false, false, false, true, 22, false));
            applicableActions.Add(new ApplicableAction(7387, "Upheaval", false, false, false, false, true, 22, false));
            applicableActions.Add(new ApplicableAction(7392, "Bloodspiller", false, false, false, false, true, 98, false));
            applicableActions.Add(new ApplicableAction(7393, "The Blackest Night", false, true, true, false, false, 98, false));
            applicableActions.Add(new ApplicableAction(7397, "Sonic Thrust", false, false, false, false, true, 23, false));
            applicableActions.Add(new ApplicableAction(7398, "Dragon Sight", false, true, true, false, false, 23, false));
            applicableActions.Add(new ApplicableAction(7399, "Mirage Dive", false, false, false, false, true, 23, false));
            applicableActions.Add(new ApplicableAction(7401, "HellfMedium", false, false, false, false, true, 92, false));
            applicableActions.Add(new ApplicableAction(7402, "Bhavacakra", false, false, false, false, true, 92, false));
            applicableActions.Add(new ApplicableAction(7404, "Pitch Perfect", false, false, false, false, true, 24, false));
            applicableActions.Add(new ApplicableAction(7406, "Caustic Bite", false, false, false, false, true, 24, false));
            applicableActions.Add(new ApplicableAction(7407, "Stormbite", false, false, false, false, true, 24, false));
            applicableActions.Add(new ApplicableAction(7408, "Nature's Minne", false, true, true, false, false, 24, false));
            applicableActions.Add(new ApplicableAction(7409, "Refulgent Arrow", false, false, false, false, true, 24, false));
            applicableActions.Add(new ApplicableAction(7410, "Heat Blast", false, false, false, false, true, 96, false));
            applicableActions.Add(new ApplicableAction(7411, "Heated Split Shot", false, false, false, false, true, 96, false));
            applicableActions.Add(new ApplicableAction(7412, "Heated Slug Shot", false, false, false, false, true, 96, false));
            applicableActions.Add(new ApplicableAction(7413, "Heated Clean Shot", false, false, false, false, true, 96, false));
            applicableActions.Add(new ApplicableAction(7419, "Between the Lines", false, false, false, false, false, 26, false));
            applicableActions.Add(new ApplicableAction(7420, "Thunder IV", false, false, false, false, true, 26, false));
            applicableActions.Add(new ApplicableAction(7422, "Foul", false, false, false, false, true, 26, false));
            applicableActions.Add(new ApplicableAction(7424, "Bio III", false, false, false, false, true, 28, false));
            applicableActions.Add(new ApplicableAction(7425, "Miasma III", false, false, false, false, true, 28, false));
            applicableActions.Add(new ApplicableAction(7429, "Enkindle Bahamut", false, false, false, false, true, 28, false));
            applicableActions.Add(new ApplicableAction(7431, "Stone IV", false, false, false, false, true, 25, false));
            applicableActions.Add(new ApplicableAction(7432, "Divine Benison", false, true, true, false, false, 25, false));
            applicableActions.Add(new ApplicableAction(7434, "Excogitation", false, true, true, false, false, 29, false));
            applicableActions.Add(new ApplicableAction(7435, "Broil II", false, false, false, false, true, 29, false));
            applicableActions.Add(new ApplicableAction(7436, "Chain Stratagem", false, false, false, false, true, 29, false));
            applicableActions.Add(new ApplicableAction(7437, "Aetherpact", false, true, true, false, false, 29, false));
            applicableActions.Add(new ApplicableAction(7439, "Earthly Star", false, false, false, false, false, 99, false));
            applicableActions.Add(new ApplicableAction(7442, "Malefic III", false, false, false, false, true, 99, false));
            applicableActions.Add(new ApplicableAction(7447, "Thunder II", false, false, false, false, true, 55, false));
            applicableActions.Add(new ApplicableAction(7477, "Hakaze", false, false, false, false, true, 111, false));
            applicableActions.Add(new ApplicableAction(7478, "Jinpu", false, false, false, false, true, 111, false));
            applicableActions.Add(new ApplicableAction(7479, "Shifu", false, false, false, false, true, 111, false));
            applicableActions.Add(new ApplicableAction(7480, "Yukikaze", false, false, false, false, true, 111, false));
            applicableActions.Add(new ApplicableAction(7481, "Gekko", false, false, false, false, true, 111, false));
            applicableActions.Add(new ApplicableAction(7482, "Kasha", false, false, false, false, true, 111, false));
            applicableActions.Add(new ApplicableAction(7483, "Fuga", false, false, false, false, true, 111, false));
            applicableActions.Add(new ApplicableAction(7486, "Enpi", false, false, false, false, true, 111, false));
            applicableActions.Add(new ApplicableAction(7490, "Hissatsu: Shinten", false, false, false, false, true, 111, false));
            applicableActions.Add(new ApplicableAction(7492, "Hissatsu: Gyoten", false, false, false, false, true, 111, false));
            applicableActions.Add(new ApplicableAction(7493, "Hissatsu: Yaten", false, false, false, false, true, 111, false));
            applicableActions.Add(new ApplicableAction(7496, "Hissatsu: Guren", false, false, false, false, true, 111, false));
            applicableActions.Add(new ApplicableAction(7501, "Hissatsu: Seigan", false, false, false, false, true, 111, false));
            applicableActions.Add(new ApplicableAction(7503, "Jolt", false, false, false, false, true, 112, false));
            applicableActions.Add(new ApplicableAction(7504, "Riposte", false, false, false, false, true, 112, false));
            applicableActions.Add(new ApplicableAction(7505, "Verthunder", false, false, false, false, true, 112, false));
            applicableActions.Add(new ApplicableAction(7506, "Corps-a-corps", false, false, false, false, true, 112, false));
            applicableActions.Add(new ApplicableAction(7507, "Veraero", false, false, false, false, true, 112, false));
            applicableActions.Add(new ApplicableAction(7509, "Scatter", false, false, false, false, true, 112, false));
            applicableActions.Add(new ApplicableAction(7510, "Verfire", false, false, false, false, true, 112, false));
            applicableActions.Add(new ApplicableAction(7511, "Verstone", false, false, false, false, true, 112, false));
            applicableActions.Add(new ApplicableAction(7512, "Zwerchhau", false, false, false, false, true, 112, false));
            applicableActions.Add(new ApplicableAction(7513, "Moulinet", false, false, false, false, true, 112, false));
            applicableActions.Add(new ApplicableAction(7514, "Vercure", false, true, true, true, false, 112, false));
            applicableActions.Add(new ApplicableAction(7515, "Displacement", false, false, false, false, true, 112, false));
            applicableActions.Add(new ApplicableAction(7516, "Redoublement", false, false, false, false, true, 112, false));
            applicableActions.Add(new ApplicableAction(7517, "Fleche", false, false, false, false, true, 112, false));
            applicableActions.Add(new ApplicableAction(7519, "Contre Sixte", false, false, false, false, true, 112, false));
            applicableActions.Add(new ApplicableAction(7523, "Verraise", false, false, true, true, false, 112, false));
            applicableActions.Add(new ApplicableAction(7524, "Jolt II", false, false, false, false, true, 112, false));
            applicableActions.Add(new ApplicableAction(7531, "Rampart", true, true, false, false, false, 113, false));
            applicableActions.Add(new ApplicableAction(7533, "Provoke", true, false, false, false, true, 113, false));
            applicableActions.Add(new ApplicableAction(7535, "Reprisal", true, true, false, false, false, 113, false));
            applicableActions.Add(new ApplicableAction(7537, "Shirk", true, false, true, false, false, 113, false));
            applicableActions.Add(new ApplicableAction(7538, "Interject", true, false, false, false, true, 113, false));
            applicableActions.Add(new ApplicableAction(7540, "Low Blow", true, false, false, false, true, 113, false));
            applicableActions.Add(new ApplicableAction(7541, "Second Wind", true, true, false, false, false, 118, false));
            applicableActions.Add(new ApplicableAction(7542, "Bloodbath", true, true, false, false, false, 114, false));
            applicableActions.Add(new ApplicableAction(7546, "True North", true, true, false, false, false, 114, false));
            applicableActions.Add(new ApplicableAction(7548, "Arm's Length", true, true, false, false, false, 161, false));
            applicableActions.Add(new ApplicableAction(7549, "Feint", true, false, false, false, true, 114, false));
            applicableActions.Add(new ApplicableAction(7551, "Head Graze", true, false, false, false, true, 115, false));
            applicableActions.Add(new ApplicableAction(7553, "Foot Graze", true, false, false, false, true, 115, false));
            applicableActions.Add(new ApplicableAction(7554, "Leg Graze", true, false, false, false, true, 115, false));
            applicableActions.Add(new ApplicableAction(7557, "Peloton", true, true, false, false, false, 115, false));
            applicableActions.Add(new ApplicableAction(7559, "Surecast", true, true, false, false, false, 120, false));
            applicableActions.Add(new ApplicableAction(7560, "Addle", true, false, false, false, true, 116, false));
            applicableActions.Add(new ApplicableAction(7561, "Swiftcast", true, true, false, false, false, 120, false));
            applicableActions.Add(new ApplicableAction(7562, "Lucid Dreaming", true, true, false, false, false, 120, false));
            applicableActions.Add(new ApplicableAction(7568, "Esuna", true, true, true, true, false, 117, false));
            applicableActions.Add(new ApplicableAction(7571, "Rescue", true, false, true, false, false, 117, false));
            applicableActions.Add(new ApplicableAction(7857, "Hissatsu: Soten", false, false, false, false, false, 111, true));
            applicableActions.Add(new ApplicableAction(7863, "Leg Sweep", true, false, false, false, true, 114, false));
            applicableActions.Add(new ApplicableAction(8752, "Holy Spirit", false, false, false, false, true, 20, true));
            applicableActions.Add(new ApplicableAction(8754, "Requiescat", false, false, false, false, true, 20, true));
            applicableActions.Add(new ApplicableAction(8763, "Fell Cleave", false, false, false, false, true, 22, true));
            applicableActions.Add(new ApplicableAction(8764, "Tomahawk", false, false, false, false, true, 22, true));
            applicableActions.Add(new ApplicableAction(8765, "Onslaught", false, false, false, false, true, 22, true));
            applicableActions.Add(new ApplicableAction(8767, "Holmgang", false, false, false, false, true, 22, true));
            applicableActions.Add(new ApplicableAction(8775, "Unmend", false, false, false, false, true, 98, true));
            applicableActions.Add(new ApplicableAction(8776, "Bloodspiller", false, false, false, false, true, 98, true));
            applicableActions.Add(new ApplicableAction(8777, "Plunge", false, false, false, false, true, 98, true));
            applicableActions.Add(new ApplicableAction(8779, "The Blackest Night", false, true, true, false, false, 98, true));
            applicableActions.Add(new ApplicableAction(8787, "Shoulder Tackle", false, false, false, false, true, 21, true));
            applicableActions.Add(new ApplicableAction(8789, "Tornado Kick", false, false, false, false, true, 21, true));
            applicableActions.Add(new ApplicableAction(8790, "The Forbidden Chakra", false, false, false, false, true, 21, true));
            applicableActions.Add(new ApplicableAction(8799, "Piercing Talon", false, false, false, false, true, 23, true));
            applicableActions.Add(new ApplicableAction(8802, "Spineshatter Dive", false, false, false, false, true, 23, true));
            applicableActions.Add(new ApplicableAction(8805, "Geirskogul", false, false, false, false, true, 23, true));
            applicableActions.Add(new ApplicableAction(8811, "Throwing Dagger", false, false, false, false, true, 92, true));
            applicableActions.Add(new ApplicableAction(8812, "Shukuchi", false, false, false, false, false, 92, true));
            applicableActions.Add(new ApplicableAction(8814, "Assassinate", false, false, false, false, true, 92, true));
            applicableActions.Add(new ApplicableAction(8815, "Bhavacakra", false, false, false, false, true, 92, true));
            applicableActions.Add(new ApplicableAction(8827, "Enpi", false, false, false, false, true, 111, true));
            applicableActions.Add(new ApplicableAction(8830, "Tenka Goken", false, false, false, false, true, 111, true));
            applicableActions.Add(new ApplicableAction(8831, "Midare Setsugekka", false, false, false, false, true, 111, true));
            applicableActions.Add(new ApplicableAction(8832, "Hissatsu: Shinten", false, false, false, false, true, 111, true));
            applicableActions.Add(new ApplicableAction(8838, "Empyreal Arrow", false, false, false, false, true, 24, true));
            applicableActions.Add(new ApplicableAction(8839, "Repelling Shot", false, false, false, false, true, 24, true));
            applicableActions.Add(new ApplicableAction(8841, "Sidewinder", false, false, false, false, true, 24, true));
            applicableActions.Add(new ApplicableAction(8842, "Pitch Perfect", false, false, false, false, true, 24, true));
            applicableActions.Add(new ApplicableAction(8843, "The Wanderer's Minuet", false, false, false, false, true, 24, true));
            applicableActions.Add(new ApplicableAction(8844, "Army's Paeon", false, false, false, false, true, 24, true));
            applicableActions.Add(new ApplicableAction(8853, "Blank", false, false, false, false, true, 96, true));
            applicableActions.Add(new ApplicableAction(8855, "Wildfire", false, false, false, false, true, 96, true));
            applicableActions.Add(new ApplicableAction(8858, "Fire", false, false, false, false, true, 26, true));
            applicableActions.Add(new ApplicableAction(8859, "Blizzard", false, false, false, false, true, 26, true));
            applicableActions.Add(new ApplicableAction(8860, "Thunder", false, false, false, false, true, 26, true));
            applicableActions.Add(new ApplicableAction(8865, "Foul", false, false, false, false, true, 26, true));
            applicableActions.Add(new ApplicableAction(8866, "Flare", false, false, false, false, true, 26, true));
            applicableActions.Add(new ApplicableAction(8867, "Freeze", false, false, false, false, true, 26, true));
            applicableActions.Add(new ApplicableAction(8869, "Aetherial Manipulation", false, false, true, false, false, 26, true));
            applicableActions.Add(new ApplicableAction(8872, "Ruin III", false, false, false, false, true, 28, true));
            applicableActions.Add(new ApplicableAction(8873, "Bio III", false, false, false, false, true, 28, true));
            applicableActions.Add(new ApplicableAction(8877, "Fester", false, false, false, false, true, 28, true));
            applicableActions.Add(new ApplicableAction(8883, "Verstone", false, false, false, false, true, 112, true));
            applicableActions.Add(new ApplicableAction(8885, "Verfire", false, false, false, false, true, 112, true));
            applicableActions.Add(new ApplicableAction(8890, "Corps-a-corps", false, false, false, false, true, 112, true));
            applicableActions.Add(new ApplicableAction(8891, "Displacement", false, false, false, false, true, 112, true));
            applicableActions.Add(new ApplicableAction(8895, "Cure", false, true, true, true, false, 25, true));
            applicableActions.Add(new ApplicableAction(8896, "Cure II", false, true, true, true, false, 25, true));
            applicableActions.Add(new ApplicableAction(8904, "Physick", false, true, true, true, false, 29, true));
            applicableActions.Add(new ApplicableAction(8905, "Adloquium", false, true, true, true, false, 29, true));
            applicableActions.Add(new ApplicableAction(8909, "Lustrate", false, true, true, true, false, 29, true));
            applicableActions.Add(new ApplicableAction(8913, "Benefic", false, true, true, true, false, 99, true));
            applicableActions.Add(new ApplicableAction(8914, "Benefic II", false, true, true, true, false, 99, true));
            applicableActions.Add(new ApplicableAction(8916, "Essential Dignity", false, true, true, true, false, 99, true));
            applicableActions.Add(new ApplicableAction(9014, "Deathflare", false, false, false, false, true, 28, true));
            applicableActions.Add(new ApplicableAction(9461, "Jugulate", false, false, false, false, true, 0, false));
            applicableActions.Add(new ApplicableAction(10025, "Vercure", false, true, true, true, false, 112, true));
            applicableActions.Add(new ApplicableAction(10032, "Dragon Sight", false, false, true, false, false, 23, true));
            applicableActions.Add(new ApplicableAction(11384, "4-tonze Weight", false, false, false, false, false, 129, false));
            applicableActions.Add(new ApplicableAction(11385, "Water Cannon", false, false, false, false, true, 129, false));
            applicableActions.Add(new ApplicableAction(11386, "Song of Torment", false, false, false, false, true, 129, false));
            applicableActions.Add(new ApplicableAction(11389, "Flying Frenzy", false, false, false, false, true, 129, false));
            applicableActions.Add(new ApplicableAction(11392, "Acorn Bomb", false, false, false, false, true, 129, false));
            applicableActions.Add(new ApplicableAction(11395, "Blood Drain", false, false, false, false, true, 129, false));
            applicableActions.Add(new ApplicableAction(11396, "Bomb Toss", false, false, false, false, false, 129, false));
            applicableActions.Add(new ApplicableAction(11398, "Drill Cannons", false, false, false, false, true, 129, false));
            applicableActions.Add(new ApplicableAction(11400, "Sharpened Knife", false, false, false, false, true, 129, false));
            applicableActions.Add(new ApplicableAction(11401, "Loom", false, false, false, false, false, 129, false));
            applicableActions.Add(new ApplicableAction(11404, "Glower", false, false, false, false, true, 129, false));
            applicableActions.Add(new ApplicableAction(11405, "Missile", false, false, false, false, true, 129, false));
            applicableActions.Add(new ApplicableAction(11407, "Final Sting", false, false, false, false, true, 129, false));
            applicableActions.Add(new ApplicableAction(11409, "Transfusion", false, false, true, false, false, 129, false));
            applicableActions.Add(new ApplicableAction(11411, "Off-guard", false, false, false, false, true, 129, false));
            applicableActions.Add(new ApplicableAction(11412, "Sticky Tongue", false, false, false, false, true, 129, false));
            applicableActions.Add(new ApplicableAction(11413, "Tail Screw", false, false, false, false, true, 129, false));
            applicableActions.Add(new ApplicableAction(11416, "Doom", false, false, false, false, true, 129, false));
            applicableActions.Add(new ApplicableAction(11423, "Flying Sardine", false, false, false, false, true, 129, false));
            applicableActions.Add(new ApplicableAction(11425, "Fire Angon", false, false, false, false, true, 129, false));
            applicableActions.Add(new ApplicableAction(11426, "Feather Rain", false, false, false, false, false, 129, false));
            applicableActions.Add(new ApplicableAction(11427, "Eruption", false, false, false, false, false, 129, false));
            applicableActions.Add(new ApplicableAction(11429, "Shock Strike", false, false, false, false, true, 129, false));
            applicableActions.Add(new ApplicableAction(13975, "Tetragrammaton", false, true, true, true, false, 25, true));
            applicableActions.Add(new ApplicableAction(15989, "Cascade", false, false, false, false, true, 150, false));
            applicableActions.Add(new ApplicableAction(15990, "Fountain", false, false, false, false, true, 150, false));
            applicableActions.Add(new ApplicableAction(15991, "Reverse Cascade", false, false, false, false, true, 150, false));
            applicableActions.Add(new ApplicableAction(15992, "Fountainfall", false, false, false, false, true, 150, false));
            applicableActions.Add(new ApplicableAction(16005, "Saber Dance", false, false, false, false, true, 150, false));
            applicableActions.Add(new ApplicableAction(16006, "Closed Position", false, false, true, false, false, 150, false));
            applicableActions.Add(new ApplicableAction(16007, "Fan Dance", false, false, false, false, true, 150, false));
            applicableActions.Add(new ApplicableAction(16009, "Fan Dance III", false, false, false, false, true, 150, false));
            applicableActions.Add(new ApplicableAction(16137, "Keen Edge", false, false, false, false, true, 149, false));
            applicableActions.Add(new ApplicableAction(16139, "Brutal Shell", false, false, false, false, true, 149, false));
            applicableActions.Add(new ApplicableAction(16143, "Lightning Shot", false, false, false, false, true, 149, false));
            applicableActions.Add(new ApplicableAction(16144, "Danger Zone", false, false, false, false, true, 149, false));
            applicableActions.Add(new ApplicableAction(16145, "Solid Barrel", false, false, false, false, true, 149, false));
            applicableActions.Add(new ApplicableAction(16146, "Gnashing Fang", false, false, false, false, true, 149, false));
            applicableActions.Add(new ApplicableAction(16147, "Savage Claw", false, false, false, false, true, 149, false));
            applicableActions.Add(new ApplicableAction(16150, "Wicked Talon", false, false, false, false, true, 149, false));
            applicableActions.Add(new ApplicableAction(16151, "Aurora", false, true, true, true, false, 149, false));
            applicableActions.Add(new ApplicableAction(16153, "Sonic Break", false, false, false, false, true, 149, false));
            applicableActions.Add(new ApplicableAction(16154, "Rough Divide", false, false, false, false, true, 149, false));
            applicableActions.Add(new ApplicableAction(16161, "Heart of Stone", false, true, true, false, false, 149, false));
            applicableActions.Add(new ApplicableAction(16162, "Burst Strike", false, false, false, false, true, 149, false));
            applicableActions.Add(new ApplicableAction(16164, "Bloodfest", false, false, false, false, true, 149, false));
            applicableActions.Add(new ApplicableAction(16165, "Blasting Zone", false, false, false, false, true, 149, false));
            applicableActions.Add(new ApplicableAction(16230, "Physick", false, true, true, true, false, 69, false));
            applicableActions.Add(new ApplicableAction(16459, "Confiteor", false, false, false, false, true, 20, false));
            applicableActions.Add(new ApplicableAction(16460, "Atonement", false, false, false, false, true, 20, false));
            applicableActions.Add(new ApplicableAction(16461, "Intervene", false, false, false, false, true, 20, false));
            applicableActions.Add(new ApplicableAction(16464, "Nascent Flash", false, false, true, false, false, 22, false));
            applicableActions.Add(new ApplicableAction(16466, "Flood of Darkness", false, false, false, false, true, 98, false));
            applicableActions.Add(new ApplicableAction(16467, "Edge of Darkness", false, false, false, false, true, 98, false));
            applicableActions.Add(new ApplicableAction(16469, "Flood of Shadow", false, false, false, false, true, 98, false));
            applicableActions.Add(new ApplicableAction(16470, "Edge of Shadow", false, false, false, false, true, 98, false));
            applicableActions.Add(new ApplicableAction(16474, "Enlightenment", false, false, false, false, true, 21, false));
            applicableActions.Add(new ApplicableAction(16476, "Six-sided Star", false, false, false, false, true, 21, false));
            applicableActions.Add(new ApplicableAction(16477, "Coerthan Torment", false, false, false, false, true, 23, false));
            applicableActions.Add(new ApplicableAction(16478, "High Jump", false, false, false, false, true, 23, false));
            applicableActions.Add(new ApplicableAction(16480, "Stardiver", false, false, false, false, true, 23, false));
            applicableActions.Add(new ApplicableAction(16481, "Hissatsu: Senei", false, false, false, false, true, 111, false));
            applicableActions.Add(new ApplicableAction(16487, "Shoha", false, false, false, false, true, 111, false));
            applicableActions.Add(new ApplicableAction(16494, "Shadowbite", false, false, false, false, true, 24, false));
            applicableActions.Add(new ApplicableAction(16495, "Burst Shot", false, false, false, false, true, 24, false));
            applicableActions.Add(new ApplicableAction(16496, "Apex Arrow", false, false, false, false, true, 24, false));
            applicableActions.Add(new ApplicableAction(16497, "Auto Crossbow", false, false, false, false, true, 96, false));
            applicableActions.Add(new ApplicableAction(16498, "Drill", false, false, false, false, true, 96, false));
            applicableActions.Add(new ApplicableAction(16499, "Bioblaster", false, false, false, false, true, 96, false));
            applicableActions.Add(new ApplicableAction(16500, "Air Anchor", false, false, false, false, true, 96, false));
            applicableActions.Add(new ApplicableAction(16505, "Despair", false, false, false, false, true, 26, false));
            applicableActions.Add(new ApplicableAction(16507, "Xenoglossy", false, false, false, false, true, 26, false));
            applicableActions.Add(new ApplicableAction(16508, "Energy Drain", false, false, false, false, true, 69, false));
            applicableActions.Add(new ApplicableAction(16510, "Energy Siphon", false, false, false, false, true, 28, false));
            applicableActions.Add(new ApplicableAction(16511, "Outburst", false, false, false, false, true, 28, false));
            applicableActions.Add(new ApplicableAction(16524, "Verthunder II", false, false, false, false, true, 112, false));
            applicableActions.Add(new ApplicableAction(16525, "Veraero II", false, false, false, false, true, 112, false));
            applicableActions.Add(new ApplicableAction(16526, "Impact", false, false, false, false, true, 112, false));
            applicableActions.Add(new ApplicableAction(16527, "Engagement", false, false, false, false, true, 112, false));
            applicableActions.Add(new ApplicableAction(16529, "Reprise", false, false, false, false, true, 112, false));
            applicableActions.Add(new ApplicableAction(16531, "Afflatus Solace", false, true, true, true, false, 25, false));
            applicableActions.Add(new ApplicableAction(16532, "Dia", false, false, false, false, true, 25, false));
            applicableActions.Add(new ApplicableAction(16533, "Glare", false, false, false, false, true, 25, false));
            applicableActions.Add(new ApplicableAction(16535, "Afflatus Misery", false, false, false, false, true, 25, false));
            applicableActions.Add(new ApplicableAction(16540, "Biolysis", false, false, false, false, true, 29, false));
            applicableActions.Add(new ApplicableAction(16541, "Broil III", false, false, false, false, true, 29, false));
            applicableActions.Add(new ApplicableAction(16554, "Combust III", false, false, false, false, true, 99, false));
            applicableActions.Add(new ApplicableAction(16555, "Malefic IV", false, false, false, false, true, 99, false));
            applicableActions.Add(new ApplicableAction(16556, "Celestial Intersection", false, true, true, false, false, 99, false));
            applicableActions.Add(new ApplicableAction(16560, "Repose", true, false, false, false, true, 117, false));
            applicableActions.Add(new ApplicableAction(17669, "Low Blow", false, false, false, false, true, 121, true));
            applicableActions.Add(new ApplicableAction(17671, "Full Swing", true, false, false, false, true, 121, true));
            applicableActions.Add(new ApplicableAction(17672, "Rampart", true, true, false, false, false, 121, true));
            applicableActions.Add(new ApplicableAction(17674, "Backstep", true, true, false, false, false, 122, true));
            applicableActions.Add(new ApplicableAction(17676, "Bloodbath", true, true, false, false, false, 122, true));
            applicableActions.Add(new ApplicableAction(17677, "Fetter Ward", true, true, false, false, false, 122, true));
            applicableActions.Add(new ApplicableAction(17678, "Leg Graze", true, false, false, false, true, 123, true));
            applicableActions.Add(new ApplicableAction(17679, "Foot Graze", true, false, false, false, true, 123, true));
            applicableActions.Add(new ApplicableAction(17680, "Head Graze", false, false, false, false, true, 123, true));
            applicableActions.Add(new ApplicableAction(17681, "Arm's Length", true, true, false, false, false, 122, true));
            applicableActions.Add(new ApplicableAction(17682, "Peloton", true, true, false, false, false, 123, true));
            applicableActions.Add(new ApplicableAction(17683, "Drain", true, false, false, false, true, 89, true));
            applicableActions.Add(new ApplicableAction(17684, "Phantom Dart", true, false, false, false, true, 89, true));
            applicableActions.Add(new ApplicableAction(17685, "Swiftcast", true, true, false, false, false, 89, true));
            applicableActions.Add(new ApplicableAction(17686, "Addle", true, false, false, false, true, 89, true));
            applicableActions.Add(new ApplicableAction(17687, "Manaward", true, true, false, false, false, 89, true));
            applicableActions.Add(new ApplicableAction(17688, "Rescue", true, false, true, false, false, 125, true));
            applicableActions.Add(new ApplicableAction(17689, "Attunement", true, true, false, false, false, 125, true));
            applicableActions.Add(new ApplicableAction(17690, "Repose", true, false, false, false, true, 125, true));
            applicableActions.Add(new ApplicableAction(17692, "Confiteor", false, false, false, false, true, 20, true));
            applicableActions.Add(new ApplicableAction(17694, "Intervene", false, false, false, false, true, 20, true));
            applicableActions.Add(new ApplicableAction(17701, "Edge of Shadow", false, false, false, false, true, 98, true));
            applicableActions.Add(new ApplicableAction(17709, "Burst Strike", false, false, false, false, true, 149, true));
            applicableActions.Add(new ApplicableAction(17716, "Rough Divide", false, false, false, false, true, 149, true));
            applicableActions.Add(new ApplicableAction(17717, "Lightning Shot", false, false, false, false, true, 149, true));
            applicableActions.Add(new ApplicableAction(17718, "Tether", false, false, false, false, true, 112, true));
            applicableActions.Add(new ApplicableAction(17719, "Six-sided Star", false, false, false, false, true, 21, true));
            applicableActions.Add(new ApplicableAction(17720, "Enlightenment", false, false, false, false, true, 21, true));
            applicableActions.Add(new ApplicableAction(17727, "Scatter", false, false, false, false, true, 112, true));
            applicableActions.Add(new ApplicableAction(17728, "High Jump", false, false, false, false, true, 23, true));
            applicableActions.Add(new ApplicableAction(17729, "Stardiver", false, false, false, false, true, 23, true));
            applicableActions.Add(new ApplicableAction(17730, "Mirage Dive", false, false, false, false, true, 23, true));
            applicableActions.Add(new ApplicableAction(17731, "HellfMedium", false, false, false, false, true, 92, true));
            applicableActions.Add(new ApplicableAction(17745, "Burst Shot", false, false, false, false, true, 24, true));
            applicableActions.Add(new ApplicableAction(17747, "Apex Arrow", false, false, false, false, true, 24, true));
            applicableActions.Add(new ApplicableAction(17749, "Drill", false, false, false, false, true, 96, true));
            applicableActions.Add(new ApplicableAction(17750, "Air Anchor", false, false, false, false, true, 96, true));
            applicableActions.Add(new ApplicableAction(17752, "Bioblaster", false, false, false, false, true, 96, true));
            applicableActions.Add(new ApplicableAction(17753, "Ricochet", false, false, false, false, true, 96, true));
            applicableActions.Add(new ApplicableAction(17760, "Saber Dance", false, false, false, false, true, 150, true));
            applicableActions.Add(new ApplicableAction(17761, "Fan Dance", false, false, false, false, true, 150, true));
            applicableActions.Add(new ApplicableAction(17762, "Fan Dance III", false, false, false, false, true, 150, true));
            applicableActions.Add(new ApplicableAction(17765, "Closed Position", false, false, true, false, false, 150, true));
            applicableActions.Add(new ApplicableAction(17774, "Xenoglossy", false, false, false, false, true, 26, true));
            applicableActions.Add(new ApplicableAction(17775, "Night Wing", false, false, false, false, true, 26, true));
            applicableActions.Add(new ApplicableAction(17777, "Energy Drain", false, false, false, false, true, 28, true));
            applicableActions.Add(new ApplicableAction(17780, "Wither", false, false, false, false, true, 28, true));
            applicableActions.Add(new ApplicableAction(17786, "Engagement", false, false, false, false, true, 112, true));
            applicableActions.Add(new ApplicableAction(17788, "Enchanted Reprise", false, false, false, false, true, 112, true));
            applicableActions.Add(new ApplicableAction(17789, "Glare", false, false, false, false, true, 25, true));
            applicableActions.Add(new ApplicableAction(17790, "Dia", false, false, false, false, true, 25, true));
            applicableActions.Add(new ApplicableAction(17791, "Afflatus Solace", false, true, true, true, false, 25, true));
            applicableActions.Add(new ApplicableAction(17793, "Afflatus Misery", false, false, false, false, true, 25, true));
            applicableActions.Add(new ApplicableAction(17795, "Broil III", false, false, false, false, true, 29, true));
            applicableActions.Add(new ApplicableAction(17796, "Biolysis", false, false, false, false, true, 29, true));
            applicableActions.Add(new ApplicableAction(17805, "Malefic IV", false, false, false, false, true, 99, true));
            applicableActions.Add(new ApplicableAction(17806, "Combust III", false, false, false, false, true, 99, true));
            applicableActions.Add(new ApplicableAction(17807, "Gravity", false, false, false, false, true, 99, true));
            applicableActions.Add(new ApplicableAction(17832, "Protect", true, true, true, false, false, 125, true));
            applicableActions.Add(new ApplicableAction(17864, "Bio", false, false, false, false, true, 29, false));
            applicableActions.Add(new ApplicableAction(17865, "Bio II", false, false, false, false, true, 29, false));
            applicableActions.Add(new ApplicableAction(17866, "Reprisal", true, true, false, false, false, 121, true));
            applicableActions.Add(new ApplicableAction(17869, "Ruin", false, false, false, false, true, 29, false));
            applicableActions.Add(new ApplicableAction(17870, "Ruin II", false, false, false, false, true, 29, false));
            applicableActions.Add(new ApplicableAction(17889, "Nascent Flash", false, false, true, false, false, 22, true));
            applicableActions.Add(new ApplicableAction(17891, "Aurora", false, true, true, true, false, 149, true));
            applicableActions.Add(new ApplicableAction(18295, "Alpine Draft", false, false, false, false, true, 129, false));
            applicableActions.Add(new ApplicableAction(18298, "Electrogenesis", false, false, false, false, true, 129, false));
            applicableActions.Add(new ApplicableAction(18300, "Abyssal Transfixion", false, false, false, false, true, 129, false));
            applicableActions.Add(new ApplicableAction(18303, "Pom Cure", false, true, true, true, false, 129, false));
            applicableActions.Add(new ApplicableAction(18305, "Magic Hammer", false, false, false, false, true, 129, false));
            applicableActions.Add(new ApplicableAction(18306, "Avail", false, false, true, false, false, 129, false));
            applicableActions.Add(new ApplicableAction(18308, "Sonic Boom", false, false, false, false, true, 129, false));
            applicableActions.Add(new ApplicableAction(18310, "White Knight's Tour", false, false, false, false, true, 129, false));
            applicableActions.Add(new ApplicableAction(18311, "Black Knight's Tour", false, false, false, false, true, 129, false));
            applicableActions.Add(new ApplicableAction(18314, "Perpetual Ray", false, false, false, false, true, 129, false));
            applicableActions.Add(new ApplicableAction(18315, "Cactguard", false, false, true, false, false, 129, false));
            applicableActions.Add(new ApplicableAction(18316, "Revenge Blast", false, false, false, false, true, 129, false));
            applicableActions.Add(new ApplicableAction(18317, "Angel Whisper", false, false, true, true, false, 129, false));
            applicableActions.Add(new ApplicableAction(18319, "Reflux", false, false, false, false, true, 129, false));
            applicableActions.Add(new ApplicableAction(18320, "Devour", false, false, false, false, true, 129, false));
            applicableActions.Add(new ApplicableAction(18321, "Condensed Libra", false, false, false, false, true, 129, false));
            applicableActions.Add(new ApplicableAction(18322, "Aetherial Mimicry", false, false, true, true, false, 129, false));
            applicableActions.Add(new ApplicableAction(18325, "J Kick", false, false, false, false, true, 129, false));
            applicableActions.Add(new ApplicableAction(18902, "Shield Lob", false, false, false, false, true, 20, true));
            applicableActions.Add(new ApplicableAction(18908, "Flood of Shadow", false, false, false, false, true, 98, true));
            applicableActions.Add(new ApplicableAction(18928, "Recuperate", true, true, false, false, false, 123, true));
            applicableActions.Add(new ApplicableAction(18929, "Shoha", false, false, false, false, true, 111, true));
            applicableActions.Add(new ApplicableAction(18930, "Quick Nock", false, false, false, false, true, 24, true));
            applicableActions.Add(new ApplicableAction(18931, "Shadowbite", false, false, false, false, true, 24, true));
            applicableActions.Add(new ApplicableAction(18932, "Spread Shot", false, false, false, false, true, 96, true));
            applicableActions.Add(new ApplicableAction(18933, "Gauss Round", false, false, false, false, true, 96, true));
            applicableActions.Add(new ApplicableAction(18935, "Thunder II", false, false, false, false, true, 26, true));
            applicableActions.Add(new ApplicableAction(18937, "Outburst", false, false, false, false, true, 28, true));
            applicableActions.Add(new ApplicableAction(18938, "Painflare", false, false, false, false, true, 28, true));
            applicableActions.Add(new ApplicableAction(18939, "Bane", false, false, false, false, true, 28, true));
            applicableActions.Add(new ApplicableAction(18944, "Enchanted Moulinet", false, false, false, false, true, 112, true));
            applicableActions.Add(new ApplicableAction(18949, "Excogitation", false, true, true, false, false, 29, true));
            applicableActions.Add(new ApplicableAction(18952, "Weapon Throw", true, false, false, false, true, 121, true));
            applicableActions.Add(new ApplicableAction(18953, "Tri-disaster", false, false, false, false, true, 28, true));
            applicableActions.Add(new ApplicableAction(18954, "Feint", true, false, false, false, true, 122, true));
            applicableActions.Add(new ApplicableAction(18955, "Concentrate", true, true, false, false, false, 123, true));
            applicableActions.Add(new ApplicableAction(18956, "Aetheric Burst", true, true, false, false, false, 89, true));
            applicableActions.Add(new ApplicableAction(18957, "Largesse", true, true, false, false, false, 125, true));
            applicableActions.Add(new ApplicableAction(18990, "Retaliation", true, true, false, false, false, 121, true));
            applicableActions.Add(new ApplicableAction(18992, "Smite", false, false, false, false, true, 122, true));
            applicableActions.Add(new ApplicableAction(19071, "Nature's Minne", false, true, true, false, false, 24, true));
            applicableActions.Add(new ApplicableAction(19085, "Intervention", false, false, true, false, false, 20, true));
            applicableActions.Add(new ApplicableAction(4401, "The Balance", false, true, true, false, false, 99, false));
            applicableActions.Add(new ApplicableAction(4402, "The Arrow", false, true, true, false, false, 99, false));
            applicableActions.Add(new ApplicableAction(4403, "The Spear", false, true, true, false, false, 99, false));
            applicableActions.Add(new ApplicableAction(4404, "The Bole", false, true, true, false, false, 99, false));
            applicableActions.Add(new ApplicableAction(4405, "The Ewer", false, true, true, false, false, 99, false));
            applicableActions.Add(new ApplicableAction(4406, "The Spire", false, true, true, false, false, 99, false));
            applicableActions.Add(new ApplicableAction(7444, "Lord of Crowns", false, true, true, false, false, 99, false));
            applicableActions.Add(new ApplicableAction(7445, "Lady of Crowns", false, true, true, false, false, 99, false));
            applicableActions.Add(new ApplicableAction(7438, "Fey Union", false, true, true, false, false, 29, false));
            SortActions();
        }
    }
}
