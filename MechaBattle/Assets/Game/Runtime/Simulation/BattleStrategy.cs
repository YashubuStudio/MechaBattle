using System;
using MechaBattle.Contracts;
namespace MechaBattle.Simulation
{
    public sealed partial class BattleSimulation
    {
        private struct StrategyState
        {
            public bool Active,Detonated; public int Id,Team,Owner,OwnerId; public StrategyKind Kind;
            public float X,Y,Z,DropVelocity; public long Armed,Expires,PickupAfter;
            public NuclearPhase NuclearPhase; public int Carrier,CarrierId,Device,DeviceId;
        }
        private readonly StrategyState[] strategies=new StrategyState[6];
        private readonly int[] strategyPoints=new int[2];
        private readonly long[,] strategyReady=new long[2,3];
        private int nextStrategyId;
        public int StrategyCapacity=>strategies.Length;
        private bool StrategyEnabled=>config.strategy!=null&&config.strategy.enabled;
        public StrategyTeamSnapshot GetStrategyTeam(int team)
        {CheckTeam(team);return new StrategyTeamSnapshot(StrategyEnabled,strategyPoints[team],StrategyEnabled?config.strategy.maximumPoints:0,StrategyEnabled?config.strategy.pointsPerLoss:0);}
        public StrategyDefinition GetStrategyDefinition(StrategyKind kind)
        {CheckStrategyKind(kind);return StrategyEnabled?config.strategy.Get(kind).Copy():null;}
        public float GetStrategyCooldown(int team,StrategyKind kind)
        {CheckTeam(team);CheckStrategyKind(kind);return Remaining(strategyReady[team,(int)kind]);}
        // Enemy non-nuclear supports are not global intelligence. Nuclear warning areas
        // are public to BOTH teams, so everyone can leave before the friendly-fire blast.
        public StrategySnapshot GetStrategy(int index,int viewerTeam)
        {
            CheckTeam(viewerTeam);if(index<0||index>=strategies.Length)throw new ArgumentOutOfRangeException(nameof(index));
            var s=strategies[index];
            if(!StrategyEnabled||!s.Active||CurrentTick>=s.Expires||s.Team!=viewerTeam&&(s.Kind!=StrategyKind.Nuclear||s.NuclearPhase<NuclearPhase.Arming))return default;
            return StrategySnapshotOf(s);
        }
        private StrategySnapshot StrategySnapshotOf(StrategyState s)
        {
            var d=config.strategy.Get(s.Kind);bool device=s.Kind==StrategyKind.Nuclear&&s.Device>=0&&deployables[s.Device].Id==s.DeviceId;
            return new StrategySnapshot(true,CurrentTick>=s.Armed,s.Detonated,s.Id,s.Team,s.Kind,s.X,s.Y,s.Z,d.radius,
                s.Kind==StrategyKind.Nuclear&&s.NuclearPhase<NuclearPhase.Arming?0:Remaining(s.Armed),Remaining(s.Expires),s.NuclearPhase,s.Carrier,device?s.Device:-1,s.DeviceId,device?deployables[s.Device].Health:0,device?config.strategy.nuclearHealth:0);
        }
        // Presentation-only physical visibility; never feed this into BOT intelligence.
        public StrategySnapshot GetStrategyForWorld(int index,int viewerSlot)
        {
            var self=Unit(viewerSlot);var known=GetStrategy(index,self.Team);if(known.Active)return known;
            var s=strategies[index];if(!s.Active||CurrentTick>=s.Expires||s.Kind!=StrategyKind.Nuclear||s.NuclearPhase>=NuclearPhase.Arming||!self.Alive)return default;
            float dx=s.X-self.X,dz=s.Z-self.Z,dy=s.Y-self.Y;
            double distance=dx*dx+dy*dy+dz*dz;
            double angle=(Math.Atan2(dx,dz)*180/Math.PI-self.AimYaw)%360;angle=(angle+540)%360-180;
            if(distance>180*180||Math.Abs(angle)>55||!HasDeployableLineOfSight(viewerSlot,s.Device))return default;
            return StrategySnapshotOf(s);
        }
        public bool CanRequestStrategy(int requester,StrategyKind kind,float x,float z,out string reason)
        {
            reason="";
            if(requester<0||requester>=units.Length||kind<StrategyKind.Nuclear||kind>StrategyKind.WideRadar){reason="選択が無効です";return false;}
            if(Phase!=MatchPhase.Running||!StrategyEnabled){reason="戦略支援を利用できません";return false;}
            if(!SimulationConfig.IsFinite(x)||!SimulationConfig.IsFinite(z)||Math.Abs(x)>halfWidth-1||Math.Abs(z)>halfDepth-1){reason="マップ内の地点を指定してください";return false;}
            var u=units[requester];var d=config.strategy.Get(kind);int index=u.Team*3+(int)kind;
            if(kind==StrategyKind.Nuclear&&!NuclearRequestNearHome(u.Team,x,z)){reason="核は自軍本拠点の近くで要請してください / 運搬が必要です";return false;}
            if(strategies[index].Active&&CurrentTick<strategies[index].Expires){reason="同じ支援が稼働中です";return false;}
            if(CurrentTick<strategyReady[u.Team,(int)kind]){reason="再使用待ちです";return false;}
            if(strategyPoints[u.Team]<d.cost){reason="戦略ポイントが不足しています";return false;}
            return true;
        }
        public bool RequestStrategy(int requester,StrategyKind kind,float x,float z,out string reason)
        {
            if(!CanRequestStrategy(requester,kind,x,z,out reason))return false;
            var u=units[requester];var d=config.strategy.Get(kind);int index=u.Team*3+(int)kind;
            if(kind==StrategyKind.Nuclear&&!CreateNuclearDevice(requester,x,z,index,out reason))return false;
            long armed=CurrentTick+SecondsToTicks(d.delaySeconds);
            if(kind!=StrategyKind.Nuclear)strategies[index]=new StrategyState{Active=true,Id=++nextStrategyId,Team=u.Team,Owner=requester,OwnerId=u.Id,Kind=kind,
                X=x,Y=GetSurfaceHeight(x,z),Z=z,Armed=armed,Expires=armed+SecondsToTicks(d.durationSeconds)};
            strategyPoints[u.Team]-=d.cost;strategyReady[u.Team,(int)kind]=CurrentTick+SecondsToTicks(d.cooldownSeconds);
            return true;
        }
        private static void CheckStrategyKind(StrategyKind kind)
        {if(kind<StrategyKind.Nuclear||kind>StrategyKind.WideRadar)throw new ArgumentOutOfRangeException(nameof(kind));}
        private void InitializeStrategy()
        {if(StrategyEnabled)for(int t=0;t<2;t++)strategyPoints[t]=config.strategy.initialPoints;}
        private void AwardStrategyLoss(int team)
        {if(StrategyEnabled)strategyPoints[team]=Math.Min(config.strategy.maximumPoints,strategyPoints[team]+config.strategy.pointsPerLoss);}
        public bool IsMapJammed(int receivingTeam,float x,float z)
        {
            CheckTeam(receivingTeam);if(!StrategyEnabled)return false;
            foreach(var s in strategies)
            {
                if(!s.Active||s.Team==receivingTeam||s.Kind!=StrategyKind.Jammer||CurrentTick<s.Armed||CurrentTick>=s.Expires)continue;
                float radius=config.strategy.jammer.radius;if(DistanceSquared(x,z,s.X,s.Z)<=radius*radius)return true;
            }
            return false;
        }
        private void AdvanceStrategies()
        {
            if(!StrategyEnabled)return;
            UpdatePortableNuclear(false);
            for(int i=0;i<strategies.Length;i++)
            {
                var s=strategies[i];if(!s.Active)continue;
                if(CurrentTick>=s.Expires){s.Active=false;strategies[i]=s;continue;}
                if(s.Kind==StrategyKind.Nuclear&&s.NuclearPhase<NuclearPhase.Arming)continue;
                if(CurrentTick<s.Armed)continue;
                var d=config.strategy.Get(s.Kind);
                if(s.Kind==StrategyKind.Nuclear&&!s.Detonated)
                {
                    s.Detonated=true;s.NuclearPhase=NuclearPhase.Detonated;strategies[i]=s;
                    var device=deployables[s.Device];device.Active=false;deployables[s.Device]=device;
                    foreach(var target in units)
                    {
                        if(!target.Alive||DistanceSquared(target.X,target.Z,s.X,s.Z)+(target.Y-s.Y)*(target.Y-s.Y)>d.radius*d.radius)continue;
                        // Same shield+HP damage for allies and enemies. Friendly losses have
                        // no enemy-kill/damage score reward; all real losses replenish their team.
                        shieldDamage[target.Slot]+=d.unitDamage;
                        RecordImpact(target.Slot,target.Team==s.Team?-1:s.Owner,s.OwnerId,0,d.unitDamage);
                    }
                    for(int b=0;b<bases.Length;b++)
                    {
                        if(bases[b].Destroyed||DistanceSquared(bases[b].X,bases[b].Z,s.X,s.Z)+(s.Y-bases[b].Y)*(s.Y-bases[b].Y)>d.radius*d.radius)continue;
                        if(bases[b].Team!=s.Team)RecordCoreHit(b,s.Owner,s.OwnerId,d.coreDamage);
                        baseDamage[b]+=d.coreDamage;baseHits++;
                    }
                    for(int j=0;j<deployables.Length;j++)
                    {
                        var target=deployables[j];if(target.Active&&DistanceSquared(target.X,target.Z,s.X,s.Z)+(target.Y-s.Y)*(target.Y-s.Y)<=d.radius*d.radius)
                            DamageDeployable(j,target.Team==s.Team?-1:s.Owner,s.OwnerId,d.unitDamage);
                    }
                }
                if(s.Kind==StrategyKind.WideRadar && CurrentTick%SecondsToTicks(config.radarScanSeconds)==0)
                    foreach(var target in units)
                        if(target.Alive&&target.Team!=s.Team&&DistanceSquared(target.X,target.Z,s.X,s.Z)<=d.radius*d.radius)ObserveForTeam(s.Team,target,false);
            }
        }
    }
}
