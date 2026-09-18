using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal class WasmGlobal_ValueTests
{
    [Test]
    public async Task 全値型を生成して更新する_ビット列と参照同一性を保持する()
    {
        // Arrange
        var function = WasmModule
            .Decode(ConstantModuleBinary.Create(0x7F, 0x41, 0x01, 0x0B))
            .Validate()
            .Instantiate([])
            .GetFunction("run");
        var reference = new object();
        WasmValue[] values =
        [
            WasmValue.FromI32(int.MinValue),
            WasmValue.FromI64(long.MinValue),
            WasmValue.FromF32Bits(0xFFC00042),
            WasmValue.FromF64Bits(0xFFF8000000000042),
            WasmValue.FromV128(0xFEDCBA9876543210, 0x0123456789ABCDEF),
            WasmValue.FromFuncRef(function),
            WasmValue.FromExternRef(reference),
        ];

        foreach (var value in values)
        {
            var global = new WasmGlobal(new WasmGlobalType(value.Kind, true), value);
            var alias = global;
            var initial = global.Value;
            var cleared = value.Kind switch
            {
                WasmValueKind.I32 => WasmValue.FromI32(0),
                WasmValueKind.I64 => WasmValue.FromI64(0),
                WasmValueKind.F32 => WasmValue.FromF32Bits(0x80000000),
                WasmValueKind.F64 => WasmValue.FromF64Bits(0x8000000000000000),
                WasmValueKind.V128 => WasmValue.FromV128(0, 0),
                WasmValueKind.FuncRef => WasmValue.FromFuncRef(null),
                _ => WasmValue.FromExternRef(null),
            };

            // Act
            alias.Value = cleared;

            // Assert
            using (Assert.Multiple())
            {
                await Assert.That(initial).IsEqualTo(value);
                await Assert.That(global.Value).IsEqualTo(cleared);
            }
        }

        var functionGlobal = new WasmGlobal(
            new WasmGlobalType(WasmValueKind.FuncRef, true),
            WasmValue.FromFuncRef(null)
        );
        var externalGlobal = new WasmGlobal(
            new WasmGlobalType(WasmValueKind.ExternRef, true),
            WasmValue.FromExternRef(null)
        );

        // Act
        functionGlobal.Value = WasmValue.FromFuncRef(function);
        externalGlobal.Value = WasmValue.FromExternRef(reference);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ReferenceEquals(functionGlobal.Value.AsFuncRef(), function)).IsTrue();
            await Assert
                .That(ReferenceEquals(externalGlobal.Value.AsExternRef(), reference))
                .IsTrue();
        }
    }

    [Test]
    public async Task 可変globalを共有参照から更新する_現在値を更新し取得済みの値は保持する()
    {
        // Arrange
        var global = new WasmGlobal(
            new WasmGlobalType(WasmValueKind.I32, true),
            WasmValue.FromI32(1)
        );
        var alias = global;
        var previous = global.Value;

        // Act
        alias.Value = WasmValue.FromI32(42);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(global.Value.AsI32()).IsEqualTo(42);
            await Assert.That(previous.AsI32()).IsEqualTo(1);
        }
    }

    [Test]
    public async Task 可変globalへ型違いを設定する_引数例外で拒否して元の値を保つ()
    {
        // Arrange
        var global = new WasmGlobal(
            new WasmGlobalType(WasmValueKind.I32, true),
            WasmValue.FromI32(42)
        );

        // Act & Assert
        await Assert
            .That(() => global.Value = WasmValue.FromI64(7))
            .ThrowsExactly<ArgumentException>();
        await Assert.That(global.Value.AsI32()).IsEqualTo(42);
    }

    [Test]
    public async Task 不変globalを更新する_状態例外で拒否して元の値を保つ()
    {
        // Arrange
        var global = new WasmGlobal(
            new WasmGlobalType(WasmValueKind.I32, false),
            WasmValue.FromI32(42)
        );

        // Act & Assert
        await Assert
            .That(() => global.Value = WasmValue.FromI32(7))
            .ThrowsExactly<InvalidOperationException>();
        await Assert.That(global.Value.AsI32()).IsEqualTo(42);
    }
}
