namespace WasmSharp.Instructions;

/// <summary>
/// 関数本体と定数式で共用する、1個の値から参照を生成する命令の型と評価
/// </summary>
internal sealed class ConstantReferenceOperation(
    WasmValueKind inputKind,
    WasmValueKind resultKind,
    Func<WasmValue, WasmValue> evaluate
)
{
    internal static ConstantReferenceOperation AnyConvertExtern { get; } =
        new ConstantReferenceOperation(WasmValueKind.ExternRef, WasmValueKind.AnyRef, x => x.InternalizeExtern());

    internal static ConstantReferenceOperation ExternConvertAny { get; } =
        new ConstantReferenceOperation(WasmValueKind.AnyRef, WasmValueKind.ExternRef, x => x.ExternalizeAny());

    internal static ConstantReferenceOperation RefI31 { get; } =
        new ConstantReferenceOperation(WasmValueKind.I32, WasmValueKind.I31Ref, x => WasmValue.FromI31(x.AsI32()));

    internal WasmValueType InputType { get; } = inputKind;

    internal static ConstantReferenceOperation? For(ValidationRule rule)
    {
        return rule switch
        {
            ValidationRule.AnyConvertExtern => AnyConvertExtern,
            ValidationRule.ExternConvertAny => ExternConvertAny,
            ValidationRule.RefI31 => RefI31,
            _ => null,
        };
    }

    // i32と到達不能な位置のbottomには非nullの結果型を付ける。参照入力はnull可否を引き継ぐ。
    internal WasmValueType GetResultType(WasmValueType? input)
    {
        return WasmValueType.Reference(resultKind, input is { IsNullable: true });
    }

    internal WasmValue Evaluate(WasmValue input)
    {
        return evaluate(input);
    }
}
