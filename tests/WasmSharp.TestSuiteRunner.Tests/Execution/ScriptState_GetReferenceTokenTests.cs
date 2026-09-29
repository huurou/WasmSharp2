using WasmSharp.TestSuiteRunner.Execution;
using WasmSharp.TestSuiteRunner.Reports;

namespace WasmSharp.TestSuiteRunner.Tests.Execution;

internal class ScriptState_GetReferenceTokenTests
{
    [Test]
    public async Task 異なる参照を順に指定する_内容の等値性ではなく参照ごとに初出順のtokenを割り当てる()
    {
        // Arrange
        var state = new ScriptState("a.wast");
        var externref = state.GetExternref(7);
        var first = new CaseId("x", 0);
        var equal = new CaseId("x", 0);

        // Act
        int[] tokens =
        [
            state.GetReferenceToken(externref),
            state.GetReferenceToken(first),
            state.GetReferenceToken(externref),
            state.GetReferenceToken(equal),
        ];

        // Assert
        await Assert.That(tokens.SequenceEqual([0, 1, 0, 2])).IsTrue();
    }

    [Test]
    public async Task 別の入力の状態で参照を指定する_以前の入力のtokenを持ち込まず0から割り当てる()
    {
        // Arrange
        var reference = new object();
        var previous = new ScriptState("a.wast");
        previous.GetReferenceToken(new object());
        previous.GetReferenceToken(reference);
        var state = new ScriptState("b.wast");

        // Act
        var token = state.GetReferenceToken(reference);

        // Assert
        await Assert.That(token).IsEqualTo(0);
    }
}
