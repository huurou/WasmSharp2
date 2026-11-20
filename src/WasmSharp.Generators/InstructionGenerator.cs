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
    private const string IntegerArithmeticName = "IntegerArithmetic";

    /// <summary>
    /// prefixと命令番号が重複する命令宣言の診断
    /// </summary>
    private readonly DiagnosticDescriptor _duplicateOpcode = new DiagnosticDescriptor(
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
    private readonly DiagnosticDescriptor _incompleteDeclaration = new DiagnosticDescriptor(
        "WSIG002",
        "命令宣言の不足",
        "命令 '{0}' の名前・仕様の版・即値・スタック効果・検証規則・handlerを確認してください。",
        "InstructionGeneration",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>
    /// 宣言で指定したhandlerが定義先に存在しない場合の診断
    /// </summary>
    private readonly DiagnosticDescriptor _missingHandler = new DiagnosticDescriptor(
        "WSIG003",
        "命令handlerの不在",
        "命令 '{0}' のhandlerが{1}に存在しません。",
        "InstructionGeneration",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>
    /// 指定したhandlerが生成する呼び出しの契約を満たさない場合の診断
    /// </summary>
    private readonly DiagnosticDescriptor _invalidHandler = new DiagnosticDescriptor(
        "WSIG004",
        "命令handlerの契約不一致",
        "命令 '{0}' のhandlerは {1} にしてください。",
        "InstructionGeneration",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>
    /// ロード命令・ストア命令のアクセス情報が規則や値型と整合しない場合の診断
    /// </summary>
    private readonly DiagnosticDescriptor _invalidMemoryAccess = new DiagnosticDescriptor(
        "WSIG005",
        "メモリアクセス情報の不整合",
        "命令 '{0}' のメモリアクセス情報・即値・スタック効果・検証規則を確認してください。",
        "InstructionGeneration",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>
    /// 生成するhandlerと手書きのメンバーが同じInterpreterに共存できない場合の診断
    /// </summary>
    private readonly DiagnosticDescriptor _conflictingHandler = new DiagnosticDescriptor(
        "WSIG006",
        "生成する命令handlerの名前の衝突",
        "命令 '{0}' から生成するInterpreter.{1}が既存のメンバーと競合します。",
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
            (context, _) =>
                context.Attributes.Select(x => ParseDeclaration(x, context.SemanticModel.Compilation)).ToImmutableArray()
        );
        context.RegisterSourceOutput(declarations.Collect(), (context, declarations) =>
        {
            var parsedDeclarations = declarations.SelectMany(x => x).ToImmutableArray();
            if (parsedDeclarations.IsEmpty)
            {
                return;
            }
            var keys = new HashSet<(byte, uint)>();
            var hasErrors = false;
            foreach (var parsedDeclaration in parsedDeclarations)
            {
                if (parsedDeclaration.Diagnostic != null)
                {
                    context.ReportDiagnostic(parsedDeclaration.Diagnostic);
                    hasErrors = true;
                    continue;
                }
                var instruction = parsedDeclaration.Declaration!;
                if (!keys.Add((instruction.Prefix, instruction.Code)))
                {
                    context.ReportDiagnostic(Diagnostic.Create(_duplicateOpcode, instruction.Location, instruction.Name));
                    hasErrors = true;
                }
            }
            if (hasErrors)
            {
                return;
            }
            var instructions = parsedDeclarations.Select(x => x.Declaration!).ToImmutableArray();
            context.AddSource("InstructionSet.g.cs", GenerateInstructionSet(instructions));
            if (instructions.Any(x => x.Handler != null))
            {
                context.AddSource("Interpreter.g.cs", GenerateInterpreter(instructions));
            }
        });
    }

    /// <summary>
    /// 命令属性の必要情報とhandler契約を検証し、有効な宣言または診断を返す
    /// </summary>
    /// <remarks>3引数は未対応、9引数は入力型列、10引数は引数の型で同型数値用とメモリ用を区別する</remarks>
    /// <param name="attribute">ソース上のInstructionAttributeの属性情報</param>
    /// <param name="compilation">Interpreterとhandlerの宣言を照合するコンパイル情報</param>
    /// <returns>有効な場合はDeclaration、宣言に不備がある場合はDiagnosticのみが設定された結果</returns>
    private (InstructionDeclaration? Declaration, Diagnostic? Diagnostic) ParseDeclaration(
        AttributeData attribute,
        Compilation compilation
    )
    {
        var arguments = attribute.ConstructorArguments;
        var location = attribute.ApplicationSyntaxReference!.GetSyntax().GetLocation();
        if (
            arguments.Length is not (3 or 7 or 9 or 10)
            || arguments.Any(x => x.Kind == TypedConstantKind.Error)
            || attribute.NamedArguments.Any(x => x.Value.Kind == TypedConstantKind.Error)
            || attribute.NamedArguments.Count(x => x.Key == "LaneCount") > 1
        )
        {
            return (null, Diagnostic.Create(_incompleteDeclaration, location, ""));
        }
        var declaration = new InstructionDeclaration(
            (byte)arguments[0].Value!,
            (uint)arguments[1].Value!,
            (string?)arguments[2].Value ?? "",
            attribute.NamedArguments.Where(x => x.Key == "IntroducedIn").Select(x => GetEnumName(x.Value)).FirstOrDefault()
                ?? "Core2",
            arguments.Length == 3 ? "Unsupported" : GetEnumName(arguments[3]),
            arguments.Length == 3 ? "Unsupported" : GetEnumName(arguments[4]),
            arguments.Length == 3 ? "Unsupported" : GetEnumName(arguments[5]),
            arguments.Length == 3 ? null : (string?)arguments[6].Value,
            arguments.Length == 9
                    ? new NumericSignatureDeclaration(
                        arguments[7].IsNull ? [] : [.. arguments[7].Values.Select(GetEnumName)],
                        GetEnumName(arguments[8])
                    )
                : arguments.Length == 10 && arguments[7].Type!.SpecialType == SpecialType.System_Byte
                    ? new NumericSignatureDeclaration(
                        [.. Enumerable.Repeat(GetEnumName(arguments[8]), (byte)arguments[7].Value!)],
                        GetEnumName(arguments[9])
                    )
                : null,
            arguments.Length == 10 && arguments[7].Type!.ToDisplayString() == "WasmSharp.WasmValueKind"
                ? new MemoryAccessSignatureDeclaration(
                    GetEnumName(arguments[7]),
                    (byte)arguments[8].Value!,
                    (bool)arguments[9].Value!
                )
                : null,
            attribute.NamedArguments.Where(x => x.Key == "LaneCount").Select(x => (byte)x.Value.Value!).SingleOrDefault(),
            attribute.NamedArguments.Any(x => x.Key == "IsConstantBinaryOperation" && x.Value.Value is true),
            location
        );
        if (
            string.IsNullOrWhiteSpace(declaration.Name)
            || declaration.IntroducedIn is not ("Core2" or "Core3")
            || (
                arguments.Length != 3
                && (
                    declaration.Immediate == "Unsupported"
                    || declaration.StackEffect == "Unsupported"
                    || declaration.Validation == "Unsupported"
                    || string.IsNullOrWhiteSpace(declaration.Handler)
                )
            )
        )
        {
            return (null, Diagnostic.Create(_incompleteDeclaration, location, declaration.Name));
        }
        if (!HasValidMemoryAccess(declaration))
        {
            return (null, Diagnostic.Create(_invalidMemoryAccess, location, declaration.Name));
        }
        var numericStackEffect = declaration.StackEffect == "Numeric";
        var numericValidation = declaration.Validation == "Numeric";
        var signature = declaration.NumericSignature;
        if (
            numericStackEffect != numericValidation
            || numericStackEffect != (signature != null)
            || (declaration.IsConstantBinaryOperation && signature is not { InputTypes.Length: 2 })
            || (
                signature != null
                && (
                    signature.InputTypes.Length is not (1 or 2 or 3)
                    || signature.InputTypes.Any(x => !IsNumericType(x))
                    || !IsNumericType(signature.ResultType)
                )
            )
            || !HasValidSimdImmediate(declaration)
        )
        {
            return (null, Diagnostic.Create(_incompleteDeclaration, location, declaration.Name));
        }
        var diagnostic = ValidateHandler(declaration, compilation);
        return diagnostic == null ? (declaration, null) : (null, diagnostic);
    }

    /// <summary>
    /// 命令宣言が指定するhandlerの定義と呼び出し契約を照合する
    /// </summary>
    private Diagnostic? ValidateHandler(InstructionDeclaration declaration, Compilation compilation)
    {
        if (declaration.Handler is not { } handler)
        {
            return null;
        }

        var ownerName = declaration.IsConstantBinaryOperation ? IntegerArithmeticName : "Interpreter";
        var owner = compilation.GetTypeByMetadataName($"WasmSharp.Execution.{ownerName}");
        var methods = owner?.GetMembers(handler).OfType<IMethodSymbol>().ToArray();
        if (methods == null || methods.Length == 0)
        {
            return Diagnostic.Create(_missingHandler, declaration.Location, declaration.Name, ownerName);
        }
        if (!methods.Any(x => IsHandler(x, declaration.IsConstantBinaryOperation, compilation)))
        {
            var contract = declaration.IsConstantBinaryOperation
                ? "生成コードから呼び出せる static WasmValue Handler(WasmValue left, WasmValue right)"
                : "static ExecutionResult Handler(InterpreterContext context, in Instruction instruction)";
            return Diagnostic.Create(_invalidHandler, declaration.Location, declaration.Name, contract);
        }
        if (declaration.IsConstantBinaryOperation)
        {
            var interpreter = compilation.GetTypeByMetadataName("WasmSharp.Execution.Interpreter");
            if (interpreter?.GetMembers(handler).Any(x => ConflictsWithGeneratedHandler(x, compilation)) == true)
            {
                return Diagnostic.Create(_conflictingHandler, declaration.Location, declaration.Name, handler);
            }
        }
        return null;
    }

    /// <summary>
    /// メソッドが生成する命令handlerの呼び出し契約を満たすか判定する
    /// </summary>
    /// <param name="method">指定されたhandler名に一致する候補メソッド</param>
    /// <param name="isConstantBinaryOperation">定数式と共有する演算を検査する場合はtrue</param>
    /// <param name="compilation">生成コードから演算を呼び出せるか照合するコンパイル情報</param>
    /// <returns>宣言に対応する呼び出し契約を満たす場合はtrue</returns>
    private static bool IsHandler(IMethodSymbol method, bool isConstantBinaryOperation, Compilation compilation)
    {
        if (!method.IsStatic || method.IsGenericMethod || method.ReturnsByRef || method.ReturnsByRefReadonly)
        {
            return false;
        }
        if (isConstantBinaryOperation)
        {
            return method.CanBeReferencedByName
                && method.ReturnType.ToDisplayString() == "WasmSharp.WasmValue"
                && method.Parameters.Length == 2
                && method.Parameters.All(x =>
                    x.RefKind == RefKind.None && SymbolEqualityComparer.Default.Equals(x.Type, method.ReturnType)
                )
                && compilation.IsSymbolAccessibleWithin(method, compilation.Assembly);
        }
        return method.ReturnType.ToDisplayString() == "WasmSharp.Execution.ExecutionResult"
            && HasInterpreterParameters(method, compilation)
            && method.Parameters[0].NullableAnnotation != NullableAnnotation.Annotated
            && method.Parameters[1].RefKind == RefKind.In;
    }

    /// <summary>
    /// 手書きのメンバーと生成するhandlerが衝突するか、C#のオーバーロード規則に従って判定する
    /// </summary>
    private static bool ConflictsWithGeneratedHandler(ISymbol member, Compilation compilation)
    {
        // in、ref、outの違いだけではオーバーロードできない。
        return member is not IMethodSymbol method
            || (
                !method.IsGenericMethod
                && HasInterpreterParameters(method, compilation)
                && method.Parameters[1].RefKind != RefKind.None
            );
    }

    /// <summary>
    /// 第1引数が値渡しのInterpreterContextで、第2引数がInstructionかを確認する
    /// </summary>
    private static bool HasInterpreterParameters(IMethodSymbol method, Compilation compilation)
    {
        // C#のメソッドの同一性では参照型のnull許容注釈を区別しない。
        return method.Parameters.Length == 2
            && method.Parameters[0].RefKind == RefKind.None
            && SymbolEqualityComparer.Default.Equals(
                method.Parameters[0].Type,
                compilation.GetTypeByMetadataName("WasmSharp.Execution.InterpreterContext")
            )
            && SymbolEqualityComparer.Default.Equals(
                method.Parameters[1].Type,
                compilation.GetTypeByMetadataName("WasmSharp.Execution.Instruction")
            );
    }

    /// <summary>
    /// 命令属性の列挙定数に対応する宣言名を取得する
    /// </summary>
    /// <param name="value">即値、スタック効果、検証規則または値型を表す列挙定数</param>
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
    /// 数値signatureに使える値型の宣言名か判定する
    /// </summary>
    /// <param name="name">WasmValueKindの列挙子名</param>
    /// <returns>i32、i64、f32、f64、v128のいずれかの場合はtrue</returns>
    private static bool IsNumericType(string name)
    {
        return name is "I32" or "I64" or "F32" or "F64" or "V128";
    }

    /// <summary>
    /// SIMD即値の形状と型効果・lane上限の整合性を検査する
    /// </summary>
    /// <param name="declaration">属性から読み取った命令宣言</param>
    /// <returns>即値に必要な型効果とlane上限が揃う場合はtrue</returns>
    private static bool HasValidSimdImmediate(InstructionDeclaration declaration)
    {
        var signature = declaration.NumericSignature;
        if (declaration.Immediate == "V128" || declaration.StackEffect == "PushV128")
        {
            return declaration.Immediate == "V128"
                && declaration.StackEffect == "PushV128"
                && declaration.Validation == "Constant"
                && declaration.LaneCount == 0
                && signature == null;
        }
        if (declaration.Immediate == "MemoryArgumentLaneIndex")
        {
            return declaration.MemoryAccessSignature != null;
        }
        if (declaration.Immediate == "LaneIndex")
        {
            if (signature == null || declaration.LaneCount is not (2 or 4 or 8 or 16))
            {
                return false;
            }
            var inputs = signature.InputTypes;
            var scalar = inputs.Length == 1 ? signature.ResultType : inputs[1];
            var validScalar = scalar switch
            {
                "I32" => declaration.LaneCount is 4 or 8 or 16,
                "I64" or "F64" => declaration.LaneCount == 2,
                "F32" => declaration.LaneCount == 4,
                _ => false,
            };
            return validScalar
                && inputs[0] == "V128"
                && (inputs.Length == 1 || (inputs.Length == 2 && signature.ResultType == "V128"));
        }
        if (declaration.LaneCount != 0)
        {
            return false;
        }
        return declaration.Immediate != "Shuffle"
            || (signature is { InputTypes.Length: 2, ResultType: "V128" } && signature.InputTypes.All(x => x == "V128"));
    }

    /// <summary>
    /// ロード命令・ストア命令の即値・型効果とアクセス情報の整合性を検査する
    /// </summary>
    /// <param name="declaration">属性から読み取った命令宣言</param>
    /// <returns>アクセス情報の過不足がなく、値型と幅・符号拡張が整合する場合はtrue</returns>
    private static bool HasValidMemoryAccess(InstructionDeclaration declaration)
    {
        var isLoad = declaration.Validation is "MemoryLoad" or "MemoryLoadLane";
        var isStore = declaration.Validation is "MemoryStore" or "MemoryStoreLane";
        var isLane = declaration.Validation is "MemoryLoadLane" or "MemoryStoreLane";
        var signature = declaration.MemoryAccessSignature;
        if (
            !isLoad
            && !isStore
            && declaration.StackEffect is not ("MemoryLoad" or "MemoryStore" or "MemoryLoadLane" or "MemoryStoreLane")
            && declaration.Immediate != "MemoryArgumentLaneIndex"
            && signature == null
        )
        {
            return true;
        }
        if (
            !(isLoad || isStore)
            || declaration.StackEffect != declaration.Validation
            || declaration.Immediate != (isLane ? "MemoryArgumentLaneIndex" : "MemoryArgument")
            || signature == null
        )
        {
            return false;
        }
        if (isLane)
        {
            return signature.ValueType == "V128"
                && signature.ByteWidth is 1 or 2 or 4 or 8
                && signature.ByteWidth * declaration.LaneCount == 16
                && !signature.SignExtend;
        }
        if (declaration.LaneCount != 0)
        {
            return false;
        }
        if (signature.ValueType == "V128")
        {
            return signature.ByteWidth is 1 or 2 or 4 or 8 or 16
                && (isLoad || signature.ByteWidth == 16)
                && !signature.SignExtend;
        }
        var valueWidth = signature.ValueType switch
        {
            "I32" or "F32" => 4,
            "I64" or "F64" => 8,
            _ => 0,
        };
        if (signature.ByteWidth is not (1 or 2 or 4 or 8) || signature.ByteWidth > valueWidth)
        {
            return false;
        }
        var isInteger = signature.ValueType is "I32" or "I64";
        if (!isInteger && signature.ByteWidth != valueWidth)
        {
            return false;
        }
        return !signature.SignExtend || (isLoad && isInteger && signature.ByteWidth < valueWidth);
    }

    /// <summary>
    /// 命令情報の検索表と、対応済み命令の実行opcodeを宣言するソースを生成する
    /// </summary>
    /// <param name="instructions">診断がなく、prefixと命令番号が重複しない命令宣言の一覧</param>
    /// <returns>全宣言の記述子と、handlerを持つ宣言の実行opcodeを含むC#ソース</returns>
    private static string GenerateInstructionSet(ImmutableArray<InstructionDeclaration> instructions)
    {
        var source = new StringBuilder(
            """
            // <auto-generated />
            #nullable enable
            using System.Collections.Frozen;

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
            /// <param name="NumericSignature">数値命令の入力型列・結果型 数値以外の場合はnull</param>
            /// <param name="MemoryAccessSignature">ロード命令・ストア命令のアクセス情報 それ以外の場合はnull</param>
            /// <param name="LaneCount">lane即値の上限 lane即値を持たない場合は0</param>
            /// <param name="ConstantBinaryOperation">Core 3.0の定数式で許可する二項演算 それ以外の場合はnull</param>
            internal readonly record struct InstructionDescriptor(
                OpcodeKey Opcode,
                string Name,
                ImmediateKind Immediate,
                StackEffectKind StackEffect,
                ValidationRule Validation,
                ExecutionOpcode? ExecutionOpcode,
                NumericSignature? NumericSignature,
                MemoryAccessSignature? MemoryAccessSignature,
                byte LaneCount,
                global::System.Func<global::WasmSharp.WasmValue, global::WasmSharp.WasmValue, global::WasmSharp.WasmValue>? ConstantBinaryOperation);

            /// <summary>
            /// 命令宣言から生成された命令情報の参照先
            /// </summary>
            internal static partial class InstructionSet
            {
                /// <summary>
                /// Core 2.0の命令識別子に対応する記述子の検索表
                /// </summary>
                private static readonly FrozenDictionary<OpcodeKey, InstructionDescriptor> __core2 = CreateTable([
            """
        );
        AppendDescriptors(source, instructions.Where(x => x.IntroducedIn == "Core2"));
        source.AppendLine(
            """
                ]);

                /// <summary>
                /// Core 2.0と共通の命令にCore 3.0で追加された割当を加えた検索表
                /// </summary>
                private static readonly FrozenDictionary<OpcodeKey, InstructionDescriptor> __core3 = CreateTable([
                    .. __core2.Values,
            """
        );
        AppendDescriptors(source, instructions.Where(x => x.IntroducedIn == "Core3"));
        source.AppendLine(
            """
                ]);

                private static FrozenDictionary<OpcodeKey, InstructionDescriptor> CreateTable(InstructionDescriptor[] instructions) =>
                    instructions.ToFrozenDictionary(x => x.Opcode);

                /// <summary>
                /// 指定した仕様の版で割り当てられた全命令を取得する
                /// </summary>
                internal static FrozenDictionary<OpcodeKey, InstructionDescriptor> ForVersion(WasmSpecVersion specVersion) =>
                    specVersion switch
                    {
                        WasmSpecVersion.Core2 => __core2,
                        WasmSpecVersion.Core3 => __core3,
                        _ => throw new global::System.ArgumentOutOfRangeException(nameof(specVersion)),
                    };

                /// <summary>
                /// 指定した仕様の版で命令識別子に対応する命令情報を取得する
                /// </summary>
                /// <param name="opcode">バイナリ上の命令識別子</param>
                /// <param name="descriptor">見つかった場合の命令情報 見つからない場合は既定値</param>
                /// <param name="specVersion">検索する仕様の版 省略時はCore 2.0</param>
                /// <returns>命令宣言が見つかった場合はtrue 実行handlerの有無とは独立する</returns>
                internal static bool TryGet(OpcodeKey opcode, out InstructionDescriptor descriptor, WasmSpecVersion specVersion = WasmSpecVersion.Core2) =>
                    ForVersion(specVersion).TryGetValue(opcode, out descriptor);
            }
            """
        );
        return source.ToString();
    }

    /// <summary>
    /// 各命令の記述子を命令表の初期化式に書き出す
    /// </summary>
    private static void AppendDescriptors(StringBuilder source, IEnumerable<InstructionDeclaration> instructions)
    {
        foreach (var instruction in instructions)
        {
            var key = $"new OpcodeKey({instruction.Prefix}, {instruction.Code}u)";
            var executionOpcode = instruction.Handler == null ? "null" : $"ExecutionOpcode.{instruction.ExecutionOpcode}";
            var signature = instruction.NumericSignature;
            var numericSignature =
                signature == null
                    ? "null"
                    : $"new NumericSignature([{string.Join(", ", signature.InputTypes.Select(x => $"global::WasmSharp.WasmValueKind.{x}"))}], global::WasmSharp.WasmValueKind.{signature.ResultType})";
            var memory = instruction.MemoryAccessSignature;
            var memoryAccessSignature =
                memory == null
                    ? "null"
                    : $"new MemoryAccessSignature(global::WasmSharp.WasmValueKind.{memory.ValueType}, {memory.ByteWidth}, {(memory.SignExtend ? "true" : "false")})";
            var constantOperation = instruction.IsConstantBinaryOperation
                ? GetIntegerArithmeticMethod(instruction.Handler!)
                : "null";
            source.AppendLine(
                $"        new InstructionDescriptor({key}, {SymbolDisplay.FormatLiteral(instruction.Name, true)}, ImmediateKind.{instruction.Immediate}, StackEffectKind.{instruction.StackEffect}, ValidationRule.{instruction.Validation}, {executionOpcode}, {numericSignature}, {memoryAccessSignature}, {instruction.LaneCount}, {constantOperation}),"
            );
        }
    }

    /// <summary>
    /// Interpreterの実行ループと、定数式と同じ二項演算を呼ぶhandlerを生成する
    /// </summary>
    /// <param name="instructions">検証を通過した命令宣言の一覧</param>
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
            source.AppendLine($"                    result = @{instruction.Handler}(context, in instruction);");
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
            """
        );
        foreach (var handler in instructions.Where(x => x.IsConstantBinaryOperation).Select(x => x.Handler!).Distinct())
        {
            source.AppendLine(
                $$"""

                    /// <summary>
                    /// スタックの2個のオペランドに{{handler}}を適用し、結果を積む
                    /// </summary>
                    internal static ExecutionResult @{{handler}}(InterpreterContext context, in Instruction instruction)
                    {
                        var right = context.PopValue();
                        context.PushValue({{GetIntegerArithmeticMethod(handler)}}(context.PopValue(), right));
                        return default;
                    }
                """
            );
        }
        source.AppendLine("}");
        return source.ToString();
    }

    /// <summary>
    /// 関数と定数式で共有する二項演算のメソッド名を型名で修飾する
    /// </summary>
    private static string GetIntegerArithmeticMethod(string handler)
    {
        return $"global::WasmSharp.Execution.{IntegerArithmeticName}.@{handler}";
    }

    /// <summary>
    /// 命令情報と実行分岐の生成に必要な属性値
    /// </summary>
    /// <param name="Prefix">通常命令では0、拡張命令ではprefix</param>
    /// <param name="Code">prefix内の命令番号</param>
    /// <param name="Name">仕様上の命令名</param>
    /// <param name="IntroducedIn">この命令番号が追加された仕様の版</param>
    /// <param name="Immediate">即値の符号化を表す列挙子名</param>
    /// <param name="StackEffect">スタック効果を表す列挙子名</param>
    /// <param name="Validation">検証規則を表す列挙子名</param>
    /// <param name="Handler">Interpreterのhandler名または定数式と共有するIntegerArithmeticの二項演算名 未対応の宣言ではnull</param>
    /// <param name="NumericSignature">数値命令の型効果を表す属性値 数値以外の宣言ではnull</param>
    /// <param name="MemoryAccessSignature">ロード命令・ストア命令のアクセス情報 それ以外の宣言ではnull</param>
    /// <param name="LaneCount">lane即値の上限 lane即値を持たない場合は0</param>
    /// <param name="IsConstantBinaryOperation">handlerをCore 3.0の定数式と共有する二項演算から生成する場合はtrue</param>
    /// <param name="Location">属性宣言のソース上の位置</param>
    private sealed record InstructionDeclaration(
        byte Prefix,
        uint Code,
        string Name,
        string IntroducedIn,
        string Immediate,
        string StackEffect,
        string Validation,
        string? Handler,
        NumericSignatureDeclaration? NumericSignature,
        MemoryAccessSignatureDeclaration? MemoryAccessSignature,
        byte LaneCount,
        bool IsConstantBinaryOperation,
        Location Location
    )
    {
        /// <summary>
        /// prefixと命令番号から得られる、生成する実行opcodeの列挙子名
        /// </summary>
        public string ExecutionOpcode => Prefix == 0 ? $"Op{Code:X2}" : $"Op{Prefix:X2}_{Code:X2}";
    }

    /// <summary>
    /// 生成する数値signatureの入力型列と結果型の宣言名
    /// </summary>
    /// <param name="InputTypes">スタック下側から上側の順序で並ぶWasmValueKindの列挙子名</param>
    /// <param name="ResultType">結果のWasmValueKindの列挙子名</param>
    private sealed record NumericSignatureDeclaration(ImmutableArray<string> InputTypes, string ResultType);

    /// <summary>
    /// 生成するメモリアクセス情報の属性値
    /// </summary>
    /// <param name="ValueType">読み書きするWasmValueKindの列挙子名</param>
    /// <param name="ByteWidth">読み書きするバイト数</param>
    /// <param name="SignExtend">狭い整数のロードで符号拡張するか</param>
    private sealed record MemoryAccessSignatureDeclaration(string ValueType, byte ByteWidth, bool SignExtend);
}
