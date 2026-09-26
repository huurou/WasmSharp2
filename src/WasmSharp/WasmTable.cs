using WasmSharp.Exceptions;

namespace WasmSharp;

/// <summary>
/// 型付き参照と要素数を保持する共有table
/// </summary>
/// <remarks>同じ実体をimportしたinstanceとホストは、更新後の要素と増大後の要素数を共有する</remarks>
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
    /// <param name="elementType">要素の参照型。FuncRefまたはExternRef</param>
    /// <param name="limits">初期要素数と任意の最大要素数</param>
    /// <exception cref="ArgumentNullException">limitsがnullの場合</exception>
    /// <exception cref="ArgumentOutOfRangeException">elementTypeが参照型ではない場合</exception>
    /// <exception cref="ArgumentException">最小要素数が最大要素数を超える場合</exception>
    /// <exception cref="WasmImplementationLimitException">初期要素数が配列の保持上限を超える場合</exception>
    /// <exception cref="OutOfMemoryException">初期領域を割り当てられない場合</exception>
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
    /// <param name="index">0からCount未満の要素位置</param>
    /// <returns>要素型と参照先の同一性を保持した現在値。型別のnullを含む</returns>
    /// <exception cref="ArgumentOutOfRangeException">indexが現在のtableの範囲外の場合</exception>
    public WasmValue Get(uint index)
    {
        ValidateIndex(index);
        return elements_[index];
    }

    /// <summary>
    /// 指定位置に型の一致する参照を設定する
    /// </summary>
    /// <param name="index">0からCount未満の要素位置</param>
    /// <param name="value">要素型が一致するnullまたは非null参照</param>
    /// <exception cref="ArgumentOutOfRangeException">indexが範囲外の場合。要素は変更しない</exception>
    /// <exception cref="ArgumentException">valueの型が要素型と一致しない場合。要素は変更しない</exception>
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
    /// <param name="delta">追加する要素数。0の場合も初期値の型を検査する</param>
    /// <param name="initialValue">追加要素へ設定する、要素型が一致するnullまたは非null参照</param>
    /// <param name="previousCount">成功・失敗にかかわらず、増大前の要素数</param>
    /// <returns>増大に成功した場合はtrue。宣言された最大値または保持上限を超える場合は、要素数と内容を変更せずfalse</returns>
    /// <exception cref="ArgumentException">initialValueの型が要素型と一致しない場合。要素数と内容は変更しない</exception>
    /// <exception cref="OutOfMemoryException">追加領域を割り当てられない場合。要素数と内容は変更しない</exception>
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

    /// <summary>
    /// 指定位置が現在のtableの範囲内にあることを確認する
    /// </summary>
    /// <param name="index">確認する要素位置</param>
    /// <exception cref="ArgumentOutOfRangeException">indexが現在の要素数以上の場合</exception>
    private void ValidateIndex(uint index)
    {
        if (index >= Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "tableの範囲外です。");
        }
    }
}
