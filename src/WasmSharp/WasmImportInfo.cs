namespace WasmSharp;

/// <summary>
/// import宣言の名前、種類と解決済みの要求型
/// </summary>
public abstract record WasmImportInfo
{
    /// <summary>
    /// 要求する提供元のmodule名
    /// </summary>
    public string ModuleName { get; }

    /// <summary>
    /// 要求する外部要素の名前
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 外部要素の種類
    /// </summary>
    public WasmExternalKind Kind => ExternalKind;

    /// <summary>
    /// 派生型ごとの外部要素の種類を取得する。実装を同一アセンブリへ限定し、外部からの派生を防ぐ
    /// </summary>
    private protected abstract WasmExternalKind ExternalKind { get; }

    /// <summary>
    /// import宣言の共通の名前情報を初期化する
    /// </summary>
    /// <param name="moduleName">要求する提供元のmodule名</param>
    /// <param name="name">要求する外部要素の名前</param>
    private WasmImportInfo(string moduleName, string name)
    {
        ModuleName = moduleName;
        Name = name;
    }

    /// <summary>
    /// 関数importの要求型
    /// </summary>
    /// <param name="ModuleName">要求する提供元のmodule名</param>
    /// <param name="Name">要求する外部要素の名前</param>
    /// <param name="Type">解決済みの関数型</param>
    public sealed record Function(string ModuleName, string Name, WasmFunctionType Type)
        : WasmImportInfo(ModuleName, Name)
    {
        /// <inheritdoc />
        private protected override WasmExternalKind ExternalKind => WasmExternalKind.Function;
    }

    /// <summary>
    /// global importの要求型
    /// </summary>
    /// <param name="ModuleName">要求する提供元のmodule名</param>
    /// <param name="Name">要求する外部要素の名前</param>
    /// <param name="Type">値型と可変性</param>
    public sealed record Global(string ModuleName, string Name, WasmGlobalType Type)
        : WasmImportInfo(ModuleName, Name)
    {
        /// <inheritdoc />
        private protected override WasmExternalKind ExternalKind => WasmExternalKind.Global;
    }

    /// <summary>
    /// memory importの要求limits
    /// </summary>
    /// <param name="ModuleName">要求する提供元のmodule名</param>
    /// <param name="Name">要求する外部要素の名前</param>
    /// <param name="Limits">宣言された最小値と任意の最大値</param>
    public sealed record Memory(string ModuleName, string Name, WasmLimits Limits)
        : WasmImportInfo(ModuleName, Name)
    {
        /// <inheritdoc />
        private protected override WasmExternalKind ExternalKind => WasmExternalKind.Memory;
    }

    /// <summary>
    /// table importの要求型
    /// </summary>
    /// <param name="ModuleName">要求する提供元のmodule名</param>
    /// <param name="Name">要求する外部要素の名前</param>
    /// <param name="ElementType">要素の参照型</param>
    /// <param name="Limits">宣言された最小値と任意の最大値</param>
    public sealed record Table(
        string ModuleName,
        string Name,
        WasmValueKind ElementType,
        WasmLimits Limits
    ) : WasmImportInfo(ModuleName, Name)
    {
        /// <inheritdoc />
        private protected override WasmExternalKind ExternalKind => WasmExternalKind.Table;
    }
}
