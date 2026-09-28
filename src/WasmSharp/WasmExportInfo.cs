namespace WasmSharp;

/// <summary>
/// moduleが宣言し、instanceが公開するexportの名前と種類
/// </summary>
/// <remarks>実体は含まない。Kindに応じたWasmInstanceのGetFunction・GetGlobalResource・GetMemory・GetTableへNameを渡して取得する</remarks>
/// <param name="Name">加工していないexport名</param>
/// <param name="Kind">公開対象の外部要素の種類</param>
public sealed record WasmExportInfo(string Name, WasmExternalKind Kind);
