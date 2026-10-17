using System;
using MechaBattle.Contracts;
using MechaBattle.Battlefield;
namespace MechaBattle.Simulation
{
    public sealed partial class BotTraversalAdapter
    {
        private sealed class StuckSample
        {
            public int Life,Phase,Attempt,Repeats;public long Tick=-1,Until,ReturnSent=-1,RetryAt,LastStall=-1;
            public float StallX,StallY,StallZ;
            public float X,Y,Z,Elapsed,Travel,Expected,LastMoveX,LastMoveZ,EscapeX,EscapeY,EscapeZ,DirectionX,DirectionZ;
            public bool Moving;
        }
        private readonly StuckSample[] stuck=new StuckSample[100];
        public int StuckAvoidanceCount{get;private set;}public int StuckReturnCount{get;private set;}
        private long StuckTicks(float seconds)=>Math.Max(1,(long)Math.Ceiling(seconds*config.tickHz));
        private void ClearStuckPath(int slot){waypoint[slot]=false;layerGoal[slot]=-1;layerCount[slot]=0;}
        private static UnitCommand StuckMotion(UnitCommand c,float x,float z,bool back=false)
        {return new UnitCommand(c.UnitId,c.Tick,c.Sequence,x,z,c.AimYaw,c.AimPitch,false,false,false,false,false,cancelHeldAttack:true,emergencyReturn:back);}
        private UnitCommand RecoverStuckMovement(UnitSnapshot self,UnitLoadout frame,UnitCommand command)
        {
            if(!config.botStuckRecoveryEnabled||self.Controller!=ControllerType.Bot||!self.IsAlive)return command;
            int slot=self.SlotId;var sample=stuck[slot];if(sample==null||sample.Life!=self.UnitId){sample=new StuckSample{Life=self.UnitId,X=self.X,Y=self.Y,Z=self.Z};stuck[slot]=sample;}
            float dx=self.X-sample.X,dy=self.Y-sample.Y,dz=self.Z-sample.Z;
            float travelled=(float)Math.Sqrt(dx*dx+dy*dy+dz*dz),seconds=sample.Tick<0?0:Math.Max(0,(command.Tick-sample.Tick)/(float)config.tickHz);
            bool available=self.Disruption==DisruptionState.Ready&&self.MeleePhase<0&&self.MeleeChargeFraction<=0;
            if(sample.Moving&&available&&seconds>0&&seconds<=.2f){sample.Elapsed+=seconds;sample.Travel+=travelled;sample.Expected+=(float)Math.Sqrt(sample.LastMoveX*sample.LastMoveX+sample.LastMoveZ*sample.LastMoveZ)*frame.MoveSpeed*seconds;}
            else if(!sample.Moving||!available){sample.Elapsed=sample.Travel=sample.Expected=0;}
            sample.X=self.X;sample.Y=self.Y;sample.Z=self.Z;sample.Tick=command.Tick;
            if(self.ReturnChanneling){sample.Moving=false;return StuckMotion(command,0,0);}
            if(!available){sample.Phase=0;sample.Moving=false;return command;}
            if(sample.Phase==3)
            {
                // Observe the next tick before retrying; a rejected/interrupted return is never spammed.
                if(sample.ReturnSent>=0){sample.Phase=0;sample.Attempt=0;sample.ReturnSent=-1;sample.RetryAt=command.Tick+StuckTicks(Math.Max(2,config.botStuckWatchSeconds));sample.Elapsed=sample.Travel=sample.Expected=0;}
                else if(!config.emergencyReturnEnabled||self.CarryingNuclear){sample.Phase=0;sample.RetryAt=command.Tick+StuckTicks(2);}
                else if(self.Grounded&&!self.BeamActive&&self.ChargeFraction<=0&&command.Tick>=sample.Until)
                {sample.ReturnSent=command.Tick;sample.Moving=false;StuckReturnCount++;return StuckMotion(command,0,0,true);}
                else {sample.Moving=false;return StuckMotion(command,0,0);}
            }
            if(sample.Phase==1)
            {
                float ex=self.X-sample.EscapeX,ey=self.Y-sample.EscapeY,ez=self.Z-sample.EscapeZ;
                if(command.Tick>=sample.Until)
                {
                    if(ex*ex+ey*ey+ez*ez>=config.botStuckMinimumProgress*config.botStuckMinimumProgress*4)
                    {sample.Phase=0;sample.Attempt=0;sample.Elapsed=sample.Travel=sample.Expected=0;ClearStuckPath(slot);}
                    else if(sample.Attempt<config.botStuckAvoidAttempts)BeginStuckAvoidance(self,sample,command.Tick);
                    else {sample.Phase=3;sample.ReturnSent=-1;sample.Until=command.Tick+2;sample.Moving=false;return StuckMotion(command,0,0);}
                }
                if(sample.Phase==1){sample.Moving=false;return StuckMotion(command,sample.DirectionX,sample.DirectionZ);}
            }
            bool moving=command.MoveX*command.MoveX+command.MoveY*command.MoveY>.01f&&!command.Crouch&&!command.EmergencyReturn;
            float watch=config.botStuckWatchSeconds*(1+(slot*13%17)*.015f);
            if(sample.Phase==0&&moving&&command.Tick>=sample.RetryAt&&sample.Elapsed>=watch)
            {
                bool blocked=sample.Expected>.5f&&sample.Travel<Math.Max(.05f,Math.Min(config.botStuckMinimumProgress,sample.Expected*.1f));
                sample.Elapsed=sample.Travel=sample.Expected=0;
                if(blocked)
                {
                    float sx=self.X-sample.StallX,sy=self.Y-sample.StallY,sz=self.Z-sample.StallZ,radius=Math.Max(1,config.botStuckMinimumProgress*4);
                    bool repeated=sample.LastStall>=0&&command.Tick-sample.LastStall<=StuckTicks(config.botStuckWatchSeconds*6+config.botStuckAvoidSeconds*config.botStuckAvoidAttempts+2)&&sx*sx+sy*sy+sz*sz<radius*radius;
                    sample.Repeats=repeated?sample.Repeats+1:0;sample.LastStall=command.Tick;sample.StallX=self.X;sample.StallY=self.Y;sample.StallZ=self.Z;
                    if(sample.Repeats>=config.botStuckAvoidAttempts&&config.emergencyReturnEnabled&&!self.CarryingNuclear){sample.Phase=3;sample.ReturnSent=-1;sample.Until=command.Tick+2;sample.Moving=false;return StuckMotion(command,0,0);}
                    sample.Attempt=0;BeginStuckAvoidance(self,sample,command.Tick);sample.Moving=false;return StuckMotion(command,sample.DirectionX,sample.DirectionZ);
                }
            }
            sample.Moving=moving;sample.LastMoveX=command.MoveX;sample.LastMoveZ=command.MoveY;return command;
        }
        private static double StuckBoxGapSquared(ObstacleDefinition box,float x,float z)
        {double dx=Math.Max(0,Math.Abs(x-box.x)-box.width*.5f),dz=Math.Max(0,Math.Abs(z-box.z)-box.depth*.5f);return dx*dx+dz*dz;}
        private void BeginStuckAvoidance(UnitSnapshot self,StuckSample sample,long tick)
        {
            sample.Phase=1;sample.Attempt++;sample.EscapeX=self.X;sample.EscapeY=self.Y;sample.EscapeZ=self.Z;
            sample.Until=tick+StuckTicks(config.botStuckAvoidSeconds);ClearStuckPath(self.SlotId);StuckAvoidanceCount++;
            double angle=Math.Atan2(sample.LastMoveX,sample.LastMoveZ)+(((self.SlotId+sample.Attempt+sample.Repeats)%2==0)?1:-1)*Math.PI*.5;
            double best=double.NegativeInfinity;float bx=0,bz=0;
            for(int step=0;step<8;step++)
            {
                double a=angle+step*Math.PI*.25;float x=(float)Math.Sin(a),z=(float)Math.Cos(a);double clear=3;
                foreach(var o in map.obstacles??Array.Empty<ObstacleDefinition>())if(o.y+o.height*.5f>self.Y+config.maximumStepHeight+.001f&&o.y-o.height*.5f<self.Y+config.unitHeight)
                {
                    double hit=MapGeometry.RayBox(o,self.X,self.Y+.1f,self.Z,x,0,z,config.unitRadius+.2f);
                    double gap=StuckBoxGapSquared(o,self.X,self.Z);
                    // A safety-expanded ray can start inside the margin while tangent walking is legal.
                    if(hit<.001&&gap>=Math.Max(0,config.unitRadius-.01f)*Math.Max(0,config.unitRadius-.01f)&&StuckBoxGapSquared(o,self.X+x*.2f,self.Z+z*.2f)>=gap-.000001)hit=3;
                    clear=Math.Min(clear,hit);
                }
                if(Math.Abs(self.X+x*1.5f)>map.halfWidth-config.unitRadius||Math.Abs(self.Z+z*1.5f)>map.halfDepth-config.unitRadius)clear=0;
                if(config.baseCollisionEnabled)foreach(var home in map.mainBases)if(self.Y<home.y+config.coreHeight&&self.Y+config.unitHeight>home.y)
                {float px=self.X+x*1.5f-home.x,pz=self.Z+z*1.5f-home.z;if(px*px+pz*pz<(home.attackRadius+config.unitRadius)*(home.attackRadius+config.unitRadius))clear=0;}
                float intentLength=Math.Max(.001f,(float)Math.Sqrt(sample.LastMoveX*sample.LastMoveX+sample.LastMoveZ*sample.LastMoveZ));
                double score=clear+.35*(x*sample.LastMoveX+z*sample.LastMoveZ)/intentLength-step*.015;if(score>best){best=score;bx=x;bz=z;}
            }
            sample.DirectionX=bx;sample.DirectionZ=bz;
        }
    }
}
