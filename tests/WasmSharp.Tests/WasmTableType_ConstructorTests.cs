namespace WasmSharp.Tests;

internal class WasmTableType_ConstructorTests
{
    [Test]
    [Arguments(WasmValueKind.I32, WasmValueKind.I64, "ElementType")]
    [Arguments(WasmValueKind.FuncRef, WasmValueKind.F32, "AddressType")]
    public async Task 要素型またはアドレス型が不正である_該当する引数を示す(
        WasmValueKind elementType,
        WasmValueKind addressType,
        string parameterName
    )
    {
        // Act & Assert
        var exception = await Assert
            .That(() => new WasmTableType(elementType, addressType, 0))
            .ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(exception!.ParamName).IsEqualTo(parameterName);
    }
}
