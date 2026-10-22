using WasmSharp.Benchmarks.Results;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Results;

internal class RunStore_SaveFileTests
{
    [Test]
    public async Task runの外を保存先に指定する_書き込みを拒否する()
    {
        // Arrange
        using var fixture = new RunStoreFixture();
        var store = RunStore.Create(fixture.DirectoryPath, fixture.Manifest);

        // Act & Assert
        await Assert
            .That(() => store.SaveFile("../outside.txt", "outside"u8))
            .ThrowsExactly<ArgumentException>();
        await Assert
            .That(() =>
                store.SaveFile(Path.Combine(fixture.DirectoryPath, "absolute.txt"), "outside"u8)
            )
            .ThrowsExactly<ArgumentException>();
    }
}
