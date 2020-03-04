using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Structs.JobGauge;
using Dalamud.Hooking;
using Serilog;

namespace MOActionPlugin
{
    public class MOAction
    {
        public delegate ulong OnRequestActionDetour(long param_1, uint param_2, ulong param_3, long param_4,
                       uint param_5, uint param_6, int param_7);

        public delegate void OnSetUiMouseoverEntityId(long param1, long param2);

        private readonly IntPtr byteBase;

        private readonly MOActionAddressResolver Address;

        private readonly MOActionConfiguration Configuration;

        private Hook<OnRequestActionDetour> requestActionHook;
        private Hook<OnSetUiMouseoverEntityId> uiMoEntityIdHook;

        private IntPtr fieldMOLocation;

        IntPtr uiMoEntityId = IntPtr.Zero;

        private IntPtr RequestActionAddress;
        private IntPtr UiMOEntityIdAddress;

        public HashSet<ulong> enabledActions { get; private set; }

        public bool IsGuiMOEnabled = false;
        public bool IsFieldMOEnabled = false;

        public MOAction(SigScanner scanner, ClientState clientState, MOActionConfiguration configuration)
        {
            Configuration = configuration;

            Address = new MOActionAddressResolver();
            Address.Setup(scanner);

            byteBase = scanner.Module.BaseAddress;
            this.fieldMOLocation = byteBase + 0x1C8E660;
            RequestActionAddress = byteBase + 0x6cbd40;
            UiMOEntityIdAddress = byteBase + 0x623100;

            Log.Verbose("===== M O A C T I O N =====");
            Log.Verbose("RequestAction address {IsIconReplaceable}", Address.RequestAction);
            Log.Verbose("SetUiMouseoverEntityId address {SetUiMouseoverEntityId}", Address.SetUiMouseoverEntityId);

            requestActionHook = new Hook<OnRequestActionDetour>(Address.RequestAction, new OnRequestActionDetour(HandleRequestAction), this);
            uiMoEntityIdHook = new Hook<OnSetUiMouseoverEntityId>(Address.SetUiMouseoverEntityId, new OnSetUiMouseoverEntityId(HandleUiMoEntityId), this);

            enabledActions = new HashSet<ulong>();
        }

        public void EnableAction(ulong ActionID)
        {
            if (enabledActions.Contains(ActionID)) return;
            enabledActions.Add(ActionID);
        }

        public void RemoveAction(ulong ActionID)
        {
            if (enabledActions.Contains(ActionID)) enabledActions.Remove(ActionID);
        }

        public void Enable()
        {
            requestActionHook.Enable();
            uiMoEntityIdHook.Enable();
        }

        public void Dispose()
        {
            requestActionHook.Dispose();
            uiMoEntityIdHook.Dispose();
        }

        private void HandleUiMoEntityId(long param1, long param2)
        {
            uiMoEntityId = (IntPtr)param2;
            uiMoEntityIdHook.Original(param1, param2);
        }

        private unsafe ulong HandleRequestAction(long param_1, uint param_2, ulong param_3, long param_4,
                       uint param_5, uint param_6, int param_7)
        {
            Log.Verbose($"RequestAction: {param_3} {param_4}");

            if (enabledActions.Count() == 0 || !enabledActions.Contains(param_3))
            {
                return this.requestActionHook.Original(param_1, param_2, param_3, param_4, param_5, param_6, param_7);
            }

            if (IsGuiMOEnabled && (uiMoEntityId != IntPtr.Zero || (int) uiMoEntityId != 0))
            {
                uint entityId = (uint)Marshal.ReadInt32(uiMoEntityId + 0x74);
                return requestActionHook.Original(param_1, param_2, param_3, entityId, param_5, param_6, param_7);
            }

            if (IsFieldMOEnabled && ((uint)Marshal.ReadInt32(fieldMOLocation) != 0xe0000000))
            {
                return requestActionHook.Original(param_1, param_2, param_3, Marshal.ReadInt32(fieldMOLocation), param_5, param_6, param_7);
            }

            return this.requestActionHook.Original(param_1, param_2, param_3, param_4, param_5, param_6, param_7);
        }
    }
}