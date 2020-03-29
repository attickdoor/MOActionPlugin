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
using Lumina;
using System.Threading.Tasks;
using System.Threading;

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
        private string[] roleActionNames = { "Caster", "Ranged",  "Melee", "Tank", "Healer"};

        private bool[] flagsSelected;
        private bool isImguiMoSetupOpen = false;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;

            this.pluginInterface.CommandManager.AddHandler("/pmoaction", new CommandInfo(OnCommandDebugMouseover)
            {
                HelpMessage = "Open a window to edit mouseover action settings.",
                ShowInHelp = true
            });

            this.applicableActions = new List<ApplicableAction>();
            Task.Run(() =>
            {
                while (!this.pluginInterface.Data.IsDataReady) { Thread.Sleep(0); }
                InitializeData();
            });
            
            //PopulateActions();

            this.pluginInterface.UiBuilder.OnOpenConfigUi += (sender, args) => isImguiMoSetupOpen = true;
            this.pluginInterface.UiBuilder.OnBuildUi += UiBuilder_OnBuildUi;
        }

        private void InitializeData()
        {
            var actionList = pluginInterface.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.Action>().GetRows().Where(row => row.IsPlayerAction);
            foreach (Lumina.Excel.GeneratedSheets.Action a in actionList)
            {
                applicableActions.Add(new ApplicableAction((ulong)a.RowId, a.Name, a.IsRoleAction, a.CanTargetSelf, a.CanTargetParty, a.CanTargetFriendly, a.CanTargetHostile, a.ClassJobCategory, a.IsPvP));
            }
            SortActions();

            flagsSelected = new bool[applicableActions.Count()];
            for (var i = 0; i < flagsSelected.Length; i++)
                flagsSelected[i] = false;

            Configuration = pluginInterface.GetPluginConfig() as MOActionConfiguration ?? new MOActionConfiguration();

            moAction = new MOAction(pluginInterface.TargetModuleScanner, pluginInterface.ClientState, Configuration);

            moAction.Enable();
            SetNewConfig();
        }

        private void UiBuilder_OnBuildUi()
        {
            if (!isImguiMoSetupOpen)
                return;

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
                UpdateList(flagsSelected);
                UpdateConfig();
                pluginInterface.SavePluginConfig(Configuration);
                
                SetNewConfig();
                isImguiMoSetupOpen = false;
            }

            ImGui.Spacing();
            ImGui.End();
        }

        private void UpdateConfig()
        {
            Configuration.IsFieldMO = moAction.IsFieldMOEnabled;
            Configuration.IsGuiMO = moAction.IsGuiMOEnabled;
            Configuration.ActiveIDs = moAction.enabledActions;
        }

        private void UpdateList(bool[] flags)
        {
            for (int i = 0; i < applicableActions.Count(); i++)
            {
                if (flags[i] == true)
                {
                    moAction.EnableAction(applicableActions.ElementAt(i).ID);
                }
                else
                {
                    moAction.RemoveAction(applicableActions.ElementAt(i).ID);
                }
            }
        }

        private void SetNewConfig()
        {
            moAction.IsGuiMOEnabled = Configuration.IsGuiMO;
            moAction.IsFieldMOEnabled = Configuration.IsFieldMO;
            foreach (ulong l in Configuration.ActiveIDs)
            {
                moAction.EnableAction(l);
                // I am not a smart programmer.
                // This will be much better once I figure out how to work with Lumina.
                for (int i = 0; i < flagsSelected.Length; i++)
                {
                    if (applicableActions.ElementAt(i).ID == l)
                        flagsSelected[i] = true;
                }
            }
        }

        public void Dispose()
        {
            moAction.Dispose();

            pluginInterface.CommandManager.RemoveHandler("/pmoaction");

            pluginInterface.Dispose();
        }

        private void OnCommandDebugMouseover(string command, string arguments)
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
                case 38: return "PLD";
                case 41: return "MNK";
                case 44: return "WAR";
                case 47: return "DRG";
                case 50: return "BRD";
                case 53: return "WHM";
                case 55: return "BLM";
                case 59: return "Tank";
                case 61: return "Healer";
                case 63: return "Caster";
                case 64: return "Healer";
                case 66: return "Ranged";
                case 68: return "ACN";
                case 69: return "SMN";
                case 73: return "Healer";
                case 89: return "Caster";
                case 91: return "ROG";
                case 92: return "NIN";
                case 93: return "NIN";
                case 94: return "NIN";
                case 95: return "NIN";
                case 96: return "MCH";
                case 98: return "DRK";
                case 99: return "AST";
                case 103: return "NIN";
                case 106: return "BRD";
                case 111: return "SAM";
                case 112: return "RDM";
                case 113: return "Tank";
                case 114: return "Melee";
                case 115: return "Ranged";
                case 116: return "Caster";
                case 117: return "Healer";
                case 121: return "Tank";
                case 122: return "Melee";
                case 123: return "Ranged";
                case 125: return "Healer";
                case 129: return "BLU";
                case 133: return "Healer";
                case 134: return "Tank";
                case 139: return "Ranged";
                case 147: return "Caster";
                case 148: return "Melee";
                case 149: return "GNB";
                case 150: return "DNC";
                case 160: return "SCH";
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
    }
}
