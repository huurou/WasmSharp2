using System.Collections.Immutable;

namespace WasmSharp.Modules.Definitions;

/// <summary>
/// 要素セグメントの初期化方式
/// </summary>
internal enum ElementMode
{
    /// <summary>
    /// インスタンス化時に対象テーブルへ転送する方式
    /// </summary>
    Active,

    /// <summary>
    /// table.initで使用するために内容を保持する方式
    /// </summary>
    Passive,

    /// <summary>
    /// 関数参照を宣言し、インスタンス化時に内容を破棄する方式
    /// </summary>
    Declarative,
}

/// <summary>
/// 要素セグメントの参照型、未評価の初期化式列、方式と入力位置
/// </summary>
internal sealed class ElementDefinition
{
    /// <summary>
    /// 各初期化式が返す参照型
    /// </summary>
    internal WasmValueKind ElementType { get; }

    /// <summary>
    /// 終端を含む未評価の初期化式を宣言順に保持する不変配列
    /// </summary>
    internal ImmutableArray<ImmutableArray<DecodedInstruction>> Initializers { get; }

    /// <summary>
    /// 要素セグメントの初期化方式
    /// </summary>
    internal ElementMode Mode { get; }

    /// <summary>
    /// アクティブ要素セグメントの初期化先 他の方式ではnull
    /// </summary>
    internal ActiveElementTarget? ActiveTarget { get; }

    /// <summary>
    /// 入力バイナリ上の要素セグメントのバイト位置
    /// </summary>
    internal long ByteOffset { get; }

    /// <summary>
    /// ファクトリーで確定した方式と初期化先を保持する
    /// </summary>
    /// <param name="elementType">初期化式の参照型</param>
    /// <param name="initializers">構築時に各命令列をコピーする初期化式列</param>
    /// <param name="mode">要素セグメントの初期化方式</param>
    /// <param name="activeTarget">アクティブの場合だけ持つ初期化先</param>
    /// <param name="byteOffset">入力バイナリ上の要素セグメントのバイト位置</param>
    private ElementDefinition(
        WasmValueKind elementType,
        ReadOnlySpan<IEnumerable<DecodedInstruction>> initializers,
        ElementMode mode,
        ActiveElementTarget? activeTarget,
        long byteOffset
    )
    {
        ElementType = elementType;
        var expressions = ImmutableArray.CreateBuilder<ImmutableArray<DecodedInstruction>>(
            initializers.Length
        );
        foreach (var initializer in initializers)
        {
            expressions.Add(ImmutableArray.CreateRange(initializer));
        }
        Initializers = expressions.MoveToImmutable();
        Mode = mode;
        ActiveTarget = activeTarget;
        ByteOffset = byteOffset;
    }

    /// <summary>
    /// 対象テーブルとオフセット式を持つアクティブ要素セグメントを構築する
    /// </summary>
    /// <param name="elementType">初期化式の参照型</param>
    /// <param name="initializers">構築時に各命令列をコピーする初期化式列</param>
    /// <param name="activeTarget">対象テーブルと未評価のオフセット式</param>
    /// <param name="byteOffset">入力バイナリ上の要素セグメントのバイト位置</param>
    /// <returns>初期化先を持つActive方式の定義</returns>
    /// <exception cref="ArgumentNullException">初期化先がnullの場合</exception>
    internal static ElementDefinition Active(
        WasmValueKind elementType,
        ReadOnlySpan<IEnumerable<DecodedInstruction>> initializers,
        ActiveElementTarget activeTarget,
        long byteOffset
    )
    {
        ArgumentNullException.ThrowIfNull(activeTarget);
        return new(elementType, initializers, ElementMode.Active, activeTarget, byteOffset);
    }

    /// <summary>
    /// 初期化先を持たないパッシブ要素セグメントを構築する
    /// </summary>
    /// <param name="elementType">初期化式の参照型</param>
    /// <param name="initializers">構築時に各命令列をコピーする初期化式列</param>
    /// <param name="byteOffset">入力バイナリ上の要素セグメントのバイト位置</param>
    /// <returns>初期化先を持たないPassive方式の定義</returns>
    internal static ElementDefinition Passive(
        WasmValueKind elementType,
        ReadOnlySpan<IEnumerable<DecodedInstruction>> initializers,
        long byteOffset
    )
    {
        return new(elementType, initializers, ElementMode.Passive, null, byteOffset);
    }

    /// <summary>
    /// 初期化先を持たない宣言的要素セグメントを構築する
    /// </summary>
    /// <param name="elementType">初期化式の参照型</param>
    /// <param name="initializers">構築時に各命令列をコピーする初期化式列</param>
    /// <param name="byteOffset">入力バイナリ上の要素セグメントのバイト位置</param>
    /// <returns>初期化先を持たないDeclarative方式の定義</returns>
    internal static ElementDefinition Declarative(
        WasmValueKind elementType,
        ReadOnlySpan<IEnumerable<DecodedInstruction>> initializers,
        long byteOffset
    )
    {
        return new(elementType, initializers, ElementMode.Declarative, null, byteOffset);
    }
}

/// <summary>
/// アクティブ要素セグメントが参照するテーブルと未評価のオフセット式
/// </summary>
/// <param name="tableIndex">初期化先の未検証のテーブルインデックス</param>
/// <param name="offsetExpression">構築時にコピーするオフセット式の命令列</param>
internal sealed class ActiveElementTarget(
    uint tableIndex,
    ReadOnlySpan<DecodedInstruction> offsetExpression
)
{
    /// <summary>
    /// 初期化先の未検証のテーブルインデックス
    /// </summary>
    internal uint TableIndex { get; } = tableIndex;

    /// <summary>
    /// 終端を含む未評価のオフセット式
    /// </summary>
    internal ImmutableArray<DecodedInstruction> OffsetExpression { get; } =
        ImmutableArray.Create(offsetExpression);
}
