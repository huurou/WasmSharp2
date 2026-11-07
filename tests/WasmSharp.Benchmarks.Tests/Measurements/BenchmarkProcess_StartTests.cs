using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Results;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Measurements;

[NotInParallel]
internal class BenchmarkProcess_StartTests
{
    [Test]
    public async Task run設定の環境変数がない_理由を示して開始を拒否する()
    {
        // Arrange
        using var fixture = new BenchmarkProcessFixture();
        Environment.SetEnvironmentVariable(RunConfiguration.ENVIRONMENT_VARIABLE, null);

        // Act & Assert
        var exception = await Assert
            .That(() => BenchmarkProcess.Start("InvokeBenchmarks.Single"))
            .ThrowsExactly<InvalidOperationException>();
        await Assert.That(exception!.Message).Contains("run設定の環境変数がありません");
        await Assert.That(RunStore.ReadChecks(fixture.Run.DirectoryPath)).IsEmpty();
    }

    [Test]
    public async Task 測定メソッドが予定ケースにない_検査を保存せず開始を拒否する()
    {
        // Arrange
        using var fixture = new BenchmarkProcessFixture();

        // Act & Assert
        var exception = await Assert
            .That(() => BenchmarkProcess.Start("InvokeBenchmarks.Multi"))
            .ThrowsExactly<InvalidDataException>();
        await Assert.That(exception!.Message).Contains("予定ケースにありません");
        await Assert.That(RunStore.ReadChecks(fixture.Run.DirectoryPath)).IsEmpty();
    }
}
