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
using MOAction.Target;

namespace MOAction
{
    internal class MOActionPlugin : IDalamudPlugin
    {
        public string Name => "Mouseover Action Plugin";

        public MOActionConfiguration Configuration;

        private DalamudPluginInterface pluginInterface;
        private MOAction moAction;

        private List<ApplicableAction> applicableActions;
        private List<(uint key, List<(uint key2, TargetType value2)> value)> Stacks;
        private List<TargetType> TargetTypes;
        private String[] tTypeNames = { "Regular Target", "Focus Target", "UI Mouseover", "Field Mouseover" };
        private List<GuiSettings> StackFlags;
        List<ApplicableAction> actions;
        String[] actionNames;

        private string[] soloJobNames = { "AST", "WHM", "SCH", "SMN", "BLM", "RDM", "BLU", "BRD", "MCH", "DNC", "DRK", "GNB", "WAR", "PLD", "DRG", "MNK", "SAM", "NIN" };
        private string[] roleActionNames = { "BLM SMN RDM", "BRD MCH DNC",  "MNK DRG SAM NIN", "PLD WAR DRK GNB", "AST SCH WHM"};

        private bool[] flagsSelected;
        private bool isImguiMoSetupOpen = false;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            Stacks = new List<(uint key, List<(uint key2, TargetType value2)> value)>();

            StackFlags = new List<GuiSettings>();

            this.pluginInterface = pluginInterface;
            actions = new List<ApplicableAction>();
            actionNames = new string[0];

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

            this.pluginInterface.UiBuilder.OnOpenConfigUi += (sender, args) => isImguiMoSetupOpen = true;
            this.pluginInterface.UiBuilder.OnBuildUi += UiBuilder_OnBuildUi;
        }

        private void InitializeData()
        {
            var rawActions = pluginInterface.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.Action>().GetRows().Where(row => row.IsPlayerAction);
            foreach (Lumina.Excel.GeneratedSheets.Action a in rawActions)
            {
                applicableActions.Add(new ApplicableAction((uint)a.RowId, a.Name, a.IsRoleAction, a.CanTargetSelf, a.CanTargetParty, a.CanTargetFriendly, a.CanTargetHostile, a.ClassJobCategory, a.IsPvP));
            }
            SortActions();

            flagsSelected = new bool[applicableActions.Count()];
            for (var i = 0; i < flagsSelected.Length; i++)
                flagsSelected[i] = false;

            Configuration = pluginInterface.GetPluginConfig() as MOActionConfiguration ?? new MOActionConfiguration();

            moAction = new MOAction(pluginInterface.TargetModuleScanner, pluginInterface.ClientState, Configuration, ref pluginInterface, rawActions);

            TargetTypes = new List<TargetType>
            {
                new ActorTarget(moAction.GetRegTargPtr),
                new EntityTarget(moAction.GetFocusPtr),
                new EntityTarget(moAction.GetGuiMoPtr),
                new ActorTarget(moAction.GetFieldMoPtr)
            };

            moAction.Enable();
            SetNewConfig();
        }

        private void UiBuilder_OnBuildUi()
        {
            if (!isImguiMoSetupOpen)
                return;

            ImGui.SetNextWindowSize(new Vector2(740, 490));

            ImGui.Begin("Action stack setup", ref isImguiMoSetupOpen,
                ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar);

            ImGui.Text("This window allows you to set up your action stacks.");
            ImGui.Separator();

            ImGui.BeginChild("scrolling", new Vector2(0, 400), true, ImGuiWindowFlags.HorizontalScrollbar);

            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 3));
            for (int i = 0; i < Stacks.Count; i++)
            {
                
                var elem = Stacks[i];
                var currentSettings = StackFlags.ElementAt(i);

                var jobname = "";
                if (currentSettings.jobs >= 0) jobname = " - " + soloJobNames[currentSettings.jobs];
                if (ImGui.CollapsingHeader(GetName(elem.key) + jobname +"###"+i))
                {
                    
                    ImGui.PushItemWidth(70);
                    
                    ImGui.Combo("Job##" +i, ref StackFlags.ElementAt(i).jobs, soloJobNames, soloJobNames.Length);
                    ImGui.PopItemWidth();
                    
                    ImGui.Indent();
                    if (currentSettings.jobs != -1)
                    {
                        actions = applicableActions.
                                    Where(item => ClassJobCategoryToName(item.ClassJobCategory).
                                                    Contains(soloJobNames[currentSettings.jobs]) && 
                                                    !item.IsPvP &&
                                                    (item.CanTargetFriendly || item.CanTargetHostile)).
                                    ToList();
                        actionNames = actions.Select(s => s.AbilityName).ToArray();
                        
                        if (currentSettings.lastJob != currentSettings.jobs)
                        {
                            for (int j = 0; j < currentSettings.stackAbilities.Count; j++)
                                StackFlags.ElementAt(i).stackAbilities[j] = -1;
                            StackFlags.ElementAt(i).baseAbility = -1;
                            StackFlags.ElementAt(i).lastJob = currentSettings.jobs;
                        }
                    }
                    else
                    {
                        actions = new List<ApplicableAction>();
                        actionNames = new string[0];
                        for (int j = 0; j < currentSettings.stackAbilities.Count; j++)
                            StackFlags.ElementAt(i).stackAbilities[j] = -1;
                        StackFlags.ElementAt(i).baseAbility = -1;
                    }
                    StackFlags.ElementAt(i).lastJob = StackFlags.ElementAt(i).jobs;
                    ImGui.PushItemWidth(200);
                    ImGui.Combo("Base Ability##"+i, ref StackFlags.ElementAt(i).baseAbility, actionNames, actions.Count);
                    var tmptargs = StackFlags[i].stackTargets.ToArray();
                    var tmpabilities = StackFlags[i].stackAbilities.ToArray();
                    ImGui.Indent();
                    for (int j = 0; j < Stacks[i].value.Count; j++) {
                        ImGui.Text("Ability #" + (j + 1));
                        ImGui.Combo("Target##"+i.ToString()+j.ToString(), ref tmptargs[j], tTypeNames, TargetTypes.Count);
                        ImGui.SameLine(0, 35);
                        ImGui.Combo("Ability##" + i.ToString() + j.ToString(), ref tmpabilities[j], actionNames, actions.Count);

                        if (tmptargs.Length != 1) ImGui.SameLine(0, 10);
                        if (tmptargs.Length != 1 && ImGui.Button("Delete Ability##" + i.ToString() + j.ToString()))
                        {
                            Stacks[i].value.RemoveAt(j);
                            StackFlags[i].stackAbilities.RemoveAt(j);
                            StackFlags[i].stackTargets.RemoveAt(j);
                            tmptargs = StackFlags[i].stackTargets.ToArray();
                            tmpabilities = StackFlags[i].stackAbilities.ToArray();
                        }
                    }
                    ImGui.Unindent();
                    ImGui.PopItemWidth();
                    StackFlags.ElementAt(i).stackTargets = tmptargs.ToList();
                    StackFlags.ElementAt(i).stackAbilities = tmpabilities.ToList();
                    (uint key, List<(uint key2, TargetType value2)> value) tmpelem = (0, null);
                    if (StackFlags.ElementAt(i).baseAbility != -1)
                    {
                        elem.key = actions[StackFlags.ElementAt(i).baseAbility].ID;
                        tmpelem.key = actions[StackFlags.ElementAt(i).baseAbility].ID;
                    }

                    if (actions.Count > 0)
                    {
                        List<(uint key2, TargetType value2)> val2 = new List<(uint key2, TargetType value2)>(1);
                        for (var j = 0; j < tmptargs.Length; j++)
                        {
                            if (StackFlags.ElementAt(i).stackAbilities[j] > -1 && tmptargs[j] > -1)
                            {
                                val2.Add((actions[StackFlags.ElementAt(i).stackAbilities[j]].ID, TargetTypes[tmptargs[j]]));
                            }
                            else
                            {
                                val2.Add(((uint)StackFlags.ElementAt(i).stackAbilities[j], null));
                            }
                        }
                        tmpelem.value = val2;
                        elem.value = val2;
                    }
                    if (ImGui.Button("Add action to bottom of stack##" + i))
                    {
                        if (StackFlags.ElementAt(i).stackAbilities.Last() != -1)
                        {
                            elem.value.Add((0, null));
                            StackFlags.ElementAt(i).stackAbilities.Add(-1);
                            StackFlags.ElementAt(i).stackTargets.Add(-1);
                        }
                    }
                    //StackFlags.RemoveAt(i);
                    //StackFlags.Insert(i, tmp);
                    Stacks.RemoveAt(i);
                    Stacks.Insert(i, elem);
                    ImGui.Unindent();
                }
            }
            
            if (ImGui.Button("Add stack"))
            {
                if (Stacks.Count == 0 || Stacks.Last().key != 0)
                {
                    List<(uint, TargetType)> newStack = new List<(uint, TargetType)>
                    {
                        (0, null)
                    };
                    Stacks.Add((0, newStack));
                    List<int> tmp1 = new List<int>(1);
                    List<int> tmp2 = new List<int>(1);
                    tmp1.Add(-1);
                    tmp2.Add(-1);
                    StackFlags.Add(new GuiSettings(-1, -1, -1, tmp1, tmp2));
                }
            }

            ImGui.PopStyleVar();

            ImGui.EndChild();

            ImGui.Separator();

            if (ImGui.Button("Save and Close"))
            {
                /*
                UpdateList(flagsSelected);
                UpdateConfig();
                pluginInterface.SavePluginConfig(Configuration);
                
                SetNewConfig();
                */
                isImguiMoSetupOpen = false;
                foreach((uint key, List<(uint key2, TargetType val2)> val) stack in this.Stacks)
                moAction.Stacks[stack.key] = stack.val;
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

        private void SetNewConfig()
        {
            moAction.IsGuiMOEnabled = Configuration.IsGuiMO;
            moAction.IsFieldMOEnabled = Configuration.IsFieldMO;
            foreach (ulong l in Configuration.ActiveIDs)
            {
                //moAction.EnableAction(l);
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

        private String GetName(uint actionID)
        {
            if (actionID == 0) return "Unset Action";
            return applicableActions.Single(elem => elem.ID == actionID).AbilityName;
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
                case 59: return "PLD WAR DRK GNB";
                case 61: return "AST SCH WHM";
                case 63: return "BLM SMN RDM";
                case 64: return "AST SCH WHM";
                case 66: return "BRD MCH DNC";
                case 68: return "SMN SCH";
                case 69: return "SMN";
                case 73: return "AST SCH WHM";
                case 89: return "BLM SMN RDM";
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
                case 113: return "PLD WAR DRK GNB";
                case 114: return "MNK DRG SAM NIN";
                case 115: return "BRD MCH DNC";
                case 116: return "BLM SMN RDM";
                case 117: return "AST SCH WHM";
                case 121: return "PLD WAR DRK GNB";
                case 122: return "MNK DRG SAM NIN";
                case 123: return "BRD MCH DNC";
                case 125: return "AST SCH WHM";
                case 129: return "BLU";
                case 133: return "AST SCH WHM";
                case 134: return "PLD WAR DRK GNB";
                case 139: return "BRD MCH DNC";
                case 147: return "BLM SMN RDM";
                case 148: return "MNK DRG SAM NIN";
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
