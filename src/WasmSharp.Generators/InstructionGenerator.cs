using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace WasmSharp.Generators;

/// <summary>
/// Wasm命令の宣言からソースを生成するインクリメンタルジェネレーター
/// </summary>
[Generator(LanguageNames.CSharp)]
internal sealed class InstructionGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor duplicateOpcode_ = new(
        "WSIG001",
        "命令番号の重複",
        "命令 '{0}' のprefixと命令番号が重複しています。",
        "InstructionGeneration",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    private static readonly DiagnosticDescriptor incompleteDeclaration_ = new(
        "WSIG002",
        "命令宣言の不足",
        "命令 '{0}' の名前・即値・スタック効果・検証規則・handlerを確認してください。",
        "InstructionGeneration",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    private static readonly DiagnosticDescriptor missingHandler_ = new(
        "WSIG003",
        "命令handlerの不在",
        "命令 '{0}' のhandlerがInterpreterに存在しません。",
        "InstructionGeneration",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    private static readonly DiagnosticDescriptor invalidHandler_ = new(
        "WSIG004",
        "命令handlerの契約不一致",
        "命令 '{0}' のhandlerは static ExecutionResult Handler(WasmExecutionContext context, in Instruction instruction) にしてください。",
        "InstructionGeneration",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>
    /// ソース生成の処理を初期化する
    /// </summary>
    /// <param name="context">ソース生成の初期化コンテキスト</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var declarations = context.SyntaxProvider.ForAttributeWithMetadataName(
            "WasmSharp.Instructions.InstructionAttribute",
            static (node, _) => node is Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax,
            static (context, _) =>
                context
                    .Attributes.Select(x => ParseDeclaration(x, context.SemanticModel.Compilation))
                    .ToImmutableArray()
        );
        context.RegisterSourceOutput(
            declarations.Collect(),
            static (context, declarations) =>
            {
                var instructions = declarations
                    .SelectMany(x => x)
                    .OrderBy(x => x.Prefix)
                    .ThenBy(x => x.Code)
                    .ToImmutableArray();
                if (instructions.IsEmpty)
                {
                    return;
                }
                var keys = new HashSet<(byte, uint)>();
                var hasErrors = false;
                foreach (var instruction in instructions)
                {
                    if (instruction.Error != null)
                    {
                        context.ReportDiagnostic(
                            Diagnostic.Create(
                                instruction.Error,
                                instruction.Location,
                                instruction.Name
                            )
                        );
                        hasErrors = true;
                        continue;
                    }
                    if (!keys.Add((instruction.Prefix, instruction.Code)))
                    {
                        context.ReportDiagnostic(
                            Diagnostic.Create(
                                duplicateOpcode_,
                                instruction.Location,
                                instruction.Name
                            )
                        );
                        hasErrors = true;
                    }
                }
                if (hasErrors)
                {
                    return;
                }
                context.AddSource("InstructionSet.g.cs", GenerateInstructionSet(instructions));
                if (instructions.Any(x => x.Handler != null))
                {
                    context.AddSource("Interpreter.g.cs", GenerateInterpreter(instructions));
                }
            }
        );
    }

    private static InstructionDeclaration ParseDeclaration(
        AttributeData attribute,
        Compilation compilation
    )
    {
        var arguments = attribute.ConstructorArguments;
        var location = attribute.ApplicationSyntaxReference!.GetSyntax().GetLocation();
        if (
            arguments.Length is not (3 or 7)
            || arguments.Any(x => x.Kind == TypedConstantKind.Error)
        )
        {
            return new InstructionDeclaration(
                0,
                0,
                "",
                "Unsupported",
                "Unsupported",
                "Unsupported",
                null,
                location,
                incompleteDeclaration_
            );
        }
        var declaration = new InstructionDeclaration(
            (byte)arguments[0].Value!,
            (uint)arguments[1].Value!,
            (string?)arguments[2].Value ?? "",
            arguments.Length == 3 ? "Unsupported" : GetEnumName(arguments[3]),
            arguments.Length == 3 ? "Unsupported" : GetEnumName(arguments[4]),
            arguments.Length == 3 ? "Unsupported" : GetEnumName(arguments[5]),
            arguments.Length == 3 ? null : (string?)arguments[6].Value,
            location,
            null
        );
        if (
            string.IsNullOrWhiteSpace(declaration.Name)
            || (
                arguments.Length == 7
                && (
                    declaration.Immediate == "Unsupported"
                    || declaration.StackEffect == "Unsupported"
                    || declaration.Validation == "Unsupported"
                    || string.IsNullOrWhiteSpace(declaration.Handler)
                )
            )
        )
        {
            return declaration with { Error = incompleteDeclaration_ };
        }
        if (declaration.Handler == null)
        {
            return declaration;
        }

        var interpreter = compilation.GetTypeByMetadataName("WasmSharp.Execution.Interpreter");
        var methods = interpreter
            ?.GetMembers(declaration.Handler)
            .OfType<IMethodSymbol>()
            .ToArray();
        if (methods == null || methods.Length == 0)
        {
            return declaration with { Error = missingHandler_ };
        }
        return methods.Any(IsHandler) ? declaration : declaration with { Error = invalidHandler_ };
    }

    private static bool IsHandler(IMethodSymbol method)
    {
        return method.IsStatic
            && !method.IsGenericMethod
            && !method.ReturnsByRef
            && !method.ReturnsByRefReadonly
            && method.ReturnType.ToDisplayString() == "WasmSharp.Execution.ExecutionResult"
            && method.Parameters.Length == 2
            && method.Parameters[0].RefKind == RefKind.None
            && method.Parameters[0].Type.ToDisplayString()
                == "WasmSharp.Execution.WasmExecutionContext"
            && method.Parameters[1].RefKind == RefKind.In
            && method.Parameters[1].Type.ToDisplayString() == "WasmSharp.Execution.Instruction";
    }

    private static string GetEnumName(TypedConstant value)
    {
        return value
                .Type!.GetMembers()
                .OfType<IFieldSymbol>()
                .SingleOrDefault(x => x.HasConstantValue && Equals(x.ConstantValue, value.Value))
                ?.Name
            ?? "Unsupported";
    }

    private static string GenerateInstructionSet(
        ImmutableArray<InstructionDeclaration> instructions
    )
    {
        var source = new StringBuilder(
            """
            // <auto-generated />
            #nullable enable
            namespace WasmSharp.Instructions;

            /// <summary>
            /// 対応済み命令の実行opcode
            /// </summary>
            internal enum ExecutionOpcode
            {

            """
        );
        foreach (var instruction in instructions.Where(x => x.Handler != null))
        {
            var name = SymbolDisplay
                .FormatLiteral(instruction.Name, false)
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
            source.AppendLine("    /// <summary>");
            source.AppendLine($"    /// {name}の実行opcode");
            source.AppendLine("    /// </summary>");
            source.AppendLine($"    {instruction.ExecutionOpcode},");
        }
        source.AppendLine(
            """
            }

            /// <summary>
            /// 命令の識別子、検証に必要な情報、および対応する実行opcode
            /// </summary>
            /// <param name="Opcode">バイナリ上の命令識別子</param>
            /// <param name="Name">仕様上の命令名</param>
            /// <param name="Immediate">即値の符号化</param>
            /// <param name="StackEffect">スタック効果</param>
            /// <param name="Validation">検証規則</param>
            /// <param name="ExecutionOpcode">対応済み命令の実行opcode。未対応の場合はnull</param>
            internal readonly record struct InstructionDescriptor(
                OpcodeKey Opcode,
                string Name,
                ImmediateKind Immediate,
                StackEffectKind StackEffect,
                ValidationRule Validation,
                ExecutionOpcode? ExecutionOpcode);

            /// <summary>
            /// 命令宣言から生成された命令情報の参照先
            /// </summary>
            internal static partial class InstructionSet
            {
                private static readonly global::System.Collections.Generic.Dictionary<OpcodeKey, InstructionDescriptor> descriptors_ = new()
                {
            """
        );
        foreach (var instruction in instructions)
        {
            var key = $"new OpcodeKey({instruction.Prefix}, {instruction.Code}u)";
            var executionOpcode =
                instruction.Handler == null
                    ? "null"
                    : $"ExecutionOpcode.{instruction.ExecutionOpcode}";
            source.AppendLine(
                $"        [{key}] = new({key}, {SymbolDisplay.FormatLiteral(instruction.Name, true)}, ImmediateKind.{instruction.Immediate}, StackEffectKind.{instruction.StackEffect}, ValidationRule.{instruction.Validation}, {executionOpcode}),"
            );
        }
        source.AppendLine(
            """
                };

                /// <summary>
                /// 命令識別子に対応する命令情報を取得する
                /// </summary>
                internal static bool TryGet(OpcodeKey opcode, out InstructionDescriptor descriptor) =>
                    descriptors_.TryGetValue(opcode, out descriptor);
            }
            """
        );
        return source.ToString();
    }

    private static string GenerateInterpreter(ImmutableArray<InstructionDeclaration> instructions)
    {
        var source = new StringBuilder(
            """
            // <auto-generated />
            #nullable enable
            namespace WasmSharp.Execution;

            /// <summary>
            /// 線形命令を実行するインタープリタ
            /// </summary>
            internal static partial class Interpreter
            {
                /// <summary>
                /// 今回の入口フレームが終了するまで命令を実行し、失敗結果はそのまま返す
                /// </summary>
                private static ExecutionResult RunLoop(WasmExecutionContext context, int entryFrameCount)
                {
                    while (context.FrameCount > entryFrameCount)
                    {
                        var instruction = context.ReadNextInstruction();
                        ExecutionResult result;
                        switch (instruction.Opcode)
                        {

            """
        );
        foreach (var instruction in instructions.Where(x => x.Handler != null))
        {
            source.AppendLine(
                $"                case global::WasmSharp.Instructions.ExecutionOpcode.{instruction.ExecutionOpcode}:"
            );
            source.AppendLine(
                $"                    result = @{instruction.Handler}(context, in instruction);"
            );
            source.AppendLine("                    break;");
        }
        source.AppendLine(
            """
                            default:
                                throw new global::System.InvalidOperationException("実行opcodeが不正です。");
                        }
                        if (result.Status != ExecutionStatus.Success)
                        {
                            return result;
                        }
                    }
                    return default;
                }
            }
            """
        );
        return source.ToString();
    }

    private sealed record InstructionDeclaration(
        byte Prefix,
        uint Code,
        string Name,
        string Immediate,
        string StackEffect,
        string Validation,
        string? Handler,
        Location Location,
        DiagnosticDescriptor? Error
    )
    {
        public string ExecutionOpcode => Prefix == 0 ? $"Op{Code:X2}" : $"Op{Prefix:X2}_{Code:X2}";
    }
}
