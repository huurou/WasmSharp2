using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal class WasmTable_TryFillTests
{
    [Test]
    [Arguments(WasmValueKind.FuncRef, false)]
    [Arguments(WasmValueKind.FuncRef, true)]
    [Arguments(WasmValueKind.ExternRef, false)]
    [Arguments(WasmValueKind.ExternRef, true)]
    public async Task 末尾まで同じ参照で埋める_範囲外を保ち参照型と同一性を保持する(
        WasmValueKind kind,
        bool isNull
    )
    {
        // Arrange
        var reference =
            kind == WasmValueKind.FuncRef
                ? WasmModule
                    .Decode(ConstantModuleBinary.Create(0x7F, 0x41, 0x01, 0x0B))
                    .Validate()
                    .Instantiate([])
                    .GetFunction("run")
                : new object();
        var original =
            kind == WasmValueKind.FuncRef
                ? WasmValue.FromFuncRef((WasmFunction)reference)
                : WasmValue.FromExternRef(reference);
        var value =
            kind == WasmValueKind.FuncRef
                ? WasmValue.FromFuncRef(isNull ? null : (WasmFunction)reference)
                : WasmValue.FromExternRef(isNull ? null : reference);
        var table = new WasmTable(kind, new WasmLimits(4));
        table.Set(0, original);
        table.Set(3, original);

        // Act
        var success = table.TryFill(1, value, 3);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(success).IsTrue();
            await Assert.That(table.Count).IsEqualTo(4u);
            await Assert.That(table.Get(0)).IsEqualTo(original);
            for (uint index = 1; index < table.Count; index++)
            {
                var saved = table.Get(index);
                await Assert.That(saved.Kind).IsEqualTo(kind);
                await Assert
                    .That(
                        ReferenceEquals(
                            kind == WasmValueKind.FuncRef ? saved.AsFuncRef() : saved.AsExternRef(),
                            isNull ? null : reference
                        )
                    )
                    .IsTrue();
            }
        }
    }

    [Test]
    [Arguments(0u, 0u)]
    [Arguments(2u, 0u)]
    [Arguments(2u, 2u)]
    public async Task 長さがゼロで開始位置が領域内または末尾にある_成功する(
        uint count,
        uint destination
    )
    {
        // Arrange
        var table = new WasmTable(WasmValueKind.ExternRef, new WasmLimits(count));

        // Act
        var success = table.TryFill(destination, WasmValue.FromExternRef(new object()), 0);

        // Assert
        await Assert.That(success).IsTrue();
    }

    [Test]
    [Arguments(2u, 2u)]
    [Arguments(4u, 0u)]
    [Arguments(uint.MaxValue, 1u)]
    [Arguments(1u, uint.MaxValue)]
    public async Task 充填先の範囲が不正である_内容を一部も変更せずfalseを返す(
        uint destination,
        uint length
    )
    {
        // Arrange
        var table = new WasmTable(WasmValueKind.ExternRef, new WasmLimits(3));
        var references = new[] { new object(), new object(), new object() };
        for (uint index = 0; index < table.Count; index++)
        {
            table.Set(index, WasmValue.FromExternRef(references[index]));
        }

        // Act
        var success = table.TryFill(destination, WasmValue.FromExternRef(null), length);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(success).IsFalse();
            await Assert.That(table.Count).IsEqualTo(3u);
            for (uint index = 0; index < table.Count; index++)
            {
                await Assert
                    .That(ReferenceEquals(table.Get(index).AsExternRef(), references[index]))
                    .IsTrue();
            }
        }
    }
}
