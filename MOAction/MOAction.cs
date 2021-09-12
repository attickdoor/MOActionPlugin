using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using Dalamud.Game.ClientState.Keys;
using MOAction.Configuration;
using Vector3 = System.Numerics.Vector3;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Dalamud.Game.Gui;
using ImGuiNET;
using Dalamud.Game.ClientState.Statuses;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace MOAction
{
    public class MOAction
    {
        public delegate bool OnRequestActionDetour(long param_1, uint param_2, ulong param_3, long param_4,
                       uint param_5, uint param_6, int param_7);

        [UnmanagedFunctionPointer(CallingConvention.ThisCall, CharSet = CharSet.Ansi)]
        private delegate ulong ResolvePlaceholderActor(long param1, string param2, byte param3, byte param4);
        private ResolvePlaceholderActor PlaceholderResolver;
        private Hook<RequestActionLocationDelegate> reqlochook;

        private delegate void PostRequest(IntPtr param1, long param2);
        private PostRequest PostRequestResolver;

        [return: MarshalAs(UnmanagedType.U1)]
        public delegate bool RequestActionLocationDelegate(IntPtr actionMgr, uint type, uint id, uint targetId, ref Vector3 location, byte zero);
        private RequestActionLocationDelegate RALDelegate;

        public delegate void OnSetUiMouseoverEntityId(long param1, long param2);

        private readonly MOActionAddressResolver Address;
        private MOActionConfiguration Configuration;

        private Hook<OnRequestActionDetour> requestActionHook;
        private Hook<OnSetUiMouseoverEntityId> uiMoEntityIdHook;

        public List<MoActionStack> Stacks { get; set; }
        private DalamudPluginInterface pluginInterface;
        private IEnumerable<Lumina.Excel.GeneratedSheets.Action> RawActions;

        public IntPtr fieldMOLocation;
        public IntPtr focusTargLocation;
        public IntPtr regularTargLocation;
        public IntPtr uiMoEntityId = IntPtr.Zero;
        //public IntPtr MagicStructInfo = IntPtr.Zero;
        //private IntPtr MagicUiObject;
        private HashSet<uint> UnorthodoxFriendly;
        private HashSet<uint> UnorthodoxHostile;

        public HashSet<ulong> enabledActions;

        public bool IsGuiMOEnabled = false;
        public bool IsFieldMOEnabled = false;

        private IntPtr thing;

        public DataManager dataManager;
        public TargetManager targetManager;
        public ClientState clientState;
        public KeyState keyState;
        public static ObjectTable objectTable;
        private GameGui gameGui;

        private unsafe PronounModule* PM;
        private unsafe ActionManager* AM;
        private readonly int IdOffset = (int)Marshal.OffsetOf<FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject>("ObjectID");

        public MOAction(SigScanner scanner, ClientState clientstate,
                        DataManager datamanager, TargetManager targetmanager, ObjectTable objects, KeyState keystate, GameGui gamegui
                        )
        {
            clientstate.Login += LoadClientModules;
            clientstate.Logout += ClearClientModules;

            fieldMOLocation = scanner.GetStaticAddressFromSig("E8 ?? ?? ?? ?? 83 BF ?? ?? ?? ?? ?? 0F 84 ?? ?? ?? ?? 48 8D 4C 24 ??", 0x283);
            focusTargLocation = scanner.GetStaticAddressFromSig("48 8B 0D ?? ?? ?? ?? 48 89 5C 24 ?? BB ?? ?? ?? ?? 48 89 7C 24 ??", 0);
            regularTargLocation = scanner.GetStaticAddressFromSig("F3 0F 11 05 ?? ?? ?? ?? EB 27", 0) + 0x4;
            //MagicStructInfo = scanner.GetStaticAddressFromSig("48 8B 0D ?? ?? ?? ?? 48 8D 05 ?? ?? ?? ?? 48 89 05 ?? ?? ?? ?? 48 85 C9 74 0C", 0);
            
            Address = new();
            Address.Setup(scanner);

            dataManager = datamanager;

            //pluginInterface = plugin;
            RawActions = dataManager.GetExcelSheet<Lumina.Excel.GeneratedSheets.Action>();

            targetManager = targetmanager;
            clientState = clientstate;
            objectTable = objects;
            keyState = keystate;
            gameGui = gamegui;

            RALDelegate = Marshal.GetDelegateForFunctionPointer<RequestActionLocationDelegate>(Address.RequestActionLocation);
            PostRequestResolver = Marshal.GetDelegateForFunctionPointer<PostRequest>(Address.PostRequest);
            thing = scanner.Module.BaseAddress + 0x1d8e490;

            Stacks = new();

            PluginLog.Log("===== M O A C T I O N =====");
            PluginLog.Log("RequestAction address {IsIconReplaceable}", Address.RequestAction);
            PluginLog.Log("SetUiMouseoverEntityId address {SetUiMouseoverEntityId}", Address.SetUiMouseoverEntityId);

            reqlochook = new Hook<RequestActionLocationDelegate>(Address.RequestActionLocation, new RequestActionLocationDelegate(ReqLocDetour));
            requestActionHook = new Hook<OnRequestActionDetour>(Address.RequestAction, new OnRequestActionDetour(HandleRequestAction));
            uiMoEntityIdHook = new Hook<OnSetUiMouseoverEntityId>(Address.SetUiMouseoverEntityId, new OnSetUiMouseoverEntityId(HandleUiMoEntityId));
            PlaceholderResolver = Marshal.GetDelegateForFunctionPointer<ResolvePlaceholderActor>(Address.ResolvePlaceholderText);
            //MagicUiObject = IntPtr.Zero;

            enabledActions = new();
            UnorthodoxFriendly = new();
            UnorthodoxHostile = new();
            UnorthodoxHostile.Add(3575);
            UnorthodoxFriendly.Add(17055);
            UnorthodoxFriendly.Add(7443);
        }

        public void SetConfig(MOActionConfiguration config)
        {
            Configuration = config;
        }

        private unsafe void LoadClientModules(object sender, EventArgs args)
        {
            var framework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance();
            var uiModule = framework->GetUiModule();
            PM = uiModule->GetPronounModule();
            AM = ActionManager.Instance();
        }

        private unsafe void ClearClientModules(object sender, EventArgs args)
        {
            PM = null;
            AM = null;
        }

        public void Enable()
        {
            requestActionHook.Enable();
            uiMoEntityIdHook.Enable();
            //reqlochook.Enable();
        }

        public void Dispose()
        {
            requestActionHook.Dispose();
            uiMoEntityIdHook.Dispose();
            //reqlochook.Dispose();
        }

        private void HandleUiMoEntityId(long param1, long param2)
        {
            //Log.Information("UI MO: {0}", param2);
            uiMoEntityId = (IntPtr)param2;
            uiMoEntityIdHook.Original(param1, param2);
        }

        private bool ReqLocDetour(IntPtr actionMgr, uint type, uint id, uint targetId, ref Vector3 location, byte zero)
        {
            PluginLog.Log($"args dump for Req loc detour: {type}, {id}, {targetId}, {location}");
            return reqlochook.Original(actionMgr, type, id, targetId, ref location, zero);
        }

        private bool HandleRequestAction(long param_1, uint param_2, ulong param_3, long param_4,
                       uint param_5, uint param_6, int param_7)
        {
            var (action, target) = GetActionTarget((uint)param_3, param_2);
            void EnqueueGroundTarget()
            {
                IntPtr self = (IntPtr)param_1;

                Marshal.WriteInt32(self + 128, (int)param_6);
                Marshal.WriteInt32(self + 132, (int)param_7);
                Marshal.WriteByte(self + 104, 1);
                Marshal.WriteInt32(self + 108, (int)param_2);
                Marshal.WriteInt32(self + 112, (int)action.RowId);
                Marshal.WriteInt64(self + 120, param_4);
            }
            
            if (action == null) return requestActionHook.Original(param_1, param_2, param_3, param_4, param_5, param_6, param_7);
            if (action.Name == "Earthly Star" && clientState.LocalPlayer.StatusList.Any(x => x.StatusId == 1248 || x.StatusId == 1224))
                return requestActionHook.Original(param_1, param_2, param_3, param_4, param_5, param_6, param_7);
            // Ground target "at my cursor"
            if (action != null && target == null)
            {
                Vector3 pos;
                var mousePos = gameGui.ScreenToWorld(ImGui.GetMousePos(), out pos);
                if (Configuration.MouseClamp)
                {
                    var playerpos = clientState.LocalPlayer.Position;
                    var distance = Vector3.Distance(playerpos, pos);
                    if (distance > action.Range + 1)
                    {
                        pos = GetClampedGroundCoords(playerpos, pos, action.Range+1);
                    }
                     
                }
                EnqueueGroundTarget();
                bool returnval = RALDelegate((IntPtr)param_1, param_2, action.RowId, (uint)param_4, ref pos, 0);
                return returnval;

            }

            if (action != null && target != null)
            {
                // ground target at non-mouse
                if (action.CastType == 7) {

                    var targpos = target.Position;
                    if (Configuration.OtherGroundClamp)
                    {
                        var playerpos = clientState.LocalPlayer.Position;
                        var distance = Vector3.Distance(playerpos, targpos);
                        if (distance > action.Range + 1)
                        {
                            targpos = GetClampedGroundCoords(playerpos, targpos, action.Range + 1);
                        }
                    }
                    EnqueueGroundTarget();
                    bool returnval = RALDelegate((IntPtr)param_1, param_2, action.RowId, (uint)param_4, ref targpos, 0);
                    return returnval;
                }
                return requestActionHook.Original(param_1, param_2, action.RowId, target.ObjectId, param_5, param_6, param_7);
            }
            return requestActionHook.Original(param_1, param_2, param_3, param_4, param_5, param_6, param_7);
        }

        private Vector3 GetClampedGroundCoords(Vector3 self, Vector3 dest, int range)
        {
            Vector2 selfv2 = new Vector2(self.X, self.Z);

            Vector2 destv2 = new Vector2(dest.X, dest.Z);
            Vector2 normal = Vector2.Normalize(destv2 - selfv2);
            Vector2 finalPos = selfv2 + (normal * (range + 1));
            return new Vector3(finalPos.X, dest.Y, finalPos.Y);
        }

        private (Lumina.Excel.GeneratedSheets.Action action, GameObject target) GetActionTarget(uint ActionID, uint ActionType)
        {
            var action = RawActions.FirstOrDefault(x => x.RowId == ActionID);
            if (action == default) return (null, null);
            //var action = RawActions.First(x => x.RowId == ActionID);
            var applicableActions = Stacks.Where(entry => entry.BaseAction == action);
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
                    if (!entry.Action.CanTargetFriendly && !entry.Action.CanTargetHostile) return (entry.Action, clientState.LocalPlayer);
                    return (entry.Action, entry.Target.getPtr());
                }
            }
            /*
            if (Stacks.ContainsKey(ActionID))
            {
                List<StackEntry> stack = Stacks[ActionID];
                foreach (StackEntry t in stack)
                {
                    if (CanUseAction(t)) return (t.actionID, t.target.GetTargetActorId());
                }
            }*/
            return (null, null);
        }

        private bool CanUseAction(StackEntry targ, uint ActionType)
        {
            if (targ.Target == null || targ.Action == null) return false;
            
            var action = targ.Action;
            var target = targ.Target.GetTargetActorId();

            // ground target "at my mouse cursor"
            if (!targ.Target.ObjectNeeded)
            {
                return true;
            }
            
            foreach (GameObject a in objectTable)            
            {
                //var a = clientState.Actors[i];
                if (a != null && a.ObjectId == target)
                {
                    unsafe
                    {
                        if (AM->IsRecastTimerActive((ActionType)ActionType, action.RowId)) return false;
                    }
                    if (Configuration.RangeCheck)
                    {
                        if (UnorthodoxFriendly.Contains((uint)action.RowId))
                        {
                            if (a.YalmDistanceX > 30) return false;
                        }
                        else if ((byte)action.Range < a.YalmDistanceX) return false;
                    }
                    if (a.ObjectKind == ObjectKind.Player) return action.CanTargetFriendly || action.CanTargetParty 
                            || action.CanTargetSelf
                            || action.RowId == 17055 || action.RowId == 7443;
                    if (a.ObjectKind == ObjectKind.BattleNpc)
                    {
                        BattleNpc b = (BattleNpc)a;
                        if (b.BattleNpcKind != BattleNpcSubKind.Enemy) return action.CanTargetFriendly || action.CanTargetParty
                                || action.CanTargetSelf
                                || UnorthodoxFriendly.Contains((uint)action.RowId);
                    }
                    return action.CanTargetHostile || UnorthodoxHostile.Contains((uint)action.RowId);
                }
            }
            return false;
        }

        public GameObject GetGuiMoPtr()
        {
            return objectTable.CreateObjectReference(uiMoEntityId);
        }
        public uint GetFieldMoPtr() => (uint)Marshal.ReadInt32(fieldMOLocation);
        public GameObject GetFocusPtr()
        {
            return objectTable.CreateObjectReference(Marshal.ReadIntPtr(focusTargLocation));
        }
        public GameObject GetRegTargPtr()
        {
            return objectTable.CreateObjectReference(regularTargLocation - IdOffset);
        }
        public GameObject NewFieldMo() => targetManager.MouseOverTarget;

        public unsafe GameObject GetActorFromPlaceholder(string placeholder)
        {
            return objectTable.CreateObjectReference((IntPtr)PM->ResolvePlaceholder(placeholder, 1, 0));
        }
    }
}