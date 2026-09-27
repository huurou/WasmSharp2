namespace WasmSharp.TestSuiteRunner.Corpus;

/// <summary>
/// 固定WABTが持つ一つの機能の設定
/// </summary>
/// <param name="Name">WABTのfeature名</param>
/// <param name="DefaultEnabled">固定ソースに記載された既定値</param>
/// <param name="Enabled">変換時の実効値</param>
internal sealed record FeatureSetting(string Name, bool DefaultEnabled, bool Enabled);
