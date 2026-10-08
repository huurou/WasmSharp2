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
    /// <exception cref="WasmDecodeException">対応する構文の符号化、sectionの構成またはfunctionとcodeの件数が不正な場合</exception>
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
    /// <exception cref="WasmDecodeException">対応する構文の符号化、sectionの構成またはfunctionとcodeの件数が不正な場合</exception>
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
        StartDefinition? start = null;
        List<uint> functionTypes = [];
        List<DecodedFunction> functions = [];
        var previousRank = 0;
        while (reader.Remaining != 0)
        {
            var offset = reader.Position;
            var section = ModuleBinaryFormat.ReadSection(ref reader, ref previousRank);
            var id = section.SectionId!.Value;

            if (id == 11 && functions.Count != functionTypes.Count)
            {
                // data以降にcodeは置けないので、その内容が未対応でも件数不一致は確定している。
                throw reader.Error(FUNCTION_CODE_LENGTH_MESSAGE, offset);
            }

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
                    functions = ReadCode(
                        ref section,
                        functionTypes,
                        (uint)imports.Count(x => x.Kind == WasmExternalKind.Function),
                        bytes.Length
                    );
                    break;

                default:
                    var feature = id switch
                    {
                        9 => "section.element",
                        11 => "section.data",
                        12 => "section.data_count",
                        _ => throw new InvalidOperationException(),
                    };
                    throw Unsupported(ref section, feature, bytes.Length, offset);
            }

            ModuleBinaryFormat.RequireEnd(ref section);
        }

        if (functions.Count != functionTypes.Count)
        {
            throw reader.Error(FUNCTION_CODE_LENGTH_MESSAGE);
        }

        return new WasmModule(
            CollectionsMarshal.AsSpan(types),
            CollectionsMarshal.AsSpan(functions),
            CollectionsMarshal.AsSpan(exports),
            bytes.Length,
            CollectionsMarshal.AsSpan(imports),
            CollectionsMarshal.AsSpan(tables),
            CollectionsMarshal.AsSpan(memories),
            CollectionsMarshal.AsSpan(globals),
            start
        );
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
            var initializer = ReadInstructions(ref reader, inputLength);
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
    /// function sectionと件数が一致するcode sectionから、定義関数の本体を取得する
    /// </summary>
    /// <param name="reader">code sectionの読み取り状態 関数本体の終端まで進める</param>
    /// <param name="functionTypes">function sectionから取得した定義順の型index</param>
    /// <param name="importedFunctionCount">診断に使用するmodule全体の関数indexの先頭となるimport関数数</param>
    /// <param name="inputLength">未確認範囲の終端に使用する入力全体のバイト数</param>
    /// <returns>型index、関数本体の位置、圧縮local宣言と入力命令を保持する定義順の関数</returns>
    /// <exception cref="WasmDecodeException">件数が一致しないか、関数本体の長さまたは内容の符号化が不正な場合</exception>
    /// <exception cref="ModuleReadBoundaryException">限定された範囲内で構文を読み終えられない場合</exception>
    /// <exception cref="WasmUnsupportedFeatureException">関数本体で未実装の命令に遭遇した場合</exception>
    /// <exception cref="WasmImplementationLimitException">宣言件数または命令数がコレクションの保持上限を超える場合</exception>
    private static List<DecodedFunction> ReadCode(
        ref ModuleBinaryReader reader,
        List<uint> functionTypes,
        uint importedFunctionCount,
        int inputLength
    )
    {
        var countOffset = reader.Position;
        var count = ModuleBinaryFormat.ReadCount(ref reader);
        if (count != functionTypes.Count)
        {
            throw reader.Error(FUNCTION_CODE_LENGTH_MESSAGE, countOffset);
        }

        List<DecodedFunction> functions = [];
        for (uint index = 0; index < count; index++)
        {
            var length = reader.ReadLength();
            var body = reader.ReadRange(length, importedFunctionCount + index);
            var offset = body.Position;
            var locals = ReadLocals(ref body);
            var instructions = ReadInstructions(ref body, inputLength);
            ModuleBinaryFormat.RequireEnd(ref body);
            functions.Add(
                new DecodedFunction(
                    functionTypes[(int)index],
                    offset,
                    CollectionsMarshal.AsSpan(locals),
                    CollectionsMarshal.AsSpan(instructions)
                )
            );
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
    /// 式の入力命令をデコードし、最初のendを含む命令列を取得する
    /// </summary>
    /// <remarks>命令の型検証と線形実行コードの生成は行わない</remarks>
    /// <param name="reader">式の先頭にある読み取り状態 endの直後まで進める</param>
    /// <param name="inputLength">未確認範囲の終端に使用する入力全体のバイト数</param>
    /// <returns>即値、indexと入力上の位置を持つ、endまでの対応済み命令列</returns>
    /// <exception cref="WasmDecodeException">命令や即値の符号化が不正か、対応するifのないelseがある場合</exception>
    /// <exception cref="ModuleReadBoundaryException">限定された範囲内で構文を読み終えられないか、endが欠落している場合</exception>
    /// <exception cref="WasmUnsupportedFeatureException">割り当て済みの未実装命令に遭遇した場合</exception>
    /// <exception cref="WasmImplementationLimitException">命令数がコレクションの保持上限を超える場合</exception>
    private static List<DecodedInstruction> ReadInstructions(
        ref ModuleBinaryReader reader,
        int inputLength
    )
    {
        List<DecodedInstruction> instructions = [];
        while (reader.TryPeekByte(out var code) && code is not (0x05 or 0x0B))
        {
            var offset = reader.Position;
            reader.ReadByte();

            var opcode = code is 0xFC or 0xFD
                ? new OpcodeKey(code, reader.ReadU32())
                : new OpcodeKey(0, code);
            if (!InstructionSet.TryGet(opcode, out var descriptor))
            {
                // 固定参照は0xFCだけprefixを表示し、0xFDは命令番号だけを表示する。
                var opcodeText =
                    opcode.Prefix == 0xFC
                        ? $"{opcode.Prefix:x2} {opcode.Code:x2}"
                        : $"{opcode.Code:x2}";
                throw reader.Error(
                    $"illegal opcode {opcodeText}: Core 2.0に割り当てられていないopcodeです。",
                    offset
                );
            }

            var immediateKind = descriptor.Immediate;
            if (immediateKind == ImmediateKind.Unsupported)
            {
                throw Unsupported(ref reader, descriptor.Name, inputLength, offset);
            }

            var index = immediateKind == ImmediateKind.Index ? reader.ReadU32() : 0;
            var immediate = immediateKind switch
            {
                ImmediateKind.None or ImmediateKind.Index => default,
                ImmediateKind.I32 => WasmValue.FromI32(reader.ReadS32()),
                ImmediateKind.I64 => WasmValue.FromI64(reader.ReadS64()),
                ImmediateKind.F32Bits => WasmValue.FromF32Bits(reader.ReadF32Bits()),
                ImmediateKind.F64Bits => WasmValue.FromF64Bits(reader.ReadF64Bits()),
                _ => throw new InvalidOperationException("対応済み命令の即値情報が不正です。"),
            };
            AddInstruction(
                instructions,
                new DecodedInstruction(opcode, immediate, offset, index),
                ref reader
            );
        }

        var endOffset = reader.Position;
        if (reader.ReadByte() != 0x0B)
        {
            throw reader.Error("END opcode expected: 式の終端にENDが必要です。", endOffset);
        }

        AddInstruction(
            instructions,
            new DecodedInstruction(new OpcodeKey(0, 0x0B), default, endOffset, 0),
            ref reader
        );
        return instructions;
    }

    /// <summary>
    /// 保持上限を確認してデコード済み命令を追加する
    /// </summary>
    /// <param name="instructions">式の命令を保持する一覧</param>
    /// <param name="instruction">追加する命令</param>
    /// <param name="reader">診断位置に使用する読取り状態 位置は変更しない</param>
    private static void AddInstruction(
        List<DecodedInstruction> instructions,
        DecodedInstruction instruction,
        ref ModuleBinaryReader reader
    )
    {
        if (instructions.Count == Array.MaxLength)
        {
            throw new WasmImplementationLimitException(
                "命令数が配列の保持上限を超えています。",
                WasmImplementationLimitReason.CollectionSize,
                Array.MaxLength,
                reader.Location(instruction.ByteOffset)
            );
        }

        instructions.Add(instruction);
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

    /// <summary>
    /// 未実装の構文と、残るデコード・検証の未確認範囲を持つ診断を作成する
    /// </summary>
    /// <param name="reader">診断に使用する関数とsectionの情報を持つ読み取り状態 位置は変更しない</param>
    /// <param name="feature">未実装のsectionまたは命令を識別する名前</param>
    /// <param name="inputLength">未確認範囲の終端となる入力全体のバイト数</param>
    /// <param name="offset">未実装の構文が始まる、Decode開始位置を0としたバイト位置</param>
    /// <returns>デコード未完了の後続範囲と、入力全体の検証未実施を保持する例外 このメソッドでは送出しない</returns>
    private static WasmUnsupportedFeatureException Unsupported(
        ref ModuleBinaryReader reader,
        string feature,
        int inputLength,
        long offset
    )
    {
        return new WasmUnsupportedFeatureException(
            "未実装の機能に遭遇しました。",
            feature,
            reader.Location(offset),
            [
                new WasmUnverifiedRange(
                    WasmProcessingStage.Decode,
                    offset,
                    inputLength,
                    "この構文以降のデコードが未完了です。"
                ),
                new WasmUnverifiedRange(
                    WasmProcessingStage.Validate,
                    0,
                    inputLength,
                    "入力全体の検証が未実施です。"
                ),
            ]
        );
    }
}
