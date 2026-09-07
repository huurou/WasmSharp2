using WasmSharp.Instructions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Instructions;

internal class InstructionSet_TryGetTests
{
    [Test]
    [Arguments((byte)0, 183)]
    [Arguments((byte)0xFC, 18)]
    [Arguments((byte)0xFD, 236)]
    public async Task 保存版の命令割当を照合する_番号と名前と欠番が一致し定数と終端だけを実行対象にする(
        byte prefix,
        int expectedCount
    )
    {
        // Arrange
        var expected = Core2InstructionFixture.Read(prefix);
        var actual = new Dictionary<uint, string>();
        var executable = new List<uint>();
        var inconsistent = new List<uint>();

        // Act
        for (uint code = 0; code <= 255; code++)
        {
            if (!InstructionSet.TryGet(new(prefix, code), out var descriptor))
            {
                continue;
            }
            actual.Add(code, descriptor.Name);
            if (descriptor.ExecutionOpcode is not null)
            {
                executable.Add(code);
            }
            else if (
                descriptor.Immediate != ImmediateKind.Unsupported
                || descriptor.StackEffect != StackEffectKind.Unsupported
                || descriptor.Validation != ValidationRule.Unsupported
            )
            {
                inconsistent.Add(code);
            }
        }

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(expected.Count).IsEqualTo(expectedCount);
            await Assert.That(actual).IsEquivalentTo(expected);
            await Assert.That(inconsistent).IsEmpty();
            await Assert
                .That(executable)
                .IsEquivalentTo(prefix == 0 ? new uint[] { 0x0B, 0x41, 0x42, 0x43, 0x44 } : []);
            await Assert.That(InstructionSet.TryGet(new(prefix, 256), out _)).IsFalse();
            await Assert.That(InstructionSet.TryGet(new(prefix, uint.MaxValue), out _)).IsFalse();
        }
    }
}
