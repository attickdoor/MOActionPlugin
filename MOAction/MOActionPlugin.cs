using Dalamud.Game.Command;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using MOAction.Target;
using MOAction.Configuration;
using Dalamud.Data;
using Dalamud.Game.ClientState;
using Dalamud.Game;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Keys;
using System.Diagnostics;
using FFXIVClientStructs;
using Dalamud.Logging;
using SigScanner = Dalamud.Game.SigScanner;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Dalamud.Game.Gui;
using System.Text.Json;
using System.Text;
using Newtonsoft.Json;

namespace MOAction
{
    // This class handles the visual GUI frontend and organizing of action stacks.
    internal class MOActionPlugin : IDalamudPlugin
    {
        public string Name => "Mouseover Action Plugin";

        public MOActionConfiguration Configuration;

        private DalamudPluginInterface pluginInterface;
        private MOAction moAction;

        private List<Lumina.Excel.GeneratedSheets.Action> applicableActions;
        private List<TargetType> TargetTypes;
        private List<TargetType> GroundTargetTypes;
        private readonly string[] tTypeNames = { "UI Mouseover", "Field Mouseover", "Regular Target", "Focus Target", "Target of Target", "Myself", "<2>", "<3>", "<4>", "<5>", "<6>", "<7>", "<8>", "Mouse Cursor (Ground Target)" };
        private List<GuiSettings> SortedStackFlags;
        private List<GuiSettings> UnsortedStackFlags;
        private List<MoActionStack> NewStacks;
        private Dictionary<string, HashSet<MoActionStack>> SavedStacks;
        //List<Lumina.Excel.GeneratedSheets.Action> actions;
    
        private bool firstTimeUpgrade = false;
        private bool rangeCheck;
        private bool mouseClamp;
        private bool otherGroundClamp;

        private readonly string[] soloJobNames = { "AST", "WHM", "SCH", "SMN", "BLM", "RDM", "BLU", "BRD", "MCH", "DNC", "DRK", "GNB", "WAR", "PLD", "DRG", "MNK", "SAM", "NIN" };
        private readonly string[] roleActionNames = { "BLM SMN RDM BLU", "BRD MCH DNC",  "MNK DRG NIN SAM", "PLD WAR DRK GNB", "WHM SCH AST"};
        private readonly uint[] GroundTargets = { 3569, 3639, 188, 7439, 2262 };

        private Dictionary<string, List<Lumina.Excel.GeneratedSheets.Action>> JobActions;

        private bool isImguiMoSetupOpen = false;

        private readonly int CURRENT_CONFIG_VERSION = 6;

        private ClientState clientState;
        private TargetManager targetManager;
        private DataManager dataManager;
        private CommandManager commandManager;
        private SigScanner SigScanner;
        private KeyState KeyState;
        //private FFXIVClientStructs.Attributes.Addon addon;

        private bool testo; 

        unsafe public MOActionPlugin(DalamudPluginInterface pluginInterface,  CommandManager commands,  DataManager datamanager, GameGui gamegui, KeyState keystate, ObjectTable objects, SigScanner scanner, ClientState clientstate, TargetManager targetmanager)
        {
            this.pluginInterface = pluginInterface;

            commands.AddHandler("/pmoaction", new CommandInfo(OnCommandDebugMouseover)
            {
                HelpMessage = "Open a window to edit mouseover action settings.",
                ShowInHelp = true
            });

            applicableActions = new List<Lumina.Excel.GeneratedSheets.Action>();
            clientState = clientstate;
            dataManager = datamanager;
            targetManager = targetmanager;
            commandManager = commands;
            SigScanner = scanner;
            KeyState = keystate;

            NewStacks = new();
            SavedStacks = new();
            foreach (var a in dataManager.GetExcelSheet<Lumina.Excel.GeneratedSheets.Action>().Where(row => row.IsPlayerAction && !row.IsPvP).ToList())
            {
                // compatability with xivcombo, enochian turns into f/b4
                if (a.RowId == 3575)
                    a.CanTargetHostile = true;
                // Ley Lines and Passage of Arms are not true ground target
                else if (a.RowId == 3573 || a.RowId == 7385)
                    a.CastType = 1;
                // Other ground targets have to be able to target anything
                else if (GroundTargets.Contains(a.RowId)) {
                    a.CanTargetDead = true;
                    a.CanTargetFriendly = true;
                    a.CanTargetHostile = true;
                    a.CanTargetParty = true;
                    a.CanTargetSelf = true;
                }
                applicableActions.Add(a);
                /*
                if (a.RowId == 3575)
                    applicableActions.Add(new ApplicableAction((uint)a.RowId, a.Name, a.IsRoleAction, a.CanTargetSelf, a.CanTargetParty, a.CanTargetFriendly, true, (byte)a.ClassJobCategory.Row, a.IsPvP));
                else if (a.RowId == 17055 || a.RowId == 7443)
                    applicableActions.Add(new ApplicableAction((uint)a.RowId, a.Name, a.IsRoleAction, true, true, a.CanTargetFriendly, a.CanTargetHostile, (byte)a.ClassJobCategory.Row, a.IsPvP));
                else
                    applicableActions.Add(new ApplicableAction((uint)a.RowId, a.Name, a.IsRoleAction, a.CanTargetSelf, a.CanTargetParty, a.CanTargetFriendly, a.CanTargetHostile, (byte)a.ClassJobCategory.Row, a.IsPvP));
                */
            }
            JobActions = new();
            SortActions();
            moAction = new MOAction(SigScanner, clientState, dataManager, targetManager, objects, keystate, gamegui);

            foreach (string jobname in soloJobNames)
            {
                JobActions.Add(jobname, applicableActions.Where(action => action.ClassJobCategory.Value.Name.ToString().Contains(jobname)).ToList());
            }

            TargetTypes = new List<TargetType>
            {
                new EntityTarget(moAction.GetGuiMoPtr, "UI Mouseover"),
                new EntityTarget(moAction.NewFieldMo, "Field Mouseover"),
                //new ActorTarget(() => objects.First(a => a.ObjectId == moAction.GetFieldMoPtr()), "Field Mouseover"),
                new EntityTarget(() => moAction.GetActorFromPlaceholder("<t>"), "Target"),
                new EntityTarget(() => moAction.GetActorFromPlaceholder("<f>"), "Focus Target"),
                new EntityTarget(() => moAction.GetActorFromPlaceholder("<tt>"), "Target of Target"),
                new EntityTarget(() => moAction.GetActorFromPlaceholder("<me>"), "Self"),
                new EntityTarget(() => moAction.GetActorFromPlaceholder("<2>"), "<2>"),
                new EntityTarget(() => moAction.GetActorFromPlaceholder("<3>"), "<3>"),
                new EntityTarget(() => moAction.GetActorFromPlaceholder("<4>"), "<4>"),
                new EntityTarget(() => moAction.GetActorFromPlaceholder("<5>"), "<5>"),
                new EntityTarget(() => moAction.GetActorFromPlaceholder("<6>"), "<6>"),
                new EntityTarget(() => moAction.GetActorFromPlaceholder("<7>"), "<7>"),
                new EntityTarget(() => moAction.GetActorFromPlaceholder("<8>"), "<8>")
            };

            GroundTargetTypes = new List<TargetType>
            {
                new EntityTarget(() => null, "Mouse Location", false),
            };

            var config = pluginInterface.GetPluginConfig() as MOActionConfiguration ?? new MOActionConfiguration();

            // big upgrade for old moaction config
            if (config.Version < 6)
            {
                config = new MOActionConfiguration();
                firstTimeUpgrade = true;
            }
            Configuration = config;
            {
                var tmpstacks = RebuildStacks(Configuration.Stacks);
                SavedStacks = SortStacks(tmpstacks);
            }
            rangeCheck = Configuration.RangeCheck;
            mouseClamp = Configuration.MouseClamp;
            otherGroundClamp = Configuration.OtherGroundClamp;

            moAction.SetConfig(Configuration);

            moAction.Enable();
            foreach (var entry in SavedStacks)
            {
                moAction.Stacks.AddRange(entry.Value);
            }
            pluginInterface.UiBuilder.OpenConfigUi += () => isImguiMoSetupOpen = true;
            pluginInterface.UiBuilder.Draw += UiBuilder_OnBuildUi;

        }


        private void UiBuilder_OnBuildUi()
        {
            if (firstTimeUpgrade)
                DrawUpgradeNotice();
            else if (isImguiMoSetupOpen)
                DrawConfig();
            else if (NewStacks.Count != 0)
                SortStacks();
        }

        private void DrawUpgradeNotice()
        {
            ImGui.SetNextWindowSize(new Vector2(800, 400), ImGuiCond.Appearing);
            ImGui.Begin("MOAction 4.0 update Information");
            ImGui.Text("MOAction 4.0 is a BREAKING update. This means that the entirety of your config has been wiped.");
            ImGui.Text("This is an unfortunate side effect of me being not the best developer. I apologize but it's the only sane way forward. Here are the patch notes:");
            ImGui.Text("- Removed \"old config\". It's never coming back. You will set up your stacks and you will like it.");
            ImGui.Text("- Added support for every PvE-usable action in the game.");
            ImGui.Text("-- Ground targeted actions WILL queue properly, and DO work for all placeholders. There is a special mouse placement option as well for them.");
            if (ImGui.Button("ok"))
                firstTimeUpgrade = false;
            ImGui.End();
        }

        private void DrawConfigForList(ICollection<MoActionStack> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                ImGui.PushID(i);
                var entry = list.ElementAt(i);
                //ImGui.PushID(entry.BaseAction.Name); //push base action
                                                   // By default, no action is selected.
                if (ImGui.CollapsingHeader(entry.BaseAction == null? "Unset Action###" : entry.BaseAction.Name + "###"))
                {
                    ImGui.SetNextItemWidth(100);
                    // Require user to select a job, filtering actions down.
                    if (ImGui.BeginCombo("Job", entry.Job))
                    {
                        foreach (string job in soloJobNames)
                        {
                            if (ImGui.Selectable(job))
                            {
                                if (entry.Job != null && entry.Job != job)
                                {
                                    entry.BaseAction = null;
                                    foreach (var stackentry in entry.Entries)
                                        stackentry.Action = null;
                                }
                                entry.Job = job;
                            }
                        }
                        ImGui.EndCombo();
                    }
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(100);
                    if (ImGui.BeginCombo("Held Modifier Key", entry.Modifier.ToString()))
                    {
                        foreach (VirtualKey vk in MoActionStack.AllKeys)
                        {
                            if (ImGui.Selectable(vk.ToString()))
                            {
                                entry.Modifier = vk;
                            }
                        }
                        ImGui.EndCombo();
                    }
                    if (entry.Job != "Unset Job")
                    {
                        // ImGui.PushID(entry.Job);
                        ImGui.Indent();
                        // Select base action.
                        ImGui.SetNextItemWidth(200);
                        if (ImGui.BeginCombo("Base Action", entry.BaseAction == null ? "" : entry.BaseAction.Name))
                        {
                            foreach (var actionEntry in JobActions[entry.Job])
                            {
                                if (ImGui.Selectable(actionEntry.Name))
                                {
                                    entry.BaseAction = actionEntry;
                                    // By default, add UI mouseover as the first TargetType
                                    if (entry.Entries.Count == 0)
                                    {
                                        entry.Entries.Add(new(actionEntry, TargetTypes[0]));
                                    }
                                    else
                                    {
                                        entry.Entries[0].Action = actionEntry;
                                    }
                                }
                            }
                            ImGui.EndCombo();
                        }
                        if (entry.BaseAction != null)
                        {
                            // ImGui.PushID(entry.BaseAction.Name); // push action name
                            ImGui.Indent();
                            for (int j = 0; j < entry.Entries.Count; j++)
                            {
                                var stackEntry = entry.Entries[j];

                                ImGui.PushID(j); // push stack entry number
                                                 //foreach (var stackEntry in entry.Entries)
                                                 //{
                                ImGui.Text($"Ability #{entry.Entries.IndexOf(stackEntry) + 1}");
                                ImGui.SetNextItemWidth(200);
                                if (ImGui.BeginCombo("Target", stackEntry.Target == null ? "" : stackEntry.Target.TargetName))
                                {
                                    foreach (var target in TargetTypes)
                                    {
                                        if (ImGui.Selectable(target.TargetName))
                                        {
                                            stackEntry.Target = target;
                                        }
                                    }
                                    if (stackEntry.Action.CastType == 7)
                                    {
                                        foreach (var target in GroundTargetTypes)
                                            if (ImGui.Selectable(target.TargetName))
                                            {
                                                stackEntry.Target = target;
                                            }
                                    }
                                    
                                    ImGui.EndCombo();
                                }
                                ImGui.SameLine();
                                ImGui.SetNextItemWidth(200);
                                if (ImGui.BeginCombo("Ability", stackEntry.Action == null ? "" : stackEntry.Action.Name))
                                {
                                    foreach (var ability in JobActions[entry.Job])
                                    {
                                        if (ImGui.Selectable(ability.Name))
                                        {
                                            stackEntry.Action = ability;
                                            if (ability.CastType != 7 && GroundTargetTypes.Contains(stackEntry.Target))
                                            {
                                                stackEntry.Target = null;
                                            }
                                        }
                                    }
                                    ImGui.EndCombo();
                                }

                                // TODO: foreach makes lists immutable, use for-loop
                                if (entry.Entries.Count() > 1)
                                {
                                    ImGui.SameLine();
                                    if (ImGui.Button("Delete Entry"))
                                    {
                                        entry.Entries.Remove(stackEntry);
                                        j--;
                                    }
                                }
                                ImGui.PopID(); //pop stack entry number
                            }

                            ImGui.Unindent();
                            // Add new entry to bottom of stack.
                            if (ImGui.Button("Add new stack entry"))
                            {
                                entry.Entries.Add(new(entry.BaseAction, null));
                            }
                            ImGui.SameLine();
                            if (ImGui.Button("Copy stack to clipboard"))
                            {
                                CopyToClipboard(new() { entry });
                            }
                            ImGui.SameLine();
                            if (ImGui.Button ("Delete Stack"))
                            {
                                list.Remove(entry);
                                //list.RemoveAt(i);
                                i--;
                            }

                            // ImGui.PopID(); //pop action name
                        }
                        // ImGui.PopID(); // pop job name
                        ImGui.Unindent();
                    }

                    //foreach (string job in soloJobNames)
                    //ImGui.Indent();
                }
                //ImGui.PopID(); //pop base action
                ImGui.PopID(); //pop i
            }
            
        }

        private void CopyToClipboard(List<MoActionStack> list)
        {
            List<ConfigurationEntry> entries = new();
            foreach (var elem in list)
            {
                var x = Configuration.Stacks.FirstOrDefault(e => elem.Equals(e));
                if (x == default) continue;
                entries.Add(x);
            }
            //var serializer = Newtonsoft.Json.JsonSerializer()
            var json = JsonConvert.SerializeObject(entries);
            ImGui.SetClipboardText(Convert.ToBase64String(Encoding.UTF8.GetBytes(json.ToString())));
        }

        private Dictionary<string, HashSet<MoActionStack>> SortStacks(List<MoActionStack> list)
        {
            Dictionary<string, HashSet<MoActionStack>> toReturn = new();
            foreach (var name in soloJobNames)
            {
                var jobstack = list.Where(x => x.Job == name).ToList();
                if (jobstack.Count > 0)
                    toReturn[name] = new(jobstack);
                else
                    toReturn[name] = new();
            }
            return toReturn;
        }

        private void SaveStacks()
        {
            SortStacks();
            moAction.Stacks.Clear();
            foreach (var x in SavedStacks)
            {
                foreach (var entry in x.Value)
                {
                    moAction.Stacks.Add(entry);
                }
            }
            Configuration.Stacks.Clear();
            foreach (var x in moAction.Stacks)
                Configuration.Stacks.Add(new ConfigurationEntry(x.BaseAction.RowId, x.Entries.Select(y => (y.Target.TargetName, y.Action.RowId)).ToList(), x.Modifier, x.Job));
            UpdateConfig();
        }
        
        private void DrawConfig()
        {
            ImGui.SetNextWindowSize(new Vector2(800, 800), ImGuiCond.Once);
            ImGui.Begin("Action stack setup", ref isImguiMoSetupOpen,
                ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar);
            ImGui.Text("This window allows you to set up your action stacks.");
            ImGui.Text("What is an action stack? ");
            ImGui.SameLine();
            if (ImGui.Button("Click me to learn!"))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://youtu.be/pm4eCxD90gs",
                    UseShellExecute = true
                });
            }
            
            ImGui.Checkbox("Stack entry fails if target is out of range.", ref rangeCheck);
            ImGui.Checkbox("Clamp Ground Target at mouse to max ability range.", ref mouseClamp);
            ImGui.Checkbox("Clamp other Ground Target to max ability range.", ref otherGroundClamp);
            if (ImGui.Button("Copy all stacks to clipboard"))
            {
                CopyToClipboard(moAction.Stacks);
            }
            ImGui.SameLine();
            if (ImGui.Button("Import stacks from clipboard"))
            {
                try
                {
                    SavedStacks = SortStacks(
                        RebuildStacks(
                            JsonConvert.DeserializeObject<List<ConfigurationEntry>>(
                                 Encoding.UTF8.GetString(
                                    Convert.FromBase64String(
                                        ImGui.GetClipboardText())))));
                }
                catch (Exception)
                {
                    ImGui.BeginPopup("oh no, cringe!");
                    ImGui.EndPopup();
                }
            }
            ImGui.BeginChild("scrolling", new Vector2(0, ImGui.GetWindowSize().Y - 250), true, ImGuiWindowFlags.NoScrollbar);
            ImGui.PushID("Sorted Stacks");

            // sorted stacks are grouped by job.
            ImGui.PushID("Sorted Stacks");
            foreach (var jobName in soloJobNames)
            {
                var entries = SavedStacks[jobName];
                if (entries.Count == 0) continue;
                var key = jobName;
                
                ImGui.PushID(key); //push job
                
                ImGui.SetNextItemWidth(300);
                if (ImGui.CollapsingHeader(key))
                {
                    if (ImGui.Button("Copy All to Clipboard"))
                    {
                        CopyToClipboard(entries.ToList());
                    }
                    ImGui.Indent();
                    DrawConfigForList(SavedStacks[jobName]);
                    
                    ImGui.Unindent();
                }
                ImGui.PopID(); //pop job
            }
            ImGui.PopID();
            ImGui.PushID("Unsorted Stacks");
            // Unsorted stacks are created when "Add stack" is clicked.
            DrawConfigForList(NewStacks);
            ImGui.PopID();
            ImGui.EndChild();
            if (ImGui.Button("Save"))
            {
                SaveStacks();
            }
            ImGui.SameLine();
            if (ImGui.Button("Save and Close"))
            {
                isImguiMoSetupOpen = false;
                SaveStacks();
            }
            ImGui.SameLine();
            if (ImGui.Button("New Stack"))
            {
                NewStacks.Add(new(null, new()));
            }
            ImGui.End();
        }

        private void SortStacks()
        {
            foreach (var x in NewStacks)
            {
                if (x.Job != "Unset Job" && x.Entries.Count > 0)
                    SavedStacks[x.Job].Add(x);
            }
            NewStacks.Clear();
        }
        #region TheOnlyGui
        /*
        private void DrawConfig()
        {
            ImGui.SetNextWindowSize(new Vector2(800, 800), ImGuiCond.FirstUseEver);
            ImGui.Begin("Action stack setup", ref isImguiMoSetupOpen,
                ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar);
            ImGui.Text("This window allows you to set up your action stacks.");
            ImGui.Checkbox("Stack entry fails if target is out of range.", ref rangeCheck);
            ImGui.Separator();
            ImGui.BeginChild("scrolling", new Vector2(0, ImGui.GetWindowSize().Y - 125), true, ImGuiWindowFlags.NoScrollbar);
            string jobname = "";
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 3));
            // listing of stacks that have not been newly created
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

                            if (ImGui.BeginCombo("test", "helo"))
                            {
                                foreach (Lumina.Excel.GeneratedSheets.Action a in applicableActions)
                                {
                                    if (ImGui.Selectable(a.Name))
                                    {

                                    }
                                }
                            }

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
                                    /*
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
            // listing of stacks that have been newly created
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
                if (UnsortedStackFlags[i].isOpen = ImGui.CollapsingHeader(abilityName + "###Unsorted" + i, ref UnsortedStackFlags[i].notDeleted, ImGuiTreeNodeFlags.DefaultOpen))
                {

                    ImGui.PushItemWidth(70);

                    ImGui.Combo("Job##Unsorted" + i, ref UnsortedStackFlags[i].jobs, soloJobNames, soloJobNames.Length);
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
                    ImGui.Combo("Base Ability##Unsorted" + i, ref UnsortedStackFlags[i].baseAbility, actionNames, actions.Count);


                    var tmptargs = UnsortedStackFlags[i].stackTargets.ToArray();
                    var tmpabilities = UnsortedStackFlags[i].stackAbilities.ToArray();
                    if (UnsortedStackFlags[i].baseAbility != -1 && tmpabilities[0] == -1)
                        tmpabilities[0] = UnsortedStackFlags[i].baseAbility;
                    /*for (int j = 0; j < tmpabilities.Length; j++)
                    {
                        if (UnsortedStackFlags[i].baseAbility != -1 && tmpabilities[j] == -1)
                            tmpabilities[j] = UnsortedStackFlags[i].baseAbility;
                    }*/ 
                    /*
                    ImGui.Indent();
                    for (int j = 0; j < UnsortedStackFlags[i].stackAbilities.Count; j++)
                    {
                        ImGui.Text("Ability #" + (j + 1));
                        ImGui.Combo("Target##Unsorted" + i.ToString() + j.ToString(), ref tmptargs[j], tTypeNames, TargetTypes.Count);
                        ImGui.SameLine(0, 35);
                        ImGui.Combo("Ability##Unsorted" + i.ToString() + j.ToString(), ref tmpabilities[j], actionNames, actions.Count);

                        if (tmptargs.Length != 1) ImGui.SameLine(0, 10);
                        if (tmptargs.Length != 1 && ImGui.Button("Delete Ability##Unsorted" + i.ToString() + j.ToString()))
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
                    
                    if (ImGui.Button("Add action to bottom of stack##Unsorted" + i))
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

            ImGui.Spacing();
            ImGui.End();
        }
        */
        #endregion TheOnlyGui
        /*
        private void StackUpdateAndSave()
        {
            SaveNew();
            MergeUnsorted();
            UpdateConfig();
            SetNewConfig();
        }
        */
        
        
        private void UpdateConfig()
        {
            Configuration.Version = CURRENT_CONFIG_VERSION;
            Configuration.RangeCheck = rangeCheck;
            Configuration.MouseClamp = mouseClamp;
            Configuration.OtherGroundClamp = otherGroundClamp;

            pluginInterface.SavePluginConfig(Configuration);
        }
        

        public List<MoActionStack> RebuildStacks(List<ConfigurationEntry> configurationEntries)
        {
            if (configurationEntries == null) return new();
            var toReturn = new List<MoActionStack>();
            foreach (var entry in configurationEntries)
            {
                var action = applicableActions.First(x => x.RowId == entry.BaseId);
                string job = entry.Job;
                List<StackEntry> entries = new();
                foreach (var stackEntry in entry.Stack)
                {
                    TargetType targ = TargetTypes.FirstOrDefault(x => x.TargetName == stackEntry.Item1);
                    if (targ == default) targ = GroundTargetTypes[0];
                    entries.Add(new(applicableActions.First(x => x.RowId == stackEntry.Item2), targ));
                }
                MoActionStack tmp = new(action, entries);
                tmp.Job = job;
                tmp.Modifier = entry.Modifier;
                toReturn.Add(tmp);
            }

            return toReturn;
        }
        public void Dispose()
        {
            moAction.Dispose();

            commandManager.RemoveHandler("/pmoaction");

            pluginInterface.Dispose();
        }

        private void OnCommandDebugMouseover(string command, string arguments)
        {
            isImguiMoSetupOpen = true;
        }

        private void SortActions()
        {
            List<Lumina.Excel.GeneratedSheets.Action> tmp = new();
            
            foreach (string elem in soloJobNames)
            {
                foreach (var action in applicableActions)
                {
                    var nameStr = action.ClassJobCategory.Value.Name.ToString();
                    if (nameStr.Contains(elem) && !action.IsRoleAction)
                        tmp.Add(action);
                }
            }

            foreach (string elem in soloJobNames)
            {
                foreach (var action in applicableActions)
                {
                    var nameStr = action.ClassJobCategory.Value.Name.ToString();                  
                    if (nameStr.Contains(elem) && action.IsRoleAction && !tmp.Contains(action))
                        tmp.Add(action);
                }
            }
            applicableActions.Clear();
            //oldActions.Clear();
            foreach (var elem in tmp)
            {
                //oldActions.Add(elem);
                applicableActions.Add(elem);
            }
        }
    }
}
