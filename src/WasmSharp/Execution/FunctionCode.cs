using System.Collections.Immutable;
using WasmSharp.Modules;

namespace WasmSharp.Execution;

/// <summary>
/// 関数の検証と同じパスで確定した不変の実行コード
/// </summary>
/// <remarks>検証で完成した非defaultの命令配列、追加localsの初期値と最大operand数を保持する</remarks>
/// <param name="instructions">検証で完成した非defaultの線形命令配列</param>
/// <param name="locals">引数の後に続く追加localsの圧縮宣言</param>
/// <param name="maxOperandStack">関数の実行に必要なoperand領域の最大要素数</param>
internal sealed class FunctionCode(
    ImmutableArray<Instruction> instructions,
    ReadOnlySpan<LocalDeclaration> locals,
    int maxOperandStack
)
{
    /// <summary>
    /// 関数内の実行順に並べた線形命令
    /// </summary>
    internal ImmutableArray<Instruction> Instructions { get; } = instructions;

    /// <summary>
    /// 引数の後に続く追加localsを、展開せずに個数と型別の初期値でまとめた配列
    /// </summary>
    internal ImmutableArray<LocalInitializer> Locals { get; } = CreateLocals(locals);

    /// <summary>
    /// 引数を含まない追加localsの合計数
    /// </summary>
    internal ulong LocalCount { get; } = CountLocals(locals);

    /// <summary>
    /// 関数の実行に必要なoperand領域の最大要素数
    /// </summary>
    internal int MaxOperandStack { get; } = maxOperandStack;

    /// <summary>
    /// 圧縮宣言の個数を保ったまま、型ごとの初期値へ変換する
    /// </summary>
    /// <param name="locals">追加localsの圧縮宣言</param>
    /// <returns>宣言順の個数と初期値</returns>
    private static ImmutableArray<LocalInitializer> CreateLocals(
        ReadOnlySpan<LocalDeclaration> locals
    )
    {
        var initializers = ImmutableArray.CreateBuilder<LocalInitializer>(locals.Length);
        foreach (var local in locals)
        {
            initializers.Add(new LocalInitializer(local.Count, CreateInitialValue(local.Type)));
        }

        return initializers.MoveToImmutable();
    }

    /// <summary>
    /// 追加localsの個数を巡回しない幅で合計する
    /// </summary>
    /// <param name="locals">追加localsの圧縮宣言</param>
    /// <returns>追加localsの合計数</returns>
    private static ulong CountLocals(ReadOnlySpan<LocalDeclaration> locals)
    {
        ulong count = 0;
        foreach (var local in locals)
        {
            count += local.Count;
        }

        return count;
    }

    /// <summary>
    /// 数値・v128では型に応じたゼロ、参照では型に応じたnullを返す
    /// </summary>
    /// <param name="kind">localの値型</param>
    /// <returns>localの初期値</returns>
    private static WasmValue CreateInitialValue(WasmValueKind kind)
    {
        return kind switch
        {
            WasmValueKind.I32 => WasmValue.FromI32(0),
            WasmValueKind.I64 => WasmValue.FromI64(0),
            WasmValueKind.F32 => WasmValue.FromF32Bits(0),
            WasmValueKind.F64 => WasmValue.FromF64Bits(0),
            WasmValueKind.V128 => WasmValue.FromV128(0, 0),
            WasmValueKind.FuncRef => WasmValue.FromFuncRef(null),
            WasmValueKind.ExternRef => WasmValue.FromExternRef(null),
            _ => throw new InvalidOperationException("localの値型が不正です。"),
        };
    }
}
