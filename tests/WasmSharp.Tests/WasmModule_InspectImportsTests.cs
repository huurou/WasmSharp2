using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal class WasmModule_InspectImportsTests
{
    [Test]
    [Arguments((byte)9, "section.element")]
    [Arguments((byte)12, "section.data_count")]
    [Arguments((byte)11, "section.data")]
    public async Task 未対応segmentがある_importなしを確定して通常Decodeとは区別する(
        byte id,
        string feature
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(HostLinkingModuleBinary.Section(id, [0xFF]));
        using var stream = new ChunkedReadStream(new MemoryStream(bytes));

        // Act
        var fromBytes = WasmModule.InspectImports(bytes);
        var fromStream = WasmModule.InspectImports(stream);

        // Assert
        await Assert.That(fromBytes.Imports.IsEmpty).IsTrue();
        await Assert.That(fromStream.Imports.IsEmpty).IsTrue();
        await Assert.That(fromBytes.UnverifiedRanges.Length).IsEqualTo(2);
        await Assert
            .That(fromStream.UnverifiedRanges.SequenceEqual(fromBytes.UnverifiedRanges))
            .IsTrue();

        // Act & Assert
        var exception = await Assert
            .That(() => WasmModule.Decode(bytes))
            .ThrowsExactly<WasmUnsupportedFeatureException>();
        await Assert.That(exception!.Feature).IsEqualTo(feature);
    }

    [Test]
    [Arguments("", 0L, null)]
    [Arguments("006173", 3L, null)]
    [Arguments("0062736D01000000", 1L, null)]
    [Arguments("0061736D02000000", 4L, null)]
    [Arguments("0061736D010000000D00", 8L, (byte)13)]
    [Arguments("0061736D01000000000201FF", 11L, (byte)0)]
    [Arguments("0061736D0100000001020161", 11L, (byte)1)]
    [Arguments("0061736D0100000002050101FF0000", 12L, (byte)2)]
    [Arguments("0061736D010000000208020000037F0001FF", 17L, (byte)2)]
    [Arguments("0061736D01000000020100020100", 11L, (byte)2)]
    [Arguments("0061736D01000000010A00", 10L, (byte)1)]
    public async Task 必要な構文が破損_両入力で元位置とDecode診断を保持する(
        string hex,
        long offset,
        byte? sectionId
    )
    {
        // Arrange
        var bytes = Convert.FromHexString(hex);
        using var stream = new ChunkedReadStream(new MemoryStream(bytes));
        Func<WasmImportInspection>[] inspectors =
        [
            () => WasmModule.InspectImports(bytes),
            () => WasmModule.InspectImports(stream),
        ];

        // Act & Assert
        foreach (var inspect in inspectors)
        {
            var exception = await Assert
                .That(inspect)
                .ThrowsExactly<WasmImportInspectionException>();
            using (Assert.Multiple())
            {
                await Assert
                    .That(exception!.Reason)
                    .IsEqualTo(WasmImportInspectionReason.MalformedBinary);
                await Assert.That(exception.Feature).IsNull();
                await Assert
                    .That(exception.Location)
                    .IsEqualTo(
                        new WasmFailureLocation(WasmProcessingStage.Decode, offset, null, sectionId)
                    );
                await Assert.That(exception.InnerException).IsTypeOf<WasmDecodeException>();
                await Assert.That(exception.UnverifiedRanges[0].StartOffset).IsEqualTo(offset);
                await Assert.That(exception.UnverifiedRanges[0].EndOffset).IsEqualTo(bytes.Length);
                await Assert
                    .That(exception.UnverifiedRanges[^1].Stage)
                    .IsEqualTo(WasmProcessingStage.Validate);
            }
        }
    }

    [Test]
    [Arguments(1)]
    [Arguments(3000)]
    public async Task 非seekでshortReadする_現在位置からEOFまで読み入力を閉じない(int chunkSize)
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(0, [0, .. new byte[5000]]),
            HostLinkingModuleBinary.Imports(("env", "g", 3, [0x7F, 0]))
        );
        using var input = new MemoryStream([0xAA, 0xBB, .. bytes]);
        input.Position = 2;
        using var stream = new ChunkedReadStream(input, chunkSize);

        // Act
        var result = WasmModule.InspectImports(stream);

        // Assert
        await Assert.That(result.Imports.Single().Name).IsEqualTo("g");
        await Assert.That(result.UnverifiedRanges[^1].EndOffset).IsEqualTo(bytes.Length);
        await Assert.That(input.Position).IsEqualTo(input.Length);
        await Assert.That(stream.CanRead).IsTrue();
    }

    [Test]
    public async Task 現在位置以降が破損_相対位置で失敗しても入力を閉じない()
    {
        // Arrange
        using var input = new MemoryStream(Convert.FromHexString("AABB0062736D01000000"));
        input.Position = 2;
        using var stream = new ChunkedReadStream(input);

        // Act & Assert
        var exception = await Assert
            .That(() => WasmModule.InspectImports(stream))
            .ThrowsExactly<WasmImportInspectionException>();
        await Assert.That(exception!.Location!.ByteOffset).IsEqualTo(1L);
        await Assert.That(exception.UnverifiedRanges[^1].EndOffset).IsEqualTo(8L);
        await Assert.That(stream.CanRead).IsTrue();
    }

    [Test]
    public async Task Null入力_引数例外で拒否する()
    {
        // Arrange
        Stream stream = null!;

        // Act & Assert
        var exception = await Assert
            .That(() => WasmModule.InspectImports(stream))
            .ThrowsExactly<ArgumentNullException>();
        await Assert.That(exception!.ParamName).IsEqualTo("stream");
    }

    [Test]
    public async Task 読み取り不可の入力_引数例外で拒否する()
    {
        // Arrange
        var stream = new MemoryStream();
        stream.Dispose();

        // Act & Assert
        var exception = await Assert
            .That(() => WasmModule.InspectImports(stream))
            .ThrowsExactly<ArgumentException>();
        await Assert.That(exception!.ParamName).IsEqualTo("stream");
    }

    [Test]
    public async Task 入力元のIO例外_元の型と実体を維持して入力を閉じない()
    {
        // Arrange
        var expected = new IOException("入力元の読取失敗");
        using var stream = new FailingReadStream(expected);

        // Act & Assert
        var actual = await Assert
            .That(() => WasmModule.InspectImports(stream))
            .ThrowsExactly<IOException>();
        await Assert.That(actual).IsSameReferenceAs(expected);
        await Assert.That(stream.CanRead).IsTrue();
    }

    [Test]
    public async Task 入力元の割当例外_調査失敗に変換せず元の実体を維持する()
    {
        // Arrange
        var expected = new OutOfMemoryException("入力元の割当失敗");
        using var stream = new FailingReadStream(expected);

        // Act & Assert
        var actual = await Assert
            .That(() => WasmModule.InspectImports(stream))
            .ThrowsExactly<OutOfMemoryException>();
        await Assert.That(actual).IsSameReferenceAs(expected);
        await Assert.That(stream.CanRead).IsTrue();
    }

    [Test]
    [Arguments(false, 0u)]
    [Arguments(true, 0u)]
    [Arguments(false, uint.MaxValue)]
    [Arguments(true, uint.MaxValue)]
    public async Task 関数型が存在しない_破損やimportなしと区別して宣言位置で失敗する(
        bool useStream,
        uint index
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Imports(("", "", 0, HostLinkingModuleBinary.Unsigned(index)))
        );
        using var stream = new ChunkedReadStream(new MemoryStream(bytes));

        // Act & Assert
        var exception = await Assert
            .That(() =>
                useStream ? WasmModule.InspectImports(stream) : WasmModule.InspectImports(bytes)
            )
            .ThrowsExactly<WasmImportInspectionException>();
        await Assert.That(exception!.Reason).IsEqualTo(WasmImportInspectionReason.UnresolvedType);
        await Assert
            .That(exception.Location)
            .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Decode, 11, null, 2));
        await Assert.That(exception.UnverifiedRanges[0].StartOffset).IsEqualTo(11L);
        await Assert.That(exception.UnverifiedRanges[^1].EndOffset).IsEqualTo(bytes.Length);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 読み飛ばしたpayloadの後が破損_元の診断と全未確認範囲を保持し部分一覧を返さない(
        bool useStream
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Imports(("", "", 3, [0x7F, 0])),
            HostLinkingModuleBinary.Section(10, [0xFF]),
            [11, 2, 0]
        );
        using var stream = new ChunkedReadStream(new MemoryStream(bytes));
        WasmImportInspection? result = null;

        // Act & Assert
        var exception = await Assert
            .That(() =>
                result = useStream
                    ? WasmModule.InspectImports(stream)
                    : WasmModule.InspectImports(bytes)
            )
            .ThrowsExactly<WasmImportInspectionException>();
        using (Assert.Multiple())
        {
            await Assert.That(result).IsNull();
            await Assert
                .That(exception!.Reason)
                .IsEqualTo(WasmImportInspectionReason.MalformedBinary);
            await Assert.That(exception.Feature).IsNull();
            await Assert
                .That(exception.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Decode, 21, null, 11));
            await Assert.That(exception.InnerException).IsTypeOf<WasmDecodeException>();
            await Assert
                .That(((WasmException)exception.InnerException!).Location)
                .IsEqualTo(exception.Location);
            await Assert.That(exception.UnverifiedRanges.Length).IsEqualTo(3);
            await Assert.That(exception.UnverifiedRanges[0].StartOffset).IsEqualTo(18L);
            await Assert.That(exception.UnverifiedRanges[0].EndOffset).IsEqualTo(19L);
            await Assert.That(exception.UnverifiedRanges[1].StartOffset).IsEqualTo(21L);
            await Assert.That(exception.UnverifiedRanges[1].EndOffset).IsEqualTo(22L);
            await Assert
                .That(exception.UnverifiedRanges[^1].Stage)
                .IsEqualTo(WasmProcessingStage.Validate);
            await Assert.That(exception.UnverifiedRanges[^1].StartOffset).IsEqualTo(0L);
            await Assert.That(exception.UnverifiedRanges[^1].EndOffset).IsEqualTo(22L);
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 未対応本体とstartと巨大limitsがある_生成や実行を要求せず全種類を取得する(
        bool useStream
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            HostLinkingModuleBinary.Imports(
                ("env", "f", 0, [0]),
                ("env", "g", 3, [0x7B, 1]),
                ("env", "m", 2, HostLinkingModuleBinary.Limits(uint.MaxValue)),
                ("env", "t", 1, [0x70, .. HostLinkingModuleBinary.Limits(uint.MaxValue)])
            ),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Memories((uint.MaxValue, null)),
            HostLinkingModuleBinary.Start(0),
            HostLinkingModuleBinary.Code(([], [0x6A, 0x0B]))
        );
        using var stream = new ChunkedReadStream(new MemoryStream(bytes));

        // Act
        var result = useStream
            ? WasmModule.InspectImports(stream)
            : WasmModule.InspectImports(bytes);

        // Assert
        await Assert.That(result.Imports.Length).IsEqualTo(4);
        await Assert
            .That(((WasmImportInfo.Function)result.Imports[0]).Type.Parameters.IsEmpty)
            .IsTrue();
        await Assert
            .That(((WasmImportInfo.Global)result.Imports[1]).Type)
            .IsEqualTo(new WasmGlobalType(WasmValueKind.V128, true));
        await Assert
            .That(((WasmImportInfo.Memory)result.Imports[2]).Limits.Minimum)
            .IsEqualTo(uint.MaxValue);
        await Assert
            .That(((WasmImportInfo.Table)result.Imports[3]).ElementType)
            .IsEqualTo(WasmValueKind.FuncRef);
        await Assert
            .That(result.UnverifiedRanges[^1].Stage)
            .IsEqualTo(WasmProcessingStage.Validate);

        // Act & Assert
        var exception = await Assert
            .That(() => WasmModule.Decode(bytes))
            .ThrowsExactly<WasmUnsupportedFeatureException>();
        await Assert.That(exception!.Feature).IsEqualTo("i32.add");
    }

    [Test]
    public async Task 異なる関数型を同名でimportする_宣言順と各型添字の解決結果を保つ()
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([0x7F], []), ([0x7E], [0x6F, 0x7B])),
            HostLinkingModuleBinary.Imports(("env", "f", 0, [1]), ("env", "f", 0, [0]))
        );

        // Act
        var result = WasmModule.InspectImports(bytes);
        var first = ((WasmImportInfo.Function)result.Imports[0]).Type;
        var second = ((WasmImportInfo.Function)result.Imports[1]).Type;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Imports.Length).IsEqualTo(2);
            await Assert.That(first.Parameters.Single()).IsEqualTo(WasmValueKind.I64);
            await Assert
                .That(
                    first.Results.SequenceEqual(
                        new[] { WasmValueKind.ExternRef, WasmValueKind.V128 }
                    )
                )
                .IsTrue();
            await Assert.That(second.Parameters.Single()).IsEqualTo(WasmValueKind.I32);
            await Assert.That(second.Results.IsEmpty).IsTrue();
        }
    }

    private sealed class FailingReadStream(Exception exception) : MemoryStream
    {
        public override int Read(Span<byte> buffer)
        {
            throw exception;
        }
    }
}
