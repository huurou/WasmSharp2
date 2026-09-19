namespace WasmSharp.Modules.Definitions;

/// <summary>
/// startの未検証の関数indexと入力位置
/// </summary>
/// <param name="FunctionIndex">startで実行する関数の未検証のindex</param>
/// <param name="ByteOffset">入力バイナリ上のstart関数indexのバイト位置</param>
internal readonly record struct StartDefinition(uint FunctionIndex, long ByteOffset);
