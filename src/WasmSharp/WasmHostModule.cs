namespace WasmSharp;

/// <summary>
/// ホスト関数の処理を定義するデリゲート
/// </summary>
/// <param name="arguments">ホスト関数に渡すvalueの列</param>
/// <returns>ホスト関数が返すvalueの列</returns>
public delegate Span<WasmValue> WasmHostCallback(ReadOnlySpan<WasmValue> arguments);

/// <summary>
/// importへ提供する名前付きの関数やリソースをまとめたホストモジュール
/// </summary>
public sealed class WasmHostModule { }
