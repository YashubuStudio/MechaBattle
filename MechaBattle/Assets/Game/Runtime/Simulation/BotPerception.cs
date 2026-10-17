using System;
using MechaBattle.Contracts;
using MechaBattle.Observation;
namespace MechaBattle.Simulation
{
    // The only world-truth boundary used by production BOT engagement sensors.
    // Radar/locks may suggest a bearing, never provide a firing target through cover.
    public sealed class BotPerception : INearbyThreatSensor
    {
        private readonly BattleSimulation world; private readonly SimulationConfig config; private readonly BotPopulation population;
        private readonly int[] identities=new int[100], candidates=new int[100], candidateIds=new int[100];
        private readonly long[] nextScan=new long[100], seenSince=new long[100], lastTurn=new long[100];
        private readonly float[] facing=new float[100], facingPitch=new float[100];
        private readonly NearbyEnemy[] samples=new NearbyEnemy[100];
        public BotPerception(BattleSimulation world,SimulationConfig config,BotPopulation population=null)
        {this.world=world??throw new ArgumentNullException(nameof(world));this.config=(config??throw new ArgumentNullException(nameof(config))).Copy();this.population=population;for(int i=0;i<100;i++)candidates[i]=-1;}
        public NearbyEnemy FindNearestEnemy(int slotId,float rangeMeters)
        {
            if(slotId<0||slotId>=100||!SimulationConfig.IsFinite(rangeMeters)||rangeMeters<=0)return NearbyEnemy.None;
            var self=world.GetUnit(slotId);if(!self.IsAlive)return NearbyEnemy.None;
            float range=Math.Min(rangeMeters,world.GetActiveWeapon(slotId).FireRange);
            if(config.botPerceptionEnabled)range=Math.Min(range,config.botVisualRange);
            if(identities[slotId]!=self.UnitId)
            {
                identities[slotId]=self.UnitId;candidates[slotId]=-1;samples[slotId]=NearbyEnemy.None;
                facing[slotId]=self.Team==0?90:-90;facingPitch[slotId]=0;lastTurn[slotId]=world.CurrentTick;
                nextScan[slotId]=world.CurrentTick+(config.botPerceptionEnabled?slotId%Math.Max(1,Ticks(config.botScanSeconds)):0);
            }
            float desiredYaw=self.AimYaw,desiredPitch=self.AimPitch;
            // An unobserved flank cannot attract a bot's attention. Only a bounded,
            // nonexpired, unjammed team report can redirect its search outside its view.
            if(candidates[slotId]>=0)
            {
                var observed=samples[slotId];double dx=observed.X-self.X,dz=observed.Z-self.Z;
                desiredYaw=(float)(Math.Atan2(dx,dz)*180/Math.PI);
                desiredPitch=(float)(-Math.Atan2(observed.Y-self.Y,Math.Sqrt(dx*dx+dz*dz))*180/Math.PI);
            }
            if(candidates[slotId]<0 && config.botPerceptionEnabled && (population==null || population.ReadsMap(slotId)))
            {
                double best=config.botVisualRange*config.botVisualRange;
                for(int i=0;i<100;i++)
                {
                    var c=world.GetTeamContact(self.Team,i);if(!c.Valid)continue;
                    double dx=c.X-self.X,dz=c.Z-self.Z,d=dx*dx+dz*dz;
                    if(d>best)continue;best=d;desiredYaw=(float)(Math.Atan2(dx,dz)*180/Math.PI);
                    desiredPitch=(float)(-Math.Atan2(c.Y-self.Y,Math.Sqrt(d))*180/Math.PI);
                }
            }
            if(config.botPerceptionEnabled)
            {
                float max=config.botViewTurnDegreesPerSecond*(world.CurrentTick-lastTurn[slotId])*world.FixedDeltaTime;
                facing[slotId]+=Math.Max(-max,Math.Min(max,Delta(facing[slotId],desiredYaw)));
                facingPitch[slotId]+=Math.Max(-max,Math.Min(max,desiredPitch-facingPitch[slotId]));
            }
            else {facing[slotId]=self.AimYaw;facingPitch[slotId]=self.AimPitch;}
            lastTurn[slotId]=world.CurrentTick;
            if(candidates[slotId]>=0 && !Visible(slotId,candidates[slotId],candidateIds[slotId],range))
            {candidates[slotId]=-1;samples[slotId]=NearbyEnemy.None;}
            if(world.CurrentTick>=nextScan[slotId])
            {
                nextScan[slotId]=world.CurrentTick+(config.botPerceptionEnabled?Ticks(config.botScanSeconds):1);
                int selected=candidates[slotId];double best=range*range;
                if(selected<0)
                    for(int i=0;i<100;i++)
                    {
                        var target=world.GetUnit(i);if(target.Team==self.Team||!Visible(slotId,i,target.UnitId,range))continue;
                        double d=(target.X-self.X)*(target.X-self.X)+(target.Y-self.Y)*(target.Y-self.Y)+(target.Z-self.Z)*(target.Z-self.Z);
                        if(d<best){best=d;selected=i;}
                    }
                if(selected>=0)
                {
                    var target=world.GetUnit(selected);
                    if(candidates[slotId]!=selected||candidateIds[slotId]!=target.UnitId)seenSince[slotId]=world.CurrentTick;
                    candidates[slotId]=selected;candidateIds[slotId]=target.UnitId;
                    samples[slotId]=new NearbyEnemy(true,target.X,target.Z,target.Y,selected,target.UnitId);
                }
            }
            if(candidates[slotId]<0)return NearbyEnemy.None;
            int delay=config.botPerceptionEnabled?Ticks(config.botReactionSeconds+config.botReactionJitterSeconds*((slotId*37+identities[slotId]*13)%101)/100f):0;
            return world.CurrentTick-seenSince[slotId]>=delay?samples[slotId]:NearbyEnemy.None;
        }
        private bool Visible(int observer,int target,int id,float range)
        {
            var a=world.GetUnit(observer);var b=world.GetUnit(target);
            return b.UnitId==id && b.Team!=a.Team && (config.botPerceptionEnabled
                ?world.CanVisuallySeeFrom(observer,target,range,facing[observer],facingPitch[observer],config.botVisualDegrees,config.botVerticalVisualDegrees)
                :b.IsAlive&&Distance(a,b)<=range*range&&world.HasLineOfSight(observer,target));
        }
        private static double Distance(UnitSnapshot a,UnitSnapshot b)=>(a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y)+(a.Z-b.Z)*(a.Z-b.Z);
        private int Ticks(float seconds)=>Math.Max(1,(int)Math.Ceiling(seconds*config.tickHz-0.00001));
        private static float Delta(float from,float to){float v=(to-from)%360;if(v>180)v-=360;if(v< -180)v+=360;return v;}
    }
    public sealed partial class BattleSimulation
    {
        public bool CanVisuallySee(int observer,int target,float range)
        {
            var u=Unit(observer);
            return CanVisuallySeeFrom(observer,target,config.botPerceptionEnabled?Math.Min(range,config.botVisualRange):range,u.AimYaw,u.AimPitch,
                config.botPerceptionEnabled?config.botVisualDegrees:110,config.botPerceptionEnabled?config.botVerticalVisualDegrees:90);
        }
        public bool CanVisuallySeeFrom(int observer,int target,float range,float yaw,float pitch,float horizontalDegrees,float verticalDegrees)
        {
            var a=Unit(observer);var b=Unit(target);
            if(!a.Alive||!b.Alive||!SimulationConfig.IsFinite(range)||range<=0)return false;
            double dx=b.X-a.X,dy=b.Y-a.Y,dz=b.Z-a.Z,flat=dx*dx+dz*dz;
            if(flat+dy*dy>range*range)return false;
            double angle=Math.Atan2(dx,dz)*180/Math.PI-yaw;angle=(angle%360+540)%360-180;
            double elevation=Math.Atan2(dy,Math.Sqrt(flat))*180/Math.PI;
            return Math.Abs(angle)<=horizontalDegrees/2 && Math.Abs(elevation+pitch)<=verticalDegrees/2 && HasLineOfSight(observer,target);
        }
    }
}
