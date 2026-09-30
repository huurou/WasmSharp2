using WasmSharp.TestSuiteRunner.Reports;

namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// 入力内で共有する固定spectest環境を公開APIで生成する
/// </summary>
internal static class SpectestFactory
{
    /// <summary>
    /// 固定型の7関数・4global・table・memoryを持つ新しい提供元を作る。
    /// </summary>
    /// <remarks>
    /// 1入力につき一度作り、全moduleへ同じ提供元を登録する。別入力では新しく作り、変更されたリソースを持ち込まない。
    /// printは現在のcommandへ記録し、callback内の例外は実体を状態へ残してそのまま再throwする。
    /// </remarks>
    /// <param name="state">printとcallback失敗を記録する入力の状態</param>
    internal static WasmHostModule Create(ScriptState state)
    {
        var host = new WasmHostModule("spectest");
        DefinePrint(host, state, "print", []);
        DefinePrint(host, state, "print_i32", [WasmValueKind.I32]);
        DefinePrint(host, state, "print_i64", [WasmValueKind.I64]);
        DefinePrint(host, state, "print_f32", [WasmValueKind.F32]);
        DefinePrint(host, state, "print_f64", [WasmValueKind.F64]);
        DefinePrint(host, state, "print_i32_f32", [WasmValueKind.I32, WasmValueKind.F32]);
        DefinePrint(host, state, "print_f64_f64", [WasmValueKind.F64, WasmValueKind.F64]);
        host.Define(
            "global_i32",
            new WasmGlobal(new(WasmValueKind.I32, false), WasmValue.FromI32(666))
        );
        host.Define(
            "global_i64",
            new WasmGlobal(new(WasmValueKind.I64, false), WasmValue.FromI64(666))
        );
        host.Define(
            "global_f32",
            new WasmGlobal(new(WasmValueKind.F32, false), WasmValue.FromF32(666.6f))
        );
        host.Define(
            "global_f64",
            new WasmGlobal(new(WasmValueKind.F64, false), WasmValue.FromF64(666.6d))
        );
        host.Define("table", new WasmTable(WasmValueKind.FuncRef, new(10, 20)));
        host.Define("memory", new WasmMemory(new(1, 2)));
        return host;
    }

    /// <summary>
    /// 引数の型とビット列を呼出順に記録し、結果0個で復帰するホスト関数を定義する。
    /// </summary>
    /// <param name="host">固定spectestの提供元</param>
    /// <param name="state">現在のcommandの記録先</param>
    /// <param name="name">print系関数名</param>
    /// <param name="parameters">固定した引数型の並び</param>
    private static void DefinePrint(
        WasmHostModule host,
        ScriptState state,
        string name,
        ReadOnlySpan<WasmValueKind> parameters
    )
    {
        host.Define(
            name,
            WasmFunction.CreateHost(
                new(parameters, []),
                arguments =>
                {
                    try
                    {
                        var print = new PrintRecord(name);
                        foreach (var argument in arguments)
                        {
                            print.Arguments.Add(ValueCodec.Record(argument, state));
                        }
                        state.RecordPrint(print);
                        return new WasmResults([]);
                    }
                    catch (Exception exception)
                    {
                        // 例外型だけではWasm由来と区別できないため、同じ例外実体を記録して伝播する。
                        state.CallbackException = exception;
                        throw;
                    }
                }
            )
        );
    }
}
