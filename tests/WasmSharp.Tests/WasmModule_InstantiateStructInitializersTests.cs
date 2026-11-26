using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_InstantiateTests
{
    [Test]
    public async Task 異なる具体型の構造体を入れ子にしてglobalを初期化する_宣言順のフィールドから格納済みの値を読める()
    {
        // Arrange
        byte[] packedType = [0x5F, 1, 0x78, 0]; // (struct (field i8))
        byte[] wideType = [0x5F, 1, 0x7E, 0]; // (struct (field i64))
        byte[] pairType = [0x5F, 2, 0x64, 0, 0, 0x64, 1, 0]; // (struct (field (ref 0)) (field (ref 1)))
        byte[] packed = [0x41, .. HostLinkingModuleBinary.Signed(384), 0xFB, 0, 0];
        byte[] wide = [0x42, 37, 0xFB, 0, 1];
        byte[] readPacked = [0x23, 0, 0xFB, 2, 2, 0, 0xFB, 3, 0, 0]; // 左の構造体のi8を符号拡張する。
        byte[] readWide = [0x23, 0, 0xFB, 2, 2, 1, 0xFB, 2, 1, 0]; // 右の構造体のi64を取り出す。
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(1, [4, .. packedType, .. wideType, .. pairType, 0x60, 0, 2, 0x7F, 0x7E]),
            HostLinkingModuleBinary.Functions(3),
            HostLinkingModuleBinary.Section(6, [1, 0x64, 2, 0, .. packed, .. wide, 0xFB, 0, 2, 0x0B]),
            HostLinkingModuleBinary.Exports(("read", 0, 0)),
            HostLinkingModuleBinary.Code(([], [.. readPacked, .. readWide, 0x0B]))
        );
        var module = WasmModule.Decode(binary, WasmSpecVersion.Core3).Validate();
        var context = new WasmExecutionContext();

        // Act
        var instance = module.Instantiate(context, []);
        var values = instance.GetFunction("read").Invoke(context, []).Values;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(values[0].AsI32()).IsEqualTo(-128);
            await Assert.That(values[1].AsI64()).IsEqualTo(37L);
        }
    }

    [Test]
    public async Task 同じ深さの定数式で構造体を初期化する_命令が増えてもインスタンス化の割り当ては増えない()
    {
        // Arrange
        var shortExpression = CreateModule(1);
        var longExpression = CreateModule(512);
        var context = new WasmExecutionContext();
        for (var index = 0; index < 4; index++)
        {
            MeasureAllocation(shortExpression);
            MeasureAllocation(longExpression);
        }

        // Act
        var shortAllocation = MeasureAllocation(shortExpression);
        var longAllocation = MeasureAllocation(longExpression);

        // Assert
        await Assert.That(longAllocation).IsLessThanOrEqualTo(shortAllocation);

        static WasmModule CreateModule(int additions)
        {
            // 加算の直後に次のi32を積み、どちらの式も最大2個の値から同じ構造体を生成する。
            byte[] addZero = [0x41, 0, 0x6A];
            var expression = Enumerable.Repeat(addZero, additions).SelectMany(x => x);
            var binary = HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Section(1, [1, 0x5F, 1, 0x7F, 0]),
                HostLinkingModuleBinary.Section(6, [1, 0x64, 0, 0, 0x41, 7, .. expression, 0xFB, 0, 0, 0x0B])
            );
            return WasmModule.Decode(binary, WasmSpecVersion.Core3).Validate();
        }

        long MeasureAllocation(WasmModule module)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            GC.KeepAlive(module.Instantiate(context, []));
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
    }
}
