using System.Runtime.CompilerServices;
using WasmSharp.Modules.Definitions;

namespace WasmSharp.Tests;

internal class WasmStruct_CreateDefaultTests
{
    [Test]
    public async Task 空の構造体を繰り返し生成する_実体のほかにフィールド配列を割り当てない()
    {
        // Arrange
        var type = (WasmStructType)WasmDefinedType.FromDecodedGroup([new TypeDeclaration([], true, [])], firstIndex: 0);
        var instances = new object[128];
        // 同じCLR型のインスタンスだけを割り当て、オブジェクトのサイズやアラインメントを固定せずに比較する。
        var instanceAllocation = Measure(() => RuntimeHelpers.GetUninitializedObject(typeof(WasmStruct)));

        // Act
        var allocated = Measure(() => WasmStruct.CreateDefault(type).AsStructRef());

        // Assert
        await Assert.That(allocated).IsLessThanOrEqualTo(instanceAllocation);

        long Measure(Func<object> create)
        {
            _ = create();
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < instances.Length; index++)
            {
                instances[index] = create();
            }
            var result = GC.GetAllocatedBytesForCurrentThread() - before;
            GC.KeepAlive(instances);
            return result;
        }
    }
}
