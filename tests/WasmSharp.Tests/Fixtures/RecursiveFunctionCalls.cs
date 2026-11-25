namespace WasmSharp.Tests.Fixtures;

internal static class RecursiveFunctionCalls
{
    internal static byte[] CallerTypes()
    {
        // 型番号は0が空の関数型、1が$base、2が$sub、3が(ref $sub) -> (ref null $base)を表す。
        byte[] caller = [0x60, 1, 0x64, 2, 1, 0x63, 1];
        return HostLinkingModuleBinary.Section(1, [4, 0x60, 0, 0, .. TypeDeclarations(1), .. caller]);
    }

    internal static WasmInstance CreateProvider(WasmExecutionContext context)
    {
        // 提供する$baseと$subを型2と型3に置き、いずれも利用側が要求する$baseの型番号1と異なるようにする。
        byte[] precedingTypes = [0x60, 0, 0, 0x60, 1, 0x7F, 0];
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(1, [4, .. precedingTypes, .. TypeDeclarations(2)]),
            HostLinkingModuleBinary.Functions(2, 3),
            HostLinkingModuleBinary.Exports(("base", 0, 0), ("sub", 0, 1)),
            HostLinkingModuleBinary.Code(([], [0x20, 0, 0x0B]), ([], [0x20, 0, 0x0B]))
        );
        return WasmModule.Decode(binary, WasmSpecVersion.Core3).Validate().Instantiate(context, []);
    }

    private static byte[] TypeDeclarations(byte baseTypeIndex)
    {
        // $baseの引数と結果はどちらも(ref null $base)で、$subは同じシグネチャを持つ。
        byte[] signature = [0x60, 1, 0x63, baseTypeIndex, 1, 0x63, baseTypeIndex];
        return [0x50, 0, .. signature, 0x4F, 1, baseTypeIndex, .. signature];
    }
}
