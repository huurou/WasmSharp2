using System.Collections.Immutable;
using WasmSharp.Exceptions;

namespace WasmSharp.Execution;

/// <summary>
/// 同じスレッドの同期呼び出しで共有する実行状態
/// </summary>
public sealed class WasmExecutionContext
{
    /// <summary>
    /// 現在のスレッドで同期呼び出しが共有する実行コンテキスト
    /// </summary>
    [ThreadStatic]
    private static WasmExecutionContext? current_;

    /// <summary>
    /// 関数呼び出しのフレームを保持する配列
    /// </summary>
    private ExecutionFrame[] frames_ = [];

    /// <summary>
    /// 引数、localsとoperandを共有して保持する値スタックの配列
    /// </summary>
    private WasmValue[] values_ = [];

    /// <summary>
    /// 現在のスレッドの実行コンテキスト。実行中でなければnull
    /// </summary>
    internal static WasmExecutionContext? Current => current_;

    /// <summary>
    /// 最外側の呼び出しで固定した呼び出し深さの上限
    /// </summary>
    internal int MaxCallDepth { get; }

    /// <summary>
    /// 同じ実行コンテキストで共有する現在の呼び出し深さ
    /// </summary>
    internal int CallDepth { get; private set; }

    /// <summary>
    /// フレーム配列内の使用中の要素数
    /// </summary>
    internal int FrameCount { get; private set; }

    /// <summary>
    /// 値スタック内の使用中の要素数
    /// </summary>
    internal int ValueCount { get; private set; }

    /// <summary>
    /// 実行ポリシーの呼び出し深さ上限を固定してコンテキストを構築する
    /// </summary>
    /// <param name="options">最外側の呼び出しに適用する実行ポリシー</param>
    private WasmExecutionContext(WasmExecutionOptions options)
    {
        MaxCallDepth = options.MaxCallDepth;
    }

    /// <summary>
    /// 同じスレッドのコンテキストを再利用し、存在しなければ新たに開く
    /// </summary>
    /// <param name="options">新しく開く場合にだけ適用する実行ポリシー</param>
    /// <param name="isOutermost">この呼び出しで新しく開いた場合はtrue</param>
    /// <returns>同期呼び出しの間で共有する実行コンテキスト</returns>
    internal static WasmExecutionContext Enter(WasmExecutionOptions options, out bool isOutermost)
    {
        isOutermost = current_ is null;
        return current_ ??= new(options);
    }

    /// <summary>
    /// 呼び出し深さが上限未満の場合にだけ1段増やす
    /// </summary>
    /// <returns>呼び出しに入れた場合はtrue。上限に達している場合は深さを変えずfalse</returns>
    internal bool TryEnterCall()
    {
        if (CallDepth == MaxCallDepth)
        {
            return false;
        }
        CallDepth++;
        return true;
    }

    /// <summary>
    /// 入場済みの関数呼び出しから戻り、呼び出し深さを1段減らす
    /// </summary>
    internal void ExitCall()
    {
        CallDepth--;
    }

    /// <summary>
    /// 最外側の呼び出しの終了時に、現在のスレッドからコンテキストの参照を解除する
    /// </summary>
    /// <param name="isOutermost">対応するEnterで取得した最外側かどうかの値</param>
    internal void Exit(bool isOutermost)
    {
        if (isOutermost)
        {
            current_ = null;
        }
    }

    /// <summary>
    /// 関数入口で必要な1フレームと値スタックの容量を確保する
    /// </summary>
    /// <param name="operandBase">共有値スタック上の引数とlocalsに続くoperand領域の開始位置</param>
    /// <param name="maxOperandStack">関数の検証で求めたoperandの最大要素数</param>
    /// <param name="location">保持上限を超えた場合に報告する処理段階と入力上の位置</param>
    /// <exception cref="WasmImplementationLimitException">必要数が配列の保持上限を超える場合</exception>
    internal void EnsureCapacity(int operandBase, int maxOperandStack, WasmFailureLocation location)
    {
        // 加算前に広げ、両方の必要数を検査してから配列を拡張する。
        var frameCapacity = CalculateCapacity(frames_.Length, (ulong)FrameCount + 1, location);
        var valueCapacity = CalculateCapacity(
            values_.Length,
            (ulong)operandBase + (ulong)maxOperandStack,
            location
        );
        if (frameCapacity != frames_.Length)
        {
            Array.Resize(ref frames_, frameCapacity);
        }
        if (valueCapacity != values_.Length)
        {
            Array.Resize(ref values_, valueCapacity);
        }
    }

    /// <summary>
    /// 容量が不足する場合、配列の保持上限内で必要数または倍増後の大きい方を返す
    /// </summary>
    /// <param name="capacity">現在の配列容量</param>
    /// <param name="requiredCount">必要な要素数</param>
    /// <param name="location">保持上限を超えた場合に報告する処理段階と入力上の位置</param>
    /// <returns>必要数を満たす容量。現在の容量が十分ならその値</returns>
    /// <exception cref="WasmImplementationLimitException">必要数が配列の保持上限を超える場合</exception>
    internal static int CalculateCapacity(
        int capacity,
        ulong requiredCount,
        WasmFailureLocation location
    )
    {
        if (requiredCount > (ulong)Array.MaxLength)
        {
            throw new WasmImplementationLimitException(
                "実行スタックの必要数がコレクションの保持上限を超えました。",
                WasmImplementationLimitReason.CollectionSize,
                Array.MaxLength,
                location
            );
        }
        if (requiredCount <= (ulong)capacity)
        {
            return capacity;
        }
        var doubled = Math.Min((ulong)capacity * 2, (ulong)Array.MaxLength);
        return (int)Math.Max(requiredCount, doubled);
    }

    /// <summary>
    /// 確保済みの領域へフレームを追加し、使用中の要素数を増やす
    /// </summary>
    /// <param name="frame">追加する関数呼び出しのフレーム</param>
    internal void PushFrame(ExecutionFrame frame)
    {
        frames_[FrameCount++] = frame;
    }

    /// <summary>
    /// 確保済みの値スタックへ値を積み、使用中の要素数を増やす
    /// </summary>
    /// <param name="value">値スタックへ積む値</param>
    internal void PushValue(WasmValue value)
    {
        values_[ValueCount++] = value;
    }

    /// <summary>
    /// 指定位置に保持しているフレームのコピーを取得する
    /// </summary>
    /// <param name="index">フレーム配列内の位置</param>
    /// <returns>指定位置のフレームの値</returns>
    internal ExecutionFrame GetFrame(int index)
    {
        return frames_[index];
    }

    /// <summary>
    /// 現在の関数の次命令を値で取得し、関数内のpcを1つ進める
    /// </summary>
    internal Instruction ReadNextInstruction()
    {
        var frameIndex = FrameCount - 1;
        var frame = frames_[frameIndex];
        var instruction = frame.Function.Code.Instructions[frame.Pc];
        frames_[frameIndex].Pc++;
        return instruction;
    }

    /// <summary>
    /// 末尾の結果を引数開始位置へ順序を保って移し、現在の関数を終了する
    /// </summary>
    internal void CompleteFrame()
    {
        var frame = frames_[FrameCount - 1];
        var resultCount = frame.Function.Type.Results.Length;
        Array.Copy(values_, ValueCount - resultCount, values_, frame.StackBase, resultCount);
        Restore(FrameCount - 1, frame.StackBase + resultCount, CallDepth - 1);
    }

    /// <summary>
    /// 共有値スタックの指定位置に保持している値を取得する
    /// </summary>
    /// <param name="index">共有値スタック内の位置</param>
    /// <returns>指定位置の値</returns>
    internal WasmValue GetValue(int index)
    {
        return values_[index];
    }

    /// <summary>
    /// 指定範囲を作業スタックから独立した戻り値の配列へコピーする
    /// </summary>
    internal ImmutableArray<WasmValue> CopyValues(int start, int count)
    {
        return ImmutableArray.Create(values_.AsSpan(start, count));
    }

    /// <summary>
    /// 呼び出し前の要素数と深さに戻し、除いたフレームと値が保持する参照を解除する
    /// </summary>
    /// <param name="frameCount">復元する呼び出し前のフレーム数</param>
    /// <param name="valueCount">復元する呼び出し前の値の数</param>
    /// <param name="callDepth">復元する呼び出し前の深さ</param>
    internal void Restore(int frameCount, int valueCount, int callDepth)
    {
        Array.Clear(frames_, frameCount, FrameCount - frameCount);
        Array.Clear(values_, valueCount, ValueCount - valueCount);
        FrameCount = frameCount;
        ValueCount = valueCount;
        CallDepth = callDepth;
    }
}
