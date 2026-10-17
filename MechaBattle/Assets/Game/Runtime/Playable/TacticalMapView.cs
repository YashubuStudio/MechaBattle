using System;
using MechaBattle.Battlefield;
using MechaBattle.Contracts;
using MechaBattle.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
namespace MechaBattle.Playable
{
    public sealed class TacticalMapClick : MonoBehaviour, IPointerClickHandler
    {
        public Action<Vector2> Choose;
        public void OnPointerClick(PointerEventData data)
        {
            if(data.button!=PointerEventData.InputButton.Left)return;
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform,data.position,data.pressEventCamera,out var local))Choose?.Invoke(local);
        }
    }
    internal sealed class TacticalMapView : IDisposable
    {
        public readonly GameObject Root;
        private readonly MapDefinition map; private readonly RectTransform field,selection;
        private readonly ReticleRingGraphic preview;
        private readonly TMP_FontAsset font;private readonly TMP_Text points,details,message,active;
        private readonly UnityEngine.UI.Button confirm;private readonly UnityEngine.UI.Button[] choices=new UnityEngine.UI.Button[3];
        private readonly UnityEngine.UI.Image[] units=new UnityEngine.UI.Image[100];
        private readonly ReticleRingGraphic[] zones=new ReticleRingGraphic[6];private readonly TMP_Text[] zoneLabels=new TMP_Text[6];
        private readonly Action<StrategyKind> choose;private readonly TMP_Text[] baseStatus;
        private readonly Color accent=new Color(.3f,.9f,.76f);
        public TacticalMapView(Transform parent,TMP_FontAsset font,MapDefinition map,Action close,Action<StrategyKind> choose,Action<float,float> point,Action use)
        {
            this.map=map;this.font=font;this.choose=choose;
            var root=Rect(parent,"TacticalMap",Vector2.zero,new Vector2(1920,1080));Root=root.gameObject;
            root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;
            Image(root,new Color(.025f,.04f,.06f,.97f),true);
            Text(root,"Title","戦術マップ / 戦闘は進行中",new Vector2(-350,475),new Vector2(1100,65),36);
            Button(root,"Close","戦闘へ戻る [M / Esc]",new Vector2(685,470),new Vector2(430,66),close);
            points=Text(root,"StrategyPoints","",new Vector2(-675,355),new Vector2(410,120),28);
            for(int i=0;i<3;i++)
            {int kind=i;choices[i]=Button(root,"Strategy"+(StrategyKind)i,"",new Vector2(-675,190-i*105),new Vector2(410,90),()=>choose((StrategyKind)kind));}
            details=Text(root,"StrategyDetails","",new Vector2(-675,-170),new Vector2(410,210),21);
            confirm=Button(root,"ConfirmStrategy","指定地点へ発動",new Vector2(-675,-335),new Vector2(410,76),use);
            message=Text(root,"StrategyMessage","地図をクリックして地点を指定",new Vector2(-675,-440),new Vector2(410,115),22);
            field=Rect(root,"StrategyMapField",new Vector2(230,80),MapTerrainBackground.FitSize(map,new Vector2(1190,690)));
            Image(field,new Color(.075f,.13f,.17f),true);
            var click=field.gameObject.AddComponent<TacticalMapClick>();click.Choose=local=>point(local.x/field.rect.width*map.halfWidth*2,local.y/field.rect.height*map.halfDepth*2);
            MapTerrainBackground.Draw(field,map);
            baseStatus=new TMP_Text[map.mainBases.Length];
            for(int i=0;i<map.mainBases.Length;i++){var b=map.mainBases[i];baseStatus[i]=Text(field,"Base_"+b.id,"■ "+map.MainBaseName(i)+(map.surfaceY>0&&b.y<map.surfaceY?" "+map.LowerLayerName:""),Position(b.x,b.z),new Vector2(150,48),16);baseStatus[i].color=b.team==0?new Color(.4f,.7f,1):new Color(1,.65f,.3f);}
            foreach(var c in map.capturePoints)Text(field,"Relay_"+c.id,"◆ "+c.id+(map.surfaceY>0&&c.y<map.surfaceY?" "+map.LowerLayerName:""),Position(c.x,c.z),new Vector2(170,36),16);
            for(int i=0;i<100;i++)units[i]=Image(Rect(field,"MapContact"+i,Vector2.zero,new Vector2(6,6)),Color.white);
            for(int i=0;i<6;i++)
            {
                var go=Rect(field,"StrategyZone"+i,Vector2.zero,Vector2.one);go.gameObject.AddComponent<CanvasRenderer>();zones[i]=go.gameObject.AddComponent<ReticleRingGraphic>();zones[i].Elliptical=true;zones[i].raycastTarget=false;
                zoneLabels[i]=Text(field,"StrategyZoneLabel"+i,"",Vector2.zero,new Vector2(200,55),18);
            }
            var previewRect=Rect(field,"StrategyAreaPreview",Vector2.zero,Vector2.one);previewRect.gameObject.AddComponent<CanvasRenderer>();preview=previewRect.gameObject.AddComponent<ReticleRingGraphic>();preview.Elliptical=true;preview.raycastTarget=false;
            selection=Rect(field,"SelectedStrategyPoint",Vector2.zero,new Vector2(32,32));Text(selection,"Pin","＋",Vector2.zero,new Vector2(40,40),32).color=accent;
            active=Text(root,"ActiveStrategies","",new Vector2(230,-405),new Vector2(1200,140),22);
            Text(root,"Legend","↑ 北 / 水色線："+map.LowerLayerName+" / 青・橙：味方と共有索敵 / 核の範囲は両軍へ予告 / 地図クリック → 左の発動ボタン",new Vector2(230,-505),new Vector2(1200,40),20);
            Root.SetActive(false);
        }
        public void Refresh(BattleSimulation world,int human,StrategyKind kind,bool selected,float x,float z,string feedback)
        {
            var self=world.GetUnit(human);var team=world.GetStrategyTeam(self.Team);
            for(int i=0;i<baseStatus.Length;i++){var core=world.GetBase(i);baseStatus[i].text="■ "+core.Name+(map.surfaceY>0&&core.Y<map.surfaceY?" "+map.LowerLayerName:"")+"\n"+(core.Destroyed?"陥落":core.MaxHealth>0?"HP "+core.Health.ToString("0"):"本拠点");}
            points.text="戦略ポイント "+team.Points+" / "+team.MaximumPoints+"\n味方1機の撃破で +"+team.PointsPerLoss;
            for(int i=0;i<3;i++)
            {
                var d=world.GetStrategyDefinition((StrategyKind)i);float wait=world.GetStrategyCooldown(self.Team,(StrategyKind)i);
                choices[i].GetComponentInChildren<TMP_Text>().text=((int)kind==i?"▶ ":"")+Title((StrategyKind)i)+"  "+(d==null?0:d.cost)+" pt"+(wait>0?"\n再使用 "+wait.ToString("0.0")+"秒":"");
            }
            var definition=world.GetStrategyDefinition(kind);string reason="地図で地点を選択してください";
            bool allowed=selected&&world.CanRequestStrategy(human,kind,x,z,out reason);
            confirm.GetComponentInChildren<TMP_Text>().text=kind==StrategyKind.Nuclear?"この自軍側地点へ核を要請":"指定地点へ発動";
            confirm.interactable=allowed;selection.gameObject.SetActive(selected);preview.gameObject.SetActive(selected&&definition!=null);
            if(selected){selection.anchoredPosition=Position(x,z);preview.rectTransform.anchoredPosition=Position(x,z);if(definition!=null){float radius=kind==StrategyKind.Nuclear?3:definition.radius;preview.rectTransform.sizeDelta=new Vector2(radius/map.halfWidth*field.rect.width,radius/map.halfDepth*field.rect.height);if(kind==StrategyKind.Nuclear)preview.rectTransform.sizeDelta=Vector2.Max(preview.rectTransform.sizeDelta,Vector2.one*18);}preview.color=kind==StrategyKind.Nuclear?new Color(1,.35f,.2f,.9f):accent;}
            details.text=Title(kind)+" / 半径 "+(definition==null?0:definition.radius).ToString("0")+"m\n"+(kind==StrategyKind.Nuclear?"自軍本拠点の近くへ配送\nFで拾得・運搬\n着地後に左クリックで起動":kind==StrategyKind.Jammer?"敵の共有索敵を範囲内で遮断\n直接の目視には影響しない":"範囲内の敵位置を味方へ共有\nHP・壁越し射撃ロックは付与しない")+(definition==null?"":kind==StrategyKind.Nuclear?"\n起動後 "+definition.delaySeconds.ToString("0")+"秒で爆発":"\n予告 "+definition.delaySeconds.ToString("0")+"秒 / 持続 "+definition.durationSeconds.ToString("0")+"秒")+(selected?"\n指定 "+x.ToString("0")+", "+z.ToString("0"):"");
            message.text=!string.IsNullOrEmpty(feedback)?feedback:allowed?"地点を確認して発動してください":reason;
            for(int i=0;i<100;i++)
            {
                var u=world.GetUnit(i);bool ally=u.Team==self.Team;var c=world.GetTeamContact(self.Team,i);
                bool local=!ally&&world.CanVisuallySee(human,i,180);
                bool visible=ally?u.IsAlive:local||c.Valid;units[i].gameObject.SetActive(visible);if(!visible)continue;
                units[i].rectTransform.anchoredPosition=Position(ally||local?u.X:c.X,ally||local?u.Z:c.Z);
                units[i].color=i==human?Color.white:ally?new Color(.3f,.65f,1):new Color(1,.45f,.2f);
            }
            var status=new System.Text.StringBuilder();
            for(int i=0;i<6;i++)
            {
                var s=world.GetStrategy(i,self.Team);zones[i].gameObject.SetActive(s.Active);zoneLabels[i].gameObject.SetActive(s.Active);if(!s.Active)continue;
                zones[i].rectTransform.anchoredPosition=Position(s.X,s.Z);zones[i].rectTransform.sizeDelta=new Vector2((s.Kind==StrategyKind.Nuclear&&s.NuclearPhase<NuclearPhase.Arming?3:s.Radius)*field.rect.width/map.halfWidth,(s.Kind==StrategyKind.Nuclear&&s.NuclearPhase<NuclearPhase.Arming?3:s.Radius)*field.rect.height/map.halfDepth);
                zones[i].color=s.Kind==StrategyKind.Nuclear?new Color(1,.35f,.2f,.9f):s.Kind==StrategyKind.Jammer?new Color(.8f,.45f,1,.8f):new Color(.35f,1,.7f,.8f);
                zoneLabels[i].rectTransform.anchoredPosition=Position(s.X,s.Z);zoneLabels[i].text=Title(s.Kind)+"\n"+StageLabel(s);
                status.Append(s.Team==self.Team?"自軍 ":"敵軍 ").Append(zoneLabels[i].text.Replace('\n',' ')).Append("   ");
            }
            active.text=status.ToString();
        }
        public static string StageLabel(StrategySnapshot s)
        {return s.Kind==StrategyKind.Nuclear?(s.NuclearPhase==NuclearPhase.AwaitingPickup?"Fで拾得 / HP "+s.Health.ToString("0"):s.NuclearPhase==NuclearPhase.Carried?"運搬中 / 機体 "+(s.CarrierSlot%50+1):s.Detonated?"爆発":s.SecondsUntilActive.ToString("0.0")+"秒後に爆発"):s.Armed?s.RemainingSeconds.ToString("0")+"秒":s.SecondsUntilActive.ToString("0.0")+"秒後";}
        public static string Title(StrategyKind kind)=>kind==StrategyKind.Nuclear?"戦略核":kind==StrategyKind.Jammer?"マップ妨害":"広域レーダー";
        private Vector2 Position(float x,float z)=>MapTerrainBackground.Position(field,map,x,z);
        private static RectTransform Rect(Transform parent,string name,Vector2 pos,Vector2 size)
        {var rt=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rt.SetParent(parent,false);rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(.5f,.5f);rt.anchoredPosition=pos;rt.sizeDelta=size;return rt;}
        private static UnityEngine.UI.Image Image(RectTransform rect,Color color,bool clickable=false)
        {var image=rect.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=color;image.raycastTarget=clickable;return image;}
        private TMP_Text Text(Transform parent,string name,string value,Vector2 pos,Vector2 size,int fontSize)
        {var t=Rect(parent,name,pos,size).gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=value;t.fontSize=fontSize;t.color=Color.white;t.raycastTarget=false;t.textWrappingMode=TextWrappingModes.Normal;t.overflowMode=TextOverflowModes.Truncate;t.alignment=TextAlignmentOptions.Center;return t;}
        private UnityEngine.UI.Button Button(Transform parent,string name,string title,Vector2 pos,Vector2 size,Action action)
        {var rt=Rect(parent,name,pos,size);var image=Image(rt,new Color(.11f,.25f,.30f),true);var button=rt.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=image;button.onClick.AddListener(()=>action());Text(rt,"Caption",title,Vector2.zero,size-Vector2.one*12,23);return button;}
        public void Dispose(){Root.SetActive(false);Root.transform.SetParent(null,false);UnityEngine.Object.Destroy(Root);}
    }
}
