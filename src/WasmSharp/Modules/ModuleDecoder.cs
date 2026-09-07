using System.Runtime.InteropServices;
using WasmSharp.Exceptions;
using WasmSharp.Instructions;

namespace WasmSharp.Modules;

/// <summary>
/// バイナリの構文を読み取り、入力から独立した静的module定義を構築する
/// </summary>
internal static class ModuleDecoder
{
    internal static WasmModule Decode(ReadOnlySpan<byte> bytes)
    {
        var reader = new WasmBinaryReader(bytes);
        ReadOnlySpan<byte> header = [0x00, 0x61, 0x73, 0x6D, 0x01, 0x00, 0x00, 0x00];
        foreach (var expected in header)
        {
            var offset = reader.Position;
            if (reader.ReadByte() != expected)
            {
                throw reader.Error("magicまたはバイナリversionが不正です。", offset);
            }
        }

        List<WasmFunctionType> types = [];
        List<FunctionExport> exports = [];
        List<uint> functionTypes = [];
        List<DecodedFunction> functions = [];
        var previousRank = 0;
        while (reader.Remaining != 0)
        {
            var offset = reader.Position;
            var id = reader.ReadByte();
            reader.SectionId = id;
            if (id > 12)
            {
                throw reader.Error("Core 2.0に存在しないsection IDです。", offset);
            }

            var length = reader.ReadU32();
            var section = reader.ReadRange(length);
            if (id != 0)
            {
                // data countはID順と異なり、elementとcodeの間に置かれる。
                var rank = id switch
                {
                    12 => 10,
                    10 => 11,
                    11 => 12,
                    _ => id,
                };
                if (rank <= previousRank)
                {
                    throw reader.Error("sectionの順序または重複が不正です。", offset);
                }

                previousRank = rank;
            }

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
                    types = ReadTypes(ref section);
                    break;
                case 7:
                    exports = ReadExports(ref section, bytes.Length);
                    break;
                case 3:
                    functionTypes = ReadFunctionTypes(ref section);
                    break;
                case 10:
                    functions = ReadCode(ref section, functionTypes, bytes.Length);
                    break;
                default:
                    var feature = id switch
                    {
                        2 => "section.import",
                        4 => "section.table",
                        5 => "section.memory",
                        6 => "section.global",
                        8 => "section.start",
                        9 => "section.element",
                        11 => "section.data",
                        12 => "section.data_count",
                        _ => throw new InvalidOperationException(),
                    };
                    throw Unsupported(ref section, feature, bytes.Length, offset);
            }

            RequireEnd(ref section);
        }

        if (functions.Count != functionTypes.Count)
        {
            throw reader.Error("functionとcodeの件数が一致しません。");
        }

        return new(
            CollectionsMarshal.AsSpan(types),
            CollectionsMarshal.AsSpan(functions),
            CollectionsMarshal.AsSpan(exports),
            bytes.Length
        );
    }

    private static List<uint> ReadFunctionTypes(ref WasmBinaryReader reader)
    {
        var count = ReadCount(ref reader);
        List<uint> types = [];
        for (uint index = 0; index < count; index++)
        {
            types.Add(reader.ReadU32());
        }

        return types;
    }

    private static List<DecodedFunction> ReadCode(
        ref WasmBinaryReader reader,
        List<uint> functionTypes,
        int inputLength
    )
    {
        var countOffset = reader.Position;
        var count = ReadCount(ref reader);
        if (count != functionTypes.Count)
        {
            throw reader.Error("functionとcodeの件数が一致しません。", countOffset);
        }

        List<DecodedFunction> functions = [];
        for (uint index = 0; index < count; index++)
        {
            var length = reader.ReadU32();
            var body = reader.ReadRange(length, index);
            var offset = body.Position;
            var locals = ReadLocals(ref body);
            var instructions = ReadInstructions(ref body, inputLength);
            functions.Add(
                new(
                    functionTypes[(int)index],
                    offset,
                    CollectionsMarshal.AsSpan(locals),
                    CollectionsMarshal.AsSpan(instructions)
                )
            );
        }

        return functions;
    }

    private static List<LocalDeclaration> ReadLocals(ref WasmBinaryReader reader)
    {
        var count = ReadCount(ref reader);
        List<LocalDeclaration> locals = [];
        ulong total = 0;
        for (uint index = 0; index < count; index++)
        {
            var offset = reader.Position;
            var localCount = reader.ReadU32();
            var type = ReadValueType(ref reader);
            total += localCount;
            if (total > uint.MaxValue)
            {
                throw reader.Error("localsの合計がCore 2.0の上限を超えています。", offset);
            }

            locals.Add(new(localCount, type));
        }

        return locals;
    }

    private static List<DecodedInstruction> ReadInstructions(
        ref WasmBinaryReader reader,
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

            if (descriptor.Immediate == ImmediateKind.Unsupported)
            {
                throw Unsupported(ref reader, descriptor.Name, inputLength, offset);
            }

            var immediate = descriptor.Immediate switch
            {
                ImmediateKind.None => default,
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

            instructions.Add(new(opcode, immediate, offset));
            if (descriptor.Validation == ValidationRule.FunctionEnd)
            {
                RequireEnd(ref reader);
                return instructions;
            }
        }

        throw reader.Error("関数本体のendがありません。");
    }

    private static List<WasmFunctionType> ReadTypes(ref WasmBinaryReader reader)
    {
        var count = ReadCount(ref reader);
        List<WasmFunctionType> types = [];
        for (uint index = 0; index < count; index++)
        {
            var offset = reader.Position;
            if (reader.ReadByte() != 0x60)
            {
                throw reader.Error("関数型の形式が不正です。", offset);
            }

            var parameters = ReadValueTypes(ref reader);
            var results = ReadValueTypes(ref reader);
            types.Add(
                new(CollectionsMarshal.AsSpan(parameters), CollectionsMarshal.AsSpan(results))
            );
        }

        return types;
    }

    private static List<WasmValueKind> ReadValueTypes(ref WasmBinaryReader reader)
    {
        var count = ReadCount(ref reader);
        List<WasmValueKind> types = [];
        for (uint index = 0; index < count; index++)
        {
            types.Add(ReadValueType(ref reader));
        }

        return types;
    }

    private static WasmValueKind ReadValueType(ref WasmBinaryReader reader)
    {
        var offset = reader.Position;
        return reader.ReadByte() switch
        {
            0x7F => WasmValueKind.I32,
            0x7E => WasmValueKind.I64,
            0x7D => WasmValueKind.F32,
            0x7C => WasmValueKind.F64,
            0x7B => WasmValueKind.V128,
            0x70 => WasmValueKind.FuncRef,
            0x6F => WasmValueKind.ExternRef,
            _ => throw reader.Error("Core 2.0に存在しない値型です。", offset),
        };
    }

    private static List<FunctionExport> ReadExports(ref WasmBinaryReader reader, int inputLength)
    {
        var count = ReadCount(ref reader);
        List<FunctionExport> exports = [];
        for (uint index = 0; index < count; index++)
        {
            var offset = reader.Position;
            var name = reader.ReadName();
            var kindOffset = reader.Position;
            var kind = reader.ReadByte();
            if (kind > 3)
            {
                throw reader.Error("Core 2.0に存在しないexternal kindです。", kindOffset);
            }

            if (kind != 0)
            {
                var feature = kind switch
                {
                    1 => "export.table",
                    2 => "export.memory",
                    _ => "export.global",
                };
                throw Unsupported(ref reader, feature, inputLength, kindOffset);
            }

            exports.Add(new(name, reader.ReadU32(), offset));
        }

        return exports;
    }

    private static uint ReadCount(ref WasmBinaryReader reader)
    {
        var offset = reader.Position;
        var count = reader.ReadU32();
        // どの要素にも最低1バイト必要。宣言件数から先に巨大配列を確保しない。
        if (count > (uint)reader.Remaining)
        {
            throw reader.Error("要素数が入力の残量を超えています。", offset);
        }

        if (count > Array.MaxLength)
        {
            throw new WasmImplementationLimitException(
                "要素数が配列の保持上限を超えています。",
                WasmImplementationLimitReason.CollectionSize,
                Array.MaxLength,
                reader.Location(offset)
            );
        }

        return count;
    }

    private static void RequireEnd(ref WasmBinaryReader reader)
    {
        if (reader.Remaining != 0)
        {
            throw reader.Error("宣言された範囲に余剰のバイトがあります。");
        }
    }

    private static WasmUnsupportedFeatureException Unsupported(
        ref WasmBinaryReader reader,
        string feature,
        int inputLength,
        long offset
    )
    {
        return new(
            "未実装の機能に遭遇しました。",
            feature,
            reader.Location(offset),
            [
                new(
                    WasmProcessingStage.Decode,
                    offset,
                    inputLength,
                    "この構文以降のデコードが未完了です。"
                ),
                new(WasmProcessingStage.Validate, 0, inputLength, "入力全体の検証が未実施です。"),
            ]
        );
    }
}
