using System.Collections.Immutable;

namespace WasmSharp.Execution;

/// <summary>
/// 関数の検証と同じパスで確定した不変の実行コード
/// </summary>
/// <remarks>検証で完成した非defaultの命令配列と最大operand数を保持する</remarks>
/// <param name="instructions">検証で完成した非defaultの線形命令配列</param>
/// <param name="maxOperandStack">関数の実行に必要なoperand領域の最大要素数</param>
internal sealed class FunctionCode(ImmutableArray<Instruction> instructions, int maxOperandStack)
{
    /// <summary>
    /// 関数内の実行順に並べた線形命令
    /// </summary>
    internal ImmutableArray<Instruction> Instructions { get; } = instructions;

    /// <summary>
    /// 関数の実行に必要なoperand領域の最大要素数
    /// </summary>
    internal int MaxOperandStack { get; } = maxOperandStack;
}
