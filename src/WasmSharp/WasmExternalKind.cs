namespace WasmSharp;

/// <summary>
/// import/exportする外部要素の種類
/// </summary>
public enum WasmExternalKind
{
    /// <summary>
    /// 関数
    /// </summary>
    Function,

    /// <summary>
    /// 参照のtable
    /// </summary>
    Table,

    /// <summary>
    /// 線形memory
    /// </summary>
    Memory,

    /// <summary>
    /// global
    /// </summary>
    Global,
}
