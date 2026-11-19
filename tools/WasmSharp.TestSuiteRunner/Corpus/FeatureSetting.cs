namespace WasmSharp.TestSuiteRunner.Corpus;

/// <summary>
/// 検証プロファイルにおける機能の状態と変換器の既定値
/// </summary>
/// <param name="Name">検証プロファイルで使う機能名</param>
/// <param name="DefaultEnabled">変換器の機能スイッチの既定値 スイッチがない場合はnull</param>
/// <param name="Enabled">検証プロファイルで機能を有効にするかどうか WABTを使う場合は変換時の実効値</param>
internal sealed record FeatureSetting(string Name, bool? DefaultEnabled, bool Enabled);
