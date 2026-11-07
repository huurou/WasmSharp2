using System.Text.Json;
using WasmSharp.TestSuiteRunner.Reports;

namespace WasmSharp.TestSuiteRunner.Tests.Reports;

internal class StrictEnumConverter_CreateConverterTests
{
    [Test]
    public async Task 複合表記の列挙値を読む_単一分類として扱わず拒否する()
    {
        // Arrange
        var options = new JsonSerializerOptions { Converters = { new StrictEnumConverter() } };

        // Act & Assert
        await Assert
            .That(() => JsonSerializer.Deserialize<CaseOutcome>("\"passed,failed\"", options))
            .ThrowsExactly<JsonException>();
    }
}
