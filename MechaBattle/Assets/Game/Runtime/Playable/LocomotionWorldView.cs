using MechaBattle.Contracts;
using MechaBattle.Simulation;
using UnityEngine;
namespace MechaBattle.Playable
{
    // A lightweight procedural rig, driven only by observed simulation movement.
    // Poses never alter collision, aim, damage or controller input.
    internal sealed class LocomotionWorldView
    {
        public const float CrouchDrop=.3f;
        private readonly GameObject[] previous = new GameObject[100];
        private readonly Transform[] left = new Transform[100], right = new Transform[100], leftKnee = new Transform[100], rightKnee = new Transform[100], leftArm = new Transform[100];
        private readonly Vector3[] positions = new Vector3[100], velocity = new Vector3[100];
        private readonly float[] phases = new float[100], landing = new float[100];
        private readonly int[] ids = new int[100]; private readonly long[] ticks = new long[100]; private readonly bool[] grounded = new bool[100];
        public void Sync(BattleSimulation world, GameObject[] models)
        {
            for(int i=0;i<models.Length;i++)
            {
                var model=models[i];if(model==null)continue;var u=world.GetUnit(i);var position=new Vector3(u.X,u.Y,u.Z);
                if(previous[i]!=model)
                {
                    previous[i]=model;left[i]=model.transform.Find("LeftLeg");right[i]=model.transform.Find("RightLeg");
                    leftKnee[i]=left[i]==null?null:left[i].Find("Knee");rightKnee[i]=right[i]==null?null:right[i].Find("Knee");leftArm[i]=model.transform.Find("LeftArm");
                }
                if(ids[i]!=u.UnitId){ids[i]=u.UnitId;positions[i]=position;ticks[i]=world.CurrentTick;velocity[i]=Vector3.zero;landing[i]=0;grounded[i]=u.Grounded;}
                float dt=(world.CurrentTick-ticks[i])*world.FixedDeltaTime;
                if(dt>0)
                {
                    var next=(position-positions[i])/dt;next.y=0;
                    velocity[i]=Vector3.Lerp(velocity[i],next,1-Mathf.Exp(-14*dt));positions[i]=position;ticks[i]=world.CurrentTick;
                    if(!grounded[i]&&u.Grounded)landing[i]=1;else landing[i]=Mathf.Max(0,landing[i]-dt*5);
                    grounded[i]=u.Grounded;phases[i]+=Mathf.Min(22,velocity[i].magnitude*1.8f)*dt;
                }
                float speed=velocity[i].magnitude;bool dash=u.BoostMoving;float blend=1-Mathf.Exp(-18*Mathf.Min(0.05f,Time.unscaledDeltaTime));
                bool down=u.Disruption==DisruptionState.KnockedDown, stagger=u.Disruption==DisruptionState.Staggered, attack=u.MeleePhase>=0;
                Vector3 motion=Quaternion.Euler(0,-u.AimYaw,0)*velocity[i];float activity=Mathf.Clamp01(speed/3);
                float wave=Mathf.Sin(phases[i])*activity;
                float hipL,hipR,kneeL,kneeR;
                float rise=Mathf.Clamp(u.VerticalVelocity/6,-1,1);
                if(u.Crouching){hipL=hipR=-48;kneeL=kneeR=84;}
                else if(!u.Grounded){hipL=-18-rise*10;hipR=-10-rise*8;kneeL=38+rise*17+(dash?8:0);kneeR=30+rise*12+(dash?8:0);}
                else if(dash){hipL=hipR=15;kneeL=kneeR=35;}
                else {hipL=wave*24;hipR=-wave*24;kneeL=Mathf.Max(0,-wave)*32;kneeR=Mathf.Max(0,wave)*32;}
                if(left[i]!=null)left[i].localRotation=Quaternion.Slerp(left[i].localRotation,Quaternion.Euler(down?0:hipL,0,down?0:-motion.x*0.65f),blend);
                if(right[i]!=null)right[i].localRotation=Quaternion.Slerp(right[i].localRotation,Quaternion.Euler(down?0:hipR,0,down?0:-motion.x*0.65f),blend);
                if(leftKnee[i]!=null)leftKnee[i].localRotation=Quaternion.Slerp(leftKnee[i].localRotation,Quaternion.Euler(down?0:kneeL,0,0),blend);
                if(rightKnee[i]!=null)rightKnee[i].localRotation=Quaternion.Slerp(rightKnee[i].localRotation,Quaternion.Euler(down?0:kneeR,0,0),blend);
                if(leftArm[i]!=null)leftArm[i].localRotation=Quaternion.Slerp(leftArm[i].localRotation,Quaternion.Euler(world.IsCarryingNuclear(i)?-65:u.Guarding?-12:-wave*12,0,world.IsCarryingNuclear(i)?-18:u.Guarding?-8:0),blend);
                float tilt=down?85:stagger?-18:attack?12:u.Crouching?14:!u.Grounded?(dash?16:5)-rise*7:dash?20:Mathf.Clamp(motion.z*0.55f,-7,9);
                model.transform.localRotation=Quaternion.Slerp(model.transform.localRotation,Quaternion.Euler(tilt,0,down?0:-Mathf.Clamp(motion.x*(u.Grounded?0.45f:0.7f),u.Grounded?-8:-14,u.Grounded?8:14)),blend);
                model.transform.localPosition=Vector3.Lerp(model.transform.localPosition,Vector3.up*(down?0.15f:u.Crouching?-CrouchDrop:Mathf.Abs(wave)*0.035f-landing[i]*0.06f),blend);
            }
        }
    }
}
