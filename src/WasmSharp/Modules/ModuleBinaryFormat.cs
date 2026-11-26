using System.Collections.Immutable;
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
    // 解決済みのグループを先頭の定義型で共有し、同じ先行グループへの参照を一度で照合する。
    private static readonly IEqualityComparer<WasmDefinedType> __resolvedTypeComparer =
        EqualityComparer<WasmDefinedType>.Create(static (x, y) => x!.HasSameStructure(y!), static x => x.StructuralHashCode);

    /// <summary>
    /// import宣言の名前、種類、未検証の要求型と入力位置を取得する
    /// </summary>
    /// <param name="reader">import sectionの読み取り状態 宣言の終端まで進める</param>
    /// <returns>入力の宣言順に並ぶimport定義 関数型のindexは解決しない</returns>
    /// <exception cref="WasmDecodeException">件数、名前、外部要素の種類または型の符号化が不正な場合</exception>
    /// <exception cref="WasmUnsupportedFeatureException">Core 3.0のタグをimportする場合</exception>
    /// <exception cref="ModuleReadBoundaryException">限定された範囲内で構文を読み終えられない場合</exception>
    /// <exception cref="WasmImplementationLimitException">宣言件数がコレクションの保持上限を超える場合</exception>
    internal static List<ModuleImport> ReadImports(ref ModuleBinaryReader reader)
    {
        var count = ReadCount(ref reader);
        List<ModuleImport> imports = [];
        for (uint index = 0; index < count; index++)
        {
            var offset = reader.Position;
            var moduleName = reader.ReadName();
            var name = reader.ReadName();
            var kind = ReadExternalKind(ref reader, true);
            imports.Add(
                kind switch
                {
                    WasmExternalKind.Function => new FunctionImport(moduleName, name, reader.ReadU32(), offset),
                    WasmExternalKind.Table => new TableImport(moduleName, name, ReadTableType(ref reader), offset),
                    WasmExternalKind.Memory => new MemoryImport(moduleName, name, ReadMemoryType(ref reader), offset),
                    WasmExternalKind.Global => new GlobalImport(moduleName, name, ReadGlobalType(ref reader), offset),
                    _ => throw new InvalidOperationException(),
                }
            );
        }
        return imports;
    }

    /// <summary>
    /// 選択した仕様版に従ってimportまたはexportの種類を1バイトの符号化から取得する
    /// </summary>
    /// <param name="reader">external kindの先頭にある読み取り状態 1バイト進める</param>
    /// <param name="isImport">import宣言の診断を選ぶ場合はtrue、export宣言の場合はfalse</param>
    /// <returns>関数、table、memoryまたはglobalの種類</returns>
    /// <exception cref="WasmDecodeException">選択した仕様版に存在しない種類の場合</exception>
    /// <exception cref="WasmUnsupportedFeatureException">Core 3.0のタグを指定した場合</exception>
    /// <exception cref="ModuleReadBoundaryException">限定された範囲内で構文を読み終えられない場合</exception>
    internal static WasmExternalKind ReadExternalKind(ref ModuleBinaryReader reader, bool isImport)
    {
        var offset = reader.Position;
        return reader.ReadByte() switch
        {
            0 => WasmExternalKind.Function,
            1 => WasmExternalKind.Table,
            2 => WasmExternalKind.Memory,
            3 => WasmExternalKind.Global,
            4 when reader.SpecVersion == WasmSpecVersion.Core3 => throw reader.Unsupported(
                isImport ? "tag import" : "tag export",
                (int)reader.InputEnd,
                offset
            ),
            _ => throw reader.Error(
                isImport
                    ? "malformed import kind: importの種類が不正です。"
                    : "malformed export kind: exportの種類が不正です。",
                offset
            ),
        };
    }

    /// <summary>
    /// tableの参照型とlimitsの構文を読み取り、型記述の位置とともに保持する
    /// </summary>
    /// <param name="reader">table型の先頭にある読み取り状態 型記述の終端まで進める</param>
    /// <returns>参照型、未検証のlimitsと型記述のバイト位置を持つtable定義</returns>
    /// <exception cref="WasmDecodeException">参照型またはlimitsの符号化が不正な場合</exception>
    /// <exception cref="ModuleReadBoundaryException">限定された範囲内で構文を読み終えられない場合</exception>
    internal static TableDefinition ReadTableType(ref ModuleBinaryReader reader)
    {
        var offset = reader.Position;
        var elementKind = ReadReferenceType(ref reader);
        var (addressType, minimum, maximum) = ReadLimits(ref reader, "テーブル");
        return new TableDefinition(offset, new WasmTableType(elementKind, addressType, minimum, maximum));
    }

    /// <summary>
    /// 仕様版に従ってメモリのアドレス型とページ数の上下限を読み取る
    /// </summary>
    /// <param name="reader">memory型の先頭にある読み取り状態 型記述の終端まで進める</param>
    /// <returns>アドレス型、未検証のページ数の上下限、型記述のバイト位置を持つメモリ定義</returns>
    /// <exception cref="WasmDecodeException">limitsの符号化が不正な場合</exception>
    /// <exception cref="ModuleReadBoundaryException">限定された範囲内で構文を読み終えられない場合</exception>
    internal static MemoryDefinition ReadMemoryType(ref ModuleBinaryReader reader)
    {
        var offset = reader.Position;
        var (addressType, minimum, maximum) = ReadLimits(ref reader, "メモリ");
        return new MemoryDefinition(offset, new WasmMemoryType(addressType, minimum, maximum));
    }

    /// <summary>
    /// globalの値型と可変性を構文から取得する
    /// </summary>
    /// <param name="reader">global型の先頭にある読み取り状態 型記述の終端まで進める</param>
    /// <returns>参照先とnull可否を含む値型と可変性を持つglobal型</returns>
    /// <exception cref="WasmDecodeException">値型または可変性の符号化が不正な場合</exception>
    /// <exception cref="ModuleReadBoundaryException">限定された範囲内で構文を読み終えられない場合</exception>
    internal static WasmGlobalType ReadGlobalType(ref ModuleBinaryReader reader)
    {
        return new WasmGlobalType(ReadValueType(ref reader), ReadMutability(ref reader, "global"));
    }

    private static bool ReadMutability(ref ModuleBinaryReader reader, string subject)
    {
        var offset = reader.Position;
        return reader.ReadByte() switch
        {
            0 => false,
            1 => true,
            _ => throw reader.Error($"malformed mutability: {subject}の可変性の符号化が不正です。", offset),
        };
    }

    /// <summary>
    /// 最小値と任意の最大値を持つlimitsの構文を取得する
    /// </summary>
    /// <remarks>最小値と最大値の大小関係や、リソースごとの仕様上の上限は検証しない</remarks>
    /// <param name="reader">limitsの先頭にある読み取り状態 宣言の終端まで進める</param>
    /// <param name="resourceName">診断に表示するリソースの名前</param>
    /// <returns>アドレス型、未検証の最小値と任意の最大値</returns>
    /// <exception cref="WasmDecodeException">フラグまたは整数の符号化が不正な場合</exception>
    /// <exception cref="ModuleReadBoundaryException">限定された範囲内で構文を読み終えられない場合</exception>
    private static (WasmValueKind AddressType, ulong Minimum, ulong? Maximum) ReadLimits(
        ref ModuleBinaryReader reader,
        string resourceName
    )
    {
        if (reader.SpecVersion == WasmSpecVersion.Core2)
        {
            var hasMaximum = reader.ReadU1();
            var minimum = reader.ReadU32();
            return (WasmValueKind.I32, minimum, hasMaximum == 1 ? reader.ReadU32() : null);
        }

        // Core 3.0ではi32の宣言もu64で符号化し、アドレス型の上限をValidateで検査する。
        var offset = reader.Position;
        var flags = reader.ReadByte();
        if ((flags & ~0x05) != 0)
        {
            throw reader.Error($"malformed limits flags: {resourceName}のlimitsフラグが不正です。", offset);
        }
        var addressType = (flags & 0x04) == 0 ? WasmValueKind.I32 : WasmValueKind.I64;
        var lower = reader.ReadU64();
        return (addressType, lower, (flags & 0x01) == 0 ? null : reader.ReadU64());
    }

    /// <summary>
    /// magicとversionがWasmバイナリのヘッダーに一致することを確認する
    /// </summary>
    /// <param name="reader">ヘッダーの先頭にある読み取り状態 正常時は8バイト進める</param>
    /// <exception cref="WasmDecodeException">magicまたはversionが一致しない場合</exception>
    /// <exception cref="ModuleReadBoundaryException">限定された範囲内で構文を読み終えられない場合</exception>
    internal static void ReadHeader(ref ModuleBinaryReader reader)
    {
        ReadHeaderWord(ref reader, [0x00, 0x61, 0x73, 0x6D], "magic header not detected");
        ReadHeaderWord(ref reader, [0x01, 0x00, 0x00, 0x00], "unknown binary version");
    }

    /// <summary>
    /// ヘッダーの4byteを逐次取得し、取得成功後に最初の相違位置を通知する
    /// </summary>
    /// <param name="reader">照合対象の先頭にある読取り状態 4byte進める</param>
    /// <param name="expected">期待する4byte</param>
    /// <param name="message">完全な不一致の診断先頭</param>
    private static void ReadHeaderWord(ref ModuleBinaryReader reader, ReadOnlySpan<byte> expected, string message)
    {
        long? mismatch = null;
        foreach (var value in expected)
        {
            var offset = reader.Position;
            if (reader.ReadByte() != value)
            {
                mismatch ??= offset;
            }
        }

        if (mismatch is not null)
        {
            throw reader.Error(message, mismatch);
        }
    }

    /// <summary>
    /// sectionの範囲を取得し、IDと標準sectionの順序・重複を検査する
    /// </summary>
    /// <param name="reader">sectionヘッダーの先頭にある読み取り状態 sectionの終端まで進める</param>
    /// <param name="previousRank">直前の標準sectionの順位 正常時に更新し、custom sectionでは変更しない</param>
    /// <returns>元の入力位置とsection IDを保持する、payloadに限定した読み取り状態</returns>
    /// <exception cref="WasmDecodeException">ID、長さ、標準sectionの順序または重複が不正な場合</exception>
    /// <exception cref="ModuleReadBoundaryException">限定された範囲内で構文を読み終えられない場合</exception>
    internal static ModuleBinaryReader ReadSection(ref ModuleBinaryReader reader, ref int previousRank)
    {
        var offset = reader.Position;
        var id = reader.ReadByte();
        reader.SectionId = id;
        if (reader.SpecVersion == WasmSpecVersion.Core3 ? id > 13 : id > 12)
        {
            throw reader.Error(
                reader.SpecVersion == WasmSpecVersion.Core3
                    ? "malformed section id: Core 3.0に存在しないsection IDです。"
                    : "malformed section id: Core 2.0に存在しないsection IDです。",
                offset
            );
        }

        if (id != 0)
        {
            // タグはmemoryとglobalの間、data countはelementとcodeの間に置かれる。
            var rank = id switch
            {
                13 => 6,
                12 => 11,
                10 => 12,
                11 => 13,
                >= 6 => id + 1,
                _ => id,
            };
            if (rank <= previousRank)
            {
                throw reader.Error("unexpected content after last section: sectionの順序または重複が不正です。", offset);
            }

            previousRank = rank;
        }
        var length = reader.ReadLength();
        return reader.ReadRange(length);
    }

    /// <summary>
    /// type sectionの型宣言を構文から取得する
    /// </summary>
    /// <param name="reader">type sectionの読み取り状態 宣言の終端まで進める</param>
    /// <param name="supertypeDeclarations">検証で使用する上位型宣言の格納先</param>
    /// <returns>型index順に並ぶ定義型</returns>
    /// <exception cref="WasmDecodeException">件数、関数型の形式、値型が不正な場合</exception>
    /// <exception cref="ModuleReadBoundaryException">限定された範囲内で構文を読み終えられない場合</exception>
    /// <exception cref="WasmImplementationLimitException">宣言件数がコレクションの保持上限を超える場合</exception>
    internal static List<WasmDefinedType> ReadTypes(
        ref ModuleBinaryReader reader,
        List<SupertypeDeclaration>? supertypeDeclarations = null
    )
    {
        var count = ReadCount(ref reader);
        List<WasmDefinedType> types = [];
        var resolvedTypes =
            reader.SpecVersion == WasmSpecVersion.Core3 ? new HashSet<WasmDefinedType>(__resolvedTypeComparer) : null;
        reader.Types = types;
        for (uint index = 0; index < count; index++)
        {
            var offset = reader.Position;
            var explicitGroup =
                reader.SpecVersion == WasmSpecVersion.Core3 && reader.TryPeekByte(out var marker) && marker == 0x4E;
            uint memberCount = 1;
            if (explicitGroup)
            {
                reader.ReadByte();
                memberCount = ReadCount(ref reader);
            }
            reader.EnsureCollectionSize(
                (ulong)types.Count + memberCount,
                offset,
                "定義型の総数が配列の保持上限を超えています。"
            );
            if (memberCount == 0)
            {
                continue;
            }

            WasmDefinedType type;
            if (memberCount == 1)
            {
                var declaration = ReadTypeDeclaration(ref reader, explicitGroup, types.Count, supertypeDeclarations);
                type = WasmDefinedType.FromDecodedGroup([declaration], types.Count);
            }
            else
            {
                // 宣言件数に合わせた領域は、全要素の構文を読み終えてから確保する。
                var declarations = new List<TypeDeclaration>();
                for (uint member = 0; member < memberCount; member++)
                {
                    declarations.Add(
                        ReadTypeDeclaration(ref reader, explicitGroup, types.Count + (int)member, supertypeDeclarations)
                    );
                }
                type = WasmDefinedType.FromDecodedGroup(CollectionsMarshal.AsSpan(declarations), types.Count);
            }
            // 未解決の参照には宣言ごとの入力位置があり、その参照を含む型も共有しない。
            if (resolvedTypes is not null && type.Unresolved is null)
            {
                if (resolvedTypes.TryGetValue(type, out var existing))
                {
                    type = existing;
                }
                else
                {
                    resolvedTypes.Add(type);
                }
            }
            type.AppendGroupTo(types);
        }
        return types;
    }

    private static TypeDeclaration ReadTypeDeclaration(
        ref ModuleBinaryReader reader,
        bool explicitGroup,
        int typeIndex,
        List<SupertypeDeclaration>? supertypeDeclarations
    )
    {
        var offset = reader.Position;
        var form = reader.ReadS7();
        var explicitSubtype = reader.SpecVersion == WasmSpecVersion.Core3 && form is -0x31 or -0x30;
        var isFinal = !explicitSubtype || form == -0x31;
        ImmutableArray<WasmValueType> supertypes = [];
        if (explicitSubtype)
        {
            supertypes = ReadSupertypes(ref reader, typeIndex, supertypeDeclarations);
            offset = reader.Position;
            form = reader.ReadS7();
        }
        if (reader.SpecVersion == WasmSpecVersion.Core3)
        {
            if (form == -0x21)
            {
                return new TypeDeclaration(ReadFields(ref reader), isFinal, supertypes);
            }
            if (form == -0x22)
            {
                throw reader.Unsupported("aggregate type", (int)reader.InputEnd, offset);
            }
        }
        if (form != -0x20)
        {
            throw reader.Error(
                explicitGroup || explicitSubtype
                    ? "malformed definition type: 関数型の定義が不正です。"
                    : "malformed function type: 関数型の形式が不正です。",
                offset
            );
        }
        return new TypeDeclaration(ReadValueTypes(ref reader), ReadValueTypes(ref reader), isFinal, supertypes);
    }

    private static ImmutableArray<WasmFieldType> ReadFields(ref ModuleBinaryReader reader)
    {
        var count = ReadCount(ref reader);
        if (count == 0)
        {
            return [];
        }
        var fields = ImmutableArray.CreateBuilder<WasmFieldType>();
        do
        {
            var offset = reader.Position;
            var code = reader.ReadS7();
            var packed = code switch
            {
                -0x08 => WasmPackedType.I8,
                -0x09 => WasmPackedType.I16,
                _ => WasmPackedType.None,
            };
            WasmValueType valueType = WasmValueKind.I32;
            if (packed == WasmPackedType.None)
            {
                try
                {
                    valueType = ReadValueType(ref reader, code, offset);
                }
                catch (WasmDecodeException)
                {
                    // 値型として読めない場合、参照実装は同じ位置を詰め込み型として検査する。
                    throw reader.Error("malformed storage type: フィールドの格納型が不正です。", offset);
                }
            }
            fields.Add(new WasmFieldType(valueType, ReadMutability(ref reader, "フィールド"), packed));
        } while (fields.Count < count);
        return fields.DrainToImmutable();
    }

    private static ImmutableArray<WasmValueType> ReadSupertypes(
        ref ModuleBinaryReader reader,
        int typeIndex,
        List<SupertypeDeclaration>? declarations
    )
    {
        var countOffset = reader.Position;
        var count = ReadCount(ref reader);
        if (count == 0)
        {
            return [];
        }

        var firstOffset = reader.Position;
        var firstReference = ReadSupertypeReference(ref reader, out var firstIndex);
        declarations?.Add(new SupertypeDeclaration(typeIndex, firstIndex, count == 1 ? firstOffset : countOffset));
        if (count == 1)
        {
            return [firstReference];
        }

        var references = ImmutableArray.CreateBuilder<WasmValueType>();
        references.Add(firstReference);
        for (uint index = 1; index < count; index++)
        {
            references.Add(ReadSupertypeReference(ref reader, out _));
        }
        return references.DrainToImmutable();
    }

    private static WasmValueType ReadSupertypeReference(ref ModuleBinaryReader reader, out uint typeIndex)
    {
        // 上位型には型番号だけが符号化される。内部では、その型への非null参照で表す。
        var offset = reader.Position;
        typeIndex = reader.ReadU32();
        return ResolveTypeReference(ref reader, typeIndex, false, offset);
    }

    /// <summary>
    /// 関数型の引数または結果を表す値型の並びを取得する
    /// </summary>
    /// <param name="reader">値型vectorの先頭にある読み取り状態 宣言の終端まで進める</param>
    /// <returns>宣言順の値型</returns>
    /// <exception cref="WasmDecodeException">件数、値型が不正な場合</exception>
    /// <exception cref="ModuleReadBoundaryException">限定された範囲内で構文を読み終えられない場合</exception>
    /// <exception cref="WasmImplementationLimitException">値型の件数がコレクションの保持上限を超える場合</exception>
    private static ImmutableArray<WasmValueType> ReadValueTypes(ref ModuleBinaryReader reader)
    {
        var count = ReadCount(ref reader);
        if (count == 0)
        {
            return [];
        }

        // 短い型列はそのまま関数型に渡し、大きな宣言では読み取った要素に合わせて容量を増やす。
        const int InitialCapacityLimit = 4;
        var types = ImmutableArray.CreateBuilder<WasmValueType>((int)Math.Min(count, InitialCapacityLimit));
        do
        {
            types.Add(ReadValueType(ref reader));
        } while (types.Count < count);

        return types.DrainToImmutable();
    }

    /// <summary>
    /// 仕様版に従って数値型、ベクトル型または参照型を読み取る
    /// </summary>
    /// <param name="reader">値型の先頭にある読み取り状態 型の終端まで進める</param>
    /// <returns>宣言された値型</returns>
    /// <exception cref="WasmDecodeException">値型の符号化が不正な場合</exception>
    /// <exception cref="ModuleReadBoundaryException">限定された範囲内で構文を読み終えられない場合</exception>
    internal static WasmValueType ReadValueType(ref ModuleBinaryReader reader)
    {
        var offset = reader.Position;
        return ReadValueType(ref reader, reader.ReadS7(), offset);
    }

    private static WasmValueType ReadValueType(ref ModuleBinaryReader reader, int code, long offset)
    {
        return code switch
        {
            -0x01 => WasmValueKind.I32,
            -0x02 => WasmValueKind.I64,
            -0x03 => WasmValueKind.F32,
            -0x04 => WasmValueKind.F64,
            -0x05 => WasmValueKind.V128,
            _ => ReadReferenceType(ref reader, code, offset, true),
        };
    }

    /// <summary>
    /// 値の宣言に使う参照型の構文を読み取る
    /// </summary>
    internal static WasmValueType ReadReferenceType(ref ModuleBinaryReader reader)
    {
        var offset = reader.Position;
        var code = reader.ReadS7();
        return ReadReferenceType(ref reader, code, offset, false);
    }

    private static WasmValueType ReadReferenceType(
        ref ModuleBinaryReader reader,
        int code,
        long offset,
        bool valueDeclaration
    )
    {
        if (code is -0x10 or -0x11)
        {
            return code == -0x10 ? WasmValueKind.FuncRef : WasmValueKind.ExternRef;
        }
        if (reader.SpecVersion == WasmSpecVersion.Core3)
        {
            if (code is -0x1c or -0x1d)
            {
                return ReadHeapType(ref reader, code == -0x1d);
            }
            if (code is >= -0x17 and <= -0x0c)
            {
                return AbstractHeapType(ref reader, code, true, offset);
            }
        }
        throw reader.Error(
            valueDeclaration && reader.SpecVersion == WasmSpecVersion.Core2
                ? "malformed reference type: Core 2.0に存在しない値型です。"
                : "malformed reference type: 参照型が不正です。",
            offset
        );
    }

    /// <summary>
    /// 定義型の添字または抽象ヒープ型から参照型を組み立てる
    /// </summary>
    internal static WasmValueType ReadHeapType(ref ModuleBinaryReader reader, bool isNullable)
    {
        if (reader.SpecVersion == WasmSpecVersion.Core2)
        {
            return ReadReferenceType(ref reader);
        }
        var offset = reader.Position;
        if (reader.TryPeekByte(out var first) && first is >= 0x40 and < 0x80)
        {
            return AbstractHeapType(ref reader, reader.ReadS7(), isNullable, offset);
        }
        var index = reader.ReadS33();
        if (index < 0)
        {
            throw reader.Error("integer representation too long: 抽象ヒープ型は1バイトで符号化します。", offset + 1);
        }
        return ResolveTypeReference(ref reader, (uint)index, isNullable, offset);
    }

    private static WasmValueType ResolveTypeReference(
        ref ModuleBinaryReader reader,
        uint index,
        bool isNullable,
        long offset
    )
    {
        var typeCount = reader.Types?.Count ?? 0;
        if (index < typeCount)
        {
            return WasmValueType.DefinedReference(reader.Types![(int)index], isNullable);
        }
        return WasmValueType.UnresolvedTypeReference(index, isNullable, reader.Location(offset));
    }

    private static WasmValueType AbstractHeapType(ref ModuleBinaryReader reader, int code, bool isNullable, long offset)
    {
        return code switch
        {
            -0x0d => WasmValueType.BottomReference(WasmValueKind.FuncRef, isNullable),
            -0x0e => WasmValueType.BottomReference(WasmValueKind.ExternRef, isNullable),
            -0x0f => WasmValueType.BottomReference(WasmValueKind.AnyRef, isNullable),
            -0x10 => WasmValueType.Reference(WasmValueKind.FuncRef, isNullable),
            -0x11 => WasmValueType.Reference(WasmValueKind.ExternRef, isNullable),
            -0x12 => WasmValueType.Reference(WasmValueKind.AnyRef, isNullable),
            -0x13 => WasmValueType.Reference(WasmValueKind.EqRef, isNullable),
            -0x14 => WasmValueType.Reference(WasmValueKind.I31Ref, isNullable),
            -0x15 => WasmValueType.Reference(WasmValueKind.StructRef, isNullable),
            -0x16 => WasmValueType.Reference(WasmValueKind.ArrayRef, isNullable),
            -0x17 or -0x0c => throw reader.Unsupported("heap type", (int)reader.InputEnd, offset),
            _ => throw reader.Error("malformed heap type: ヒープ型が不正です。", offset),
        };
    }

    /// <summary>
    /// vectorの件数を取得し、物理入力の上限とコレクションの保持上限を確認する
    /// </summary>
    /// <remarks>各要素に最低1バイト必要なvectorで使用する</remarks>
    /// <param name="reader">件数の先頭にある読み取り状態 件数の符号化の終端まで進める</param>
    /// <returns>物理入力の上限と保持上限以内の件数</returns>
    /// <exception cref="WasmDecodeException">件数の符号化が不正な場合</exception>
    /// <exception cref="ModuleReadBoundaryException">件数の符号化が未完了の場合</exception>
    /// <exception cref="WasmImplementationLimitException">件数がコレクションの保持上限を超える場合</exception>
    internal static uint ReadCount(ref ModuleBinaryReader reader)
    {
        var offset = reader.Position;
        var count = reader.ReadLength();
        reader.EnsureCollectionSize(count, offset, "要素数が配列の保持上限を超えています。");
        return count;
    }

    /// <summary>
    /// ストリームの現在位置から終端まで読み、保持上限を超える前に原因と位置を通知する
    /// </summary>
    /// <param name="stream">読み取り可能な入力ストリーム</param>
    /// <param name="maxLength">保持できる入力の最大バイト数</param>
    /// <param name="describeLimit">独自の診断が必要な場合に、上限例外と読取済みの終端から例外を作る処理</param>
    /// <returns>入力から読み取ったバイト列</returns>
    internal static ReadOnlySpan<byte> ReadInput(
        Stream stream,
        int maxLength,
        Func<WasmImplementationLimitException, long, WasmException>? describeLimit = null
    )
    {
        using var buffer = new MemoryStream();
        Span<byte> chunk = stackalloc byte[4096];
        int count;
        while ((count = stream.Read(chunk)) != 0)
        {
            // seekできない入力でも、読み取ったバイト数から追加前の位置と読取済みの終端を区別する。
            var readEnd = buffer.Length + count;
            if (readEnd > maxLength)
            {
                var diagnostic = new WasmImplementationLimitException(
                    "入力バイナリが保持上限を超えています。",
                    WasmImplementationLimitReason.InputSize,
                    maxLength,
                    new WasmFailureLocation(WasmProcessingStage.Decode, buffer.Length)
                );
                throw describeLimit?.Invoke(diagnostic, readEnd) ?? diagnostic;
            }
            buffer.Write(chunk[..count]);
        }
        return buffer.GetBuffer().AsSpan(0, (int)buffer.Length);
    }

    /// <summary>
    /// 宣言された読み取り範囲を過不足なく読み終えたことを確認する
    /// </summary>
    /// <param name="reader">宣言を読み終えた範囲の読み取り状態 位置は変更しない</param>
    /// <exception cref="ModuleReadBoundaryException">現在位置が宣言終端と一致しない場合</exception>
    /// <exception cref="WasmDecodeException">診断用読取りで現在位置が宣言終端と一致しない場合</exception>
    internal static void RequireEnd(ref ModuleBinaryReader reader)
    {
        if (reader.Position != reader.DeclaredEnd)
        {
            throw reader.BoundaryError("section size mismatch: 読取り位置が宣言終端と一致しません。");
        }
    }
}
