namespace WasmSharp.TestSuiteRunner.Reports;

/// <summary>
/// CPUのendianやCLRのアドレスに依存しない実値の記録
/// </summary>
/// <param name="Type">WABTの値型名</param>
internal sealed record ValueRecord(string Type)
{
    /// <summary>
    /// i32/f32は8桁、i64/f64は16桁の小文字hexによるビット列 scalar以外ではnull
    /// </summary>
    public string? Bits { get; init; }

    /// <summary>
    /// v128の下位64ビットを表す16桁の小文字hex v128以外ではnull
    /// </summary>
    public string? Low64 { get; init; }

    /// <summary>
    /// v128の上位64ビットを表す16桁の小文字hex v128以外ではnull
    /// </summary>
    public string? High64 { get; init; }

    /// <summary>
    /// 参照がnullかどうか 参照以外ではnull
    /// </summary>
    public bool? IsNull { get; init; }

    /// <summary>
    /// 非null参照の入力内で初出順に割り当てたtoken 参照以外とnull参照ではnull
    /// </summary>
    public int? Token { get; init; }

    /// <summary>
    /// ランナーが割り当てたexternrefの元番号 既知のexternref以外ではnull
    /// </summary>
    public uint? ExternrefNumber { get; init; }
}
