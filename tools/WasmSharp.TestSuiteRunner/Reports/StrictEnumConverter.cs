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
    /// <summary>
    /// 対象の型が列挙型で、この変換器を使用できるかを判定する。
    /// </summary>
    /// <param name="typeToConvert">JSONへ変換する型</param>
    /// <returns>対象が列挙型の場合はtrue</returns>
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.IsEnum;
    }

    /// <summary>
    /// 指定された列挙型の保存名を厳密に照合する変換器を作る。
    /// </summary>
    /// <param name="typeToConvert">変換する列挙型</param>
    /// <param name="options">シリアライザーから渡される設定</param>
    /// <returns>指定された列挙型専用の変換器</returns>
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        return (JsonConverter)
            Activator.CreateInstance(typeof(EnumConverter<>).MakeGenericType(typeToConvert))!;
    }

    /// <summary>
    /// 一つの列挙型について、JsonStringEnumMemberNameの名前、なければメンバー名と値を対応付ける変換
    /// </summary>
    /// <typeparam name="T">保存名と値を対応付ける列挙型</typeparam>
    private sealed class EnumConverter<T> : JsonConverter<T>
        where T : struct, Enum
    {
        /// <summary>
        /// 定義済みの保存名と列挙値の対応表 名前は大文字と小文字を区別する
        /// </summary>
        private readonly Dictionary<string, T> values_ = Enum.GetValues<T>()
            .ToDictionary(GetName, StringComparer.Ordinal);

        /// <summary>
        /// 保存名と完全に一致するJSON文字列を、定義済みの列挙値へ変換する。
        /// </summary>
        /// <param name="reader">現在のJSON値を読むリーダー</param>
        /// <param name="typeToConvert">変換先の列挙型</param>
        /// <param name="options">シリアライザーから渡される設定</param>
        /// <returns>保存名に対応する列挙値</returns>
        /// <exception cref="JsonException">JSON値が文字列でない場合、または定義済みの保存名と一致しない場合</exception>
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

        /// <summary>
        /// 属性で指定された保存名、なければ列挙値の文字列表現をJSON文字列として出力する。
        /// </summary>
        /// <param name="writer">JSON文字列の出力先</param>
        /// <param name="value">保存名を出力する列挙値</param>
        /// <param name="options">シリアライザーから渡される設定</param>
        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(GetName(value));
        }

        /// <summary>
        /// 列挙値に対応する保存名を取得する。
        /// </summary>
        /// <param name="value">保存名を取得する列挙値</param>
        /// <returns>JsonStringEnumMemberName属性の名前 指定がない場合は列挙値の文字列表現</returns>
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
