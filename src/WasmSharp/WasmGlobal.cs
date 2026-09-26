namespace WasmSharp;

/// <summary>
/// 型・可変性・現在値を保持する共有global
/// </summary>
public sealed class WasmGlobal
{
    /// <summary>
    /// 現在の値
    /// </summary>
    private WasmValue value_;

    /// <summary>
    /// globalの型と可変性
    /// </summary>
    public WasmGlobalType Type { get; }

    /// <summary>
    /// globalの現在値
    /// </summary>
    public WasmValue Value
    {
        get => value_;
        set
        {
            if (!Type.IsMutable)
            {
                throw new InvalidOperationException("不変globalは更新できません。");
            }

            if (value.Kind != Type.ValueKind)
            {
                throw new ArgumentException("globalの型と値の型が一致しません。", nameof(value));
            }

            value_ = value;
        }
    }

    /// <summary>
    /// 型と初期値を指定してglobalを生成する
    /// </summary>
    public WasmGlobal(WasmGlobalType type, WasmValue initialValue)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (
            type.ValueKind
            is not (
                WasmValueKind.I32
                or WasmValueKind.I64
                or WasmValueKind.F32
                or WasmValueKind.F64
                or WasmValueKind.V128
                or WasmValueKind.FuncRef
                or WasmValueKind.ExternRef
            )
        )
        {
            throw new ArgumentException(
                "Core 2.0で定義されていないvalueの種類です。",
                nameof(type)
            );
        }

        if (initialValue.Kind != type.ValueKind)
        {
            throw new ArgumentException(
                "globalの型と初期値の型が一致しません。",
                nameof(initialValue)
            );
        }

        Type = type;
        // 不変globalへの更新は禁止するが、生成時の初期値は直接設定する。
        value_ = initialValue;
    }

    /// <summary>
    /// 検証済みのglobal.setから、可変性と型を再検査せずに現在値を更新する
    /// </summary>
    /// <param name="value">検証で型とglobalの可変性を確認済みの値</param>
    internal void SetValidatedValue(WasmValue value)
    {
        value_ = value;
    }
}
