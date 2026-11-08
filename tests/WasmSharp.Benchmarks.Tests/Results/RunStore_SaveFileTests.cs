using WasmSharp.Benchmarks.Cases;
using WasmSharp.Benchmarks.Results;
using WasmSharp.Benchmarks.Tests.Fixtures;

namespace WasmSharp.Benchmarks.Tests.Results;

internal class RunStore_SaveFileTests
{
    private readonly CaseJson json_ = new CaseJson();

    [Test]
    public async Task Runの外を保存先に指定する_書き込みを拒否する()
    {
        // Arrange
        using var fixture = new RunStoreFixture();
        var store = RunStore.Create(fixture.DirectoryPath, fixture.Manifest, json_);

        // Act & Assert
        var failure = await Assert
            .That(() => store.SaveFile("../outside.txt", "outside"u8))
            .ThrowsExactly<ArgumentException>();
        await Assert.That(failure!.Message).Contains("run内").And.Contains("相対path");
        await Assert
            .That(() =>
                store.SaveFile(Path.Combine(fixture.DirectoryPath, "absolute.txt"), "outside"u8)
            )
            .ThrowsExactly<ArgumentException>();
    }
}
