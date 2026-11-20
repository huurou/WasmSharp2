namespace WasmSharp.Tests.Fixtures;

internal static class MemoryGrowModuleBinary
{
    public static byte[] CreateWithNonMinimalZeroIndex()
    {
        return HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [0x7F])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Memories((1, 2)),
            HostLinkingModuleBinary.Exports(("grow", 0, 0), ("memory", 2, 0)),
            HostLinkingModuleBinary.Code(([], [0x41, 1, 0x40, 0x80, 0x00, 0x0B]))
        );
    }
}
