using System;
using MechaBattle.AI.Squads;
using MechaBattle.Battlefield;
using MechaBattle.Contracts;
namespace MechaBattle.Simulation
{
    // Only public objectives and allied state. This class never submits player commands.
    public sealed class BotSquadCoordinator
    {
        private readonly BattleSimulation world;private readonly MapDefinition map;private readonly BotBehaviorRules rules;
        private readonly SquadDefinition[] squads;private readonly int[] membership=new int[100],objectives=new int[100];
        private readonly bool[] leaders=new bool[100];private readonly long[] until=new long[100],rallyUntil;
        public int ObjectiveOrders{get;private set;}public int RegroupOrders{get;private set;}
        public BotSquadCoordinator(BattleSimulation world,MapDefinition map,BotBehaviorRules rules)
        {
            this.world=world??throw new ArgumentNullException(nameof(world));this.map=map??throw new ArgumentNullException(nameof(map));this.rules=(rules??throw new ArgumentNullException(nameof(rules))).Copy();this.rules.Validate();
            squads=SquadRoster.BuildStandardSquads(rules.squadsPerTeam,rules.squadSize);rallyUntil=new long[squads.Length];
            for(int i=0;i<100;i++)membership[i]=objectives[i]=-1;
            for(int i=0;i<squads.Length;i++){leaders[squads[i].MemberSlots[0]]=true;foreach(int slot in squads[i].MemberSlots)membership[slot]=i;}
        }
        public bool IsLeader(int slot)=>slot>=0&&slot<100&&leaders[slot];
        public int SquadIndex(int slot)=>slot>=0&&slot<100?membership[slot]:-1;
        public BotTacticalIntent GetObjective(UnitSnapshot self,bool readsMap)
        {
            if(!rules.enabled||!rules.smartObjectivesEnabled||!self.IsAlive||self.Controller!=ControllerType.Bot)return default;
            int local=self.SlotId%50;
            // Preserve the existing independent raiding lanes instead of recalling them to relays.
            if(local%9==0&&local/9%3!=0)return default;
            int group=rules.squadTacticsEnabled?membership[self.SlotId]:-1;
            if(group<0&&!readsMap)return default;
            var anchor=self;int key=self.SlotId;
            if(group>=0)
            {
                key=squads[group].MemberSlots[0];
                foreach(int slot in squads[group].MemberSlots){var ally=world.GetUnit(slot);if(ally.Team==self.Team&&ally.IsAlive&&!ally.ReturnChanneling&&ally.Health>=world.GetLoadout(slot,ally.DeckIndex).MaxHealth*.6f){anchor=ally;break;}}
            }
            int relay=objectives[key];
            if(relay<0||relay>=map.capturePoints.Length||world.GetCapturePoint(relay).OwnerTeam==self.Team||world.CurrentTick>=until[key])
            {
                relay=SelectRelay(anchor,key);objectives[key]=relay;until[key]=world.CurrentTick+Ticks(rules.objectiveCommitSeconds);
                if(relay>=0){ObjectiveOrders++;if(group>=0)rallyUntil[group]=world.CurrentTick+Ticks(2);}
            }
            if(relay<0)return default;
            var point=map.capturePoints[relay];
            if(group>=0)
            {
                double gap=Distance(self.X,self.Y,self.Z,anchor.X,anchor.Y,anchor.Z);
                if(anchor.SlotId!=self.SlotId&&gap>rules.squadRallyDistance*rules.squadRallyDistance)
                {RegroupOrders++;return new BotTacticalIntent(anchor.X,anchor.Y,anchor.Z,rules.squadRallyDistance*.5f);}
                if(anchor.SlotId==self.SlotId&&world.CurrentTick<rallyUntil[group]&&HealthyDistantMember(group,self))
                {RegroupOrders++;return new BotTacticalIntent(self.X,self.Y,self.Z,1);}
            }
            return new BotTacticalIntent(point.x,point.y,point.z,Math.Max(1,point.radius*.45f));
        }
        private bool HealthyDistantMember(int group,UnitSnapshot self)
        {
            foreach(int slot in squads[group].MemberSlots)
            {var ally=world.GetUnit(slot);if(slot==self.SlotId||ally.Team!=self.Team||!ally.IsAlive||ally.ReturnChanneling)continue;var frame=world.GetLoadout(slot,ally.DeckIndex);if(ally.Health<frame.MaxHealth*.6f)continue;double d=Distance(self.X,self.Y,self.Z,ally.X,ally.Y,ally.Z);if(d>rules.squadRallyDistance*rules.squadRallyDistance&&d<rules.squadRallyDistance*rules.squadRallyDistance*16)return true;}
            return false;
        }
        private int SelectRelay(UnitSnapshot self,int key)
        {
            // Keep an already reached enemy core as the primary objective.
            for(int i=0;i<map.mainBases.Length;i++){var home=world.GetBase(i);if(home.Team!=self.Team&&!home.Destroyed&&Distance(self.X,self.Y,self.Z,home.X,home.Y,home.Z)<Math.Max(25,map.mainBases[i].buildingRadius)*Math.Max(25,map.mainBases[i].buildingRadius))return -1;}
            int selected=-1;double best=double.PositiveInfinity;
            for(int i=0;i<map.capturePoints.Length;i++)
            {
                if(world.GetCapturePoint(i).OwnerTeam==self.Team)continue;var p=map.capturePoints[i];
                int friends=0;for(int slot=self.Team*50;slot<self.Team*50+50;slot++){var ally=world.GetUnit(slot);if(ally.Team==self.Team&&ally.IsAlive&&Distance(ally.X,ally.Y,ally.Z,p.x,p.y,p.z)<p.radius*p.radius*4)friends++;}
                int assigned=0;for(int slot=self.Team*50;slot<self.Team*50+50;slot++)if(slot!=key&&objectives[slot]==i&&until[slot]>world.CurrentTick)assigned++;
                double score=Math.Sqrt(Distance(self.X,self.Y,self.Z,p.x,p.y,p.z))+.3*Math.Abs(p.z)+friends*12+assigned*45;
                if(score<best){best=score;selected=i;}
            }
            return selected;
        }
        private long Ticks(float seconds)=>Math.Max(1,(long)Math.Ceiling(seconds/world.FixedDeltaTime));
        private static double Distance(float ax,float ay,float az,float bx,float by,float bz)=>(ax-bx)*(double)(ax-bx)+4*(ay-by)*(double)(ay-by)+(az-bz)*(double)(az-bz);
    }
}
