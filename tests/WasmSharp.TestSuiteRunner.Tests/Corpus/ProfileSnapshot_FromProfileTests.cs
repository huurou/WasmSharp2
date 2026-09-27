using WasmSharp.TestSuiteRunner.Corpus;

namespace WasmSharp.TestSuiteRunner.Tests.Corpus;

internal class ProfileSnapshot_FromProfileTests
{
    [Test]
    public async Task 保存用profileを変更する_元の固定profileと別の保存用profileに影響しない()
    {
        // Arrange
        var profile = Core2Profile.Load();
        var snapshot = ProfileSnapshot.FromProfile(profile);
        var other = ProfileSnapshot.FromProfile(profile);

        // Act
        snapshot.Inputs.Clear();
        snapshot.Features.Clear();
        snapshot.LogicalArguments.Clear();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(profile.Inputs.Length).IsEqualTo(147);
            await Assert.That(profile.Features.Length).IsEqualTo(21);
            await Assert.That(profile.LogicalArguments.Length).IsEqualTo(3);
            await Assert.That(other.Inputs.SequenceEqual(profile.Inputs)).IsTrue();
            await Assert.That(other.Features.SequenceEqual(profile.Features)).IsTrue();
            await Assert
                .That(other.LogicalArguments.SequenceEqual(profile.LogicalArguments))
                .IsTrue();
        }
    }
}
