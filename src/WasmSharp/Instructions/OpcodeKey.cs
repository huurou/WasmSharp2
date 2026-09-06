namespace WasmSharp.Instructions;

/// <summary>
/// 通常命令またはprefix付き拡張命令の識別子
/// </summary>
/// <param name="Prefix">通常命令では0、拡張命令ではprefix</param>
/// <param name="Code">命令番号</param>
public readonly record struct OpcodeKey(byte Prefix, uint Code);
