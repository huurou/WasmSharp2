using WasmSharp.Exceptions;

namespace WasmSharp;

/// <summary>
/// tableを表現するクラス
/// </summary>
public sealed class WasmTable
{
    /// <summary>
    /// 型付き参照の要素領域
    /// </summary>
    private WasmValue[] elements_;

    /// <summary>
    /// 要素の参照型
    /// </summary>
    public WasmValueKind ElementType { get; }

    /// <summary>
    /// 現在の要素数
    /// </summary>
    public uint Count => (uint)elements_.Length;

    /// <summary>
    /// 宣言された最大要素数。指定がなければnull
    /// </summary>
    public uint? MaximumElements { get; }

    /// <summary>
    /// limitsに従って型別nullで初期化されたtableを生成する
    /// </summary>
    public WasmTable(WasmValueKind elementType, WasmLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        if (elementType is not (WasmValueKind.FuncRef or WasmValueKind.ExternRef))
        {
            throw new ArgumentOutOfRangeException(
                nameof(elementType),
                "tableには参照型を指定してください。"
            );
        }
        if (limits.Minimum > limits.Maximum)
        {
            throw new ArgumentException(
                "tableの最小要素数が最大要素数を超えています。",
                nameof(limits)
            );
        }
        if (limits.Minimum > Array.MaxLength)
        {
            throw new WasmImplementationLimitException(
                "tableの要素数が配列の保持上限を超えています。",
                WasmImplementationLimitReason.CollectionSize,
                Array.MaxLength
            );
        }

        ElementType = elementType;
        MaximumElements = limits.Maximum;
        elements_ = new WasmValue[limits.Minimum];
        Array.Fill(
            elements_,
            elementType == WasmValueKind.FuncRef
                ? WasmValue.FromFuncRef(null)
                : WasmValue.FromExternRef(null)
        );
    }

    /// <summary>
    /// 指定位置の参照を取得する
    /// </summary>
    public WasmValue Get(uint index)
    {
        ValidateIndex(index);
        return elements_[index];
    }

    /// <summary>
    /// 指定位置に型の一致する参照を設定する
    /// </summary>
    public void Set(uint index, WasmValue value)
    {
        ValidateIndex(index);
        if (value.Kind != ElementType)
        {
            throw new ArgumentException("tableの要素型と値の型が一致しません。", nameof(value));
        }

        elements_[index] = value;
    }

    /// <summary>
    /// 既存要素を保持して増大し、追加要素を指定参照で初期化する
    /// </summary>
    /// <remarks>予測可能な上限超過はfalseを返す。実割当例外は伝播し、既存状態を維持する</remarks>
    public bool TryGrow(uint delta, WasmValue initialValue, out uint previousCount)
    {
        previousCount = Count;
        if (initialValue.Kind != ElementType)
        {
            throw new ArgumentException(
                "tableの要素型と初期値の型が一致しません。",
                nameof(initialValue)
            );
        }

        var nextCount = (ulong)previousCount + delta;
        if (
            nextCount > uint.MaxValue
            || nextCount > MaximumElements
            || nextCount > (ulong)Array.MaxLength
        )
        {
            return false;
        }

        if (delta == 0)
        {
            return true;
        }

        // コピーと追加要素の初期化が完了してから、要素領域を確定する。
        var elements = new WasmValue[(int)nextCount];
        elements_.CopyTo(elements, 0);
        Array.Fill(elements, initialValue, elements_.Length, (int)delta);
        elements_ = elements;
        return true;
    }

    private void ValidateIndex(uint index)
    {
        if (index >= Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "tableの範囲外です。");
        }
    }
}
