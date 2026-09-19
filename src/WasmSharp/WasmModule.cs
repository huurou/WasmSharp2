using System.Collections.Immutable;
using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Modules;
using WasmSharp.Modules.Definitions;
using WasmSharp.Modules.Imports;

namespace WasmSharp;

/// <summary>
/// 静的なmodule定義を表現するクラス
/// </summary>
public sealed class WasmModule
{
    private bool isValidated_;

    /// <summary>
    /// 検証成功時に確定するordinalのexport名と関数indexの対応
    /// </summary>
    internal ImmutableDictionary<string, int> FunctionExportIndices { get; private set; } =
        ImmutableDictionary.Create<string, int>(StringComparer.Ordinal);

    /// <summary>
    /// 全関数の検証成功時に設定する関数index順の実行コード
    /// </summary>
    internal ImmutableArray<FunctionCode> FunctionCodes { get; set; } = [];

    /// <summary>
    /// 型index順の関数型を保持する不変配列
    /// </summary>
    internal ImmutableArray<WasmFunctionType> Types { get; }

    /// <summary>
    /// importを含まない定義順のデコード済み関数を保持する不変配列
    /// </summary>
    internal ImmutableArray<DecodedFunction> Functions { get; }

    /// <summary>
    /// 4種のexportの宣言を保持する不変配列
    /// </summary>
    internal ImmutableArray<ModuleExport> Exports { get; }

    /// <summary>
    /// 宣言順のimportを保持する不変配列
    /// </summary>
    internal ImmutableArray<ModuleImport> Imports { get; }

    /// <summary>
    /// 定義順のtable型を保持する不変配列
    /// </summary>
    internal ImmutableArray<TableDefinition> Tables { get; }

    /// <summary>
    /// 定義順のmemory型を保持する不変配列
    /// </summary>
    internal ImmutableArray<MemoryDefinition> Memories { get; }

    /// <summary>
    /// 定義順のglobal型と未評価の初期化式を保持する不変配列
    /// </summary>
    internal ImmutableArray<GlobalDefinition> Globals { get; }

    /// <summary>
    /// 省略可能なstart宣言
    /// </summary>
    internal StartDefinition? Start { get; }

    /// <summary>
    /// デコード元の入力バイナリのバイト数
    /// </summary>
    internal long InputLength { get; }

    /// <summary>
    /// デコードした各定義をコピーしてmodule定義を構築する
    /// </summary>
    /// <param name="types">型index順の関数型</param>
    /// <param name="functions">importを含まない定義順の関数</param>
    /// <param name="exports">4種のexportの宣言</param>
    /// <param name="inputLength">入力バイナリのバイト数</param>
    /// <param name="imports">宣言順のimport</param>
    /// <param name="tables">定義順のtable型</param>
    /// <param name="memories">定義順のmemory型</param>
    /// <param name="globals">定義順のglobal型と初期化式</param>
    /// <param name="start">省略可能なstart宣言</param>
    internal WasmModule(
        ReadOnlySpan<WasmFunctionType> types,
        ReadOnlySpan<DecodedFunction> functions,
        ReadOnlySpan<ModuleExport> exports,
        long inputLength,
        ReadOnlySpan<ModuleImport> imports,
        ReadOnlySpan<TableDefinition> tables,
        ReadOnlySpan<MemoryDefinition> memories,
        ReadOnlySpan<GlobalDefinition> globals,
        StartDefinition? start
    )
    {
        Types = [.. types];
        Functions = [.. functions];
        Exports = [.. exports];
        Imports = [.. imports];
        Tables = [.. tables];
        Memories = [.. memories];
        Globals = [.. globals];
        Start = start;
        InputLength = inputLength;
    }

    /// <summary>
    /// 入力バイト列をmoduleにデコードする
    /// </summary>
    /// <param name="bytes">入力バイト列</param>
    /// <returns>デコードされたモジュール</returns>
    public static WasmModule Decode(ReadOnlySpan<byte> bytes)
    {
        return ModuleDecoder.Decode(bytes);
    }

    /// <summary>
    /// 入力ストリームをmoduleにデコードする
    /// </summary>
    /// <param name="stream">入力ストリーム</param>
    /// <returns>デコードされたmodule</returns>
    public static WasmModule Decode(Stream stream)
    {
        if (!stream.CanRead)
        {
            throw new ArgumentException("入力ストリームが読み取り不可でした。", nameof(stream));
        }

        using var buffer = new MemoryStream();
        Span<byte> chunk = stackalloc byte[4096];
        while (true)
        {
            var count = stream.Read(chunk);
            if (count == 0)
            {
                break;
            }

            // 入力元のLengthやseekに依存せず、実際に読んだ量で保持上限を確認する。
            if (count > Array.MaxLength - buffer.Length)
            {
                throw new WasmImplementationLimitException(
                    "入力バイナリが保持上限を超えています。",
                    WasmImplementationLimitReason.InputSize,
                    Array.MaxLength,
                    new WasmFailureLocation(WasmProcessingStage.Decode, buffer.Length)
                );
            }

            buffer.Write(chunk[..count]);
        }

        return Decode(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
    }

    /// <summary>
    /// moduleがWasmの型規則と構造規則を満たすか検証する
    /// </summary>
    /// <returns>検証済みのmodulle</returns>
    public WasmModule Validate()
    {
        if (isValidated_)
        {
            return this;
        }

        RequireSupportedDefinitions();
        var functionCodes = ModuleValidator.Validate(this);
        var functionExportIndices = Exports.ToImmutableDictionary(
            x => x.Name,
            x => (int)x.Index,
            StringComparer.Ordinal
        );
        // 全成果が揃ってから反映し、最後に検証成功状態にする。
        FunctionCodes = functionCodes;
        FunctionExportIndices = functionExportIndices;
        isValidated_ = true;

        return this;
    }

    private void RequireSupportedDefinitions()
    {
        // 対応する意味検証が揃うまでは、静的定義を旧検証経路で無視しない。
        if (!Imports.IsEmpty)
        {
            throw UnsupportedDefinition("section.import", Imports[0].ByteOffset, 2);
        }
        if (!Tables.IsEmpty)
        {
            throw UnsupportedDefinition("section.table", Tables[0].ByteOffset, 4);
        }
        if (!Memories.IsEmpty)
        {
            throw UnsupportedDefinition("section.memory", Memories[0].ByteOffset, 5);
        }
        if (!Globals.IsEmpty)
        {
            throw UnsupportedDefinition("section.global", Globals[0].ByteOffset, 6);
        }
        foreach (var export in Exports)
        {
            if (export.Kind != WasmExternalKind.Function)
            {
                throw UnsupportedDefinition(
                    $"export.{export.Kind.ToString().ToLowerInvariant()}",
                    export.ByteOffset,
                    7
                );
            }
        }
        if (Start is { } start)
        {
            throw UnsupportedDefinition("section.start", start.ByteOffset, 8);
        }
    }

    private WasmUnsupportedFeatureException UnsupportedDefinition(
        string feature,
        long offset,
        byte sectionId
    )
    {
        return new WasmUnsupportedFeatureException(
            "この定義の検証は未実装です。",
            feature,
            new WasmFailureLocation(WasmProcessingStage.Validate, offset, null, sectionId),
            [
                new WasmUnverifiedRange(
                    WasmProcessingStage.Validate,
                    0,
                    InputLength,
                    "入力全体の検証が未完了です。"
                ),
            ]
        );
    }

    /// <summary>
    /// host moduleでimportを解決しmoduleをインスタンス化する
    /// </summary>
    /// <param name="hostModules">importする関数やリソースを提供するhost moduleのリスト</param>
    /// <param name="options">実行時のオプション設定</param>
    /// <returns>instance</returns>
    public WasmInstance Instantiate(
        ReadOnlySpan<WasmHostModule> hostModules,
        WasmExecutionOptions? options = default
    )
    {
        if (!isValidated_)
        {
            throw new InvalidOperationException("インスタンス化には検証の成功が必要です。");
        }

        return new WasmInstance(this, options ?? WasmExecutionOptions.Default);
    }
}
