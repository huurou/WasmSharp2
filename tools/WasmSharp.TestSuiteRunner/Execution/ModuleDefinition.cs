using System.Collections.Immutable;

namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// Validateに成功した定義 同じ定義からのインスタンス化で再Decode・再Validateせずに使う
/// </summary>
/// <param name="Module">Validateに成功したmodule</param>
/// <param name="Bytes">Instantiate前の公開import情報取得に使う照合済みbinary</param>
internal sealed record ModuleDefinition(WasmModule Module, ImmutableArray<byte> Bytes);
