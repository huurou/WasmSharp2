using System.Text.Json;
using System.Text.Json.Serialization;

namespace WasmSharp.Benchmarks.Cases;

/// <summary>
/// 素材とケースの保存用DTOに共通のJSON形式
/// </summary>
internal sealed class CaseJson
{
    internal JsonSerializerOptions Options { get; } = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
        Converters =
            {
                new JsonStringEnumConverter(
                    JsonNamingPolicy.SnakeCaseLower,
                    allowIntegerValues: false
                ),
            },
    };
}
