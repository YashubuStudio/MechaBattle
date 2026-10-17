using System;
using MechaBattle.Contracts;
using MechaBattle.Observation;
using MechaBattle.Battlefield;
namespace MechaBattle.Simulation
{
    public sealed partial class BotTraversalAdapter
    {
        private readonly int[] layerBase=new int[100],layerGoal=new int[100],layerCursor=new int[100],layerCount=new int[100],layerLife=new int[100];
        private int[,] layerPath;private float[] routeDistance;private int[] routePrevious;private bool[] routeVisited;
        private int CaptureOwner(int index)=>captures!=null&&index<captures.CapturePointCount?captures.GetCapturePoint(index).OwnerTeam:-1;
        private bool BaseFallen(int index)=>captures is IMainBaseStatusSource source && index>=0&&index<source.MainBaseCount&&source.IsMainBaseDestroyed(index);
        private void RouteLayeredPublicObjective(UnitSnapshot self,UnitCommand command,bool resupply,ref float mx,ref float mz,ref bool fire,ref float yaw,BotTacticalIntent intent=default)
        {
            int slot=self.SlotId;var nodes=map.navigationNodes;
            if(layerPath==null){layerPath=new int[100,nodes.Length];routeDistance=new float[nodes.Length];routePrevious=new int[nodes.Length];routeVisited=new bool[nodes.Length];for(int i=0;i<100;i++)layerGoal[i]=layerBase[i]=-1;}
            int role=slot%50%10,relay=-1,core=-1;float gx=self.X,gy=self.Y,gz=self.Z,stop=2;
            if(intent.HasGoal&&!resupply){gx=intent.X;gy=intent.Y;gz=intent.Z;stop=intent.StopDistance;}
            else if(resupply)
            {float best=float.PositiveInfinity;for(int i=0;i<map.mainBases.Length;i++){var b=map.mainBases[i];if(b.team!=self.Team||BaseFallen(i))continue;float score=Square(b.x-self.X)+Square(b.z-self.Z)+4*Square(b.y-self.Y);if(score<best){best=score;core=i;gx=b.x;gy=b.y;gz=b.z;}}stop=config.homeSupplyRadius*.5f;}
            else
            {
                // Public roles: most hold the main street, others defend A/X or use the lower corridor.
                int ownNorth=self.Team==0?1:2,ownMouth=self.Team==0?4:5;
                if(self.Y<map.surfaceY-4||role<2)relay=CaptureOwner(ownMouth)==self.Team||self.Y<map.surfaceY-4?3:ownMouth;
                else if(role<4)relay=CaptureOwner(ownNorth)==self.Team?0:ownNorth;
                else relay=0;
                if(relay>=0&&CaptureOwner(relay)!=self.Team){var c=map.capturePoints[relay];gx=c.x;gy=c.y;gz=c.z;stop=c.radius*.4f;}
                else
                {float best=float.PositiveInfinity;for(int i=0;i<map.mainBases.Length;i++){var b=map.mainBases[i];if(b.team==self.Team||BaseFallen(i))continue;float lane=role<2?-220:role<4?185:0;float score=Math.Abs(b.z-lane)+.02f*Math.Abs(b.x-self.X);if(score<best){best=score;core=i;gx=b.x;gy=b.y;gz=b.z;}}stop=core<0?4:map.mainBases[core].attackRadius+2.2f;}
            }
            layerBase[slot]=resupply?-1:core;
            float dx=gx-self.X,dz=gz-self.Z,dist=(float)Math.Sqrt(dx*dx+dz*dz);
            fire=!resupply&&core>=0&&Math.Abs(self.Y-gy)<2&&dist<map.mainBases[core].buildingRadius;
            yaw=(float)(Math.Atan2(dx,dz)*180/Math.PI);
            int goal=NearestLayerNode(gx,gy,gz),start=NearestLayerNode(self.X,self.Y,self.Z);
            if(layerGoal[slot]!=goal||layerLife[slot]!=self.UnitId||layerCount[slot]==0)
            {BuildLayerPath(slot,start,goal);layerGoal[slot]=goal;layerLife[slot]=self.UnitId;}
            while(layerCursor[slot]<layerCount[slot])
            {var n=nodes[layerPath[slot,layerCursor[slot]]];if(Square(n.x-self.X)+Square(n.z-self.Z)<6.25f&&Math.Abs(n.y-self.Y)<.65f)layerCursor[slot]++;else break;}
            if(layerCursor[slot]<layerCount[slot]){var n=nodes[layerPath[slot,layerCursor[slot]]];dx=n.x-self.X;dz=n.z-self.Z;dist=(float)Math.Sqrt(dx*dx+dz*dz);stop=.15f;}
            else if(Math.Abs(self.Y-gy)>2){BuildLayerPath(slot,start,goal);}
            float length=Math.Max(.001f,dist);mx=dist>stop?dx/length:0;mz=dist>stop?dz/length:0;
        }
        private int NearestLayerNode(float x,float y,float z)
        {int best=0;float score=float.PositiveInfinity;for(int i=0;i<map.navigationNodes.Length;i++){var n=map.navigationNodes[i];float d=Square(n.x-x)+Square(n.z-z)+64*Square(n.y-y);if(d<score){score=d;best=i;}}return best;}
        private void BuildLayerPath(int slot,int start,int goal)
        {
            var nodes=map.navigationNodes;for(int i=0;i<nodes.Length;i++){routeDistance[i]=float.PositiveInfinity;routePrevious[i]=-1;routeVisited[i]=false;}routeDistance[start]=0;
            for(int visit=0;visit<nodes.Length;visit++)
            {int current=-1;float best=float.PositiveInfinity;for(int i=0;i<nodes.Length;i++)if(!routeVisited[i]&&routeDistance[i]<best){best=routeDistance[i];current=i;}if(current<0||current==goal)break;routeVisited[current]=true;var a=nodes[current];foreach(int next in a.links){var b=nodes[next];float candidate=best+(float)Math.Sqrt(Square(a.x-b.x)+Square(a.y-b.y)+Square(a.z-b.z));if(candidate<routeDistance[next]){routeDistance[next]=candidate;routePrevious[next]=current;}}}
            int count=0;for(int at=goal;at>=0&&count<nodes.Length;at=routePrevious[at]){layerPath[slot,count++]=at;if(at==start)break;}
            for(int i=0;i<count/2;i++){int swap=layerPath[slot,i];layerPath[slot,i]=layerPath[slot,count-1-i];layerPath[slot,count-1-i]=swap;}layerCount[slot]=count;layerCursor[slot]=0;
        }
        private static float Square(float value)=>value*value;
    }
}
