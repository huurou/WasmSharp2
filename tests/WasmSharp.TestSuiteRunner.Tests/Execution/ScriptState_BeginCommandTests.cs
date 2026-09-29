using WasmSharp.TestSuiteRunner.Execution;
using WasmSharp.TestSuiteRunner.Reports;

namespace WasmSharp.TestSuiteRunner.Tests.Execution;

internal class ScriptState_BeginCommandTests
{
    [Test]
    public async Task Commandを順に開始する_入力pathとindexの識別を返し現在commandを置換する()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        state.BeginCommand(0);

        // Act
        var id = state.BeginCommand(3);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(id).IsEqualTo(new CaseId("a.wast", 3));
            await Assert.That(state.CurrentCommand).IsEqualTo(id);
        }
    }
}
