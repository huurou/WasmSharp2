using System.Collections.Immutable;
using WasmSharp.Exceptions;

namespace WasmSharp.Execution;

/// <summary>
/// 命令または関数の正常結果と実行失敗の情報
/// </summary>
internal readonly struct ExecutionResult
{
    /// <summary>
    /// 正常終了時の戻り値。未指定の場合はdefaultを保持する
    /// </summary>
    private readonly ImmutableArray<WasmValue> values_;

    /// <summary>
    /// 正常終了、trapまたは実行資源の上限到達の区分
    /// </summary>
    internal ExecutionStatus Status { get; }

    /// <summary>
    /// 正常終了時の戻り値。未指定または失敗時は空配列を返す
    /// </summary>
    internal ImmutableArray<WasmValue> Values => values_.IsDefault ? [] : values_;

    /// <summary>
    /// trapの原因。trap以外ではnull
    /// </summary>
    internal WasmTrapReason? TrapReason { get; }

    /// <summary>
    /// 上限に達した実行資源の原因。exhaustion以外ではnull
    /// </summary>
    internal WasmExhaustionReason? ExhaustionReason { get; }

    /// <summary>
    /// 到達した実行資源の上限。exhaustion以外ではnull
    /// </summary>
    internal int? Limit { get; }

    /// <summary>
    /// 失敗が発生した関数のindex。正常終了時はnull
    /// </summary>
    internal uint? FunctionIndex { get; }

    /// <summary>
    /// 失敗が発生した入力バイナリ上のバイト位置。正常終了時はnull
    /// </summary>
    internal long? ByteOffset { get; }

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
    private ExecutionResult(
        ExecutionStatus status,
        ImmutableArray<WasmValue> values = default,
        WasmTrapReason? trapReason = null,
        WasmExhaustionReason? exhaustionReason = null,
        int? limit = null,
        uint? functionIndex = null,
        long? byteOffset = null
    )
    {
        Status = status;
        values_ = values;
        TrapReason = trapReason;
        ExhaustionReason = exhaustionReason;
        Limit = limit;
        FunctionIndex = functionIndex;
        ByteOffset = byteOffset;
    }

    /// <summary>
    /// 正常終了の結果を構築する
    /// </summary>
    /// <param name="values">戻り値。未指定の場合は空の結果として扱う</param>
    /// <returns>指定した戻り値を保持する正常結果</returns>
    internal static ExecutionResult Success(ImmutableArray<WasmValue> values = default)
    {
        return new ExecutionResult(ExecutionStatus.Success, values);
    }

    /// <summary>
    /// trapの原因と発生位置を保持する失敗結果を構築する
    /// </summary>
    /// <param name="reason">trapの原因</param>
    /// <param name="functionIndex">trapが発生した関数のindex</param>
    /// <param name="byteOffset">trapが発生した入力バイナリ上のバイト位置</param>
    /// <returns>trapを表す実行結果</returns>
    internal static ExecutionResult Trap(WasmTrapReason reason, uint functionIndex, long byteOffset)
    {
        return new ExecutionResult(
            ExecutionStatus.Trap,
            trapReason: reason,
            functionIndex: functionIndex,
            byteOffset: byteOffset
        );
    }

    /// <summary>
    /// 実行資源の上限到達の原因、適用上限と発生位置を保持する失敗結果を構築する
    /// </summary>
    /// <param name="reason">上限に達した実行資源の原因</param>
    /// <param name="limit">この実行に適用された正の上限</param>
    /// <param name="functionIndex">上限に達した関数のindex</param>
    /// <param name="byteOffset">上限に達した入力バイナリ上のバイト位置</param>
    /// <returns>実行資源の上限到達を表す実行結果</returns>
    /// <exception cref="ArgumentOutOfRangeException">上限が0以下の場合</exception>
    internal static ExecutionResult Exhaustion(
        WasmExhaustionReason reason,
        int limit,
        uint functionIndex,
        long byteOffset
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        return new ExecutionResult(
            ExecutionStatus.Exhaustion,
            exhaustionReason: reason,
            limit: limit,
            functionIndex: functionIndex,
            byteOffset: byteOffset
        );
    }
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
