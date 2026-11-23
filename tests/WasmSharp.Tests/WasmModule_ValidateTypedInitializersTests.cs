using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_ValidateTests
{
    [Test]
    [Arguments("6400", "D2000B")]
    [Arguments("6400", "D0000B")]
    [Arguments("6400", "23000B")]
    [Arguments("6300", "D0010B")]
    [Arguments("6301", "23000B")]
    public async Task 型付きglobalの定数式が宣言の関数型やnull可否に適合しない_検証で型不一致を報告する(
        string globalType,
        string initializer
    )
    {
        // Arrange
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], []), ([], [0x7F])),
            HostLinkingModuleBinary.Imports(("env", "f", 0, [1]), ("env", "g", 3, [0x63, 0, 0])),
            HostLinkingModuleBinary.Section(
                6,
                [1, .. Convert.FromHexString(globalType), 0, .. Convert.FromHexString(initializer)]
            )
        );
        var module = WasmModule.Decode(binary, WasmSpecVersion.Core3);

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).StartsWith("type mismatch");
            await Assert
                .That(exception.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Validate, binary.Length - 1, null, 6));
        }
    }

    [Test]
    [Arguments((byte)1, "016001630700", 14L)]
    [Arguments((byte)1, "016000016407", 15L)]
    [Arguments((byte)2, "01000003630700", 15L)]
    [Arguments((byte)2, "0100000163070000", 15L)]
    [Arguments((byte)4, "0163070000", 12L)]
    [Arguments((byte)6, "01630700D0700B", 12L)]
    [Arguments((byte)6, "017000D0070B", 14L)]
    [Arguments((byte)9, "0105630700", 13L)]
    public async Task 型宣言や定数式で存在しない型番号を参照する_検証で番号と参照位置を報告する(
        byte sectionId,
        string payload,
        long typeOffset
    )
    {
        // Arrange
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(sectionId, Convert.FromHexString(payload))
        );
        var module = WasmModule.Decode(binary, WasmSpecVersion.Core3);

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).StartsWith("unknown type 7");
            await Assert
                .That(exception.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Validate, typeOffset, null, sectionId));
        }
    }

    [Test]
    public async Task Nullを許さないテーブルに暗黙の初期値を使う_要素数が0でも型不一致を報告する()
    {
        // Arrange
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            HostLinkingModuleBinary.Section(4, [1, 0x64, 0, 0, 0])
        );
        var module = WasmModule.Decode(binary, WasmSpecVersion.Core3);

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).StartsWith("type mismatch");
            await Assert.That(exception.Location?.SectionId).IsEqualTo((byte)4);
        }
    }

    [Test]
    [Arguments("23000B", true)]
    [Arguments("D0000B", false)]
    [Arguments("D0730B", false)]
    public async Task Nullを許す型付きglobalに関数参照またはnullを初期化する_初期値の参照先を保持する(
        string initializer,
        bool expectFunction
    )
    {
        // Arrange
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], [])),
            HostLinkingModuleBinary.Functions(0),
            HostLinkingModuleBinary.Section(
                6,
                [
                    2,
                    0x64,
                    0,
                    0,
                    0xD2,
                    0,
                    0x0B, // (ref 0)のglobalをref.funcで初期化する。
                    0x63,
                    0,
                    0,
                    .. Convert.FromHexString(initializer),
                ]
            ),
            HostLinkingModuleBinary.Exports(("target", 0, 0), ("copy", 3, 1)),
            HostLinkingModuleBinary.Code(([], [0x0B]))
        );
        var context = new WasmExecutionContext();

        // Act
        var instance = WasmModule.Decode(binary, WasmSpecVersion.Core3).Validate().Instantiate(context, []);
        var function = instance.GetGlobal("copy").AsFuncRef();

        // Assert
        if (expectFunction)
        {
            await Assert.That(function).IsSameReferenceAs(instance.GetFunction("target"));
        }
        else
        {
            await Assert.That(function).IsNull();
        }
    }

    [Test]
    public async Task 型付きテーブルへ別の関数型の要素セグメントを配置する_空でも型不一致を報告する()
    {
        // Arrange
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types(([], []), ([], [0x7F])),
            HostLinkingModuleBinary.Section(4, [1, 0x63, 0, 0, 0]),
            HostLinkingModuleBinary.Section(9, [1, 6, 0, 0x41, 0, 0x0B, 0x63, 1, 0])
        );
        var module = WasmModule.Decode(binary, WasmSpecVersion.Core3);

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).StartsWith("type mismatch");
            await Assert.That(exception.Location?.SectionId).IsEqualTo((byte)9);
        }
    }
}
