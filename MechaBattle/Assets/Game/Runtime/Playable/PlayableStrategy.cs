using MechaBattle.Contracts;
using MechaBattle.Simulation;
using TMPro;
using UnityEngine;
namespace MechaBattle.Playable
{
    public sealed partial class PlayableEntry
    {
        private TacticalMapView tacticalMap;private StrategyWorldView strategyWorld;
        private StrategyKind selectedStrategy=StrategyKind.WideRadar;
        private bool strategyPointSelected;private float strategyX,strategyZ;
        private string strategyFeedback="";
        private TMP_Text strategyHud,nuclearWarning,returnHud;
        private bool nuclearInteractQueued,emergencyReturnQueued;
        public bool IsTacticalMapOpen=>tacticalMap!=null&&tacticalMap.Root.activeSelf;
        private void InitializeStrategyViews()
        {
            DisposeStrategyViews();strategyPointSelected=false;strategyFeedback="";
            tacticalMap=new TacticalMapView(ui.Hud.transform.parent,font,map,CloseTacticalMap,SelectStrategy,SelectStrategyPoint,ConfirmStrategy);
            strategyWorld=new StrategyWorldView(transform,followCamera,font,unlitShader);
            if(strategyHud==null)
            {
                strategyHud=StrategyHudText("StrategyStatus",new Vector2(40,-390),new Vector2(500,80),23);
                returnHud=StrategyHudText("ReturnAndCarryStatus",new Vector2(40,-480),new Vector2(550,170),23);
                nuclearWarning=StrategyHudText("NuclearWarning",new Vector2(630,-390),new Vector2(660,110),25);nuclearWarning.alignment=TextAlignmentOptions.Center;nuclearWarning.color=new Color(1,.45f,.18f);
            }
            RefreshStrategy();
        }
        private TMP_Text StrategyHudText(string name,Vector2 position,Vector2 size,int fontSize)
        {
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(ui.Hud.transform,false);
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=position;rect.sizeDelta=size;
            var text=rect.gameObject.AddComponent<TextMeshProUGUI>();text.font=font;text.fontSize=fontSize;text.color=Color.white;text.raycastTarget=false;return text;
        }
        public void OpenTacticalMap()
        {
            if(Simulation==null||Simulation.Phase!=MatchPhase.Running||IsPaused||IsSettingsOpen||tacticalMap==null)return;
            Simulation.CancelHeldActions(HumanSlotId);ResetInput();inputArmed=false;
            tacticalMap.Root.SetActive(true);tacticalMap.Root.transform.SetAsLastSibling();SetCursor(false);RefreshStrategy();
        }
        public void CloseTacticalMap()
        {
            if(tacticalMap==null)return;tacticalMap.Root.SetActive(false);ResetInput();inputArmed=false;
            if(Simulation!=null&&!IsPaused&&Simulation.Phase==MatchPhase.Running)SetCursor(Simulation.GetUnit(HumanSlotId).IsAlive);
        }
        public void SelectStrategy(StrategyKind kind)
        {if(kind<StrategyKind.Nuclear||kind>StrategyKind.WideRadar)return;selectedStrategy=kind;strategyFeedback="";RefreshStrategy();}
        public void SelectStrategyPoint(float x,float z)
        {if(!IsTacticalMapOpen||float.IsNaN(x)||float.IsInfinity(x)||float.IsNaN(z)||float.IsInfinity(z))return;strategyX=x;strategyZ=z;strategyPointSelected=true;strategyFeedback="";RefreshStrategy();}
        public void ConfirmStrategy()
        {
            if(!IsTacticalMapOpen||!strategyPointSelected||Simulation==null)return;
            bool accepted=Simulation.RequestStrategy(HumanSlotId,selectedStrategy,strategyX,strategyZ,out var reason);
            strategyFeedback=accepted?(selectedStrategy==StrategyKind.Nuclear?"核を要請しました / 受取地点へ移動してFで拾得":TacticalMapView.Title(selectedStrategy)+"を要請しました"):reason;
            if(accepted)strategyPointSelected=false;RefreshStrategy();
        }
        private void RefreshStrategy()
        {
            if(Simulation==null)return;var self=Simulation.GetUnit(HumanSlotId);var team=Simulation.GetStrategyTeam(self.Team);
            if(strategyHud!=null)strategyHud.text="[M] 戦術マップ・戦略支援\n戦略ポイント "+team.Points+" / "+team.MaximumPoints+(Simulation.IsMapJammed(self.Team,self.X,self.Z)?" / 妨害域":"");
            string warning="";
            for(int i=0;i<Simulation.StrategyCapacity;i++)
            {var s=Simulation.GetStrategy(i,self.Team);if(!s.Active||s.Kind!=StrategyKind.Nuclear||s.NuclearPhase!=NuclearPhase.Arming)continue;
             float dx=self.X-s.X,dz=self.Z-s.Z;bool danger=self.IsAlive&&dx*dx+dz*dz<=s.Radius*s.Radius;
             warning+=(s.Team==self.Team?"味方核":"敵核")+" / "+s.SecondsUntilActive.ToString("0.0")+"秒後"+(danger?"  危険！範囲外へ退避":"  地図で範囲を確認")+"\n";}
            if(nuclearWarning!=null)nuclearWarning.text=warning;
            RefreshReturnUI();
            if(IsTacticalMapOpen)tacticalMap.Refresh(Simulation,HumanSlotId,selectedStrategy,strategyPointSelected,strategyX,strategyZ,strategyFeedback);
            if(strategyWorld!=null)strategyWorld.Sync(Simulation,HumanSlotId);
        }
        public void RequestEmergencyReturn(){if(Simulation!=null&&!IsPaused)emergencyReturnQueued=true;}
        public void InteractNuclear(){if(Simulation!=null&&!IsPaused)nuclearInteractQueued=true;}
        private void HandleReturnTransition()
        {
            if(!Simulation.GetEmergencyReturn(HumanSlotId).InReserve||!lastAlive)return;
            lastAlive=false;ClearTargetLock();ResetInput();inputArmed=false;respawnDeck=Simulation.GetUnit(HumanSlotId).DeckIndex;
            spawnPoint=-1;selectedMainBase=EnsureInitialMainBase();SetCursor(false);ui.ShowBattle(true,false);RefreshBattle();
        }
        private void RefreshReturnUI()
        {
            if(Simulation==null)return;var self=Simulation.GetUnit(HumanSlotId);var state=Simulation.GetEmergencyReturn(HumanSlotId);
            bool carrying=Simulation.IsCarryingNuclear(HumanSlotId);string text=state.Channeling?"緊急帰投 "+state.SecondsRemaining.ToString("0.0")+"秒\n動かず待機 / 被弾・移動・攻撃・Bで中断":carrying?"核運搬中 / 着地して左クリックで起動\nF: 手放す / 通常武器・盾・帰投は使用不可":"[B] 緊急帰投 / [F] 核の拾得・手放し";
            string feedback=Simulation.GetActionFeedback(HumanSlotId);if(!string.IsNullOrEmpty(feedback))text+="\n"+feedback;
            if(returnHud!=null)returnHud.text=text;
            if(carrying){ui.CombatStatus.text="戦略核を運搬中\n着地して左クリック：この場所で起動\nF：手放す / 敵の射撃で破壊されます";ui.WeaponInfo.text="運搬中は通常武器・盾を使用できません";}
            if(state.InReserve)
            {
                respawnDeck=self.DeckIndex;var frame=Simulation.GetLoadout(HumanSlotId,self.DeckIndex);
                ui.Waiting.text="帰投補給中 / HP "+self.Health.ToString("0")+"/"+frame.MaxHealth.ToString("0")+"  盾 "+self.Shield.ToString("0")+"\n同じ機体で出撃先を選択。補給禁止の弾数は回復しません。";
                for(int i=0;i<ui.RespawnChoices.Length;i++)ui.RespawnChoices[i].interactable=i==self.DeckIndex;
            }
        }
        private void DisposeStrategyViews()
        {if(tacticalMap!=null){tacticalMap.Dispose();tacticalMap=null;}if(strategyWorld!=null){strategyWorld.Dispose();strategyWorld=null;}}
    }
}
