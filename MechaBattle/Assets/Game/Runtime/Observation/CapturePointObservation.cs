namespace MechaBattle.Observation
{
    // Capture-point ownership is battlefield/scoreboard-level public information (every
    // player's minimap/HUD already shows who owns each relay - see PlayableMinimap /
    // PlayableWorldView), not hidden enemy intel. Unlike INearbyThreatSensor this is NOT
    // bounded by weapon range: knowing "who owns Relay-North" is not the same as knowing
    // where an enemy unit is standing. New file (not an edit to SelfObservation.cs) to
    // avoid touching a contract file under concurrent FIELD-04 revision.
    public readonly struct CapturePointStatus
    {
        public readonly int OwnerTeam; // -1 = neutral/contested
        public readonly float X, Z;
        public CapturePointStatus(int ownerTeam, float x, float z) { OwnerTeam = ownerTeam; X = x; Z = z; }
    }

    // Public objective destruction state only; never an enemy-unit position source.
    public interface IMainBaseStatusSource
    {
        int MainBaseCount { get; }
        bool IsMainBaseDestroyed(int index);
    }

    public interface ICapturePointStatusSource
    {
        int CapturePointCount { get; }
        CapturePointStatus GetCapturePoint(int index);
    }
}
