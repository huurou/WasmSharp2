namespace WasmSharp.Modules.Imports;

/// <summary>
/// 値型と可変性を保持するglobal import
/// </summary>
/// <param name="ModuleName">要求する提供元のmodule名</param>
/// <param name="Name">要求するglobalの名前</param>
/// <param name="Type">要求するglobalの値型と可変性</param>
/// <param name="ByteOffset">入力バイナリ上のimport宣言のバイト位置</param>
internal sealed record GlobalImport(
    string ModuleName,
    string Name,
    WasmGlobalType Type,
    long ByteOffset
) : ModuleImport(ModuleName, Name, ByteOffset)
{
    internal override WasmExternalKind Kind => WasmExternalKind.Global;
}
