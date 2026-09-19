namespace WasmSharp.Modules.Imports;

/// <summary>
/// 宣言順に保持するimport名、外部要素の種類と入力位置
/// </summary>
/// <param name="ModuleName">要求する提供元のmodule名</param>
/// <param name="Name">要求する外部要素の名前</param>
/// <param name="ByteOffset">入力バイナリ上のimport宣言のバイト位置</param>
internal abstract record ModuleImport(string ModuleName, string Name, long ByteOffset)
{
    internal abstract WasmExternalKind Kind { get; }
}
