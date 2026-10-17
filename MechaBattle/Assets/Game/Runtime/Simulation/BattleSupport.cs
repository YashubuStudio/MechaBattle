using System;
using MechaBattle.Contracts;
namespace MechaBattle.Simulation
{
    public sealed partial class BattleSimulation
    {
        private readonly int[] repairIntents=new int[100];
        private void FireRepairGun(UnitState source,UnitLoadout w,double dx,double dy,double dz)
        {
            FindRepairHit(source.Slot,source.X,source.Y+ShotHeight,source.Z,dx,dy,dz,w.FireRange,out double distance,out int target);
            source.ShotEndX=source.X+(float)(dx*distance);source.ShotEndY=source.Y+ShotHeight+(float)(dy*distance);source.ShotEndZ=source.Z+(float)(dz*distance);
            // Friendly HP only; enemies block the repair ray and can never be healed.
            if(target<0 || units[target].Team!=source.Team)return;
            repairIntents[source.Slot]=target+1; // Source-to-target intent, resolved after simultaneous damage.
        }
        private void FindRepairHit(int source,float x,float y,float z,double dx,double dy,double dz,double range,out double distance,out int target)
        {
            distance=range;target=-1;
            foreach(var o in obstacles)distance=Math.Min(distance,MechaBattle.Battlefield.MapGeometry.RayBox(o,x,y,z,dx,dy,dz));
            foreach(var d in deployables)if(d.Active && d.Health>0 && d.Team!=units[source].Team)
            {double hit=config.aimIn3DEnabled?RayCylinder(x,y-d.Y,z,dx,dy,dz,d.X,d.Z,DeployableRadius(d),DeployableHeight(d)):RayCircle(x,z,dx,dz,d.X,d.Z,DeployableRadius(d));distance=Math.Min(distance,hit);}
            for(int i=0;i<units.Length;i++)
            {
                var u=units[i];if(!u.Alive || i==source)continue;
                double hit=config.aimIn3DEnabled?RayCylinder(x,y-u.Y,z,dx,dy,dz,u.X,u.Z,config.unitRadius,config.unitHeight):RayCircle(x,z,dx,dz,u.X,u.Z,config.unitRadius);
                if(hit<distance){distance=hit;target=i;}
            }
        }
        // Called only for actual healing, never for the full-health reset on respawn.
        // Attribution belongs to the controller slot even after its pod owner respawns.
        private float RestoreHealth(UnitState target, float amount, int source = -1)
        {
            float before = target.Health;
            target.Health = Math.Min(target.Loadouts[target.Deck].MaxHealth, before + amount);
            float restored = target.Health - before;
            if (restored > 0)
            {
                target.HealingReceived += restored;
                if (source >= 0 && source != target.Slot) units[source].HealingDealt += restored;
            }
            return restored;
        }
        private void ResolveRepairs()
        {
            for(int i=0;i<repairIntents.Length;i++)
            {
                int target=(int)repairIntents[i]-1;repairIntents[i]=0;if(target<0)continue;
                var source=units[i];var u=units[target];if(!u.Alive)continue;
                var w=ActiveWeapon(source);float amount=RestoreHealth(u,w.FireDamage,i);
                if(amount<=0)continue;
                EmitCombat(CombatEventKind.Healed,i,source.Id,target,u.Id,amount,0,u.X,u.Y+ShotHeight,u.Z);
            }
        }
    }
}
