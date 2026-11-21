using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_GrowTests
{
    [Test]
    [NotInParallel]
    public async Task 宣言範囲内でもページ表を確保できない_マイナス1を返しページ数と内容を保つ()
    {
        // Arrange
        var memory = WasmMemory.Create(new WasmMemoryType(WasmValueKind.I64, 1));
        memory.Write(0, [42]);
        var bytes = new byte[1];

        // Act
        var actual = await GrowAllocationFixture.RunAsync(pages =>
            MemoryHandlerFixture.Execute(Interpreter.Grow, memory, default, [WasmValue.FromI64(pages)])
        );
        memory.Read(0, bytes);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(actual.Result.Status).IsEqualTo(ExecutionStatus.Success);
            await Assert.That(actual.Value).IsEqualTo(WasmValue.FromI64(-1));
            await Assert.That(memory.PageCount).IsEqualTo(1u);
            await Assert.That(bytes[0]).IsEqualTo((byte)42);
        }
    }
}
