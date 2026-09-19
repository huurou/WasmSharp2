using System.Runtime.InteropServices;
using WasmSharp.Exceptions;
using WasmSharp.Modules.Definitions;
using WasmSharp.Modules.Imports;

namespace WasmSharp.Modules;

/// <summary>
/// Decodeとimport調査で共有するバイナリ構文の読み取り
/// </summary>
internal static class ModuleBinaryFormat
{
    internal static List<ModuleImport> ReadImports(ref WasmBinaryReader reader)
    {
        var count = ReadCount(ref reader);
        List<ModuleImport> imports = [];
        for (uint index = 0; index < count; index++)
        {
            var offset = reader.Position;
            var moduleName = reader.ReadName();
            var name = reader.ReadName();
            var kind = ReadExternalKind(ref reader);
            imports.Add(
                kind switch
                {
                    WasmExternalKind.Function => new FunctionImport(
                        moduleName,
                        name,
                        reader.ReadU32(),
                        offset
                    ),
                    WasmExternalKind.Table => new TableImport(
                        moduleName,
                        name,
                        ReadTableType(ref reader),
                        offset
                    ),
                    WasmExternalKind.Memory => new MemoryImport(
                        moduleName,
                        name,
                        ReadMemoryType(ref reader),
                        offset
                    ),
                    WasmExternalKind.Global => new GlobalImport(
                        moduleName,
                        name,
                        ReadGlobalType(ref reader),
                        offset
                    ),
                    _ => throw new InvalidOperationException(),
                }
            );
        }
        return imports;
    }

    internal static WasmExternalKind ReadExternalKind(ref WasmBinaryReader reader)
    {
        var offset = reader.Position;
        return reader.ReadByte() switch
        {
            0 => WasmExternalKind.Function,
            1 => WasmExternalKind.Table,
            2 => WasmExternalKind.Memory,
            3 => WasmExternalKind.Global,
            _ => throw reader.Error("Core 2.0に存在しないexternal kindです。", offset),
        };
    }

    internal static TableDefinition ReadTableType(ref WasmBinaryReader reader)
    {
        var offset = reader.Position;
        var elementKind = ReadValueType(ref reader);
        if (elementKind is not (WasmValueKind.FuncRef or WasmValueKind.ExternRef))
        {
            throw reader.Error("tableの要素型が参照型ではありません。", offset);
        }
        return new TableDefinition(elementKind, ReadLimits(ref reader), offset);
    }

    internal static MemoryDefinition ReadMemoryType(ref WasmBinaryReader reader)
    {
        var offset = reader.Position;
        return new MemoryDefinition(ReadLimits(ref reader), offset);
    }

    internal static WasmGlobalType ReadGlobalType(ref WasmBinaryReader reader)
    {
        var valueKind = ReadValueType(ref reader);
        var offset = reader.Position;
        var isMutable = reader.ReadByte() switch
        {
            0 => false,
            1 => true,
            _ => throw reader.Error("globalの可変性の符号化が不正です。", offset),
        };
        return new WasmGlobalType(valueKind, isMutable);
    }

    private static WasmLimits ReadLimits(ref WasmBinaryReader reader)
    {
        var offset = reader.Position;
        var flags = reader.ReadByte();
        if (flags > 1)
        {
            throw reader.Error(
                "最大値の有無を示すフラグが不正です。0x00または0x01が必要です。",
                offset
            );
        }
        var minimum = reader.ReadU32();
        return new WasmLimits(minimum, flags == 1 ? reader.ReadU32() : null);
    }

    internal static void ReadHeader(ref WasmBinaryReader reader)
    {
        ReadOnlySpan<byte> header = [0x00, 0x61, 0x73, 0x6D, 0x01, 0x00, 0x00, 0x00];
        foreach (var expected in header)
        {
            var offset = reader.Position;
            if (reader.ReadByte() != expected)
            {
                throw reader.Error("magicまたはバイナリversionが不正です。", offset);
            }
        }
    }

    internal static WasmBinaryReader ReadSection(ref WasmBinaryReader reader, ref int previousRank)
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
        return section;
    }

    internal static List<WasmFunctionType> ReadTypes(ref WasmBinaryReader reader)
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
                new WasmFunctionType(
                    CollectionsMarshal.AsSpan(parameters),
                    CollectionsMarshal.AsSpan(results)
                )
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

    internal static WasmValueKind ReadValueType(ref WasmBinaryReader reader)
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

    internal static uint ReadCount(ref WasmBinaryReader reader)
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

    internal static void RequireEnd(ref WasmBinaryReader reader)
    {
        if (reader.Remaining != 0)
        {
            throw reader.Error("宣言された範囲に余剰のバイトがあります。");
        }
    }
}
