namespace WasmSharp;

/// <summary>
/// フィールドに宣言できる詰め込み型
/// </summary>
public enum WasmPackedType
{
    /// <summary>
    /// 値型のまま格納する
    /// </summary>
    None,

    /// <summary>
    /// i32の下位8ビットを格納する
    /// </summary>
    I8,

    /// <summary>
    /// i32の下位16ビットを格納する
    /// </summary>
    I16,
}
