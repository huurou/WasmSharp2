using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_ValidateTests
{
    [Test]
    [Arguments("3F00", (byte)0x7E, 1)]
    [Arguments("3F01", (byte)0x7F, 1)]
    [Arguments("42004000", (byte)0x7F, 3)]
    [Arguments("41004001", (byte)0x7E, 3)]
    [Arguments("41004000", (byte)0x7E, 1)]
    [Arguments("42004001", (byte)0x7F, 1)]
    public async Task メモリのページ数の型が指定先と異なる_検証で型不一致を報告する(
        string instructions,
        byte resultType,
        int offsetFromEnd
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [resultType])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Section(5, [2, 0x00, 1, 0x04, 2]),
            HostLinkingModuleBinary.Code(([], [.. Convert.FromHexString(instructions), 0x0B]))
        );
        var module = WasmModule.Decode(bytes, WasmSpecVersion.Core3);

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).StartsWith("type mismatch");
            await Assert.That(exception.Location!.ByteOffset).IsEqualTo(bytes.Length - offsetFromEnd);
        }
    }

    [Test]
    [Arguments("284002001A", "unknown memory 2", "メモリが存在しません")]
    [Arguments("FD5940FFFFFFFF0F0003", "unknown memory 4294967295", "メモリが存在しません")]
    [Arguments("28400180808080101A", "offset out of range", "32ビットメモリの上限")]
    [Arguments("FD594001FFFFFFFFFFFFFFFFFF0103", "offset out of range", "32ビットメモリの上限")]
    [Arguments("284301FFFFFFFFFFFFFFFFFF011A", "alignment must not be larger than natural", "命令のアクセス幅")]
    public async Task Core3複数メモリのロードやストアの即値が不正_Validateで原因を示す(
        string instruction,
        string diagnostic,
        string expectedDetail
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Memories((1, null), (1, null)),
            HostLinkingModuleBinary.Code(([], [0x00, .. Convert.FromHexString(instruction), 0x0B]))
        );
        var module = WasmModule.Decode(bytes, WasmSpecVersion.Core3);

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).StartsWith(diagnostic);
            await Assert.That(exception.Message).Contains(expectedDetail);
        }
    }

    [Test]
    [Arguments((byte)0x7F, "284001001A")] // i32.load
    [Arguments((byte)0x7E, "2001FD0B0000")] // v128.store
    [Arguments((byte)0x7F, "2001FD55410100031A")] // v128.load16_lane
    [Arguments((byte)0x7E, "2001FD59010003")] // v128.store16_lane
    public async Task ロードやストアのアドレス型が指定したメモリに合わない_検証で型不一致を報告する(
        byte addressType,
        string instruction
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([addressType, 0x7B], [])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Section(5, [2, 0, 1, 4, 1]),
            HostLinkingModuleBinary.Code(([], [0x20, 0, .. Convert.FromHexString(instruction), 0x0B]))
        );
        var module = WasmModule.Decode(bytes, WasmSpecVersion.Core3);

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        await Assert.That(exception!.Message).StartsWith("type mismatch");
    }

    [Test]
    [Arguments("3F81010B")]
    [Arguments("41014081010B")]
    public async Task Core3でページ数の取得または拡張に存在しないメモリを指定する_検証で指定番号を報告する(
        string instructions
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [0x7F])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Memories((1, null)),
            HostLinkingModuleBinary.Code(([], Convert.FromHexString(instructions)))
        );
        var module = WasmModule.Decode(bytes, WasmSpecVersion.Core3);

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).StartsWith("unknown memory 129");
            await Assert
                .That(exception.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Validate, bytes.Length - 4, 0, 10));
        }
    }

    [Test]
    [Arguments(false, "FC0A00000B", "unknown memory 0", 5)]
    [Arguments(false, "00FC0A00000B", "unknown memory 0", 5)]
    [Arguments(false, "FC0B000B", "unknown memory 0", 4)]
    [Arguments(false, "00FC0B000B", "unknown memory 0", 4)]
    [Arguments(true, "FC0A00000B", "type mismatch", 5)]
    [Arguments(true, "41004100FC0A00000B", "type mismatch", 5)]
    [Arguments(true, "410041004200FC0A00000B", "type mismatch", 5)]
    [Arguments(true, "410042004100FC0A00000B", "type mismatch", 5)]
    [Arguments(true, "420041004100FC0A00000B", "type mismatch", 5)]
    [Arguments(true, "004200FC0A00000B", "type mismatch", 5)]
    [Arguments(true, "410041004100FC0A00001A0B", "type mismatch", 2)]
    [Arguments(true, "FC0B000B", "type mismatch", 4)]
    [Arguments(true, "41004100FC0B000B", "type mismatch", 4)]
    [Arguments(true, "410041004200FC0B000B", "type mismatch", 4)]
    [Arguments(true, "410042004100FC0B000B", "type mismatch", 4)]
    [Arguments(true, "420041004100FC0B000B", "type mismatch", 4)]
    [Arguments(true, "004200FC0B000B", "type mismatch", 4)]
    [Arguments(true, "410041004100FC0B001A0B", "type mismatch", 2)]
    [Arguments(false, "3F000B", "unknown memory 0", 3)]
    [Arguments(false, "40000B", "unknown memory 0", 3)]
    [Arguments(false, "003F001A0B", "unknown memory 0", 4)]
    [Arguments(false, "0040001A0B", "unknown memory 0", 4)]
    [Arguments(false, "420040000B", "unknown memory 0", 3)]
    [Arguments(true, "3F000B", "type mismatch", 1)]
    [Arguments(true, "40000B", "type mismatch", 3)]
    [Arguments(true, "420040000B", "type mismatch", 3)]
    [Arguments(true, "410040000B", "type mismatch", 1)]
    [Arguments(true, "00420040001A0B", "type mismatch", 4)]
    [Arguments(false, "2803000B", "unknown memory 0", 4)]
    [Arguments(false, "002803000B", "unknown memory 0", 4)]
    [Arguments(false, "003603000B", "unknown memory 0", 4)]
    [Arguments(true, "2803000B", "alignment must not be larger than natural", 4)]
    [Arguments(true, "2840000B", "alignment must not be larger than natural", 4)]
    [Arguments(true, "002803000B", "alignment must not be larger than natural", 4)]
    [Arguments(true, "003603000B", "alignment must not be larger than natural", 4)]
    [Arguments(true, "2821000B", "alignment must not be larger than natural", 4)]
    [Arguments(true, "28FFFFFFFF0F000B", "alignment must not be larger than natural", 8)]
    [Arguments(true, "2802000B", "type mismatch", 4)]
    [Arguments(true, "42002802000B", "type mismatch", 4)]
    [Arguments(true, "41002802000B", "type mismatch", 1)]
    [Arguments(true, "410041003702000B", "type mismatch", 4)]
    [Arguments(true, "420042003702000B", "type mismatch", 4)]
    [Arguments(true, "0041003702000B", "type mismatch", 4)]
    public async Task メモリ命令に参照とalignと型の違反がある_参照とalignと型の順にValidateで拒否する(
        bool hasMemory,
        string instructions,
        string diagnostic,
        int offsetFromEnd
    )
    {
        // Arrange
        var executionContext = new WasmExecutionContext();
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            HostLinkingModuleBinary.Functions(0),
            hasMemory ? HostLinkingModuleBinary.Memories((1, null)) : [],
            HostLinkingModuleBinary.Code(([], Convert.FromHexString(instructions)))
        );
        var module = WasmModule.Decode(bytes, WasmSpecVersion.Core2);

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message.StartsWith(diagnostic, StringComparison.Ordinal)).IsTrue();
            await Assert
                .That(exception.Location)
                .IsEqualTo(new(WasmProcessingStage.Validate, bytes.Length - offsetFromEnd, 0, 10));
            await Assert.That(() => module.Instantiate(executionContext, [])).ThrowsExactly<InvalidOperationException>();
        }
    }

    [Test]
    [Arguments("FC08008001", "unknown memory 128")]
    [Arguments("FC0B01", "unknown memory 1")]
    [Arguments("FC0A0100", "unknown memory 1")]
    [Arguments("FC0A0002", "unknown memory 2")]
    [Arguments("FC0A0201", "unknown memory 2")]
    public async Task Core3の一括メモリ命令で存在しないメモリを参照する_検証で該当番号を報告する(
        string operation,
        string diagnostic
    )
    {
        // Arrange
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Memories((1, null)),
            HostLinkingModuleBinary.Section(12, [1]),
            HostLinkingModuleBinary.Code(([], [0, .. Convert.FromHexString(operation), 0x0B])),
            HostLinkingModuleBinary.Section(11, [1, 1, 0])
        );
        var module = WasmModule.Decode(bytes, WasmSpecVersion.Core3);

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        await Assert.That(exception!.Message).StartsWith(diagnostic);
    }

    [Test]
    [Arguments("FC0B01", "7F7F7E")] // memory.fill 64ビットメモリの宛先がi32
    [Arguments("FC0B01", "7E7E7E")] // 充填値がi64
    [Arguments("FC0B01", "7E7F7F")] // 長さがi32
    [Arguments("FC0A0100", "7F7F7F")] // memory.copy 32→64
    [Arguments("FC0A0100", "7E7E7F")]
    [Arguments("FC0A0100", "7E7F7E")]
    [Arguments("FC0A0001", "7E7E7F")] // 64→32
    [Arguments("FC0A0001", "7F7F7F")]
    [Arguments("FC0A0001", "7F7E7E")]
    [Arguments("FC0A0101", "7E7E7F")] // 64→64の長さがi32
    public async Task 充填またはコピーの入力型が対象メモリに合わない_検証で命令の位置と型不一致を報告する(
        string operation,
        string parameterTypes
    )
    {
        // Arrange
        var instruction = Convert.FromHexString(operation);
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types((Convert.FromHexString(parameterTypes), [])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Section(5, [2, 0, 1, 4, 1]),
            HostLinkingModuleBinary.Code(([], [0x20, 0, 0x20, 1, 0x20, 2, .. instruction, 0x0B]))
        );
        var module = WasmModule.Decode(bytes, WasmSpecVersion.Core3);

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).StartsWith("type mismatch");
            await Assert
                .That(exception.Location)
                .IsEqualTo(
                    new WasmFailureLocation(WasmProcessingStage.Validate, bytes.Length - instruction.Length - 1, 0, 10)
                );
        }
    }

    [Test]
    [Arguments("002802001A0B")]
    [Arguments("003703000B")]
    [Arguments("003F001A0B")]
    [Arguments("0040001A0B")]
    [Arguments("00FC0A00000B")]
    [Arguments("00FC0B000B")]
    [Arguments("00FC8A0000000B")]
    [Arguments("00FC8B00000B")]
    public async Task 到達不能なメモリ命令に具体型と参照とalignの違反がない_スタック多相性でValidateに成功する(
        string instructions
    )
    {
        // Arrange
        var executionContext = new WasmExecutionContext();
        var bytes = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Memories((1, null)),
            HostLinkingModuleBinary.Code(([], Convert.FromHexString(instructions)))
        );
        var module = WasmModule.Decode(bytes, WasmSpecVersion.Core2);

        // Act
        var validated = module.Validate();
        var instance = validated.Instantiate(executionContext, []);

        // Assert
        await Assert.That(validated).IsSameReferenceAs(module);
        await Assert.That(instance).IsNotNull();
    }

    [Test]
    public async Task 後半関数のメモリ命令が不正_検証済みの命令を部分公開せず再試行でも拒否する()
    {
        // Arrange
        var executionContext = new WasmExecutionContext();
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [])),
                HostLinkingModuleBinary.Functions(0, 0),
                HostLinkingModuleBinary.Memories((1, null)),
                HostLinkingModuleBinary.Code(([], [0x41, 0, 0x28, 2, 0, 0x1A, 0x0B]), ([], [0x28, 3, 0, 0x0B]))
            ),
            WasmSpecVersion.Core2
        );

        // Act & Assert
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
            await Assert.That(exception!.Location!.FunctionIndex).IsEqualTo(1u);
            await Assert.That(() => module.Instantiate(executionContext, [])).ThrowsExactly<InvalidOperationException>();
        }
    }
}
