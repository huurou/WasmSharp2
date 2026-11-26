using System.Collections.Immutable;
using WasmSharp.Modules.Definitions;

namespace WasmSharp.Tests;

internal class WasmStructType_ToStringTests
{
    [Test]
    [Arguments(16, "")]
    [Arguments(17, " …")]
    public async Task フィールド数が表示上限に達する_16個までを表示し残りがあるときだけ省略を示す(int count, string suffix)
    {
        // Arrange
        var field = new WasmFieldType(WasmValueKind.I32, false, WasmPackedType.None);
        ImmutableArray<WasmFieldType> fields =
        [
            new WasmFieldType(WasmValueKind.I32, true, WasmPackedType.I8),
            new WasmFieldType(WasmValueKind.I32, false, WasmPackedType.I16),
            .. Enumerable.Repeat(field, count - 2),
        ];
        var type = WasmDefinedType.FromDecodedGroup([new TypeDeclaration(fields, true, [])], firstIndex: 0);
        var expectedFields = " (field (mut i8)) (field i16)" + string.Concat(Enumerable.Repeat(" (field I32)", 14));

        // Act
        var text = type.ToString();

        // Assert
        await Assert.That(text).IsEqualTo($"(struct{expectedFields}{suffix})");
    }
}
