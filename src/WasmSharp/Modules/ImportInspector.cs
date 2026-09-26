using System.Collections.Immutable;
using WasmSharp.Exceptions;
using WasmSharp.Modules.Imports;

namespace WasmSharp.Modules;

/// <summary>
/// moduleの生成や実行を伴わずに完全なimport情報を取得する
/// </summary>
internal static class ImportInspector
{
    /// <summary>
    /// 現在位置から入力終端まで読み、入力ストリームを閉じずにimport情報を調査する
    /// </summary>
    /// <param name="stream">呼び出し元で読み取り可能性を確認した入力ストリーム</param>
    /// <returns>完全取得したimport一覧と構文・検証の未確認範囲</returns>
    internal static WasmImportInspection Inspect(Stream stream)
    {
        using var buffer = new MemoryStream();
        Span<byte> chunk = stackalloc byte[4096];
        while (true)
        {
            var count = stream.Read(chunk);
            if (count == 0)
            {
                break;
            }
            if (count > Array.MaxLength - buffer.Length)
            {
                var diagnostic = new WasmImplementationLimitException(
                    "入力バイナリが保持上限を超えています。",
                    WasmImplementationLimitReason.InputSize,
                    Array.MaxLength,
                    new WasmFailureLocation(WasmProcessingStage.Decode, buffer.Length)
                );
                throw new WasmImportInspectionException(
                    diagnostic.Message,
                    WasmImportInspectionReason.ImplementationLimit,
                    null,
                    diagnostic.Location!,
                    [
                        new WasmUnverifiedRange(
                            WasmProcessingStage.Decode,
                            0,
                            buffer.Length + count,
                            "読取済み範囲の構文は未確認です。入力終端にも未到達です。"
                        ),
                        new WasmUnverifiedRange(
                            WasmProcessingStage.Validate,
                            0,
                            buffer.Length + count,
                            "入力全体の検証は未実施で、入力終端も未確認です。"
                        ),
                    ],
                    diagnostic
                );
            }
            buffer.Write(chunk[..count]);
        }
        return Inspect(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
    }

    /// <summary>
    /// import情報を調査し、構文・未対応・保持上限の診断を元位置付きの取得失敗へ変換する
    /// </summary>
    /// <param name="bytes">入力バイト列</param>
    /// <returns>全走査が成功した場合だけ返すimport一覧と未確認範囲</returns>
    internal static WasmImportInspection Inspect(ReadOnlySpan<byte> bytes)
    {
        var ranges = ImmutableArray.CreateBuilder<WasmUnverifiedRange>();
        try
        {
            return InspectCore(bytes, ranges);
        }
        catch (WasmException exception)
            when (exception
                    is WasmDecodeException
                        or WasmUnsupportedFeatureException
                        or WasmImplementationLimitException
            )
        {
            var reason = exception switch
            {
                WasmDecodeException => WasmImportInspectionReason.MalformedBinary,
                WasmUnsupportedFeatureException => WasmImportInspectionReason.UnsupportedFeature,
                _ => WasmImportInspectionReason.ImplementationLimit,
            };
            var location = exception.Location!;
            throw new WasmImportInspectionException(
                exception.Message,
                reason,
                (exception as WasmUnsupportedFeatureException)?.Feature,
                location,
                FailureRanges(ranges, location.ByteOffset!.Value, bytes.Length),
                exception
            );
        }
    }

    /// <summary>
    /// 全sectionの外枠とtype/importの内容を確認し、要求型を解決した宣言順の一覧を構築する
    /// </summary>
    /// <param name="bytes">入力バイト列</param>
    /// <param name="ranges">読み飛ばしたpayloadを蓄積し、成功時に全体の検証未実施範囲を追加する先</param>
    /// <returns>入力から独立して所有するimport一覧と未確認範囲</returns>
    private static WasmImportInspection InspectCore(
        ReadOnlySpan<byte> bytes,
        ImmutableArray<WasmUnverifiedRange>.Builder ranges
    )
    {
        var reader = new ModuleBinaryReader(bytes);
        ModuleBinaryFormat.ReadHeader(ref reader);
        List<WasmFunctionType> types = [];
        var imports = ImmutableArray.CreateBuilder<WasmImportInfo>();
        var previousRank = 0;
        while (reader.Remaining != 0)
        {
            var section = ModuleBinaryFormat.ReadSection(ref reader, ref previousRank);
            switch (section.SectionId)
            {
                case 0:
                    section.ReadName();
                    if (section.Remaining != 0)
                    {
                        SkipPayload(ref section, ranges);
                    }
                    break;

                case 1:
                    types = ModuleBinaryFormat.ReadTypes(ref section);
                    break;

                case 2:
                    foreach (var import in ModuleBinaryFormat.ReadImports(ref section))
                    {
                        imports.Add(
                            import switch
                            {
                                FunctionImport function
                                    when function.TypeIndex < (uint)types.Count =>
                                    new WasmImportInfo.Function(
                                        function.ModuleName,
                                        function.Name,
                                        types[(int)function.TypeIndex]
                                    ),
                                FunctionImport function => throw new WasmImportInspectionException(
                                    "importの関数型添字を解決できません。",
                                    WasmImportInspectionReason.UnresolvedType,
                                    null,
                                    section.Location(function.ByteOffset),
                                    FailureRanges(ranges, function.ByteOffset, bytes.Length),
                                    null
                                ),
                                GlobalImport global => new WasmImportInfo.Global(
                                    global.ModuleName,
                                    global.Name,
                                    global.Type
                                ),
                                MemoryImport memory => new WasmImportInfo.Memory(
                                    memory.ModuleName,
                                    memory.Name,
                                    memory.Type.Limits
                                ),
                                TableImport table => new WasmImportInfo.Table(
                                    table.ModuleName,
                                    table.Name,
                                    table.Type.ElementKind,
                                    table.Type.Limits
                                ),
                                _ => throw new InvalidOperationException(),
                            }
                        );
                    }
                    break;

                default:
                    SkipPayload(ref section, ranges);
                    break;
            }
            ModuleBinaryFormat.RequireEnd(ref section);
        }
        ranges.Add(
            new WasmUnverifiedRange(
                WasmProcessingStage.Validate,
                0,
                bytes.Length,
                "入力全体の検証が未実施です。"
            )
        );
        return new WasmImportInspection(imports.ToImmutable(), ranges.ToImmutable());
    }

    /// <summary>
    /// 読み飛ばしたpayloadに失敗位置以降の範囲と全体の検証未実施範囲を加える
    /// </summary>
    /// <param name="ranges">失敗前に読み飛ばしたpayloadの範囲</param>
    /// <param name="failureOffset">入力先頭を0とした失敗位置</param>
    /// <param name="inputLength">入力全体のバイト数</param>
    /// <returns>取得失敗の診断が所有する未確認範囲</returns>
    private static ImmutableArray<WasmUnverifiedRange> FailureRanges(
        ImmutableArray<WasmUnverifiedRange>.Builder ranges,
        long failureOffset,
        long inputLength
    )
    {
        return
        [
            .. ranges,
            new WasmUnverifiedRange(
                WasmProcessingStage.Decode,
                failureOffset,
                inputLength,
                "この位置以降のimport調査が未完了です。"
            ),
            new WasmUnverifiedRange(
                WasmProcessingStage.Validate,
                0,
                inputLength,
                "入力全体の検証が未実施です。"
            ),
        ];
    }

    /// <summary>
    /// payloadの残りを解釈せず読み飛ばし、空の場合もゼロ幅の構文未確認範囲を記録する
    /// </summary>
    /// <param name="section">読み飛ばすpayloadの現在位置を持つreader</param>
    /// <param name="ranges">構文未確認範囲を追加する先</param>
    private static void SkipPayload(
        ref ModuleBinaryReader section,
        ImmutableArray<WasmUnverifiedRange>.Builder ranges
    )
    {
        ranges.Add(
            new WasmUnverifiedRange(
                WasmProcessingStage.Decode,
                section.Position,
                section.Position + section.Remaining,
                "このpayloadの構文は未確認です。"
            )
        );
        section.ReadBytes((uint)section.Remaining);
    }
}
