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
using MOAction.Configuration;
using Dalamud.Plugin;

namespace MOAction
{
    internal class MOActionPlugin : IDalamudPlugin
    {
        public string Name => "Mouseover Action Plugin";

        public MOActionConfiguration Configuration;

        private DalamudPluginInterface pluginInterface;
        private MOAction moAction;

        private List<ApplicableAction> applicableActions;
        private List<(uint key, List<StackEntry> value)> Stacks;
        private List<TargetType> TargetTypes;
        private readonly string[] tTypeNames = { "UI Mouseover", "Field Mouseover", "Regular Target", "Focus Target" };
        private List<GuiSettings> SortedStackFlags;
        private List<GuiSettings> UnsortedStackFlags;
        List<ApplicableAction> actions;
        List<ApplicableAction> oldActions;
        String[] actionNames;
        private bool OldConfig;
        private bool[] flagsSelected;
        private bool UIMOEnabled;
        private bool FieldMOEnabled;

        private readonly string[] soloJobNames = { "AST", "WHM", "SCH", "SMN", "BLM", "RDM", "BLU", "BRD", "MCH", "DNC", "DRK", "GNB", "WAR", "PLD", "DRG", "MNK", "SAM", "NIN" };
        private readonly string[] roleActionNames = { "BLM SMN RDM BLU", "BRD MCH DNC",  "MNK DRG NIN SAM", "PLD WAR DRK GNB", "WHM SCH AST"};

        private bool isImguiMoSetupOpen = false;

        private readonly int CURRENT_CONFIG_VERSION = 3;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            Stacks = new List<(uint key, List<StackEntry> value)>();

            SortedStackFlags = new List<GuiSettings>();
            UnsortedStackFlags = new List<GuiSettings>();

            this.pluginInterface = pluginInterface;
            actions = new List<ApplicableAction>();
            actionNames = new string[0];

            this.pluginInterface.CommandManager.AddHandler("/pmoaction", new CommandInfo(OnCommandDebugMouseover)
            {
                HelpMessage = "Open a window to edit mouseover action settings.",
                ShowInHelp = true
            });

            this.applicableActions = new List<ApplicableAction>();
            oldActions = new List<ApplicableAction>();
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
                oldActions.Add(new ApplicableAction((uint)a.RowId, a.Name, a.IsRoleAction, a.CanTargetSelf, a.CanTargetParty, a.CanTargetFriendly, a.CanTargetHostile, a.ClassJobCategory, a.IsPvP));
            }
            SortActions();
            oldActions.Remove(new ApplicableAction(173));
            var config = pluginInterface.GetPluginConfig() as MOActionConfiguration ?? new MOActionConfiguration();
            
            if (config.Version < CURRENT_CONFIG_VERSION)
            {
                config.SetOldFlags(new bool[oldActions.Count]);

                if (config.StackFlags == null)
                    config.SetStackFlags(new List<GuiSettings>());

                config.Version = CURRENT_CONFIG_VERSION;

                for (int i = 0; i < config.StackFlags.Count; i++)
                {
                    config.StackFlags[i].refjob = config.StackFlags[i].jobs;
                }
                for (int i = 0; i < config.OldFlags.Length; i++)
                    config.OldFlags[i] = false;

            }

            Configuration = config as MOActionConfiguration;

            OldConfig = Configuration.OldConfigActive;
            UIMOEnabled = Configuration.oldMO;
            FieldMOEnabled = Configuration.oldField;
            SortedStackFlags = Configuration.StackFlags;
            moAction = new MOAction(pluginInterface.TargetModuleScanner, pluginInterface.ClientState, Configuration, ref pluginInterface, rawActions);

            TargetTypes = new List<TargetType>();
            TargetTypes.Add(new EntityTarget(moAction.GetGuiMoPtr));
            TargetTypes.Add(new ActorTarget(moAction.GetFieldMoPtr));
            TargetTypes.Add(new ActorTarget(moAction.GetRegTargPtr));
            TargetTypes.Add(new EntityTarget(moAction.GetFocusPtr));

            flagsSelected = Configuration.OldFlags;

            moAction.Enable();
            if (OldConfig) SetOldConfig();
            else
            {
                SetNewConfig();
                MergeUnsorted();
            }
        }

        private void UiBuilder_OnBuildUi()
        {
            if (!isImguiMoSetupOpen)
            {
                if (UnsortedStackFlags.Count != 0)
                {
                    StackUpdateAndSave();
                }
                return;
            }
            if (Configuration.OldConfigActive)
                DrawOldConfig();
            else
                DrawNewConfig();           
        }

        private void DrawOldConfig()
        {
            ImGui.SetNextWindowSize(new Vector2(740, 490));

            ImGui.Begin("MouseOver action setup", ref isImguiMoSetupOpen,
                ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar);
            ImGui.Text("This window allows you to enable and disable actions which will affect your mouse over targets.");
            ImGui.Separator();

            ImGui.BeginChild("scrolling", new Vector2(0, 400), true, ImGuiWindowFlags.HorizontalScrollbar);

            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 3));

            ImGui.Checkbox("Enable mouseover on UI elements", ref UIMOEnabled);
            ImGui.Checkbox("Enable mouseover on field entities", ref FieldMOEnabled);

            string lastClassJob = "";

            // Support actions first
            if (ImGui.CollapsingHeader("Support Actions"))
            {
                ImGui.Indent();
                for (var i = 0; i < oldActions.Count(); i++)
                {
                    var action = oldActions[i];

                    if (action.CanTargetParty && !action.IsPvP && !action.IsRoleAction)
                    {
                        if (!lastClassJob.Equals(ClassJobCategoryToName_(action.ClassJobCategory)))
                        {
                            lastClassJob = ClassJobCategoryToName_(action.ClassJobCategory);
                            if (ImGui.CollapsingHeader(lastClassJob + "##Support"))
                            {
                                for (int j = i; j < oldActions.Count(); j++)
                                {
                                    action = oldActions[j];
                                    if (action.IsRoleAction || action.IsPvP) continue;
                                    if (!ClassJobCategoryToName_(action.ClassJobCategory).Contains(lastClassJob))
                                    {
                                        break;
                                    }
                                    if ((action.CanTargetParty && !action.IsPvP && !action.IsRoleAction) || action.ID == 17055 || action.ID == 7443)
                                    {
                                        ImGui.Indent();
                                        ImGui.Checkbox(action.AbilityName, ref flagsSelected[j]);
                                        ImGui.Unindent();
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
                for (var i = 0; i < oldActions.Count(); i++)
                {
                    var action = oldActions[i];

                    if (action.CanTargetHostile && !action.IsPvP && !action.IsRoleAction)
                    {
                        if (!lastClassJob.Equals(ClassJobCategoryToName_(action.ClassJobCategory)))
                        {
                            lastClassJob = ClassJobCategoryToName_(action.ClassJobCategory);
                            if (ImGui.CollapsingHeader(lastClassJob + "##Damage"))
                            {
                                for (int j = i; j < oldActions.Count(); j++)
                                {
                                    action = oldActions[j];
                                    if (action.IsRoleAction || action.IsPvP) continue;
                                    if (!ClassJobCategoryToName_(action.ClassJobCategory).Contains(lastClassJob))
                                    {
                                        break;
                                    }
                                    if (action.CanTargetHostile && !action.IsPvP && !action.IsRoleAction)
                                    {
                                        ImGui.Indent();
                                        ImGui.Checkbox(action.AbilityName, ref flagsSelected[j]);
                                        ImGui.Unindent();
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
                for (var i = 0; i < oldActions.Count(); i++)
                {
                    var action = oldActions[i];
                    if (action.IsRoleAction)
                    {
                        if (!lastClassJob.Equals(ClassJobCategoryToName_(action.ClassJobCategory)))
                        {
                            lastClassJob = ClassJobCategoryToName_(action.ClassJobCategory);
                            if (ImGui.CollapsingHeader(lastClassJob))
                            {
                                for (int j = i; j < oldActions.Count(); j++)
                                {
                                    action = oldActions[j];
                                    if (!lastClassJob.Equals(ClassJobCategoryToName_(action.ClassJobCategory)))
                                    {
                                        break;
                                    }
                                    if (!action.IsPvP && (action.CanTargetHostile || action.CanTargetParty)){
                                        ImGui.Indent();
                                        ImGui.Checkbox(action.AbilityName, ref flagsSelected[j]);
                                        ImGui.Unindent();
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

            if (ImGui.Button("Save"))
            {
                SaveOld();
            }
            ImGui.SameLine();

            if (ImGui.Button("Save and Close"))
            {
                isImguiMoSetupOpen = false;
                SaveOld();
            }

            ImGui.SameLine();
            if (ImGui.Button("Use action stacks"))
            {
                OldConfig = false;
                SaveOld();
                SetNewConfig();
                return;
            }

            ImGui.Spacing();
            ImGui.End();
        }

        private void DrawNewConfig()
        {
            ImGui.Begin("Action stack setup", ref isImguiMoSetupOpen,
                ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar);
            ImGui.Text("This window allows you to set up your action stacks.");
            ImGui.Text("YOU SHOULD PROBABLY WATCH THIS VIDEO FOR THE 2.0 UPDATE: ");
            ImGui.SameLine();
            if (ImGui.Button("\"This video\""))
            {
                System.Diagnostics.Process.Start("https://youtu.be/pm4eCxD90gs");
            }
            ImGui.Separator();
            ImGui.BeginChild("scrolling", new Vector2(0, ImGui.GetWindowSize().Y - 125), true, ImGuiWindowFlags.NoScrollbar);
            string jobname = "";
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 3));
            for (var i = 0; i < SortedStackFlags.Count; i++)
            {
                var currentStack = SortedStackFlags[i];

                if (!currentStack.notDeleted)
                {
                    SortedStackFlags.RemoveAt(i);
                    Stacks.RemoveAt(i);
                    i--;
                    continue;
                }

                if (!jobname.Equals(soloJobNames[currentStack.refjob])) {
                    jobname = soloJobNames[currentStack.refjob];

                    if (ImGui.CollapsingHeader(jobname))
                    {
                        ImGui.Indent();
                        for (int j = i; j < SortedStackFlags.Count(); j++)
                        {
                            currentStack = SortedStackFlags[j];

                            if (!jobname.Equals(soloJobNames[currentStack.refjob]))
                            {
                                break;
                            }

                            actions = GetJobActions(currentStack.jobs);
                            actionNames = actions.Select(s => s.AbilityName).ToArray();

                            ImGui.SetNextItemOpen(SortedStackFlags[j].isOpen);
                            try
                            {
                                var abilityName = "";
                                if (currentStack.baseAbility == -1) abilityName = "Unset Ability";
                                else abilityName = actionNames[currentStack.baseAbility];
                                if (SortedStackFlags[j].isOpen = ImGui.CollapsingHeader(abilityName + "##" + j, ref SortedStackFlags[j].notDeleted, ImGuiTreeNodeFlags.DefaultOpen))
                                {

                                    ImGui.PushItemWidth(70);

                                    ImGui.Combo("Job##" + j, ref SortedStackFlags[j].jobs, soloJobNames, soloJobNames.Length);
                                    ImGui.PopItemWidth();

                                    ImGui.Indent();
                                    actions = GetJobActions(currentStack.jobs);
                                    actionNames = actions.Select(s => s.AbilityName).ToArray();
                                    if (currentStack.lastJob != currentStack.jobs)
                                    {
                                        SortedStackFlags[j].stackAbilities.Clear();
                                        SortedStackFlags[j].stackAbilities.Add(-1);
                                        SortedStackFlags[j].baseAbility = -1;
                                        actions = GetJobActions(currentStack.jobs);
                                        actionNames = actions.Select(s => s.AbilityName).ToArray();
                                        SortedStackFlags[j].lastJob = currentStack.jobs;
                                    }

                                    ImGui.PushItemWidth(200);
                                    ImGui.Combo("Base Ability##" + j, ref SortedStackFlags[j].baseAbility, actionNames, actions.Count);

                                    var tmptargs = SortedStackFlags[j].stackTargets.ToArray();
                                    var tmpabilities = SortedStackFlags[j].stackAbilities.ToArray();
                                    if (SortedStackFlags[i].baseAbility != -1 && tmpabilities[0] == -1)
                                        tmpabilities[0] = SortedStackFlags[i].baseAbility;
                                    /*for (int k = 0; k < tmpabilities.Length; k++)
                                    {
                                        if (SortedStackFlags[j].baseAbility != -1 && tmpabilities[k] == -1)
                                            tmpabilities[k] = SortedStackFlags[j].baseAbility;
                                    }*/
                                    ImGui.Indent();

                                    for (int k = 0; k < SortedStackFlags[j].stackAbilities.Count; k++)
                                    {
                                        ImGui.Text("Ability #" + (k + 1));
                                        ImGui.Combo("Target##" + j.ToString() + k.ToString(), ref tmptargs[k], tTypeNames, TargetTypes.Count);
                                        ImGui.SameLine(0, 35);
                                        ImGui.Combo("Ability##" + j.ToString() + k.ToString(), ref tmpabilities[k], actionNames, actions.Count);

                                        if (tmptargs.Length != 1) ImGui.SameLine(0, 10);
                                        if (tmptargs.Length != 1 && ImGui.Button("Delete Ability##" + j.ToString() + k.ToString()))
                                        {
                                            SortedStackFlags[j].stackAbilities.RemoveAt(k);
                                            SortedStackFlags[j].stackTargets.RemoveAt(k);
                                            tmptargs = SortedStackFlags[j].stackTargets.ToArray();
                                            tmpabilities = SortedStackFlags[j].stackAbilities.ToArray();
                                        }
                                    }
                                    ImGui.Unindent();
                                    ImGui.PopItemWidth();
                                    SortedStackFlags[j].stackTargets = tmptargs.ToList();
                                    SortedStackFlags[j].stackAbilities = tmpabilities.ToList();
                                    

                                    if (SortedStackFlags[j].baseAbility != -1)
                                    {
                                        actions = GetJobActions(currentStack.jobs);
                                        actionNames = actions.Select(s => s.AbilityName).ToArray();
                                    }
                                    
                                    if (ImGui.Button("Add action to bottom of stack##" + i.ToString() + j.ToString()))
                                    {
                                        if (SortedStackFlags[j].stackAbilities.Last() != -1)
                                        {
                                            SortedStackFlags[j].stackAbilities.Add(-1);
                                            SortedStackFlags[j].stackTargets.Add(-1);
                                        }
                                    }
                                    ImGui.Unindent();
                                }
                            } catch (Exception e)
                            {
                                PluginLog.LogError(e.Message);
                            }
                        }
                        ImGui.Unindent();
                    }
                }
            }
            for (int i = 0; i < UnsortedStackFlags.Count; i++)
            {
                var currentSettings = UnsortedStackFlags[i];

                if (!currentSettings.notDeleted)
                {
                    UnsortedStackFlags.RemoveAt(i);
                    i--;
                    continue;
                }

                jobname = "";
                if (currentSettings.jobs >= 0) jobname = " - " + soloJobNames[currentSettings.jobs];
                ImGui.SetNextItemOpen(UnsortedStackFlags[i].isOpen);
                actions = GetJobActions(currentSettings.jobs);
                actionNames = actions.Select(s => s.AbilityName).ToArray();
                var abilityName = "";
                if (currentSettings.baseAbility == -1) abilityName = "Unset Ability";
                else abilityName = actionNames[currentSettings.baseAbility];
                if (UnsortedStackFlags[i].isOpen = ImGui.CollapsingHeader(abilityName + "###" + i, ref UnsortedStackFlags[i].notDeleted, ImGuiTreeNodeFlags.DefaultOpen))
                {

                    ImGui.PushItemWidth(70);

                    ImGui.Combo("Job##" + i, ref UnsortedStackFlags[i].jobs, soloJobNames, soloJobNames.Length);
                    ImGui.PopItemWidth();

                    ImGui.Indent();

                    if (currentSettings.jobs != -1)
                    {
                        actions = GetJobActions(currentSettings.jobs);
                        actionNames = actions.Select(s => s.AbilityName).ToArray();

                        if (currentSettings.lastJob != currentSettings.jobs)
                        {
                            for (int j = 0; j < currentSettings.stackAbilities.Count; j++)
                                UnsortedStackFlags[i].stackAbilities[j] = -1;
                            UnsortedStackFlags[i].baseAbility = -1;
                            UnsortedStackFlags[i].lastJob = currentSettings.jobs;
                        }
                    }
                    else
                    {
                        actions = new List<ApplicableAction>();
                        actionNames = new string[0];
                        for (int j = 0; j < currentSettings.stackAbilities.Count; j++)
                            UnsortedStackFlags[i].stackAbilities[j] = -1;
                        UnsortedStackFlags[i].baseAbility = -1;
                    }
                    UnsortedStackFlags[i].lastJob = UnsortedStackFlags[i].jobs;
                    ImGui.PushItemWidth(200);
                    ImGui.Combo("Base Ability##" + i, ref UnsortedStackFlags[i].baseAbility, actionNames, actions.Count);


                    var tmptargs = UnsortedStackFlags[i].stackTargets.ToArray();
                    var tmpabilities = UnsortedStackFlags[i].stackAbilities.ToArray();
                    if (UnsortedStackFlags[i].baseAbility != -1 && tmpabilities[0] == -1)
                        tmpabilities[0] = UnsortedStackFlags[i].baseAbility;
                    /*for (int j = 0; j < tmpabilities.Length; j++)
                    {
                        if (UnsortedStackFlags[i].baseAbility != -1 && tmpabilities[j] == -1)
                            tmpabilities[j] = UnsortedStackFlags[i].baseAbility;
                    }*/
                    ImGui.Indent();
                    for (int j = 0; j < UnsortedStackFlags[i].stackAbilities.Count; j++)
                    {
                        ImGui.Text("Ability #" + (j + 1));
                        ImGui.Combo("Target##" + i.ToString() + j.ToString(), ref tmptargs[j], tTypeNames, TargetTypes.Count);
                        ImGui.SameLine(0, 35);
                        ImGui.Combo("Ability##" + i.ToString() + j.ToString(), ref tmpabilities[j], actionNames, actions.Count);

                        if (tmptargs.Length != 1) ImGui.SameLine(0, 10);
                        if (tmptargs.Length != 1 && ImGui.Button("Delete Ability##" + i.ToString() + j.ToString()))
                        {
                            UnsortedStackFlags[i].stackAbilities.RemoveAt(j);
                            UnsortedStackFlags[i].stackTargets.RemoveAt(j);
                            tmptargs = UnsortedStackFlags[i].stackTargets.ToArray();
                            tmpabilities = UnsortedStackFlags[i].stackAbilities.ToArray();
                        }
                    }

                    ImGui.Unindent();
                    ImGui.PopItemWidth();
                    UnsortedStackFlags[i].stackTargets = tmptargs.ToList();
                    UnsortedStackFlags[i].stackAbilities = tmpabilities.ToList();
                    
                    if (ImGui.Button("Add action to bottom of stack##" + i))
                    {
                        if (UnsortedStackFlags[i].stackAbilities.Last() != -1)
                        {
                            UnsortedStackFlags[i].stackAbilities.Add(-1);
                            UnsortedStackFlags[i].stackTargets.Add(-1);
                        }
                    }
                    ImGui.Unindent();
                }

            }
            ImGui.Separator();
            if (ImGui.Button("Add stack"))
            {
                if (UnsortedStackFlags.Count == 0 || UnsortedStackFlags.Last().jobs != -1)
                {

                    List<StackEntry> newStack = new List<StackEntry>
                    {
                        new StackEntry(0, null)
                    };
                    List<int> tmp1 = new List<int>(1);
                    List<int> tmp2 = new List<int>(1);
                    tmp1.Add(-1);
                    tmp2.Add(0);
                    UnsortedStackFlags.Add(new GuiSettings(true, true, -1, -1, -1, -1, tmp1, tmp2));
                }
            }

            ImGui.PopStyleVar();

            ImGui.EndChild();

            ImGui.Separator();

            if (ImGui.Button("Save"))
            {
                SaveNew();
                UpdateConfig();
                SetNewConfig();
            }
            ImGui.SameLine();

            if (ImGui.Button("Save and Close"))
            {
                isImguiMoSetupOpen = false;
                StackUpdateAndSave();
            }
            ImGui.SameLine();
            if (ImGui.Button("Use Old Configuration"))
            {
                OldConfig = true;
                StackUpdateAndSave();
                SetOldConfig();
                return;
            }
            ImGui.Spacing();
            ImGui.End();
        }

        private void StackUpdateAndSave()
        {
            SaveNew();
            MergeUnsorted();
            UpdateConfig();
            SetNewConfig();
        }

        private void MergeUnsorted()
        {
            SortedStackFlags.AddRange(UnsortedStackFlags);
            SortedStackFlags.Sort();
            UnsortedStackFlags.Clear();
            for (var i = 0; i < SortedStackFlags.Count; i++)
                SortedStackFlags[i].refjob = SortedStackFlags[i].jobs;
            SaveNew();
        }

        private void SaveOld()
        {
            UpdateConfig();
            pluginInterface.SavePluginConfig(Configuration);
            SetOldConfig();
        }

        private void SaveNew()
        {
            for (var i = 0; i < SortedStackFlags.Count; i++)
            {
                //if (SortedStackFlags[i].refjob != SortedStackFlags[i].jobs) SortedStackFlags[i].refjob = SortedStackFlags[i].jobs;
                for (var j = 0; j < SortedStackFlags[i].stackAbilities.Count; j++)
                {
                    if (SortedStackFlags[i].stackAbilities[j] == -1 || SortedStackFlags[i].stackTargets[j] == -1)
                    {
                        SortedStackFlags[i].stackAbilities.RemoveAt(j);
                        SortedStackFlags[i].stackTargets.RemoveAt(j);
                        j--;
                    }
                }
                if (SortedStackFlags[i].stackAbilities.Count == 0)
                {
                    SortedStackFlags.RemoveAt(i);
                    i--;
                }
            }
            for (var i = 0; i < UnsortedStackFlags.Count; i++)
            {
                for (var j = 0; j < UnsortedStackFlags[i].stackAbilities.Count; j++)
                {
                    if (UnsortedStackFlags[i].stackAbilities[j] == -1 || UnsortedStackFlags[i].stackTargets[j] == -1)
                    {
                        UnsortedStackFlags[i].stackAbilities.RemoveAt(j);
                        UnsortedStackFlags[i].stackTargets.RemoveAt(j);
                        j--;
                    }
                }
                if (UnsortedStackFlags[i].stackAbilities.Count == 0)
                {
                    UnsortedStackFlags.RemoveAt(i);
                    i--;
                }
            }
            
        }

        private void UpdateConfig()
        {
            Configuration.Version = CURRENT_CONFIG_VERSION;
            Configuration.SetStackFlags(SortedStackFlags);
            Configuration.SetStacks(Stacks);
            Configuration.SetOldFlags(flagsSelected);
            Configuration.SetWindowVersion(OldConfig);
            Configuration.SetOldField(FieldMOEnabled);
            Configuration.SetOldMO(UIMOEnabled);
            pluginInterface.SavePluginConfig(Configuration);
        }

        private void SetOldConfig()
        {
            moAction.Stacks.Clear();
            for (int i = 0; i < oldActions.Count(); i++)
            {
                if (flagsSelected[i] == true)
                {
                    var tmp = new List<StackEntry>();
                    if (UIMOEnabled) tmp.Add(new StackEntry(oldActions[i].ID, TargetTypes[0]));
                    if (FieldMOEnabled) tmp.Add(new StackEntry(oldActions[i].ID, TargetTypes[1]));
                    moAction.Stacks[oldActions[i].ID] = tmp;
                }
                else
                {
                    moAction.Stacks.Remove(oldActions[i].ID);
                }
            }
        }

        private void SetNewConfig()
        {
            moAction.Stacks.Clear();
            Stacks.Clear();
            //Stacks = Configuration.Stacks;
            SortedStackFlags = Configuration.StackFlags;
            for (var i = 0; i < SortedStackFlags.Count; i++)
            {
                var actions = GetJobActions(SortedStackFlags[i].jobs);
                var hasActions = actions.Count > 0;
                if (!hasActions) Stacks.Add((0, new List<StackEntry>()));
                else Stacks.Add((actions[SortedStackFlags[i].baseAbility].ID, new List<StackEntry>()));
                for (var j = 0; j < SortedStackFlags[i].stackAbilities.Count; j++) {
                    if (!hasActions) Stacks[i].value.Add(new StackEntry(0, null));
                    else Stacks[i].value.Add(new StackEntry(actions[SortedStackFlags[i].stackAbilities[j]].ID, TargetTypes[SortedStackFlags[i].stackTargets[j]]));
                    //Stacks[i].value[j].target = TargetTypes[StackFlags[i].stackTargets[j]];
                }
                if (Stacks[i].key > 0)
                    moAction.Stacks[Stacks[i].key] = Stacks[i].value;
            }
            var currsize = Stacks.Count;
            for (var i = 0; i < UnsortedStackFlags.Count; i++)
            {
                var actions = GetJobActions(UnsortedStackFlags[i].jobs);
                var hasActions = actions.Count > 0;
                if (!hasActions) Stacks.Add((0, new List<StackEntry>()));
                else Stacks.Add((actions[UnsortedStackFlags[i].baseAbility].ID, new List<StackEntry>()));
                for (var j = 0; j < UnsortedStackFlags[i].stackAbilities.Count; j++)
                {
                    if (!hasActions) Stacks[i + currsize].value.Add(new StackEntry(0, null));
                    else Stacks[i + currsize].value.Add(new StackEntry(actions[UnsortedStackFlags[i].stackAbilities[j]].ID, TargetTypes[UnsortedStackFlags[i].stackTargets[j]]));
                    //Stacks[i].value[j].target = TargetTypes[StackFlags[i].stackTargets[j]];
                }
                if (Stacks[i + currsize].key > 0)
                    moAction.Stacks[Stacks[i + currsize].key] = Stacks[i + currsize].value;
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
            return applicableActions.First(elem => elem.ID == actionID).AbilityName;
        }

        private String ClassJobCategoryToName_(byte key)
        {
            if (key == 68) return "ACN SCH SMN";
            var tmp = ClassJobCategoryToName(key);
            switch (tmp)
            {
                case "SMN SCH":
                    return "ACN SCH SMN";
                case "WHM SCH AST":
                    return "Healers";
                case "BLM SMN RDM BLU":
                    return "Casters";
                case "BRD MCH DNC":
                    return "Ranged";
                case "PLD WAR DRK GNB":
                    return "Tanks";
                case "MNK DRG NIN SAM":
                    return "Melee";
            }
            return tmp;
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
                case 61: return "WHM SCH AST";
                case 63: return "BLM SMN RDM BLU";
                case 64: return "WHM SCH AST";
                case 66: return "BRD MCH DNC";
                case 68: return "SCH SMN";
                case 69: return "SMN";
                case 73: return "WHM SCH AST";
                case 89: return "BLM SMN RDM BLU";
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
                case 114: return "MNK DRG NIN SAM";
                case 115: return "BRD MCH DNC";
                case 116: return "BLM SMN RDM BLU";
                case 117: return "WHM SCH AST";
                case 118: return "MNK DRG NIN SAM";
                case 121: return "PLD WAR DRK GNB";
                case 122: return "MNK DRG NIN SAM";
                case 123: return "BRD MCH DNC";
                case 125: return "WHM SCH AST";
                case 129: return "BLU";
                case 133: return "WHM SCH AST";
                case 134: return "PLD WAR DRK GNB";
                case 139: return "BRD MCH DNC";
                case 147: return "BLM SMN RDM BLU";
                case 148: return "MNK DRG NIN SAM";
                case 149: return "GNB";
                case 150: return "DNC";
                case 160: return "SCH";
                default: return "Unknown";

            }
        }

        private List<ApplicableAction> GetJobActions(int index)
        {
            if (index >= 0)
            {
                return applicableActions.
                        Where(item => ClassJobCategoryToName(item.ClassJobCategory).
                                        Contains(soloJobNames[index]) &&
                                        !item.IsPvP &&
                                        (item.CanTargetParty || item.CanTargetHostile
                                        || item.ID == 17055 || item.ID == 7443)).
                        ToList();
            }
            return new List<ApplicableAction>();
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
            oldActions.Clear();
            foreach (ApplicableAction elem in tmp)
            {
                applicableActions.Add(elem);
                oldActions.Add(elem);
            }
        }
    }
}
