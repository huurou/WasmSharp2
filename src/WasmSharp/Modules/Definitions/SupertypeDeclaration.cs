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
                        : "上位型には構造体型を指定し、そのすべてのフィールドを子の構造体の先頭に同じ順序で宣言します。"
                            + "対応するフィールドの可変性と詰め込み型は一致させます。"
                            + "不変フィールドには親と同じ値型かその部分型を、可変フィールドには親と同値の値型を使います。"
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
