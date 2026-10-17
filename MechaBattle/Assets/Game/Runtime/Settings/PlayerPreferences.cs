using System;
using System.IO;
using MechaBattle.Contracts.Settings;
namespace MechaBattle.Settings
{
    public static class PlayerPreferencesPolicy
    {
        public static PlayerPreferences Resolve(PlayerPreferences saved, PlayerPreferences defaults, string[] qualityNames, float minimumSensitivity, float maximumSensitivity)
        {
            if (defaults == null) throw new ArgumentNullException(nameof(defaults));
            if (!Finite(minimumSensitivity) || !Finite(maximumSensitivity) || minimumSensitivity <= 0 || maximumSensitivity <= minimumSensitivity)
                throw new ArgumentOutOfRangeException(nameof(minimumSensitivity));
            var result = defaults.Copy(); result.schemaVersion = 1;
            result.mouseSensitivity = Clamp(Finite(defaults.mouseSensitivity) ? defaults.mouseSensitivity : minimumSensitivity, minimumSensitivity, maximumSensitivity);
            result.masterVolume = Clamp(Finite(defaults.masterVolume) ? defaults.masterVolume : 1, 0, 1);
            int count = qualityNames == null ? 0 : qualityNames.Length;
            result.qualityLevel = count == 0 ? -1 : Math.Max(0, Math.Min(count - 1, defaults.qualityLevel));
            if (saved != null && saved.schemaVersion == 1)
            {
                if (Finite(saved.mouseSensitivity)) result.mouseSensitivity = Clamp(saved.mouseSensitivity, minimumSensitivity, maximumSensitivity);
                if (Finite(saved.masterVolume)) result.masterVolume = Clamp(saved.masterVolume, 0, 1);
                // Prefer the stable preset name if a later build reorders quality levels.
                int named = count == 0 || string.IsNullOrEmpty(saved.qualityName) ? -1 : Array.IndexOf(qualityNames, saved.qualityName);
                if (named >= 0) result.qualityLevel = named;
                else if (string.IsNullOrEmpty(saved.qualityName) && saved.qualityLevel >= 0 && saved.qualityLevel < count) result.qualityLevel = saved.qualityLevel;
            }
            var mix = saved != null && saved.schemaVersion == 1 && saved.botMixConfigured ? saved : defaults;
            result.botRadarFraction=Clamp(Finite(mix.botRadarFraction)?mix.botRadarFraction:defaults.botRadarFraction,0,1);
            result.botInterceptorFraction=Clamp(Finite(mix.botInterceptorFraction)?mix.botInterceptorFraction:defaults.botInterceptorFraction,0,1);
            float total=result.botRadarFraction+result.botInterceptorFraction;if(total>1){result.botRadarFraction/=total;result.botInterceptorFraction/=total;}
            result.botMapReaderFraction=Clamp(Finite(mix.botMapReaderFraction)?mix.botMapReaderFraction:defaults.botMapReaderFraction,0,1);result.botMixConfigured=true;
            result.qualityName = result.qualityLevel >= 0 ? qualityNames[result.qualityLevel] : "";
            return result;
        }
        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        private static float Clamp(float value, float minimum, float maximum) => Math.Max(minimum, Math.Min(maximum, value));
    }

    // Serialization belongs to the Unity adapter; atomic file IO remains independently testable.
    public static class PlayerPreferencesStore
    {
        public static bool TryRead(string file, out string json, out string error)
        {
            json = null; error = "";
            try { if (!File.Exists(file)) return false; json = File.ReadAllText(file); return true; }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)
            { error = e.Message; return false; }
        }
        public static bool TryWrite(string file, string json, out string error)
        {
            error = ""; string temporary = null;
            try
            {
                if (string.IsNullOrWhiteSpace(file) || string.IsNullOrWhiteSpace(json)) throw new ArgumentException("A settings file and JSON are required.");
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file)));
                temporary = file + ".tmp"; File.WriteAllText(temporary, json);
                if (File.Exists(file)) File.Replace(temporary, file, file + ".bak"); else File.Move(temporary, file);
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)
            { error = e.Message; return false; }
            finally
            {
                try { if (temporary != null && File.Exists(temporary)) File.Delete(temporary); }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
            }
        }
    }
}
