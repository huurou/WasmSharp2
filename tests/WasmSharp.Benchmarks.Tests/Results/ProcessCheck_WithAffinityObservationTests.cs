using WasmSharp.Benchmarks.Results;

namespace WasmSharp.Benchmarks.Tests.Results;

internal class ProcessCheck_WithAffinityObservationTests
{
    [Test]
    [Arguments(null, "開始時は取得不能", 8L, null, "前観測: 開始時は取得不能")]
    [Arguments(4L, null, null, "終了時は取得不能", "後観測: 終了時は取得不能")]
    public async Task 一方のCPU割り当てを取得できない_前後のマスクと失敗した段階と理由を記録する(
        long? beforeMask,
        string? beforeReason,
        long? afterMask,
        string? afterReason,
        string expectedReason
    )
    {
        // Arrange
        var check = new ProcessCheck("run", "invoke.single", "Measurement", 10, null, false, null, null, [], []);

        // Act
        var started = check.WithAffinityObservation(after: false, beforeMask, beforeReason);
        var completed = started.WithAffinityObservation(after: true, afterMask, afterReason);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(completed.AffinityBefore).IsEqualTo(beforeMask);
            await Assert.That(completed.AffinityAfter).IsEqualTo(afterMask);
            await Assert.That(completed.AffinityUnavailableReason).IsEqualTo(expectedReason);
        }
    }
}
