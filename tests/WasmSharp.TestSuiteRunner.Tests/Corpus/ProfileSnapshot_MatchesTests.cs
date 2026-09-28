using WasmSharp.TestSuiteRunner.Corpus;

namespace WasmSharp.TestSuiteRunner.Tests.Corpus;

internal class ProfileSnapshot_MatchesTests
{
    [Test]
    public async Task 同じprofileから保存した条件と比べる_一致すると判定する()
    {
        // Arrange
        var profile = Core2Profile.Load();
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
        var profile = Core2Profile.Load();
        var snapshot = ProfileSnapshot.FromProfile(profile);
        snapshot.Inputs.RemoveAt(snapshot.Inputs.Count - 1);

        // Act
        var matches = snapshot.Matches(profile);

        // Assert
        await Assert.That(matches).IsFalse();
    }
}
