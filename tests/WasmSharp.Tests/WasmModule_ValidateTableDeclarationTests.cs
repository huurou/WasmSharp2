using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_ValidateTests
{
    [Test]
    [Arguments(false, "008080808010", "table size must be at most 2^32-1 for i32")]
    [Arguments(true, "01008080808010", "table size must be at most 2^32-1 for i32")]
    [Arguments(true, "01808080801000", "table size must be at most 2^32-1 for i32")]
    [Arguments(false, "05FFFFFFFFFFFFFFFFFF01FEFFFFFFFFFFFFFFFF01", "size minimum must not be greater than maximum")]
    [Arguments(true, "0581808080108080808010", "size minimum must not be greater than maximum")]
    public async Task テーブルの最小値または最大値が宣言の制約に違反する_検証段階で要素数の診断を返す(
        bool imported,
        string limits,
        string diagnostic
    )
    {
        // Arrange
        byte[] type = [0x6F, .. Convert.FromHexString(limits)];
        var section = imported
            ? HostLinkingModuleBinary.Imports(("env", "t", 1, type))
            : HostLinkingModuleBinary.Section(4, [1, .. type]);
        var module = WasmModule.Decode(HostLinkingModuleBinary.Create(section), WasmSpecVersion.Core3);

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).StartsWith(diagnostic);
            await Assert
                .That(exception.Location)
                .IsEqualTo(new WasmFailureLocation(WasmProcessingStage.Validate, 11, null, imported ? (byte)2 : (byte)4));
        }
    }

    [Test]
    [Arguments(false, "7004FFFFFFFFFFFFFFFFFF01")]
    [Arguments(true, "7004FFFFFFFFFFFFFFFFFF01")]
    [Arguments(false, "700100FFFFFFFF0F")]
    [Arguments(true, "7000FFFFFFFF0F")]
    public async Task テーブルの最小値または最大値がアドレス型の上限に等しい_領域を確保せず定義とimportを検証できる(
        bool imported,
        string encodedType
    )
    {
        // Arrange
        var type = Convert.FromHexString(encodedType);
        var section = imported
            ? HostLinkingModuleBinary.Imports(("env", "t", 1, type))
            : HostLinkingModuleBinary.Section(4, [1, .. type]);
        var module = WasmModule.Decode(HostLinkingModuleBinary.Create(section));

        // Act
        var validated = module.Validate();

        // Assert
        await Assert.That(validated).IsSameReferenceAs(module);
    }
}
