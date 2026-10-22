namespace WasmSharp.Benchmarks.Cases;

/// <summary>
/// 入力と期待値を一か所に保持する測定ケースの保存用定義
/// </summary>
/// <param name="AssetSha256">採用したWasmバイナリのSHA-256</param>
/// <param name="InputHash">固定入力全体のSHA-256</param>
/// <param name="ComparisonGroup">同じ入力・期待値・仕事量のケースを対応付ける識別子</param>
internal sealed record BenchmarkCase(
    string CaseId,
    uint CaseRevision,
    string AssetId,
    string AssetSha256,
    MeasurementStage MeasurementStage,
    OperationDefinition OperationDefinition,
    List<WorkloadQuantity> Workload,
    CaseInput Input,
    string InputHash,
    ExpectedResult ExpectedResult,
    StatePolicy StatePolicy,
    string ComparisonGroup
);

/// <summary>
/// 計時対象とする公開操作の区間
/// </summary>
internal enum MeasurementStage
{
    Decode,
    DecodeValidate,
    Instantiate,
    Invoke,
}

/// <summary>
/// 公開操作の回数と状態復元を計時へ含めるかの定義 guestの仕事量とは分けて保持する。
/// </summary>
internal sealed record OperationDefinition(
    string Description,
    uint OperationsPerInvoke,
    bool IncludesStateRestoration
);

/// <summary>
/// 1操作で処理する仕事量 Countは符号なし64ビット整数
/// </summary>
internal sealed record WorkloadQuantity(string Name, ulong Count, WorkloadUnit Unit);

/// <summary>
/// 固定ケースで扱う仕事量の単位
/// </summary>
internal enum WorkloadUnit
{
    Iterations,
    Elements,
    Bytes,
    Functions,
}

/// <summary>
/// 反復間で同じ開始条件を保つ方法
/// </summary>
internal enum StatePolicy
{
    /// <summary>
    /// 固定入力を読み取り、状態を変更しない
    /// </summary>
    ReadOnly,

    /// <summary>
    /// 事前に最終状態へ揃え、1操作後も同じ状態になる
    /// </summary>
    FixedPoint,

    /// <summary>
    /// 前回出力を入力にせず、出力全体を毎回上書きする
    /// </summary>
    OverwriteOutput,

    /// <summary>
    /// 固定バイト列から毎回新しいmoduleを生成する
    /// </summary>
    FreshModule,

    /// <summary>
    /// 検証済みmoduleから毎回新しいinstanceを生成する
    /// </summary>
    FreshInstance,
}
