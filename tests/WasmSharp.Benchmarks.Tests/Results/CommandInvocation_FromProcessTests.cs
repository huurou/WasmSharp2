using WasmSharp.Benchmarks.Results;

namespace WasmSharp.Benchmarks.Tests.Results;

internal class CommandInvocation_FromProcessTests
{
    [Test]
    [Arguments("dotnet", true)]
    [Arguments("DOTNET.exe", true)]
    [Arguments("WasmSharp.Benchmarks", false)]
    [Arguments("WasmSharp.Benchmarks.exe", false)]
    public async Task 起動方法がdotnetまたはapphost_同じコマンドを再現できる引数列を保持する(string program, bool includeAssembly)
    {
        // Arrange
        var executable = Path.Combine("bin", program);
        const string Assembly = "WasmSharp.Benchmarks.dll";
        string[] arguments = ["run", "--output", "folder with spaces"];
        string[] commandLine = [includeAssembly ? Assembly : executable, .. arguments];
        var expected = includeAssembly ? [Assembly, .. arguments] : arguments;
        var workingDirectory = Path.GetTempPath();

        // Act
        var command = CommandInvocation.FromProcess(executable, commandLine, workingDirectory);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(command.ExecutablePath).IsEqualTo(executable);
            await Assert.That(command.Arguments.SequenceEqual(expected)).IsTrue();
            await Assert.That(command.WorkingDirectory).IsEqualTo(workingDirectory);
        }
    }
}
