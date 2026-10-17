using System;
using MechaBattle.Contracts;
using MechaBattle.Battlefield;

namespace MechaBattle.Simulation
{
    public sealed partial class BattleSimulation
    {
        private sealed class UnitState
        {
            public bool Returned, NuclearTriggerHeld, NuclearSuppressTrigger;
            public int CarriedNuclear=-1; public long ReturnUntil=-1, ActionFeedbackUntil; public string ActionFeedback="";
            public int Id, Team, Deck = -1, Ammo, PreferredDeck = -1, PreferredSpawn = -2, PreferredMainBase = -1;
            public bool BoostOverheated, BoostMoving, Crouching;
            public int LastSpawnMainBase=-1,BotReturnTrips;
            public float MeleeLungeRemaining, MeleeRecoverySeconds, MeleeBoostSpent;
            public bool MeleeHitAdjusted;
            // Set only by TryNormalMeleeWhileDisabled's exception swing, consumed by the one
            // Melee() hit-check it unlocks, then cleared - never left set across swings.
            public bool MeleeBypassingDisabled;
            public bool AutoRespawn = true, SpawnRequested, Grounded = true, JumpHeld;
            public float Y, VerticalVelocity;
            public float AerialOriginY, AerialVelocityX, AerialVelocityZ;
            public int Slot, ActiveWeaponIndex;
            public int[] WeaponAmmo, WeaponMagazines;
            public float[] WeaponAmmoFraction;
            public long[] WeaponNextShot, WeaponReloadReady, WeaponLastShot;
            public float AimStartYaw, AimStartPitch;
            public float Boost, AimYaw, AimPitch, ShotEndX, ShotEndY, ShotEndZ;
            public long LastShotTick = -1, NextMelee;
            public int IncomingLockCount;
            public long LastDamageTick = -1, LastLockedHitTick = -1;
            public int Magazine, ComboStep, DownStage;
            public bool TriggerHeld, MeleeHeld, Guarding, MeleePending, MeleeCharging;
            public long ReloadReady = -1, ChargeStart = -1, BeamUntil = -1, GuardUntil, DisabledUntil, LastImpact = -1;
            public long LastShieldBlock = -1, LastMelee = -1, MeleeHitTick, MeleeUntil;
            public float DownPoints, MeleePower = 1, MeleeYaw;
            // Position at windup start, before the lunge moves the attacker (task #38): a long
            // lunge (e.g. Dash's 5m) can carry the attacker past a target that was already
            // within range, leaving them behind the attacker's final facing cone. Melee()'s hit
            // check accepts either this origin or the final position so a genuinely-in-range
            // target at the moment of commitment is never lost purely to the attacker's own
            // follow-through.
            public float MeleeOriginX, MeleeOriginZ;
            public MeleeAttackDefinition MeleeAttack;
            public DisruptionState Disruption;
            public UnitLoadout[] Loadouts;
            public long[] ShotIntervals, Cooldowns;
            // Lifetime scoreboard stats (user request 2026-10-16): never reset by respawn/ResetCombat,
            // only at match construction. Presentation-only, never read by AI.Bots/Observation.
            public int Kills, Deaths;
            public double DamageDealt, CoreDamageDealt, DamageReceived, HealingDealt, HealingReceived;
            public bool Alive, HasCommand, HasSequence;
            public uint Sequence;
            public float X, Z, Health, Shield, AmmoFraction;
            public long NextRespawn, NextShot;
            public long[] DeckReady;
            public UnitCommand Command;
        }
        private sealed class BaseState
        {
            public string Id,Name;
            public int Team;
            public float X,Y,Z,SpawnX,SpawnY,SpawnZ,SpawnRadius,AttackRadius,BuildingRadius,Health,MaxHealth;
            public bool Destroyed=>MaxHealth>0&&Health<=0;
            public double Damage;
            public long NextCounterShot, LastCounterShot = -1;
        }
        private sealed class CaptureState
        {
            public string Id;
            public float X,Y,Z,Radius,Progress;
            public int Owner = -1, Blue, Orange;
        }

        private readonly SimulationConfig config;
        private readonly MatchRoster roster;
        private readonly UnitState[] units = new UnitState[MatchLimits.TotalControllerSlots];
        private readonly BaseState[] bases;
        private readonly ObstacleDefinition[] obstacles;
        private readonly CaptureState[] captures;
        private readonly int[][] teamBaseIndices;
        private readonly bool[] lockedHitDamage = new bool[MatchLimits.TotalControllerSlots];
        private readonly double[] unitDamage = new double[MatchLimits.TotalControllerSlots];
        private readonly double[] shieldDamage = new double[MatchLimits.TotalControllerSlots];
        private readonly double[] unitDown = new double[MatchLimits.TotalControllerSlots];
        private readonly double[] shieldDown = new double[MatchLimits.TotalControllerSlots];
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

        public BattleSimulation(MapDefinition map, SimulationConfig config, int humanSlotId = 0, UnitLoadout[][] loadouts = null, int initialHumanDeckIndex = 0, int humanMainBaseIndex = -1)
        {
            MapValidator.ThrowIfInvalid(map);
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.Validate();
            if (initialHumanDeckIndex < 0 || initialHumanDeckIndex >= config.deckCosts.Length || humanSlotId < 0 && initialHumanDeckIndex != 0)
                throw new ArgumentOutOfRangeException(nameof(initialHumanDeckIndex));
            this.config = config.Copy();
            roster = new MatchRoster(humanSlotId);
            if (humanMainBaseIndex < -1 || humanMainBaseIndex >= map.mainBases.Length
                || humanMainBaseIndex >= 0 && (humanSlotId < 0 || map.mainBases[humanMainBaseIndex].team != roster.Controllers[humanSlotId].Team))
                throw new ArgumentOutOfRangeException(nameof(humanMainBaseIndex));
            FixedDeltaTime = 1f / config.tickHz;
            durationTicks = SecondsToTicks(config.maxDurationSeconds);
            shotIntervalTicks = SecondsToTicks(config.fireIntervalSeconds);
            respawnDelayTicks = SecondsToTicks(config.respawnDelaySeconds);
            baseCounterFireIntervalTicks = config.baseCounterFireIntervalSeconds > 0
                ? SecondsToTicks(config.baseCounterFireIntervalSeconds) : 0;
            cooldownTicks = new long[config.deckCosts.Length];
            for (int i = 0; i < cooldownTicks.Length; i++) cooldownTicks[i] = SecondsToTicks(config.deckCooldownSeconds[i]);
            halfWidth = map.halfWidth; halfDepth = map.halfDepth;
            var sourceObstacles = map.obstacles ?? Array.Empty<ObstacleDefinition>();
            obstacles = new ObstacleDefinition[config.traversalEnabled ? sourceObstacles.Length : 0];
            for (int i = 0; i < obstacles.Length; i++) obstacles[i] = sourceObstacles[i].Copy();
            if (config.unitRadius >= halfWidth || config.unitRadius >= halfDepth)
                throw new ArgumentException("Unit radius must fit inside the map.");
            InitializeStrategy();
            bases = new BaseState[map.mainBases.Length];
            baseDamage = new double[bases.Length];
            var counts = new int[MatchLimits.TeamCount];
            for (int i = 0; i < bases.Length; i++)
            {
                var source = map.mainBases[i];
                float health=this.config.mainBaseDurabilityEnabled?(source.maxHealth>0?source.maxHealth:this.config.mainBaseHealth):0;
                bases[i] = new BaseState { Id = source.id,Name=map.MainBaseName(i), Team = source.team, X = source.x,Y=source.y,
                    Z = source.z,SpawnX=source.SpawnX,SpawnY=source.SpawnY,SpawnZ=source.SpawnZ,Health=health,MaxHealth=health,SpawnRadius = source.spawnRadius, AttackRadius = source.attackRadius, BuildingRadius = source.buildingRadius };
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
                captures[i] = new CaptureState { Id = source.id, X = source.x,Y=map.surfaceY>0?source.y:GetSurfaceHeight(source.x,source.z), Z = source.z, Radius = source.radius };
            }
            if (loadouts != null && loadouts.Length != units.Length) throw new ArgumentException("Expected loadouts for all 100 controller slots.");
            for (int i = 0; i < units.Length; i++)
            {
                var deck = new UnitLoadout[cooldownTicks.Length];
                if (loadouts != null && (loadouts[i] == null || loadouts[i].Length != deck.Length)) throw new ArgumentException("Deck sizes must match the configured deck slots.");
                var intervals = new long[deck.Length]; var cooldowns = new long[deck.Length];
                for (int d = 0; d < deck.Length; d++)
                {
                    deck[d] = loadouts == null ? new UnitLoadout("p0-" + d, config.maxHealth, config.maxShield, config.moveSpeed, config.maxAmmo,
                        config.fireDamage, config.fireRange, config.fireIntervalSeconds, config.deckCosts[d], config.deckCooldownSeconds[d]) : loadouts[i][d];
                    if (deck[d] == null || (double)deck[d].CooldownSeconds * config.tickHz > long.MaxValue / 4d
                        || (double)deck[d].FireIntervalSeconds * config.tickHz > long.MaxValue / 4d) throw new ArgumentException("Invalid loadout timing.");
                    intervals[d] = SecondsToTicks(deck[d].FireIntervalSeconds); cooldowns[d] = SecondsToTicks(deck[d].CooldownSeconds);
                }
                units[i] = new UnitState { Slot = i, Team = roster.Controllers[i].Team, DeckReady = new long[deck.Length], Loadouts = deck, ShotIntervals = intervals, Cooldowns = cooldowns, PreferredDeck = i == humanSlotId ? initialHumanDeckIndex : -1, PreferredMainBase = i == humanSlotId ? humanMainBaseIndex : -1 };
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
        public UnitLoadout GetLoadout(int slot, int deck)
        {
            if (deck < 0 || deck >= DeckCount) throw new ArgumentOutOfRangeException(nameof(deck));
            return Unit(slot).Loadouts[deck];
        }
        public long GetRespawnReadyTick(int slot) { return Unit(slot).NextRespawn; }
        public void SetAutomaticRespawn(int slot, bool enabled) { Unit(slot).AutoRespawn = enabled; }
        public UnitSnapshot GetUnit(int slotId)
        {
            var u = Unit(slotId);
            return new UnitSnapshot(slotId, u.Id, u.Team, roster.Controllers[slotId].Controller,
                u.Alive, u.Deck, u.X, u.Z, u.Health, u.Shield, u.Ammo, u.Boost, u.AimYaw, u.LastShotTick, u.AimPitch, u.ShotEndX, u.ShotEndY, u.ShotEndZ, u.Magazine, Remaining(u.ReloadReady), ChargeFraction(u), u.BeamUntil > CurrentTick,
                u.LastMelee, u.MeleeAttack == null ? MeleeStyle.Normal : u.MeleeAttack.style, u.ComboStep, MeleeChargeFraction(u), MeleePhase(u), u.Guarding, u.LastShieldBlock,
                u.DownPoints, u.Loadouts[u.Deck].DownPointCapacity, CurrentTick < u.DisabledUntil ? u.Disruption : DisruptionState.Ready, Remaining(u.DisabledUntil), u.Y, u.VerticalVelocity, u.Grounded, u.ActiveWeaponIndex, u.Loadouts[u.Deck].WeaponCount, u.IncomingLockCount, u.LastDamageTick, u.LastLockedHitTick, u.BoostOverheated, u.BoostMoving,u.Crouching,u.Alive&&u.ReturnUntil>=0,u.CarriedNuclear>=0);
        }
        public SlotStatsSnapshot GetSlotStats(int slotId)
        {
            var u = Unit(slotId);
            return new SlotStatsSnapshot(slotId, u.Team, roster.Controllers[slotId].Controller, u.Kills, u.Deaths, u.DamageDealt, u.CoreDamageDealt, u.DamageReceived, u.HealingDealt, u.HealingReceived);
        }
        public MainBaseSnapshot GetBase(int index)
        {
            if (index < 0 || index >= bases.Length) throw new ArgumentOutOfRangeException(nameof(index));
            var b = bases[index]; return new MainBaseSnapshot(b.Id, b.Team, b.X, b.Z, b.AttackRadius, b.Damage, b.BuildingRadius, b.LastCounterShot,b.Y,b.Name,b.Health,b.MaxHealth,b.Destroyed);
        }
        public CaptureSnapshot GetCapturePoint(int index)
        {
            if (index < 0 || index >= captures.Length) throw new ArgumentOutOfRangeException(nameof(index));
            var c = captures[index]; return new CaptureSnapshot(c.Id, c.X, c.Z, c.Radius, c.Owner, c.Progress, c.Blue, c.Orange,c.Y);
        }
        public bool CanSpawnAtCapture(int team, int captureIndex)
        {
            CheckTeam(team);
            if (captureIndex < 0 || captureIndex >= captures.Length) throw new ArgumentOutOfRangeException(nameof(captureIndex));
            var c = captures[captureIndex];
            return c.Owner == team && (team == 0 ? c.Orange : c.Blue) == 0;
        }
        public float GetYellowLockRange(int slot)
        {var w=ActiveWeapon(Unit(slot));return w.Kind==WeaponKind.Ranged||w.Kind==WeaponKind.Melee?(w.YellowLockRange>0?w.YellowLockRange:w.FireRange):0;}
        public bool IsYellowLocked(int fromSlot,int toSlot)
        {
            var a=Unit(fromSlot);var b=Unit(toSlot);double range=GetYellowLockRange(fromSlot);
            return range>0 && a.Alive && b.Alive && a.Team!=b.Team && DistanceSquared(a.X,a.Z,b.X,b.Z)+(a.Y-b.Y)*(a.Y-b.Y)<=range*range && HasLineOfSight(fromSlot,toSlot);
        }
        public float GetHipSpreadDegrees(int slot)=>config.weaponSpreadEnabled?ActiveWeapon(Unit(slot)).HipSpreadDegrees:0;
        public float GetRedLockRange(int attackerSlot, int targetSlot)
        {
            var a = Unit(attackerSlot); var target = Unit(targetSlot);
            var active = ActiveWeapon(a);
            if (active.Kind == WeaponKind.RepairPod || active.Kind == WeaponKind.Mine || active.Kind == WeaponKind.Grenade || active.Kind == WeaponKind.Radar || active.Kind == WeaponKind.RepairGun) return 0;
            float range = active.RedLockRange > 0 ? active.RedLockRange : config.redLockRange;
            return roster.Controllers[attackerSlot].Controller == ControllerType.Bot &&
                (roster.Controllers[targetSlot].Controller == ControllerType.Human || roster.Controllers[targetSlot].Controller == ControllerType.RemoteHuman)
                ? range * config.botToHumanRedLockMultiplier : range;
        }
        public bool IsRedLocked(int attackerSlot, int targetSlot)
        {
            var a = Unit(attackerSlot); var target = Unit(targetSlot);
            double range = GetRedLockRange(attackerSlot, targetSlot);
            return a.Alive && target.Alive && a.Team != target.Team && DistanceSquared(a.X, a.Z, target.X, target.Z) + (a.Y - target.Y) * (a.Y - target.Y) <= range * range && HasLineOfSight(attackerSlot, targetSlot);
        }

        // Self-facing warning only: no attacker identity, bearing or hidden position.
        private void UpdateIncomingLocks()
        {
            for (int i = 0; i < units.Length; i++) units[i].IncomingLockCount = 0;
            for (int i = 0; i < units.Length; i++)
            {
                var source = units[i];
                if (!source.Alive || !source.HasCommand) continue;
                int target = source.Command.LockedTargetSlot;
                if (target >= 0 && units[target].Id == source.Command.LockedTargetUnitId && IsYellowLocked(i, target))
                    units[target].IncomingLockCount++;
            }
        }

        public bool Submit(int slotId, UnitCommand command)
        {
            if (Phase != MatchPhase.Running || slotId < 0 || slotId >= units.Length) return false;
            var u = units[slotId];
            if (!u.Alive || command.UnitId != u.Id || command.Tick != CurrentTick ||
                (u.HasSequence && unchecked((int)(command.Sequence - u.Sequence)) <= 0)) return false;
            if(command.LockedDeployableIndex < -1 || command.LockedDeployableIndex>=deployables.Length || command.LockedDeployableId<0
                || command.LockedDeployableIndex>=0 && (command.LockedDeployableId<=0 || command.LockedTargetSlot>=0))return false;
            if (command.WeaponSwitchDelta < -2 || command.WeaponSwitchDelta > 2 || command.LockedTargetSlot < -1 || command.LockedTargetSlot >= units.Length || command.LockedTargetUnitId < 0) return false;
            if (command.HasAimPoint && (!SimulationConfig.IsFinite(command.AimTargetX) || !SimulationConfig.IsFinite(command.AimTargetY) || !SimulationConfig.IsFinite(command.AimTargetZ))) return false;
            if (command.MeleeStyle < MeleeStyle.Normal || command.MeleeStyle > MeleeStyle.Charge) return false;
            if (!SimulationConfig.IsFinite(command.MoveX) || !SimulationConfig.IsFinite(command.MoveY) ||
                !SimulationConfig.IsFinite(command.AimYaw) || !SimulationConfig.IsFinite(command.AimPitch) ||
                Math.Abs(command.MoveX) > 1f || Math.Abs(command.MoveY) > 1f) return false;
            u.Command = command; u.Sequence = command.Sequence; u.HasSequence = true; u.HasCommand = true;
            return true;
        }

        public void Step()
        {
            if (Phase != MatchPhase.Running) return;
            BeginImpacts();
            Array.Clear(lockedHitDamage, 0, lockedHitDamage.Length);
            Array.Clear(unitDamage, 0, unitDamage.Length); Array.Clear(shieldDamage, 0, shieldDamage.Length);
            Array.Clear(unitDown, 0, unitDown.Length); Array.Clear(shieldDown, 0, shieldDown.Length); Array.Clear(baseDamage, 0, baseDamage.Length);
            RepairReturnedUnits();
            for (int i = 0; i < units.Length; i++) if (!units[i].Alive) TrySpawn(i);
            for (int i = 0; i < units.Length; i++) { units[i].AimStartYaw=units[i].AimYaw; units[i].AimStartPitch=units[i].AimPitch; units[i].BoostMoving=false; PrepareEmergencyReturn(units[i]); PreparePortableNuclearInput(units[i]); PrepareInventory(units[i]); PrepareCombat(units[i]); PrepareCrouch(units[i]); }
            for (int i = 0; i < units.Length; i++) Move(units[i]);
            for (int i = 0; i < units.Length; i++) AimAfterMovement(units[i]);
            UpdateIncomingLocks();
            UpdatePortableNuclear(true);
            // Gather all shot intents before resolving any lethal damage.
            for (int i = 0; i < units.Length; i++) { Fire(units[i]); Melee(units[i]); }
            AdvanceProjectiles();
            ResolveDeployableDamage();
            AdvanceDeployables();
            ApplyBaseCounterFire();
            AdvanceStrategies();
            for (int i = 0; i < units.Length; i++) ResolveDamage(i);
            UpdatePortableNuclear(false);
            FinishEmergencyReturns();
            foreach(var u in units)PrepareCrouch(u);
            ResolveDeployableDamage();
            // Clear warnings immediately when either end of a lock was destroyed.
            UpdateIncomingLocks();
            ResolveRepairs();
            RepairFromPods();
            for (int i = 0; i < bases.Length; i++)
            {
                var b=bases[i];if(b.Destroyed)continue;
                double applied=b.MaxHealth>0?Math.Min(b.Health,baseDamage[i]):baseDamage[i];
                b.Damage+=applied;if(b.MaxHealth>0)b.Health=(float)Math.Max(0,b.Health-applied);
                double loss=applied*config.baseGaugeDamageFactor+(b.Destroyed?config.mainBaseDestructionGaugeLoss:0);
                gauges[b.Team]=Math.Max(0,gauges[b.Team]-loss);
            }
            UpdateTeamContacts();
            UpdateCapture();
            for (int i = 0; i < units.Length; i++) { Supply(units[i]); units[i].TriggerHeld = units[i].HasCommand && units[i].Command.FirePrimary;
                units[i].JumpHeld = units[i].HasCommand && units[i].Command.Jump;
                units[i].MeleeHeld = units[i].HasCommand && (units[i].Command.Melee || (ActiveWeapon(units[i]).Kind == WeaponKind.Melee && units[i].Command.FirePrimary)); units[i].HasCommand = false; }
            CurrentTick++;
            if (gauges[0] <= 0 || gauges[1] <= 0 || CurrentTick >= durationTicks)
            {
                Phase = MatchPhase.Finished;
                WinnerTeam = gauges[0] == gauges[1] ? -1 : gauges[0] > gauges[1] ? 0 : 1;
            }
        }

        private void Move(UnitState u)
        {
            if (!u.Alive) return;
            UpdateOverheat(u);
            IntegrateVertical(u);
            if (CurrentTick < u.DisabledUntil) { u.AerialVelocityX=u.AerialVelocityZ=0; return; }
            double x = u.HasCommand ? u.Command.MoveX : 0, z = u.HasCommand ? u.Command.MoveY : 0;
            double length = Math.Sqrt(x * x + z * z);
            if (length > 1) { x /= length; z /= length; }
            if (u.HasCommand && CurrentTick >= u.MeleeUntil) { ApplyControllerAim(u,u.Command.AimYaw,u.Command.AimPitch); }
            double multiplier = 1;
            if (config.boostCapacity > 0)
            {
                double needed = config.boostDrainPerSecond * FixedDeltaTime;
                if (u.HasCommand && u.Command.Boost && length > 0 && u.Boost > 0 && !u.BoostOverheated && CurrentTick>=u.MeleeUntil)
                {
                    double fraction = Math.Min(1, u.Boost / needed);
                    multiplier += (config.boostSpeedMultiplier - 1) * fraction;
                    u.BoostMoving=true;
                    ConsumeBoost(u,(float)needed);
                }
                else if ((!config.traversalEnabled || u.Grounded) && CurrentTick>=u.MeleeUntil && !u.MeleeCharging)
                {
                    float recovery=BoostRecoveryRate(u);
                    float cap=EffectiveBoostCapacity(u);
                    u.Boost=Math.Min(cap,u.Boost+recovery*FixedDeltaTime);
                    if(u.BoostOverheated && u.Boost>=cap-0.0001f){u.Boost=cap;u.BoostOverheated=false;}
                }
            }
            var movementWeapon = ActiveWeapon(u);
            if (u.MeleeAttack != null && (u.MeleeCharging || CurrentTick < u.MeleeUntil)) multiplier *= u.MeleeAttack.moveMultiplier;
            else if (u.ChargeStart >= 0 || u.BeamUntil > CurrentTick) multiplier *= movementWeapon.FiringMoveMultiplier;
            if (config.traversalEnabled && !u.Grounded) multiplier *= config.airControl;
            double speed = movementWeapon.MoveSpeed * multiplier;
            if(config.aerialBoostEnabled) IntegrateHorizontalMotion(u, (float)(x * speed), (float)(z * speed));
            else MoveHorizontal(u, (float)(x * speed * FixedDeltaTime), (float)(z * speed * FixedDeltaTime));
            AdvanceMeleeLunge(u);
        }

        private void LegacyMelee(UnitState attacker)
        {
            if (config.meleeDamage <= 0 || !attacker.Alive || !attacker.HasCommand || !attacker.Command.Melee || CurrentTick < attacker.NextMelee) return;
            attacker.NextMelee = CurrentTick + SecondsToTicks(config.meleeIntervalSeconds);
            double yaw = attacker.Command.AimYaw % 360f * Math.PI / 180;
            double fx = Math.Sin(yaw), fz = Math.Cos(yaw), cosine = Math.Cos(config.meleeHalfAngleDegrees * Math.PI / 180);
            for (int i = 0; i < units.Length; i++)
            {
                var target = units[i]; if (!target.Alive || target.Team == attacker.Team) continue;
                double dx = target.X - attacker.X, dz = target.Z - attacker.Z, distance = Math.Sqrt(dx * dx + dz * dz);
                if (distance <= config.meleeRange && (distance < 0.001 || (dx * fx + dz * fz) / distance >= cosine))
                { RegisterLockedHit(i,attacker.Slot,attacker.Id,config.meleeDamage); RecordImpact(i,attacker.Slot,attacker.Id,config.meleeDamage,0); unitDamage[i] += config.meleeDamage; }
            }
            for (int i = 0; i < bases.Length; i++)
            {
                var b = bases[i]; if (b.Team == attacker.Team||b.Destroyed||Math.Abs(attacker.Y-b.Y)>config.coreHeight+2) continue;
                if (b.BuildingRadius > 0 && DistanceSquared(attacker.X, attacker.Z, b.X, b.Z) > (double)b.BuildingRadius * b.BuildingRadius) continue;
                double dx = b.X - attacker.X, dz = b.Z - attacker.Z, distance = Math.Sqrt(dx * dx + dz * dz);
                if (distance <= config.meleeRange + b.AttackRadius && (distance < 0.001 || (dx * fx + dz * fz) / distance >= cosine)) { RecordCoreHit(i,attacker.Slot,attacker.Id,config.meleeDamage); baseDamage[i] += config.meleeDamage; }
            }
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
            for (int b = 0; b < bases.Length; b++)
            {
                var baseState = bases[b];
                double radius = baseState.BuildingRadius > 0 ? baseState.BuildingRadius : config.baseCounterFireRadius;
                double radiusSquared = radius * radius;
                if (baseState.Destroyed||CurrentTick < baseState.NextCounterShot) continue;
                bool fired = false;
                for (int i = 0; i < units.Length; i++)
                {
                    var u = units[i];
                    if (!u.Alive || u.Team == baseState.Team) continue;
                    if (DistanceSquared(u.X, u.Z, baseState.X, baseState.Z) > radiusSquared||Math.Abs(u.Y-baseState.Y)>config.unitHeight+1||!ClearSegment(baseState.X,baseState.Y+ShotHeight,baseState.Z,u.X,u.Y+ShotHeight,u.Z)) continue;
                    RecordImpact(i,-1,0,config.baseCounterFireDamage,0);
                    unitDamage[i] += config.baseCounterFireDamage; unitDown[i] += config.baseCounterFireDownPoints;
                    fired = true;
                }
                if (fired) { baseState.LastCounterShot = CurrentTick; baseState.NextCounterShot = CurrentTick + baseCounterFireIntervalTicks; baseCounterShotsFired++; }
            }
        }

        private void ResolveDamage(int slot)
        {
            var u = units[slot]; if (!u.Alive) return;
            double damage = unitDamage[slot], down = unitDown[slot];
            double guarded = shieldDamage[slot];
            if (!config.directionalShieldEnabled) { guarded += damage; damage = 0; }
            double absorbed = Math.Min(u.Shield, guarded);
            if (absorbed > 0) { u.Shield -= (float)absorbed; u.LastShieldBlock = CurrentTick; if (u.Shield <= 0) u.Guarding = false; }
            if (guarded > 0) down += shieldDown[slot] * (1 - absorbed / guarded);
            damage += guarded - absorbed;
            if (damage > 0 || absorbed > 0)
            { u.LastDamageTick = CurrentTick; if (lockedHitDamage[slot]) u.LastLockedHitTick = CurrentTick; }
            if (damage <= 0 && down <= 0) { PublishImpacts(u,0,absorbed,0,guarded); return; }
            double hpLost=Math.Min(u.Health,damage);
            u.Health = (float)Math.Max(0, u.Health - damage);
            PublishImpacts(u,hpLost,absorbed,damage,guarded);
            ApplyDownPoints(u, down);
            if (u.Health > 0) return;
            u.Alive = false; u.Guarding = u.MeleePending = u.MeleeCharging = false; u.ChargeStart = -1; EndBeam(u); AliveCount--; unitsDestroyed++;
            u.DeckReady[u.Deck] = CurrentTick + u.Cooldowns[u.Deck];
            u.NextRespawn = CurrentTick + respawnDelayTicks;
            AwardStrategyLoss(u.Team);
            gauges[u.Team] = Math.Max(0, gauges[u.Team] - u.Loadouts[u.Deck].Cost * (double)config.unitGaugeLossPerCost);
        }

        private void TrySpawn(int slot)
        {
            var u = units[slot]; if (u.Alive || CurrentTick < u.NextRespawn || (!u.AutoRespawn && !u.SpawnRequested)) return;
            if(u.Returned&&!u.SpawnRequested)return;
            bool returning=u.Returned;
            int deck = u.PreferredDeck >= 0 && u.DeckReady[u.PreferredDeck] <= CurrentTick ? u.PreferredDeck : -1;
            for (int i = 1; deck < 0 && i <= cooldownTicks.Length; i++)
            {
                int candidate = (u.Deck + i) % cooldownTicks.Length;
                if (u.DeckReady[candidate] <= CurrentTick) { deck = candidate; break; }
            }
            if (deck < 0) return;
            var indices = teamBaseIndices[u.Team];
            int local = slot % MatchLimits.ControllerSlotsPerTeam;
            var home = bases[u.PreferredMainBase >= 0 ? u.PreferredMainBase : indices[local % indices.Length]];
            if(u.PreferredMainBase>=0&&bases[u.PreferredMainBase].Destroyed){u.SpawnRequested=false;return;}
            if(home.Destroyed){home=null;foreach(int index in indices)if(!bases[index].Destroyed){home=bases[index];break;}if(home==null)return;}
            if(u.PreferredMainBase < 0 && config.botCentralApproachEnabled && roster.Controllers[slot].Controller==ControllerType.Bot && local%9==0)
            {
                bool raider=local/9%3!=0;float lane=raider?(u.Team==0?80:-80):(u.Team==0?-80:80),best=float.PositiveInfinity;
                foreach(int index in indices){if(bases[index].Destroyed)continue;float d=Math.Abs(bases[index].Z-lane);if(d<best){best=d;home=bases[index];}}
            }
            float x = home.SpawnX, z = home.SpawnZ, spawnY=home.SpawnY, radius = home.SpawnRadius;
            double bestScore = double.PositiveInfinity; bool captureSpawn=false;
            for (int i = 0; i < captures.Length; i++)
            {
                if (u.PreferredMainBase >= 0 || u.SpawnRequested && u.PreferredSpawn == -1) continue;
                if (u.SpawnRequested && u.PreferredSpawn >= 0 && i != u.PreferredSpawn) continue;
                if (!CanSpawnAtCapture(u.Team, i)) continue;
                var c = captures[i];
                // Public objective geometry only; no enemy-unit truth in spawn selection.
                double goalX = u.Team == 0 ? halfWidth : -halfWidth;
                double score = DistanceSquared(c.X, c.Z, goalX, home.Z);
                if (score < bestScore) { bestScore = score; x = c.X; z = c.Z;spawnY=c.Y; radius = c.Radius; captureSpawn=true; }
            }
            if (u.SpawnRequested && u.PreferredSpawn >= 0 && !CanSpawnAtCapture(u.Team, u.PreferredSpawn)) { u.SpawnRequested = false; return; }
            int ordinal = local / indices.Length;
            int perBase = (MatchLimits.ControllerSlotsPerTeam + indices.Length - 1) / indices.Length;
            double scatter = radius * config.spawnScatterFraction * Math.Sqrt((ordinal + 0.5) / perBase);
            if(config.traversalEnabled && !captureSpawn && radius>home.AttackRadius+2*config.unitRadius)
                scatter=Math.Min(radius-config.unitRadius,Math.Max(scatter,home.AttackRadius+config.unitRadius+0.25f));
            double angle = ordinal * 2.399963229728653;
            double spawnSide=config.traversalEnabled && !captureSpawn && u.Team==1?-1:1;
            u.X = Clamp((float)(x + spawnSide * Math.Cos(angle) * scatter), -halfWidth + config.unitRadius, halfWidth - config.unitRadius);
            u.Z = Clamp((float)(z + Math.Sin(angle) * scatter), -halfDepth + config.unitRadius, halfDepth - config.unitRadius);
            u.LastSpawnMainBase=captureSpawn?-1:Array.IndexOf(bases,home);u.Crouching=false;
            u.Deck = deck; u.Id = checked(++nextUnitId); u.Alive = true;
            var loadout = u.Loadouts[deck];
            u.Y = GetSurfaceHeightBelow(u.X,u.Z,spawnY+config.maximumStepHeight,config.unitRadius); u.VerticalVelocity = 0; u.Grounded = true; u.JumpHeld = false;
            u.AerialOriginY=u.Y;u.AerialVelocityX=u.AerialVelocityZ=0;
            if(!returning){u.Health = loadout.MaxHealth; u.Shield = loadout.MaxShield;} if(!returning){u.Boost = EffectiveBoostCapacity(u);u.BoostOverheated=false;}u.BoostMoving=false;u.MeleeLungeRemaining=0;
            if(!returning)ResetCombat(u, loadout);
            else {u.Disruption=DisruptionState.Ready;u.DisabledUntil=u.MeleeUntil=0;u.DownPoints=0;u.DownStage=0;u.MeleeAttack=null;u.MeleePending=u.MeleeCharging=false;u.NuclearSuppressTrigger=true;}
            u.Returned=false;u.ReturnUntil=-1;
            u.IncomingLockCount = 0; u.LastDamageTick = u.LastLockedHitTick = -1;
            u.LastShotTick = -1; u.NextMelee = CurrentTick; u.SpawnRequested = false; u.PreferredDeck = -1; u.PreferredSpawn = -2; u.PreferredMainBase = -1;
            if(!returning){u.Ammo = loadout.MaxAmmo; u.AmmoFraction = 0; u.NextShot = CurrentTick;}
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
                    var u = units[slot]; if (!u.Alive || Math.Abs(u.Y-c.Y)>3 || DistanceSquared(u.X, u.Z, c.X, c.Z) > radiusSquared) continue;
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
            if (!u.Alive || (config.traversalEnabled && !u.Grounded)) return;
            bool available = false;
            var homeIndices = teamBaseIndices[u.Team];
            double radius = config.homeSupplyRadius;
            for (int i = 0; i < homeIndices.Length; i++)
            {
                var b = bases[homeIndices[i]];
                if (!b.Destroyed&&Math.Abs(u.Y-b.Y)<3&&DistanceSquared(u.X, u.Z, b.X, b.Z) <= radius * radius) { available = true; break; }
            }
            if (!available) return;
            var loadout = u.Loadouts[u.Deck];
            RestoreHealth(u, config.homeHealthPerSecond * FixedDeltaTime);
            u.Shield = Math.Min(loadout.MaxShield, u.Shield + config.homeShieldPerSecond * FixedDeltaTime);
            ResupplyAmmo(u,config.homeAmmoPerSecond);

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
