namespace WasmSharp.Modules.Imports;

/// <summary>
/// 解決前の型indexを保持する関数import
/// </summary>
/// <param name="ModuleName">要求する提供元のmodule名</param>
/// <param name="Name">要求する関数の名前</param>
/// <param name="TypeIndex">要求する関数型の未検証のindex</param>
/// <param name="ByteOffset">入力バイナリ上のimport宣言のバイト位置</param>
internal sealed record FunctionImport(
    string ModuleName,
    string Name,
    uint TypeIndex,
    long ByteOffset
) : ModuleImport(ModuleName, Name, ByteOffset)
{
    internal override WasmExternalKind Kind => WasmExternalKind.Function;
}
