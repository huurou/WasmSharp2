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
    /// <summary>
    /// import宣言の名前、種類、未検証の要求型と入力位置を取得する
    /// </summary>
    /// <param name="reader">import sectionの読み取り状態 宣言の終端まで進める</param>
    /// <returns>入力の宣言順に並ぶimport定義 関数型のindexは解決しない</returns>
    /// <exception cref="WasmDecodeException">件数、名前、外部要素の種類または型の符号化が不正な場合</exception>
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

    /// <summary>
    /// Core 2.0のexternal kindを1バイトの符号化から取得する
    /// </summary>
    /// <param name="reader">external kindの先頭にある読み取り状態 1バイト進める</param>
    /// <param name="isImport">import宣言の診断を選ぶ場合はtrue、export宣言の場合はfalse</param>
    /// <returns>関数、table、memoryまたはglobalの種類</returns>
    /// <exception cref="WasmDecodeException">Core 2.0に存在しない種類の場合</exception>
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
        return new TableDefinition(elementKind, ReadLimits(ref reader), offset);
    }

    /// <summary>
    /// memoryのlimitsの構文を読み取り、型記述の位置とともに保持する
    /// </summary>
    /// <param name="reader">memory型の先頭にある読み取り状態 型記述の終端まで進める</param>
    /// <returns>未検証のlimitsと型記述のバイト位置を持つmemory定義</returns>
    /// <exception cref="WasmDecodeException">limitsの符号化が不正な場合</exception>
    /// <exception cref="ModuleReadBoundaryException">限定された範囲内で構文を読み終えられない場合</exception>
    internal static MemoryDefinition ReadMemoryType(ref ModuleBinaryReader reader)
    {
        var offset = reader.Position;
        return new MemoryDefinition(ReadLimits(ref reader), offset);
    }

    /// <summary>
    /// globalの値型と可変性を構文から取得する
    /// </summary>
    /// <param name="reader">global型の先頭にある読み取り状態 型記述の終端まで進める</param>
    /// <returns>Core 2.0の値型と可変性を持つglobal型</returns>
    /// <exception cref="WasmDecodeException">値型または可変性の符号化が不正な場合</exception>
    /// <exception cref="ModuleReadBoundaryException">限定された範囲内で構文を読み終えられない場合</exception>
    internal static WasmGlobalType ReadGlobalType(ref ModuleBinaryReader reader)
    {
        var valueKind = ReadValueType(ref reader);
        var offset = reader.Position;
        var isMutable = reader.ReadByte() switch
        {
            0 => false,
            1 => true,
            _ => throw reader.Error(
                "malformed mutability: globalの可変性の符号化が不正です。",
                offset
            ),
        };
        return new WasmGlobalType(valueKind, isMutable);
    }

    /// <summary>
    /// 最小値と任意の最大値を持つlimitsの構文を取得する
    /// </summary>
    /// <remarks>最小値と最大値の大小関係や、リソースごとの仕様上の上限は検証しない</remarks>
    /// <param name="reader">limitsの先頭にある読み取り状態 宣言の終端まで進める</param>
    /// <returns>未検証の最小値と任意の最大値</returns>
    /// <exception cref="WasmDecodeException">フラグまたは整数の符号化が不正な場合</exception>
    /// <exception cref="ModuleReadBoundaryException">限定された範囲内で構文を読み終えられない場合</exception>
    private static WasmLimits ReadLimits(ref ModuleBinaryReader reader)
    {
        var flags = reader.ReadU1();
        var minimum = reader.ReadU32();
        return new WasmLimits(minimum, flags == 1 ? reader.ReadU32() : null);
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
    private static void ReadHeaderWord(
        ref ModuleBinaryReader reader,
        ReadOnlySpan<byte> expected,
        string message
    )
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
    internal static ModuleBinaryReader ReadSection(
        ref ModuleBinaryReader reader,
        ref int previousRank
    )
    {
        var offset = reader.Position;
        var id = reader.ReadByte();
        reader.SectionId = id;
        if (id > 12)
        {
            throw reader.Error(
                "malformed section id: Core 2.0に存在しないsection IDです。",
                offset
            );
        }

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
                throw reader.Error(
                    "unexpected content after last section: sectionの順序または重複が不正です。",
                    offset
                );
            }

            previousRank = rank;
        }
        var length = reader.ReadLength();
        return reader.ReadRange(length);
    }

    /// <summary>
    /// type sectionの関数型宣言を構文から取得する
    /// </summary>
    /// <param name="reader">type sectionの読み取り状態 宣言の終端まで進める</param>
    /// <returns>型index順に並ぶ、引数型と結果型を保持する関数型</returns>
    /// <exception cref="WasmDecodeException">件数、関数型の形式、値型が不正な場合</exception>
    /// <exception cref="ModuleReadBoundaryException">限定された範囲内で構文を読み終えられない場合</exception>
    /// <exception cref="WasmImplementationLimitException">宣言件数がコレクションの保持上限を超える場合</exception>
    internal static List<WasmFunctionType> ReadTypes(ref ModuleBinaryReader reader)
    {
        var count = ReadCount(ref reader);
        List<WasmFunctionType> types = [];
        for (uint index = 0; index < count; index++)
        {
            var offset = reader.Position;
            if (reader.ReadS7() != -0x20)
            {
                throw reader.Error("malformed function type: 関数型の形式が不正です。", offset);
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

    /// <summary>
    /// 関数型の引数または結果を表す値型の並びを取得する
    /// </summary>
    /// <param name="reader">値型vectorの先頭にある読み取り状態 宣言の終端まで進める</param>
    /// <returns>宣言順の値型</returns>
    /// <exception cref="WasmDecodeException">件数、値型が不正な場合</exception>
    /// <exception cref="ModuleReadBoundaryException">限定された範囲内で構文を読み終えられない場合</exception>
    /// <exception cref="WasmImplementationLimitException">値型の件数がコレクションの保持上限を超える場合</exception>
    private static List<WasmValueKind> ReadValueTypes(ref ModuleBinaryReader reader)
    {
        var count = ReadCount(ref reader);
        List<WasmValueKind> types = [];
        for (uint index = 0; index < count; index++)
        {
            types.Add(ReadValueType(ref reader));
        }

        return types;
    }

    /// <summary>
    /// Core 2.0の値型をs7の符号化から取得する
    /// </summary>
    /// <param name="reader">値型の先頭にある読み取り状態 1バイト進める</param>
    /// <returns>数値型、v128、funcrefまたはexternrefの種類</returns>
    /// <exception cref="WasmDecodeException">Core 2.0に存在しない値型の場合</exception>
    /// <exception cref="ModuleReadBoundaryException">限定された範囲内で構文を読み終えられない場合</exception>
    internal static WasmValueKind ReadValueType(ref ModuleBinaryReader reader)
    {
        var offset = reader.Position;
        return reader.ReadS7() switch
        {
            -0x01 => WasmValueKind.I32,
            -0x02 => WasmValueKind.I64,
            -0x03 => WasmValueKind.F32,
            -0x04 => WasmValueKind.F64,
            -0x05 => WasmValueKind.V128,
            -0x10 => WasmValueKind.FuncRef,
            -0x11 => WasmValueKind.ExternRef,
            _ => throw reader.Error(
                "malformed reference type: Core 2.0に存在しない値型です。",
                offset
            ),
        };
    }

    /// <summary>
    /// tableの参照型をs7の符号化から取得する
    /// </summary>
    /// <param name="reader">参照型の先頭にある読取り状態 1byte進める</param>
    /// <returns>funcrefまたはexternref</returns>
    /// <exception cref="WasmDecodeException">参照型の符号化が不正か、診断用読取りで物理EOFへ達した場合</exception>
    /// <exception cref="ModuleReadBoundaryException">通常解析で参照型を限定範囲内で取得できない場合</exception>
    private static WasmValueKind ReadReferenceType(ref ModuleBinaryReader reader)
    {
        var offset = reader.Position;
        return reader.ReadS7() switch
        {
            -0x10 => WasmValueKind.FuncRef,
            -0x11 => WasmValueKind.ExternRef,
            _ => throw reader.Error("malformed reference type: tableの参照型が不正です。", offset),
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
            throw reader.BoundaryError(
                "section size mismatch: 読取り位置が宣言終端と一致しません。"
            );
        }
    }
}
