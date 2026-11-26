using System.Collections.Immutable;
using WasmSharp.Exceptions;

namespace WasmSharp.Modules.Definitions;

/// <summary>
/// 定義型が宣言した上位型と両立しない理由
/// </summary>
internal enum SupertypeError
{
    None,
    Multiple,
    Forward,
    Final,
    Structure,
}

/// <summary>
/// 型セクションで上位型を指定した宣言 同値な定義型を共有しても診断には元の型番号と位置を使う
/// </summary>
internal readonly record struct SupertypeDeclaration(int TypeIndex, uint FirstIndex, long ByteOffset)
{
    internal void ThrowIfInvalid(ImmutableArray<WasmDefinedType> types)
    {
        switch (types[TypeIndex].SupertypeError)
        {
            case SupertypeError.Multiple:
                throw Failure("multiple super types", "指定できる上位型は1個までです。");
            case SupertypeError.Forward:
                throw Failure(
                    $"forward use of type {FirstIndex} in sub type definition",
                    "上位型は先行する型番号で指定します。"
                );
            case SupertypeError.Final:
                throw Failure(
                    $"sub type {TypeIndex} has final super type {FirstIndex}",
                    types[(int)FirstIndex] is WasmFunctionType
                        ? "finalな関数型を継承できません。"
                        : "finalな構造体型を継承できません。"
                );
            case SupertypeError.Structure:
                throw Failure(
                    $"sub type {TypeIndex} does not match super type {FirstIndex}",
                    types[TypeIndex] is WasmFunctionType
                        ? "上位型は関数型で、引数の反変または結果の共変を満たす必要があります。"
                        : "上位型は構造体型で、先頭フィールドの可変性と格納型を引き継ぐ必要があります。"
                );
        }
    }

    private WasmValidateException Failure(string prefix, string message)
    {
        return ValidationDiagnostics.ValidationFailure(
            prefix,
            message,
            new WasmFailureLocation(WasmProcessingStage.Validate, ByteOffset, null, 1)
        );
    }
}
