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
    public async Task フィールド33個の構造体を定数式で生成する_先頭の具体的な参照と末尾の数値を保持する()
    {
        // Arrange
        byte[] numberField = [0x7F, 0];
        var numberFields = Enumerable.Repeat(numberField, 32).SelectMany(x => x);
        var numbers = Enumerable.Range(0, 32).SelectMany(x => new byte[] { 0x41, (byte)x });
        var binary = HostLinkingModuleBinary.Create(
            // 型1の先頭は型0への非null参照で、続く32フィールドはi32を保持する。
            HostLinkingModuleBinary.Section(1, [3, 0x5F, 0, 0x5F, 33, 0x64, 0, 0, .. numberFields, 0x60, 0, 2, 0x7F, 0x7F]),
            HostLinkingModuleBinary.Functions(2),
            HostLinkingModuleBinary.Section(
                6,
                [2, 0x64, 0, 0, 0xFB, 0, 0, 0x0B, 0x64, 1, 0, 0x23, 0, .. numbers, 0xFB, 0, 1, 0x0B]
            ),
            HostLinkingModuleBinary.Exports(("read", 0, 0)),
            // 先頭の参照をglobal 0と比較し、末尾のi32とともに返す。
            HostLinkingModuleBinary.Code(([], [0x23, 1, 0xFB, 2, 1, 0, 0x23, 0, 0xD3, 0x23, 1, 0xFB, 2, 1, 32, 0x0B]))
        );
        var context = new WasmExecutionContext();

        // Act
        var instance = WasmModule.Decode(binary, WasmSpecVersion.Core3).Validate().Instantiate(context, []);
        var values = instance.GetFunction("read").Invoke(context, []).Values;

        // Assert
        await Assert.That((values[0].AsI32(), values[1].AsI32())).IsEqualTo((1, 31));
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 同じ深さの定数式で構造体を初期化する_命令が増えてもインスタンス化の割り当ては増えない(bool keepOperand)
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

        WasmModule CreateModule(int additions)
        {
            // 加算の間に別の値を保持すると最大の深さが2から3になり、評価用スタックが1回拡張される。
            byte[] addZero = [0x41, 0, 0x6A];
            byte[] initial = keepOperand ? [0x41, 0] : [];
            byte[] finish = keepOperand ? [0x6A] : [];
            var expression = Enumerable.Repeat(addZero, additions).SelectMany(x => x);
            var binary = HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Section(1, [1, 0x5F, 1, 0x7F, 0]),
                HostLinkingModuleBinary.Section(
                    6,
                    [1, 0x64, 0, 0, .. initial, 0x41, 7, .. expression, .. finish, 0xFB, 0, 0, 0x0B]
                )
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
