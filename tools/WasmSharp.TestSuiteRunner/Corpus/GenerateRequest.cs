namespace WasmSharp.TestSuiteRunner.Corpus;

/// <summary>
/// CLIで解決済みの配置を使う素材生成の要求
/// </summary>
/// <param name="Profile">対象入力と変換条件を固定するprofile</param>
/// <param name="SpecRoot">公式入力を含むspec-rootの絶対path</param>
/// <param name="WabtRoot">変換器ソースのcheckoutの絶対path</param>
/// <param name="ConverterPath">起動するwast2jsonの絶対path</param>
/// <param name="OutputRoot">manifestと素材を保存する出力先の絶対path</param>
internal sealed record GenerateRequest(
    Core2Profile Profile,
    string SpecRoot,
    string WabtRoot,
    string ConverterPath,
    string OutputRoot
);
