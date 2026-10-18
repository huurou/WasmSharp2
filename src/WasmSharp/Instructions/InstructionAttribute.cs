using System.Linq;

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
    /// 数値命令の入力型列・結果型を取得する 数値以外の宣言ではnull
    /// </summary>
    public NumericSignature? NumericSignature { get; }

    /// <summary>
    /// メモリ命令の値型・アクセス幅・符号拡張の指定を取得する ロード・ストア以外ではnull
    /// </summary>
    public MemoryAccessSignature? MemoryAccessSignature { get; }

    /// <summary>
    /// lane即値の上限を取得または設定する lane即値を持たない場合は0
    /// </summary>
    public byte LaneCount { get; set; }

    /// <summary>
    /// スタック下側から上側の入力型列をコピーして数値命令を宣言する
    /// </summary>
    /// <param name="prefix">通常命令では0、拡張命令では先頭のprefix</param>
    /// <param name="code">prefix内の命令番号</param>
    /// <param name="name">Wasm仕様上の命令名</param>
    /// <param name="immediate">即値の符号化</param>
    /// <param name="stackEffect">検証時のスタック効果</param>
    /// <param name="validation">命令の検証規則</param>
    /// <param name="executionHandler">Interpreterに宣言された静的handler名</param>
    /// <param name="inputTypes">スタック下側から上側の入力数値型列</param>
    /// <param name="resultType">結果の数値型</param>
    public InstructionAttribute(
        byte prefix,
        uint code,
        string name,
        ImmediateKind immediate,
        StackEffectKind stackEffect,
        ValidationRule validation,
        string? executionHandler,
        WasmValueKind[] inputTypes,
        WasmValueKind resultType
    )
        : this(prefix, code, name, immediate, stackEffect, validation, executionHandler)
    {
        NumericSignature = new([.. inputTypes], resultType);
    }

    /// <summary>
    /// 数値命令の型効果と実行handlerを宣言する
    /// </summary>
    /// <param name="prefix">通常命令では0、拡張命令では先頭のprefix</param>
    /// <param name="code">prefix内の命令番号</param>
    /// <param name="name">Wasm仕様上の命令名</param>
    /// <param name="immediate">即値の符号化</param>
    /// <param name="stackEffect">検証時のスタック効果</param>
    /// <param name="validation">命令の検証規則</param>
    /// <param name="executionHandler">Interpreterに宣言された静的handler名</param>
    /// <param name="inputCount">同じ型の入力値の個数</param>
    /// <param name="inputType">入力の数値型</param>
    /// <param name="resultType">結果の数値型</param>
    public InstructionAttribute(
        byte prefix,
        uint code,
        string name,
        ImmediateKind immediate,
        StackEffectKind stackEffect,
        ValidationRule validation,
        string? executionHandler,
        byte inputCount,
        WasmValueKind inputType,
        WasmValueKind resultType
    )
        : this(prefix, code, name, immediate, stackEffect, validation, executionHandler)
    {
        NumericSignature = new([.. Enumerable.Repeat(inputType, inputCount)], resultType);
    }

    /// <summary>
    /// ロード命令・ストア命令のアクセス情報と実行handlerを宣言する
    /// </summary>
    /// <param name="prefix">通常命令では0、拡張命令では先頭のprefix</param>
    /// <param name="code">prefix内の命令番号</param>
    /// <param name="name">Wasm仕様上の命令名</param>
    /// <param name="immediate">即値の符号化</param>
    /// <param name="stackEffect">検証時のスタック効果</param>
    /// <param name="validation">命令の検証規則</param>
    /// <param name="executionHandler">Interpreterに宣言された静的handler名</param>
    /// <param name="valueType">読み書きする値の数値型</param>
    /// <param name="byteWidth">読み書きするバイト数</param>
    /// <param name="signExtend">狭い整数のロードで符号拡張するか</param>
    public InstructionAttribute(
        byte prefix,
        uint code,
        string name,
        ImmediateKind immediate,
        StackEffectKind stackEffect,
        ValidationRule validation,
        string? executionHandler,
        WasmValueKind valueType,
        byte byteWidth,
        bool signExtend
    )
        : this(prefix, code, name, immediate, stackEffect, validation, executionHandler)
    {
        MemoryAccessSignature = new(valueType, byteWidth, signExtend);
    }

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
