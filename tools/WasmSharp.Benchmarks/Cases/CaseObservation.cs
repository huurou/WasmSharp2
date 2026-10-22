namespace WasmSharp.Benchmarks.Cases;

/// <summary>
/// 計時外で組み立てる段階別の観測 計時内では既存fieldへ公開操作の戻り値だけを保持する。
/// </summary>
internal abstract record CaseObservation
{
    private protected CaseObservation() { }
}

/// <summary>
/// DecodeまたはDecode+Validateで最後に生成したmodule
/// </summary>
internal sealed record ModuleObservation(WasmModule Module) : CaseObservation;

/// <summary>
/// Instantiateで最後に生成したinstance 資源初期状態とstart結果を公開APIで照合できる。
/// </summary>
internal sealed record InstanceObservation(WasmInstance Instance) : CaseObservation;

/// <summary>
/// 最後のInvokeの所属instanceと戻り値 memory/table/globalの観測は同じinstanceから行う。
/// </summary>
internal sealed record InvokeObservation(WasmInstance Instance, WasmResults Results)
    : CaseObservation;
