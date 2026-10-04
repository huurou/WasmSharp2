namespace WasmSharp.Modules;

/// <summary>
/// 通常解析の限定範囲と、境界失敗後の診断用読取りを区別する
/// </summary>
internal enum ModuleReadMode
{
    /// <summary>
    /// 宣言範囲内だけを取得する通常の読取り
    /// </summary>
    Bounded,

    /// <summary>
    /// 物理入力内で診断を選び、宣言終端を別に検査する読取り
    /// </summary>
    Diagnostic,
}
