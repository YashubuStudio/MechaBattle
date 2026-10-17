using System;
using MechaBattle.Contracts;
namespace MechaBattle.Simulation
{
    public sealed partial class BattleSimulation
    {
        private struct Projectile
        {
            public bool Active; public int Team, HomingSlot, HomingUnitId, OwnerSlot, OwnerUnitId; public float Power; public long FuseTick; public float X, Y, Z, OriginX, OriginZ, Remaining, Dx, Dy, Dz;
            public UnitLoadout Weapon;
        }
        private readonly Projectile[] projectiles = new Projectile[512];
        private readonly ExplosionSnapshot[] explosions = new ExplosionSnapshot[64];
        private int nextExplosion;
        public int ProjectileCapacity => projectiles.Length;
        public int ExplosionCapacity => explosions.Length;
        public ProjectileSnapshot GetProjectile(int index)
        {
            if (index < 0 || index >= projectiles.Length) throw new ArgumentOutOfRangeException(nameof(index));
            var p = projectiles[index]; return new ProjectileSnapshot(p.Active, p.Team, p.X, p.Y, p.Z, p.Weapon == null ? 0 : p.Weapon.BlastRadius, p.Weapon == null ? WeaponKind.Ranged : p.Weapon.Kind, p.Active && p.HomingSlot >= 0,p.Dx,p.Dy,p.Dz);
        }
        public ExplosionSnapshot GetExplosion(int index)
        { if (index < 0 || index >= explosions.Length) throw new ArgumentOutOfRangeException(nameof(index)); return explosions[index]; }
        public void CancelHeldActions(int slot)
        {
            var u = Unit(slot); u.ChargeStart = -1; EndBeam(u);
            if (u.MeleeCharging) { u.MeleeCharging = false; u.MeleeAttack = null; }
            u.TriggerHeld = u.MeleeHeld = false;
        }
        private bool TryRedTarget(UnitState u, out UnitState target)
        {
            target = null;
            if (ActiveWeapon(u).Kind != WeaponKind.Ranged || !config.redLockAssistEnabled || !u.HasCommand || u.Command.LockedTargetSlot < 0) return false;
            var candidate = units[u.Command.LockedTargetSlot];
            if (candidate.Id != u.Command.LockedTargetUnitId || !IsRedLocked(u.Slot, u.Command.LockedTargetSlot)) return false;
            target = candidate; return true;
        }
        private void AimAfterMovement(UnitState u)
        {
            if (!u.Alive || !u.HasCommand || CurrentTick < u.DisabledUntil || CurrentTick < u.MeleeUntil) return;
            float x, y, z;
            if (TryRedTarget(u, out var target)) { x = target.X; y = target.Y + ShotHeight; z = target.Z; }
            else if (u.Command.HasAimPoint) { x = u.Command.AimTargetX; y = u.Command.AimTargetY; z = u.Command.AimTargetZ; }
            else return;
            double dx = x - u.X, dz = z - u.Z, planar = Math.Sqrt(dx * dx + dz * dz);
            float yaw=planar>0.000001?(float)(Math.Atan2(dx,dz)*180/Math.PI):u.AimYaw;
            float pitch=config.aimIn3DEnabled?(float)(-Math.Atan2(y-u.Y-ShotHeight,planar)*180/Math.PI):0;
            if(target==null&&roster.Controllers[u.Slot].Controller!=ControllerType.Bot)
            {
                double cy=u.Command.AimYaw*Math.PI/180,cp=u.Command.AimPitch*Math.PI/180;
                double ahead=dx*Math.Sin(cy)*Math.Cos(cp)+dz*Math.Cos(cy)*Math.Cos(cp)-(y-u.Y-ShotHeight)*Math.Sin(cp);
                if(ahead<-.05)return; // A nearby camera hit must never turn the freely aimed body backwards after movement.
            }
            ApplyControllerAim(u,yaw,pitch);
        }
        private void ApplyControllerAim(UnitState u,float yaw,float pitch)
        {
            if(config.botPerceptionEnabled && roster.Controllers[u.Slot].Controller==ControllerType.Bot)
            {
                float limit=config.botViewTurnDegreesPerSecond*FixedDeltaTime;
                float delta=(yaw-u.AimStartYaw)%360;if(delta>180)delta-=360;if(delta< -180)delta+=360;
                u.AimYaw=u.AimStartYaw+Clamp(delta,-limit,limit);
                u.AimPitch=u.AimStartPitch+Clamp(pitch-u.AimStartPitch,-limit,limit);
            }
            else {u.AimYaw=yaw;u.AimPitch=pitch;}
        }
        private float Remaining(long tick) { return Math.Max(0, (tick - CurrentTick) * FixedDeltaTime); }
        private float ChargeFraction(UnitState u)
        { return u.ChargeStart < 0 ? 0 : Clamp((CurrentTick - u.ChargeStart) * FixedDeltaTime / ActiveWeapon(u).FullChargeSeconds, 0, 1); }
        private float MeleeChargeFraction(UnitState u)
        { return !u.MeleeCharging || u.MeleeAttack == null ? 0 : Clamp((CurrentTick - u.MeleeHitTick) * FixedDeltaTime / u.MeleeAttack.fullChargeSeconds, 0, 1); }
        public float GetMeleeContactPhase(int slot)
        {var u=Unit(slot);var a=u.MeleeAttack;return a==null?0.3f:a.windupSeconds/(a.windupSeconds+(u.MeleeRecoverySeconds>0?u.MeleeRecoverySeconds:a.recoverySeconds));}
        private float MeleePhase(UnitState u)
        { return u.LastMelee < 0 || u.MeleeAttack == null || CurrentTick >= u.MeleeUntil ? -1 : Clamp((CurrentTick - u.LastMelee) * FixedDeltaTime / (u.MeleeAttack.windupSeconds + u.MeleeRecoverySeconds), 0, 1); }
        private void ResetCombat(UnitState u, UnitLoadout w)
        {
            InitializeWeapons(u);
            u.ReloadReady = u.ChargeStart = u.BeamUntil = u.LastShieldBlock = u.LastMelee = u.LastImpact = -1;
            u.GuardUntil = u.DisabledUntil = u.MeleeUntil = 0; u.ComboStep = u.DownStage = 0; u.DownPoints = 0;
            u.TriggerHeld = u.MeleeHeld = u.MeleePending = u.MeleeCharging = u.MeleeBypassingDisabled = false; u.MeleeAttack = null; u.MeleeHitAdjusted=false;u.MeleeRecoverySeconds=u.MeleeBoostSpent=0;
            u.Disruption = DisruptionState.Ready; u.Guarding = config.directionalShieldEnabled && w.MaxShield > 0;
        }
        private void PrepareCombat(UnitState u)
        {
            if (!u.Alive) return;
            if (u.HasCommand && u.Command.CancelHeldAttack) CancelHeldActions(u.Slot);
            if (config.downPointRecoverySeconds > 0 && u.LastImpact >= 0 && CurrentTick - u.LastImpact >= SecondsToTicks(config.downPointRecoverySeconds))
            { u.DownPoints = 0; u.DownStage = 0; u.LastImpact = -1; }
            if (u.ReloadReady >= 0 && CurrentTick >= u.ReloadReady)
            { u.Magazine = Math.Min(ActiveWeapon(u).MagazineCapacity, u.Ammo); u.ReloadReady = -1; }
            if (CurrentTick >= u.DisabledUntil) u.Disruption = DisruptionState.Ready;
            bool disabled = CurrentTick < u.DisabledUntil;
            if (u.HasCommand && !disabled && CurrentTick >= u.MeleeUntil) { ApplyControllerAim(u,u.Command.AimYaw,u.Command.AimPitch); }
            if (u.BeamUntil >= 0 && (CurrentTick >= u.BeamUntil || !u.HasCommand || !u.Command.FirePrimary || disabled)) EndBeam(u);
            if (!disabled) PrepareMelee(u);
            else if (config.normalMeleeBypassesDisabledEnabled) TryNormalMeleeWhileDisabled(u);
            bool attacking = !disabled && u.HasCommand && (u.Command.FirePrimary || u.Command.Melee);
            if (attacking || u.MeleeCharging || CurrentTick < u.MeleeUntil || u.ChargeStart >= 0 || u.BeamUntil > CurrentTick)
                u.GuardUntil = CurrentTick + (config.directionalShieldEnabled ? SecondsToTicks(config.shieldRecoverySeconds) : 1);
            u.Guarding = config.directionalShieldEnabled && u.Shield > 0 && !disabled && !IsCarryingNuclear(u.Slot) && !attacking && CurrentTick >= u.GuardUntil;
            if (u.HasCommand && u.Command.Reload && !disabled && CurrentTick >= u.MeleeUntil && !u.MeleeCharging) BeginReload(u);
            if (u.Magazine == 0 && u.Ammo > 0) BeginReload(u);
        }
        private void BeginReload(UnitState u)
        {
            var w = ActiveWeapon(u);
            if (w.MagazineCapacity <= 0 || u.ReloadReady >= 0 || u.Ammo <= u.Magazine || u.Magazine >= w.MagazineCapacity) return;
            u.ReloadReady = CurrentTick + SecondsToTicks(w.ReloadSeconds); u.ChargeStart = -1;
            if (u.BeamUntil >= 0) EndBeam(u);
        }
        private void EndBeam(UnitState u)
        {
            if (u.BeamUntil < 0) return;
            u.BeamUntil = -1; u.NextShot = Math.Max(u.NextShot, CurrentTick + SecondsToTicks(ActiveWeapon(u).BeamRecoverySeconds));
        }
        private void Fire(UnitState u)
        {
            var w = ActiveWeapon(u);
            if (w.Kind == WeaponKind.Melee || u.HasCommand && u.Command.CancelHeldAttack) return;
            bool held = u.HasCommand && u.Command.FirePrimary;
            if (!u.Alive || CurrentTick < u.DisabledUntil || CurrentTick < u.MeleeUntil || u.MeleeCharging || u.ReloadReady >= 0 || u.Ammo <= 0 || u.Magazine <= 0)
            { u.ChargeStart = -1; EndBeam(u); return; }
            if (w.FireMode == WeaponFireMode.Charge)
            {
                if (held && u.ChargeStart < 0 && CurrentTick >= u.NextShot) u.ChargeStart = CurrentTick;
                if (u.ChargeStart < 0 || held) return;
                long start = u.ChargeStart; u.ChargeStart = -1;
                if (!u.HasCommand || CurrentTick - start < SecondsToTicks(w.MinimumChargeSeconds)) return;
                float power = Clamp((CurrentTick - start) * FixedDeltaTime / w.FullChargeSeconds, 0, 1);
                Shoot(u, power); return;
            }
            if (w.FireMode == WeaponFireMode.ContinuousBeam)
            {
                if (!held) { EndBeam(u); return; }
                if (u.BeamUntil < 0 && CurrentTick >= u.NextShot) u.BeamUntil = CurrentTick + SecondsToTicks(w.MaximumBeamSeconds);
                if (u.BeamUntil <= CurrentTick || CurrentTick < u.NextShot) return;
                Shoot(u, 1); return;
            }
            if (!held || CurrentTick < u.NextShot) return;
            if ((w.FireMode == WeaponFireMode.SemiAutomatic || w.FireMode == WeaponFireMode.SingleShot) && u.TriggerHeld) return;
            Shoot(u, 1);
        }
        private bool HasManualWeaponLock(UnitState u)
        {
            int slot=u.HasCommand?u.Command.LockedTargetSlot:-1;
            return slot>=0 && units[slot].Id==u.Command.LockedTargetUnitId && (IsYellowLocked(u.Slot,slot) || ActiveWeapon(u).Kind==WeaponKind.RepairGun && IsFriendlyYellowLocked(u.Slot,slot))
                || u.HasCommand && IsDeployableYellowLocked(u.Slot,u.Command.LockedDeployableIndex,u.Command.LockedDeployableId);
        }
        private void Shoot(UnitState u, float power)
        {
            var w = ActiveWeapon(u);
            double yaw = u.AimYaw % 360f * Math.PI / 180, pitch = config.aimIn3DEnabled ? u.AimPitch % 360f * Math.PI / 180 : 0;
            if(config.weaponSpreadEnabled && w.HipSpreadDegrees>0 && !HasManualWeaponLock(u))
            {
                uint seed=unchecked((uint)u.Id*747796405u+(uint)CurrentTick*2891336453u);
                seed^=seed>>16;seed*=2246822519u;seed^=seed>>13;
                double radius=Math.Sqrt((seed&65535)/65535.0)*w.HipSpreadDegrees*Math.PI/180,angle=(seed>>16)/65535.0*Math.PI*2;
                yaw+=Math.Cos(angle)*radius;if(config.aimIn3DEnabled)pitch+=Math.Sin(angle)*radius;
            }
            double dx = Math.Sin(yaw) * Math.Cos(pitch), dy = -Math.Sin(pitch), dz = Math.Cos(yaw) * Math.Cos(pitch);
            float originY = u.Y + ShotHeight;
            if (w.Kind == WeaponKind.RepairPod || w.Kind == WeaponKind.Mine || w.Kind == WeaponKind.Radar)
            { if (!PlaceDeployable(u, w)) return; }
            else if (w.Kind == WeaponKind.RepairGun)
            { FireRepairGun(u,w,dx,dy,dz); }
            else if (w.ProjectileSpeed > 0)
            {
                int free = -1; for (int i = 0; i < projectiles.Length; i++) if (!projectiles[i].Active) { free = i; break; }
                if (free < 0) return;
                bool guided = TryRedTarget(u, out var guidedTarget);
                projectiles[free] = new Projectile { Active = true, Team = u.Team, OwnerSlot=u.Slot, OwnerUnitId=u.Id, X = u.X, Y = originY, Z = u.Z, OriginX = u.X, OriginZ = u.Z,
                    HomingSlot = guided ? guidedTarget.Slot : -1, HomingUnitId = guided ? guidedTarget.Id : 0, Power = power, FuseTick = w.Kind == WeaponKind.Grenade ? CurrentTick + SecondsToTicks(w.FuseSeconds) : -1,
                    Remaining = w.FireRange, Dx = (float)dx, Dy = (float)dy + (w.Kind == WeaponKind.Grenade ? 0.45f : 0), Dz = (float)dz, Weapon = w };
                u.ShotEndX = u.X + (float)dx * w.FireRange; u.ShotEndY = originY + (float)dy * w.FireRange; u.ShotEndZ = u.Z + (float)dz * w.FireRange;
            }
            else
            {
                FindHit(u.Team, u.X, originY, u.Z, u.X, u.Z, dx, dy, dz, w.FireRange, out double distance, out int target, out int core, out int objectIndex);
                if (target >= 0) RangedDamage(target, u.X, u.Z, w.FireDamage * power, w.FireDownPoints * power, u.Slot, u.Id);
                else if(objectIndex>=0)DamageDeployable(objectIndex,u.Slot,u.Id,w.FireDamage*power);
                else if (core >= 0) { RecordCoreHit(core,u.Slot,u.Id,w.FireDamage*power); baseDamage[core] += w.FireDamage * power; baseHits++; }
                u.ShotEndX = (float)(u.X + dx * distance); u.ShotEndY = originY + (float)(dy * distance); u.ShotEndZ = (float)(u.Z + dz * distance);
            }
            u.Ammo--; u.Magazine--; u.LastShotTick = CurrentTick; shotsFired++;
            u.NextShot = CurrentTick + SecondsToTicks(w.FireIntervalSeconds);
            if (u.Magazine <= 0) { EndBeam(u); BeginReload(u); }
        }
        private void FindHit(int team, float x, float y, float z, float originX, float originZ, double dx, double dy, double dz, double range, out double distance, out int target, out int core, out int objectIndex)
        {
            distance = config.aimIn3DEnabled && dy < -0.000001 ? Math.Min(range, Math.Max(0, -y / dy)) : range; target = core = objectIndex = -1;
            for (int i = 0; i < units.Length; i++)
            {
                var u = units[i]; if (!u.Alive || u.Team == team) continue;
                double hit = config.aimIn3DEnabled ? RayCylinder(x, y - u.Y, z, dx, dy, dz, u.X, u.Z, config.unitRadius, config.unitHeight) : RayCircle(x, z, dx, dz, u.X, u.Z, config.unitRadius);
                if (hit <= distance) { distance = hit; target = i; }
            }
            for (int i = 0; i < bases.Length; i++)
            {
                var b = bases[i];bool damageable=!b.Destroyed&&b.Team!=team && (b.BuildingRadius<=0 || DistanceSquared(originX,originZ,b.X,b.Z)<=b.BuildingRadius*(double)b.BuildingRadius);
                if(!damageable&&!config.baseCollisionEnabled)continue;
                double hit = config.aimIn3DEnabled ? RayCylinder(x, y-b.Y, z, dx, dy, dz, b.X, b.Z, b.AttackRadius, config.coreHeight) : RayCircle(x, z, dx, dz, b.X, b.Z, b.AttackRadius);
                if (hit < distance) { distance = hit; core = damageable?i:-1; target = objectIndex = -1; }
            }
            for(int i=0;i<deployables.Length;i++)
            {
                var d=deployables[i];if(!d.Active || CurrentTick>=d.Expires || d.Health<=0 || d.Team==team)continue;
                double hit=config.aimIn3DEnabled?RayCylinder(x,y-d.Y,z,dx,dy,dz,d.X,d.Z,DeployableRadius(d),DeployableHeight(d)):RayCircle(x,z,dx,dz,d.X,d.Z,DeployableRadius(d));
                if(hit<distance){distance=hit;objectIndex=i;target=core=-1;}
            }
            foreach (var o in obstacles)
            {
                double hit = MechaBattle.Battlefield.MapGeometry.RayBox(o, x, y, z, dx, dy, dz);
                if (hit <= distance) { distance = hit; target = core = objectIndex = -1; }
            }

        }
        public float ShotHeight => config.aimIn3DEnabled ? config.shotOriginHeight : 1.3f;
        // Human presentation may trace the exact center-camera ray. Bots still receive
        // only filtered observations and do not call this truth/presentation API.
        public AimPoint TraceAim(int slot, float x, float y, float z, float dx, float dy, float dz, float range)
        {
            var u = Unit(slot);
            if (!SimulationConfig.IsFinite(x) || !SimulationConfig.IsFinite(y) || !SimulationConfig.IsFinite(z) || !SimulationConfig.IsFinite(dx)
                || !SimulationConfig.IsFinite(dy) || !SimulationConfig.IsFinite(dz) || !SimulationConfig.IsFinite(range) || range <= 0) throw new ArgumentException("Invalid aim ray.");
            double length = Math.Sqrt(dx * (double)dx + dy * (double)dy + dz * (double)dz);
            // A camera hit can coincide with the muzzle when a target overlaps it.
            // There is no segment to trace; report no hit instead of breaking the HUD.
            if (length <= 0.000001) return new AimPoint(x, y, z, -1, -1);
            double vx = dx / length, vy = dy / length, vz = dz / length;
            if (!config.aimIn3DEnabled) { double planar = Math.Sqrt(vx * vx + vz * vz); if (planar < 0.000001) return new AimPoint(x, y, z, -1, -1); vx /= planar; vz /= planar; vy = 0; }
            double distance;int target,core=-1,objectIndex=-1;
            if(ActiveWeapon(u).Kind==WeaponKind.RepairGun)FindRepairHit(slot,x,y,z,vx,vy,vz,range,out distance,out target);
            else FindHit(u.Team, x, y, z, u.X, u.Z, vx, vy, vz, range, out distance, out target, out core, out objectIndex);
            return new AimPoint(x + (float)(vx * distance), y + (float)(vy * distance), z + (float)(vz * distance), target, core, objectIndex, objectIndex<0?0:deployables[objectIndex].Id);
        }
        public AimPoint TraceCameraAim(int slot,float x,float y,float z,float dx,float dy,float dz,float range)
        {
            var result=TraceAim(slot,x,y,z,dx,dy,dz,range);var self=Unit(slot);
            double forward=(result.X-self.X)*dx+(result.Y-self.Y-ShotHeight)*dy+(result.Z-self.Z)*dz;
            if(forward>.15)return result;
            return TraceAim(slot,self.X,self.Y+ShotHeight,self.Z,dx,dy,dz,range);
        }
        private static double RayCylinder(float x, float y, float z, double dx, double dy, double dz, float cx, float cz, float radius, float height)
        {
            double ox = x - cx, oz = z - cz, a = dx * dx + dz * dz, c = ox * ox + oz * oz - radius * (double)radius;
            double enter = 0, exit = double.PositiveInfinity;
            if (a < 0.000000001) { if (c > 0) return double.PositiveInfinity; }
            else
            {
                double b = ox * dx + oz * dz, discriminant = b * b - a * c;
                if (discriminant < 0) return double.PositiveInfinity;
                double root = Math.Sqrt(discriminant); enter = Math.Max(enter, (-b - root) / a); exit = (-b + root) / a;
            }
            if (Math.Abs(dy) < 0.000000001) { if (y < 0 || y > height) return double.PositiveInfinity; }
            else
            {
                double bottom = -y / dy, top = (height - y) / dy;
                enter = Math.Max(enter, Math.Min(bottom, top)); exit = Math.Min(exit, Math.Max(bottom, top));
            }
            return enter <= exit && exit >= 0 ? enter : double.PositiveInfinity;
        }
        private void RegisterLockedHit(int target,int ownerSlot,int ownerUnitId,double damage)
        {
            if(damage<=0 || ownerUnitId<=0 || ownerSlot<0 || ownerSlot>=units.Length)return;
            var source=units[ownerSlot];
            // A base/mine/another attacker must not borrow a different enemy's lock.
            // Projectile ownership also prevents a respawn from inheriting an old shot.
            if(source.Alive && source.Id==ownerUnitId && source.HasCommand
                && source.Command.LockedTargetSlot==target && source.Command.LockedTargetUnitId==units[target].Id
                && IsYellowLocked(ownerSlot,target)) lockedHitDamage[target]=true;
        }
        private void RangedDamage(int target, float originX, float originZ, double damage, double down, int ownerSlot, int ownerUnitId)
        {
            RegisterLockedHit(target,ownerSlot,ownerUnitId,damage);
            var u = units[target]; double x = originX - u.X, z = originZ - u.Z, length = Math.Sqrt(x * x + z * z);
            double yaw = u.AimYaw * Math.PI / 180;
            bool front = length < 0.0001 || (x * Math.Sin(yaw) + z * Math.Cos(yaw)) / length >= Math.Cos(config.shieldHalfAngleDegrees * Math.PI / 180);
            if (config.directionalShieldEnabled && u.Guarding && front) { RecordImpact(target,ownerSlot,ownerUnitId,0,damage); shieldDamage[target] += damage; shieldDown[target] += down; }
            else { RecordImpact(target,ownerSlot,ownerUnitId,damage,0); unitDamage[target] += damage; unitDown[target] += down; }
        }
        private void AdvanceProjectiles()
        {
            for (int i = 0; i < projectiles.Length; i++)
            {
                var p = projectiles[i]; if (!p.Active) continue;
                if (p.Weapon.Kind == WeaponKind.Grenade) p.Dy -= p.Weapon.ProjectileGravity / p.Weapon.ProjectileSpeed * FixedDeltaTime;
                if (p.HomingSlot >= 0)
                {
                    var followed = units[p.HomingSlot];
                    if (followed.Alive && followed.Id == p.HomingUnitId)
                    {
                        double hx = followed.X - p.X, hy = followed.Y + ShotHeight - p.Y, hz = followed.Z - p.Z, length = Math.Sqrt(hx * hx + hy * hy + hz * hz);
                        if (length > 0.000001) { p.Dx = (float)(hx / length); p.Dy = (float)(hy / length); p.Dz = (float)(hz / length); }
                    }
                    else p.HomingSlot = -1;
                }
                double travel = Math.Min(p.Remaining, p.Weapon.ProjectileSpeed * FixedDeltaTime);
                FindHit(p.Team, p.X, p.Y, p.Z, p.OriginX, p.OriginZ, p.Dx, p.Dy, p.Dz, travel, out double hit, out int target, out int core, out int objectIndex);
                float oldX = p.X, oldZ = p.Z; p.X += (float)(p.Dx * hit); p.Y += (float)(p.Dy * hit); p.Z += (float)(p.Dz * hit); p.Remaining -= (float)hit;
                bool solidHit = hit < travel - 0.00001;
                bool groundHit = config.aimIn3DEnabled && p.Dy < 0 && p.Y <= 0.0001f;
                if (p.Weapon.Kind == WeaponKind.Grenade && p.FuseTick > CurrentTick && target < 0 && core < 0 && objectIndex<0 && (solidHit || groundHit))
                {
                    p.Y = Math.Max(0.08f, p.Y + 0.02f); p.X -= p.Dx * 0.03f; p.Z -= p.Dz * 0.03f;
                    p.Dy = Math.Abs(p.Dy) * 0.35f; p.Dx *= groundHit ? 0.6f : 0; p.Dz *= groundHit ? 0.6f : 0;
                    projectiles[i] = p; continue;
                }
                if (target >= 0 || core >= 0 || objectIndex>=0 || (p.FuseTick >= 0 && CurrentTick >= p.FuseTick) || solidHit || (config.aimIn3DEnabled && p.Dy < 0 && p.Y <= 0.0001f) || p.Remaining <= 0.0001f || Math.Abs(p.X) >= halfWidth || Math.Abs(p.Z) >= halfDepth)
                {
                    if (target >= 0) RangedDamage(target, oldX, oldZ, p.Weapon.FireDamage * p.Power, p.Weapon.FireDownPoints * p.Power, p.OwnerSlot, p.OwnerUnitId);
                    else if(objectIndex>=0)DamageDeployable(objectIndex,p.OwnerSlot,p.OwnerUnitId,p.Weapon.FireDamage*p.Power);
                    else if (core >= 0) { RecordCoreHit(core,p.OwnerSlot,p.OwnerUnitId,p.Weapon.FireDamage*p.Power); baseDamage[core] += p.Weapon.FireDamage * p.Power; baseHits++; }
                    if (p.Weapon.BlastRadius > 0) Explode(p, target, core, objectIndex); p.Active = false;
                }
                projectiles[i] = p;
            }
        }
        private void Explode(Projectile p, int directTarget, int directCore, int directObject=-1)
        {
            var w = p.Weapon;
            HitDeployablesInBlast(p,directObject);
            explosions[nextExplosion++ % explosions.Length] = new ExplosionSnapshot(true, p.Team, p.X, p.Y, p.Z, w.BlastRadius, CurrentTick);
            for (int i = 0; i < units.Length; i++)
            {
                var u = units[i]; if (!u.Alive || u.Team == p.Team || i == directTarget) continue;
                double distance = Math.Sqrt(DistanceSquared(u.X, u.Z, p.X, p.Z) + (config.aimIn3DEnabled ? (p.Y - u.Y - ShotHeight) * (p.Y - u.Y - ShotHeight) : 0));
                if (distance >= w.BlastRadius || !ClearSegment(p.X, p.Y, p.Z, u.X, u.Y + ShotHeight, u.Z)) continue;
                double falloff = 1 - distance / w.BlastRadius;
                RegisterLockedHit(i,p.OwnerSlot,p.OwnerUnitId,w.SplashDamage*falloff);
                RecordImpact(i,p.OwnerSlot,p.OwnerUnitId,w.SplashDamage*falloff,0);
                unitDamage[i] += w.SplashDamage * falloff; unitDown[i] += w.SplashDownPoints * falloff;
            }
            for (int i = 0; i < bases.Length; i++)
            {
                var b = bases[i]; if (b.Team == p.Team || b.Destroyed || i == directCore || (b.BuildingRadius > 0 && DistanceSquared(p.OriginX, p.OriginZ, b.X, b.Z) > (double)b.BuildingRadius * b.BuildingRadius)) continue;
                double horizontal = Math.Max(0, Math.Sqrt(DistanceSquared(b.X, b.Z, p.X, p.Z)) - b.AttackRadius);
                double vertical = config.aimIn3DEnabled ? Math.Max(0,Math.Max(b.Y-p.Y,p.Y-b.Y-config.coreHeight)) : 0;
                double distance = Math.Sqrt(horizontal * horizontal + vertical * vertical);
                if (distance >= w.BlastRadius || !ClearSegment(p.X, p.Y, p.Z, b.X, Math.Min(b.Y+config.coreHeight, Math.Max(b.Y+0.1f, p.Y)), b.Z)) continue;
                RecordCoreHit(i,p.OwnerSlot,p.OwnerUnitId,w.SplashDamage*(1-distance/w.BlastRadius)); baseDamage[i] += w.SplashDamage * (1 - distance / w.BlastRadius); baseHits++;
            }
        }
        private void ApplyDownPoints(UnitState u, double points)
        {
            if (config.downPointRecoverySeconds <= 0) return;
            u.LastImpact = CurrentTick;
            u.DownPoints = Math.Min(u.Loadouts[u.Deck].DownPointCapacity * config.knockdownThresholdMultiplier, u.DownPoints + (float)points);
            int stage = u.DownPoints >= u.Loadouts[u.Deck].DownPointCapacity * config.knockdownThresholdMultiplier ? 2 : u.DownPoints >= u.Loadouts[u.Deck].DownPointCapacity ? 1 : 0;
            if (stage <= u.DownStage) return;
            u.DownStage = stage; u.Disruption = stage == 2 ? DisruptionState.KnockedDown : DisruptionState.Staggered;
            u.DisabledUntil = Math.Max(u.DisabledUntil, CurrentTick + 1 + SecondsToTicks(stage == 2 ? config.knockdownSeconds : config.staggerSeconds));
            u.MeleePending = u.MeleeCharging = false; u.MeleeLungeRemaining=0; u.MeleeUntil = 0; u.MeleeAttack = null; u.ChargeStart = -1; EndBeam(u); u.Guarding = false;
        }
        private MeleeStyle RequestedMeleeStyle(UnitState u)
        {
            var style=u.Command.MeleeStyle;
            if(!config.boostOverheatEnabled)return !u.Command.Boost?style:style==MeleeStyle.Normal?MeleeStyle.Dash:style==MeleeStyle.Combo?MeleeStyle.DashCombo:style;
            return HasBoostMotion(u)?style==MeleeStyle.Combo||style==MeleeStyle.DashCombo?MeleeStyle.DashCombo:MeleeStyle.Dash:style;
        }
        private void PrepareMelee(UnitState u)
        {
            if (config.meleeAttacks == null || config.meleeAttacks.Length == 0) return;
            if (config.equipmentRulesEnabled && ActiveWeapon(u).Kind != WeaponKind.Melee) return;
            bool held = u.HasCommand && (u.Command.Melee || (ActiveWeapon(u).Kind == WeaponKind.Melee && u.Command.FirePrimary));
            if (u.MeleeCharging)
            {
                if (held && RequestedMeleeStyle(u) == MeleeStyle.Charge) return;
                if (held) { u.MeleeCharging = false; u.MeleeAttack = null; return; }
                var attack = u.MeleeAttack; float seconds = (CurrentTick - u.MeleeHitTick) * FixedDeltaTime; u.MeleeCharging = false;
                if (!u.HasCommand || seconds < attack.minimumChargeSeconds) { u.MeleeAttack = null; return; }
                float fraction = Math.Min(1, seconds / attack.fullChargeSeconds);
                // The two new arm-type archetypes read the raw charge fraction themselves (see
                // Melee()); only the legacy single-scalar path still floors it at minimumChargeMultiplier.
                float power = (attack.chargeScalesRange || attack.thresholdChargeFraction > 0) ? fraction : Math.Max(attack.minimumChargeMultiplier, fraction);
                StartMelee(u, attack, power, 1); return;
            }
            if (!held || CurrentTick < u.DisabledUntil || CurrentTick < u.MeleeUntil) return;
            if (u.MeleeAttack != null && CurrentTick > u.MeleeUntil + SecondsToTicks(0.2f)) u.MeleeAttack = null;
            if (u.MeleeAttack != null && u.ComboStep < u.MeleeAttack.comboSteps && RequestedMeleeStyle(u) == u.MeleeAttack.style)
            { StartMelee(u, u.MeleeAttack, 1, u.ComboStep + 1); return; }
            if (u.MeleeHeld || CurrentTick < u.NextMelee) return;
            MeleeAttackDefinition selected = null;
            foreach (var a in config.meleeAttacks) if (a.style == RequestedMeleeStyle(u)) { selected = a; break; }
            // User request 2026-10-16: charge melee is derived from the build's arm family
            // when the build declares one, overriding the shared global Charge fallback.
            if (RequestedMeleeStyle(u) == MeleeStyle.Charge && u.Loadouts[u.Deck].ChargeMelee != null) selected = u.Loadouts[u.Deck].ChargeMelee;
            if (selected == null || u.Boost < selected.boostCost || u.BoostOverheated && selected.boostCost>0) return;
            u.ChargeStart = -1; EndBeam(u);
            if (selected.style == MeleeStyle.Charge)
            { u.MeleeAttack = selected; u.MeleeCharging = true; u.MeleeHitTick = CurrentTick; u.ComboStep = 0; return; }
            StartMelee(u, selected, 1, 1);
        }
        // Deliberately separate from PrepareMelee (user request 2026-10-16): only a fresh,
        // non-boosted MeleeStyle.Normal swing may fire while the unit is staggered/knocked
        // down. Combo/Dash/DashCombo/Charge, and continuing an existing combo, stay locked
        // out - this is a single narrow exception, not a general relaxation of the disabled
        // window. RequestedMeleeStyle already resolves to Dash/DashCombo whenever boost
        // motion is held, so filtering for exactly MeleeStyle.Normal also excludes those.
        private void TryNormalMeleeWhileDisabled(UnitState u)
        {
            if (config.meleeAttacks == null || config.meleeAttacks.Length == 0) return;
            if (config.equipmentRulesEnabled && ActiveWeapon(u).Kind != WeaponKind.Melee) return;
            bool held = u.HasCommand && (u.Command.Melee || (ActiveWeapon(u).Kind == WeaponKind.Melee && u.Command.FirePrimary));
            if (!held || u.MeleeCharging || u.MeleeHeld || CurrentTick < u.MeleeUntil || CurrentTick < u.NextMelee) return;
            if (RequestedMeleeStyle(u) != MeleeStyle.Normal) return;
            MeleeAttackDefinition selected = null;
            foreach (var a in config.meleeAttacks) if (a.style == MeleeStyle.Normal) { selected = a; break; }
            if (selected == null || u.Boost < selected.boostCost || u.BoostOverheated && selected.boostCost > 0) return;
            StartMelee(u, selected, 1, 1);
            // Set after StartMelee (which always clears it first): DisabledUntil may still be in
            // the future when the windup finishes (stagger/knockdown usually outlasts a Normal
            // swing's windup), so flag this one swing so Melee()'s own disabled check doesn't
            // also block the hit it was just explicitly allowed to start.
            u.MeleeBypassingDisabled = true;
        }
        private void StartMelee(UnitState u, MeleeAttackDefinition attack, float power, int comboStep)
        {
            u.MeleeBypassingDisabled=false;
            if (u.Boost < attack.boostCost || u.BoostOverheated && attack.boostCost>0) { u.MeleeAttack = null; return; }
            u.MeleeBoostSpent=attack.boostCost;u.MeleeHitAdjusted=false;u.MeleeRecoverySeconds=attack.recoverySeconds;
            ConsumeBoost(u,attack.boostCost); u.MeleeAttack = attack; u.MeleePower = power; u.ComboStep = comboStep;
            u.LastMelee = CurrentTick; u.MeleeHitTick = CurrentTick + SecondsToTicks(attack.windupSeconds);
            u.MeleeUntil = u.MeleeHitTick + SecondsToTicks(attack.recoverySeconds); u.MeleePending = true; u.MeleeYaw = u.AimYaw;
            u.NextMelee = u.MeleeUntil + SecondsToTicks(attack.cooldownSeconds); u.ChargeStart = -1; EndBeam(u);
            u.MeleeOriginX = u.X; u.MeleeOriginZ = u.Z;
            // Authorised attack input causes the lunge; no target acquisition or automatic counter.
            double yaw = u.MeleeYaw * Math.PI / 180;
            if(config.seamlessMeleeEnabled)u.MeleeLungeRemaining=attack.lungeMeters*power;
            else MoveHorizontal(u, (float)Math.Sin(yaw) * attack.lungeMeters * power, (float)Math.Cos(yaw) * attack.lungeMeters * power);
        }
        private void Melee(UnitState u)
        {
            if (config.meleeAttacks == null || config.meleeAttacks.Length == 0) { LegacyMelee(u); return; }
            if (!u.Alive || !u.MeleePending || CurrentTick < u.MeleeHitTick) return;
            if (CurrentTick < u.DisabledUntil && !u.MeleeBypassingDisabled) return;
            u.MeleeBypassingDisabled = false;
            u.MeleePending = false; var a = u.MeleeAttack; if (a == null) return;
            double yaw = u.MeleeYaw * Math.PI / 180, fx = Math.Sin(yaw), fz = Math.Cos(yaw), cosine = Math.Cos(a.halfAngleDegrees * Math.PI / 180);
            // Arm-type-specific charge melee (user request 2026-10-16): a standard-arm charge
            // attack spends its charge on area instead of power (fixed damage/downPoints,
            // range grows from minimumRange to range); a reinforced-arm charge attack spends
            // it on power through a two-phase curve (flat/weak below the threshold, then
            // scaling up to the full damage/downPoints value). Every other attack (including
            // the shared global Charge fallback) keeps the original single "power" scalar.
            double effectiveRange = a.range, effectiveDamage = a.damage, effectiveDownPoints = a.downPoints;
            if (a.chargeScalesRange) effectiveRange = a.minimumRange + (a.range - a.minimumRange) * Math.Clamp(u.MeleePower, 0f, 1f);
            else if (a.thresholdChargeFraction > 0)
            {
                if (u.MeleePower < a.thresholdChargeFraction) { effectiveDamage = a.belowThresholdDamage; effectiveDownPoints = a.belowThresholdDownPoints; }
                else
                {
                    float t = Math.Clamp(u.MeleePower, 0f, 1f);
                    effectiveDamage = a.belowThresholdDamage + (a.damage - a.belowThresholdDamage) * t;
                    effectiveDownPoints = a.belowThresholdDownPoints + (a.downPoints - a.belowThresholdDownPoints) * t;
                }
            }
            else { effectiveDamage = a.damage * u.MeleePower; effectiveDownPoints = a.downPoints * u.MeleePower; }
            double verticalTolerance = config.meleeVerticalToleranceMeters > 0 ? config.meleeVerticalToleranceMeters : effectiveRange * 0.75f;
            // Select nearest eligible targets deterministically, without allocating a hit list.
            double previous = -1; int previousSlot = -1, unitsHit=0;
            for (int hitCount = 0; hitCount < a.maximumTargets; hitCount++)
            {
                double best = double.PositiveInfinity; int target = -1;
                for (int i = 0; i < units.Length; i++)
                {
                    var t = units[i]; if (!t.Alive || t.Team == u.Team) continue;
                    double dx = t.X - u.X, dz = t.Z - u.Z, d = Math.Sqrt(dx * dx + dz * dz);
                    if (Math.Abs(t.Y - u.Y) > verticalTolerance || !ClearSegment(u.X, u.Y + ShotHeight, u.Z, t.X, t.Y + ShotHeight, t.Z)) continue;
                    // A long lunge (task #38, e.g. Dash's 5m) can carry the attacker past a
                    // target that was already within range at windup start, leaving the target
                    // behind the final-position facing cone. Accept either the pre-lunge origin
                    // or the post-lunge final position so the attacker's own follow-through
                    // cannot overshoot a target it was already correctly aimed at.
                    double ox = t.X - u.MeleeOriginX, oz = t.Z - u.MeleeOriginZ, od = Math.Sqrt(ox * ox + oz * oz);
                    bool inFinalArc = d <= effectiveRange && (d < 0.0001 || (dx * fx + dz * fz) / d >= cosine);
                    bool inOriginArc = od <= effectiveRange && (od < 0.0001 || (ox * fx + oz * fz) / od >= cosine);
                    if (!inFinalArc && !inOriginArc) continue;
                    double rank = Math.Min(d, od);
                    if (rank < previous || (rank == previous && i <= previousSlot)) continue;
                    if (rank < best) { best = rank; target = i; }
                }
                if (target < 0) break;
                unitsHit++;RegisterLockedHit(target,u.Slot,u.Id,effectiveDamage);
                RecordImpact(target,u.Slot,u.Id,effectiveDamage,0,true);
                unitDamage[target] += effectiveDamage; unitDown[target] += effectiveDownPoints; previous = best; previousSlot = target;
            }
            HitDeployablesInMelee(u,a,(float)(effectiveDamage/a.damage),u.MeleeYaw,a.maximumTargets-unitsHit);
            for (int i = 0; i < bases.Length; i++)
            {
                var b = bases[i]; if (b.Team == u.Team || b.Destroyed || (b.BuildingRadius > 0 && DistanceSquared(u.X, u.Z, b.X, b.Z) > (double)b.BuildingRadius * b.BuildingRadius)) continue;
                double dx = b.X - u.X, dz = b.Z - u.Z, d = Math.Sqrt(dx * dx + dz * dz);
                if (config.aimIn3DEnabled && (u.Y>b.Y+config.coreHeight+effectiveRange || u.Y+effectiveRange<b.Y)) continue;
                if (!ClearSegment(u.X, u.Y + ShotHeight, u.Z, b.X, Math.Max(b.Y+0.1f, Math.Min(b.Y+config.coreHeight, u.Y + ShotHeight)), b.Z)) continue;
                // Same origin-or-final overshoot allowance as the unit loop above (task #38).
                double obx = b.X - u.MeleeOriginX, obz = b.Z - u.MeleeOriginZ, obd = Math.Sqrt(obx * obx + obz * obz);
                bool baseInFinalArc = d <= effectiveRange + b.AttackRadius && (d < 0.0001 || (dx * fx + dz * fz) / d >= cosine);
                bool baseInOriginArc = obd <= effectiveRange + b.AttackRadius && (obd < 0.0001 || (obx * fx + obz * fz) / obd >= cosine);
                if (baseInFinalArc || baseInOriginArc) { RecordCoreHit(i,u.Slot,u.Id,effectiveDamage,true); baseDamage[i] += effectiveDamage; baseHits++; }
            }
        }
    }
}
