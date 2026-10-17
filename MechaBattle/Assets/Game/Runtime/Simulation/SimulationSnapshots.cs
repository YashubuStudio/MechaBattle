using MechaBattle.Contracts;

namespace MechaBattle.Simulation
{
    public enum MatchPhase { Running, Finished }

    // User request 2026-10-16: scoreboard. Lifetime (whole-match, survives respawn) stats for
    // one controller slot. Presentation-only, like UnitSnapshot - never fed to AI.Bots/Observation.
    public readonly struct SlotStatsSnapshot
    {
        public readonly int Slot, Team, Kills, Deaths;
        public readonly ControllerType Controller;
        // Damage counts effective unit HP + shield loss; core damage is separate.
        // Healing counts actual HP restored; received healing includes home supply/self pods.
        public readonly double DamageDealt, CoreDamageDealt, DamageReceived, HealingDealt, HealingReceived;
        internal SlotStatsSnapshot(int slot, int team, ControllerType controller, int kills, int deaths,
            double damageDealt, double coreDamageDealt, double damageReceived, double healingDealt, double healingReceived)
        {
            Slot = slot; Team = team; Controller = controller; Kills = kills; Deaths = deaths;
            DamageDealt = damageDealt; CoreDamageDealt = coreDamageDealt; DamageReceived = damageReceived;
            HealingDealt = healingDealt; HealingReceived = healingReceived;
        }
    }

    // Copy-only truth views for presentation/observation/tests. AI must receive a
    // filtered observation view, never this world's unrestricted accessor.
    public readonly struct UnitSnapshot
    {
        public readonly int SlotId, UnitId, Team, DeckIndex, Ammo;
        public readonly ControllerType Controller;
        public readonly bool IsAlive;
        public readonly float Y, VerticalVelocity;
        public readonly bool Grounded, BoostOverheated, BoostMoving, Crouching, ReturnChanneling, CarryingNuclear;
        public readonly float X, Z, Health, Shield, Boost, AimYaw, AimPitch, ShotEndX, ShotEndY, ShotEndZ;
        public readonly long LastShotTick, LastMeleeTick, LastShieldBlockTick;
        public readonly int Magazine, ComboStep, ActiveWeaponIndex, WeaponCount;
        // Own warning state; attacker information is deliberately absent.
        public readonly int IncomingLockCount;
        public readonly long LastDamageTick, LastLockedHitTick;
        public readonly bool Guarding, BeamActive;
        public readonly float ReloadSeconds, ChargeFraction, MeleeChargeFraction, DownPoints, DownPointCapacity, DisruptionSeconds, MeleePhase;
        public readonly MeleeStyle MeleeStyle;
        public readonly DisruptionState Disruption;
        internal UnitSnapshot(int slot, int unit, int team, ControllerType controller,
            bool alive, int deck, float x, float z, float health, float shield, int ammo, float boost, float aimYaw, long lastShotTick, float aimPitch, float shotEndX, float shotEndY, float shotEndZ, int magazine, float reloadSeconds, float chargeFraction, bool beamActive,
            long meleeTick, MeleeStyle meleeStyle, int comboStep, float meleeCharge, float meleePhase, bool guarding, long shieldBlockTick,
            float downPoints, float downCapacity, DisruptionState disruption, float disruptionSeconds, float y, float verticalVelocity, bool grounded, int activeWeaponIndex, int weaponCount, int incomingLockCount, long lastDamageTick, long lastLockedHitTick, bool boostOverheated, bool boostMoving, bool crouching=false, bool returnChanneling=false, bool carryingNuclear=false)
        {
            BoostOverheated=boostOverheated;BoostMoving=boostMoving;Crouching=crouching;ReturnChanneling=returnChanneling;CarryingNuclear=carryingNuclear;
            ActiveWeaponIndex = activeWeaponIndex; WeaponCount = weaponCount;
            IncomingLockCount = incomingLockCount; LastDamageTick = lastDamageTick; LastLockedHitTick = lastLockedHitTick;
            Y = y; VerticalVelocity = verticalVelocity; Grounded = grounded;
            SlotId = slot; UnitId = unit; Team = team; Controller = controller;
            IsAlive = alive; DeckIndex = deck; X = x; Z = z;
            Health = health; Shield = shield; Ammo = ammo; Boost = boost; AimYaw = aimYaw;
            Magazine = magazine; ReloadSeconds = reloadSeconds; ChargeFraction = chargeFraction; BeamActive = beamActive;
            LastMeleeTick = meleeTick; MeleeStyle = meleeStyle; ComboStep = comboStep; MeleeChargeFraction = meleeCharge; MeleePhase = meleePhase;
            Guarding = guarding; LastShieldBlockTick = shieldBlockTick; DownPoints = downPoints; DownPointCapacity = downCapacity; Disruption = disruption; DisruptionSeconds = disruptionSeconds;
            AimPitch = aimPitch; ShotEndY = shotEndY; LastShotTick = lastShotTick; ShotEndX = shotEndX; ShotEndZ = shotEndZ;
        }
    }

    public readonly struct AimPoint
    {
        public readonly float X, Y, Z; public readonly int TargetSlot, CoreIndex, DeployableIndex, DeployableId;
        internal AimPoint(float x, float y, float z, int targetSlot, int coreIndex, int deployableIndex=-1, int deployableId=0) { X = x; Y = y; Z = z; TargetSlot = targetSlot; CoreIndex = coreIndex; DeployableIndex=deployableIndex;DeployableId=deployableId; }
    }
    public readonly struct ExplosionSnapshot
    {
        public readonly bool Valid; public readonly int Team; public readonly float X, Y, Z, Radius; public readonly long Tick;
        internal ExplosionSnapshot(bool valid, int team, float x, float y, float z, float radius, long tick) { Valid = valid; Team = team; X = x; Y = y; Z = z; Radius = radius; Tick = tick; }
    }
    public readonly struct DeployableSnapshot
    {
        public readonly bool Active, Armed; public readonly int Team, OwnerSlot, Id;
        public readonly float Health, MaxHealth, HitRadius, HitHeight; public readonly float X, Y, Z, Radius, SecondsLeft; public readonly WeaponKind Kind;
        internal DeployableSnapshot(bool active, int team, int owner, float x, float y, float z, float radius, WeaponKind kind, bool armed, float seconds, int id, float health, float maxHealth, float hitRadius, float hitHeight)
        { Id=id;Health=health;MaxHealth=maxHealth;HitRadius=hitRadius;HitHeight=hitHeight;Active = active; Team = team; OwnerSlot = owner; X = x; Y = y; Z = z; Radius = radius; Kind = kind; Armed = armed; SecondsLeft = seconds; }
    }
    public readonly struct ProjectileSnapshot
    {
        public readonly bool Active, Guided; public readonly WeaponKind Kind; public readonly int Team; public readonly float X, Y, Z, BlastRadius, Dx, Dy, Dz;
        internal ProjectileSnapshot(bool active, int team, float x, float y, float z, float radius, WeaponKind kind, bool guided, float dx=0, float dy=0, float dz=1) { Dx=dx;Dy=dy;Dz=dz;Kind = kind; Guided = guided; Active = active; Team = team; X = x; Y = y; Z = z; BlastRadius = radius; }
    }

    public readonly struct MainBaseSnapshot
    {
        public readonly string Id;
        public readonly int Team;
        public readonly float X,Y,Z,AttackRadius,BuildingRadius,Health,MaxHealth;public readonly string Name;public readonly bool Destroyed;
        public readonly long LastCounterShotTick;
        public readonly double DamageReceived;
        internal MainBaseSnapshot(string id, int team, float x, float z, float radius, double damage, float buildingRadius, long counterShot,float y=0,string name=null,float health=0,float maxHealth=0,bool destroyed=false)
        {Y=y;Name=name??id;Health=health;MaxHealth=maxHealth;Destroyed=destroyed; BuildingRadius = buildingRadius; LastCounterShotTick = counterShot; Id = id; Team = team; X = x; Z = z; AttackRadius = radius; DamageReceived = damage; }
    }

    public readonly struct CaptureSnapshot
    {
        public readonly string Id;
        public readonly int OwnerTeam, BluePresence, OrangePresence;
        public readonly float X,Y,Z,Radius,Progress;
        internal CaptureSnapshot(string id, float x, float z, float radius, int owner,
            float progress, int blue, int orange,float y=0)
        {Y=y; Id = id; X = x; Z = z; Radius = radius; OwnerTeam = owner;
          Progress = progress; BluePresence = blue; OrangePresence = orange; }
    }

    public readonly struct SimulationCounters
    {
        public readonly long ShotsFired, UnitsDestroyed, UnitsSpawned, BaseHits, CaptureChanges, BaseCounterShotsFired;
        internal SimulationCounters(long shots, long destroyed, long spawned, long baseHits, long captures, long baseCounterShots)
        { ShotsFired = shots; UnitsDestroyed = destroyed; UnitsSpawned = spawned;
          BaseHits = baseHits; CaptureChanges = captures; BaseCounterShotsFired = baseCounterShots; }
    }
}
