using System;
using System.IO;
using MechaBattle.Contracts.Settings;
using MechaBattle.Settings;
using NUnit.Framework;
namespace MechaBattle.Tests
{
    public sealed class PlayerPreferencesTests
    {
        private static readonly string[] Names = { "Low", "Medium", "High" };
        private static PlayerPreferences Defaults() => new PlayerPreferences { mouseSensitivity = 0.12f, masterVolume = 0.8f, qualityLevel = 1, qualityName = "Medium" };
        private static PlayerPreferences Resolve(PlayerPreferences value, string[] names = null) => PlayerPreferencesPolicy.Resolve(value, Defaults(), names ?? Names, 0.01f, 1);
        [Test]
        public void FirstLaunchKeepsProjectSensitivityVolumeAndQuality()
        { var p = Resolve(null); Assert.That(p.mouseSensitivity, Is.EqualTo(0.12f)); Assert.That(p.masterVolume, Is.EqualTo(0.8f)); Assert.That(p.qualityName, Is.EqualTo("Medium")); }
        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)]
        public void NonFiniteValuesFallBackWithoutMutingThePlayer(float invalid)
        { var p = Resolve(new PlayerPreferences { mouseSensitivity = invalid, masterVolume = invalid }); Assert.That(p.mouseSensitivity, Is.EqualTo(0.12f)); Assert.That(p.masterVolume, Is.EqualTo(0.8f)); }
        [TestCase(-5f, 0.01f, -3f, 0f)] [TestCase(5f, 1f, 4f, 1f)]
        public void FiniteOutOfRangeValuesAreClamped(float sensitivity, float expectedSensitivity, float volume, float expectedVolume)
        { var p = Resolve(new PlayerPreferences { mouseSensitivity = sensitivity, masterVolume = volume }); Assert.That(p.mouseSensitivity, Is.EqualTo(expectedSensitivity)); Assert.That(p.masterVolume, Is.EqualTo(expectedVolume)); }
        [TestCase(0)] [TestCase(2)]
        public void UnsupportedOrMissingVersionUsesDefaults(int version)
        { var p = Resolve(new PlayerPreferences { schemaVersion = version, mouseSensitivity = 0.9f, masterVolume = 0, qualityLevel = 2, qualityName = "High" }); Assert.That(p.mouseSensitivity, Is.EqualTo(0.12f)); Assert.That(p.masterVolume, Is.EqualTo(0.8f)); Assert.That(p.qualityLevel, Is.EqualTo(1)); }
        [Test]
        public void ReorderedQualityLevelsUseTheSavedName()
        { var p = Resolve(new PlayerPreferences { qualityLevel = 0, qualityName = "High" }, new[] { "Low", "Medium", "High" }); Assert.That(p.qualityLevel, Is.EqualTo(2)); }
        [Test]
        public void RemovedQualityPresetFallsBackToTheCurrentProjectDefault()
        { var p = Resolve(new PlayerPreferences { qualityLevel = 2, qualityName = "Deleted" }); Assert.That(p.qualityLevel, Is.EqualTo(1)); Assert.That(p.qualityName, Is.EqualTo("Medium")); }
        [Test]
        public void EmptyQualityListIsSafeAndCopyingCannotMutateActivePreferences()
        {
            var d = Defaults(); var saved = new PlayerPreferences { qualityLevel = 99 };
            var p = PlayerPreferencesPolicy.Resolve(saved, d, Array.Empty<string>(), 0.01f, 1);
            Assert.That(p.qualityLevel, Is.EqualTo(-1)); Assert.That(p.qualityName, Is.Empty);
            var copy = p.Copy(); copy.mouseSensitivity = 0.7f;
            Assert.That(p.mouseSensitivity, Is.EqualTo(0.12f)); Assert.That(d.qualityLevel, Is.EqualTo(1)); Assert.That(saved.qualityLevel, Is.EqualTo(99));
        }
        [TestCase(0f, 1f)] [TestCase(1f, 1f)] [TestCase(1f, 0.5f)] [TestCase(float.NaN, 1f)]
        public void SensitivityBoundsMustBeFinitePositiveAndOrdered(float minimum, float maximum)
        { Assert.Throws<ArgumentOutOfRangeException>(() => PlayerPreferencesPolicy.Resolve(null, Defaults(), Names, minimum, maximum)); }
        private static void WithFile(Action<string> body)
        {
            string folder = Path.Combine(Path.GetTempPath(), "MechaBattleSettings-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder); string file = Path.Combine(folder, "settings.json");
            try { body(file); }
            finally { foreach (var suffix in new[] { "", ".bak", ".tmp" }) if (File.Exists(file + suffix)) File.Delete(file + suffix); Directory.Delete(folder, false); }
        }
        [Test]
        public void AtomicSaveCanBeReadAndReplacementPreservesThePreviousFile()
        {
            WithFile(file =>
            {
                Assert.That(PlayerPreferencesStore.TryWrite(file, "{\"schemaVersion\":1,\"masterVolume\":0.4}", out var error), Is.True, error);
                Assert.That(PlayerPreferencesStore.TryRead(file, out var first, out error), Is.True, error);
                Assert.That(first, Does.Contain("0.4"));
                Assert.That(PlayerPreferencesStore.TryWrite(file, "{\"schemaVersion\":1,\"masterVolume\":0.7}", out error), Is.True, error);
                Assert.That(File.ReadAllText(file + ".bak"), Is.EqualTo(first)); Assert.That(File.Exists(file + ".tmp"), Is.False);
                Assert.That(File.ReadAllText(file), Does.Contain("0.7"));
            });
        }
        [Test]
        public void SaveFailureReturnsAnErrorAndDoesNotDestroyTheExistingFile()
        {
            WithFile(file =>
            {
                File.WriteAllText(file, "existing settings");
                Assert.That(PlayerPreferencesStore.TryWrite(Path.Combine(file, "child.json"), "{}", out var error), Is.False);
                Assert.That(error, Is.Not.Empty); Assert.That(File.ReadAllText(file), Is.EqualTo("existing settings"));
            });
        }
        [Test]
        public void MissingSavePathFailsBeforeAnyTemporaryPathIsCreated()
        { Assert.That(PlayerPreferencesStore.TryWrite(null, "{}", out var error), Is.False); Assert.That(error, Is.Not.Empty); }
        [Test]
        public void MissingFileIsANormalFirstRunAndEmptySaveCannotOverwriteValidData()
        {
            WithFile(file =>
            {
                Assert.That(PlayerPreferencesStore.TryRead(file, out var json, out var error), Is.False); Assert.That(json, Is.Null); Assert.That(error, Is.Empty);
                File.WriteAllText(file, "previous"); Assert.That(PlayerPreferencesStore.TryWrite(file, "", out error), Is.False);
                Assert.That(File.ReadAllText(file), Is.EqualTo("previous"));
            });
        }
        [Test] public void LegacySettingsKeepTheConfiguredBotDefaultsAndNewMixRoundTrips()
        {
            var defaults=Defaults();defaults.botRadarFraction=.06f;defaults.botInterceptorFraction=.1f;defaults.botMapReaderFraction=.25f;
            var legacy=PlayerPreferencesPolicy.Resolve(new PlayerPreferences{botRadarFraction=.9f},defaults,Names,.01f,1);
            Assert.That(legacy.botRadarFraction,Is.EqualTo(.06f));Assert.That(legacy.botInterceptorFraction,Is.EqualTo(.1f));
            var saved=legacy.Copy();saved.botMixConfigured=true;saved.botRadarFraction=.1f;saved.botInterceptorFraction=.2f;saved.botMapReaderFraction=.6f;
            var resolved=Resolve(saved);Assert.That(resolved.botRadarFraction,Is.EqualTo(.1f));Assert.That(resolved.botInterceptorFraction,Is.EqualTo(.2f));Assert.That(resolved.botMapReaderFraction,Is.EqualTo(.6f));Assert.That(legacy.botRadarFraction,Is.EqualTo(.06f));
        }
        [Test] public void MalformedBotMixIsFiniteBoundedAndCannotExceedOneTeam()
        {
            var p=Resolve(new PlayerPreferences{botMixConfigured=true,botRadarFraction=.9f,botInterceptorFraction=.9f,botMapReaderFraction=float.NaN});
            Assert.That(p.botRadarFraction+p.botInterceptorFraction,Is.EqualTo(1).Within(.00001));Assert.That(p.botMapReaderFraction,Is.EqualTo(.35f));
            p=Resolve(new PlayerPreferences{botMixConfigured=true,botRadarFraction=float.PositiveInfinity,botInterceptorFraction=-2,botMapReaderFraction=2});
            Assert.That(p.botRadarFraction,Is.EqualTo(.04f));Assert.That(p.botInterceptorFraction,Is.Zero);Assert.That(p.botMapReaderFraction,Is.EqualTo(1));
        }
    }
}
