using Dalamud.Game;
using System;
using System.Runtime.InteropServices;

namespace MOAction
{
    public class MOActionAddressResolver
    {
        public IntPtr SetUiMouseoverEntityId { get; private set; }
        public IntPtr GtQueuePatch { get; private set; }


        public IntPtr PronounModule;
        public IntPtr GetGroupTimer;

        public MOActionAddressResolver(ISigScanner sig)
        {
            SetUiMouseoverEntityId = sig.ScanText("48 89 91 ?? ?? ?? ?? C3 CC CC CC CC CC CC CC CC 48 89 5C 24 ?? 55 56 57 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 48 8D B1 ?? ?? ?? ?? 44 89 44 24 ?? 48 8B EA 48 8B D9 48 8B CE 48 8D 15 ?? ?? ?? ?? 41 B9 ?? ?? ?? ??");

            GtQueuePatch = sig.ScanModule("74 20 81 FD F5 0D 00 00");
            PronounModule = sig.GetStaticAddressFromSig("48 8B 0D ?? ?? ?? ?? 48 8D 05 ?? ?? ?? ?? 48 89 05 ?? ?? ?? ?? 48 85 C9 74 0C", 0);
        }

        [StructLayout(LayoutKind.Explicit, Size = 0x14)]
        public unsafe struct RecastTimer
        {
            [FieldOffset(0x0)] public byte IsActive;
            [FieldOffset(0x4)] public uint ActionID;
            [FieldOffset(0x8)] public float Elapsed;
            [FieldOffset(0xC)] public float Total;
        }

    }
}
