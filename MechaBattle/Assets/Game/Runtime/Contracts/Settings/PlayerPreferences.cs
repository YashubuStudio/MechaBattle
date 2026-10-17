using System;
namespace MechaBattle.Contracts.Settings
{
    [Serializable]
    public sealed class PlayerPreferences
    {
        public int schemaVersion = 1;
        public float mouseSensitivity = 0.12f, masterVolume = 1;
        public int qualityLevel = -1;
        public string qualityName = "";
        public bool botMixConfigured;
        public float botRadarFraction=.04f,botInterceptorFraction=.08f,botMapReaderFraction=.35f;
        public PlayerPreferences Copy() => (PlayerPreferences)MemberwiseClone();
    }
}
