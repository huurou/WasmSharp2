using WasmSharp.Execution;
using WasmSharp.Instructions;

namespace WasmSharp.Tests.Fixtures;

internal static class InstructionFixture
{
    public static Instruction Create(uint code, long byteOffset, uint index = 0, WasmValue immediate = default)
    {
        InstructionSet.TryGet(new(0, code), WasmSpecVersion.Core2, out var descriptor);
        return new(descriptor.ExecutionOpcode!.Value, immediate, byteOffset, index);
    }
}
