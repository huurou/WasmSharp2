namespace WasmSharp.Benchmarks.Cases;

/// <summary>
/// 計時外で照合する固定期待値 戻り値は個数と順序を保ち、資源の期待状態も同じケースに保持する。
/// </summary>
internal sealed record ExpectedResult(
    List<NumericValue> Values,
    List<ArrayDigest> Arrays,
    List<GlobalExpectation> Globals,
    List<TableExpectation> Tables
);

/// <summary>
/// memory内の出力配列の固定digest Lengthは要素数で、hashはlittle-endianの全要素バイト列から求める。
/// </summary>
internal sealed record ArrayDigest(
    string MemoryExport,
    uint Offset,
    ArrayElementType ElementType,
    uint Length,
    string HashAlgorithm,
    string Digest
);

/// <summary>
/// start結果など、数値globalの固定期待値
/// </summary>
internal sealed record GlobalExpectation(string ExportName, NumericValue Value);

/// <summary>
/// tableの要素数と、指定位置の関数実体への参照の期待値
/// </summary>
internal sealed record TableExpectation(
    string ExportName,
    uint Length,
    List<TableElementExpectation> Elements
);

/// <summary>
/// 同じinstanceの関数exportを使って照合するtable要素 FunctionExportがnullならnull参照を期待する。
/// </summary>
internal sealed record TableElementExpectation(uint Index, string? FunctionExport);
