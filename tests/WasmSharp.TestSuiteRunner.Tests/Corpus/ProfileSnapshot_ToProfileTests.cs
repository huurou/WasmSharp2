using WasmSharp.TestSuiteRunner.Corpus;

namespace WasmSharp.TestSuiteRunner.Tests.Corpus;

internal class ProfileSnapshot_ToProfileTests
{
    private readonly Core2ProfileLoader _profileLoader = new Core2ProfileLoader();

    [Test]
    public async Task 保存用profileを不変モデルへ戻す_全条件を保持してコレクションをコピーする()
    {
        // Arrange
        var original = _profileLoader.Load();
        var snapshot = ProfileSnapshot.FromProfile(original);

        // Act
        var profile = snapshot.ToProfile();
        snapshot.Inputs.Clear();
        snapshot.Features.Clear();
        snapshot.LogicalArguments.Clear();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(profile.Id).IsEqualTo(original.Id);
            await Assert.That(profile.Spec).IsEqualTo(original.Spec);
            await Assert.That(profile.Wabt).IsEqualTo(original.Wabt);
            await Assert.That(profile.WorkingDirectory).IsEqualTo(original.WorkingDirectory);
            await Assert.That(profile.Conversion).IsEqualTo(original.Conversion);
            await Assert.That(profile.Inputs.SequenceEqual(original.Inputs)).IsTrue();
            await Assert.That(profile.Features.SequenceEqual(original.Features)).IsTrue();
            await Assert.That(profile.LogicalArguments.SequenceEqual(original.LogicalArguments)).IsTrue();
        }
    }
}
