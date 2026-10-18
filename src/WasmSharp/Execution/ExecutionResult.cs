using System.Collections.Immutable;
using WasmSharp.Exceptions;

namespace WasmSharp.Execution;

/// <summary>
/// 命令または関数の正常結果と実行失敗の情報
/// </summary>
internal readonly struct ExecutionResult
{
    /// <summary>
    /// 正常終了時の戻り値
    /// </summary>
    private readonly ImmutableArray<WasmValue> values_;

    /// <summary>
    /// 正常終了、trapまたは実行資源の上限到達の区分
    /// </summary>
    internal ExecutionStatus Status { get; }

    /// <summary>
    /// 正常終了時の戻り値 defaultの実行結果または失敗時は空配列を返す
    /// </summary>
    internal ImmutableArray<WasmValue> Values => values_.IsDefault ? [] : values_;

    /// <summary>
    /// trapの原因 trap以外ではnull
    /// </summary>
    internal WasmTrapReason? TrapReason { get; }

    /// <summary>
    /// 上限に達した実行資源の原因 exhaustion以外ではnull
    /// </summary>
    internal WasmExhaustionReason? ExhaustionReason { get; }

    /// <summary>
    /// 到達した実行資源の上限 未計測のCLRスタック上限またはexhaustion以外ではnull
    /// </summary>
    internal int? Limit { get; }

    /// <summary>
    /// 失敗の診断に用いる関数index 正常終了時と位置を特定できない場合はnull
    /// </summary>
    internal uint? FunctionIndex { get; }

    /// <summary>
    /// 失敗の診断に用いる入力バイナリ上のバイト位置 正常終了時と位置を特定できない場合はnull
    /// </summary>
    internal long? ByteOffset { get; }

    /// <summary>
    /// 失敗の診断に用いるセクションID 正常終了時と位置を特定できない場合はnull
    /// </summary>
    internal byte? SectionId { get; }

    /// <summary>
    /// trapの公開診断の種別 既存結果とtrap以外ではDefault
    /// </summary>
    internal TrapDiagnosticKind TrapDiagnosticKind { get; }

    /// <summary>
    /// trapの診断に用いるテーブル要素添字 指定しない場合はnull
    /// </summary>
    internal uint? TableElementIndex { get; }

    /// <summary>
    /// 実行状態と、その状態に対応する戻り値または失敗情報を保持する
    /// </summary>
    /// <param name="status">実行状態</param>
    /// <param name="values">正常終了時の戻り値</param>
    /// <param name="trapReason">trapの原因</param>
    /// <param name="exhaustionReason">上限に達した実行資源の原因</param>
    /// <param name="limit">到達した実行資源の上限</param>
    /// <param name="functionIndex">失敗が発生した関数のindex</param>
    /// <param name="byteOffset">失敗が発生した入力バイナリ上のバイト位置</param>
    /// <param name="sectionId">失敗が発生したセクションのID</param>
    /// <param name="trapDiagnosticKind">trapの公開診断の種別</param>
    /// <param name="tableElementIndex">trapの診断に用いるテーブル要素添字</param>
    private ExecutionResult(
        ExecutionStatus status,
        ImmutableArray<WasmValue> values,
        WasmTrapReason? trapReason,
        WasmExhaustionReason? exhaustionReason,
        int? limit,
        uint? functionIndex,
        long? byteOffset,
        byte? sectionId = null,
        TrapDiagnosticKind trapDiagnosticKind = TrapDiagnosticKind.Default,
        uint? tableElementIndex = null
    )
    {
        Status = status;
        values_ = values;
        TrapReason = trapReason;
        ExhaustionReason = exhaustionReason;
        Limit = limit;
        FunctionIndex = functionIndex;
        ByteOffset = byteOffset;
        SectionId = sectionId;
        TrapDiagnosticKind = trapDiagnosticKind;
        TableElementIndex = tableElementIndex;
    }

    /// <summary>
    /// 正常終了の結果を構築する
    /// </summary>
    /// <param name="values">戻り値</param>
    /// <returns>指定した戻り値を保持する正常結果</returns>
    internal static ExecutionResult Success(ImmutableArray<WasmValue> values)
    {
        return new ExecutionResult(ExecutionStatus.Success, values, null, null, null, null, null);
    }

    /// <summary>
    /// trapの原因と発生位置を保持する失敗結果を構築する
    /// </summary>
    /// <param name="reason">trapの原因</param>
    /// <param name="functionIndex">trapが発生した関数のindex 関数外ではnull</param>
    /// <param name="byteOffset">trapが発生した入力バイナリ上のバイト位置</param>
    /// <param name="sectionId">trapが発生したセクションのID 特定しない場合はnull</param>
    /// <param name="trapDiagnosticKind">trapの公開診断の種別</param>
    /// <param name="tableElementIndex">trapの診断に用いるテーブル要素添字 指定しない場合はnull</param>
    /// <returns>trapを表す実行結果</returns>
    internal static ExecutionResult Trap(
        WasmTrapReason reason,
        uint? functionIndex,
        long byteOffset,
        byte? sectionId = null,
        TrapDiagnosticKind trapDiagnosticKind = TrapDiagnosticKind.Default,
        uint? tableElementIndex = null
    )
    {
        return new ExecutionResult(
            ExecutionStatus.Trap,
            values: [],
            trapReason: reason,
            exhaustionReason: null,
            limit: null,
            functionIndex: functionIndex,
            byteOffset: byteOffset,
            sectionId: sectionId,
            trapDiagnosticKind: trapDiagnosticKind,
            tableElementIndex: tableElementIndex
        );
    }

    /// <summary>
    /// 実行資源の上限到達の原因、適用上限と発生位置を保持する失敗結果を構築する
    /// </summary>
    /// <param name="reason">上限に達した実行資源の原因</param>
    /// <param name="limit">この実行に適用された正の上限 CLRスタックの未計測上限はnull</param>
    /// <param name="functionIndex">上限に達した定義関数、またはホスト呼び出し元の関数index 特定できない場合はnull</param>
    /// <param name="byteOffset">上限に達した入力バイナリ上のバイト位置 該当位置がなければnull</param>
    /// <returns>実行資源の上限到達を表す実行結果</returns>
    /// <exception cref="ArgumentOutOfRangeException">上限が0以下の場合</exception>
    internal static ExecutionResult Exhaustion(
        WasmExhaustionReason reason,
        int? limit,
        uint? functionIndex,
        long? byteOffset
    )
    {
        if (limit is { } measuredLimit)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(measuredLimit);
        }
        return new ExecutionResult(
            ExecutionStatus.Exhaustion,
            values: [],
            trapReason: null,
            exhaustionReason: reason,
            limit: limit,
            functionIndex: functionIndex,
            byteOffset: byteOffset
        );
    }
}

/// <summary>
/// trapの原因に対応する公開診断の選択
/// </summary>
internal enum TrapDiagnosticKind
{
    /// <summary>
    /// 原因に対応する既定の診断
    /// </summary>
    Default,

    /// <summary>
    /// 間接呼び出しのテーブル範囲外を示す診断
    /// </summary>
    UndefinedElement,
}

/// <summary>
/// 命令または関数の実行状態
/// </summary>
internal enum ExecutionStatus
{
    /// <summary>
    /// 正常終了
    /// </summary>
    Success = 0,

    /// <summary>
    /// Wasmの実行規則によるtrap
    /// </summary>
    Trap,

    /// <summary>
    /// ランタイムが管理する実行資源の上限到達
    /// </summary>
    Exhaustion,
}
