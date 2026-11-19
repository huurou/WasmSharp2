using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

Console.OutputEncoding = Encoding.UTF8;
if (args is ["--version"])
{
    Console.WriteLine("wasm-tools 1.263.0 (7a2fa422c 2026-10-30)");
    return 0;
}

if (OperatingSystem.IsWindows() && args is ["--console-status"])
{
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        has_window = WindowsConsole.GetConsoleWindow() != 0,
        attached_process_count = WindowsConsole.GetConsoleProcessList(out _, 1),
    }));
    return 0;
}

var outputIndex = Array.IndexOf(args, "-o");
if (args.Length == 0 || outputIndex < 1 || outputIndex + 1 >= args.Length)
{
    Console.Error.WriteLine("使用法: ConverterFixture <input.wast> -o <output.json>");
    return 2;
}

Console.WriteLine(JsonSerializer.Serialize(new { arguments = args, working_directory = Environment.CurrentDirectory }));
var inputPath = args[0] == "json-from-wast" ? args[1] : args[0];
var outputPath = Path.GetFullPath(args[outputIndex + 1]);
try
{
    return args[0] == "json-from-wast" && inputPath != "failure.wast"
        ? ConvertCore3(inputPath, outputPath, args[Array.IndexOf(args, "--wasm-dir") + 1])
        : Convert(inputPath, outputPath);
}
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"変換・出力に失敗しました: {outputPath}: {exception.Message}");
    return 2;
}

static int ConvertCore3(string inputPath, string outputPath, string moduleDirectory)
{
    var source = File.ReadAllText(inputPath);
    var script = Path.Combine(AppContext.BaseDirectory, "Data", "core3-script.json");
    if (source == "text companion")
    {
        File.WriteAllText(outputPath, """
            {"source_filename":"gc/sample.wast","commands":[
            {"type":"module","line":1,"filename":"sample.0.wat","module_type":"text","binary_filename":"sample.0.wasm"}]}
            """);
        File.WriteAllText(Path.Combine(moduleDirectory, "sample.0.wat"), "(module)");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Data", "module.wasm"), Path.Combine(moduleDirectory, "sample.0.wasm"));
        return 0;
    }
    if (source is "numeric companion" or "duplicate companion")
    {
        var binaryFilename = source == "numeric companion"
            ? "\"binary_filename\":7"
            : "\"binary_filename\":\"sample.2.wasm\",\"binary_filename\":\"duplicate.wasm\"";
        File.WriteAllText(outputPath, File.ReadAllText(script).Replace(
            "\"binary_filename\":\"sample.2.wasm\"", binaryFilename, StringComparison.Ordinal));
    }
    else
    {
        File.Copy(script, outputPath);
    }
    File.Copy(Path.Combine(AppContext.BaseDirectory, "Data", "module.wasm"), Path.Combine(moduleDirectory, "sample.0.wasm"));
    File.WriteAllBytes(Path.Combine(moduleDirectory, "sample.1.wasm"), [0, 0x61, 0x73]);
    File.WriteAllText(Path.Combine(moduleDirectory, "sample.2.wat"), "(module)");
    if (source is not ("missing companion" or "numeric companion" or "duplicate companion"))
    {
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Data", "module.wasm"), Path.Combine(moduleDirectory, "sample.2.wasm"));
    }
    File.WriteAllText(Path.Combine(moduleDirectory, "sample.3.wat"), "(module ?)");
    return 0;
}

static int Convert(string inputPath, string outputPath)
{
    _ = File.ReadAllBytes(inputPath);
    if (Path.GetFileName(inputPath) == "failure.wast")
    {
        Console.Error.WriteLine("failure.wastの変換に失敗しました。");
        return 1;
    }

    var moduleName = Path.GetFileNameWithoutExtension(outputPath) + ".0.wasm";
    var script = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "minimal-script.json")))!;
    script["source_filename"] = inputPath;
    script["commands"]![0]!["filename"] = moduleName;
    if (Path.GetFileName(inputPath) == "partial-modules.wast")
    {
        var second = script["commands"]![0]!.DeepClone();
        second["filename"] = Path.GetFileNameWithoutExtension(outputPath) + ".1.wasm";
        script["commands"]!.AsArray().Add(second);
    }

    File.WriteAllText(outputPath, script.ToJsonString());
    if (Path.GetFileName(inputPath) is "partial.wast" or "partial-failure.wast" or "partial-modules.wast")
    {
        Console.Error.WriteLine("JSONのみ生成しました。参照先binaryは欠落しています。");
        return Path.GetFileName(inputPath) == "partial-failure.wast" ? 1 : 0;
    }

    File.Copy(
        Path.Combine(AppContext.BaseDirectory, "Data", "module.wasm"),
        Path.Combine(Path.GetDirectoryName(outputPath)!, moduleName)
    );
    return 0;
}

internal static partial class WindowsConsole
{
    [LibraryImport("kernel32.dll")]
    internal static partial nint GetConsoleWindow();

    [LibraryImport("kernel32.dll")]
    internal static partial uint GetConsoleProcessList(out uint processId, uint capacity);
}
