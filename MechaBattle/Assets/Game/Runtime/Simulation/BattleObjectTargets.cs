using System;
using MechaBattle.Contracts;
namespace MechaBattle.Simulation
{
    public sealed partial class BattleSimulation
    {
        public bool HasDeployableLineOfSight(int from,int index)
        {
            var source=Unit(from);if(index<0||index>=deployables.Length)return false;var d=deployables[index];
            return source.Alive && d.Active && ClearSegment(source.X,source.Y+ShotHeight,source.Z,d.X,d.Y+DeployableHeight(d)*0.5f,d.Z);
        }
        public bool IsDeployableYellowLocked(int from,int index,int id)
        {
            var source=Unit(from);if(index<0||index>=deployables.Length)return false;var d=deployables[index];float range=GetYellowLockRange(from);
            float dy=d.Y+DeployableHeightSafe(d)*0.5f-source.Y-ShotHeight;
            return source.Alive && d.Active && CurrentTick<d.Expires && d.Id==id && d.Health>0 && d.Team!=source.Team && range>0
                && DistanceSquared(source.X,source.Z,d.X,d.Z)+dy*dy<=range*range && HasDeployableLineOfSight(from,index);
        }
        private static float DeployableHeightSafe(Deployable d)=>d.Weapon==null?0:DeployableHeight(d);
        public bool IsDeployableRedLocked(int from,int index,int id)
        {
            if(!IsDeployableYellowLocked(from,index,id))return false;var source=Unit(from);var d=deployables[index];var w=ActiveWeapon(source);
            float range=w.RedLockRange>0?w.RedLockRange:config.redLockRange;
            float dy=d.Y+DeployableHeight(d)*0.5f-source.Y-ShotHeight;
            return DistanceSquared(source.X,source.Z,d.X,d.Z)+dy*dy<=range*range;
        }
        private void DamageDeployable(int index,int source,int sourceId,double amount,bool melee=false)
        {
            if(index<0 || amount<=0)return;var d=deployables[index];if(!d.Active || d.Health<=0)return;
            if(deployableDamageIds[index]!=d.Id){deployableDamageIds[index]=d.Id;deployableDamage[index]=0;deployableImpactHeads[index]=-1;}
            deployableDamage[index]+=amount;
            for(int i=deployableImpactHeads[index];i>=0;i=pendingImpacts[i].Next)
                if(pendingImpacts[i].Source==source && pendingImpacts[i].SourceId==sourceId){pendingImpacts[i].Direct+=amount;if(melee)pendingImpacts[i].Melee+=amount;return;}
            if(impactCount>=pendingImpacts.Length)throw new InvalidOperationException("Combat attribution capacity exceeded.");
            pendingImpacts[impactCount]=new PendingImpact{Next=deployableImpactHeads[index],Source=source,SourceId=sourceId,Direct=amount,Melee=melee?amount:0};deployableImpactHeads[index]=impactCount++;
        }
        private void ResolveDeployableDamage()
        {
            for(int index=0;index<deployables.Length;index++)
            {
                var d=deployables[index];double total=deployableDamage[index];if(total<=0)continue;deployableDamage[index]=0;
                if(!d.Active || d.Health<=0 || deployableDamageIds[index]!=d.Id){deployableImpactHeads[index]=-1;continue;}
                double actual=Math.Min(d.Health,total),best=0;int killer=-1,killerId=0;d.Health=(float)Math.Max(0,d.Health-total);
                for(int i=deployableImpactHeads[index];i>=0;i=pendingImpacts[i].Next)
                {
                    var p=pendingImpacts[i];float amount=(float)(actual*p.Direct/total);
                    if(p.Direct>best || p.Direct==best && p.Source<killer){best=p.Direct;killer=p.Source;killerId=p.SourceId;}
                    EmitCombat(CombatEventKind.ObjectHit,p.Source,p.SourceId,-1,d.Id,amount,0,d.X,d.Y+DeployableHeight(d)*0.5f,d.Z);
                    if(amount>0 && p.Melee>0)ConfirmMeleeHit(p.Source,p.SourceId);
                }
                if(d.Health<=0){d.Active=false;EmitCombat(CombatEventKind.ObjectDestroyed,killer,killerId,-1,d.Id,0,0,d.X,d.Y+DeployableHeight(d)*0.5f,d.Z);}
                deployables[index]=d;deployableImpactHeads[index]=-1;
            }
        }
        private void HitDeployablesInBlast(Projectile p,int direct)
        {
            for(int i=0;i<deployables.Length;i++)
            {
                var d=deployables[i];if(!d.Active || d.Health<=0 || d.Team==p.Team || i==direct)continue;
                float centerY=d.Y+DeployableHeight(d)*0.5f;
                double horizontal=Math.Max(0,Math.Sqrt(DistanceSquared(d.X,d.Z,p.X,p.Z))-DeployableRadius(d));
                double vertical=Math.Max(0,Math.Abs(p.Y-centerY)-DeployableHeight(d)*0.5f);
                double distance=Math.Sqrt(horizontal*horizontal+vertical*vertical);
                if(distance>=p.Weapon.BlastRadius || !ClearSegment(p.X,p.Y,p.Z,d.X,centerY,d.Z))continue;
                DamageDeployable(i,p.OwnerSlot,p.OwnerUnitId,p.Weapon.SplashDamage*(1-distance/p.Weapon.BlastRadius));
            }
        }
        private void HitDeployablesInMelee(UnitState source,MeleeAttackDefinition attack,float power,float attackYaw,int maximum)
        {
            double yaw=attackYaw*Math.PI/180,fx=Math.Sin(yaw),fz=Math.Cos(yaw),cosine=Math.Cos(attack.halfAngleDegrees*Math.PI/180),previous=-1;int previousIndex=-1;
            for(int count=0;count<maximum;count++)
            {
                double best=double.PositiveInfinity;int target=-1;
                for(int i=0;i<deployables.Length;i++)
                {
                    var d=deployables[i];if(!d.Active || d.Health<=0 || d.Team==source.Team)continue;
                    double dx=d.X-source.X,dz=d.Z-source.Z,distance=Math.Sqrt(dx*dx+dz*dz);
                    if(distance<previous || distance==previous && i<=previousIndex || distance>attack.range+DeployableRadius(d) || Math.Abs(d.Y-source.Y)>attack.range || distance>0.0001 && (dx*fx+dz*fz)/distance<cosine
                        || !HasDeployableLineOfSight(source.Slot,i))continue;
                    if(distance<best){best=distance;target=i;}
                }
                if(target<0)break;DamageDeployable(target,source.Slot,source.Id,attack.damage*power,true);previous=best;previousIndex=target;
            }
        }
    }
}
