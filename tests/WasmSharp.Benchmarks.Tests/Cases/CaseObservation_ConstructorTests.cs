using WasmSharp.Benchmarks.Cases;

namespace WasmSharp.Benchmarks.Tests.Cases;

internal class CaseObservation_ConstructorTests
{
    [Test]
    public async Task 各段階の観測を作る_moduleとinstanceとInvoke結果を区別して保持する()
    {
        // Arrange
        var module = WasmModule.Decode(new byte[] { 0, 0x61, 0x73, 0x6d, 1, 0, 0, 0 });
        var instance = module.Validate().Instantiate(new WasmImports());
        var results = new WasmResults([
            WasmValue.FromI32(-1),
            WasmValue.FromI64(long.MinValue),
            WasmValue.FromF32Bits(0x80000000U),
            WasmValue.FromF64Bits(0x7ff8000000000042UL),
        ]);

        // Act
        CaseObservation decoded = new ModuleObservation(module);
        CaseObservation instantiated = new InstanceObservation(instance);
        CaseObservation invoked = new InvokeObservation(instance, results);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(decoded is ModuleObservation).IsTrue();
            await Assert.That(instantiated is InstanceObservation).IsTrue();
            await Assert.That(invoked is InvokeObservation).IsTrue();
            await Assert
                .That(ReferenceEquals(((ModuleObservation)decoded).Module, module))
                .IsTrue();
            await Assert
                .That(ReferenceEquals(((InstanceObservation)instantiated).Instance, instance))
                .IsTrue();
            var actual = (InvokeObservation)invoked;
            await Assert.That(ReferenceEquals(actual.Instance, instance)).IsTrue();
            await Assert.That(ReferenceEquals(actual.Results, results)).IsTrue();
            await Assert.That(actual.Results.Values.Length).IsEqualTo(4);
            await Assert.That(actual.Results.Values[0].AsI32()).IsEqualTo(-1);
            await Assert.That(actual.Results.Values[1].AsI64()).IsEqualTo(long.MinValue);
            await Assert.That(actual.Results.Values[2].AsF32Bits()).IsEqualTo(0x80000000U);
            await Assert.That(actual.Results.Values[3].AsF64Bits()).IsEqualTo(0x7ff8000000000042UL);
        }
    }
}
