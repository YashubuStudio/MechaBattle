using System;
using MechaBattle.Contracts;
using MechaBattle.Battlefield;
namespace MechaBattle.Simulation
{
    public sealed partial class BattleSimulation
    {
        public bool IsCarryingNuclear(int slot)
        {
            var u=Unit(slot);int i=u.CarriedNuclear;
            return i>=0&&i<strategies.Length&&strategies[i].Active&&strategies[i].NuclearPhase==NuclearPhase.Carried&&strategies[i].Carrier==slot&&strategies[i].CarrierId==u.Id;
        }
        public int GetNearbyNuclearDevice(int slot)
        {
            var u=Unit(slot);if(!u.Alive||!StrategyEnabled)return -1;
            int best=-1;double distance=config.strategy.nuclearPickupRadius*config.strategy.nuclearPickupRadius;
            for(int i=0;i<strategies.Length;i++)
            {
                var s=strategies[i];if(!s.Active||s.Kind!=StrategyKind.Nuclear||s.Team!=u.Team||s.NuclearPhase!=NuclearPhase.AwaitingPickup||CurrentTick<s.PickupAfter||CurrentTick>=s.Expires)continue;
                var device=deployables[s.Device];if(!device.Active||device.Id!=s.DeviceId||device.Health<=0)continue;
                double d=DistanceSquared(u.X,u.Z,s.X,s.Z)+(u.Y-s.Y)*(u.Y-s.Y);
                if(d>distance||!HasDeployableLineOfSight(slot,s.Device))continue;distance=d;best=i;
            }
            return best;
        }
        private bool NuclearRequestNearHome(int team,float x,float z)
        {
            foreach(var home in bases)if(!home.Destroyed&&home.Team==team&&DistanceSquared(home.X,home.Z,x,z)<=config.strategy.nuclearDeploymentRadius*config.strategy.nuclearDeploymentRadius)return true;
            return false;
        }
        private bool CreateNuclearDevice(int requester,float x,float z,int index,out string reason)
        {
            reason="";var u=units[requester];int free=-1;
            for(int i=0;i<deployables.Length;i++)if(!deployables[i].Active||CurrentTick>=deployables[i].Expires){free=i;break;}
            if(free<0){reason="設置物の空きがありません";return false;}
            // Clicking a base symbol delivers beside its core, never inside its visible model.
            foreach(var home in bases)if(home.Team==u.Team&&DistanceSquared(home.X,home.Z,x,z)<(home.AttackRadius+2)*(home.AttackRadius+2))
            {x=home.X+(u.Team==0?1:-1)*(home.AttackRadius+2);z=home.Z;break;}
            float level=0,bestHome=float.PositiveInfinity;foreach(var home in bases)if(home.Team==u.Team&&!home.Destroyed){float delta=(float)DistanceSquared(home.X,home.Z,x,z);if(delta<bestHome){bestHome=delta;level=home.Y;}}
            float y=GetSurfaceHeightBelow(x,z,level+.25f);var rules=config.strategy;
            var weapon=new UnitLoadout("strategy.nuclear.device",1,0,1,1,1,1,.1f,1,1,WeaponFireMode.SingleShot,1,.1f,
                kind:WeaponKind.StrategicNuclear,utilityRadius:rules.nuclear.radius,utilityDuration:rules.nuclearLifetimeSeconds,utilityHealth:rules.nuclearHealth,ammoResupplyDisabled:true);
            int id=checked(++nextDeployableId);long expiry=CurrentTick+SecondsToTicks(rules.nuclearLifetimeSeconds);
            deployables[free]=new Deployable{Active=true,Id=id,Team=u.Team,Owner=requester,OwnerId=u.Id,X=x,Y=y,Z=z,Health=rules.nuclearHealth,Armed=CurrentTick,Expires=expiry,Weapon=weapon};
            strategies[index]=new StrategyState{Active=true,Id=++nextStrategyId,Team=u.Team,Owner=requester,OwnerId=u.Id,Kind=StrategyKind.Nuclear,X=x,Y=y,Z=z,
                Armed=long.MaxValue,Expires=expiry,NuclearPhase=NuclearPhase.AwaitingPickup,Carrier=-1,Device=free,DeviceId=id};
            return true;
        }
        private UnitCommand WithoutWeaponActions(UnitCommand c)
        {
            return new UnitCommand(c.UnitId,c.Tick,c.Sequence,c.MoveX,c.MoveY,c.AimYaw,c.AimPitch,c.Boost,false,false,false,false,false,c.MeleeStyle,c.HasAimPoint,
                c.AimTargetX,c.AimTargetY,c.AimTargetZ,-1,0,c.Jump,0,c.CancelHeldAttack,-1,0,c.EmergencyReturn,c.Crouch);
        }
        private void PreparePortableNuclearInput(UnitState u)
        {
            if(!u.Alive)return;bool raw=u.HasCommand&&u.Command.FirePrimary;bool pressed=raw&&!u.NuclearTriggerHeld;u.NuclearTriggerHeld=raw;
            bool carrying=IsCarryingNuclear(u.Slot),used=false;
            if(u.HasCommand&&u.Command.UseAbility)
            {
                if(carrying){DropNuclear(u.CarriedNuclear);ActionMessage(u,"核を手放しました / Fで再取得");used=true;}
                else if(CurrentTick<u.DisabledUntil||CurrentTick<u.MeleeUntil||u.ReturnUntil>=0)ActionMessage(u,"行動阻害・攻撃・帰投中は核を拾えません");
                else
                {
                    int i=GetNearbyNuclearDevice(u.Slot);
                    if(i>=0)
                    {
                        var s=strategies[i];s.NuclearPhase=NuclearPhase.Carried;s.Carrier=u.Slot;s.CarrierId=u.Id;strategies[i]=s;u.CarriedNuclear=i;
                        u.NuclearSuppressTrigger=true;CancelHeldActions(u.Slot);ActionMessage(u,"核運搬中 / 着地して左クリックで起動 / Fで手放す");used=true;
                    }
                    else ActionMessage(u,"付近に拾える味方核がありません");
                }
            }
            if(carrying&&!used&&u.HasCommand&&pressed)
            {
                if(!u.Grounded||u.Command.Jump)ActionMessage(u,"核の起動は着地してから左クリックを押してください");
                else if(CurrentTick<u.DisabledUntil||CurrentTick<u.MeleeUntil)ActionMessage(u,"行動阻害中は核を起動できません");
                else
                {
                    int i=u.CarriedNuclear;var s=strategies[i];s.NuclearPhase=NuclearPhase.Arming;s.Carrier=-1;s.Owner=u.Slot;s.OwnerId=u.Id;s.X=u.X;s.Y=u.Y;s.Z=u.Z;
                    s.Armed=CurrentTick+SecondsToTicks(config.strategy.nuclear.delaySeconds);s.Expires=s.Armed+SecondsToTicks(config.strategy.nuclear.durationSeconds);strategies[i]=s;
                    var d=deployables[s.Device];d.X=s.X;d.Y=s.Y;d.Z=s.Z;d.Owner=u.Slot;d.OwnerId=u.Id;d.Expires=s.Expires;deployables[s.Device]=d;
                    u.CarriedNuclear=-1;u.NuclearSuppressTrigger=true;ActionMessage(u,"核を起動！ 味方も爆発範囲から退避");used=true;
                }
            }
            if(!raw&&!IsCarryingNuclear(u.Slot))u.NuclearSuppressTrigger=false;
            if(u.HasCommand&&(carrying||used||IsCarryingNuclear(u.Slot)||u.NuclearSuppressTrigger&&raw))u.Command=WithoutWeaponActions(u.Command);
        }
        private void DropNuclear(int index)
        {
            var s=strategies[index];if(s.Carrier>=0){var u=units[s.Carrier];if(u.Id==s.CarrierId){s.X=u.X;s.Y=u.Y;s.Z=u.Z;u.CarriedNuclear=-1;u.NuclearSuppressTrigger=true;}}
            s.Carrier=-1;s.CarrierId=0;s.NuclearPhase=NuclearPhase.AwaitingPickup;s.DropVelocity=0;s.PickupAfter=CurrentTick+SecondsToTicks(.5f);strategies[index]=s;
            var d=deployables[s.Device];d.X=s.X;d.Y=s.Y;d.Z=s.Z;deployables[s.Device]=d;
        }
        private void UpdatePortableNuclear(bool advanceMotion)
        {
            if(!StrategyEnabled)return;
            for(int i=0;i<strategies.Length;i++)
            {
                var s=strategies[i];if(!s.Active||s.Kind!=StrategyKind.Nuclear||s.NuclearPhase==NuclearPhase.Detonated)continue;
                var d=deployables[s.Device];
                if(!d.Active||d.Id!=s.DeviceId||d.Health<=0||CurrentTick>=s.Expires)
                {if(s.Carrier>=0&&units[s.Carrier].Id==s.CarrierId){units[s.Carrier].CarriedNuclear=-1;ActionMessage(units[s.Carrier],"運搬中の核が破壊されました");}s.Active=false;strategies[i]=s;continue;}
                if(s.NuclearPhase==NuclearPhase.Carried)
                {
                    var carrier=units[s.Carrier];
                    if(!carrier.Alive||carrier.Id!=s.CarrierId||carrier.Disruption==DisruptionState.KnockedDown){DropNuclear(i);s=strategies[i];}
                    else
                    {
                        double a=carrier.AimYaw*Math.PI/180;float x=carrier.X+(float)Math.Sin(a)*.8f,z=carrier.Z+(float)Math.Cos(a)*.8f;
                        if(!ClearSegment(carrier.X,carrier.Y+ShotHeight,carrier.Z,x,carrier.Y+1,z)){x=carrier.X;z=carrier.Z;}
                        s.X=x;s.Y=carrier.Y+.8f;s.Z=z;
                    }
                }
                if(advanceMotion&&s.NuclearPhase==NuclearPhase.AwaitingPickup)
                {
                    float floor=0;foreach(var o in obstacles)if(o.y+o.height/2<=s.Y+.01f&&MapGeometry.CircleOverlaps(o,s.X,s.Z,.7f))floor=Math.Max(floor,o.y+o.height/2);
                    s.DropVelocity-=Math.Max(24,config.gravity)*FixedDeltaTime;s.Y=Math.Max(floor,s.Y+s.DropVelocity*FixedDeltaTime);if(s.Y<=floor+.001f)s.DropVelocity=0;
                }
                d.X=s.X;d.Y=s.Y;d.Z=s.Z;deployables[s.Device]=d;strategies[i]=s;
            }
        }
    }
}
