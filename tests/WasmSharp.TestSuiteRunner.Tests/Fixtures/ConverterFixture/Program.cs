using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

Console.OutputEncoding = Encoding.UTF8;
var outputIndex = Array.IndexOf(args, "-o");
if (args.Length == 0 || outputIndex < 1 || outputIndex + 1 >= args.Length)
{
    Console.Error.WriteLine("使用法: ConverterFixture <input.wast> -o <output.json>");
    return 2;
}

Console.WriteLine(
    JsonSerializer.Serialize(
        new { arguments = args, working_directory = Environment.CurrentDirectory }
    )
);
var inputPath = args[0];
var outputPath = Path.GetFullPath(args[outputIndex + 1]);
try
{
    return Convert(inputPath, outputPath);
}
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"変換・出力に失敗しました: {outputPath}: {exception.Message}");
    return 2;
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
    var script = JsonNode.Parse(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "minimal-script.json"))
    )!;
    script["source_filename"] = inputPath;
    script["commands"]![0]!["filename"] = moduleName;
    File.WriteAllText(outputPath, script.ToJsonString());
    if (Path.GetFileName(inputPath) is "partial.wast" or "partial-failure.wast")
    {
        Console.Error.WriteLine("JSONのみ生成しました。参照先binaryは欠落しています。");
        return Path.GetFileName(inputPath) == "partial.wast" ? 0 : 1;
    }

    File.Copy(
        Path.Combine(AppContext.BaseDirectory, "Data", "module.wasm"),
        Path.Combine(Path.GetDirectoryName(outputPath)!, moduleName)
    );
    return 0;
}
