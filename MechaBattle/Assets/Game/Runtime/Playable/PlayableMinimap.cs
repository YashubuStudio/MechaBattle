using MechaBattle.Battlefield;
using MechaBattle.Contracts;
using MechaBattle.Simulation;
using UnityEngine;
using TMPro;
namespace MechaBattle.Playable
{
    internal sealed class PlayableMinimap
    {
        private readonly UnityEngine.UI.Image[] units=new UnityEngine.UI.Image[100];
        private readonly UnityEngine.UI.Image[] bases,captures;
        private readonly UnityEngine.UI.Image[] equipment=new UnityEngine.UI.Image[BattleSimulation.MaximumDeployables];
        private readonly ReticleRingGraphic[] coverage=new ReticleRingGraphic[BattleSimulation.MaximumDeployables];
        private readonly Transform panel,coverageRoot;
        private readonly RectTransform field;
        private readonly MapDefinition map;
        private readonly Color[] teams={new Color(0.3f,0.6f,1),new Color(1,0.5f,0.2f)};
        public PlayableMinimap(RectTransform panel,MapDefinition map)
        {
            this.panel=panel;this.map=map;if(panel.GetComponent<UnityEngine.UI.RectMask2D>()==null)panel.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            field=new GameObject("MapField",typeof(RectTransform)).GetComponent<RectTransform>();field.SetParent(panel,false);
            field.anchorMin=field.anchorMax=field.pivot=Vector2.one*.5f;
            field.sizeDelta=MapTerrainBackground.FitSize(map,panel.rect.size-new Vector2(20,20));
            MapTerrainBackground.Draw(field,map);
            var zones=new GameObject("RadarCoverage",typeof(RectTransform));zones.transform.SetParent(field,false);coverageRoot=zones.transform;
            var z=zones.GetComponent<RectTransform>();z.anchorMin=z.anchorMax=new Vector2(0.5f,0.5f);z.sizeDelta=Vector2.zero;
            bases=new UnityEngine.UI.Image[map.mainBases.Length];captures=new UnityEngine.UI.Image[map.capturePoints.Length];
            for(int i=0;i<bases.Length;i++){var b=map.mainBases[i];bases[i]=Dot(field,"Base",11,teams[b.team]);Position(bases[i].rectTransform,b.x,b.z);Label(bases[i].transform,map.MainBaseName(i));}
            for(int i=0;i<captures.Length;i++){var c=map.capturePoints[i];captures[i]=Dot(field,"Relay",9,Color.gray);Position(captures[i].rectTransform,c.x,c.z);Label(captures[i].transform,c.id);}
            for(int i=0;i<units.Length;i++)units[i]=Dot(field,"Unit"+i,4,Color.white);
        }
        public void Update(BattleSimulation world,int human,int lockedObject=-1,int lockedId=0)
        {
            var self=world.GetUnit(human);float range=world.GetActiveWeapon(human).FireRange;
            for(int i=0;i<units.Length;i++)
            {
                var u=world.GetUnit(i);var report=world.GetTeamContact(self.Team,i);
                float dx=self.X-u.X,dz=self.Z-u.Z,dy=self.Y-u.Y;
                bool ally=u.Team==self.Team;
                bool local=!ally && world.CanVisuallySee(human,i,Mathf.Min(range,180));
                bool visible=u.IsAlive && (ally||local||report.Valid);
                units[i].gameObject.SetActive(visible);if(!visible)continue;
                // Without a local observation, use the report's frozen last known position.
                Position(units[i].rectTransform,ally||local?u.X:report.X,ally||local?u.Z:report.Z);
                units[i].color=i==human?Color.white:ally?teams[u.Team]:report.LockDetected?new Color(1,0.2f,0.18f):report.RadarDetected?new Color(1,0.84f,0.2f):teams[u.Team];
                units[i].rectTransform.sizeDelta=Vector2.one*(i==human?8:report.LockDetected?8:report.RadarDetected?6:4);
                units[i].rectTransform.localRotation=Quaternion.Euler(0,0,!ally&&report.LockDetected?45:0);
            }
            for(int i=0;i<world.DeployableCapacity;i++)
            {
                var d=world.GetDeployable(i);bool visible=d.Active && (d.Team==self.Team || i==lockedObject && d.Id==lockedId && world.IsDeployableYellowLocked(human,i,lockedId));
                if(!visible){if(equipment[i]!=null)equipment[i].gameObject.SetActive(false);if(coverage[i]!=null)coverage[i].gameObject.SetActive(false);continue;}
                if(equipment[i]==null)equipment[i]=Dot(field,"AlliedEquipment"+i,7,new Color(0.3f,1,0.75f));
                equipment[i].gameObject.SetActive(true);Position(equipment[i].rectTransform,d.X,d.Z);
                equipment[i].color=d.Team!=self.Team?new Color(1,0.2f,0.18f):d.Kind==WeaponKind.StrategicNuclear?new Color(1,.3f,.12f):d.Kind==WeaponKind.Radar?new Color(1,0.9f,0.2f):new Color(0.3f,1,0.75f);
                if(d.Kind!=WeaponKind.Radar || d.Team!=self.Team){if(coverage[i]!=null)coverage[i].gameObject.SetActive(false);continue;}
                if(coverage[i]==null){var go=new GameObject("RadarRange"+i,typeof(RectTransform),typeof(CanvasRenderer));go.transform.SetParent(coverageRoot,false);coverage[i]=go.AddComponent<ReticleRingGraphic>();coverage[i].Elliptical=true;coverage[i].raycastTarget=false;}
                coverage[i].gameObject.SetActive(d.Armed);coverage[i].color=new Color(0.6f,0.9f,0.35f,0.4f);
                var rect=coverage[i].rectTransform;rect.sizeDelta=new Vector2(d.Radius/map.halfWidth*field.rect.width,d.Radius/map.halfDepth*field.rect.height);Position(rect,d.X,d.Z);
            }
            for(int i=0;i<bases.Length;i++)bases[i].color=world.GetBase(i).Destroyed?Color.gray:teams[map.mainBases[i].team];
            for(int i=0;i<captures.Length;i++){int owner=world.GetCapturePoint(i).OwnerTeam;captures[i].color=owner<0?Color.gray:teams[owner];}
        }
        public void Dispose()
        {
            while(panel.childCount>0){var child=panel.GetChild(0);child.gameObject.SetActive(false);child.SetParent(null,false);UnityEngine.Object.Destroy(child.gameObject);}
        }
        private void Position(RectTransform rect,float x,float z){rect.anchoredPosition=MapTerrainBackground.Position(field,map,x,z);}
        private static void Label(Transform parent,string value)
        {var go=new GameObject("ObjectiveName",typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);var text=go.GetComponent<TextMeshProUGUI>();text.font=TMP_Settings.defaultFontAsset;text.fontSize=14;text.text=value;text.alignment=TextAlignmentOptions.Center;text.color=Color.white;text.raycastTarget=false;var rect=go.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(40,20);rect.anchoredPosition=new Vector2(0,-12);}
        private static UnityEngine.UI.Image Dot(Transform parent,string name,float size,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image));go.transform.SetParent(parent,false);
            var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(0.5f,0.5f);rect.sizeDelta=Vector2.one*size;
            var image=go.GetComponent<UnityEngine.UI.Image>();image.color=color;image.raycastTarget=false;return image;
        }
    }
}
