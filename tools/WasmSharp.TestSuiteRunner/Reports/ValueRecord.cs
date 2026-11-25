namespace WasmSharp.TestSuiteRunner.Reports;

/// <summary>
/// CPUのendianやCLRのアドレスに依存しない実値の記録
/// </summary>
/// <param name="Type">変換済みスクリプトの値型名</param>
internal sealed record ValueRecord(string Type)
{
    /// <summary>
    /// i32/f32/i31refは8桁、i64/f64は16桁の小文字hexによるビット列 i31refの最上位ビットは0で、ほかの型ではnull
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
    /// 参照先オブジェクトの入力内で初出順に割り当てたtoken 参照先を持たない値ではnull
    /// </summary>
    public int? Token { get; init; }

    /// <summary>
    /// ref.hostまたはref.externで指定したホスト値の元番号 ほかの参照はnull
    /// </summary>
    public uint? ExternrefNumber { get; init; }
}
