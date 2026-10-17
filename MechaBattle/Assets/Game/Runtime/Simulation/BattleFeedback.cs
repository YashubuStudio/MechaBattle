using System;
namespace MechaBattle.Simulation
{
    public enum CombatEventKind { Hit, Destroyed, Healed, CoreHit, ObjectHit, ObjectDestroyed }
    public readonly struct CombatEventSnapshot
    {
        public readonly long Sequence, Tick;
        public readonly CombatEventKind Kind;
        public readonly int SourceSlot, SourceUnitId, TargetSlot, TargetUnitId;
        public readonly float HealthAmount, ShieldAmount, X, Y, Z;
        internal CombatEventSnapshot(long sequence, long tick, CombatEventKind kind, int source, int sourceId, int target, int targetId, float hp, float shield, float x, float y, float z)
        { Sequence=sequence; Tick=tick; Kind=kind; SourceSlot=source; SourceUnitId=sourceId; TargetSlot=target; TargetUnitId=targetId; HealthAmount=hp; ShieldAmount=shield; X=x; Y=y; Z=z; }
    }
    public sealed partial class BattleSimulation
    {
        // Presentation stream; never supplied as enemy intelligence to BOTs.
        private readonly CombatEventSnapshot[] combatEvents = new CombatEventSnapshot[2048];
        public long CombatEventSequence { get; private set; }
        public int CombatEventCapacity => combatEvents.Length;
        public bool TryGetCombatEvent(long sequence, out CombatEventSnapshot result)
        {
            result=default;
            if(sequence<=0 || sequence>CombatEventSequence || CombatEventSequence-sequence>=combatEvents.Length)return false;
            result=combatEvents[(int)((sequence-1)%combatEvents.Length)];return result.Sequence==sequence;
        }
        private void EmitCombat(CombatEventKind kind,int source,int sourceId,int target,int targetId,float hp,float shield,float x,float y,float z)
        {
            long sequence=++CombatEventSequence;
            combatEvents[(int)((sequence-1)%combatEvents.Length)]=new CombatEventSnapshot(sequence,CurrentTick,kind,source,sourceId,target,targetId,hp,shield,x,y,z);
        }
        private struct PendingImpact { public int Next, Source, SourceId; public double Direct, Guarded, Melee; }
        private readonly PendingImpact[] pendingImpacts=new PendingImpact[65536];
        private readonly int[] impactHeads=new int[100];
        private int impactCount;
        private void BeginImpacts(){impactCount=0;for(int i=0;i<impactHeads.Length;i++)impactHeads[i]=-1;
            Array.Clear(deployableDamage,0,deployableDamage.Length);for(int i=0;i<deployableImpactHeads.Length;i++){deployableImpactHeads[i]=-1;if(deployables[i].Active && CurrentTick>=deployables[i].Expires)deployables[i].Active=false;}}
        private void RecordImpact(int target,int source,int sourceId,double direct,double guarded,bool melee=false)
        {
            if(direct+guarded<=0)return;
            for(int i=impactHeads[target];i>=0;i=pendingImpacts[i].Next)
                if(pendingImpacts[i].Source==source && pendingImpacts[i].SourceId==sourceId)
                {pendingImpacts[i].Direct+=direct;pendingImpacts[i].Guarded+=guarded;if(melee)pendingImpacts[i].Melee+=direct+guarded;return;}
            if(impactCount>=pendingImpacts.Length)throw new InvalidOperationException("Combat attribution capacity exceeded.");
            pendingImpacts[impactCount]=new PendingImpact{Next=impactHeads[target],Source=source,SourceId=sourceId,Direct=direct,Guarded=guarded,Melee=melee?direct+guarded:0};impactHeads[target]=impactCount++;
        }
        private void PublishImpacts(UnitState target,double hpLost,double shieldLost,double potentialHp,double guarded)
        {
            target.DamageReceived += hpLost + shieldLost;
            double fraction=guarded>0?shieldLost/guarded:0;
            double scale=potentialHp>0?hpLost/potentialHp:0;
            double best=0;int killer=-1,killerId=0;
            for(int i=impactHeads[target.Slot];i>=0;i=pendingImpacts[i].Next)
            {
                var p=pendingImpacts[i];double guard=p.Guarded,direct=p.Direct;
                if(!config.directionalShieldEnabled){guard+=direct;direct=0;}
                double shield=guard*fraction,hp=(direct+guard-shield)*scale;
                if(hp>best || hp==best && p.Source<killer){best=hp;killer=p.Source;killerId=p.SourceId;}
                if(p.Source>=0 && hp+shield>0.00001)
                {
                    EmitCombat(CombatEventKind.Hit,p.Source,p.SourceId,target.Slot,target.Id,(float)hp,(float)shield,target.X,target.Y+ShotHeight,target.Z);if(p.Melee>0)ConfirmMeleeHit(p.Source,p.SourceId);
                    // Slot statistics survive respawn, including projectiles from a previous unit.
                    units[p.Source].DamageDealt+=hp+shield;
                }
            }
            if(target.Health<=0)
            {
                if(killer>=0)units[killer].Kills++;
                target.Deaths++;
                EmitCombat(CombatEventKind.Destroyed,killer,killerId,target.Slot,target.Id,0,0,target.X,target.Y+ShotHeight,target.Z);
            }
        }
        private void RecordCoreHit(int core,int source,int sourceId,double amount,bool melee=false)
        {
            if(amount<=0)return;var b=bases[core];if(b.Destroyed)return;
            if(b.MaxHealth>0)amount=Math.Min(amount,Math.Max(0,b.Health-baseDamage[core]));if(amount<=0)return;
            EmitCombat(CombatEventKind.CoreHit,source,sourceId,-1,0,(float)amount,0,b.X,b.Y+ShotHeight,b.Z);
            if(source>=0)units[source].CoreDamageDealt+=amount;
            if(melee)ConfirmMeleeHit(source,sourceId);
        }
    }
}
