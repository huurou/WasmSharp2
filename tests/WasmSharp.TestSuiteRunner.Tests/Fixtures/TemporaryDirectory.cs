using System.Text.Json;

namespace WasmSharp.TestSuiteRunner.Tests.Fixtures;

internal sealed class TemporaryDirectory : IDisposable
{
    private static readonly JsonSerializerOptions jsonOptions_ = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    internal string Root { get; } =
        Directory.CreateTempSubdirectory("WasmSharp.TestSuiteRunner.Tests ").FullName;

    internal string Combine(string name)
    {
        return Path.Combine(Root, name);
    }

    internal async Task<string> WriteJsonAsync<T>(string name, T value)
    {
        var path = Combine(name);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, jsonOptions_));
        return path;
    }

    public void Dispose()
    {
        Directory.Delete(Root, recursive: true);
    }
}
