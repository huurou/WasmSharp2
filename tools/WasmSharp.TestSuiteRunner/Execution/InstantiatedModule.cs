namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// 通常moduleの成功実体。registerでexport一覧を取得するため、instanceと生成元のmoduleを組で保持する
/// </summary>
/// <param name="Module">instanceの生成元</param>
/// <param name="Instance">Instantiateに成功したinstance</param>
internal sealed record InstantiatedModule(WasmModule Module, WasmInstance Instance);
