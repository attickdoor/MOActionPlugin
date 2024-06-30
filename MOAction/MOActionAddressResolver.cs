using Dalamud.Game;
using System;
using System.Runtime.InteropServices;

namespace MOAction
{

    public class MOActionAddressResolver
    {
        public IntPtr GtQueuePatch { get; private set; }

        public byte[] preGtQueuePatchData {get; set;}

        public MOActionAddressResolver(ISigScanner sig, bool enableGroundTargetQueuePatch)
        {
            
            if(enableGroundTargetQueuePatch){
            GtQueuePatch = sig.ScanModule("75 49 44 8B C7");
            }
            else{
                GtQueuePatch = 0;
            }
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
