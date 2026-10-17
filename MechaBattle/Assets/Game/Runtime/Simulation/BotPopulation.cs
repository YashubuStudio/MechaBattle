using System;
using MechaBattle.Contracts;
namespace MechaBattle.Simulation
{
    // Draw roles once per match, using per-team quotas so rare roles cannot clump.
    public sealed class BotPopulation
    {
        private readonly BotRole[] roles=new BotRole[100];private readonly bool[] bots=new bool[100],readers=new bool[100];private readonly int[] ranks=new int[100];
        public bool Enabled {get;}
        public BotPopulation(BotBehaviorRules rules,int seed,int humanSlot=-1):this(rules,seed,CreateMask(humanSlot)){}
        public BotPopulation(BotBehaviorRules rules,int seed,bool[] botSlots)
        {
            if(rules==null)throw new ArgumentNullException(nameof(rules));rules.Validate();
            if(botSlots==null||botSlots.Length!=100)throw new ArgumentException("Expected 100 controller flags.");
            Enabled=rules.enabled;Array.Copy(botSlots,bots,100);
            for(int team=0;team<2;team++)
            {
                var slots=new int[50];int count=0;for(int i=team*50;i<team*50+50;i++)if(bots[i])slots[count++]=i;
                uint state=unchecked((uint)seed+0x9e3779b9u*(uint)(team+1));if(state==0)state=1;
                for(int i=count-1;i>0;i--){state^=state<<13;state^=state>>17;state^=state<<5;int j=(int)(state%(uint)(i+1)),v=slots[i];slots[i]=slots[j];slots[j]=v;}
                int radar=Enabled?(int)Math.Floor(count*rules.radarFraction+.5):0;
                int defense=Enabled?Math.Min(count-radar,(int)Math.Floor(count*rules.interceptorFraction+.5)):0;
                int mapReaders=(int)Math.Floor((count-radar-defense)*rules.frontlineMapReaderFraction+.5);
                for(int i=0;i<count;i++){int slot=slots[i];roles[slot]=i<radar?BotRole.RadarSupport:i<radar+defense?BotRole.Interceptor:BotRole.Frontline;ranks[slot]=i<radar?i:i<radar+defense?i-radar:i-radar-defense;readers[slot]=!Enabled||i<radar+defense+mapReaders;}
            }
        }
        private static bool[] CreateMask(int human){var mask=new bool[100];for(int i=0;i<100;i++)mask[i]=i!=human;return mask;}
        public bool IsBot(int slot)=>slot>=0&&slot<100&&bots[slot];
        public BotRole RoleOf(int slot)=>roles[slot];public bool ReadsMap(int slot)=>IsBot(slot)&&readers[slot];public int RoleRank(int slot)=>ranks[slot];
    }
}
