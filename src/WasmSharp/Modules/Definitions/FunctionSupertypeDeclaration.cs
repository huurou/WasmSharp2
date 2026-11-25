using WasmSharp.Exceptions;

namespace WasmSharp.Modules.Definitions;

/// <summary>
/// 関数型が宣言した上位型と両立しない理由
/// </summary>
internal enum FunctionSupertypeError
{
    None,
    Multiple,
    Forward,
    Final,
    Signature,
}

/// <summary>
/// 型セクションで上位型を指定した宣言 同値な関数型を共有しても診断には元の型番号と位置を使う
/// </summary>
internal readonly record struct FunctionSupertypeDeclaration(int TypeIndex, uint FirstIndex, long ByteOffset)
{
    internal void ThrowIfInvalid(FunctionSupertypeError error)
    {
        switch (error)
        {
            case FunctionSupertypeError.Multiple:
                throw Failure("multiple super types", "指定できる上位型は1個までです。");
            case FunctionSupertypeError.Forward:
                throw Failure(
                    $"forward use of type {FirstIndex} in sub type definition",
                    "上位型は先行する型番号で指定します。"
                );
            case FunctionSupertypeError.Final:
                throw Failure($"sub type {TypeIndex} has final super type {FirstIndex}", "finalな関数型を継承できません。");
            case FunctionSupertypeError.Signature:
                throw Failure(
                    $"sub type {TypeIndex} does not match super type {FirstIndex}",
                    "引数の反変または結果の共変を満たしていません。"
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
