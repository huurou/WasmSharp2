using System.Runtime.InteropServices;
using WasmSharp.Exceptions;
using WasmSharp.Instructions;
using WasmSharp.Modules.Definitions;
using WasmSharp.Modules.Imports;

namespace WasmSharp.Modules;

/// <summary>
/// バイナリの構文を読み取り、入力から独立した静的module定義を構築する
/// </summary>
internal static class ModuleDecoder
{
    /// <summary>
    /// functionとcodeの件数不一致を示す共通診断
    /// </summary>
    private const string FUNCTION_CODE_LENGTH_MESSAGE =
        "function and code section have inconsistent lengths: functionとcodeの件数が一致しません。";

    /// <summary>
    /// 対応するCore 2.0構文を、入力から独立した静的module定義へデコードする
    /// </summary>
    /// <remarks>型や参照の妥当性の検証と、インスタンス化は行わない</remarks>
    /// <param name="bytes">ヘッダーから始まる入力バイナリ全体</param>
    /// <returns>入力上の診断位置を保持する、未検証のmodule定義</returns>
    /// <exception cref="WasmDecodeException">対応する構文の符号化、sectionの構成、functionとcodeの件数、またはデータ数セクションの件数や要否が不正な場合</exception>
    /// <exception cref="WasmUnsupportedFeatureException">仕様に存在する、未実装のsectionまたは命令に遭遇した場合 入力全体の有効性は保証しない</exception>
    /// <exception cref="WasmImplementationLimitException">宣言件数または命令数がコレクションの保持上限を超える場合</exception>
    internal static WasmModule Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            return DecodeCore(bytes, ModuleReadMode.Bounded);
        }
        catch (ModuleReadBoundaryException exception)
        {
            throw ResolveBoundaryFailure(bytes, exception);
        }
    }

    /// <summary>
    /// 確定した境界失敗について、同じ構文処理を1回だけ使い原因を選択する
    /// </summary>
    /// <param name="bytes">通常解析に使用した入力バイナリ全体</param>
    /// <param name="failure">通常解析で確定した境界失敗</param>
    /// <returns>再走査の構文診断 未対応・実装上限・正常終了の場合は元の診断</returns>
    private static WasmDecodeException ResolveBoundaryFailure(
        ReadOnlySpan<byte> bytes,
        ModuleReadBoundaryException failure
    )
    {
        try
        {
            DecodeCore(bytes, ModuleReadMode.Diagnostic);
        }
        catch (WasmDecodeException exception)
        {
            return exception;
        }
        catch (WasmUnsupportedFeatureException)
        {
            // 未対応の後続構文で、通常解析が確定した境界不正を置き換えない。
        }
        catch (WasmImplementationLimitException)
        {
            // 診断用の追加走査が保持上限に達した場合も、元の不正を保持する。
        }

        return failure.Fallback;
    }

    /// <summary>
    /// 共通の構文処理でヘッダーと各sectionを読み、静的module定義を構築する
    /// </summary>
    /// <param name="bytes">ヘッダーから始まる入力バイナリ全体</param>
    /// <param name="mode">通常解析または境界失敗後の診断用読取り</param>
    /// <returns>入力上の診断位置を保持する、未検証のmodule定義</returns>
    /// <exception cref="WasmDecodeException">対応する構文の符号化、sectionの構成、functionとcodeの件数、またはデータ数セクションの件数や要否が不正な場合</exception>
    /// <exception cref="ModuleReadBoundaryException">限定された範囲内で構文を読み終えられない場合</exception>
    /// <exception cref="WasmUnsupportedFeatureException">仕様に存在する、未実装のsectionまたは命令に遭遇した場合 入力全体の有効性は保証しない</exception>
    /// <exception cref="WasmImplementationLimitException">宣言件数または命令数がコレクションの保持上限を超える場合</exception>
    private static WasmModule DecodeCore(ReadOnlySpan<byte> bytes, ModuleReadMode mode)
    {
        var reader = new ModuleBinaryReader(bytes, mode: mode);
        ModuleBinaryFormat.ReadHeader(ref reader);

        List<WasmFunctionType> types = [];
        List<ModuleExport> exports = [];
        List<ModuleImport> imports = [];
        List<TableDefinition> tables = [];
        List<MemoryDefinition> memories = [];
        List<GlobalDefinition> globals = [];
        List<DataDefinition> dataSegments = [];
        DataCountDefinition? dataCount = null;
        uint declaredDataCount = 0;
        StartDefinition? start = null;
        List<uint> functionTypes = [];
        List<DecodedFunction> functions = [];
        uint declaredCodeCount = 0;
        var previousRank = 0;
        while (reader.Remaining != 0)
        {
            var offset = reader.Position;
            var section = ModuleBinaryFormat.ReadSection(ref reader, ref previousRank);
            var id = section.SectionId!.Value;

            switch (id)
            {
                case 0:
                    section.ReadName();
                    var remaining = section.DeclaredRemaining;
                    if (remaining < 0)
                    {
                        throw section.Error(
                            "unexpected end of section or function: custom名が宣言範囲を超えています。"
                        );
                    }

                    section.ReadBytes((uint)remaining);
                    break;

                case 1:
                    types = ModuleBinaryFormat.ReadTypes(ref section);
                    break;

                case 2:
                    imports = ModuleBinaryFormat.ReadImports(ref section);
                    break;

                case 3:
                    functionTypes = ReadFunctionTypes(ref section);
                    break;

                case 4:
                    tables = ReadTables(ref section);
                    break;

                case 5:
                    memories = ReadMemories(ref section);
                    break;

                case 6:
                    globals = ReadGlobals(ref section, bytes.Length);
                    break;

                case 7:
                    exports = ReadExports(ref section);
                    break;

                case 8:
                    var startOffset = section.Position;
                    start = new StartDefinition(section.ReadU32(), startOffset);
                    break;

                case 10:
                    try
                    {
                        functions = ReadCode(
                            ref section,
                            functionTypes,
                            (uint)imports.Count(x => x.Kind == WasmExternalKind.Function),
                            bytes.Length,
                            out declaredCodeCount
                        );
                    }
                    catch (WasmUnsupportedFeatureException) when (mode == ModuleReadMode.Bounded)
                    {
                        // データ件数はまだ未確定なので、確定した関数件数だけを照合する。
                        RequireSectionCounts(
                            ref section,
                            functionTypes.Count,
                            declaredCodeCount,
                            functions,
                            dataCount,
                            null
                        );
                        throw;
                    }
                    break;

                case 11:
                    try
                    {
                        dataSegments = ReadData(ref section, bytes.Length, out declaredDataCount);
                    }
                    catch (WasmUnsupportedFeatureException) when (mode == ModuleReadMode.Bounded)
                    {
                        // data以降に件数を変更する標準sectionは置けないため、既知の宣言だけで照合する。
                        // 境界失敗後の診断再走査では、未対応より元の構文違反を優先する。
                        RequireSectionCounts(
                            ref section,
                            functionTypes.Count,
                            declaredCodeCount,
                            functions,
                            dataCount,
                            declaredDataCount
                        );
                        throw;
                    }
                    break;

                case 12:
                    dataCount = ReadDataCount(ref section);
                    break;

                default:
                    var feature = id switch
                    {
                        9 => "section.element",
                        _ => throw new InvalidOperationException(),
                    };
                    throw section.Unsupported(feature, bytes.Length, offset);
            }

            ModuleBinaryFormat.RequireEnd(ref section);
        }

        RequireSectionCounts(
            ref reader,
            functionTypes.Count,
            declaredCodeCount,
            functions,
            dataCount,
            declaredDataCount
        );

        return new WasmModule(
            CollectionsMarshal.AsSpan(types),
            CollectionsMarshal.AsSpan(functions),
            CollectionsMarshal.AsSpan(exports),
            bytes.Length,
            CollectionsMarshal.AsSpan(imports),
            CollectionsMarshal.AsSpan(tables),
            CollectionsMarshal.AsSpan(memories),
            CollectionsMarshal.AsSpan(globals),
            start,
            CollectionsMarshal.AsSpan(dataSegments),
            dataCount
        );
    }

    /// <summary>
    /// 構文を読了した時点かデータ部で中断した時点で、確定済みの件数制約を順に照合する
    /// </summary>
    /// <param name="reader">照合時の診断位置を持つ読み取り状態</param>
    /// <param name="functionCount">functionセクションの宣言件数</param>
    /// <param name="codeCount">codeセクションの宣言件数 省略時は0</param>
    /// <param name="functions">コード内のデータ参照を含む、読み取り済みの関数定義</param>
    /// <param name="dataCount">省略可能なデータ数セクションの宣言</param>
    /// <param name="segmentCount">データセクションの宣言件数 省略時は0、まだ読み取れない場合はnull</param>
    /// <exception cref="WasmDecodeException">関数件数、データ件数またはデータ数セクションの要否が不正な場合</exception>
    private static void RequireSectionCounts(
        ref ModuleBinaryReader reader,
        int functionCount,
        uint codeCount,
        List<DecodedFunction> functions,
        DataCountDefinition? dataCount,
        uint? segmentCount
    )
    {
        if (codeCount != functionCount)
        {
            throw reader.Error(FUNCTION_CODE_LENGTH_MESSAGE);
        }

        if (segmentCount is not { } count)
        {
            return;
        }

        RequireDataCountMatch(ref reader, dataCount, count);
        if (
            dataCount is null
            && functions.Any(x =>
                x.Instructions.Any(x =>
                    InstructionSet.TryGet(x.Opcode, out var descriptor)
                    && descriptor.Immediate
                        is ImmediateKind.DataIndexMemoryZero
                            or ImmediateKind.DataIndex
                )
            )
        )
        {
            throw reader.Error(
                "data count section required: データインデックスを使用するコードにはデータ数セクションが必要です。"
            );
        }
    }

    /// <summary>
    /// データセグメントの初期バイト列、任意の初期化先と宣言位置を取得する
    /// </summary>
    /// <param name="reader">データセクションの読み取り状態 宣言の終端まで進める</param>
    /// <param name="inputLength">未確認範囲の終端に使用する入力全体のバイト数</param>
    /// <param name="declaredCount">読み取ったデータセグメントの宣言件数 後続の式が未対応でも保持する</param>
    /// <returns>定義順のデータセグメント メモリの存在とオフセット式の型は検証しない</returns>
    /// <exception cref="WasmDecodeException">件数、フラグ、長さまたはオフセット式の符号化が不正な場合</exception>
    /// <exception cref="ModuleReadBoundaryException">限定された範囲内で構文を読み終えられない場合</exception>
    /// <exception cref="WasmUnsupportedFeatureException">オフセット式で未実装の命令に遭遇した場合</exception>
    /// <exception cref="WasmImplementationLimitException">宣言件数、データ長または命令数がコレクションの保持上限を超える場合</exception>
    internal static List<DataDefinition> ReadData(
        ref ModuleBinaryReader reader,
        int inputLength,
        out uint declaredCount
    )
    {
        declaredCount = ModuleBinaryFormat.ReadCount(ref reader);
        List<DataDefinition> segments = [];
        for (uint index = 0; index < declaredCount; index++)
        {
            var offset = reader.Position;
            var flags = reader.ReadU32();
            ActiveDataTarget? target = null;
            switch (flags)
            {
                case 0:
                case 2:
                    var memoryIndex = flags == 0 ? 0 : reader.ReadU32();
                    var expression = ExpressionDecoder.Read(ref reader, inputLength);
                    target = new ActiveDataTarget(
                        memoryIndex,
                        CollectionsMarshal.AsSpan(expression)
                    );
                    break;

                case 1:
                    break;

                default:
                    throw reader.Error(
                        "malformed data segment kind: データセグメントのフラグが不正です。",
                        reader.Position - 1
                    );
            }

            var length = ModuleBinaryFormat.ReadCount(ref reader);
            segments.Add(new DataDefinition(reader.ReadBytes(length), target, offset));
        }

        return segments;
    }

    /// <summary>
    /// データ数セクションの個数と宣言位置を取得する
    /// </summary>
    /// <param name="reader">データ数セクションの読み取り状態 個数の直後まで進める</param>
    /// <returns>未照合のデータセグメント数と入力位置</returns>
    /// <exception cref="WasmDecodeException">個数の符号化が不正な場合</exception>
    /// <exception cref="ModuleReadBoundaryException">限定された範囲内で個数を読み終えられない場合</exception>
    internal static DataCountDefinition ReadDataCount(ref ModuleBinaryReader reader)
    {
        var offset = reader.Position;
        return new DataCountDefinition(reader.ReadU32(), offset);
    }

    /// <summary>
    /// データ数セクションの宣言がある場合、データセグメントの実数との一致を要求する
    /// </summary>
    /// <param name="reader">照合時の診断位置を持つ読み取り状態</param>
    /// <param name="dataCount">省略可能なデータ数セクションの宣言</param>
    /// <param name="segmentCount">データセクションの省略時は0とするデータセグメントの実数</param>
    /// <exception cref="WasmDecodeException">宣言された個数と実数が一致しない場合</exception>
    internal static void RequireDataCountMatch(
        ref ModuleBinaryReader reader,
        DataCountDefinition? dataCount,
        uint segmentCount
    )
    {
        if (dataCount is { } declaration && declaration.Count != segmentCount)
        {
            throw reader.Error(
                "data count and data section have inconsistent lengths: 宣言されたデータセグメント数と実数が一致しません。"
            );
        }
    }

    /// <summary>
    /// globalの型、未評価の初期化式と宣言位置を取得する
    /// </summary>
    /// <param name="reader">global sectionの読み取り状態 宣言の終端まで進める</param>
    /// <param name="inputLength">未確認範囲の終端に使用する入力全体のバイト数</param>
    /// <returns>定義順のglobal宣言 初期化式の型や使用できる命令は検証しない</returns>
    /// <exception cref="WasmDecodeException">件数、global型または初期化式の符号化が不正な場合</exception>
    /// <exception cref="ModuleReadBoundaryException">限定された範囲内で構文を読み終えられない場合</exception>
    /// <exception cref="WasmUnsupportedFeatureException">初期化式で未実装の命令に遭遇した場合</exception>
    /// <exception cref="WasmImplementationLimitException">宣言件数または命令数がコレクションの保持上限を超える場合</exception>
    private static List<GlobalDefinition> ReadGlobals(
        ref ModuleBinaryReader reader,
        int inputLength
    )
    {
        var count = ModuleBinaryFormat.ReadCount(ref reader);
        List<GlobalDefinition> globals = [];
        for (uint index = 0; index < count; index++)
        {
            var offset = reader.Position;
            var type = ModuleBinaryFormat.ReadGlobalType(ref reader);
            var initializer = ExpressionDecoder.Read(ref reader, inputLength);
            globals.Add(new GlobalDefinition(type, CollectionsMarshal.AsSpan(initializer), offset));
        }
        return globals;
    }

    /// <summary>
    /// table sectionの型宣言を取得する
    /// </summary>
    /// <param name="reader">table sectionの読み取り状態 宣言の終端まで進める</param>
    /// <returns>定義順のtable型 limitsの仕様上の制約は検証しない</returns>
    /// <exception cref="WasmDecodeException">件数またはtable型の符号化が不正な場合</exception>
    /// <exception cref="ModuleReadBoundaryException">限定された範囲内で構文を読み終えられない場合</exception>
    /// <exception cref="WasmImplementationLimitException">宣言件数がコレクションの保持上限を超える場合</exception>
    private static List<TableDefinition> ReadTables(ref ModuleBinaryReader reader)
    {
        var count = ModuleBinaryFormat.ReadCount(ref reader);
        List<TableDefinition> tables = [];
        for (uint index = 0; index < count; index++)
        {
            tables.Add(ModuleBinaryFormat.ReadTableType(ref reader));
        }
        return tables;
    }

    /// <summary>
    /// memory sectionの型宣言を取得する
    /// </summary>
    /// <param name="reader">memory sectionの読み取り状態 宣言の終端まで進める</param>
    /// <returns>定義順のmemory型 limitsやmemoryの個数制約は検証しない</returns>
    /// <exception cref="WasmDecodeException">件数またはmemory型の符号化が不正な場合</exception>
    /// <exception cref="ModuleReadBoundaryException">限定された範囲内で構文を読み終えられない場合</exception>
    /// <exception cref="WasmImplementationLimitException">宣言件数がコレクションの保持上限を超える場合</exception>
    private static List<MemoryDefinition> ReadMemories(ref ModuleBinaryReader reader)
    {
        var count = ModuleBinaryFormat.ReadCount(ref reader);
        List<MemoryDefinition> memories = [];
        for (uint index = 0; index < count; index++)
        {
            memories.Add(ModuleBinaryFormat.ReadMemoryType(ref reader));
        }
        return memories;
    }

    /// <summary>
    /// function sectionの各定義関数が参照する型indexを取得する
    /// </summary>
    /// <param name="reader">function sectionの読み取り状態 宣言の終端まで進める</param>
    /// <returns>定義関数の宣言順に並ぶ型index 参照先の存在は検証しない</returns>
    /// <exception cref="WasmDecodeException">件数または型indexの符号化が不正な場合</exception>
    /// <exception cref="ModuleReadBoundaryException">限定された範囲内で構文を読み終えられない場合</exception>
    /// <exception cref="WasmImplementationLimitException">宣言件数がコレクションの保持上限を超える場合</exception>
    private static List<uint> ReadFunctionTypes(ref ModuleBinaryReader reader)
    {
        var count = ModuleBinaryFormat.ReadCount(ref reader);
        List<uint> types = [];
        for (uint index = 0; index < count; index++)
        {
            types.Add(reader.ReadU32());
        }

        return types;
    }

    /// <summary>
    /// codeセクションの全本体を読み取り、対応するfunction宣言がある定義関数を取得する
    /// </summary>
    /// <param name="reader">code sectionの読み取り状態 関数本体の終端まで進める</param>
    /// <param name="functionTypes">function sectionから取得した定義順の型index</param>
    /// <param name="importedFunctionCount">診断に使用するmodule全体の関数indexの先頭となるimport関数数</param>
    /// <param name="inputLength">未確認範囲の終端に使用する入力全体のバイト数</param>
    /// <param name="declaredCount">読み取ったcode本体の宣言件数 後続の式が未対応でも保持する</param>
    /// <returns>型index、関数本体の位置、圧縮local宣言と入力命令を保持する定義順の関数</returns>
    /// <exception cref="WasmDecodeException">関数本体の長さまたは内容の符号化が不正な場合</exception>
    /// <exception cref="ModuleReadBoundaryException">限定された範囲内で構文を読み終えられない場合</exception>
    /// <exception cref="WasmUnsupportedFeatureException">関数本体で未実装の命令に遭遇した場合</exception>
    /// <exception cref="WasmImplementationLimitException">宣言件数または命令数がコレクションの保持上限を超える場合</exception>
    private static List<DecodedFunction> ReadCode(
        ref ModuleBinaryReader reader,
        List<uint> functionTypes,
        uint importedFunctionCount,
        int inputLength,
        out uint declaredCount
    )
    {
        declaredCount = ModuleBinaryFormat.ReadCount(ref reader);
        List<DecodedFunction> functions = [];
        for (uint index = 0; index < declaredCount; index++)
        {
            var length = reader.ReadLength();
            var body = reader.ReadRange(length, importedFunctionCount + index);
            var offset = body.Position;
            var locals = ReadLocals(ref body);
            var instructions = ExpressionDecoder.Read(ref body, inputLength);
            ModuleBinaryFormat.RequireEnd(ref body);
            // 余剰の本体も構文検査し、型indexのない定義は作らず、読了後の件数照合で拒否する。
            if (index < functionTypes.Count)
            {
                functions.Add(
                    new DecodedFunction(
                        functionTypes[(int)index],
                        offset,
                        CollectionsMarshal.AsSpan(locals),
                        CollectionsMarshal.AsSpan(instructions)
                    )
                );
            }
        }

        return functions;
    }

    /// <summary>
    /// 追加localsの圧縮宣言を取得し、合計個数がCore 2.0の上限以内であることを確認する
    /// </summary>
    /// <param name="reader">関数本体のlocal宣言の先頭にある読み取り状態 宣言の終端まで進める</param>
    /// <returns>入力順の個数と値型の組 個々のlocalへの展開は行わない</returns>
    /// <exception cref="WasmDecodeException">宣言の符号化が不正か、追加localsの合計がu32の範囲を超える場合</exception>
    /// <exception cref="ModuleReadBoundaryException">限定された範囲内で構文を読み終えられない場合</exception>
    /// <exception cref="WasmImplementationLimitException">圧縮宣言の件数がコレクションの保持上限を超える場合</exception>
    private static List<LocalDeclaration> ReadLocals(ref ModuleBinaryReader reader)
    {
        var count = ModuleBinaryFormat.ReadCount(ref reader);
        List<LocalDeclaration> locals = [];
        ulong total = 0;
        long? overflowOffset = null;
        for (uint index = 0; index < count; index++)
        {
            var offset = reader.Position;
            var localCount = reader.ReadU32();
            var type = ModuleBinaryFormat.ReadValueType(ref reader);
            total += localCount;
            if (total > uint.MaxValue)
            {
                overflowOffset ??= offset;
            }

            locals.Add(new LocalDeclaration(localCount, type));
        }

        if (overflowOffset is not null)
        {
            throw reader.Error(
                "too many locals: localsの合計がCore 2.0の上限を超えています。",
                overflowOffset
            );
        }

        return locals;
    }

    /// <summary>
    /// exportの名前、種類、参照indexと宣言位置を取得する
    /// </summary>
    /// <param name="reader">export sectionの読み取り状態 宣言の終端まで進める</param>
    /// <returns>宣言順のexport定義 名前の重複と参照先の存在は検証しない</returns>
    /// <exception cref="WasmDecodeException">件数、名前、種類またはindexの符号化が不正な場合</exception>
    /// <exception cref="ModuleReadBoundaryException">限定された範囲内で構文を読み終えられない場合</exception>
    /// <exception cref="WasmImplementationLimitException">宣言件数がコレクションの保持上限を超える場合</exception>
    private static List<ModuleExport> ReadExports(ref ModuleBinaryReader reader)
    {
        var count = ModuleBinaryFormat.ReadCount(ref reader);
        List<ModuleExport> exports = [];
        for (uint index = 0; index < count; index++)
        {
            var offset = reader.Position;
            var name = reader.ReadName();
            var kind = ModuleBinaryFormat.ReadExternalKind(ref reader, false);
            exports.Add(new ModuleExport(name, reader.ReadU32(), offset, kind));
        }

        return exports;
    }
}
