using WasmSharp.TestSuiteRunner.Execution;
using WasmSharp.TestSuiteRunner.Reports;

namespace WasmSharp.TestSuiteRunner.Tests.Execution;

internal class UnavailableCause_CreateCaseCauseTests
{
    [Test]
    public async Task 複数の利用不能状態に依存する_直接原因と元の失敗を重複なくindex順に並べる()
    {
        // Arrange
        UnavailableCause[] causes =
        [
            new(Id(5), [Id(3), Id(1)]),
            new(Id(2), [Id(1)]),
            new(Id(5), [Id(3), Id(1)]),
        ];

        // Act
        var cause = UnavailableCause.CreateCaseCause(causes);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cause.Direct.SequenceEqual([Id(2), Id(5)])).IsTrue();
            await Assert.That(cause.Origins.SequenceEqual([Id(1), Id(3)])).IsTrue();
        }
    }

    private static CaseId Id(int index)
    {
        return new("a.wast", index);
    }
}
