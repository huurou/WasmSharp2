using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace WasmSharp.Benchmarks.Cases;

/// <summary>
/// 固定入力のhashを確認し、明示型の引数とlittle-endianの配列を計時前に復元する。
/// </summary>
internal static class InputFactory
{
    internal static PreparedInput Create(BenchmarkCase definition)
    {
        var input = definition.Input;
        var hash = Convert.ToHexStringLower(
            SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(input, CaseJson.Options))
        );
        if (hash != definition.InputHash)
        {
            throw new InvalidDataException(
                $"ケース'{definition.CaseId}'の入力hashが一致しません。"
            );
        }
        return new PreparedInput(
            input.FunctionExport,
            input.Arguments.Select(x => CreateValue(x, definition.CaseId)).ToImmutableArray(),
            input.Arrays.Select(x => CreateArray(x, definition.CaseId)).ToImmutableArray()
        );
    }

    private static WasmValue CreateValue(NumericValue value, string caseId)
    {
        var width = value.Type switch
        {
            NumericType.I32 or NumericType.F32 => 8,
            NumericType.I64 or NumericType.F64 => 16,
            _ => 0,
        };
        if (
            width == 0
            || value.Bits.Length != width
            || value.Bits.Any(x => x is not (>= '0' and <= '9' or >= 'a' and <= 'f'))
        )
        {
            throw new InvalidDataException(
                $"ケース'{caseId}'の{value.Type}のbit列'{value.Bits}'は固定桁数の小文字16進表記ではありません。"
            );
        }
        var bits = ulong.Parse(
            value.Bits,
            NumberStyles.AllowHexSpecifier,
            CultureInfo.InvariantCulture
        );
        return value.Type switch
        {
            NumericType.I32 => WasmValue.FromI32(unchecked((int)bits)),
            NumericType.I64 => WasmValue.FromI64(unchecked((long)bits)),
            NumericType.F32 => WasmValue.FromF32Bits((uint)bits),
            NumericType.F64 => WasmValue.FromF64Bits(bits),
            _ => throw new InvalidDataException(
                $"ケース'{caseId}'の数値型'{value.Type}'は未対応です。"
            ),
        };
    }

    private static PreparedArrayInput CreateArray(ArrayInput input, string caseId)
    {
        var valid = input.Pattern switch
        {
            ArrayInputPattern.RepeatedByte => input.ElementType == ArrayElementType.U8
                && input.FillByte.HasValue
                && input.Seed is null,
            ArrayInputPattern.IndexModulo256 => input.ElementType
                is ArrayElementType.U8
                    or ArrayElementType.U32
                && input.Seed is null
                && input.FillByte is null,
            ArrayInputPattern.SeededU32 => input.ElementType == ArrayElementType.U32
                && input.Seed.HasValue
                && input.FillByte is null,
            _ => false,
        };
        if (!valid)
        {
            throw new InvalidDataException(
                $"ケース'{caseId}'の配列生成方式'{input.Pattern}'と要素型・seed・fill_byteが一致しません。"
            );
        }

        var elementSize = input.ElementType == ArrayElementType.U8 ? 1 : sizeof(uint);
        var byteLength = (ulong)input.Length * (uint)elementSize;
        if (byteLength > int.MaxValue)
        {
            throw new InvalidDataException(
                $"ケース'{caseId}'の配列長{input.Length}が入力生成の上限を超えています。"
            );
        }
        var bytes = new byte[(int)byteLength];
        if (input.Pattern == ArrayInputPattern.RepeatedByte)
        {
            bytes.AsSpan().Fill(input.FillByte!.Value);
        }
        else
        {
            var state = input.Seed.GetValueOrDefault();
            for (var index = 0; index < (int)input.Length; index++)
            {
                uint value;
                if (input.Pattern == ArrayInputPattern.SeededU32)
                {
                    // xorshift32は更新後の状態を格納する 左右行列のseedはケース定義で固定する。
                    state ^= state << 13;
                    state ^= state >> 17;
                    state ^= state << 5;
                    value = state;
                }
                else
                {
                    value = (uint)(index % 256);
                }
                if (input.ElementType == ArrayElementType.U8)
                {
                    bytes[index] = (byte)value;
                }
                else
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(
                        bytes.AsSpan(index * sizeof(uint)),
                        value
                    );
                }
            }
        }
        return new PreparedArrayInput(input.MemoryExport, input.Offset, bytes.ToImmutableArray());
    }
}

/// <summary>
/// 公開Invokeへ渡す型付き引数とmemoryへ配置する固定配列
/// </summary>
internal sealed record PreparedInput(
    string FunctionExport,
    ImmutableArray<WasmValue> Arguments,
    ImmutableArray<PreparedArrayInput> Arrays
);

/// <summary>
/// 配置先とバイト列を保持する生成済み入力配列
/// </summary>
internal sealed record PreparedArrayInput(
    string MemoryExport,
    uint Offset,
    ImmutableArray<byte> Bytes
);
