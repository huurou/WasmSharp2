using WasmSharp.TestSuiteRunner.Reports;

namespace WasmSharp.TestSuiteRunner.Tests.Reports;

internal class CaseId_ToStringTests
{
    [Test]
    public async Task ケース識別を表示する_入力の相対pathとcommandの順序を続けて示す()
    {
        // Arrange
        var id = new CaseId("simd/simd_lane.wast", 7);

        // Act
        var text = id.ToString();

        // Assert
        await Assert.That(text).IsEqualTo("simd/simd_lane.wast#7");
    }
}
