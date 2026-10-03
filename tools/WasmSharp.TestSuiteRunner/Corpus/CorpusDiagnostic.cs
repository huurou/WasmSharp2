namespace WasmSharp.TestSuiteRunner.Corpus;

/// <summary>
/// 素材の生成・列挙・照合における診断
/// </summary>
/// <param name="Operation">失敗した操作</param>
/// <param name="Message">加工しない診断内容</param>
/// <param name="Path">対象の入力または素材の相対path 操作全体の診断ではnull</param>
/// <param name="ExceptionType">観測した例外の完全型名 例外以外の失敗ではnull</param>
internal sealed record CorpusDiagnostic(
    string Operation,
    string Message,
    string? Path = null,
    string? ExceptionType = null
);
