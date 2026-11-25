using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace WasmSharp.Generators.Tests;

internal static class GeneratorTestSource
{
    public const string EXECUTION_CONTRACTS = """
        namespace WasmSharp
        {
            public sealed class WasmFunction;
            public readonly record struct WasmValueType(WasmValueKind Kind, bool IsNullable = false)
            {
                public static implicit operator WasmValueType(WasmValueKind kind) => new WasmValueType(kind, kind.IsReference());
                public static WasmValueType Reference(WasmValueKind kind, bool isNullable) => new WasmValueType(kind, isNullable);
            }
        }
        namespace WasmSharp.Exceptions
        {
            public enum WasmTrapReason { Unreachable, IntegerDivideByZero }
            public enum WasmExhaustionReason { CallDepthLimit, HostStackLimit }
        }
        namespace WasmSharp.Execution
        {
            internal sealed class InterpreterContext(int frameCount)
            {
                private readonly System.Collections.Generic.Stack<WasmValue> _operands = new System.Collections.Generic.Stack<WasmValue>();
                public Instruction[] Instructions { get; init; } = [];
                internal int FrameCount { get; private set; } = frameCount;
                public int Pc { get; private set; }
                public WasmValue Value { get; set; }
                public System.Collections.Generic.List<string> Log { get; } = [];
                public uint FunctionIndex => 7;
                public Instruction ReadNextInstruction() => Instructions[Pc++];
                public WasmValue PopValue() => _operands.Pop();
                public void PushValue(WasmValue value) => _operands.Push(value);
                internal void CompleteFrame() => FrameCount--;
            }
        }
        """;

    private static readonly ImmutableArray<MetadataReference> __references =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(x => MetadataReference.CreateFromFile(x)),
    ];

    public static CSharpCompilation CreateCompilation(string declarations, string? executionContracts = null)
    {
        var assembly = typeof(GeneratorTestSource).Assembly;
        // WasmValueKindは命令宣言にも必要なため、常にInstructionContractsから取り込む。
        // 実行契約を使わない場合は、命令記述子が参照するWasmValueの型だけを用意する。
        var contracts = assembly
            .GetManifestResourceNames()
            .Where(x =>
                x.StartsWith("InstructionContracts.", StringComparison.Ordinal)
                || (
                    executionContracts is not null
                    && (
                        x.StartsWith("ExecutionContracts.", StringComparison.Ordinal)
                        || x.StartsWith("ModuleContracts.", StringComparison.Ordinal)
                    )
                )
            )
            .Select(x =>
            {
                using var reader = new StreamReader(assembly.GetManifestResourceStream(x)!);
                return CSharpSyntaxTree.ParseText(reader.ReadToEnd());
            });
        // Roslynで直接コンパイルするため、埋め込んだソースに必要な暗黙のusingを補う
        var implicitUsings = CSharpSyntaxTree.ParseText("global using System;\nglobal using System.Linq;");
        return CSharpCompilation.Create(
            "GeneratedInstructions_" + Guid.NewGuid().ToString("N"),
            contracts
                .Append(implicitUsings)
                .Append(CSharpSyntaxTree.ParseText(declarations))
                .Append(
                    CSharpSyntaxTree.ParseText(
                        executionContracts
                            ?? """
                            namespace WasmSharp
                            {
                                internal readonly struct WasmValue;
                            }
                            namespace WasmSharp.Instructions
                            {
                                internal sealed partial class ConstantReferenceOperation;
                            }
                            namespace WasmSharp.Execution
                            {
                                internal readonly struct ExecutionResult;
                                internal sealed class InterpreterContext;
                                internal readonly struct Instruction;
                            }
                            """
                    )
                ),
            __references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable
            )
        );
    }

    public static async Task AssertDiagnostic(
        GeneratorDriver driver,
        ImmutableArray<Diagnostic> diagnostics,
        string expectedId
    )
    {
        await Assert.That(diagnostics.Length).IsEqualTo(1);
        using (Assert.Multiple())
        {
            await Assert.That(diagnostics[0].Id).IsEqualTo(expectedId);
            await Assert.That(diagnostics[0].Severity).IsEqualTo(DiagnosticSeverity.Error);
            await Assert.That(diagnostics[0].Descriptor.IsEnabledByDefault).IsTrue();
            await Assert.That(diagnostics[0].Descriptor.Title.ToString()).IsNotEmpty();
            await Assert.That(diagnostics[0].Descriptor.Category).IsEqualTo("InstructionGeneration");
            await Assert.That(diagnostics[0].GetMessage()).IsNotEmpty();
            await Assert.That(diagnostics[0].Location.IsInSource).IsTrue();
            await Assert.That(driver.GetRunResult().GeneratedTrees).IsEmpty();
        }
    }

    public static async Task AssertCompilesAndRuns(Compilation compilation, string body)
    {
        var output = compilation.AddSyntaxTrees(
            CSharpSyntaxTree.ParseText(
                $$"""
                internal static class Probe
                {
                    public static bool Run()
                    {
                        {{body}}
                    }
                }
                """
            )
        );
        using var stream = new MemoryStream();
        var emit = output.Emit(stream);
        await Assert
            .That(string.Join(Environment.NewLine, emit.Diagnostics.Where(x => x.Severity >= DiagnosticSeverity.Warning)))
            .IsEqualTo("");
        await Assert.That(emit.Success).IsTrue();
        stream.Position = 0;
        var loadContext = new AssemblyLoadContext(null, isCollectible: true);
        try
        {
            var assembly = loadContext.LoadFromStream(stream);
            var run = assembly.GetType("Probe")!.GetMethod("Run", BindingFlags.Public | BindingFlags.Static)!;
            await Assert.That((bool)run.Invoke(null, null)!).IsTrue();
        }
        finally
        {
            loadContext.Unload();
        }
    }
}
