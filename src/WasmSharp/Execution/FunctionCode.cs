using System.Collections.Immutable;
using WasmSharp.Modules;

namespace WasmSharp.Execution;

/// <summary>
/// 関数の検証と同じパスで確定した不変の実行コード
/// </summary>
/// <remarks>検証で完成した命令・分岐情報、追加localsの初期値と最大operand数を保持する</remarks>
/// <param name="instructions">検証で完成した非defaultの線形命令配列</param>
/// <param name="locals">引数の後に続く追加localsの圧縮宣言</param>
/// <param name="maxOperandStack">関数の実行に必要なoperand領域の最大要素数</param>
/// <param name="branchTargets">検証で完成した分岐情報 分岐を持たない関数では省略する</param>
/// <param name="castTargets">型判定やキャストの対象となる参照型を命令順に並べた配列 対象がなければ省略する</param>
internal sealed class FunctionCode(
    ImmutableArray<Instruction> instructions,
    ReadOnlySpan<LocalDeclaration> locals,
    int maxOperandStack,
    ImmutableArray<BranchTarget> branchTargets = default,
    ImmutableArray<WasmValueType> castTargets = default
)
{
    /// <summary>
    /// 型判定とキャストの命令が即値の添字で参照する型
    /// </summary>
    private readonly ImmutableArray<WasmValueType> _castTargets = castTargets;

    /// <summary>
    /// 関数内の実行順に並べた線形命令
    /// </summary>
    internal ImmutableArray<Instruction> Instructions { get; } = instructions;

    /// <summary>
    /// 命令から参照する関数内の確定済み分岐情報 分岐表は候補の後に既定1件を置く
    /// </summary>
    internal ImmutableArray<BranchTarget> BranchTargets { get; } = branchTargets.IsDefault ? [] : branchTargets;

    /// <summary>
    /// 引数の後に続く追加localsを、展開せずに個数と型別の初期値でまとめた配列
    /// </summary>
    internal ImmutableArray<LocalInitializer> Locals { get; } = CreateLocals(locals);

    /// <summary>
    /// 引数を含まない追加localsの合計数
    /// </summary>
    internal ulong LocalCount { get; } = locals.IsEmpty ? 0u : locals[^1].EndIndex;

    /// <summary>
    /// 関数の実行に必要なoperand領域の最大要素数
    /// </summary>
    internal int MaxOperandStack { get; } = maxOperandStack;

    /// <summary>
    /// 検証済み命令が指定するキャスト先の参照型に値が一致するかを判定する
    /// </summary>
    internal bool MatchesCastTarget(uint index, WasmValue value)
    {
        return _castTargets[(int)index].Accepts(value);
    }

    /// <summary>
    /// 圧縮宣言の個数を保ったまま、型ごとの初期値へ変換する
    /// </summary>
    /// <param name="locals">追加localsの圧縮宣言</param>
    /// <returns>宣言順の個数と初期値</returns>
    private static ImmutableArray<LocalInitializer> CreateLocals(ReadOnlySpan<LocalDeclaration> locals)
    {
        var initializers = ImmutableArray.CreateBuilder<LocalInitializer>(locals.Length);
        uint previousEnd = 0;
        foreach (var local in locals)
        {
            initializers.Add(
                new LocalInitializer(local.EndIndex - previousEnd, WasmValue.CreateInitialValue(local.Type.Kind))
            );
            previousEnd = local.EndIndex;
        }

        return initializers.MoveToImmutable();
    }
}
