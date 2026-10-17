using System;
namespace MechaBattle.Contracts
{
    public enum BotRole { Frontline, RadarSupport, Interceptor }
    [Serializable] public sealed class BotBehaviorRules
    {
        public bool enabled;
        public bool smartObjectivesEnabled,squadTacticsEnabled;
        public int squadsPerTeam=2,squadSize=4,maximumRespondersPerContact=2;
        public float objectiveCommitSeconds=8,squadRallyDistance=36;
        public float radarFraction=.04f,interceptorFraction=.08f,frontlineMapReaderFraction=.35f;
        public float mapThinkSeconds=1.5f,mapReactionSeconds=.8f,defenseRadius=170,mapInterestRadius=180,radarMinimumIntervalSeconds=45;
        public int seed=7321;
        public BotBehaviorRules Copy()=>(BotBehaviorRules)MemberwiseClone();
        public void Validate()
        {
            foreach(float rate in new[]{radarFraction,interceptorFraction,frontlineMapReaderFraction})if(!Finite(rate)||rate<0||rate>1)throw new ArgumentException("Invalid BOT role fraction.");
            if(squadsPerTeam<0||squadSize<1||(long)squadsPerTeam*squadSize>50||maximumRespondersPerContact<1||maximumRespondersPerContact>50)throw new ArgumentException("Invalid BOT squad/response limits.");
            if(!Finite(objectiveCommitSeconds)||objectiveCommitSeconds<=0||objectiveCommitSeconds>60||!Finite(squadRallyDistance)||squadRallyDistance<4||squadRallyDistance>200)throw new ArgumentException("Invalid BOT objective/rally timing.");
            if(radarFraction+interceptorFraction>1.000001f)throw new ArgumentException("BOT role fractions exceed 100%.");
            foreach(float v in new[]{mapThinkSeconds,mapReactionSeconds,defenseRadius,mapInterestRadius,radarMinimumIntervalSeconds})if(!Finite(v)||v<=0||v>3600)throw new ArgumentException("Invalid BOT tactical timing/range.");
        }
        private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    }
    // A public-map destination is not a firing target. Only the visual sensor provides one.
    public readonly struct BotTacticalIntent
    {
        public readonly bool HasGoal,DeployRadar;public readonly float X,Y,Z,StopDistance;
        public BotTacticalIntent(float x,float y,float z,float stopDistance,bool deployRadar=false)
        {HasGoal=true;X=x;Y=y;Z=z;StopDistance=stopDistance;DeployRadar=deployRadar;}
    }
}
