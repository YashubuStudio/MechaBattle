using System;
using MechaBattle.Contracts;
using MechaBattle.Simulation;
using TMPro;
using UnityEngine;
namespace MechaBattle.Playable
{
    internal sealed class StrategyWorldView : IDisposable
    {
        private readonly GameObject root;private readonly Transform[] markers=new Transform[6];
        private readonly LineRenderer[] rings=new LineRenderer[6];private readonly TMP_Text[] labels=new TMP_Text[6];
        private readonly GameObject[] flashes=new GameObject[6];private readonly Transform[] payloads=new Transform[6];private readonly Material[] materials=new Material[6];
        private readonly Camera camera;
        public StrategyWorldView(Transform parent,Camera camera,TMP_FontAsset font,Shader unlit)
        {
            this.camera=camera;root=new GameObject("StrategyEffects");root.transform.SetParent(parent,false);
            var colors=new[]{new Color(1,.25f,.07f),new Color(.75f,.35f,1),new Color(.2f,1,.65f)};
            for(int k=0;k<3;k++){materials[k]=new Material(unlit){name="StrategySignal"+k};materials[k].color=colors[k];}
            materials[3]=new Material(unlit){name="NuclearCase",color=new Color(.12f,.18f,.23f)};
            materials[4]=new Material(unlit){name="NuclearBlue",color=new Color(.25f,.55f,1)};
            materials[5]=new Material(unlit){name="NuclearOrange",color=new Color(1,.55f,.16f)};
            for(int i=0;i<6;i++)
            {
                markers[i]=new GameObject("StrategySignal"+i).transform;markers[i].SetParent(root.transform,false);
                payloads[i]=new GameObject("PortableNuclearDevice").transform;payloads[i].SetParent(markers[i],false);
                Shape(payloads[i],"ReactorCase",new Vector3(0,.62f,0),new Vector3(.95f,1.15f,.8f),materials[3]);
                Shape(payloads[i],"WarningBand",new Vector3(0,.65f,0),new Vector3(1.0f,.18f,.84f),materials[0]);
                Shape(payloads[i],"TeamPanel",new Vector3(0,.95f,-.42f),new Vector3(.6f,.2f,.08f),materials[4+i/3]);
                Shape(payloads[i],"LeftHandle",new Vector3(-.56f,.9f,0),new Vector3(.15f,.5f,.4f),materials[3]);
                Shape(payloads[i],"RightHandle",new Vector3(.56f,.9f,0),new Vector3(.15f,.5f,.4f),materials[3]);
                var ring=new GameObject("Area",typeof(LineRenderer));ring.transform.SetParent(markers[i],false);rings[i]=ring.GetComponent<LineRenderer>();
                rings[i].useWorldSpace=false;rings[i].loop=true;rings[i].positionCount=64;rings[i].widthMultiplier=.25f;
                for(int p=0;p<64;p++){float a=p*Mathf.PI*2/64;rings[i].SetPosition(p,new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)));}
                var text=new GameObject("Warning",typeof(TextMeshPro));text.transform.SetParent(markers[i],false);labels[i]=text.GetComponent<TextMeshPro>();labels[i].font=font;labels[i].fontSize=7;labels[i].alignment=TextAlignmentOptions.Center;labels[i].rectTransform.sizeDelta=new Vector2(45,16);labels[i].transform.localPosition=Vector3.up*14;
                flashes[i]=GameObject.CreatePrimitive(PrimitiveType.Sphere);flashes[i].name="DetonationFlash";flashes[i].transform.SetParent(markers[i],false);
                var collider=flashes[i].GetComponent<Collider>();if(collider!=null)UnityEngine.Object.Destroy(collider);
                flashes[i].GetComponent<Renderer>().sharedMaterial=materials[0];flashes[i].SetActive(false);
            }
        }
        public void Sync(BattleSimulation world,int viewerSlot)
        {
            for(int i=0;i<6;i++)
            {
                var s=world.GetStrategyForWorld(i,viewerSlot);markers[i].gameObject.SetActive(s.Active);if(!s.Active)continue;
                markers[i].position=new Vector3(s.X,s.Y+.12f,s.Z);rings[i].sharedMaterial=materials[(int)s.Kind];float radius=s.Kind==StrategyKind.Nuclear&&s.NuclearPhase<NuclearPhase.Arming?1.5f:s.Radius;rings[i].transform.localScale=new Vector3(radius,1,radius);
                rings[i].enabled=s.NuclearPhase!=NuclearPhase.Carried;payloads[i].gameObject.SetActive(s.Kind==StrategyKind.Nuclear&&!s.Detonated);
                payloads[i].rotation=s.NuclearPhase==NuclearPhase.Carried?Quaternion.Euler(0,world.GetUnit(s.CarrierSlot).AimYaw,0):Quaternion.identity;
                labels[i].text=TacticalMapView.Title(s.Kind)+"\n"+TacticalMapView.StageLabel(s)+(s.NuclearPhase==NuclearPhase.Arming?" / 味方も退避":"");
                labels[i].transform.localPosition=Vector3.up*(s.Kind==StrategyKind.Nuclear&&s.NuclearPhase<NuclearPhase.Arming?2.5f:14);labels[i].fontSize=s.Kind==StrategyKind.Nuclear&&s.NuclearPhase<NuclearPhase.Arming?2.5f:7;
                labels[i].color=materials[(int)s.Kind].color;labels[i].transform.rotation=camera.transform.rotation;
                flashes[i].SetActive(s.Detonated&&s.RemainingSeconds>1.5f);
                if(flashes[i].activeSelf){float elapsed=2-s.RemainingSeconds;flashes[i].transform.localScale=Vector3.one*s.Radius*Mathf.Clamp01(elapsed*4);}
            }
        }
        private static void Shape(Transform parent,string name,Vector3 position,Vector3 size,Material material)
        {
            var shape=GameObject.CreatePrimitive(PrimitiveType.Cube);shape.name=name;shape.transform.SetParent(parent,false);shape.transform.localPosition=position;shape.transform.localScale=size;
            shape.GetComponent<Renderer>().sharedMaterial=material;var collider=shape.GetComponent<Collider>();if(collider!=null){collider.enabled=false;UnityEngine.Object.Destroy(collider);}
        }
        public void Dispose(){root.SetActive(false);root.transform.SetParent(null,false);UnityEngine.Object.Destroy(root);foreach(var material in materials)UnityEngine.Object.Destroy(material);}
    }
}
