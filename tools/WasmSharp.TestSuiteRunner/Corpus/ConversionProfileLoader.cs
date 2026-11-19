using System.Text.Json;

namespace WasmSharp.TestSuiteRunner.Corpus;

/// <summary>
/// Core 2.0とCore 3.0の固定検証プロファイルを同じJSON設定で読み取る
/// </summary>
internal sealed class ConversionProfileLoader
{
    /// <summary>
    /// 埋込みprofileのsnake_case名に対応する読取設定
    /// </summary>
    private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>
    /// Core 2.0の公式入力とWABTの変換条件を埋込みプロファイルから読み取る。
    /// </summary>
    /// <returns>埋込みJSONに記録された固定の入力集合と変換条件</returns>
    /// <exception cref="JsonException">埋込みJSONをprofileとして読み取れない、または読み取った値がnullの場合</exception>
    internal ConversionProfile Load()
    {
        using var stream = typeof(ConversionProfile).Assembly.GetManifestResourceStream("Core2.Profile")!;
        return Load(stream);
    }

    /// <summary>
    /// Core 3.0の公式入力とwasm-toolsの変換条件を埋込みプロファイルから読み取る。
    /// </summary>
    /// <returns>Core 3.0の固定入力、対象機能、変換器のリリースと論理引数</returns>
    /// <exception cref="JsonException">埋込みJSONをプロファイルとして読み取れない、または読み取った値がnullの場合</exception>
    internal ConversionProfile LoadCore3()
    {
        using var stream = typeof(ConversionProfile).Assembly.GetManifestResourceStream("Core3.Profile")!;
        return Load(stream);
    }

    /// <summary>
    /// 指定したstreamからprofileを読み取り、streamの所有権を呼び出し元に残す。
    /// </summary>
    /// <param name="stream">埋込みprofileと同じ形式のUTF-8のJSONを読み取るstream</param>
    /// <returns>JSONに記録された入力集合と変換条件</returns>
    /// <exception cref="JsonException">JSONをprofileとして読み取れない、または読み取った値がnullの場合</exception>
    internal ConversionProfile Load(Stream stream)
    {
        var snapshot = JsonSerializer.Deserialize<ProfileSnapshot>(stream, _jsonOptions)
            ?? throw new JsonException("profileがnullです。");
        return snapshot.ToProfile();
    }
}
