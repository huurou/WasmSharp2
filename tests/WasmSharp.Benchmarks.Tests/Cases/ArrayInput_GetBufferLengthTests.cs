using WasmSharp.Benchmarks.Cases;

namespace WasmSharp.Benchmarks.Tests.Cases;

internal class ArrayInput_GetBufferLengthTests
{
    [Test]
    [Arguments(ArrayElementType.U8, 1)]
    [Arguments(ArrayElementType.U32, 4)]
    public async Task 入力バッファーに収まる最大要素数を指定する_要素幅に応じたバイト数を返す(
        ArrayElementType elementType,
        int elementSize
    )
    {
        // Arrange
        var length = (uint)(Array.MaxLength / elementSize);
        var array = new ArrayInput("memory", 0, elementType, length, ArrayInputPattern.IndexModulo256, null, null);

        // Act
        var byteLength = array.GetBufferLength("scan");

        // Assert
        await Assert.That(byteLength).IsEqualTo(Array.MaxLength - Array.MaxLength % elementSize);
    }
}
