using MechaBattle.Contracts.Settings;
using MechaBattle.Settings;
using System;
using System.IO;
using UnityEngine;

namespace MechaBattle.Playable
{
    public sealed partial class PlayableEntry
    {
        private PlayerPreferences playerPreferences, defaultPlayerPreferences;
        private string[] qualityNames;
        private int initialQuality;
        private float initialMasterVolume;
        private bool enginePreferencesCaptured;
        public bool IsSettingsOpen => ui != null && ui.Settings.IsOpen;
        public PlayerPreferences Preferences => playerPreferences.Copy();
        private string PreferencesPath => Path.Combine(Application.persistentDataPath, "mechabattle-settings-v1.json");

        private PlayerPreferences ResolvePreferences(PlayerPreferences saved) => PlayerPreferencesPolicy.Resolve(saved, defaultPlayerPreferences, qualityNames,
            configuration.playablePrototype.settingsMinimumSensitivity, configuration.playablePrototype.settingsMaximumSensitivity);
        private void LoadPlayerPreferences()
        {
            qualityNames = QualitySettings.names; initialQuality = QualitySettings.GetQualityLevel(); initialMasterVolume = AudioListener.volume;
            enginePreferencesCaptured = true;
            var mix=configuration.p0Simulation.botBehavior??new MechaBattle.Contracts.BotBehaviorRules();
            defaultPlayerPreferences = new PlayerPreferences { mouseSensitivity = configuration.playablePrototype.mouseSensitivity, masterVolume = initialMasterVolume, qualityLevel = initialQuality, botMixConfigured=true,botRadarFraction=mix.radarFraction,botInterceptorFraction=mix.interceptorFraction,botMapReaderFraction=mix.frontlineMapReaderFraction };
            defaultPlayerPreferences = ResolvePreferences(null); playerPreferences = defaultPlayerPreferences.Copy();
            if (persistProfile)
            {
                bool read = PlayerPreferencesStore.TryRead(PreferencesPath, out var json, out var error);
                if (read && TryParsePreferences(json, out var loaded)) playerPreferences = loaded;
                else if (PlayerPreferencesStore.TryRead(PreferencesPath + ".bak", out var backup, out var ignored) && TryParsePreferences(backup, out loaded))
                { playerPreferences = loaded; notice = "設定のバックアップを読み込みました。"; }
                else if (read || !string.IsNullOrEmpty(error)) notice = "設定を読み込めないため初期設定を使用します。";
            }
            ApplyRuntimePreferences(playerPreferences);
        }
        private bool TryParsePreferences(string json, out PlayerPreferences loaded)
        {
            loaded = null;
            if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{", StringComparison.Ordinal)) return false;
            try
            {
                var candidate = defaultPlayerPreferences.Copy(); candidate.schemaVersion = 0; candidate.qualityName = "";
                // Missing fields keep the current defaults; a missing/unsupported schema is rejected.
                JsonUtility.FromJsonOverwrite(json, candidate);
                if (candidate.schemaVersion != 1) return false;
                loaded = ResolvePreferences(candidate); return true;
            }
            catch (ArgumentException) { return false; }
        }
        private void ApplyRuntimePreferences(PlayerPreferences value)
        {
            configuration.playablePrototype.mouseSensitivity = value.mouseSensitivity;
            AudioListener.volume = value.masterVolume;
            if (value.qualityLevel >= 0 && value.qualityLevel != QualitySettings.GetQualityLevel()) QualitySettings.SetQualityLevel(value.qualityLevel, true);
        }
        public void OpenSettings()
        {
            if (ui == null || IsSettingsOpen) return;
            if (Simulation != null && Simulation.Phase == MechaBattle.Simulation.MatchPhase.Running && !IsPaused) PauseMatch();
            ResetInput(); inputArmed = false; SetCursor(false);
            ui.Settings.Open(playerPreferences, defaultPlayerPreferences, qualityNames,
                configuration.playablePrototype.settingsMinimumSensitivity, configuration.playablePrototype.settingsMaximumSensitivity);
        }
        public void CancelSettings()
        {
            if (!IsSettingsOpen) return;
            ui.Settings.Close(); ResetInput(); inputArmed = false;
            SetCursor(false); // Return to the originating frontend/pause screen; never resume combat here.
        }
        public bool ApplyPlayerPreferences(PlayerPreferences requested)
        {
            if (requested == null) throw new ArgumentNullException(nameof(requested));
            var candidate = ResolvePreferences(requested);
            if (persistProfile && !PlayerPreferencesStore.TryWrite(PreferencesPath, JsonUtility.ToJson(candidate, true), out var error))
            {
                if (ui != null) ui.Settings.ShowError("保存できませんでした。現在の設定は変更していません。\n" + error);
                return false;
            }
            playerPreferences = candidate; ApplyRuntimePreferences(candidate);
            CancelSettings(); return true;
        }
        private void RestoreEnginePreferences()
        {
            if (!enginePreferencesCaptured) return;
            AudioListener.volume = initialMasterVolume;
            if (initialQuality >= 0 && initialQuality < QualitySettings.names.Length && QualitySettings.GetQualityLevel() != initialQuality)
                QualitySettings.SetQualityLevel(initialQuality, true);
            enginePreferencesCaptured = false;
        }
    }
}
