using System.Collections.Immutable;
using WasmSharp.Modules.Definitions;

namespace WasmSharp.Tests.Modules.Definitions;

internal class TypeDeclaration_ConstructorTests
{
    [Test]
    public async Task 未初期化のフィールド配列で構造体を宣言する_空のフィールド列を持つ構造体になる()
    {
        // Arrange
        ImmutableArray<WasmFieldType> fields = default;

        // Act
        var declaration = new TypeDeclaration(fields, isFinal: true, supertypes: []);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(declaration.IsStruct).IsTrue();
            await Assert.That(declaration.Fields.IsDefault).IsFalse();
            await Assert.That(declaration.Fields.IsDefaultOrEmpty).IsTrue();
        }
    }
}
