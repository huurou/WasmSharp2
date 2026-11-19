namespace WasmSharp.TestSuiteRunner.Corpus;

/// <summary>
/// CLIで解決済みの配置を使う素材生成の要求
/// </summary>
internal sealed record GenerateRequest
{
    internal ConversionProfile Profile { get; }

    internal string SpecRoot { get; init; }

    internal string ConverterPath { get; init; }

    internal string OutputRoot { get; init; }

    /// <summary>
    /// WABTを使う要求では固定条件とcheckoutの配置を揃えて保持する。
    /// </summary>
    internal (string Root, ConversionProfile.WabtSettings Settings)? WabtCheckout { get; }

    /// <summary>
    /// profileに応じて、配置情報とWABTに必要な固定条件を結び付ける。
    /// </summary>
    /// <param name="profile">対象入力と変換条件を固定するprofile</param>
    /// <param name="specRoot">公式入力を含むspec-rootの絶対path</param>
    /// <param name="wabtRoot">WABTソースのcheckoutの絶対path WABTを使わない場合は不要</param>
    /// <param name="converterPath">起動する変換器の絶対path</param>
    /// <param name="outputRoot">manifestと素材を保存する出力先の絶対path</param>
    internal GenerateRequest(
        ConversionProfile profile,
        string specRoot,
        string? wabtRoot,
        string converterPath,
        string outputRoot
    )
    {
        Profile = profile;
        SpecRoot = specRoot;
        ConverterPath = converterPath;
        OutputRoot = outputRoot;
        WabtCheckout = profile.Wabt is { } wabt
            ? (wabtRoot ?? throw new ArgumentNullException(nameof(wabtRoot)), wabt)
            : null;
    }
}
