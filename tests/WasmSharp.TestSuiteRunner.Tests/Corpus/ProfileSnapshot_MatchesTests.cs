using WasmSharp.TestSuiteRunner.Corpus;

namespace WasmSharp.TestSuiteRunner.Tests.Corpus;

internal class ProfileSnapshot_MatchesTests
{
    private readonly ConversionProfileLoader _profileLoader = new ConversionProfileLoader();

    [Test]
    public async Task 同じprofileから保存した条件と比べる_一致すると判定する()
    {
        // Arrange
        var profile = _profileLoader.Load();
        var snapshot = ProfileSnapshot.FromProfile(profile);

        // Act
        var matches = snapshot.Matches(profile);

        // Assert
        await Assert.That(matches).IsTrue();
    }

    [Test]
    public async Task 入力を1件欠いた条件と比べる_一致しないと判定する()
    {
        // Arrange
        var profile = _profileLoader.Load();
        var snapshot = ProfileSnapshot.FromProfile(profile);
        snapshot.Inputs.RemoveAt(snapshot.Inputs.Count - 1);

        // Act
        var matches = snapshot.Matches(profile);

        // Assert
        await Assert.That(matches).IsFalse();
    }

    [Test]
    [Arguments("id")]
    [Arguments("spec")]
    [Arguments("wabt")]
    [Arguments("wasm_tools")]
    [Arguments("working_directory")]
    [Arguments("conversion")]
    [Arguments("features")]
    [Arguments("logical_arguments")]
    public async Task 入力集合が同じでも生成条件の1項目が異なる_一致しないと判定する(string condition)
    {
        // Arrange
        var profile = condition is "wabt" or "conversion" ? _profileLoader.LoadLegacyCore2() : _profileLoader.Load();
        var snapshot = ProfileSnapshot.FromProfile(profile);
        snapshot = condition switch
        {
            "id" => snapshot with { Id = "different-profile" },
            "spec" => snapshot with { Spec = snapshot.Spec with { Commit = new string('a', 40) } },
            "wabt" => snapshot with { Wabt = snapshot.Wabt! with { Commit = new string('b', 40) } },
            "wasm_tools" => snapshot with { WasmTools = snapshot.WasmTools! with { Version = "1.263.1" } },
            "working_directory" => snapshot with { WorkingDirectory = "different-directory" },
            "conversion" => snapshot with { Conversion = snapshot.Conversion! with { Check = !snapshot.Conversion!.Check } },
            "features" => snapshot with { Features = [.. snapshot.Features.Skip(1)] },
            "logical_arguments" => snapshot with { LogicalArguments = [.. snapshot.LogicalArguments, "--debug-names"] },
            _ => throw new ArgumentOutOfRangeException(nameof(condition)),
        };

        // Act
        var matches = snapshot.Matches(profile);

        // Assert
        await Assert.That(matches).IsFalse();
    }
}
