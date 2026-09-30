using System.Text;

namespace WasmSharp.TestSuiteRunner.Tests.Fixtures;

internal static class SpectestModuleFixture
{
    internal static readonly string[] functionNames_ =
    [
        "print",
        "print_i32",
        "print_i64",
        "print_f32",
        "print_f64",
        "print_i32_f32",
        "print_f64_f64",
    ];

    internal static readonly byte[][] parameterTypes_ =
    [
        [],
        [0x7F],
        [0x7E],
        [0x7D],
        [0x7C],
        [0x7F, 0x7D],
        [0x7C, 0x7C],
    ];

    internal static WasmInstance Instantiate(WasmHostModule host)
    {
        var imports = new WasmImports();
        imports.Add(host);
        return Create().Instantiate(imports);
    }

    internal static WasmModule Create()
    {
        var imports = functionNames_
            .Select((x, i) => (x, (byte)0, new byte[] { (byte)i }))
            .ToList();
        imports.AddRange([
            ("global_i32", 3, new byte[] { 0x7F, 0 }),
            ("global_i64", 3, new byte[] { 0x7E, 0 }),
            ("global_f32", 3, new byte[] { 0x7D, 0 }),
            ("global_f64", 3, new byte[] { 0x7C, 0 }),
            ("table", 1, new byte[] { 0x70, 1, 10, 20 }),
            ("memory", 2, new byte[] { 1, 1, 2 }),
        ]);
        return Create([.. imports]);
    }

    internal static WasmModule Create(params (string Name, byte Kind, byte[] Type)[] imports)
    {
        return Create(false, imports);
    }

    internal static WasmModule CreateWithStart()
    {
        return Create(true, ("print", 0, [0]));
    }

    private static WasmModule Create(
        bool start,
        params (string Name, byte Kind, byte[] Type)[] imports
    )
    {
        var indices = new uint[4];
        byte[] types =
        [
            7,
            .. parameterTypes_.SelectMany(x => (byte[])[0x60, (byte)x.Length, .. x, 0]),
        ];
        byte[] declarations =
        [
            .. Unsigned((uint)imports.Length),
            .. imports.SelectMany(x =>
                (byte[])[.. Name("spectest"), .. Name(x.Name), x.Kind, .. x.Type]
            ),
        ];
        byte[] exports =
        [
            .. Unsigned((uint)imports.Length),
            .. imports.SelectMany(x =>
                (byte[])[.. Name(x.Name), x.Kind, .. Unsigned(indices[x.Kind]++)]
            ),
        ];
        byte[] binary =
        [
            0,
            0x61,
            0x73,
            0x6D,
            1,
            0,
            0,
            0,
            .. Section(1, types),
            .. Section(2, declarations),
            .. Section(7, exports),
            .. start ? Section(8, [0]) : [],
        ];
        return WasmModule.Decode(binary).Validate();
    }

    internal static WasmModule CreateInvalidImport(string issue)
    {
        return issue switch
        {
            "name" => Create(("missing", 0, [0])),
            "case" => Create(("Print", 0, [0])),
            "kind" => Create(("print", 2, [1, 1, 2])),
            "function" => Create(("print_i32", 0, [0])),
            "global_type" => Create(("global_i32", 3, [0x7E, 0])),
            "global_mutability" => Create(("global_i32", 3, [0x7F, 1])),
            "table_type" => Create(("table", 1, [0x6F, 1, 10, 20])),
            "table_minimum" => Create(("table", 1, [0x70, 1, 11, 20])),
            "table_maximum" => Create(("table", 1, [0x70, 1, 10, 19])),
            "memory_minimum" => Create(("memory", 2, [1, 2, 2])),
            "memory_maximum" => Create(("memory", 2, [1, 1, 1])),
            _ => throw new ArgumentOutOfRangeException(nameof(issue)),
        };
    }

    private static byte[] Section(byte id, byte[] payload)
    {
        return [id, .. Unsigned((uint)payload.Length), .. payload];
    }

    private static byte[] Name(string name)
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
            if (value != 0)
            {
                next |= 0x80;
            }
            bytes.Add(next);
        } while (value != 0);
        return [.. bytes];
    }
}
