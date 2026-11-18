using System.Text.Json;

namespace WasmSharp.TestSuiteRunner.Corpus;

/// <summary>
/// 読取設定を共有し、固定Core 2.0 profileのJSONを読み取る
/// </summary>
internal sealed class Core2ProfileLoader
{
    /// <summary>
    /// 埋込みprofileのsnake_case名に対応する読取設定
    /// </summary>
    private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>
    /// 配置先やランタイムの実装状況に依存しない埋込みprofileを読み取る。
    /// </summary>
    /// <returns>埋込みJSONに記録された固定の入力集合と変換条件</returns>
    /// <exception cref="JsonException">埋込みJSONをprofileとして読み取れない、または読み取った値がnullの場合</exception>
    internal Core2Profile Load()
    {
        using var stream = typeof(Core2Profile).Assembly.GetManifestResourceStream("Core2.Profile")!;
        return Load(stream);
    }

    /// <summary>
    /// 指定したstreamからprofileを読み取り、streamの所有権を呼び出し元に残す。
    /// </summary>
    /// <param name="stream">埋込みprofileと同じ形式のUTF-8のJSONを読み取るstream</param>
    /// <returns>JSONに記録された入力集合と変換条件</returns>
    /// <exception cref="JsonException">JSONをprofileとして読み取れない、または読み取った値がnullの場合</exception>
    internal Core2Profile Load(Stream stream)
    {
        return JsonSerializer.Deserialize<Core2Profile>(stream, _jsonOptions)
            ?? throw new JsonException("profileがnullです。");
    }
}
