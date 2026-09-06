using System.Text;

namespace WasmSharp.Tests.Fixtures;

public static class ConstantModuleBinary
{
    public static byte[] Create(byte resultType, params byte[] instructions)
    {
        return Create([(resultType, instructions)], [("run", 0)]);
    }

    public static byte[] Create(
        (byte ResultType, byte[] Instructions)[] functions,
        (string Name, uint FunctionIndex)[] exports
    )
    {
        List<byte> types = [];
        List<byte> functionIndices = [];
        List<byte> code = [];
        AppendUnsigned(types, (uint)functions.Length);
        AppendUnsigned(functionIndices, (uint)functions.Length);
        AppendUnsigned(code, (uint)functions.Length);

        for (var index = 0; index < functions.Length; index++)
        {
            var function = functions[index];
            // 各関数に、引数なし・結果1個の型を1つずつ割り当てる。
            types.AddRange([0x60, 0x00, 0x01, function.ResultType]);
            AppendUnsigned(functionIndices, (uint)index);
            AppendUnsigned(code, (uint)function.Instructions.Length + 1);
            code.Add(0x00); // locals宣言は0個。
            // endも入力に含める。負例の不正命令や終端欠落を補正しない。
            code.AddRange(function.Instructions);
        }

        List<byte> exportEntries = [];
        AppendUnsigned(exportEntries, (uint)exports.Length);
        foreach (var export in exports)
        {
            var name = Encoding.UTF8.GetBytes(export.Name);
            AppendUnsigned(exportEntries, (uint)name.Length);
            exportEntries.AddRange(name);
            exportEntries.Add(0x00); // exportの種類は関数。
            AppendUnsigned(exportEntries, export.FunctionIndex);
        }

        List<byte> module = [0x00, 0x61, 0x73, 0x6D, 0x01, 0x00, 0x00, 0x00];
        AppendSection(module, 0x01, types);
        AppendSection(module, 0x03, functionIndices);
        AppendSection(module, 0x07, exportEntries);
        AppendSection(module, 0x0A, code);
        return [.. module];
    }

    private static void AppendSection(List<byte> module, byte id, List<byte> payload)
    {
        module.Add(id);
        AppendUnsigned(module, (uint)payload.Count);
        module.AddRange(payload);
    }

    private static void AppendUnsigned(List<byte> bytes, uint value)
    {
        // section長・件数・添字は符号なしLEB128で記録する。
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
    }
}
