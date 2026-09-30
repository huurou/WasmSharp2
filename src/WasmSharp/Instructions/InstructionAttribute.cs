namespace WasmSharp.Instructions;

/// <summary>
/// 命令情報と実行分岐の生成に用いる命令宣言
/// </summary>
/// <param name="prefix">通常命令では0、拡張命令では先頭のprefix</param>
/// <param name="code">prefix内の命令番号</param>
/// <param name="name">Wasm仕様上の命令名</param>
/// <param name="immediate">即値の符号化</param>
/// <param name="stackEffect">検証時のスタック効果</param>
/// <param name="validation">命令の検証規則</param>
/// <param name="executionHandler">Interpreterに宣言された静的handler名 未対応の宣言ではnull</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
internal sealed class InstructionAttribute(
    byte prefix,
    uint code,
    string name,
    ImmediateKind immediate,
    StackEffectKind stackEffect,
    ValidationRule validation,
    string? executionHandler
) : Attribute
{
    /// <summary>
    /// 通常命令では0、拡張命令ではprefixを取得する
    /// </summary>
    public byte Prefix { get; } = prefix;

    /// <summary>
    /// 命令番号を取得する
    /// </summary>
    public uint Code { get; } = code;

    /// <summary>
    /// 仕様上の命令名を取得する
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// 即値の符号化を取得する
    /// </summary>
    public ImmediateKind Immediate { get; } = immediate;

    /// <summary>
    /// スタック効果を取得する
    /// </summary>
    public StackEffectKind StackEffect { get; } = stackEffect;

    /// <summary>
    /// 検証規則を取得する
    /// </summary>
    public ValidationRule Validation { get; } = validation;

    /// <summary>
    /// 対応済み命令の静的handler名を取得する
    /// </summary>
    public string? ExecutionHandler { get; } = executionHandler;

    /// <summary>
    /// 番号と名前のみで未対応の命令を宣言する
    /// </summary>
    /// <param name="prefix">通常命令では0、拡張命令では先頭のprefix</param>
    /// <param name="code">prefix内の命令番号</param>
    /// <param name="name">Wasm仕様上の命令名</param>
    public InstructionAttribute(byte prefix, uint code, string name)
        : this(
            prefix,
            code,
            name,
            ImmediateKind.Unsupported,
            StackEffectKind.Unsupported,
            ValidationRule.Unsupported,
            null
        ) { }
}
