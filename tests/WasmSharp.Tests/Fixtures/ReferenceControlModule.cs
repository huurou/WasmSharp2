namespace WasmSharp.Tests.Fixtures;

internal static class ReferenceControlModule
{
    internal static WasmModule CreateWithNonNullableLocal(string instructions)
    {
        // (param (ref extern)) (result (ref extern)) (local (ref extern))
        return Create(
            [1, 0x60, 1, 0x64, 0x6F, 1, 0x64, 0x6F],
            Convert.FromHexString(instructions),
            locals: [1, 1, 0x64, 0x6F]
        );
    }

    internal static WasmModule Create(byte[] types, byte[] instructions, uint typeIndex = 0, byte[]? locals = null)
    {
        byte[] body = [.. locals ?? [0], .. instructions, 0x0B];
        return WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Section(1, types),
                HostLinkingModuleBinary.Functions(typeIndex),
                HostLinkingModuleBinary.Exports(("f", 0, 0u)),
                HostLinkingModuleBinary.Section(10, [1, .. HostLinkingModuleBinary.Unsigned((uint)body.Length), .. body])
            ),
            WasmSpecVersion.Core3
        );
    }
}
