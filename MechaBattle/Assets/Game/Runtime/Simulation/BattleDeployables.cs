using System;
using MechaBattle.Contracts;
using MechaBattle.Battlefield;
namespace MechaBattle.Simulation
{
    public sealed partial class BattleSimulation
    {
        private struct Deployable
        {
            public bool Active; public int Team, Owner, OwnerId, Id; public float X, Y, Z, Health; public long Armed, Expires;
            public UnitLoadout Weapon;
        }
        public const int RadarPerOwnerLimit=3;
        private const int RepairPodPerOwnerLimit=3,MinePerOwnerLimit=5;
        // Every controller may maintain all three kinds, plus one portable nuclear per team.
        public const int MaximumDeployables=MatchLimits.TotalControllerSlots*(RadarPerOwnerLimit+RepairPodPerOwnerLimit+MinePerOwnerLimit)+MatchLimits.TeamCount;
        private readonly Deployable[] deployables = new Deployable[MaximumDeployables];
        private int nextDeployableId;
        private readonly int[] deployableDamageIds=new int[MaximumDeployables];
        private readonly double[] deployableDamage=new double[MaximumDeployables];
        private readonly int[] deployableImpactHeads=new int[MaximumDeployables];
        private static float DeployableHeight(Deployable d)=>d.Weapon.Kind==WeaponKind.Mine?0.24f:d.Weapon.Kind==WeaponKind.Radar?1.6f:d.Weapon.Kind==WeaponKind.StrategicNuclear?1.25f:0.9f;
        private static float DeployableRadius(Deployable d)=>d.Weapon.Kind==WeaponKind.Mine?0.45f:d.Weapon.Kind==WeaponKind.StrategicNuclear?0.7f:0.55f;
        public int DeployableCapacity => deployables.Length;
        public DeployableSnapshot GetDeployable(int index)
        {
            if (index < 0 || index >= deployables.Length) throw new ArgumentOutOfRangeException(nameof(index));
            var d = deployables[index]; return new DeployableSnapshot(d.Active && CurrentTick<d.Expires, d.Team, d.Owner, d.X, d.Y, d.Z, d.Weapon == null ? 0 : d.Weapon.UtilityRadius,
                d.Weapon == null ? WeaponKind.RepairPod : d.Weapon.Kind, d.Armed <= CurrentTick, Remaining(d.Expires), d.Id, d.Health, d.Weapon==null?0:d.Weapon.UtilityHealth, d.Weapon==null?0:DeployableRadius(d), d.Weapon==null?0:DeployableHeight(d));
        }
        private bool PlaceDeployable(UnitState u, UnitLoadout w)
        {
            if (!u.Grounded) return false;
            double yaw = u.AimYaw * Math.PI / 180; float x = u.X + (float)Math.Sin(yaw) * (config.unitRadius + 0.65f), z = u.Z + (float)Math.Cos(yaw) * (config.unitRadius + 0.65f), y = u.Y + 0.04f;
            // An owner can stand at a platform edge while its placement point hangs
            // beyond the ledge. Require support at the placement height, not only under
            // the owner, so pods/mines never float or consume a charge on invalid ground.
            if (config.traversalEnabled && u.Y > 0.001f)
            {
                float support = float.NegativeInfinity;
                foreach (var o in obstacles)
                    if (Math.Abs(u.Y - (o.y + o.height / 2)) <= config.maximumStepHeight + 0.001f
                        && x - 0.35f >= o.x - o.width / 2 && x + 0.35f <= o.x + o.width / 2
                        && z - 0.35f >= o.z - o.depth / 2 && z + 0.35f <= o.z + o.depth / 2)
                        support = Math.Max(support, o.y + o.height / 2);
                if (float.IsNegativeInfinity(support)) return false;
                y = support + 0.04f;
            }
            if (!ClearSegment(u.X, u.Y + ShotHeight, u.Z, x, y + 0.25f, z)) return false;
            foreach (var o in obstacles) if (MapGeometry.CircleOverlaps(o, x, z, 0.35f) && y >= o.y - o.height / 2 && y < o.y + o.height / 2) return false;
            int free = -1, count = 0, oldest = -1;
            for (int i = 0; i < deployables.Length; i++)
            {
                var d = deployables[i]; if (!d.Active || CurrentTick>=d.Expires) { if (free < 0) free = i; continue; }
                if (d.Owner != u.Slot || d.Weapon.Kind != w.Kind) continue;
                count++; if (oldest < 0 || d.Id < deployables[oldest].Id) oldest = i;
            }
            int limit=w.Kind==WeaponKind.Mine?MinePerOwnerLimit:w.Kind==WeaponKind.Radar?RadarPerOwnerLimit:RepairPodPerOwnerLimit;
            if (count >= limit) free = oldest;
            if (free < 0) return false;
            deployables[free] = new Deployable { Active = true, Id=checked(++nextDeployableId),Health=w.UtilityHealth, Team = u.Team, Owner = u.Slot, OwnerId = u.Id, X = x, Y = y, Z = z,
                Armed = CurrentTick + SecondsToTicks(w.Kind == WeaponKind.Mine ? 0.8f : 0.2f), Expires = CurrentTick + SecondsToTicks(w.UtilityDuration), Weapon = w };
            u.ShotEndX = x; u.ShotEndY = y; u.ShotEndZ = z; return true;
        }
        private void AdvanceDeployables()
        {
            for (int i = 0; i < deployables.Length; i++)
            {
                var d = deployables[i]; if (!d.Active) continue;
                if (CurrentTick >= d.Expires) { d.Active = false; deployables[i] = d; continue; }
                if (d.Weapon.Kind != WeaponKind.Mine || CurrentTick < d.Armed) continue;
                foreach (var u in units)
                {
                    if (!u.Alive || u.Team == d.Team || DistanceSquared(d.X, d.Z, u.X, u.Z) + (d.Y - u.Y) * (d.Y - u.Y) > d.Weapon.UtilityRadius * d.Weapon.UtilityRadius
                        || !ClearSegment(d.X, d.Y + 0.2f, d.Z, u.X, u.Y + ShotHeight, u.Z)) continue;
                    var p = new Projectile { Team = d.Team, OwnerSlot = d.Owner, OwnerUnitId = d.OwnerId, X = d.X, Y = d.Y + 0.2f, Z = d.Z, OriginX = d.X, OriginZ = d.Z, Weapon = d.Weapon, Power = 1 };
                    Explode(p, -1, -1); d.Active = false; break;
                }
                deployables[i] = d;
            }
        }
        private void RepairFromPods()
        {
            foreach (var d in deployables)
            {
                if (!d.Active || d.Weapon.Kind != WeaponKind.RepairPod || CurrentTick < d.Armed) continue;
                foreach (var u in units)
                {
                    if (!u.Alive || u.Team != d.Team || DistanceSquared(d.X, d.Z, u.X, u.Z) + (d.Y - u.Y) * (d.Y - u.Y) > d.Weapon.UtilityRadius * d.Weapon.UtilityRadius
                        || !ClearSegment(d.X, d.Y + 0.25f, d.Z, u.X, u.Y + ShotHeight, u.Z)) continue;
                    var frame = u.Loadouts[u.Deck]; RestoreHealth(u,d.Weapon.FireDamage * FixedDeltaTime,d.Owner);
                    u.Shield = Math.Min(frame.MaxShield, u.Shield + (d.Weapon.PodShieldPerSecond > 0 ? d.Weapon.PodShieldPerSecond : d.Weapon.FireDamage * 0.4f) * FixedDeltaTime);
                    ResupplyAmmo(u,d.Weapon.PodAmmoPerSecond);
                }
            }
        }
    }
}
