using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal class WasmTable_TryGrowTests
{
    [Test]
    [Arguments(WasmValueKind.FuncRef)]
    [Arguments(WasmValueKind.ExternRef)]
    public async Task 非null参照を指定して増大する_既存と追加の参照同一性を保持する(
        WasmValueKind kind
    )
    {
        // Arrange
        var module = WasmModule
            .Decode(ConstantModuleBinary.Create(0x7F, 0x41, 0x01, 0x0B))
            .Validate();
        var original =
            kind == WasmValueKind.FuncRef
                ? module.Instantiate([]).GetFunction("run")
                : new object();
        var added =
            kind == WasmValueKind.FuncRef
                ? module.Instantiate([]).GetFunction("run")
                : new object();
        var originalValue =
            kind == WasmValueKind.FuncRef
                ? WasmValue.FromFuncRef((WasmFunction)original)
                : WasmValue.FromExternRef(original);
        var addedValue =
            kind == WasmValueKind.FuncRef
                ? WasmValue.FromFuncRef((WasmFunction)added)
                : WasmValue.FromExternRef(added);
        var table = new WasmTable(kind, new WasmLimits(1, 3));
        table.Set(0, originalValue);
        var alias = table;

        // Act
        var success = alias.TryGrow(2, addedValue, out var previous);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(success).IsTrue();
            await Assert.That(previous).IsEqualTo(1u);
            await Assert.That(table.Count).IsEqualTo(3u);
            await Assert.That(table.MaximumElements).IsEqualTo(3u);
            for (uint index = 0; index < 3; index++)
            {
                var value = table.Get(index);
                await Assert.That(value.Kind).IsEqualTo(kind);
                var reference =
                    kind == WasmValueKind.FuncRef ? value.AsFuncRef() : value.AsExternRef();
                await Assert
                    .That(ReferenceEquals(reference, index == 0 ? original : added))
                    .IsTrue();
            }
        }
    }

    [Test]
    [Arguments(WasmValueKind.FuncRef)]
    [Arguments(WasmValueKind.ExternRef)]
    public async Task 空のtableを型別nullで増大する_全追加要素を指定型のnullにする(
        WasmValueKind kind
    )
    {
        // Arrange
        var table = new WasmTable(kind, new WasmLimits(0));
        var initial =
            kind == WasmValueKind.FuncRef
                ? WasmValue.FromFuncRef(null)
                : WasmValue.FromExternRef(null);

        // Act
        var success = table.TryGrow(2, initial, out var previous);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(success).IsTrue();
            await Assert.That(previous).IsEqualTo(0u);
            await Assert.That(table.Count).IsEqualTo(2u);
            await Assert.That(table.Get(0)).IsEqualTo(initial);
            await Assert.That(table.Get(1)).IsEqualTo(initial);
        }
    }

    [Test]
    public async Task 最大値で増大量ゼロを指定する_内容を保って現在サイズを返す()
    {
        // Arrange
        var table = new WasmTable(WasmValueKind.ExternRef, new WasmLimits(1, 1));
        var reference = new object();
        table.Set(0, WasmValue.FromExternRef(reference));

        // Act
        var success = table.TryGrow(0, WasmValue.FromExternRef(null), out var previous);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(success).IsTrue();
            await Assert.That(previous).IsEqualTo(1u);
            await Assert.That(table.Count).IsEqualTo(1u);
            await Assert.That(ReferenceEquals(table.Get(0).AsExternRef(), reference)).IsTrue();
        }
    }

    [Test]
    [Arguments(0u)]
    [Arguments(1u)]
    public async Task 初期参照の型が異なる_増大量や上限に関わらず拒否して内容を保つ(uint delta)
    {
        // Arrange
        var table = new WasmTable(WasmValueKind.ExternRef, new WasmLimits(1, 1));
        var reference = new object();
        table.Set(0, WasmValue.FromExternRef(reference));

        // Act & Assert
        await Assert
            .That(() => table.TryGrow(delta, WasmValue.FromFuncRef(null), out _))
            .ThrowsExactly<ArgumentException>();
        using (Assert.Multiple())
        {
            await Assert.That(table.Count).IsEqualTo(1u);
            await Assert.That(ReferenceEquals(table.Get(0).AsExternRef(), reference)).IsTrue();
        }
    }

    [Test]
    [Arguments(1u, 1u)]
    [Arguments(null, uint.MaxValue)]
    public async Task 宣言または仕様上限を超える_現在サイズを返して内容を保ちfalseになる(
        uint? maximum,
        uint delta
    )
    {
        // Arrange
        var table = new WasmTable(WasmValueKind.ExternRef, new WasmLimits(1, maximum));
        var reference = new object();
        table.Set(0, WasmValue.FromExternRef(reference));

        // Act
        var success = table.TryGrow(delta, WasmValue.FromExternRef(null), out var previous);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(success).IsFalse();
            await Assert.That(previous).IsEqualTo(1u);
            await Assert.That(table.Count).IsEqualTo(1u);
            await Assert.That(ReferenceEquals(table.Get(0).AsExternRef(), reference)).IsTrue();
        }
    }

    [Test]
    public async Task 配列保持上限を超える_割当前にfalseを返して元の参照とサイズを保つ()
    {
        // Arrange
        var table = new WasmTable(WasmValueKind.ExternRef, new WasmLimits(1, uint.MaxValue));
        var reference = new object();
        table.Set(0, WasmValue.FromExternRef(reference));

        // Act
        var success = table.TryGrow(
            (uint)Array.MaxLength,
            WasmValue.FromExternRef(null),
            out var previous
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(success).IsFalse();
            await Assert.That(previous).IsEqualTo(1u);
            await Assert.That(table.Count).IsEqualTo(1u);
            await Assert.That(ReferenceEquals(table.Get(0).AsExternRef(), reference)).IsTrue();
        }
    }
}
