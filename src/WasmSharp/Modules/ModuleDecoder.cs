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
    internal static WasmModule Decode(ReadOnlySpan<byte> bytes)
    {
        var reader = new ModuleBinaryReader(bytes);
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
                throw reader.Error("functionとcodeの件数が一致しません。", offset);
            }

            switch (id)
            {
                case 0:
                    section.ReadName();
                    section.ReadBytes((uint)section.Remaining);
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
            throw reader.Error("functionとcodeの件数が一致しません。");
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
            throw reader.Error("functionとcodeの件数が一致しません。", countOffset);
        }

        List<DecodedFunction> functions = [];
        for (uint index = 0; index < count; index++)
        {
            var length = reader.ReadU32();
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

    private static List<LocalDeclaration> ReadLocals(ref ModuleBinaryReader reader)
    {
        var count = ModuleBinaryFormat.ReadCount(ref reader);
        List<LocalDeclaration> locals = [];
        ulong total = 0;
        for (uint index = 0; index < count; index++)
        {
            var offset = reader.Position;
            var localCount = reader.ReadU32();
            var type = ModuleBinaryFormat.ReadValueType(ref reader);
            total += localCount;
            if (total > uint.MaxValue)
            {
                throw reader.Error("localsの合計がCore 2.0の上限を超えています。", offset);
            }

            locals.Add(new LocalDeclaration(localCount, type));
        }

        return locals;
    }

    private static List<DecodedInstruction> ReadInstructions(
        ref ModuleBinaryReader reader,
        int inputLength
    )
    {
        List<DecodedInstruction> instructions = [];
        while (reader.Remaining != 0)
        {
            var offset = reader.Position;
            var code = reader.ReadByte();
            if (code == 0x05)
            {
                throw reader.Error("対応するifのないelseです。", offset);
            }

            var opcode = code is 0xFC or 0xFD
                ? new OpcodeKey(code, reader.ReadU32())
                : new OpcodeKey(0, code);
            if (!InstructionSet.TryGet(opcode, out var descriptor))
            {
                throw reader.Error("Core 2.0に割り当てられていないopcodeです。", offset);
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
            if (instructions.Count == Array.MaxLength)
            {
                throw new WasmImplementationLimitException(
                    "命令数が配列の保持上限を超えています。",
                    WasmImplementationLimitReason.CollectionSize,
                    Array.MaxLength,
                    reader.Location(offset)
                );
            }

            instructions.Add(new DecodedInstruction(opcode, immediate, offset, index));
            if (descriptor.Validation == ValidationRule.FunctionEnd)
            {
                return instructions;
            }
        }

        throw reader.Error("式のendがありません。");
    }

    private static List<ModuleExport> ReadExports(ref ModuleBinaryReader reader)
    {
        var count = ModuleBinaryFormat.ReadCount(ref reader);
        List<ModuleExport> exports = [];
        for (uint index = 0; index < count; index++)
        {
            var offset = reader.Position;
            var name = reader.ReadName();
            var kind = ModuleBinaryFormat.ReadExternalKind(ref reader);
            exports.Add(new ModuleExport(name, reader.ReadU32(), offset, kind));
        }

        return exports;
    }

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
