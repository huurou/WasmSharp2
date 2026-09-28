using WasmSharp.TestSuiteRunner.Reports;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests.Reports;

internal class ReportStore_WriteTests
{
    [Test]
    public async Task 書込み中の内容を観測する_同じディレクトリの一時ファイルに書き保存先へは確定後に現れる()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var path = directory.Combine("run.json");
        string[] filesDuringWrite = [];
        var existedDuringWrite = true;

        // Act
        ReportStore.Write(
            path,
            replaceExisting: false,
            x =>
            {
                x.Write("{}"u8);
                x.Flush();
                filesDuringWrite = Directory.GetFiles(directory.Root);
                existedDuringWrite = File.Exists(path);
            }
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(existedDuringWrite).IsFalse();
            await Assert.That(filesDuringWrite.Length).IsEqualTo(1);
            await Assert.That(filesDuringWrite[0]).EndsWith(".tmp");
            await Assert.That(await File.ReadAllTextAsync(path)).IsEqualTo("{}");
            await Assert.That(Directory.GetFiles(directory.Root)).Count().IsEqualTo(1);
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task 書込み中に中断する_一時ファイルを残さず既存の結果を保持する(
        bool replaceExisting
    )
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var path = directory.Combine("run.json");
        await File.WriteAllTextAsync(path, "既存の結果");

        // Act & Assert
        await Assert
            .That(() =>
                ReportStore.Write(
                    path,
                    replaceExisting,
                    x =>
                    {
                        x.Write("""{"schema_version": 1, "ki"""u8);
                        throw new OperationCanceledException();
                    }
                )
            )
            .ThrowsExactly<OperationCanceledException>();
        using (Assert.Multiple())
        {
            await Assert.That(await File.ReadAllTextAsync(path)).IsEqualTo("既存の結果");
            await Assert.That(Directory.GetFiles(directory.Root)).Count().IsEqualTo(1);
        }
    }

    [Test]
    public async Task 確定前に保存先が作られる_競合したファイルを上書きせず保存失敗を報告する()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var path = directory.Combine("run.json");

        // Act & Assert
        var exception = await Assert
            .That(() =>
                ReportStore.Write(
                    path,
                    replaceExisting: false,
                    x =>
                    {
                        x.Write("{}"u8);
                        File.WriteAllText(path, "競合した結果");
                    }
                )
            )
            .ThrowsExactly<ReportStoreException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).Contains(path);
            await Assert.That(await File.ReadAllTextAsync(path)).IsEqualTo("競合した結果");
            await Assert.That(Directory.GetFiles(directory.Root)).Count().IsEqualTo(1);
        }
    }
}
