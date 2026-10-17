using System;
namespace MechaBattle.Contracts
{
    public enum StrategyKind { Nuclear, Jammer, WideRadar }
    public enum NuclearPhase { None, AwaitingPickup, Carried, Arming, Detonated }
    [Serializable] public sealed class StrategyRules
    {
        public bool enabled;
        public int initialPoints, maximumPoints, pointsPerLoss;
        public float nuclearDeploymentRadius=24, nuclearPickupRadius=3, nuclearHealth=2400, nuclearLifetimeSeconds=600;
        public StrategyDefinition nuclear = new StrategyDefinition(), jammer = new StrategyDefinition(), wideRadar = new StrategyDefinition();
        public StrategyDefinition Get(StrategyKind kind) => kind == StrategyKind.Nuclear ? nuclear : kind == StrategyKind.Jammer ? jammer : wideRadar;
        public StrategyRules Copy() => new StrategyRules { enabled=enabled, initialPoints=initialPoints, maximumPoints=maximumPoints, pointsPerLoss=pointsPerLoss,
            nuclear=nuclear?.Copy(), jammer=jammer?.Copy(), wideRadar=wideRadar?.Copy(), nuclearDeploymentRadius=nuclearDeploymentRadius,nuclearPickupRadius=nuclearPickupRadius,nuclearHealth=nuclearHealth,nuclearLifetimeSeconds=nuclearLifetimeSeconds };
        public void Validate()
        {
            if(!enabled)return;
            foreach(float v in new[]{nuclearDeploymentRadius,nuclearPickupRadius,nuclearHealth,nuclearLifetimeSeconds})if(float.IsNaN(v)||float.IsInfinity(v)||v<=0||v>100000)throw new ArgumentException("Invalid portable nuclear settings.");
            if(maximumPoints<=0 || maximumPoints>100000 || initialPoints<0 || initialPoints>maximumPoints || pointsPerLoss<=0 || pointsPerLoss>maximumPoints)throw new ArgumentException("Invalid strategy point economy.");
            for(int i=0;i<3;i++) { var d=Get((StrategyKind)i); if(d==null)throw new ArgumentException("All three strategies are required.");d.Validate(maximumPoints); }
            if(nuclear.unitDamage<=0 || nuclear.coreDamage<=0)throw new ArgumentException("Nuclear damage must affect units and bases.");
        }
    }
    [Serializable] public sealed class StrategyDefinition
    {
        public int cost;
        public float cooldownSeconds, delaySeconds, durationSeconds, radius, unitDamage, coreDamage;
        public StrategyDefinition Copy() => (StrategyDefinition)MemberwiseClone();
        internal void Validate(int cap)
        {
            if(cost<=0 || cost>cap)throw new ArgumentException("Invalid strategy cost.");
            foreach(float v in new[]{cooldownSeconds,delaySeconds,durationSeconds,radius})
                if(float.IsNaN(v)||float.IsInfinity(v)||v<=0||v>3600)throw new ArgumentException("Invalid strategy range or timing.");
            foreach(float v in new[]{unitDamage,coreDamage})
                if(float.IsNaN(v)||float.IsInfinity(v)||v<0||v>1000000)throw new ArgumentException("Invalid strategy damage.");
        }
    }
    public readonly struct StrategyTeamSnapshot
    {
        public readonly bool Enabled; public readonly int Points, MaximumPoints, PointsPerLoss;
        public StrategyTeamSnapshot(bool enabled,int points,int max,int gain){Enabled=enabled;Points=points;MaximumPoints=max;PointsPerLoss=gain;}
    }
    public readonly struct StrategySnapshot
    {
        public readonly bool Active, Armed, Detonated;
        public readonly int Id, Team; public readonly StrategyKind Kind;
        public readonly float X,Y,Z,Radius,SecondsUntilActive,RemainingSeconds,Health,MaxHealth;
        public readonly NuclearPhase NuclearPhase; public readonly int CarrierSlot,DeviceIndex,DeviceId;
        public StrategySnapshot(bool active,bool armed,bool detonated,int id,int team,StrategyKind kind,float x,float y,float z,float radius,float wait,float remaining,NuclearPhase nuclearPhase=NuclearPhase.None,int carrierSlot=-1,int deviceIndex=-1,int deviceId=0,float health=0,float maxHealth=0)
        {NuclearPhase=nuclearPhase;CarrierSlot=carrierSlot;DeviceIndex=deviceIndex;DeviceId=deviceId;Health=health;MaxHealth=maxHealth;Active=active;Armed=armed;Detonated=detonated;Id=id;Team=team;Kind=kind;X=x;Y=y;Z=z;Radius=radius;SecondsUntilActive=wait;RemainingSeconds=remaining;}
    }
}
