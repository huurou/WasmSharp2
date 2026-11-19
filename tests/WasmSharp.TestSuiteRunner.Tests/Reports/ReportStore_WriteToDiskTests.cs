using WasmSharp.TestSuiteRunner.Reports;
using WasmSharp.TestSuiteRunner.Tests.Fixtures;

namespace WasmSharp.TestSuiteRunner.Tests.Reports;

internal class ReportStore_WriteToDiskTests
{
    [Test]
    public async Task 内容を書き終える_全バイトをディスクへ同期する()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        using var stream = new ObservedFileStream(directory.Combine("run.json"));

        // Act
        ReportStore.WriteToDisk(stream, x => x.Write("{}"u8));

        // Assert
        await Assert.That(stream.SyncedLength).IsEqualTo(2L);
    }

    private sealed class ObservedFileStream(string path) : FileStream(path, FileMode.CreateNew)
    {
        internal long? SyncedLength { get; private set; }

        public override void Flush(bool flushToDisk)
        {
            base.Flush(flushToDisk);
            if (flushToDisk)
            {
                SyncedLength = Length;
            }
        }
    }
}
