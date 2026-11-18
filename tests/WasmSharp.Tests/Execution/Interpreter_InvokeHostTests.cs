using WasmSharp.Execution;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests.Execution;

internal class Interpreter_InvokeHostTests
{
    [Test]
    [Arguments(0, "結果の個数")]
    [Arguments(1, "結果の個数")]
    [Arguments(2, "結果の型")]
    public async Task Callback結果がnullまたは個数か型が異なる_原因を示して呼出しを中断する(
        int invalidResult,
        string expectedReason
    )
    {
        // Arrange
        var executionContext = new WasmExecutionContext();
        var function = WasmFunction.CreateHost(
            new([], [WasmValueKind.I32]),
            (_, _) =>
                invalidResult switch
                {
                    0 => null!,
                    1 => new([]),
                    _ => new([WasmValue.FromI64(42)]),
                }
        );

        // Act & Assert
        var exception = await Assert
            .That(() => Interpreter.InvokeHost(executionContext, function, null, []))
            .ThrowsExactly<InvalidOperationException>();
        await Assert.That(exception!.Message).Contains(expectedReason);
    }

    [Test]
    public async Task Callbackが例外を投げる_元の例外実体を伝播する()
    {
        // Arrange
        var executionContext = new WasmExecutionContext();
        var expected = new InvalidOperationException("ホストの失敗");
        var function = WasmFunction.CreateHost(new([], []), (WasmHostCallback)((_, _) => throw expected));

        // Act & Assert
        var actual = await Assert
            .That(() => Interpreter.InvokeHost(executionContext, function, null, []))
            .ThrowsExactly<InvalidOperationException>();
        await Assert.That(actual).IsSameReferenceAs(expected);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 両形式のcallbackを呼ぶ_引数の順序と参照を保ち呼出し専用コピーと指定instanceを渡す(bool withInstance)
    {
        // Arrange
        var executionContext = new WasmExecutionContext();
        var instance = WasmModule.Decode(HostLinkingModuleBinary.Create()).Validate().Instantiate(executionContext, []);
        var reference = new object();
        WasmValue[] arguments = [WasmValue.FromI32(42), WasmValue.FromExternRef(reference)];
        WasmInstance? observedInstance = null;
        WasmExecutionContext? observedContext = null;
        WasmResults Callback(WasmExecutionContext callbackContext, ReadOnlySpan<WasmValue> values)
        {
            observedContext = callbackContext;
            arguments[0] = WasmValue.FromI32(-1);
            return new([values[1], values[0]]);
        }
        var type = new WasmFunctionType(
            [WasmValueKind.I32, WasmValueKind.ExternRef],
            [WasmValueKind.ExternRef, WasmValueKind.I32]
        );
        var function = withInstance
            ? WasmFunction.CreateHost(
                type,
                (callbackContext, target, values) =>
                {
                    observedInstance = target;
                    return Callback(callbackContext, values);
                }
            )
            : WasmFunction.CreateHost(type, Callback);

        // Act
        var result = Interpreter.InvokeHost(executionContext, function, instance, arguments);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Values[0].AsExternRef()).IsSameReferenceAs(reference);
            await Assert.That(result.Values[1].AsI32()).IsEqualTo(42);
            await Assert.That(observedInstance).IsSameReferenceAs(withInstance ? instance : null);
            await Assert.That(observedContext).IsSameReferenceAs(executionContext);
        }
    }
}
