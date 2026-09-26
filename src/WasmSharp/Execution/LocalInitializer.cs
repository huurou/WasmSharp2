namespace WasmSharp.Execution;

/// <summary>
/// 同じ型で連続する追加localsの個数と初期値
/// </summary>
/// <remarks>関数入口で積む追加localsを個数と初期値でまとめて構築する</remarks>
/// <param name="count">localの個数</param>
/// <param name="value">型に応じたゼロまたはnullの初期値</param>
internal readonly struct LocalInitializer(uint count, WasmValue value)
{
    /// <summary>
    /// 同じ初期値で連続するlocalの個数
    /// </summary>
    internal uint Count { get; } = count;

    /// <summary>
    /// 型に応じたゼロまたはnullの初期値
    /// </summary>
    internal WasmValue Value { get; } = value;
}
