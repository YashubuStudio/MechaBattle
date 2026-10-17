namespace MechaBattle.Simulation
{
    // Presentation-only read model. Never fed to bot observation/command sources.
    public readonly struct UnitHealthBarSnapshot
    {
        public readonly bool Visible, Friendly;
        public readonly int UnitId;
        public readonly float X, Y, Z, Health, MaxHealth, HealthAgeSeconds;
        internal UnitHealthBarSnapshot(bool friendly, int id, float x, float y, float z, float health, float maxHealth, float age)
        { Visible = true; Friendly = friendly; UnitId = id; X = x; Y = y; Z = z; Health = health; MaxHealth = maxHealth; HealthAgeSeconds = age; }
    }
    public sealed partial class BattleSimulation
    {
        public UnitHealthBarSnapshot GetUnitHealthBar(int viewer, int target)
        {
            var self = Unit(viewer); var u = Unit(target);
            if (!self.Alive || !u.Alive || viewer == target || !HasLineOfSight(viewer, target)) return default;
            if (self.Team == u.Team)
                return new UnitHealthBarSnapshot(true, u.Id, u.X, u.Y, u.Z, u.Health, u.Loadouts[u.Deck].MaxHealth, 0);
            var contact = GetTeamContact(self.Team, target);
            if (!contact.Valid || !contact.HasHealth) return default;
            return new UnitHealthBarSnapshot(false, contact.UnitId, contact.X, contact.Y, contact.Z, contact.Health, contact.MaxHealth, contact.HealthAgeSeconds);
        }
    }
}
