using System.Text;

namespace WasmSharp.Tests.Fixtures;

internal static class HostLinkingModuleBinary
{
    private static readonly UTF8Encoding utf8_ = new(false, true);

    public static byte[] Create(params byte[][] sections)
    {
        return [0x00, 0x61, 0x73, 0x6D, 0x01, 0x00, 0x00, 0x00, .. sections.SelectMany(x => x)];
    }

    public static byte[] Types(params (byte[] Parameters, byte[] Results)[] types)
    {
        return VectorSection(
            1,
            types.Select(x =>
                (byte[])
                    [
                        0x60,
                        .. Unsigned((uint)x.Parameters.Length),
                        .. x.Parameters,
                        .. Unsigned((uint)x.Results.Length),
                        .. x.Results,
                    ]
            )
        );
    }

    public static byte[] Functions(params uint[] typeIndices)
    {
        return VectorSection(3, typeIndices.Select(x => Unsigned(x)));
    }

    public static byte[] Imports(
        params (string Module, string Name, byte Kind, byte[] Type)[] imports
    )
    {
        return VectorSection(
            2,
            imports.Select(x => (byte[])[.. Name(x.Module), .. Name(x.Name), x.Kind, .. x.Type])
        );
    }

    public static byte[] Tables(params (byte ElementType, uint Minimum, uint? Maximum)[] tables)
    {
        return VectorSection(
            4,
            tables.Select(x => (byte[])[x.ElementType, .. Limits(x.Minimum, x.Maximum)])
        );
    }

    public static byte[] Memories(params (uint Minimum, uint? Maximum)[] memories)
    {
        return VectorSection(5, memories.Select(x => Limits(x.Minimum, x.Maximum)));
    }

    public static byte[] Globals(
        params (byte ValueType, bool IsMutable, byte[] Initializer)[] globals
    )
    {
        return VectorSection(
            6,
            globals.Select(x =>
                (byte[])[x.ValueType, x.IsMutable ? (byte)1 : (byte)0, .. x.Initializer]
            )
        );
    }

    public static byte[] Start(uint functionIndex)
    {
        return Section(8, Unsigned(functionIndex));
    }

    public static byte[] Limits(uint minimum, uint? maximum = null)
    {
        return maximum is { } max
            ? [0x01, .. Unsigned(minimum), .. Unsigned(max)]
            : [0x00, .. Unsigned(minimum)];
    }

    public static byte[] Exports(params (string Name, byte Kind, uint Index)[] exports)
    {
        return VectorSection(
            7,
            exports.Select(x => (byte[])[.. Name(x.Name), x.Kind, .. Unsigned(x.Index)])
        );
    }

    public static byte[] Code(
        params ((uint Count, byte Kind)[] Locals, byte[] Instructions)[] functions
    )
    {
        return VectorSection(
            10,
            functions.Select(x =>
            {
                List<byte> body = [.. Unsigned((uint)x.Locals.Length)];
                foreach (var local in x.Locals)
                {
                    body.AddRange(Unsigned(local.Count));
                    body.Add(local.Kind);
                }

                // 命令終端も入力に含め、欠落や破損を補正しない。
                body.AddRange(x.Instructions);
                return (byte[])[.. Unsigned((uint)body.Count), .. body];
            })
        );
    }

    public static byte[] Section(byte id, byte[] payload, uint? declaredLength = null)
    {
        return [id, .. Unsigned(declaredLength ?? (uint)payload.Length), .. payload];
    }

    public static byte[] Unsigned(uint value)
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

    private static byte[] Name(string value)
    {
        var bytes = utf8_.GetBytes(value);
        return [.. Unsigned((uint)bytes.Length), .. bytes];
    }

    private static byte[] VectorSection(byte id, IEnumerable<byte[]> entries)
    {
        var values = entries.ToArray();
        return Section(id, [.. Unsigned((uint)values.Length), .. values.SelectMany(x => x)]);
    }
}
