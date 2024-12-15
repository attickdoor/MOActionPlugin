using Dalamud.Game;

namespace MOAction;

public class MOActionAddressResolver
{
    public nint GtQueuePatch { get; private set; }
    public byte[] PreGtQueuePatchData { get; set; }

    public MOActionAddressResolver(ISigScanner sig)
    {
        GtQueuePatch = sig.ScanModule("75 49 44 8B C7");
    }
}