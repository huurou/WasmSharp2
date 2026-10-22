using System.Text.Json;
using System.Text.Json.Serialization;

namespace WasmSharp.Benchmarks.Cases;

/// <summary>
/// 素材とケースの保存用DTOに共通のJSON形式
/// </summary>
internal static class CaseJson
{
    internal static JsonSerializerOptions Options { get; } =
        new()
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
