namespace WasmSharp.TestSuiteRunner.Reports;

/// <summary>
/// 公開例外が報告した処理段階と入力上の位置
/// </summary>
/// <param name="Stage">ランタイムの処理段階の列挙名</param>
/// <param name="ByteOffset">Decode開始位置を0としたbyte offset</param>
/// <param name="FunctionIndex">失敗した関数のindex</param>
/// <param name="SectionId">失敗したsectionのID</param>
internal sealed record FailureLocationRecord(
    string Stage,
    long? ByteOffset,
    uint? FunctionIndex,
    byte? SectionId
);
