using System;
namespace MechaBattle.Simulation
{
    public sealed partial class BattleSimulation
    {
        private bool IntegrateAerialVelocity(UnitState u)
        {
            bool canThrust=!u.BoostOverheated && CurrentTick>=u.DisabledUntil && CurrentTick>=u.MeleeUntil && !u.MeleeCharging && u.HasCommand && u.Boost>0;
            if(canThrust && u.Command.Jump)
            {
                float required=config.jumpHoldDrainPerSecond*FixedDeltaTime;
                float fraction=Math.Min(1,u.Boost/required);
                float remaining=Math.Max(0,u.AerialOriginY+config.aerialMaximumRise-u.Y);
                float target=Math.Min(config.aerialAscentSpeed,(float)Math.Sqrt(2*config.aerialAscentAcceleration*remaining));
                float powered=Towards(u.VerticalVelocity,target,config.aerialAscentAcceleration*FixedDeltaTime);
                u.VerticalVelocity+=(powered-u.VerticalVelocity)*fraction;
                u.VerticalVelocity-=config.gravity*FixedDeltaTime*(1-fraction);
                ConsumeBoost(u,Math.Min(u.Boost,required));
                return true;
            }
            bool glide=canThrust && u.Command.Boost && u.Command.MoveX*u.Command.MoveX+u.Command.MoveY*u.Command.MoveY>0.0001f;
            float falling=u.VerticalVelocity-config.gravity*FixedDeltaTime;
            if(glide && falling < -config.aerialGlideFallSpeed)
            {
                // Horizontal dash pays the shared fuel cost in Move(), once per tick.
                float fraction=Math.Min(1,u.Boost/(config.boostDrainPerSecond*FixedDeltaTime));
                float controlled=u.VerticalVelocity < -config.aerialGlideFallSpeed
                    ? Towards(u.VerticalVelocity,-config.aerialGlideFallSpeed,config.aerialAscentAcceleration*FixedDeltaTime)
                    : -config.aerialGlideFallSpeed;
                falling+=(controlled-falling)*fraction;
            }
            u.VerticalVelocity=falling;return false;
        }
        private void IntegrateHorizontalMotion(UnitState u,float vx,float vz)
        {
            if(config.aerialBoostEnabled && !u.Grounded && CurrentTick>=u.MeleeUntil && !u.MeleeCharging)
            {
                float dx=vx-u.AerialVelocityX,dz=vz-u.AerialVelocityZ;
                float length=(float)Math.Sqrt(dx*dx+dz*dz),step=config.aerialControlAcceleration*FixedDeltaTime;
                if(length>step){dx*=step/length;dz*=step/length;}
                vx=u.AerialVelocityX+dx;vz=u.AerialVelocityZ+dz;
            }
            float x=u.X,z=u.Z;
            MoveHorizontal(u,vx*FixedDeltaTime,vz*FixedDeltaTime);
            // Collision-clipped motion prevents stored velocity from pushing through walls.
            u.AerialVelocityX=(u.X-x)/FixedDeltaTime;u.AerialVelocityZ=(u.Z-z)/FixedDeltaTime;
        }
        private static float Towards(float from,float to,float step)=>from<to?Math.Min(from+step,to):Math.Max(from-step,to);
    }
}
