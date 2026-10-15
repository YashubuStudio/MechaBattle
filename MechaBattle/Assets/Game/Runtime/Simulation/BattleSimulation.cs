using System;
using MechaBattle.Contracts;
using MechaBattle.Battlefield;

namespace MechaBattle.Simulation
{
    public sealed class BattleSimulation
    {
        private sealed class UnitState
        {
            public int Id, Team, Deck = -1, Ammo;
            public bool Alive, HasCommand, HasSequence;
            public uint Sequence;
            public float X, Z, Health, Shield, AmmoFraction;
            public long NextRespawn, NextShot;
            public long[] DeckReady;
            public UnitCommand Command;
        }
        private sealed class BaseState
        {
            public string Id;
            public int Team;
            public float X, Z, SpawnRadius, AttackRadius;
            public double Damage;
            public long NextCounterShot;
        }
        private sealed class CaptureState
        {
            public string Id;
            public float X, Z, Radius, Progress;
            public int Owner = -1, Blue, Orange;
        }

        private readonly SimulationConfig config;
        private readonly MatchRoster roster;
        private readonly UnitState[] units = new UnitState[MatchLimits.TotalControllerSlots];
        private readonly BaseState[] bases;
        private readonly CaptureState[] captures;
        private readonly int[][] teamBaseIndices;
        private readonly double[] unitDamage = new double[MatchLimits.TotalControllerSlots];
        private readonly double[] baseDamage;
        private readonly double[] gauges = new double[MatchLimits.TeamCount];
        private readonly float halfWidth, halfDepth;
        private readonly long durationTicks, shotIntervalTicks, respawnDelayTicks, baseCounterFireIntervalTicks;
        private readonly long[] cooldownTicks;
        private int nextUnitId;
        private long shotsFired, unitsDestroyed, unitsSpawned, baseHits, captureChanges, baseCounterShotsFired;

        public long CurrentTick { get; private set; }
        public float FixedDeltaTime { get; }
        public MatchPhase Phase { get; private set; }
        public int WinnerTeam { get; private set; } = -1;
        public int AliveCount { get; private set; }
        public int MainBaseCount => bases.Length;
        public int CapturePointCount => captures.Length;
        public int DeckCount => cooldownTicks.Length;
        public SimulationCounters Counters => new SimulationCounters(shotsFired, unitsDestroyed, unitsSpawned, baseHits, captureChanges, baseCounterShotsFired);

        public BattleSimulation(MapDefinition map, SimulationConfig config, int humanSlotId = 0)
        {
            MapValidator.ThrowIfInvalid(map);
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.Validate();
            this.config = config.Copy();
            roster = new MatchRoster(humanSlotId);
            FixedDeltaTime = 1f / config.tickHz;
            durationTicks = SecondsToTicks(config.maxDurationSeconds);
            shotIntervalTicks = SecondsToTicks(config.fireIntervalSeconds);
            respawnDelayTicks = SecondsToTicks(config.respawnDelaySeconds);
            baseCounterFireIntervalTicks = config.baseCounterFireIntervalSeconds > 0
                ? SecondsToTicks(config.baseCounterFireIntervalSeconds) : 0;
            cooldownTicks = new long[config.deckCosts.Length];
            for (int i = 0; i < cooldownTicks.Length; i++) cooldownTicks[i] = SecondsToTicks(config.deckCooldownSeconds[i]);
            halfWidth = map.halfWidth; halfDepth = map.halfDepth;
            if (config.unitRadius >= halfWidth || config.unitRadius >= halfDepth)
                throw new ArgumentException("Unit radius must fit inside the map.");
            bases = new BaseState[map.mainBases.Length];
            baseDamage = new double[bases.Length];
            var counts = new int[MatchLimits.TeamCount];
            for (int i = 0; i < bases.Length; i++)
            {
                var source = map.mainBases[i];
                bases[i] = new BaseState { Id = source.id, Team = source.team, X = source.x,
                    Z = source.z, SpawnRadius = source.spawnRadius, AttackRadius = source.attackRadius };
                counts[source.team]++;
            }
            teamBaseIndices = new int[MatchLimits.TeamCount][];
            for (int t = 0; t < MatchLimits.TeamCount; t++)
            { teamBaseIndices[t] = new int[counts[t]]; counts[t] = 0; gauges[t] = config.initialGauge; }
            for (int i = 0; i < bases.Length; i++) teamBaseIndices[bases[i].Team][counts[bases[i].Team]++] = i;
            captures = new CaptureState[map.capturePoints.Length];
            for (int i = 0; i < captures.Length; i++)
            {
                var source = map.capturePoints[i];
                captures[i] = new CaptureState { Id = source.id, X = source.x, Z = source.z, Radius = source.radius };
            }
            for (int i = 0; i < units.Length; i++)
            {
                units[i] = new UnitState { Team = roster.Controllers[i].Team, DeckReady = new long[cooldownTicks.Length] };
                TrySpawn(i);
            }
        }

        private long SecondsToTicks(float seconds)
        {
            double scaled = (double)seconds * config.tickHz;
            // Avoid turning an exact design interval such as 0.4s * 50Hz into
            // 21 ticks because its float representation is slightly above 20.
            return Math.Max(1L, (long)Math.Ceiling(scaled - Math.Min(0.0001, scaled * 0.0000001)));
        }
        private static void CheckTeam(int team)
        { if (team < 0 || team >= MatchLimits.TeamCount) throw new ArgumentOutOfRangeException(nameof(team)); }
        private UnitState Unit(int slot)
        { if (slot < 0 || slot >= units.Length) throw new ArgumentOutOfRangeException(nameof(slot)); return units[slot]; }
        public double GetGauge(int team) { CheckTeam(team); return gauges[team]; }
        public long GetDeckReadyTick(int slot, int deck)
        {
            if (deck < 0 || deck >= cooldownTicks.Length) throw new ArgumentOutOfRangeException(nameof(deck));
            return Unit(slot).DeckReady[deck];
        }
        public UnitSnapshot GetUnit(int slotId)
        {
            var u = Unit(slotId);
            return new UnitSnapshot(slotId, u.Id, u.Team, roster.Controllers[slotId].Controller,
                u.Alive, u.Deck, u.X, u.Z, u.Health, u.Shield, u.Ammo);
        }
        public MainBaseSnapshot GetBase(int index)
        {
            if (index < 0 || index >= bases.Length) throw new ArgumentOutOfRangeException(nameof(index));
            var b = bases[index]; return new MainBaseSnapshot(b.Id, b.Team, b.X, b.Z, b.AttackRadius, b.Damage);
        }
        public CaptureSnapshot GetCapturePoint(int index)
        {
            if (index < 0 || index >= captures.Length) throw new ArgumentOutOfRangeException(nameof(index));
            var c = captures[index]; return new CaptureSnapshot(c.Id, c.X, c.Z, c.Radius, c.Owner, c.Progress, c.Blue, c.Orange);
        }
        public bool CanSpawnAtCapture(int team, int captureIndex)
        {
            CheckTeam(team);
            if (captureIndex < 0 || captureIndex >= captures.Length) throw new ArgumentOutOfRangeException(nameof(captureIndex));
            var c = captures[captureIndex];
            return c.Owner == team && (team == 0 ? c.Orange : c.Blue) == 0;
        }
        public float GetRedLockRange(int attackerSlot, int targetSlot)
        {
            var a = Unit(attackerSlot); var target = Unit(targetSlot);
            return roster.Controllers[attackerSlot].Controller == ControllerType.Bot &&
                roster.Controllers[targetSlot].Controller == ControllerType.Human
                ? config.redLockRange * config.botToHumanRedLockMultiplier : config.redLockRange;
        }
        public bool IsRedLocked(int attackerSlot, int targetSlot)
        {
            var a = Unit(attackerSlot); var target = Unit(targetSlot);
            double range = GetRedLockRange(attackerSlot, targetSlot);
            return a.Alive && target.Alive && a.Team != target.Team && DistanceSquared(a.X, a.Z, target.X, target.Z) <= range * range;
        }

        public bool Submit(int slotId, UnitCommand command)
        {
            if (Phase != MatchPhase.Running || slotId < 0 || slotId >= units.Length) return false;
            var u = units[slotId];
            if (!u.Alive || command.UnitId != u.Id || command.Tick != CurrentTick ||
                (u.HasSequence && unchecked((int)(command.Sequence - u.Sequence)) <= 0)) return false;
            if (!SimulationConfig.IsFinite(command.MoveX) || !SimulationConfig.IsFinite(command.MoveY) ||
                !SimulationConfig.IsFinite(command.AimYaw) || !SimulationConfig.IsFinite(command.AimPitch) ||
                Math.Abs(command.MoveX) > 1f || Math.Abs(command.MoveY) > 1f) return false;
            u.Command = command; u.Sequence = command.Sequence; u.HasSequence = true; u.HasCommand = true;
            return true;
        }

        public void Step()
        {
            if (Phase != MatchPhase.Running) return;
            Array.Clear(unitDamage, 0, unitDamage.Length); Array.Clear(baseDamage, 0, baseDamage.Length);
            for (int i = 0; i < units.Length; i++) if (!units[i].Alive) TrySpawn(i);
            for (int i = 0; i < units.Length; i++) Move(units[i]);
            // Gather all shot intents before resolving any lethal damage.
            for (int i = 0; i < units.Length; i++) Fire(units[i]);
            ApplyBaseCounterFire();
            for (int i = 0; i < units.Length; i++) ResolveDamage(i);
            for (int i = 0; i < bases.Length; i++)
            {
                bases[i].Damage += baseDamage[i];
                gauges[bases[i].Team] = Math.Max(0, gauges[bases[i].Team] - baseDamage[i] * config.baseGaugeDamageFactor);
            }
            UpdateCapture();
            for (int i = 0; i < units.Length; i++) { Supply(units[i]); units[i].HasCommand = false; }
            CurrentTick++;
            if (gauges[0] <= 0 || gauges[1] <= 0 || CurrentTick >= durationTicks)
            {
                Phase = MatchPhase.Finished;
                WinnerTeam = gauges[0] == gauges[1] ? -1 : gauges[0] > gauges[1] ? 0 : 1;
            }
        }

        private void Move(UnitState u)
        {
            if (!u.Alive || !u.HasCommand) return;
            double x = u.Command.MoveX, z = u.Command.MoveY;
            double length = Math.Sqrt(x * x + z * z);
            if (length > 1) { x /= length; z /= length; }
            u.X = Clamp((float)(u.X + x * config.moveSpeed * FixedDeltaTime), -halfWidth + config.unitRadius, halfWidth - config.unitRadius);
            u.Z = Clamp((float)(u.Z + z * config.moveSpeed * FixedDeltaTime), -halfDepth + config.unitRadius, halfDepth - config.unitRadius);
        }

        private void Fire(UnitState attacker)
        {
            if (!attacker.Alive || !attacker.HasCommand || !attacker.Command.FirePrimary || attacker.Ammo <= 0 || CurrentTick < attacker.NextShot) return;
            attacker.Ammo--; attacker.NextShot = CurrentTick + shotIntervalTicks; shotsFired++;
            double yaw = attacker.Command.AimYaw % 360f * Math.PI / 180;
            double dx = Math.Sin(yaw), dz = Math.Cos(yaw), nearest = config.fireRange;
            int targetUnit = -1, targetBase = -1;
            for (int i = 0; i < units.Length; i++)
            {
                var u = units[i]; if (!u.Alive || u.Team == attacker.Team) continue;
                double hit = RayCircle(attacker.X, attacker.Z, dx, dz, u.X, u.Z, config.unitRadius);
                if (hit <= nearest) { nearest = hit; targetUnit = i; }
            }
            for (int i = 0; i < bases.Length; i++)
            {
                var b = bases[i]; if (b.Team == attacker.Team) continue;
                double hit = RayCircle(attacker.X, attacker.Z, dx, dz, b.X, b.Z, b.AttackRadius);
                if (hit < nearest) { nearest = hit; targetBase = i; targetUnit = -1; }
            }
            if (targetUnit >= 0) unitDamage[targetUnit] += config.fireDamage;
            else if (targetBase >= 0) { baseDamage[targetBase] += config.fireDamage; baseHits++; }
        }

        // Main-base defensive weapon, NOT a per-unit auto-counter (AGENTS.md prohibits
        // automatic melee retaliation/guard-interrupt between mobile units; this is a
        // static structure's own slow-firing area weapon, independent of whether the
        // base itself was hit). Fires at most once per baseCounterFireIntervalTicks, and
        // only once an enemy is actually within range - an idle base keeps waiting
        // rather than firing at nothing. Hits every enemy unit in range simultaneously
        // (area attack), folded into the same unitDamage[] buffer Fire() uses so it
        // resolves fairly alongside ordinary unit fire in ResolveDamage().
        private void ApplyBaseCounterFire()
        {
            if (baseCounterFireIntervalTicks <= 0) return;
            double radiusSquared = (double)config.baseCounterFireRadius * config.baseCounterFireRadius;
            for (int b = 0; b < bases.Length; b++)
            {
                var baseState = bases[b];
                if (CurrentTick < baseState.NextCounterShot) continue;
                bool fired = false;
                for (int i = 0; i < units.Length; i++)
                {
                    var u = units[i];
                    if (!u.Alive || u.Team == baseState.Team) continue;
                    if (DistanceSquared(u.X, u.Z, baseState.X, baseState.Z) > radiusSquared) continue;
                    unitDamage[i] += config.baseCounterFireDamage;
                    fired = true;
                }
                if (fired) { baseState.NextCounterShot = CurrentTick + baseCounterFireIntervalTicks; baseCounterShotsFired++; }
            }
        }

        private void ResolveDamage(int slot)
        {
            var u = units[slot]; if (!u.Alive || unitDamage[slot] <= 0) return;
            double damage = unitDamage[slot];
            double absorbed = Math.Min(u.Shield, damage);
            u.Shield -= (float)absorbed; damage -= absorbed;
            u.Health = (float)Math.Max(0, u.Health - damage);
            if (u.Health > 0) return;
            u.Alive = false; AliveCount--; unitsDestroyed++;
            u.DeckReady[u.Deck] = CurrentTick + cooldownTicks[u.Deck];
            u.NextRespawn = CurrentTick + respawnDelayTicks;
            gauges[u.Team] = Math.Max(0, gauges[u.Team] - config.deckCosts[u.Deck] * (double)config.unitGaugeLossPerCost);
        }

        private void TrySpawn(int slot)
        {
            var u = units[slot]; if (u.Alive || CurrentTick < u.NextRespawn) return;
            int deck = -1;
            for (int i = 1; i <= cooldownTicks.Length; i++)
            {
                int candidate = (u.Deck + i) % cooldownTicks.Length;
                if (u.DeckReady[candidate] <= CurrentTick) { deck = candidate; break; }
            }
            if (deck < 0) return;
            var indices = teamBaseIndices[u.Team];
            int local = slot % MatchLimits.ControllerSlotsPerTeam;
            var home = bases[indices[local % indices.Length]];
            float x = home.X, z = home.Z, radius = home.SpawnRadius;
            double bestScore = double.PositiveInfinity;
            for (int i = 0; i < captures.Length; i++)
            {
                if (!CanSpawnAtCapture(u.Team, i)) continue;
                var c = captures[i];
                // Public objective geometry only; no enemy-unit truth in spawn selection.
                double goalX = u.Team == 0 ? halfWidth : -halfWidth;
                double score = DistanceSquared(c.X, c.Z, goalX, home.Z);
                if (score < bestScore) { bestScore = score; x = c.X; z = c.Z; radius = c.Radius; }
            }
            int ordinal = local / indices.Length;
            int perBase = (MatchLimits.ControllerSlotsPerTeam + indices.Length - 1) / indices.Length;
            double scatter = radius * config.spawnScatterFraction * Math.Sqrt((ordinal + 0.5) / perBase);
            double angle = ordinal * 2.399963229728653;
            u.X = Clamp((float)(x + Math.Cos(angle) * scatter), -halfWidth + config.unitRadius, halfWidth - config.unitRadius);
            u.Z = Clamp((float)(z + Math.Sin(angle) * scatter), -halfDepth + config.unitRadius, halfDepth - config.unitRadius);
            u.Deck = deck; u.Id = checked(++nextUnitId); u.Alive = true;
            u.Health = config.maxHealth; u.Shield = config.maxShield;
            u.Ammo = config.maxAmmo; u.AmmoFraction = 0; u.NextShot = CurrentTick;
            u.HasCommand = false; u.HasSequence = false;
            AliveCount++; unitsSpawned++;
        }

        private void UpdateCapture()
        {
            for (int i = 0; i < captures.Length; i++)
            {
                var c = captures[i]; c.Blue = 0; c.Orange = 0;
                double radiusSquared = (double)c.Radius * c.Radius;
                for (int slot = 0; slot < units.Length; slot++)
                {
                    var u = units[slot]; if (!u.Alive || DistanceSquared(u.X, u.Z, c.X, c.Z) > radiusSquared) continue;
                    if (u.Team == 0) c.Blue++; else c.Orange++;
                }
                int advantage = c.Blue - c.Orange;
                int oldOwner = c.Owner;
                if (advantage != 0)
                {
                    float delta = Math.Sign(advantage) * Math.Min(Math.Abs(advantage), config.capturePresenceCap) * FixedDeltaTime / config.captureSecondsPerAdvantage;
                    c.Progress = Clamp(c.Progress + delta, -1, 1);
                    if ((c.Owner == 0 && c.Progress <= 0) || (c.Owner == 1 && c.Progress >= 0)) c.Owner = -1;
                    if (c.Progress >= 1) c.Owner = 0;
                    else if (c.Progress <= -1) c.Owner = 1;
                }
                if (c.Owner != oldOwner) captureChanges++;
            }
        }

        private void Supply(UnitState u)
        {
            if (!u.Alive) return;
            bool available = false;
            var homeIndices = teamBaseIndices[u.Team];
            double radius = config.homeSupplyRadius;
            for (int i = 0; i < homeIndices.Length; i++)
            {
                var b = bases[homeIndices[i]];
                if (DistanceSquared(u.X, u.Z, b.X, b.Z) <= radius * radius) { available = true; break; }
            }
            if (!available) return;
            u.Health = Math.Min(config.maxHealth, u.Health + config.homeHealthPerSecond * FixedDeltaTime);
            u.Shield = Math.Min(config.maxShield, u.Shield + config.homeShieldPerSecond * FixedDeltaTime);
            if (u.Ammo < config.maxAmmo)
            {
                u.AmmoFraction += config.homeAmmoPerSecond * FixedDeltaTime;
                int restored = (int)Math.Min(config.maxAmmo - u.Ammo, Math.Floor(u.AmmoFraction));
                u.Ammo += restored; u.AmmoFraction -= restored;
            }
            if (u.Ammo >= config.maxAmmo) u.AmmoFraction = 0;
        }

        private static float Clamp(float v, float min, float max) { return Math.Max(min, Math.Min(max, v)); }
        private static double DistanceSquared(float x, float z, double otherX, double otherZ)
        { double dx = x - otherX, dz = z - otherZ; return dx * dx + dz * dz; }
        private static double RayCircle(float x, float z, double dx, double dz, float cx, float cz, float radius)
        {
            double ox = cx - (double)x, oz = cz - (double)z;
            double radiusSquared = (double)radius * radius;
            double distanceSquared = ox * ox + oz * oz;
            if (distanceSquared <= radiusSquared) return 0;
            double projection = ox * dx + oz * dz;
            if (projection < 0) return double.PositiveInfinity;
            double perpendicularSquared = Math.Max(0, distanceSquared - projection * projection);
            if (perpendicularSquared > radiusSquared) return double.PositiveInfinity;
            return projection - Math.Sqrt(radiusSquared - perpendicularSquared);
        }
    }
}
