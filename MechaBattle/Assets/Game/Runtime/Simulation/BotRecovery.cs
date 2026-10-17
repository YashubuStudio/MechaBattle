using System;
using MechaBattle.Contracts;
using MechaBattle.Battlefield;
namespace MechaBattle.Simulation
{
    public sealed partial class BotTraversalAdapter
    {
        private readonly bool[] retreating = new bool[100], recoveryCover = new bool[100];
        private readonly long[] seenLockedHit = new long[100], coverUntil = new long[100];
        private readonly float[] recoveryX = new float[100], recoveryZ = new float[100];
        private void ResetRecovery(int slot)
        { retreating[slot] = recoveryCover[slot] = false; seenLockedHit[slot] = -1; }

        private void UpdateRecovery(UnitSnapshot self, UnitLoadout frame, long tick, int visibleSlot, float tx, float ty, float tz)
        {
            if (!config.botLockRetreatEnabled) return;
            int slot = self.SlotId;
            bool lockedHit = self.LastLockedHitTick > seenLockedHit[slot];
            seenLockedHit[slot] = self.LastLockedHitTick;
            if (!retreating[slot] && (lockedHit || self.Health < frame.MaxHealth * config.botRetreatHealthFraction && !InsideAssaultObjective(self)))
            {
                retreating[slot] = true; waypoint[slot] = false;
                recoveryCover[slot] = !resupplying[slot] && !podPlaced[slot] && HasRecoveryPod(slot, frame)
                    && visibleSlot >= 0 && FindRecoveryCover(self, tx, ty, tz, out recoveryX[slot], out recoveryZ[slot]);
                // Includes travel time and the finite pod's lifetime. Failure to heal
                // at this position falls back to ordinary home supply, never free HP.
                float duration = 0;
                for (int i = 0; i < frame.WeaponCount; i++) if (frame.GetWeapon(i).Kind == WeaponKind.RepairPod) duration = Math.Max(duration, frame.GetWeapon(i).UtilityDuration);
                coverUntil[slot] = tick + (long)Math.Ceiling((duration + config.botCoverSearchRadius / Math.Max(1,frame.MoveSpeed) + config.botRecoveryQuietSeconds + 2) * config.tickHz);
            }
            if (!retreating[slot]) return;
            bool quiet = self.LastDamageTick < 0 || (tick-self.LastDamageTick)/(float)config.tickHz >= config.botRecoveryQuietSeconds;
            if (self.Health >= frame.MaxHealth * config.botRecoveryHealthFraction - 0.01f
                && self.Shield >= frame.MaxShield * config.botRecoveryShieldFraction - 0.01f
                && quiet)
            { retreating[slot] = recoveryCover[slot] = waypoint[slot] = false; podPlaced[slot] = false; return; }
            float dx = recoveryX[slot]-self.X, dz = recoveryZ[slot]-self.Z;
            if (recoveryCover[slot] && (tick > coverUntil[slot] || resupplying[slot]
                || lockedHit && dx*dx+dz*dz < 9))
            { recoveryCover[slot] = false; waypoint[slot] = false; }
        }
        private bool InsideAssaultObjective(UnitSnapshot self)
        {
            if(!config.botCentralApproachEnabled)return false;
            int local=self.SlotId%50;if(local%9!=0 && local%config.botAssaultModulo!=0)return false;
            foreach(var core in map.mainBases)if(core.team!=self.Team && core.buildingRadius>0
                && (core.x-self.X)*(core.x-self.X)+(core.z-self.Z)*(core.z-self.Z)<core.buildingRadius*core.buildingRadius)return true;
            return false;
        }
        private bool HasRecoveryPod(int slot, UnitLoadout frame)
        {
            for (int i = 0; i < frame.WeaponCount; i++)
                if (frame.GetWeapon(i).Kind == WeaponKind.RepairPod && !exhausted[slot,i]) return true;
            return false;
        }
        private bool CanDeployRecoveryPod(UnitSnapshot self, long tick, int visibleSlot)
        {
            int slot = self.SlotId; float dx = recoveryX[slot]-self.X, dz = recoveryZ[slot]-self.Z;
            return recoveryCover[slot] && dx*dx+dz*dz <= 9 && self.IncomingLockCount == 0 && visibleSlot < 0
                && (self.LastDamageTick < 0 || (tick-self.LastDamageTick)/(float)config.tickHz >= config.botRecoveryQuietSeconds);
        }
        private void ApplyRecoveryMotion(UnitSnapshot self, UnitLoadout active, long tick, int visibleSlot,
            ref float mx, ref float mz, ref bool fire, ref bool melee, ref bool boost, ref float yaw)
        {
            int slot = self.SlotId; float x = recoveryX[slot], z = recoveryZ[slot], stop = 1.5f;
            if (!recoveryCover[slot])
            {
                float best = float.PositiveInfinity;
                foreach (var home in map.mainBases) if (home.team == self.Team&&!BaseFallen(Array.IndexOf(map.mainBases,home)))
                { float d = (home.x-self.X)*(home.x-self.X)+(home.z-self.Z)*(home.z-self.Z)+4*(home.y-self.Y)*(home.y-self.Y); if (d < best) { best = d; x = home.x; z = home.z; } }
                stop = config.homeSupplyRadius * 0.5f;
            }
            float dx = x-self.X, dz = z-self.Z, distance = (float)Math.Sqrt(dx*dx+dz*dz);
            mx = distance > stop ? dx/Math.Max(0.001f,distance) : 0;
            mz = distance > stop ? dz/Math.Max(0.001f,distance) : 0;
            if (distance <= stop) waypoint[slot] = false;
            melee = false; fire = active.Kind == WeaponKind.RepairPod && CanDeployRecoveryPod(self,tick,visibleSlot);
            boost = distance > stop+3 && self.Boost > 15;
            // Hold frontal shield toward the observed threat while moving away. No
            // enemy coordinates are available once that bounded contact is lost.
            if (visibleSlot < 0 && distance > stop) yaw = (float)(Math.Atan2(dx,dz)*180/Math.PI);
        }
        private bool FindRecoveryCover(UnitSnapshot self, float tx, float ty, float tz, out float x, out float z)
        {
            x = self.X; z = self.Z; float best = float.PositiveInfinity;
            float margin = config.unitRadius + 1.5f;
            foreach (var wall in map.obstacles ?? Array.Empty<ObstacleDefinition>())
            {
                if (wall.y+wall.height/2 < self.Y+config.shotOriginHeight) continue;
                // Public geometry only; test eight points outside the known solid.
                for (int ix = -1; ix <= 1; ix++) for (int iz = -1; iz <= 1; iz++)
                {
                    if (ix == 0 && iz == 0) continue;
                    float cx = wall.x+ix*(wall.width/2+margin), cz = wall.z+iz*(wall.depth/2+margin);
                    float dx = cx-self.X, dz = cz-self.Z, distance = (float)Math.Sqrt(dx*dx+dz*dz);
                    if (distance < 0.5f || distance > config.botCoverSearchRadius || distance >= best
                        || Math.Abs(cx) > map.halfWidth-config.unitRadius || Math.Abs(cz) > map.halfDepth-config.unitRadius) continue;
                    if (Blocking(self.X,self.Y,self.Z,dx/distance,dz/distance,distance,true) != null) continue;
                    float cy = 0; bool clear = true;
                    foreach (var solid in map.obstacles ?? Array.Empty<ObstacleDefinition>())
                    {
                        if (!MapGeometry.CircleOverlaps(solid,cx,cz,config.unitRadius)) continue;
                        float top = solid.y+solid.height/2;
                        if (top > self.Y+config.maximumStepHeight && solid.y-solid.height/2 < self.Y+config.unitHeight) { clear = false; break; }
                        if (top <= self.Y+config.maximumStepHeight) cy = Math.Max(cy,top);
                    }
                    if (!clear) continue;
                    double vx = cx-tx, vy = cy-ty, vz = cz-tz, length = Math.Sqrt(vx*vx+vy*vy+vz*vz);
                    if (length < 0.001 || MapGeometry.RayBox(wall,tx,ty+config.shotOriginHeight,tz,vx/length,vy/length,vz/length) >= length) continue;
                    best = distance; x = cx; z = cz;
                }
            }
            return best < float.PositiveInfinity;
        }
    }
}
