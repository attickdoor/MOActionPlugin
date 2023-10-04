using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Game;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Hooking;
using Dalamud.Logging;
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
        public delegate bool OnRequestActionDetour(long param_1, byte param_2, ulong param_3, long param_4,
                       uint param_5, uint param_6, uint param_7, long param_8);

        [UnmanagedFunctionPointer(CallingConvention.ThisCall, CharSet = CharSet.Ansi)]
        private delegate ulong ResolvePlaceholderActor(long param1, string param2, byte param3, byte param4);

        public delegate void OnSetUiMouseoverEntityId(long param1, long param2);

        private readonly MOActionAddressResolver Address;
        private MOActionConfiguration Configuration;

        private Hook<OnRequestActionDetour> requestActionHook;
        private Hook<OnSetUiMouseoverEntityId> uiMoEntityIdHook;

        public unsafe delegate RecastTimer* GetGroupTimerDelegate(void* @this, int cooldownGroup);
        private GetGroupTimerDelegate getGroupTimer;

        public List<MoActionStack> Stacks { get; set; }
        private Lumina.Excel.ExcelSheet<Lumina.Excel.GeneratedSheets.Action> RawActions;

        public IntPtr uiMoEntityId = IntPtr.Zero;

        private HashSet<uint> UnorthodoxFriendly;
        private HashSet<uint> UnorthodoxHostile;

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

        private unsafe PronounModule* PM;
        private unsafe ActionManager* AM;
        private readonly int IdOffset = (int)Marshal.OffsetOf<FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject>("ObjectID");

        public MOAction(ISigScanner scanner,
                        IClientState clientstate,
                        IDataManager datamanager, 
                        ITargetManager targetmanager, 
                        IObjectTable objects, 
                        IKeyState keystate,
                        IGameGui gamegui,
                        IGameInteropProvider hookprovider,
                        IPluginLog pluginLog
                        )
        {
            Address = new(scanner);
            clientstate.Login += LoadClientModules;
            clientstate.Logout += ClearClientModules;
            if (clientstate.IsLoggedIn){
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

            Stacks = new();

            pluginLog.Info("===== M O A C T I O N =====");
            pluginLog.Debug("SetUiMouseoverEntityId address {SetUiMouseoverEntityId}", Address.SetUiMouseoverEntityId);
            
            uiMoEntityIdHook = hookprovider.HookFromAddress(Address.SetUiMouseoverEntityId, new OnSetUiMouseoverEntityId(HandleUiMoEntityId));

            enabledActions = new();
            UnorthodoxFriendly = new(){
                17055,
                7443
            };
            UnorthodoxHostile = new()
            {
                3575
            };
        }

        public void SetConfig(MOActionConfiguration config)
        {
            Configuration = config;
        }

        private unsafe void LoadClientModules()
        {
            try
            {
                var framework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance();
                var uiModule = framework->GetUiModule();
                PM = uiModule->GetPronounModule();
                AM = ActionManager.Instance();
                getGroupTimer = Marshal.GetDelegateForFunctionPointer<GetGroupTimerDelegate>((IntPtr)ActionManager.Addresses.GetRecastGroupDetail.Value);
            }
            catch (Exception e) {
                pluginLog.Warning(e.Message);
                pluginLog.Warning(e.StackTrace);
                pluginLog.Warning(e.InnerException.ToString());
            }
        }

        private unsafe void ClearClientModules()
        {
            PM = null;
            AM = null;
        }

        private unsafe void HookUseAction()
        {
            SafeMemory.WriteBytes(Address.GtQueuePatch, new byte[] { 0xEB });
            requestActionHook = hookprovider.HookFromAddress((IntPtr)ActionManager.Addresses.UseAction.Value, new OnRequestActionDetour(HandleRequestAction));
            requestActionHook.Enable();
        }

        public void Enable()
        {
            uiMoEntityIdHook.Enable();

            HookUseAction();
        }

        public void Dispose()
        {
            if (requestActionHook.IsEnabled)
            {
                requestActionHook.Dispose();
                uiMoEntityIdHook.Dispose();
                
                SafeMemory.WriteBytes(Address.GtQueuePatch, new byte[] { 0x74 });
            }
        }

        public unsafe RecastTimer* GetGroupRecastTimer(int group)
        {
            return group < 1 ? null : getGroupTimer(AM, group - 1);
        }

        private void HandleUiMoEntityId(long param1, long param2)
        {
            uiMoEntityId = (IntPtr)param2;
            uiMoEntityIdHook.Original(param1, param2);
        }

        private unsafe bool HandleRequestAction(long param_1, byte actionType, ulong actionID, long param_4,
                       uint param_5, uint param_6, uint param_7, long param_8)
        {
            // Only care about "real" actions. Not doing anything dodgy, except for GT.
            if (actionType != 1)
            {
                return requestActionHook.Original(param_1, actionType, actionID, param_4, param_5, param_6, param_7, param_8);
            }
            var (action, target) = GetActionTarget((uint)actionID, actionType);
            
            if (action == null) return requestActionHook.Original(param_1, actionType, actionID, param_4, param_5, param_6, param_7, param_8);

            // Earthly Star is the only GT that changes to a different action.
            if (action.RowId == 7439 && clientState.LocalPlayer.StatusList.Any(x => x.StatusId == 1248 || x.StatusId == 1224))
                return requestActionHook.Original(param_1, actionType, actionID, param_4, param_5, param_6, param_7, param_8);
            

            long objectId = target == null ? 0xE0000000 : target.ObjectId;

            bool ret = requestActionHook.Original(param_1, actionType, action.RowId, objectId, param_5, param_6, param_7, param_8);

            // Enqueue GT action
            if (action.TargetArea)
            {
                *(long*)((IntPtr)AM + 0x98) = objectId;
                *(byte*)((IntPtr)AM + 0xB8) = 1;
            }
            return ret;
        }

        private unsafe (Lumina.Excel.GeneratedSheets.Action action, GameObject target) GetActionTarget(uint ActionID, uint ActionType)
        {
            var action = RawActions.GetRow(ActionID);
            
            uint adjusted = AM->GetAdjustedActionId(ActionID);

            if (action == null) return (null, null);
            var applicableActions = Stacks.Where(entry => entry.BaseAction.RowId == action.RowId || entry.BaseAction.RowId == adjusted || AM->GetAdjustedActionId(entry.BaseAction.RowId) == adjusted);
            
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
                return (null, null);
            }
            foreach (StackEntry entry in stackToUse.Entries)
            {
                if (CanUseAction(entry, ActionType))
                {
                    if (!entry.Action.CanTargetFriendly && !entry.Action.CanTargetHostile && !entry.Action.CanTargetParty && !entry.Action.CanTargetDead) return (entry.Action, clientState.LocalPlayer);
                    return (entry.Action, entry.Target.getPtr());
                }
            }
            return (null, null);
        }

        private unsafe int AvailableCharges(Lumina.Excel.GeneratedSheets.Action action)
        {
            RecastTimer* timer;
            if (action.CooldownGroup == 58)
                timer = GetGroupRecastTimer(action.AdditionalCooldownGroup);
            else
                timer = GetGroupRecastTimer(action.CooldownGroup);
            if (action.MaxCharges == 0) return timer->IsActive ^ 1;
            return (int)((action.MaxCharges+1) * (timer->Elapsed / timer->Total));
        }

        private unsafe bool CanUseAction(StackEntry targ, uint actionType)
        {
            if (targ.Target == null || targ.Action == null) return false;
            
            var action = targ.Action;
            action = RawActions.GetRow(AM->GetAdjustedActionId(targ.Action.RowId));
            if (action == null) return false; // just in case
            var target = targ.Target.GetTarget();
            if (target == null)
            {
                return !targ.Target.ObjectNeeded;
            }
         
            // Check if ability is on CD or not (charges are fun!)
            unsafe
            {
                if (action.ActionCategory.Value.RowId == (uint)ActionType.Ability && action.MaxCharges == 0)
                {
                    if (AM->IsRecastTimerActive((ActionType)actionType, action.RowId))
                    {
                        return false;
                    }
                }
                else if (action.MaxCharges > 0 || (action.CooldownGroup != 0 && action.AdditionalCooldownGroup != 0))
                {
                    if (AvailableCharges(action) == 0) return false;
                }
            }
            if (Configuration.RangeCheck)
            { 
                var player = clientState.LocalPlayer;
                var player_ptr = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)player.Address;
                var target_ptr = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)target.Address;
                
                uint err = ActionManager.GetActionInRangeOrLoS(action.RowId, player_ptr, target_ptr);
                if (action.TargetArea) return true;
                else if (err != 0 && err != 565) return false;
            }
            if (target.ObjectKind == ObjectKind.Player) return action.CanTargetFriendly ||
                    action.CanTargetParty ||
                    action.CanTargetSelf ||
                    action.TargetArea ||
                    UnorthodoxFriendly.Contains((uint)action.RowId);
            if (target.ObjectKind == ObjectKind.BattleNpc)
            {
                BattleNpc b = (BattleNpc)target;
                if (!(b.BattleNpcKind == BattleNpcSubKind.Enemy || b.BattleNpcKind == BattleNpcSubKind.BattleNpcPart)){
                    return action.CanTargetFriendly ||
                        action.CanTargetParty ||
                        action.CanTargetSelf ||
                        action.TargetArea ||
                        UnorthodoxFriendly.Contains((uint)action.RowId);
                }
            }
            return action.CanTargetHostile ||
                action.TargetArea ||
                UnorthodoxHostile.Contains((uint)action.RowId);
        }

        public GameObject GetGuiMoPtr()
        {
            return objectTable.CreateObjectReference(uiMoEntityId);
        }
        public GameObject GetFocusPtr()
        {
            return targetManager.FocusTarget;
        }
        public GameObject GetRegTargPtr()
        {
            return targetManager.Target;
        }
        public GameObject NewFieldMo() => targetManager.MouseOverTarget;

        public unsafe GameObject GetActorFromPlaceholder(string placeholder)
        {
            return objectTable.CreateObjectReference((IntPtr)PM->ResolvePlaceholder(placeholder, 1, 0));
        }
    }
}