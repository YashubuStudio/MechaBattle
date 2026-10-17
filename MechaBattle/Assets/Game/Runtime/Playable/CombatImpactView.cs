using MechaBattle.Simulation;
using UnityEngine;
namespace MechaBattle.Playable
{
    internal sealed class CombatImpactView
    {
        private readonly LineRenderer[] effects=new LineRenderer[96];
        private readonly float[] until=new float[96],duration=new float[96];private readonly Vector3[] positions=new Vector3[96];
        private readonly bool[] kills=new bool[96];private int next;private long sequence;
        private readonly Material flash,repair;
        public CombatImpactView(Transform root,Material flash,Material repair)
        {
            this.flash=flash;this.repair=repair;
            for(int i=0;i<effects.Length;i++){var go=new GameObject("CombatImpact"+i);go.transform.SetParent(root,false);var line=go.AddComponent<LineRenderer>();effects[i]=line;line.useWorldSpace=true;line.positionCount=12;line.widthMultiplier=0.065f;line.enabled=false;line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;}
        }
        public void Sync(BattleSimulation world)
        {
            long first=System.Math.Max(sequence+1,world.CombatEventSequence-world.CombatEventCapacity+1);
            for(long n=first;n<=world.CombatEventSequence;n++)
            {
                if(!world.TryGetCombatEvent(n,out var e) || e.Kind==CombatEventKind.CoreHit)continue;
                int i=next++%effects.Length;positions[i]=new Vector3(e.X,e.Y,e.Z);kills[i]=e.Kind==CombatEventKind.Destroyed || e.Kind==CombatEventKind.ObjectDestroyed;
                duration[i]=kills[i]?0.75f:e.Kind==CombatEventKind.Healed?0.2f:0.16f;until[i]=Time.unscaledTime+duration[i];
                effects[i].sharedMaterial=e.Kind==CombatEventKind.Healed?repair:flash;effects[i].widthMultiplier=kills[i]?0.18f:0.065f;
            }
            sequence=world.CombatEventSequence;
            for(int i=0;i<effects.Length;i++)
            {
                float life=until[i]-Time.unscaledTime;effects[i].enabled=life>0;if(life<=0)continue;
                float radius=kills[i]?Mathf.Lerp(0.7f,3.5f,1-life/duration[i]):Mathf.Lerp(0.12f,0.65f,life/duration[i]);
                var p=positions[i];
                for(int j=0;j<6;j++){float angle=j*Mathf.PI/3;Vector3 v=new Vector3(Mathf.Cos(angle),j%2==0?0.65f:-0.65f,Mathf.Sin(angle))*radius;effects[i].SetPosition(j*2,p+v);effects[i].SetPosition(j*2+1,p-v*0.25f);}
            }
        }
    }
}
