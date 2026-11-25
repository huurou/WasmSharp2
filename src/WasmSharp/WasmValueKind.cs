namespace WasmSharp;

/// <summary>
/// valueの種類
/// </summary>
public enum WasmValueKind
{
    /// <summary>
    /// 32ビット整数
    /// </summary>
    I32,

    /// <summary>
    /// 64ビット整数
    /// </summary>
    I64,

    /// <summary>
    /// 32ビット浮動小数点数
    /// </summary>
    F32,

    /// <summary>
    /// 64ビット浮動小数点数
    /// </summary>
    F64,

    /// <summary>
    /// 128ビットベクトル
    /// </summary>
    V128,

    /// <summary>
    /// 関数への参照
    /// </summary>
    FuncRef,

    /// <summary>
    /// ホスト側のvalueへの外部参照
    /// </summary>
    ExternRef,

    /// <summary>
    /// Wasm例外への参照
    /// </summary>
    ExnRef,

    /// <summary>
    /// 内部参照の最上位型
    /// </summary>
    AnyRef,

    /// <summary>
    /// 同一性を比較できる内部参照
    /// </summary>
    EqRef,

    /// <summary>
    /// 31ビット整数への参照
    /// </summary>
    I31Ref,

    /// <summary>
    /// 構造体への参照
    /// </summary>
    StructRef,

    /// <summary>
    /// 配列への参照
    /// </summary>
    ArrayRef,
}

/// <summary>
/// 値の種類に共通する分類と参照型の階層
/// </summary>
internal static class WasmValueKindExtensions
{
    internal static bool IsSupported(this WasmValueKind kind)
    {
        return kind is WasmValueKind.I32 or WasmValueKind.I64 or WasmValueKind.F32 or WasmValueKind.F64 or WasmValueKind.V128
            || kind.IsReference();
    }

    internal static bool IsReference(this WasmValueKind kind)
    {
        return kind.GetTopKind() is WasmValueKind.FuncRef or WasmValueKind.ExternRef or WasmValueKind.AnyRef;
    }

    /// <summary>
    /// 参照型を所属する階層の最上位型にまとめる 数値とベクトルの種類はそのまま返す
    /// </summary>
    internal static WasmValueKind GetTopKind(this WasmValueKind kind)
    {
        return kind is WasmValueKind.EqRef or WasmValueKind.I31Ref or WasmValueKind.StructRef or WasmValueKind.ArrayRef
            ? WasmValueKind.AnyRef
            : kind;
    }
}
