using System.Text.Json;
using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Results;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Results;

internal class RunStore_SaveCheckTests
{
    [Test]
    [Arguments("other-run", "invoke.single")]
    [Arguments("fixture-success", "other-case")]
    public async Task 別のrunまたは予定外ケースの検査である_混在を拒否する(
        string runId,
        string caseId
    )
    {
        // Arrange
        using var fixture = new RunStoreFixture();
        var store = RunStore.Create(fixture.DirectoryPath, fixture.Manifest);
        var check = fixture.Check with { RunId = runId, CaseId = caseId };

        // Act & Assert
        await Assert.That(() => store.SaveCheck(check)).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task 同じprocessの段階記録を保存する_以前の検査を変更せず追記する()
    {
        // Arrange
        using var fixture = new RunStoreFixture();
        var store = RunStore.Create(fixture.DirectoryPath, fixture.Manifest);
        var precheck = fixture.Check with
        {
            ExecutionStarted = false,
            Postcheck = null,
            LastConfirmedStage = RunStage.Precheck,
        };

        // Act
        var first = store.SaveCheck(precheck);
        var second = store.SaveCheck(
            precheck with
            {
                ExecutionStarted = true,
                LastConfirmedStage = RunStage.ExecutionStarted,
            }
        );
        var last = store.SaveCheck(fixture.Check);
        var restored = JsonSerializer.Deserialize<ProcessCheck>(
            File.ReadAllBytes(Path.Combine(fixture.DirectoryPath, first.RelativePath)),
            CaseJson.Options
        )!;

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(
                    new[] { first.RelativePath, second.RelativePath, last.RelativePath }
                        .Distinct()
                        .Count()
                )
                .IsEqualTo(3);
            await Assert.That(restored.LastConfirmedStage).IsEqualTo(RunStage.Precheck);
            await Assert.That(restored.ExecutionStarted).IsFalse();
            await Assert.That(restored.Postcheck).IsNull();
        }
    }
}
