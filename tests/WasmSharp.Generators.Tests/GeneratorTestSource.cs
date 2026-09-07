using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace WasmSharp.Generators.Tests;

internal static class GeneratorTestSource
{
    public const string EXECUTION_CONTRACTS = """
        global using System;
        global using System.Collections.Immutable;
        namespace WasmSharp
        {
            public readonly record struct WasmValue(long Bits);
        }
        namespace WasmSharp.Exceptions
        {
            public enum WasmTrapReason { Unreachable, IntegerDivideByZero }
            public enum WasmExhaustionReason { CallDepthLimit }
        }
        namespace WasmSharp.Execution
        {
            internal enum ExecutionStatus { Success, Trap, Exhaustion }
            internal readonly record struct ExecutionResult(
                ExecutionStatus Status,
                ImmutableArray<WasmValue> Values,
                Exceptions.WasmTrapReason? TrapReason,
                Exceptions.WasmExhaustionReason? ExhaustionReason,
                int? Limit,
                uint? FunctionIndex,
                long? ByteOffset);
            internal readonly record struct Instruction(
                Instructions.ExecutionOpcode Opcode, WasmValue Immediate, long ByteOffset);
            internal sealed class WasmExecutionContext
            {
                public Instruction[] Instructions { get; init; } = [];
                public int FrameCount { get; set; }
                public int Pc { get; private set; }
                public WasmValue Value { get; set; }
                public uint FunctionIndex => 7;
                public Instruction ReadNextInstruction() => Instructions[Pc++];
            }
        }
        """;

    private static readonly ImmutableArray<MetadataReference> references_ =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(x => MetadataReference.CreateFromFile(x)),
    ];

    public static CSharpCompilation CreateCompilation(
        string declarations,
        string? executionContracts = null
    )
    {
        var assembly = typeof(GeneratorTestSource).Assembly;
        var contracts = assembly
            .GetManifestResourceNames()
            .Where(x => x.StartsWith("InstructionContracts.", StringComparison.Ordinal))
            .Select(x =>
            {
                using var reader = new StreamReader(assembly.GetManifestResourceStream(x)!);
                return CSharpSyntaxTree.ParseText(reader.ReadToEnd());
            });
        return CSharpCompilation.Create(
            "GeneratedInstructions_" + Guid.NewGuid().ToString("N"),
            contracts
                .Append(CSharpSyntaxTree.ParseText(declarations))
                .Append(
                    CSharpSyntaxTree.ParseText(
                        executionContracts
                            ?? """
                            global using System;
                            namespace WasmSharp.Execution
                            {
                                internal readonly struct ExecutionResult;
                                internal sealed class WasmExecutionContext;
                                internal readonly struct Instruction;
                            }
                            """
                    )
                ),
            references_,
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
            .That(
                string.Join(
                    Environment.NewLine,
                    emit.Diagnostics.Where(x => x.Severity >= DiagnosticSeverity.Warning)
                )
            )
            .IsEqualTo("");
        await Assert.That(emit.Success).IsTrue();
        stream.Position = 0;
        var loadContext = new AssemblyLoadContext(null, isCollectible: true);
        try
        {
            var assembly = loadContext.LoadFromStream(stream);
            var run = assembly
                .GetType("Probe")!
                .GetMethod("Run", BindingFlags.Public | BindingFlags.Static)!;
            await Assert.That((bool)run.Invoke(null, null)!).IsTrue();
        }
        finally
        {
            loadContext.Unload();
        }
    }
}
