using WasmSharp.Benchmarks.Measurements;
using WasmSharp.Benchmarks.Results;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Measurements;

internal class BenchmarkRun_AttachChecksTests
{
    [Test]
    public async Task 同じcaseに別Jobと診断processがある_最新検査をprocessごとに対応付ける()
    {
        // Arrange
        using var fixture = new RunStoreFixture();
        var store = RunStore.Create(fixture.DirectoryPath, fixture.Manifest);
        var check = fixture.Check;
        store.SaveCheck(
            check with
            {
                Postcheck = null,
                LastConfirmedStage = RunStage.ExecutionStarted,
            }
        );
        var final = store.SaveCheck(check);
        store.SaveCheck(check with { JobId = "OtherJob" });
        var diagnostic = store.SaveCheck(check with { ProcessId = check.ProcessId + 1 });

        // Act
        var results = BenchmarkRun.AttachChecks(
            fixture.DirectoryPath,
            check.RunId,
            check.CaseId,
            check.JobId,
            [new(check.JobId, check.ProcessId, 0, [], true, null)]
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(results.Count).IsEqualTo(2);
            await Assert
                .That(results.Single(x => x.ProcessId == check.ProcessId).CheckFile)
                .IsEqualTo(final);
            await Assert
                .That(results.Single(x => x.ProcessId == check.ProcessId + 1).CheckFile)
                .IsEqualTo(diagnostic);
            await Assert
                .That(results.Single(x => x.ProcessId == check.ProcessId + 1).ExitCode)
                .IsNull();
        }
    }
}
