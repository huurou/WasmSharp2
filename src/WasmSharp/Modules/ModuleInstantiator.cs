using System.Collections.Immutable;
using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Instructions;
using WasmSharp.Modules.ExternalValues;
using WasmSharp.Modules.Imports;

namespace WasmSharp.Modules;

/// <summary>
/// importの照合、リソースの構築、startの実行によってinstanceを生成する
/// </summary>
internal static class ModuleInstantiator
{
    /// <summary>
    /// 検証済みmoduleのリソースを構築し、startの実行を完了したinstanceを返す
    /// </summary>
    /// <param name="module">静的検証が成功したmodule</param>
    /// <param name="imports">名前で参照する関数と共有リソースの提供登録</param>
    /// <param name="options">生成するinstanceが新しい実行コンテキストを作る際のポリシー</param>
    /// <returns>startがあればその実行も正常に終了したinstance</returns>
    /// <remarks>
    /// importした実体は共有し、module内のリソース定義には新しい実体を割り当てる。
    /// importの照合やリソースの構築に失敗した場合はstartを呼ばない。
    /// startは構築済みのリソースとexportへアクセスできる。startが失敗しても、
    /// ホスト側へ保存された参照や実行済みの変更は取り消さない
    /// </remarks>
    /// <exception cref="WasmInstantiateException">importの名前・種類・型が提供登録と一致しない場合</exception>
    /// <exception cref="WasmImplementationLimitException">リソースの構築またはstartの実行に必要な保持数が実装上限を超える場合</exception>
    /// <exception cref="WasmTrapException">startの実行結果がtrapの場合</exception>
    /// <exception cref="WasmExhaustionException">startの実行結果が資源枯渇の場合</exception>
    /// <exception cref="InvalidOperationException">startから呼んだホスト関数の結果が宣言型に一致しないか、nullの場合</exception>
    internal static WasmInstance Instantiate(
        WasmModule module,
        WasmImports imports,
        WasmExecutionOptions options
    )
    {
        var linked = Link(module, imports);
        var tables = CreateIndex(
            linked.OfType<TableExternalValue>().Select(x => x.Value),
            module.Tables.Length,
            4
        );
        foreach (var table in module.Tables)
        {
            try
            {
                tables.Add(new WasmTable(table.ElementKind, table.Limits));
            }
            catch (WasmImplementationLimitException exception)
            {
                throw new WasmImplementationLimitException(
                    exception.Message,
                    exception.Reason,
                    exception.Limit,
                    new WasmFailureLocation(
                        WasmProcessingStage.Instantiate,
                        table.ByteOffset,
                        null,
                        4
                    ),
                    exception
                );
            }
        }
        var memories = CreateIndex(
            linked.OfType<MemoryExternalValue>().Select(x => x.Value),
            module.Memories.Length,
            5
        );
        foreach (var memory in module.Memories)
        {
            memories.Add(new WasmMemory(memory.Limits));
        }
        var globals = CreateIndex(
            linked.OfType<GlobalExternalValue>().Select(x => x.Value),
            module.Globals.Length,
            6
        );
        foreach (var global in module.Globals)
        {
            var initializer = global.Initializer[0];
            var value =
                initializer.Opcode == new OpcodeKey(0, 0x23)
                    ? globals[(int)initializer.Index].Value
                    : initializer.Immediate;
            globals.Add(new WasmGlobal(global.Type, value));
        }
        var instance = new WasmInstance(
            module,
            options,
            [.. linked.OfType<FunctionExternalValue>().Select(x => x.Value)],
            globals.MoveToImmutable(),
            memories.MoveToImmutable(),
            tables.MoveToImmutable()
        );
        if (module.Start is { } start)
        {
            ExecutionBoundary.RunStart(instance, instance.Functions[(int)start.FunctionIndex]);
        }
        return instance;
    }

    /// <summary>
    /// importした実体を先頭に持ち、定義した実体を追加できる実体表を作る
    /// </summary>
    /// <typeparam name="T">共有するリソースの型</typeparam>
    /// <param name="imports">宣言順に並んだimportの実体</param>
    /// <param name="definitionCount">後から追加する定義の数</param>
    /// <param name="sectionId">保持上限を超えた場合の診断に用いるセクションID</param>
    /// <returns>全実体を保持できる容量を確保し、importだけを追加した実体表</returns>
    /// <exception cref="WasmImplementationLimitException">importと定義の合計数が配列の保持上限を超える場合</exception>
    private static ImmutableArray<T>.Builder CreateIndex<T>(
        IEnumerable<T> imports,
        int definitionCount,
        byte sectionId
    )
    {
        var values = imports.ToImmutableArray();
        var count = (long)values.Length + definitionCount;
        if (count > Array.MaxLength)
        {
            throw new WasmImplementationLimitException(
                "実体表が保持上限を超えています。",
                WasmImplementationLimitReason.CollectionSize,
                Array.MaxLength,
                new WasmFailureLocation(WasmProcessingStage.Instantiate, SectionId: sectionId)
            );
        }
        var index = ImmutableArray.CreateBuilder<T>((int)count);
        index.AddRange(values);
        return index;
    }

    /// <summary>
    /// importの名前・種類・型を提供登録と照合し、対応する実体を解決する
    /// </summary>
    /// <param name="module">静的検証が成功したmodule</param>
    /// <param name="imports">照合対象の提供登録</param>
    /// <returns>moduleのimport宣言順に並んだ外部実体</returns>
    /// <remarks>
    /// 提供登録の対応表を固定して照合する。memoryとtableは現在サイズと宣言上限で型を照合する。
    /// リソースの新規生成、内容の変更、ホスト処理の呼び出しは行わない
    /// </remarks>
    /// <exception cref="WasmInstantiateException">対応する名前がないか、種類または型が一致しない場合</exception>
    internal static ImmutableArray<ExternalValue> Link(WasmModule module, WasmImports imports)
    {
        var providers = imports.Snapshot();
        var linked = ImmutableArray.CreateBuilder<ExternalValue>(module.Imports.Length);
        for (var ordinal = 0; ordinal < module.Imports.Length; ordinal++)
        {
            var import = module.Imports[ordinal];
            if (
                !providers.TryGetValue(import.ModuleName, out var items)
                || !items.TryGetValue(import.Name, out var value)
            )
            {
                throw LinkFailure(
                    import,
                    ordinal,
                    WasmInstantiateReason.MissingImport,
                    "名前に一致する提供登録がありません。"
                );
            }
            var actualKind = value switch
            {
                FunctionExternalValue => WasmExternalKind.Function,
                GlobalExternalValue => WasmExternalKind.Global,
                MemoryExternalValue => WasmExternalKind.Memory,
                TableExternalValue => WasmExternalKind.Table,
                _ => throw new InvalidOperationException("外部要素の種類が不明です。"),
            };
            if (actualKind != import.Kind)
            {
                throw LinkFailure(
                    import,
                    ordinal,
                    WasmInstantiateReason.KindMismatch,
                    $"要求種類 {import.Kind} に対して提供種類は {actualKind} です。"
                );
            }
            var matches = (import, value) switch
            {
                (FunctionImport required, FunctionExternalValue provided) => module
                    .Types[(int)required.TypeIndex]
                    .Parameters.SequenceEqual(provided.Value.Type.Parameters)
                    && module
                        .Types[(int)required.TypeIndex]
                        .Results.SequenceEqual(provided.Value.Type.Results),
                (GlobalImport required, GlobalExternalValue provided) => required.Type
                    == provided.Value.Type,
                (MemoryImport required, MemoryExternalValue provided) => MatchesLimits(
                    required.Type.Limits,
                    provided.Value.PageCount,
                    provided.Value.MaximumPages
                ),
                (TableImport required, TableExternalValue provided) => required.Type.ElementKind
                    == provided.Value.ElementType
                    && MatchesLimits(
                        required.Type.Limits,
                        provided.Value.Count,
                        provided.Value.MaximumElements
                    ),
                _ => throw new InvalidOperationException("外部要素の種類が一致しません。"),
            };
            if (!matches)
            {
                throw LinkFailure(
                    import,
                    ordinal,
                    WasmInstantiateReason.TypeMismatch,
                    $"要求型 {DescribeImport(module, import)} に対して提供型は {DescribeValue(value)} です。"
                );
            }
            linked.Add(value);
        }
        return linked.MoveToImmutable();
    }

    /// <summary>
    /// importが要求する型を診断用の文字列へ変換する
    /// </summary>
    /// <param name="module">関数の型indexを解決するmodule</param>
    /// <param name="import">型を表示するimport宣言</param>
    /// <returns>要求する関数型、global型、またはリソースのlimits</returns>
    private static string DescribeImport(WasmModule module, ModuleImport import)
    {
        return import switch
        {
            FunctionImport function => DescribeFunction(module.Types[(int)function.TypeIndex]),
            GlobalImport global => global.Type.ToString(),
            MemoryImport memory => memory.Type.Limits.ToString(),
            TableImport table => $"{table.Type.ElementKind} {table.Type.Limits}",
            _ => throw new InvalidOperationException("importの種類が不明です。"),
        };
    }

    /// <summary>
    /// 提供された実体の型を診断用の文字列へ変換する
    /// </summary>
    /// <param name="value">型を表示する提供実体</param>
    /// <returns>提供する型。memoryとtableの最小値には現在サイズを使用する</returns>
    private static string DescribeValue(ExternalValue value)
    {
        return value switch
        {
            FunctionExternalValue function => DescribeFunction(function.Value.Type),
            GlobalExternalValue global => global.Value.Type.ToString(),
            MemoryExternalValue memory => new WasmLimits(
                memory.Value.PageCount,
                memory.Value.MaximumPages
            ).ToString(),
            TableExternalValue table =>
                $"{table.Value.ElementType} {new WasmLimits(table.Value.Count, table.Value.MaximumElements)}",
            _ => throw new InvalidOperationException("外部要素の種類が不明です。"),
        };
    }

    /// <summary>
    /// 関数の引数型と結果型を診断用の文字列へ変換する
    /// </summary>
    /// <param name="type">表示する関数型</param>
    /// <returns>引数と結果をそれぞれ宣言順に列挙した文字列</returns>
    private static string DescribeFunction(WasmFunctionType type)
    {
        return $"[{string.Join(", ", type.Parameters)}] -> [{string.Join(", ", type.Results)}]";
    }

    /// <summary>
    /// 提供リソースの現在サイズと宣言上限がimportのlimitsを満たすか判定する
    /// </summary>
    /// <param name="required">importが要求するlimits</param>
    /// <param name="current">提供リソースの現在サイズ</param>
    /// <param name="maximum">提供リソースの宣言上限。指定がなければnull</param>
    /// <returns>現在サイズが要求最小値以上で、要求上限があれば宣言上限も存在してその値以下である場合はtrue</returns>
    private static bool MatchesLimits(WasmLimits required, uint current, uint? maximum)
    {
        return current >= required.Minimum
            && (required.Maximum is null || maximum is { } actual && actual <= required.Maximum);
    }

    /// <summary>
    /// importの宣言位置と識別情報を含む照合失敗の例外を作る
    /// </summary>
    /// <param name="import">照合に失敗したimport宣言</param>
    /// <param name="ordinal">importセクション内での宣言順。0始まり</param>
    /// <param name="reason">名前・種類・型のいずれの照合に失敗したかを示す理由</param>
    /// <param name="message">要求と提供の違いを説明するメッセージ</param>
    /// <returns>処理段階をInstantiateとした照合失敗の例外</returns>
    private static WasmInstantiateException LinkFailure(
        ModuleImport import,
        int ordinal,
        WasmInstantiateReason reason,
        string message
    )
    {
        return new WasmInstantiateException(
            message,
            reason,
            ordinal,
            import.ModuleName,
            import.Name,
            import.Kind,
            new WasmFailureLocation(WasmProcessingStage.Instantiate, import.ByteOffset, null, 2)
        );
    }
}
