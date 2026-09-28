namespace WasmSharp.TestSuiteRunner.Reports;

/// <summary>
/// リンク不成立となったimportの識別
/// </summary>
/// <param name="Ordinal">importの0始まり宣言番号</param>
/// <param name="ModuleName">importが要求したmodule名</param>
/// <param name="ImportName">importが要求したitem名</param>
/// <param name="ExpectedKind">importが要求した外部要素の種類の列挙名</param>
internal sealed record ImportRecord(
    int Ordinal,
    string? ModuleName,
    string? ImportName,
    string? ExpectedKind
);
