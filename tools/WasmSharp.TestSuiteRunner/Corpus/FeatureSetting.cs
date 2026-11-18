namespace WasmSharp.TestSuiteRunner.Corpus;

/// <summary>
/// 固定WABTが持つ一つの機能の設定
/// </summary>
/// <param name="Name">WABTのfeature名</param>
/// <param name="DefaultEnabled">変換器の機能スイッチの既定値 スイッチがない場合はnull</param>
/// <param name="Enabled">変換時の実効値</param>
internal sealed record FeatureSetting(string Name, bool? DefaultEnabled, bool Enabled);
