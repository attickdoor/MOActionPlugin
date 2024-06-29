using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Game;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Hooking;
using Dalamud.Game.ClientState.Keys;
using MOAction.Configuration;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Client.Game;
using static MOAction.MOActionAddressResolver;
using Dalamud;
using Dalamud.Plugin.Services;

namespace MOAction
{
    public class MOAction
    {
        private readonly Dictionary<uint, Lumina.Excel.GeneratedSheets.ClassJob> JobDictionary;
        public delegate bool OnRequestActionDetour(long param_1, byte param_2, ulong param_3, long param_4,
                       uint param_5, uint param_6, uint param_7, long param_8);

        [UnmanagedFunctionPointer(CallingConvention.ThisCall, CharSet = CharSet.Ansi)]
        private delegate ulong ResolvePlaceholderActor(long param1, string param2, byte param3, byte param4);

        private readonly MOActionAddressResolver Address;
        private MOActionConfiguration Configuration;

        private Hook<OnRequestActionDetour> requestActionHook;

        public unsafe delegate RecastTimer* GetGroupTimerDelegate(void* @this, int cooldownGroup);
        private GetGroupTimerDelegate getGroupTimer;

        public List<MoActionStack> Stacks { get; set; }
        private Lumina.Excel.ExcelSheet<Lumina.Excel.GeneratedSheets.Action> RawActions;

        public IntPtr uiMoEntityId = IntPtr.Zero;

        public HashSet<ulong> enabledActions;

        public bool IsGuiMOEnabled = false;
        public bool IsFieldMOEnabled = false;

        private IClientState clientState;
        private ITargetManager targetManager;
        private IDataManager dataManager;
        private ICommandManager commandManager;
        public static IObjectTable objectTable;
        private IGameGui gameGui;
        private IKeyState keyState;
        private IGameInteropProvider hookprovider;
        private IPluginLog pluginLog;

        private unsafe PronounModule* pronounModule;
        private unsafe ActionManager* actionManager;
        private readonly int IdOffset = (int)Marshal.OffsetOf<FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject>("ObjectID");

        public MOAction(ISigScanner scanner,
                        IClientState clientstate,
                        IDataManager datamanager,
                        ITargetManager targetmanager,
                        IObjectTable objects,
                        IKeyState keystate,
                        IGameGui gamegui,
                        IGameInteropProvider hookprovider,
                        IPluginLog pluginLog,
                        Dictionary<uint, Lumina.Excel.GeneratedSheets.ClassJob> JobDictionary
                        )
        {
            Address = new(scanner);
            clientstate.Login += LoadClientModules;
            clientstate.Logout += ClearClientModules;
            if (clientstate.IsLoggedIn)
            {
                LoadClientModules();
            }


            dataManager = datamanager;

            RawActions = dataManager.GetExcelSheet<Lumina.Excel.GeneratedSheets.Action>();

            targetManager = targetmanager;
            clientState = clientstate;
            objectTable = objects;
            keyState = keystate;
            gameGui = gamegui;
            this.hookprovider = hookprovider;
            this.pluginLog = pluginLog;
            this.JobDictionary = JobDictionary;

            Stacks = new();

            pluginLog.Info("===== M O A C T I O N =====");

            enabledActions = new();
        }

        public void SetConfig(MOActionConfiguration config)
        {
            Configuration = config;
        }

        private unsafe void LoadClientModules()
        {
            try
            {
                pronounModule = PronounModule.Instance();
                actionManager = ActionManager.Instance();
                getGroupTimer = Marshal.GetDelegateForFunctionPointer<GetGroupTimerDelegate>((IntPtr)ActionManager.Addresses.GetRecastGroupDetail.Value);
            }
            catch (Exception e)
            {
                pluginLog.Warning(e.Message);
                pluginLog.Warning(e.StackTrace);
                pluginLog.Warning(e.InnerException.ToString());
            }
        }

        private unsafe void ClearClientModules()
        {
            pronounModule = null;
            actionManager = null;
        }

        public void Enable()
        {
            //read current bytes at GtQueuePatch for Dispose
            SafeMemory.ReadBytes(Address.GtQueuePatch, 2, out var prePatch);
            Address.preGtQueuePatchData = prePatch;

            //Apply 2 NOOP actions there (0x90 is noop right?)
            SafeMemory.WriteBytes(Address.GtQueuePatch, new byte[] { 0x90, 0x90 });

            requestActionHook = hookprovider.HookFromAddress((IntPtr)ActionManager.Addresses.UseAction.Value, new OnRequestActionDetour(HandleRequestAction));
            requestActionHook.Enable();
        }

        public void Dispose()
        {
            if (requestActionHook.IsEnabled)
            {
                requestActionHook.Dispose();

                //re-write the original 2 bytes that were there
                //6.5: 0x75 , 0x49 (when printed out i got "uI", and referencing a hex table that'd be 0x75 and 0x49) but this would mean I don't need to know this anymore.
                SafeMemory.WriteBytes(Address.GtQueuePatch, Address.preGtQueuePatchData);
            }
        }

        public unsafe RecastTimer* GetGroupRecastTimer(int group)
        {
            return group < 1 ? null : getGroupTimer(actionManager, group - 1);
        }


        private unsafe bool HandleRequestAction(long param_1, byte actionType, ulong actionID, long target_ptr,
                       uint param_5, uint param_6, uint param_7, long param_8)
        {
            // Only care about "real" actions. Not doing anything dodgy
            if (actionType != 1)
            {
                return requestActionHook.Original(param_1, actionType, actionID, target_ptr, param_5, param_6, param_7, param_8);
            }
            pluginLog.Verbose("Receiving handling request for Action: {id}", actionID);

            var (action, target) = GetActionTarget((uint)actionID, actionType);

            if (action == null)
            {
                return requestActionHook.Original(param_1, actionType, actionID, target_ptr, param_5, param_6, param_7, param_8);
            }

            long objectId = target == null ? 0xE0000000 : target.ObjectId;
            pluginLog.Verbose("Execution Action {action} with ActionID {actionid} on object with ObjectId {objectid}", action.Name, action.RowId, objectId);

            bool ret = requestActionHook.Original(param_1, actionType, action.RowId, objectId, param_5, param_6, param_7, param_8);

            // Enqueue GT action
            if (action.TargetArea)
            {
                *(long*)((IntPtr)actionManager + 0x98) = objectId;
                *(byte*)((IntPtr)actionManager + 0xB8) = 1;
            }
            return ret;
        }

        private unsafe (Lumina.Excel.GeneratedSheets.Action action, GameObject target) GetActionTarget(uint ActionID, uint ActionType)
        {
            var action = RawActions.GetRow(ActionID);

            uint adjusted = actionManager->GetAdjustedActionId(ActionID);

            if (action == null) return (null, null);

            var applicableActions = Stacks.Where(entry => (entry.BaseAction.RowId == action.RowId ||
                                                          entry.BaseAction.RowId == adjusted ||
                                                          actionManager->GetAdjustedActionId(entry.BaseAction.RowId) == adjusted)
                                                          && (clientState.LocalPlayer.ClassJob.Id == UInt32.Parse(entry.Job)
                                                          //FUCK this wasn't easy to get WHM stacks to work on CNJ, ....
                                                          //This works by grabbing parentJob. ParentJob is either a self-reference OR a reference to the non-upgraded class.
                                                          //row ids and job ids are equal, so your classjob.id of 6 = CNJ -> Jobdictionary[24].ClassjobParent (this be CNJ with row 6)
                                                          || clientState.LocalPlayer.ClassJob.Id == JobDictionary[UInt32.Parse(entry.Job)].ClassJobParent.Row
                                                          ));

            MoActionStack stackToUse = null;
            foreach (var entry in applicableActions)
            {
                if (entry.Modifier == VirtualKey.NO_KEY)
                {
                    stackToUse = entry;
                }
                else if (keyState[entry.Modifier])
                {
                    stackToUse = entry;
                    break;
                }
            }

            if (stackToUse == null)
            {
                pluginLog.Verbose("No action stack applicable for action: {action}", action.Name);
                return (null, null);
            }
            foreach (StackEntry entry in stackToUse.Entries)
            {
                pluginLog.Verbose("unadjusted entry action, {rowid}, {name}", entry.Action.RowId, entry.Action.Name);
                var (response, target) = CanUseAction(entry, ActionType);
                if (response)
                {
                    return (entry.Action, target);
                }

            }
            pluginLog.Verbose("Chosen MoAction Entry stack did not have any usable actions.");
            return (null, null);
        }

        private unsafe (bool, GameObject Target) CanUseAction(StackEntry targ, uint actionType)
        {
            if (targ.Target == null || targ.Action == null) return (false, null);

            var unadjustedAction = targ.Action;
            var action = RawActions.GetRow(actionManager->GetAdjustedActionId(targ.Action.RowId));
            if (action == null) return (false, null); // just in case

            var target = targ.Target.GetTarget();
            if (target == null)
            {
                return (!targ.Target.ObjectNeeded, clientState.LocalPlayer);
            }

            // Check if ability is on CD or not (charges are fun!)
            bool abilityOnCoolDownResponse = actionManager->IsActionOffCooldown((ActionType)actionType, action.RowId);
            pluginLog.Verbose("Is {ability} off cooldown? : {response}", action.Name, abilityOnCoolDownResponse);
            if (!abilityOnCoolDownResponse)
            {
                return (false, target);
            }

            var player = clientState.LocalPlayer;
            var target_ptr = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)target.Address;
            if (Configuration.RangeCheck)
            {

                var player_ptr = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)player.Address;

                uint err = ActionManager.GetActionInRangeOrLoS(action.RowId, player_ptr, target_ptr);
                if (action.TargetArea) return (true, target);
                else if (err != 0 && err != 565) return (false, target);
            }

            //Skip actions you cannot use when synced down, excluding role actions that can be used even when syncing down.
            pluginLog.Verbose("is {actionname} a role action?: {answer}", action.Name, action.IsRoleAction);
            if (!action.IsRoleAction)
            {
                pluginLog.Verbose("is {actionName} usable at level: {level} available for player {playername} with {playerlevel}?", action.Name, action.ClassJobLevel, player.Name, player.Level);
                if (action.ClassJobLevel > clientState.LocalPlayer.Level) return (false, target);
            }

            //area of effect spells do not require a target to work.
            pluginLog.Verbose("is {actionname} a area spell/ability? {answer}", action.Name, action.TargetArea);
            if (action.TargetArea) return (true, target);



            //sanity check on spells that can not be used on friendly, hostile or party
            bool selfOnlyTargetAction = !action.CanTargetFriendly && !action.CanTargetHostile && !action.CanTargetParty;
            pluginLog.Verbose("Can {actionname} target: friendly - {friendly}, hostile  - {hostile}, party  - {party}, dead - {dead}, self - {self}", action.Name, action.CanTargetFriendly, action.CanTargetHostile, action.CanTargetParty, action.CanTargetDead, action.CanTargetSelf);
            if (selfOnlyTargetAction)
            {
                pluginLog.Verbose("Can only use this action on player, setting player as target");
                target = clientState.LocalPlayer;
            }

            //handoff to game native code, returns true if the actionManager declares that the action can be used on the target specified
            bool gameCanUseActionResponse = ActionManager.CanUseActionOnTarget(action.RowId, (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)target.Address);
            pluginLog.Verbose("can I use action: {rowid} with name {actionname} on target {targetid} with name {targetname} : {response}", action.RowId.ToString(), action.Name, target.DataId, target.Name, gameCanUseActionResponse);

            return (gameCanUseActionResponse, target);
        }

        public unsafe GameObject GetGuiMoPtr()
        {
            return objectTable.CreateObjectReference((IntPtr)pronounModule -> UiMouseOverTarget);
        }
        public GameObject GetFocusPtr()
        {
            return targetManager.FocusTarget;
        }
        public GameObject GetRegTargPtr()
        {
            return targetManager.Target;
        }
        public GameObject getFieldMo()
        {
            return targetManager.MouseOverTarget;
        }

        public unsafe GameObject GetActorFromPlaceholder(string placeholder)
        {
            return objectTable.CreateObjectReference((IntPtr)pronounModule->ResolvePlaceholder(placeholder, 1, 0));
        }
    }
}