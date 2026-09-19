using WasmSharp.Modules.Definitions;

namespace WasmSharp.Modules.Imports;

/// <summary>
/// limitsを保持するmemory import
/// </summary>
/// <param name="ModuleName">要求する提供元のmodule名</param>
/// <param name="Name">要求するmemoryの名前</param>
/// <param name="Type">要求するmemoryのlimitsと型記述のバイト位置</param>
/// <param name="ByteOffset">入力バイナリ上のimport宣言のバイト位置</param>
internal sealed record MemoryImport(
    string ModuleName,
    string Name,
    MemoryDefinition Type,
    long ByteOffset
) : ModuleImport(ModuleName, Name, ByteOffset)
{
    internal override WasmExternalKind Kind => WasmExternalKind.Memory;
}
