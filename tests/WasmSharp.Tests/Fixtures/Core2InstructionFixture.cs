using System.Text.RegularExpressions;

namespace WasmSharp.Tests.Fixtures;

internal static partial class Core2InstructionFixture
{
    public static Dictionary<uint, string> Read(byte prefix)
    {
        var macros = MacroDefinitionRegex()
            .Matches(ReadResource("Core2.Macros"))
            .ToDictionary(x => x.Groups[1].Value, x => x.Groups[2].Value);
        var instructions = new Dictionary<uint, string>();
        // 固定した公式付録の割当行を読み、Noneで表される欠番は取り込まない。
        foreach (Match row in InstructionRowRegex().Matches(ReadResource("Core2.Instructions")))
        {
            var bytes = HexByteRegex()
                .Matches(row.Groups[2].Value)
                .Select(x => Convert.ToByte(x.Groups[1].Value, 16))
                .ToArray();
            var rowPrefix = bytes.Length == 1 ? 0 : bytes[0];
            if (rowPrefix != prefix)
            {
                continue;
            }
            uint code = bytes[0];
            if (prefix != 0)
            {
                // 付録の拡張opcodeはsubopcode値ではなくLEBのバイト列。
                code = 0;
                for (var i = 1; i < bytes.Length; i++)
                {
                    code |= (uint)(bytes[i] & 0x7F) << ((i - 1) * 7);
                }
            }
            var name = row.Groups[1].Value.Split('~')[0];
            name = MacroReferenceRegex()
                .Replace(name, x => x.Groups[1].Value == "K" ? x.Value : macros[x.Groups[1].Value]);
            name = KeywordRegex().Replace(name, "$1").Replace(@"\_", "_").Replace("{.}", ".");
            instructions.Add(code, name);
        }
        return instructions;
    }

    private static string ReadResource(string name)
    {
        using var stream = typeof(Core2InstructionFixture).Assembly.GetManifestResourceStream(
            name
        )!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [GeneratedRegex(@"^\.\. \|([A-Z0-9]+)\| mathdef::.*?\\K\{(.*)\}\}\r?$", RegexOptions.Multiline)]
    private static partial Regex MacroDefinitionRegex();

    [GeneratedRegex(@"^    Instruction\(r'([^']+)', r'([^']+)'", RegexOptions.Multiline)]
    private static partial Regex InstructionRowRegex();

    [GeneratedRegex(@"\\hex\{([0-9A-F]{2})\}")]
    private static partial Regex HexByteRegex();

    [GeneratedRegex(@"\\([A-Z][A-Z0-9]*)")]
    private static partial Regex MacroReferenceRegex();

    [GeneratedRegex(@"\\K\{([^}]*)\}")]
    private static partial Regex KeywordRegex();
}
