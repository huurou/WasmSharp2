using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WasmSharp.TestSuiteRunner.Reports;

/// <summary>
/// 保存時の名前と完全に一致する単一の列挙値だけを読み取るJSON変換
/// </summary>
/// <remarks>
/// 標準の文字列変換はFlagsでない列挙でもカンマ区切りの複合表記を受け付け、未定義値や別の分類へ読み替えるため、読取では使わない。
/// </remarks>
internal sealed class StrictEnumConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.IsEnum;
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        return (JsonConverter)
            Activator.CreateInstance(typeof(EnumConverter<>).MakeGenericType(typeToConvert))!;
    }

    /// <summary>
    /// 一つの列挙型について、JsonStringEnumMemberNameの名前、なければメンバー名と値を対応付ける変換
    /// </summary>
    private sealed class EnumConverter<T> : JsonConverter<T>
        where T : struct, Enum
    {
        private readonly Dictionary<string, T> values_ = Enum.GetValues<T>()
            .ToDictionary(GetName, StringComparer.Ordinal);

        public override T Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options
        )
        {
            // 位置情報付きの標準の読取失敗にするため、メッセージを指定しない。
            return
                reader.TokenType == JsonTokenType.String
                && values_.TryGetValue(reader.GetString()!, out var value)
                ? value
                : throw new JsonException();
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(GetName(value));
        }

        private static string GetName(T value)
        {
            var name = value.ToString();
            return typeof(T)
                    .GetField(name)
                    ?.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()
                    ?.Name
                ?? name;
        }
    }
}
