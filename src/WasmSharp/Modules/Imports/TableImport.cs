using WasmSharp.Modules.Definitions;

namespace WasmSharp.Modules.Imports;

/// <summary>
/// 参照型とlimitsを保持するtable import
/// </summary>
/// <param name="ModuleName">要求する提供元のmodule名</param>
/// <param name="Name">要求するtableの名前</param>
/// <param name="Type">要求するtableの参照型・limitsと型記述のバイト位置</param>
/// <param name="ByteOffset">入力バイナリ上のimport宣言のバイト位置</param>
internal sealed record TableImport(
    string ModuleName,
    string Name,
    TableDefinition Type,
    long ByteOffset
) : ModuleImport(ModuleName, Name, ByteOffset)
{
    internal override WasmExternalKind Kind => WasmExternalKind.Table;
}
