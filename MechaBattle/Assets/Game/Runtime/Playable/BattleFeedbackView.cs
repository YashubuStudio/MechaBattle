using MechaBattle.Simulation;
using TMPro;
using UnityEngine;
namespace MechaBattle.Playable
{
    // Cached HUD objects; simulation events confirm hits, not reticle overlap.
    internal sealed class BattleFeedbackView
    {
        private readonly TMP_Text marker,hitText,killText,supplyText;
        private readonly RectTransform[] bars=new RectTransform[100];
        private readonly UnityEngine.UI.Image[] fills=new UnityEngine.UI.Image[100];
        private readonly UnityEngine.CanvasGroup[] barGroups=new UnityEngine.CanvasGroup[100];
        private readonly TMP_Text[] hpLabels=new TMP_Text[100];
        private readonly Transform parent;private readonly TMP_FontAsset font;
        private readonly TMP_Text[] numbers=new TMP_Text[24];private readonly Vector3[] numberPositions=new Vector3[24];private readonly float[] numberUntil=new float[24];
        private int nextNumber,ownId,kills;private long sequence;
        private float hitUntil,killUntil,supplyUntil,windowUntil,hpTotal,shieldTotal,previousHp,previousShield;private int previousAmmo;
        public bool HitSound {get;private set;}public bool KillSound {get;private set;}public bool HealSound {get;private set;}
        public BattleFeedbackView(Transform parent,TMP_FontAsset font)
        {
            this.parent=parent;this.font=font;
            marker=Text("ConfirmedHitMarker",56,new Vector2(0,0),new Vector2(80,80));
            hitText=Text("ConfirmedHitAmount",23,new Vector2(0,-112),new Vector2(520,42));
            killText=Text("ConfirmedKillNotice",34,new Vector2(0,158),new Vector2(650,58));
            supplyText=Text("RecoveryNotice",22,new Vector2(-640,-220),new Vector2(550,50));Reset();
        }
        public void Reset()
        {
            sequence=0;ownId=0;kills=0;hitUntil=killUntil=supplyUntil=windowUntil=0;hpTotal=shieldTotal=0;
            marker.text=hitText.text=killText.text=supplyText.text="";
            for(int i=0;i<bars.Length;i++)if(bars[i]!=null)bars[i].gameObject.SetActive(false);
            for(int i=0;i<numbers.Length;i++){numberUntil[i]=0;if(numbers[i]!=null)numbers[i].gameObject.SetActive(false);}
        }
        public void Update(BattleSimulation world,int human,Camera camera,int lockedTarget=-1)
        {
            HitSound=KillSound=HealSound=false;float now=Time.unscaledTime;var self=world.GetUnit(human);
            if(ownId!=self.UnitId){ownId=self.UnitId;previousHp=self.Health;previousShield=self.Shield;previousAmmo=TotalAmmo(world,human);hitUntil=supplyUntil=windowUntil=0;hpTotal=shieldTotal=0;}
            long first=System.Math.Max(sequence+1,world.CombatEventSequence-world.CombatEventCapacity+1);
            for(long n=first;n<=world.CombatEventSequence;n++)
            {
                if(!world.TryGetCombatEvent(n,out var e))continue;
                bool own=e.SourceSlot==human && e.SourceUnitId==self.UnitId;
                if(own && (e.Kind==CombatEventKind.Hit || e.Kind==CombatEventKind.CoreHit || e.Kind==CombatEventKind.ObjectHit))
                {
                    if(now>=windowUntil){hpTotal=shieldTotal=0;windowUntil=now+0.15f;}
                    hpTotal+=e.HealthAmount;shieldTotal+=e.ShieldAmount;HitSound=true;hitUntil=now+0.28f;
                    marker.text="×";marker.color=e.HealthAmount>0?Color.white:new Color(0.3f,0.8f,1);
                    hitText.color=marker.color;hitText.text=e.Kind==CombatEventKind.ObjectHit?"設置物命中  -"+hpTotal.ToString("0"):e.Kind==CombatEventKind.CoreHit?"本拠点命中  "+hpTotal.ToString("0"):hpTotal>0?"命中  HP -"+hpTotal.ToString("0")+(shieldTotal>0?" / 盾 -"+shieldTotal.ToString("0"):""):"シールド命中  -"+shieldTotal.ToString("0");
                    FloatNumber(e,new Color(1,0.92f,0.4f),"-"+(e.HealthAmount+e.ShieldAmount).ToString("0"));
                }
                if(own && e.Kind==CombatEventKind.Destroyed){kills++;KillSound=true;killUntil=now+2.2f;killText.text="敵機撃破！  /  "+kills;killText.color=new Color(1,0.78f,0.2f);}
                if(own && e.Kind==CombatEventKind.ObjectDestroyed){KillSound=true;killUntil=now+2.2f;killText.text="敵設置物を破壊";killText.color=new Color(1,0.78f,0.2f);}
                if(own && e.Kind==CombatEventKind.Healed){HealSound=true;hitUntil=now+0.28f;marker.text="＋";marker.color=new Color(0.3f,1,0.65f);hitText.color=marker.color;hitText.text="味方HP回復 +"+e.HealthAmount.ToString("0");FloatNumber(e,marker.color,"+"+e.HealthAmount.ToString("0"));}
            }
            sequence=world.CombatEventSequence;
            int ammo=TotalAmmo(world,human);float hp=self.Health-previousHp,shield=self.Shield-previousShield;
            if(self.IsAlive && (hp>0.01f || shield>0.01f || ammo>previousAmmo))
            {supplyUntil=now+0.8f;supplyText.text="回復・補給中"+(hp>0.01f?"  HP ↑":"")+(shield>0.01f?"  盾 ↑":"")+(ammo>previousAmmo?"  弾薬 ↑":"");supplyText.color=new Color(0.35f,1,0.75f);}
            previousHp=self.Health;previousShield=self.Shield;previousAmmo=ammo;
            marker.gameObject.SetActive(now<hitUntil);hitText.gameObject.SetActive(now<hitUntil);killText.gameObject.SetActive(now<killUntil);supplyText.gameObject.SetActive(now<supplyUntil);
            for(int i=0;i<bars.Length;i++)
            {
                var c=world.GetUnitHealthBar(human,i);Vector3 v=camera.WorldToViewportPoint(new Vector3(c.X,c.Y+3,c.Z));
                bool visible=c.Visible && OnScreen(v);
                if(!visible){if(bars[i]!=null)bars[i].gameObject.SetActive(false);continue;}
                if(bars[i]==null)MakeBar(i);
                bars[i].gameObject.SetActive(true);bars[i].anchorMin=bars[i].anchorMax=new Vector2(v.x,v.y);bars[i].anchoredPosition=Vector2.zero;
                barGroups[i].alpha=c.Friendly?0.5f:1;
                PlayableUI.Fill(fills[i],c.MaxHealth>0?c.Health/c.MaxHealth:0);fills[i].color=c.Friendly?new Color(0.3f,1,0.7f):c.HealthAgeSeconds>0.05f?new Color(1,0.63f,0.3f):new Color(1,0.26f,0.16f);
                hpLabels[i].color=c.Friendly?new Color(0.65f,1,0.85f):Color.white;
                hpLabels[i].text=(c.Friendly?(i==lockedTarget?"[味方ロック] ":"味方 "):"")+c.Health.ToString("0")+" / "+c.MaxHealth.ToString("0");
            }
            for(int i=0;i<numbers.Length;i++)
            {
                if(numbers[i]==null)continue;var v=camera.WorldToViewportPoint(numberPositions[i]+Vector3.up*(0.7f-(numberUntil[i]-now)));
                bool visible=now<numberUntil[i] && OnScreen(v);numbers[i].gameObject.SetActive(visible);if(!visible)continue;
                numbers[i].rectTransform.anchorMin=numbers[i].rectTransform.anchorMax=new Vector2(v.x,v.y);numbers[i].rectTransform.anchoredPosition=Vector2.zero;
            }
        }
        private static bool OnScreen(Vector3 v)=>v.z>0 && v.x>0.02f && v.x<0.98f && v.y>0.02f && v.y<0.98f;
        private static int TotalAmmo(BattleSimulation w,int slot){int total=0;var u=w.GetUnit(slot);for(int i=0;i<u.WeaponCount;i++)total+=w.GetWeaponAmmo(slot,i);return total;}
        private void FloatNumber(CombatEventSnapshot e,Color color,string value)
        {
            int i=nextNumber++%numbers.Length;if(numbers[i]==null)numbers[i]=Text("HitNumber"+i,23,Vector2.zero,new Vector2(140,36));
            numbers[i].text=value;numbers[i].color=color;numberUntil[i]=Time.unscaledTime+0.7f;numberPositions[i]=new Vector3(e.X,e.Y+0.5f,e.Z);
        }
        private void MakeBar(int i)
        {
            var go=new GameObject("SharedTargetHP"+i,typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.CanvasGroup));go.transform.SetParent(parent,false);
            barGroups[i]=go.GetComponent<UnityEngine.CanvasGroup>();barGroups[i].interactable=false;barGroups[i].blocksRaycasts=false;
            bars[i]=go.GetComponent<RectTransform>();bars[i].sizeDelta=new Vector2(126,10);go.GetComponent<UnityEngine.UI.Image>().color=new Color(0.02f,0.03f,0.04f,0.9f);go.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
            var f=new GameObject("HPFill",typeof(RectTransform),typeof(UnityEngine.UI.Image));f.transform.SetParent(go.transform,false);fills[i]=f.GetComponent<UnityEngine.UI.Image>();fills[i].raycastTarget=false;
            var rt=f.GetComponent<RectTransform>();rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;
            hpLabels[i]=Text("SharedTargetHPLabel"+i,16,Vector2.zero,new Vector2(180,25));hpLabels[i].transform.SetParent(go.transform,false);hpLabels[i].rectTransform.anchorMin=hpLabels[i].rectTransform.anchorMax=new Vector2(0.5f,1);hpLabels[i].rectTransform.anchoredPosition=new Vector2(0,18);
        }
        private TMP_Text Text(string name,int size,Vector2 offset,Vector2 dimensions)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);var rt=go.GetComponent<RectTransform>();rt.anchorMin=rt.anchorMax=new Vector2(0.5f,0.5f);rt.sizeDelta=dimensions;rt.anchoredPosition=offset;
            var text=go.GetComponent<TextMeshProUGUI>();text.font=font;text.fontSize=size;text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;text.textWrappingMode=TextWrappingModes.NoWrap;return text;
        }
    }
}
