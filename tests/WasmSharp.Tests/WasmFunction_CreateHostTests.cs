namespace WasmSharp.Tests;

internal class WasmFunction_CreateHostTests
{
    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task 型またはcallbackがnull_生成時に引数名付きで拒否する(
        bool withInstance,
        bool nullType
    )
    {
        // Arrange
        var type = nullType ? null! : new WasmFunctionType([], []);
        WasmHostCallback callback = nullType ? _ => new([]) : null!;
        WasmHostInstanceCallback instanceCallback = nullType ? (_, _) => new([]) : null!;

        // Act & Assert
        var exception = await Assert
            .That(() =>
                withInstance
                    ? WasmFunction.CreateHost(type, instanceCallback)
                    : WasmFunction.CreateHost(type, callback)
            )
            .ThrowsExactly<ArgumentNullException>();
        await Assert.That(exception!.ParamName).IsEqualTo(nullType ? "type" : "callback");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 両形式を明示型で生成する_型と選択したcallbackだけを保持する(bool withInstance)
    {
        // Arrange
        var type = new WasmFunctionType([WasmValueKind.I32], [WasmValueKind.ExternRef]);
        WasmHostCallback callback = _ => new([]);
        WasmHostInstanceCallback instanceCallback = (_, _) => new([]);

        // Act
        var function = withInstance
            ? WasmFunction.CreateHost(type, instanceCallback)
            : WasmFunction.CreateHost(type, callback);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ReferenceEquals(function.Type, type)).IsTrue();
            await Assert.That(function.IsHost).IsTrue();
            await Assert
                .That(ReferenceEquals(function.HostCallback, withInstance ? null : callback))
                .IsTrue();
            await Assert
                .That(
                    ReferenceEquals(
                        function.HostInstanceCallback,
                        withInstance ? instanceCallback : null
                    )
                )
                .IsTrue();
        }
    }
}
