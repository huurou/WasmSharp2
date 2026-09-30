namespace WasmSharp.TestSuiteRunner.Tests.Fixtures;

internal sealed class ProgressWriter(Action<string?> onWrite) : StringWriter
{
    public override void WriteLine(string? value)
    {
        onWrite(value);
        base.WriteLine(value);
    }
}
