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
    private WasmValue[] _elements;

    /// <summary>
    /// 宣言された要素型、アドレス型、要素数の上下限
    /// </summary>
    public WasmTableType Type { get; }

    /// <summary>
    /// 要素の参照型
    /// </summary>
    public WasmValueKind ElementType => Type.ElementType;

    /// <summary>
    /// 現在の要素数
    /// </summary>
    public uint Count => (uint)_elements.Length;

    /// <summary>
    /// 宣言された最大要素数を32ビットの値で返す 指定がなければnull
    /// </summary>
    /// <exception cref="InvalidOperationException">最大要素数が32ビットで表せない場合</exception>
    public uint? MaximumElements =>
        Type.MaximumElements is > uint.MaxValue
            ? throw new InvalidOperationException(
                "最大要素数が32ビットで表せません。Type.MaximumElementsから取得してください。"
            )
            : (uint?)Type.MaximumElements;

    /// <summary>
    /// 指定位置の参照を取得または設定する
    /// </summary>
    /// <param name="index">0からCount未満の要素位置</param>
    /// <value>要素型が一致するnullまたは非null参照</value>
    /// <exception cref="ArgumentOutOfRangeException">indexが現在のテーブルの範囲外の場合</exception>
    /// <exception cref="ArgumentException">設定する値の型が要素型と一致しない場合</exception>
    public WasmValue this[ulong index]
    {
        get
        {
            if (!TryGet(index, out var value))
            {
                throw new ArgumentOutOfRangeException(nameof(index), "tableの範囲外です。");
            }

            return value;
        }
        set
        {
            if (value.Kind != ElementType)
            {
                // 要素型と添字が両方不正な場合は、範囲違反を優先する。
                _ = this[index];
                throw new ArgumentException("tableの要素型と値の型が一致しません。", nameof(value));
            }

            if (!TrySet(index, value))
            {
                throw new ArgumentOutOfRangeException(nameof(index), "tableの範囲外です。");
            }
        }
    }

    /// <summary>
    /// limitsに従って型別nullで初期化されたtableを生成する
    /// </summary>
    /// <param name="elementType">要素の参照型 FuncRefまたはExternRef</param>
    /// <param name="limits">初期要素数と任意の最大要素数</param>
    /// <exception cref="ArgumentNullException">limitsがnullの場合</exception>
    /// <exception cref="ArgumentOutOfRangeException">elementTypeが参照型ではない場合</exception>
    /// <exception cref="ArgumentException">最小要素数が最大要素数を超える場合</exception>
    /// <exception cref="WasmImplementationLimitException">初期要素数が配列の保持上限を超える場合</exception>
    /// <exception cref="OutOfMemoryException">初期領域を割り当てられない場合</exception>
    public WasmTable(WasmValueKind elementType, WasmLimits limits)
        : this(Create32BitType(elementType, limits), nameof(limits)) { }

    private WasmTable(WasmTableType type, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(type, parameterName);
        if (type.MinimumElements > type.MaximumElements)
        {
            throw new ArgumentException("tableの最小要素数が最大要素数を超えています。", parameterName);
        }
        if (type.ExceedsElementLimit)
        {
            throw new ArgumentException("32ビットテーブルの要素数が仕様上限を超えています。", parameterName);
        }
        if (ExceedsStorageLimit(type.MinimumElements))
        {
            throw new WasmImplementationLimitException(
                "tableの要素数が配列の保持上限を超えています。",
                WasmImplementationLimitReason.CollectionSize,
                Array.MaxLength
            );
        }

        Type = type;
        _elements = new WasmValue[(int)type.MinimumElements];
        Array.Fill(
            _elements,
            type.ElementType == WasmValueKind.FuncRef ? WasmValue.FromFuncRef(null) : WasmValue.FromExternRef(null)
        );
    }

    /// <summary>
    /// 宣言された型の最小要素数に従い、すべての要素を型別のnullで初期化する
    /// </summary>
    /// <param name="type">要素型、アドレス型、要素数の上下限</param>
    /// <returns>指定した型のテーブル</returns>
    /// <exception cref="ArgumentNullException">typeがnullの場合</exception>
    /// <exception cref="ArgumentException">要素数の上下限がアドレス型の制約を満たさない場合</exception>
    /// <exception cref="WasmImplementationLimitException">初期要素数が配列の保持上限を超える場合</exception>
    /// <exception cref="OutOfMemoryException">初期領域を割り当てられない場合</exception>
    public static WasmTable Create(WasmTableType type)
    {
        return new WasmTable(type, nameof(type));
    }

    private static WasmTableType Create32BitType(WasmValueKind elementType, WasmLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        if (!WasmTableType.IsSupportedElementType(elementType))
        {
            throw new ArgumentOutOfRangeException(nameof(elementType), "tableには参照型を指定してください。");
        }
        return new WasmTableType(elementType, limits);
    }

    /// <summary>
    /// 指定位置の参照を取得する
    /// </summary>
    /// <param name="index">0からCount未満の要素位置</param>
    /// <returns>要素型と参照先の同一性を保持した現在値 型別のnullを含む</returns>
    /// <exception cref="ArgumentOutOfRangeException">indexが現在のtableの範囲外の場合</exception>
    public WasmValue Get(uint index)
    {
        return this[index];
    }

    /// <summary>
    /// 指定位置に型の一致する参照を設定する
    /// </summary>
    /// <param name="index">0からCount未満の要素位置</param>
    /// <param name="value">要素型が一致するnullまたは非null参照</param>
    /// <exception cref="ArgumentOutOfRangeException">indexが範囲外の場合 要素は変更しない</exception>
    /// <exception cref="ArgumentException">valueの型が要素型と一致しない場合 要素は変更しない</exception>
    public void Set(uint index, WasmValue value)
    {
        this[index] = value;
    }

    /// <summary>
    /// 既存要素を保持して増大し、追加要素を指定参照で初期化する
    /// </summary>
    /// <param name="delta">追加する要素数 0の場合も初期値の型を検査する</param>
    /// <param name="initialValue">追加要素へ設定する、要素型が一致するnullまたは非null参照</param>
    /// <param name="previousCount">成功・失敗にかかわらず、増大前の要素数</param>
    /// <returns>増大に成功した場合はtrue 宣言された最大値または保持上限を超える場合は、要素数と内容を変更せずfalse</returns>
    /// <exception cref="ArgumentException">initialValueの型が要素型と一致しない場合 要素数と内容は変更しない</exception>
    /// <exception cref="OutOfMemoryException">追加領域を割り当てられない場合 要素数と内容は変更しない</exception>
    public bool TryGrow(uint delta, WasmValue initialValue, out uint previousCount)
    {
        return TryGrow((ulong)delta, initialValue, out previousCount);
    }

    /// <inheritdoc cref="TryGrow(uint, WasmValue, out uint)"/>
    internal bool TryGrow(ulong delta, WasmValue initialValue, out uint previousCount)
    {
        previousCount = Count;
        if (initialValue.Kind != ElementType)
        {
            throw new ArgumentException("tableの要素型と初期値の型が一致しません。", nameof(initialValue));
        }

        if (ExceedsStorageLimit(delta, previousCount))
        {
            return false;
        }
        var nextCount = previousCount + delta;
        if (nextCount > Type.MaximumElements)
        {
            return false;
        }

        if (delta == 0)
        {
            return true;
        }

        // コピーと追加要素の初期化が完了してから、要素領域を確定する。
        var elements = new WasmValue[(int)nextCount];
        _elements.CopyTo(elements, 0);
        Array.Fill(elements, initialValue, _elements.Length, (int)delta);
        _elements = elements;
        return true;
    }

    /// <summary>
    /// 要素の追加後に配列の保持上限を超えるかを返す
    /// </summary>
    /// <param name="additionalCount">これから確保する要素数</param>
    /// <param name="currentCount">現在の要素数 初期割り当てでは0</param>
    /// <returns>追加の要素を保持できない場合はtrue</returns>
    internal static bool ExceedsStorageLimit(ulong additionalCount, uint currentCount = 0)
    {
        // 加算前に残り容量と比較し、64ビットの増分が折り返すのを防ぐ。
        return additionalCount > (ulong)Array.MaxLength - currentCount;
    }

    /// <summary>
    /// 指定位置の参照を取得し、範囲内かを返す
    /// </summary>
    /// <param name="index">取得する要素位置</param>
    /// <param name="value">取得した参照 範囲外の場合はdefault</param>
    /// <returns>取得できた場合はtrue 範囲外の場合はfalse</returns>
    internal bool TryGet(ulong index, out WasmValue value)
    {
        if (index >= Count)
        {
            value = default;
            return false;
        }

        value = _elements[(int)index];
        return true;
    }

    /// <summary>
    /// 指定位置へ参照を設定し、範囲内かを返す
    /// </summary>
    /// <param name="index">設定する要素位置</param>
    /// <param name="value">要素型と一致する検証済みの参照</param>
    /// <returns>設定できた場合はtrue 範囲外の場合は内容を変更せずfalse</returns>
    internal bool TrySet(ulong index, WasmValue value)
    {
        if (index >= Count)
        {
            return false;
        }

        _elements[(int)index] = value;
        return true;
    }

    /// <summary>
    /// 加算のオーバーフローを避けて、指定範囲が現在のテーブルに収まるかを返す
    /// </summary>
    private bool ContainsRange(ulong offset, ulong length)
    {
        return offset <= Count && length <= Count - offset;
    }

    /// <summary>
    /// 指定位置へ参照列を転送し、全範囲が収まるかを返す
    /// </summary>
    /// <param name="destination">転送先の先頭要素位置</param>
    /// <param name="values">要素型と一致する検証済みの参照列</param>
    /// <returns>転送できた場合はtrue 範囲外の場合は内容を変更せずfalse</returns>
    internal bool TryWrite(ulong destination, ReadOnlySpan<WasmValue> values)
    {
        if (!ContainsRange(destination, (ulong)values.Length))
        {
            return false;
        }

        values.CopyTo(_elements.AsSpan((int)destination, values.Length));
        return true;
    }

    /// <summary>
    /// 同型tableの参照列をコピーし、両範囲が収まるかを返す
    /// </summary>
    /// <param name="source">要素型が一致する検証済みのコピー元table</param>
    /// <param name="destination">コピー先の先頭要素位置</param>
    /// <param name="sourceOffset">コピー元の先頭要素位置</param>
    /// <param name="length">コピーする要素数</param>
    /// <returns>コピーできた場合はtrue 範囲外の場合は内容を変更せずfalse</returns>
    internal bool TryCopy(WasmTable source, ulong destination, ulong sourceOffset, ulong length)
    {
        if (!ContainsRange(destination, length) || !source.ContainsRange(sourceOffset, length))
        {
            return false;
        }

        source._elements.AsSpan((int)sourceOffset, (int)length).CopyTo(_elements.AsSpan((int)destination, (int)length));
        return true;
    }

    /// <summary>
    /// 指定範囲を同じ参照で埋め、全範囲が収まるかを返す
    /// </summary>
    /// <param name="destination">書き込み先の先頭要素位置</param>
    /// <param name="value">要素型と一致する検証済みの参照</param>
    /// <param name="length">書き込む要素数</param>
    /// <returns>書き込めた場合はtrue 範囲外の場合は内容を変更せずfalse</returns>
    internal bool TryFill(ulong destination, WasmValue value, ulong length)
    {
        if (!ContainsRange(destination, length))
        {
            return false;
        }

        _elements.AsSpan((int)destination, (int)length).Fill(value);
        return true;
    }
}
