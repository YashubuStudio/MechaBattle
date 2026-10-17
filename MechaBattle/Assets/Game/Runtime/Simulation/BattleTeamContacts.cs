using System;
using MechaBattle.Contracts;
namespace MechaBattle.Simulation
{
    public readonly struct TeamContactSnapshot
    {
        public readonly bool Valid, HasHealth, RadarDetected, LockDetected;
        public readonly int Slot, UnitId;
        public readonly float X,Y,Z,Health,MaxHealth,AgeSeconds,HealthAgeSeconds;
        internal TeamContactSnapshot(bool valid,bool hp,bool radar,bool locked,int slot,int id,float x,float y,float z,float health,float max,float age,float hpAge)
        {Valid=valid;HasHealth=hp;RadarDetected=radar;LockDetected=locked;Slot=slot;UnitId=id;X=x;Y=y;Z=z;Health=health;MaxHealth=max;AgeSeconds=age;HealthAgeSeconds=hpAge;}
    }
    public sealed partial class BattleSimulation
    {
        private struct TeamContact
        {
            public int Id;public float X,Y,Z,Health,MaxHealth;
            public long Seen,HealthSeen,LockUntil,RadarUntil;
        }
        private readonly TeamContact[,] teamContacts=new TeamContact[2,100];
        public TeamContactSnapshot GetTeamContact(int team,int slot)
        {
            CheckTeam(team);var u=Unit(slot);var c=teamContacts[team,slot];
            bool hp=c.Id>0 && CurrentTick<c.LockUntil,radar=c.Id>0 && CurrentTick<c.RadarUntil;
            bool valid=u.Alive && u.Team!=team && c.Id==u.Id && (hp||radar) && !IsMapJammed(team,c.X,c.Z);
            return new TeamContactSnapshot(valid,valid&&hp,valid&&radar,valid&&hp,slot,c.Id,c.X,c.Y,c.Z,c.Health,c.MaxHealth,Math.Max(0,(CurrentTick-c.Seen)*FixedDeltaTime),Math.Max(0,(CurrentTick-c.HealthSeen)*FixedDeltaTime));
        }
        private void ObserveForTeam(int team,UnitState target,bool locked)
        {
            if(IsMapJammed(team,target.X,target.Z))return;
            var c=teamContacts[team,target.Slot];if(c.Id!=target.Id)c=default;
            long observed=CurrentTick+1;c.Id=target.Id;c.X=target.X;c.Y=target.Y;c.Z=target.Z;c.Seen=observed;
            if(locked)
            {
                c.Health=target.Health;c.MaxHealth=target.Loadouts[target.Deck].MaxHealth;c.HealthSeen=observed;
                c.LockUntil=observed+SecondsToTicks(config.sharedLockHealthSeconds);
            }
            else c.RadarUntil=observed+SecondsToTicks(config.radarContactSeconds);
            teamContacts[team,target.Slot]=c;
        }
        private void UpdateTeamContacts()
        {
            foreach(var source in units)
            {
                if(!source.Alive || !source.HasCommand)continue;
                int slot=source.Command.LockedTargetSlot;
                if(slot>=0 && units[slot].Id==source.Command.LockedTargetUnitId && IsYellowLocked(source.Slot,slot))ObserveForTeam(source.Team,units[slot],true);
            }
            if(CurrentTick%SecondsToTicks(config.radarScanSeconds)!=0)return;
            foreach(var d in deployables)
            {
                if(!d.Active || d.Weapon.Kind!=WeaponKind.Radar || CurrentTick<d.Armed || CurrentTick>=d.Expires)continue;
                double radius=d.Weapon.UtilityRadius;
                foreach(var target in units)
                {
                    if(!target.Alive || target.Team==d.Team || DistanceSquared(target.X,target.Z,d.X,d.Z)+(target.Y-d.Y)*(target.Y-d.Y)>radius*radius)continue;
                    // Radar reports bounded positions through cover, never HP or a firing lock.
                    ObserveForTeam(d.Team,target,false);
                }
            }
        }
    }
}
