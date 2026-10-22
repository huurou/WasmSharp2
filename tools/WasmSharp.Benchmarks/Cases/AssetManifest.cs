namespace WasmSharp.Benchmarks.Cases;

/// <summary>
/// 固定素材とケース定義、生成時のprofile・source・tool・引数を結び付ける保存用記録
/// </summary>
internal sealed record AssetManifest(
    uint SchemaVersion,
    ManifestFile Profile,
    ManifestFile Cases,
    List<AssetDefinition> Assets
)
{
    /// <summary>
    /// manifestの相対参照を照合し、計時前に素材と入力を読み込む
    /// </summary>
    internal static LoadedAssets Load(string path) => LoadedAssets.Load(path);
}

/// <summary>
/// manifestからの相対pathとファイル内容のSHA-256
/// </summary>
internal sealed record ManifestFile(string RelativePath, string Sha256);

/// <summary>
/// 1つの生成済みWasm素材と、その生成に使用した入力・手順
/// </summary>
internal sealed record AssetDefinition(
    string AssetId,
    ManifestFile Binary,
    List<AssetSource> Sources,
    List<AssetGenerationStep> GenerationSteps
);

/// <summary>
/// 素材sourceの相対path・固定revision・内容のSHA-256
/// </summary>
internal sealed record AssetSource(string RelativePath, string Revision, string Sha256);

/// <summary>
/// 素材生成またはprofile検査で使用したツールと、順序を保った引数
/// </summary>
internal sealed record AssetGenerationStep(
    AssetTool Tool,
    List<string> Arguments,
    string ArgumentsSha256
);

/// <summary>
/// 生成ツールの固定版・revision・実行ファイル・ビルド条件
/// </summary>
internal sealed record AssetTool(
    string Name,
    string Version,
    string Revision,
    ManifestFile Executable,
    string BuildConfiguration
);
