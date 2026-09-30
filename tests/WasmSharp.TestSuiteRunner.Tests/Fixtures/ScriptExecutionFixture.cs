using System.Collections.Immutable;
using System.Text;
using WasmSharp.TestSuiteRunner.Corpus;
using WasmSharp.TestSuiteRunner.Execution;
using WasmSharp.TestSuiteRunner.Reports;

namespace WasmSharp.TestSuiteRunner.Tests.Fixtures;

internal static class ScriptExecutionFixture
{
    internal static byte[] Empty => Binary();

    internal static ImmutableArray<CaseResult> Execute(
        string commands,
        ScriptState state,
        params (int Index, byte[] Binary)[] modules
    )
    {
        var document = ScriptDocument.Parse(
            Encoding.UTF8.GetBytes(
                "{\"source_filename\":\"a.wast\",\"commands\":[" + commands + "]}"
            ),
            "a.json"
        );
        var input = new InputVerification(
            new(state.InputPath, new string('a', 64)),
            document,
            [],
            modules.ToImmutableDictionary(x => x.Index, x => ImmutableArray.Create(x.Binary)),
            []
        );
        return new ScriptExecutor(input).Execute(
            ScriptReader.Read(document, state.InputPath),
            state
        );
    }

    internal static string Module(int index, string? name = null)
    {
        return "{\"type\":\"module\",\"line\":1,\"filename\":\"a."
            + index
            + ".wasm\""
            + (name is null ? "" : ",\"name\":\"" + name + "\"")
            + "}";
    }

    internal static byte[] ImportFunctions(params string[] names)
    {
        return Binary(
            (1, [1, 0x60, 0, 0]),
            (
                2,
                [
                    (byte)names.Length,
                    .. names.SelectMany(x => (byte[])[.. Name(x), .. Name("f"), 0, 0]),
                ]
            )
        );
    }

    internal static byte[] Exports()
    {
        return Binary(
            (1, [1, 0x60, 0, 1, 0x7F]),
            (3, [1, 0]),
            (4, [1, 0x70, 1, 1, 2]),
            (5, [1, 1, 1, 2]),
            (6, [1, 0x7F, 1, 0x41, 0, 0x0B]),
            (
                7,
                [4, .. Name("f"), 0, 0, .. Name("t"), 1, 0, .. Name("m"), 2, 0, .. Name("g"), 3, 0]
            ),
            (10, [1, 4, 0, 0x41, 7, 0x0B])
        );
    }

    internal static byte[] ImportExports(string name)
    {
        return Binary(
            (1, [1, 0x60, 0, 1, 0x7F]),
            (
                2,
                [
                    4,
                    .. Name(name),
                    .. Name("f"),
                    0,
                    0,
                    .. Name(name),
                    .. Name("t"),
                    1,
                    0x70,
                    1,
                    1,
                    2,
                    .. Name(name),
                    .. Name("m"),
                    2,
                    1,
                    1,
                    2,
                    .. Name(name),
                    .. Name("g"),
                    3,
                    0x7F,
                    1,
                ]
            ),
            (7, [4, .. Name("f"), 0, 0, .. Name("t"), 1, 0, .. Name("m"), 2, 0, .. Name("g"), 3, 0])
        );
    }

    internal static byte[] Binary(params (byte Id, byte[] Payload)[] sections)
    {
        return
        [
            0,
            0x61,
            0x73,
            0x6D,
            1,
            0,
            0,
            0,
            .. sections.SelectMany(x =>
                (byte[])[x.Id, .. Unsigned((uint)x.Payload.Length), .. x.Payload]
            ),
        ];
    }

    internal static byte[] Name(string name)
    {
        var bytes = Encoding.UTF8.GetBytes(name);
        return [.. Unsigned((uint)bytes.Length), .. bytes];
    }

    private static byte[] Unsigned(uint value)
    {
        List<byte> bytes = [];
        do
        {
            var next = (byte)(value & 0x7F);
            value >>= 7;
            bytes.Add(value == 0 ? next : (byte)(next | 0x80));
        } while (value != 0);
        return [.. bytes];
    }
}
