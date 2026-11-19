using WasmSharp.Benchmarks.Measurements;

namespace WasmSharp.Benchmarks.Tests.Measurements;

internal class EnvironmentCapture_ReadLinuxCpuNameTests
{
    [Test]
    public async Task 複数のCPUが記録される_最初のモデル名を値内のコロンも含めて返す()
    {
        // Arrange
        string[] cpuInfo = ["processor : 0", "model name\t:  CPU: A  ", "processor : 1", "model name\t: CPU B"];

        // Act
        var name = EnvironmentCapture.ReadLinuxCpuName(cpuInfo);

        // Assert
        await Assert.That(name).IsEqualTo("CPU: A");
    }

    [Test]
    public async Task モデル名のフィールドがない_CPU名を取得できない理由を返す()
    {
        // Arrange
        string[] cpuInfo = ["processor : 0", "CPU architecture: 8"];

        // Act
        var name = EnvironmentCapture.ReadLinuxCpuName(cpuInfo);

        // Assert
        await Assert.That(name).Contains("Unavailable:").And.Contains("CPU名");
    }
}
