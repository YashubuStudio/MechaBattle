using System;
using System.Collections.Generic;
using MechaBattle.Contracts;
using MechaBattle.Battlefield;
using MechaBattle.Observation;
namespace MechaBattle.Simulation
{
    // Decisions read self state, public terrain/objectives, allied equipment and bounded team reports.
    // No enemy UnitSnapshot or live enemy equipment position is used here.
    public sealed class BotBattlefieldPlanner
    {
        private readonly BattleSimulation world;private readonly MapDefinition map;private readonly BotBehaviorRules rules;private readonly BotPopulation population;
        private readonly int[][] homes=new int[2][];private readonly long[] nextThink=new long[100],candidateSince=new long[100],radarReady=new long[100];
        private readonly int[] identities=new int[100],candidates=new int[100],candidateIds=new int[100],goalTargets=new int[100],goalIds=new int[100],ownRadarIds=new int[100];
        private readonly BotTacticalIntent[] goals=new BotTacticalIntent[100];private readonly int[] thinks=new int[100];
        public BotSquadCoordinator Squads{get;}
        public int InterceptionOrders{get;private set;}public int IntelInvestigations{get;private set;}public int GetThinkCount(int slot)=>thinks[slot];
        public BotBattlefieldPlanner(BattleSimulation world,MapDefinition map,BotBehaviorRules rules,BotPopulation population)
        {
            this.world=world??throw new ArgumentNullException(nameof(world));this.map=map??throw new ArgumentNullException(nameof(map));this.rules=(rules??throw new ArgumentNullException(nameof(rules))).Copy();this.rules.Validate();this.population=population??throw new ArgumentNullException(nameof(population));Squads=new BotSquadCoordinator(world,map,this.rules);
            for(int i=0;i<100;i++)candidates[i]=goalTargets[i]=-1;
            for(int t=0;t<2;t++){var list=new List<int>();for(int i=0;i<map.mainBases.Length;i++)if(map.mainBases[i].team==t)list.Add(i);list.Sort((a,b)=>map.mainBases[b].z.CompareTo(map.mainBases[a].z));homes[t]=list.ToArray();}
        }
        public BotTacticalIntent GetIntent(int slot,NearbyEnemy visible)
        {
            if(!rules.enabled||!population.IsBot(slot))return default;
            bool reader=population.ReadsMap(slot);
            if(!reader&&!(rules.smartObjectivesEnabled&&rules.squadTacticsEnabled&&Squads.SquadIndex(slot)>=0&&population.RoleOf(slot)==BotRole.Frontline))return default;
            var self=world.GetUnit(slot);if(!self.IsAlive||self.Controller!=ControllerType.Bot)return default;
            if(identities[slot]!=self.UnitId){identities[slot]=self.UnitId;goals[slot]=default;goalTargets[slot]=candidates[slot]=-1;nextThink[slot]=world.CurrentTick+Phase(slot);}
            if(goalTargets[slot]>=0){var contact=world.GetTeamContact(self.Team,goalTargets[slot]);if(!contact.Valid||contact.UnitId!=goalIds[slot]){goals[slot]=default;goalTargets[slot]=-1;}}
            if(candidates[slot]>=0){var candidate=world.GetTeamContact(self.Team,candidates[slot]);if(!candidate.Valid||candidate.UnitId!=candidateIds[slot])candidates[slot]=-1;}
            bool ownsRadar=false;int radarCount=0,latestRadar=0;
            if(population.RoleOf(slot)==BotRole.RadarSupport)
                for(int i=0;i<world.DeployableCapacity;i++){var d=world.GetDeployable(i);if(!d.Active||d.Team!=self.Team||d.Kind!=WeaponKind.Radar||d.OwnerSlot!=slot)continue;ownsRadar=true;radarCount++;latestRadar=Math.Max(latestRadar,d.Id);}
            if(latestRadar>0&&ownRadarIds[slot]!=latestRadar){ownRadarIds[slot]=latestRadar;radarReady[slot]=world.CurrentTick+Ticks(rules.radarMinimumIntervalSeconds);}
            if(ownsRadar&&goalTargets[slot]<0&&(!rules.smartObjectivesEnabled||radarCount>=BattleSimulation.RadarPerOwnerLimit||world.CurrentTick<radarReady[slot]))goals[slot]=default;
            if(visible.Found)return default;
            if(world.CurrentTick<nextThink[slot]){var goal=goals[slot];if(goal.DeployRadar)goals[slot]=new BotTacticalIntent(goal.X,goal.Y,goal.Z,goal.StopDistance);return goals[slot];}
            nextThink[slot]=world.CurrentTick+Ticks(rules.mapThinkSeconds*(.8f+(slot*37%101)/250f));
            int previousTarget=goalTargets[slot];thinks[slot]++;goals[slot]=default;goalTargets[slot]=-1;
            if(population.RoleOf(slot)==BotRole.RadarSupport&&(!ownsRadar||rules.smartObjectivesEnabled&&radarCount<BattleSimulation.RadarPerOwnerLimit)&&world.CurrentTick>=radarReady[slot])
            {
                var frame=world.GetLoadout(slot,self.DeckIndex);int radar=-1;for(int i=0;i<frame.WeaponCount;i++)if(frame.GetWeapon(i).Kind==WeaponKind.Radar&&world.GetWeaponAmmo(slot,i)>0){radar=i;break;}
                if(radar>=0&&self.Health>=frame.MaxHealth*.5f&&self.IncomingLockCount==0&&FindRadarAnchor(self,out float x,out float y,out float z))
                {
                    bool place=self.Grounded&&Distance(self.X,self.Y,self.Z,x,y,z)<=16;
                    goals[slot]=new BotTacticalIntent(x,y,z,2,place);return goals[slot];
                }
            }
            int selected=-1;double best=double.PositiveInfinity;TeamContactSnapshot chosen=default;
            for(int i=0;i<100;i++)
            {
                var c=reader?world.GetTeamContact(self.Team,i):default;if(!c.Valid)continue;
                if(rules.smartObjectivesEnabled&&population.RoleOf(slot)==BotRole.Interceptor&&Responders(self.Team,slot,c)>=rules.maximumRespondersPerContact)continue;
                double d=Distance(self.X,self.Y,self.Z,c.X,c.Y,c.Z);
                if(population.RoleOf(slot)==BotRole.Interceptor){double danger=double.PositiveInfinity;foreach(int home in homes[self.Team]){var b=world.GetBase(home);if(!b.Destroyed)danger=Math.Min(danger,Distance(c.X,c.Y,c.Z,b.X,b.Y,b.Z));}if(danger>rules.defenseRadius*rules.defenseRadius)continue;d=danger+.15*d;}
                else if(d>rules.mapInterestRadius*rules.mapInterestRadius)continue;
                if(d<best){best=d;selected=i;chosen=c;}
            }
            if(selected>=0)
            {
                if(candidates[slot]!=selected||candidateIds[slot]!=chosen.UnitId){candidates[slot]=selected;candidateIds[slot]=chosen.UnitId;candidateSince[slot]=world.CurrentTick;}
                if(world.CurrentTick-candidateSince[slot]>=Ticks(rules.mapReactionSeconds)){if(previousTarget!=selected){IntelInvestigations++;if(population.RoleOf(slot)==BotRole.Interceptor)InterceptionOrders++;}goalTargets[slot]=selected;goalIds[slot]=chosen.UnitId;goals[slot]=new BotTacticalIntent(chosen.X,chosen.Y,chosen.Z,8);return goals[slot];}
            }
            else candidates[slot]=-1;
            if(population.RoleOf(slot)==BotRole.Interceptor)
            {
                int index=Home(self);if(index>=0){var b=map.mainBases[index];float sign=self.Team==0?1:-1;goals[slot]=new BotTacticalIntent(b.x+sign*(b.attackRadius+12),b.y,b.z,5);}
            }
            if(population.RoleOf(slot)==BotRole.Frontline)goals[slot]=Squads.GetObjective(self,reader);
            return goals[slot];
        }
        private int Responders(int team,int slot,TeamContactSnapshot contact)
        {int count=0;for(int other=team*50;other<team*50+50;other++){if(other==slot||goalTargets[other]<0||population.RoleOf(other)!=BotRole.Interceptor)continue;var ally=world.GetUnit(other);if(ally.Team!=team||!ally.IsAlive||ally.ReturnChanneling)continue;var c=world.GetTeamContact(team,goalTargets[other]);if(c.Valid&&c.UnitId==goalIds[other]&&Distance(c.X,c.Y,c.Z,contact.X,contact.Y,contact.Z)<24*24&&Math.Abs(c.Y-contact.Y)<6)count++;}return count;}
        private int Home(UnitSnapshot self)
        {
            var list=homes[self.Team];int rank=population.RoleRank(self.SlotId)%3;int pick=rank==0?0:rank==1?list.Length-1:list.Length/2;
            for(int i=0;i<list.Length;i++){int at=list[(pick+i)%list.Length];if(!world.GetBase(at).Destroyed)return at;}return-1;
        }
        private bool FindRadarAnchor(UnitSnapshot self,out float x,out float y,out float z)
        {
            x=self.X;y=self.Y;z=self.Z;int home=Home(self);if(home<0)return false;var b=map.mainBases[home];CapturePointDefinition relay=null;float best=float.PositiveInfinity;
            foreach(var c in map.capturePoints){float score=Math.Abs(c.z-b.z)+4*Math.Abs(c.y-b.y);if(score<best){best=score;relay=c;}}
            float gx=relay==null?b.SpawnX:b.SpawnX+(relay.x-b.SpawnX)*.45f,gz=relay==null?b.SpawnZ:b.SpawnZ+(relay.z-b.SpawnZ)*.45f,gy=b.SpawnY;
            double scoreBest=double.PositiveInfinity;
            if(map.navigationNodes!=null&&map.navigationNodes.Length>0)
            {foreach(var n in map.navigationNodes){if(n.x*(self.Team==0?1:-1)>map.halfWidth*.08f||Covered(self.Team,n.x,n.y,n.z))continue;double score=Distance(n.x,n.y,n.z,gx,gy,gz);if(score<scoreBest){scoreBest=score;x=n.x;y=n.y;z=n.z;}}}
            else
            {
                for(int ix=-3;ix<=3;ix++)for(int iz=-3;iz<=3;iz++)
                {float cx=gx+ix*8,cz=gz+iz*8;if(Math.Abs(cx)>map.halfWidth-2||Math.Abs(cz)>map.halfDepth-2)continue;float cy=world.GetSurfaceHeight(cx,cz,.8f);if(cy>gy+2||Covered(self.Team,cx,cy,cz))continue;bool blocked=false;foreach(var o in map.obstacles)if(MapGeometry.CircleOverlaps(o,cx,cz,.8f)&&cy<o.y+o.height/2-.001f&&cy+2.5f>o.y-o.height/2+.001f){blocked=true;break;}if(blocked)continue;foreach(var core in map.mainBases)if(Distance(cx,cy,cz,core.x,core.y,core.z)<(core.attackRadius+3)*(core.attackRadius+3)){blocked=true;break;}if(blocked)continue;double score=Distance(cx,cy,cz,gx,gy,gz);if(score<scoreBest){scoreBest=score;x=cx;y=cy;z=cz;}}
            }
            return scoreBest<double.PositiveInfinity;
        }
        private bool Covered(int team,float x,float y,float z)
        {for(int i=0;i<world.DeployableCapacity;i++){var d=world.GetDeployable(i);if(d.Active&&d.Team==team&&d.Kind==WeaponKind.Radar&&Distance(x,y,z,d.X,d.Y,d.Z)<d.Radius*d.Radius*.49)return true;}return false;}
        private long Phase(int slot)=>slot*37%Math.Max(1,Ticks(rules.mapThinkSeconds));
        private long Ticks(float seconds)=>Math.Max(1,(long)Math.Ceiling(seconds/world.FixedDeltaTime-.00001));
        private static double Distance(float ax,float ay,float az,float bx,float by,float bz)=>(ax-bx)*(double)(ax-bx)+(ay-by)*(double)(ay-by)+(az-bz)*(double)(az-bz);
    }
}
