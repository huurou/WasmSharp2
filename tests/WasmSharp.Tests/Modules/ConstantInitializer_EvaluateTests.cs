using System.Collections.Immutable;
using WasmSharp.Modules;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Modules;

internal class ConstantInitializer_EvaluateTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 検証済みの定数またはglobal取得式を渡す_I32の値を保持して評価する(
        bool useGlobal
    )
    {
        // Arrange
        ImmutableArray<DecodedInstruction> expression =
        [
            useGlobal
                ? DecodedInstruction.WithIndex(new(0, 0x23), 1, 40)
                : DecodedInstruction.WithImmediate(new(0, 0x41), WasmValue.FromI32(-1), 40),
            DecodedInstruction.WithoutImmediate(new(0, 0x0B), 42),
        ];
        WasmGlobal[] globals =
        [
            new(new(WasmValueKind.I32, false), WasmValue.FromI32(0)),
            new(new(WasmValueKind.I32, false), WasmValue.FromI32(-1)),
        ];

        // Act
        var result = ConstantInitializer.Evaluate(expression, globals, []);

        // Assert
        await Assert.That(result.AsI32()).IsEqualTo(-1);
    }

    [Test]
    [Arguments(WasmValueKind.FuncRef)]
    [Arguments(WasmValueKind.ExternRef)]
    public async Task 参照null式を渡す_参照型を保持してnullを返す(WasmValueKind kind)
    {
        // Arrange
        var value =
            kind == WasmValueKind.FuncRef
                ? WasmValue.FromFuncRef(null)
                : WasmValue.FromExternRef(null);
        ImmutableArray<DecodedInstruction> expression =
        [
            DecodedInstruction.WithImmediate(new(0, 0xD0), value, 40),
            DecodedInstruction.WithoutImmediate(new(0, 0x0B), 42),
        ];

        // Act
        var result = ConstantInitializer.Evaluate(expression, [], []);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Kind).IsEqualTo(kind);
            await Assert
                .That(kind == WasmValueKind.FuncRef ? result.AsFuncRef() : result.AsExternRef())
                .IsNull();
        }
    }

    [Test]
    [Arguments(0u)]
    [Arguments(1u)]
    public async Task Importまたは定義関数の参照式を渡す_構築済みの同じ関数実体を返す(uint index)
    {
        // Arrange
        var hostFunction = WasmFunction.CreateHost(new([], []), _ => new([]));
        var module = WasmModule
            .Decode(
                HostLinkingModuleBinary.Create(
                    HostLinkingModuleBinary.Types(([], [])),
                    HostLinkingModuleBinary.Imports(("env", "f", 0, [0])),
                    HostLinkingModuleBinary.Functions(0),
                    HostLinkingModuleBinary.Code(([], [0x0B]))
                )
            )
            .Validate();
        var instance = new WasmInstance(
            module,
            WasmExecutionOptions.Default,
            [hostFunction],
            [],
            [],
            []
        );
        ImmutableArray<DecodedInstruction> expression =
        [
            DecodedInstruction.WithIndex(new(0, 0xD2), index, 40),
            DecodedInstruction.WithoutImmediate(new(0, 0x0B), 42),
        ];

        // Act
        var result = ConstantInitializer.Evaluate(expression, [], instance.Functions.AsSpan());

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Kind).IsEqualTo(WasmValueKind.FuncRef);
            await Assert.That(result.AsFuncRef()).IsSameReferenceAs(instance.Functions[(int)index]);
        }
    }

    [Test]
    [Arguments(WasmValueKind.FuncRef)]
    [Arguments(WasmValueKind.ExternRef)]
    public async Task 参照globalの取得式を渡す_参照型と同じ参照先を返す(WasmValueKind kind)
    {
        // Arrange
        var function = WasmFunction.CreateHost(new([], []), _ => new([]));
        var external = new object();
        var value =
            kind == WasmValueKind.FuncRef
                ? WasmValue.FromFuncRef(function)
                : WasmValue.FromExternRef(external);
        WasmGlobal[] globals = [new(new(kind, false), value)];
        ImmutableArray<DecodedInstruction> expression =
        [
            DecodedInstruction.WithIndex(new(0, 0x23), 0, 40),
            DecodedInstruction.WithoutImmediate(new(0, 0x0B), 42),
        ];

        // Act
        var result = ConstantInitializer.Evaluate(expression, globals, []);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Kind).IsEqualTo(kind);
            await Assert
                .That(kind == WasmValueKind.FuncRef ? result.AsFuncRef() : result.AsExternRef())
                .IsSameReferenceAs(kind == WasmValueKind.FuncRef ? function : external);
        }
    }
}
