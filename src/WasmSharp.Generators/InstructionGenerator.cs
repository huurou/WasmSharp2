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
    /// <summary>
    /// prefixと命令番号が重複する命令宣言の診断
    /// </summary>
    private static readonly DiagnosticDescriptor duplicateOpcode_ = new(
        "WSIG001",
        "命令番号の重複",
        "命令 '{0}' のprefixと命令番号が重複しています。",
        "InstructionGeneration",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>
    /// 命令の名前または実行に必要な宣言情報が不足する場合の診断
    /// </summary>
    private static readonly DiagnosticDescriptor incompleteDeclaration_ = new(
        "WSIG002",
        "命令宣言の不足",
        "命令 '{0}' の名前・即値・スタック効果・検証規則・handlerを確認してください。",
        "InstructionGeneration",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>
    /// 宣言で指定したhandlerがInterpreterに存在しない場合の診断
    /// </summary>
    private static readonly DiagnosticDescriptor missingHandler_ = new(
        "WSIG003",
        "命令handlerの不在",
        "命令 '{0}' のhandlerがInterpreterに存在しません。",
        "InstructionGeneration",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>
    /// 指定したhandlerが生成する呼び出しの契約を満たさない場合の診断
    /// </summary>
    private static readonly DiagnosticDescriptor invalidHandler_ = new(
        "WSIG004",
        "命令handlerの契約不一致",
        "命令 '{0}' のhandlerは static ExecutionResult Handler(InterpreterContext context, in Instruction instruction) にしてください。",
        "InstructionGeneration",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>
    /// 命令属性の収集と診断を登録し、宣言が有効な場合に命令情報と実行分岐を生成する
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

    /// <summary>
    /// 命令属性を生成用の宣言へ変換し、必要情報とhandler契約の診断を付ける
    /// </summary>
    /// <remarks>3引数の属性は未対応命令として保持し、7引数の属性は実行に必要な情報を検査する</remarks>
    /// <param name="attribute">ソース上のInstructionAttributeの属性情報</param>
    /// <param name="compilation">Interpreterとhandlerの宣言を照合するコンパイル情報</param>
    /// <returns>生成用の命令宣言 不完全な宣言やhandler契約違反はErrorに診断を保持する</returns>
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

    /// <summary>
    /// メソッドが生成する命令handlerの呼び出し契約を満たすか判定する
    /// </summary>
    /// <param name="method">指定されたhandler名に一致する候補メソッド</param>
    /// <returns>非ジェネリックのstaticメソッドで、値で返すExecutionResultとInterpreterContext・in Instructionの引数を持つ場合はtrue</returns>
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
                == "WasmSharp.Execution.InterpreterContext"
            && method.Parameters[1].RefKind == RefKind.In
            && method.Parameters[1].Type.ToDisplayString() == "WasmSharp.Execution.Instruction";
    }

    /// <summary>
    /// 命令属性の列挙定数に対応する宣言名を取得する
    /// </summary>
    /// <param name="value">即値、スタック効果または検証規則を表す列挙定数</param>
    /// <returns>定数値に一致する列挙子名 一致する宣言がない場合はUnsupported</returns>
    private static string GetEnumName(TypedConstant value)
    {
        return value
                .Type!.GetMembers()
                .OfType<IFieldSymbol>()
                .SingleOrDefault(x => x.HasConstantValue && Equals(x.ConstantValue, value.Value))
                ?.Name
            ?? "Unsupported";
    }

    /// <summary>
    /// 命令情報の検索表と、対応済み命令の実行opcodeを宣言するソースを生成する
    /// </summary>
    /// <param name="instructions">診断がなく、prefixと命令番号が重複しない命令宣言の一覧</param>
    /// <returns>全宣言の記述子と、handlerを持つ宣言の実行opcodeを含むC#ソース</returns>
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
            /// <param name="ExecutionOpcode">対応済み命令の実行opcode 未対応の場合はnull</param>
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
                /// <summary>
                /// Core 2.0の命令識別子に対応する記述子の検索表
                /// </summary>
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
                /// <param name="opcode">バイナリ上の命令識別子</param>
                /// <param name="descriptor">見つかった場合の命令情報 見つからない場合は既定値</param>
                /// <returns>命令宣言が見つかった場合はtrue 実行handlerの有無とは独立する</returns>
                internal static bool TryGet(OpcodeKey opcode, out InstructionDescriptor descriptor) =>
                    descriptors_.TryGetValue(opcode, out descriptor);
            }
            """
        );
        return source.ToString();
    }

    /// <summary>
    /// 実行opcodeと宣言済みhandlerを結び付けるInterpreterの実行ループを生成する
    /// </summary>
    /// <param name="instructions">handler契約の検査を通過した命令宣言を含む一覧</param>
    /// <returns>handlerを持つ宣言の実行分岐と、失敗結果を保持する実行ループのC#ソース</returns>
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
                /// <param name="context">今回の入口フレームを積んだ共有実行コンテキスト</param>
                /// <param name="entryFrameCount">今回の入口フレームを積む前のフレーム数</param>
                /// <returns>入口フレームが正常終了した場合は値を持たない成功結果 handlerが失敗した場合はその結果</returns>
                /// <exception cref="global::System.InvalidOperationException">命令に対応する実行分岐がない場合</exception>
                private static ExecutionResult RunLoop(InterpreterContext context, int entryFrameCount)
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

    /// <summary>
    /// 命令情報と実行分岐の生成に必要な属性値および宣言の診断
    /// </summary>
    /// <param name="Prefix">通常命令では0、拡張命令ではprefix</param>
    /// <param name="Code">prefix内の命令番号</param>
    /// <param name="Name">仕様上の命令名</param>
    /// <param name="Immediate">即値の符号化を表す列挙子名</param>
    /// <param name="StackEffect">スタック効果を表す列挙子名</param>
    /// <param name="Validation">検証規則を表す列挙子名</param>
    /// <param name="Handler">対応するInterpreterの静的handler名 未対応の宣言ではnull</param>
    /// <param name="Location">属性宣言のソース上の位置</param>
    /// <param name="Error">宣言の不備を報告する診断 宣言が有効な場合はnull</param>
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
        /// <summary>
        /// prefixと命令番号から得られる、生成する実行opcodeの列挙子名
        /// </summary>
        public string ExecutionOpcode => Prefix == 0 ? $"Op{Code:X2}" : $"Op{Prefix:X2}_{Code:X2}";
    }
}
