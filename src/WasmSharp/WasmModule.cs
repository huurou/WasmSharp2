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
    /// <summary>
    /// 静的検証が成功し、実行コードとexport索引が確定しているかどうか
    /// </summary>
    private bool isValidated_;

    /// <summary>
    /// 検証成功時に確定するordinalのexport名と、importを含むmodule全体の関数indexの対応
    /// </summary>
    internal ImmutableDictionary<string, int> FunctionExportIndices { get; private set; } =
        ImmutableDictionary.Create<string, int>(StringComparer.Ordinal);

    /// <summary>
    /// 全関数の検証成功時に設定する、importを含まない定義順の実行コード
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
    /// 入力バイト列から完全なimport情報と未確認範囲を取得する。moduleの有効性は保証しない
    /// </summary>
    /// <param name="bytes">入力バイト列</param>
    /// <returns>完全取得したimport情報と未確認範囲</returns>
    public static WasmImportInspection InspectImports(ReadOnlySpan<byte> bytes)
    {
        return ImportInspector.Inspect(bytes);
    }

    /// <summary>
    /// ストリームの現在位置からimport情報を取得する。入力は閉じず、seekを要求しない
    /// </summary>
    /// <param name="stream">読み取り可能な入力ストリーム</param>
    /// <returns>完全取得したimport情報と未確認範囲</returns>
    public static WasmImportInspection InspectImports(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
        {
            throw new ArgumentException("入力ストリームが読み取り不可でした。", nameof(stream));
        }
        return ImportInspector.Inspect(stream);
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

        var functionCodes = ModuleValidator.Validate(this);
        var functionExportIndices = Exports
            .Where(x => x.Kind == WasmExternalKind.Function)
            .ToImmutableDictionary(x => x.Name, x => (int)x.Index, StringComparer.Ordinal);

        // 全成果が揃ってから反映し、最後に検証成功状態にする。
        FunctionCodes = functionCodes;
        FunctionExportIndices = functionExportIndices;
        isValidated_ = true;

        return this;
    }

    /// <summary>
    /// ホストモジュールの提供登録を使い、startの実行を完了したinstanceを生成する
    /// </summary>
    /// <param name="hostModules">importに対応する名前、関数、リソースを提供するホストモジュール</param>
    /// <param name="options">生成するinstanceの実行ポリシー。nullの場合は<see cref="WasmExecutionOptions.Default"/></param>
    /// <returns>startがあればその実行も正常に終了したinstance</returns>
    /// <remarks>
    /// <see cref="Validate"/>の成功後に呼び出す。提供登録は呼び出し時点の名前対応を使い、関数とリソースの実体は共有する。
    /// startの実行、実行ポリシー、失敗時の参照と変更の扱いは<see cref="Instantiate(WasmImports, WasmExecutionOptions)"/>と同じ
    /// </remarks>
    /// <exception cref="ArgumentNullException">ホストモジュールの要素がnullの場合</exception>
    /// <exception cref="ArgumentException">同じmodule名とitem名の提供登録が重複する場合</exception>
    /// <exception cref="InvalidOperationException">静的検証に成功していないか、ホスト関数の結果がnullまたは宣言型と一致しない場合</exception>
    /// <exception cref="WasmInstantiateException">importの名前・種類・型が提供登録と一致しない場合</exception>
    /// <exception cref="WasmImplementationLimitException">リソースの構築またはstartの実行に必要な保持数が実装上限を超える場合</exception>
    /// <exception cref="WasmTrapException">startの実行結果がtrapの場合</exception>
    /// <exception cref="WasmExhaustionException">startの実行結果が資源枯渇の場合</exception>
    public WasmInstance Instantiate(
        ReadOnlySpan<WasmHostModule> hostModules,
        WasmExecutionOptions? options = default
    )
    {
        RequireValidated();
        var imports = new WasmImports();
        foreach (var hostModule in hostModules)
        {
            imports.Add(hostModule);
        }
        return Instantiate(imports, options);
    }

    /// <summary>
    /// importを結び付け、startの実行を完了したinstanceを生成する
    /// </summary>
    /// <param name="imports">importに対応する名前、関数、リソースの提供登録</param>
    /// <param name="options">生成するinstanceの実行ポリシー。nullの場合は<see cref="WasmExecutionOptions.Default"/></param>
    /// <returns>startがあればその実行も正常に終了したinstance</returns>
    /// <remarks>
    /// <para>
    /// <see cref="Validate"/>の成功後に呼び出す。importの実体は共有し、module内のリソース定義には毎回新しい実体を割り当てる。
    /// startはこの呼び出しにつき1回実行し、構築済みのリソースとexportへアクセスできる。
    /// importの照合やリソースの構築に失敗した場合はstartを実行しない
    /// </para>
    /// <para>
    /// startは進行中の実行コンテキストがあればその上限を共有し、なければこのinstanceのポリシーで実行する。
    /// startが失敗しても、ホストへ保存されたinstanceやリソースの参照は有効で、実行済みの変更は取り消さない。
    /// 実行結果から生成するtrapと資源枯渇の例外は処理段階をInstantiateとする。
    /// ホスト処理の例外と実際のメモリ割当例外は変換せず、そのまま伝播する
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="imports"/>がnullの場合</exception>
    /// <exception cref="InvalidOperationException">静的検証に成功していないか、ホスト関数の結果がnullまたは宣言型と一致しない場合</exception>
    /// <exception cref="WasmInstantiateException">importの名前・種類・型が提供登録と一致しない場合</exception>
    /// <exception cref="WasmImplementationLimitException">リソースの構築またはstartの実行に必要な保持数が実装上限を超える場合</exception>
    /// <exception cref="WasmTrapException">startの実行結果がtrapの場合</exception>
    /// <exception cref="WasmExhaustionException">startの実行結果が資源枯渇の場合</exception>
    public WasmInstance Instantiate(WasmImports imports, WasmExecutionOptions? options = null)
    {
        RequireValidated();
        ArgumentNullException.ThrowIfNull(imports);
        return ModuleInstantiator.Instantiate(
            this,
            imports,
            options ?? WasmExecutionOptions.Default
        );
    }

    /// <summary>
    /// インスタンス化に必要な静的検証が成功していることを確認する
    /// </summary>
    /// <exception cref="InvalidOperationException">静的検証が完了していない場合</exception>
    private void RequireValidated()
    {
        if (!isValidated_)
        {
            throw new InvalidOperationException("インスタンス化には検証の成功が必要です。");
        }
    }
}
