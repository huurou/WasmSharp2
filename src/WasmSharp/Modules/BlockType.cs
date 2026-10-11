namespace WasmSharp.Modules;

/// <summary>
/// 構造化制御の型指定の形式
/// </summary>
internal enum BlockTypeKind
{
    /// <summary>
    /// 引数と結果を持たない空型
    /// </summary>
    Empty,

    /// <summary>
    /// 単一の結果値型
    /// </summary>
    Value,

    /// <summary>
    /// module内の関数型の添字
    /// </summary>
    TypeIndex,
}

/// <summary>
/// 空型、単一結果型と型添字を区別する不変の型指定
/// </summary>
internal readonly struct BlockType
{
    /// <summary>
    /// 型指定の形式
    /// </summary>
    internal BlockTypeKind Kind { get; }

    /// <summary>
    /// 単一の結果値型 Value形式の場合だけ参照する
    /// </summary>
    internal WasmValueKind ValueType { get; }

    /// <summary>
    /// 関数型の添字 TypeIndex形式の場合だけ参照する
    /// </summary>
    internal uint TypeIndex { get; }

    /// <summary>
    /// 空型の指定
    /// </summary>
    internal static BlockType Empty => default;

    /// <summary>
    /// 形式と対応する値を保持する
    /// </summary>
    /// <param name="kind">型指定の形式</param>
    /// <param name="valueType">単一の結果値型 他の形式ではdefault</param>
    /// <param name="typeIndex">関数型の添字 他の形式では0</param>
    private BlockType(BlockTypeKind kind, WasmValueKind valueType, uint typeIndex)
    {
        Kind = kind;
        ValueType = valueType;
        TypeIndex = typeIndex;
    }

    /// <summary>
    /// 単一の結果値型を指定する
    /// </summary>
    /// <param name="valueType">結果の値型</param>
    /// <returns>Value形式の型指定</returns>
    internal static BlockType FromValue(WasmValueKind valueType)
    {
        return new(BlockTypeKind.Value, valueType, 0);
    }

    /// <summary>
    /// 非負の関数型添字を指定する
    /// </summary>
    /// <param name="typeIndex">関数型の添字 存在検査はValidateで行う</param>
    /// <returns>TypeIndex形式の型指定</returns>
    internal static BlockType FromTypeIndex(uint typeIndex)
    {
        return new(BlockTypeKind.TypeIndex, default, typeIndex);
    }
}
