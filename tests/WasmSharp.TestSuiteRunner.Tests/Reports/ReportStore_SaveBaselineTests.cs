using WasmSharp.TestSuiteRunner.Reports;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests.Reports;

internal class ReportStore_SaveBaselineTests
{
    [Test]
    public async Task 既存baselineがある_指定した内容で明示的に置換する()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var path = directory.Combine("baseline.json");
        await File.WriteAllTextAsync(path, "旧baseline");
        byte[] content = [0x7b, 0x7d, 0x0a];

        // Act
        ReportStore.SaveBaseline(content, path);

        // Assert
        using (Assert.Multiple())
        {
            await Assert
                .That(Convert.ToHexString(await File.ReadAllBytesAsync(path)))
                .IsEqualTo(Convert.ToHexString(content));
            await Assert.That(Directory.GetFiles(directory.Root, "*.tmp")).IsEmpty();
        }
    }
}
