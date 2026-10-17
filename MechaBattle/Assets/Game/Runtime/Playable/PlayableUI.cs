using MechaBattle.Contracts.Settings;
using MechaBattle.Settings;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace MechaBattle.Playable
{
    // Built once; screen transitions only toggle panels. Labels/counters are cached.
    internal sealed class PlayableUI
    {
        public readonly GameObject Menu, Hud, Respawn, Pause, Results, MainMenu, BattlePrep;
        public readonly GameObject PartPicker;
        public readonly PlayableSettingsView Settings;
        private readonly UnityEngine.UI.RawImage garagePreview, battlePreview;
        public readonly TMP_Text BattlePrepStatus;
        public readonly TMP_Text[] WeaponSlotLabels = new TMP_Text[3];
        public readonly TMP_Text[] PersonalResultValues = new TMP_Text[5];
        public readonly TMP_Text PersonalResultCombat;
        private readonly UnityEngine.UI.Image[] weaponCards = new UnityEngine.UI.Image[3];
        private readonly UnityEngine.UI.Button[] teamChoices = new UnityEngine.UI.Button[2], deckChoices = new UnityEngine.UI.Button[4], partOptions = new UnityEngine.UI.Button[14];
        private readonly UnityEngine.UI.Button[] battleTeamChoices = new UnityEngine.UI.Button[2], battleDeckChoices = new UnityEngine.UI.Button[4];
        public readonly TMP_Text[] BattleDeckLabels = new TMP_Text[4];
        private readonly TMP_Text pickerTitle, pickerHelp, danger;
        public readonly TMP_Text FrameLabel, PaintLabel, MapLabel;
        private readonly TMP_Text[] lockModeLabels=new TMP_Text[3];
        private readonly UnityEngine.UI.Image damageFlash, incomingLockPanel;
        public readonly TMP_Text IncomingLockWarning;
        public readonly TMP_Text EventFeed;
        public readonly TMP_Text Scoreboard;
        private Action<int> pickPart; private float damageTime;
        public readonly TMP_Text Status, Stats, PreviewTitle, Gauges, OrangeText, Timer, Vitals, CombatStatus, LoadoutStatus, LockStatus, Objective, DeckStatus, Waiting, ResultTitle, ResultStats;
        public readonly RectTransform MinimapPanel;
        public readonly TMP_Text[] DeckLabels = new TMP_Text[4], PartLabels = new TMP_Text[5], RespawnLabels = new TMP_Text[4];
        public readonly UnityEngine.UI.Button[] RespawnChoices = new UnityEngine.UI.Button[4];
        public readonly UnityEngine.UI.Button Deploy;
        public readonly TMP_Text SpawnLabel, PrepSpawnLabel;
        public readonly RectTransform PrepDeploymentPanel, RespawnDeploymentPanel;
        public readonly UnityEngine.UI.Image HealthFill, ShieldFill, BoostFill, DownFill, BlueGauge, OrangeGauge;
        public readonly TMP_Text Crosshair, WeaponInfo;
        public readonly ReticleRingGraphic ReticleRing;
        public readonly TMP_Text DamageDirection;
        private readonly RectTransform lockMarker; private readonly TMP_Text markerDistance, markerDiamond;
        private readonly TMP_FontAsset font;
        private static readonly Color Ink = new Color(0.035f, 0.06f, 0.09f, 0.97f);
        private static readonly Color Panel = new Color(0.075f, 0.11f, 0.16f, 0.97f);
        private static readonly Color Accent = new Color(0.23f, 0.79f, 0.74f);

        public PlayableUI(Transform parent, TMP_FontAsset font, Action<int> team, Action<int> deck, Action<int> preset,
            Action<int> part, Action frame, Action paint, Action map, Action save, Action start, Action resume, Action back, Action<int> respawnDeck, Action deploy,
            Action toGarage, Action toMainMenu,Action toggleLockMode,Action openSettings,Action<PlayerPreferences> applySettings,Action cancelSettings)
        {
            this.font = font;
            var canvas = new GameObject("PlayableCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvas.transform.SetParent(parent, false); canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)); events.transform.SetParent(parent, false);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            var root = canvas.transform;
            MainMenu = Screen(root, "MainMenu", new Color(0.02f, 0.035f, 0.06f, 1));
            Label(MainMenu.transform, "MECHABATTLE", 660, 300, 600, 90, 52, Color.white).alignment = TextAlignmentOptions.Center;
            Label(MainMenu.transform, "50 vs 50  /  OFFLINE", 660, 380, 600, 40, 22, Accent).alignment = TextAlignmentOptions.Center;
            ButtonAt(MainMenu.transform, "参戦", 660, 480, 600, 90, back, true).name = "OpenBattlePrep";
            ButtonAt(MainMenu.transform, "機体制作", 660, 590, 600, 90, toGarage).name = "OpenGarage";

            var mainLockMode=ButtonAt(MainMenu.transform,"",660,710,600,60,toggleLockMode);mainLockMode.name="LockModeSetting";lockModeLabels[0]=mainLockMode.GetComponentInChildren<TMP_Text>();
            Label(MainMenu.transform,"右クリック: 取得/解除　Tab: 対象切替",600,790,720,40,21,Color.white).alignment=TextAlignmentOptions.Center;
            ButtonAt(MainMenu.transform,"設定",660,858,600,64,openSettings).name="OpenSettings";
            Menu = Screen(root, "Preparation", new Color(0.025f, 0.045f, 0.07f, 1));
            Label(Menu.transform, "MECHABATTLE", 50, 35, 900, 70, 46, Color.white);
            ButtonAt(Menu.transform, "設定", 1620, 40, 250, 62, openSettings).name="OpenSettings";
            var left = Box(Menu.transform, "Hangar", 48, 142, 886, 850, Panel);
            var column = left.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>(); column.padding = new RectOffset(24, 24, 18, 18);
            column.spacing = 6; column.childControlWidth = true; column.childControlHeight = true; column.childForceExpandHeight = false; column.childForceExpandWidth = true;
            FlowLabel(left, "出撃準備・機体構築", 38, 34);
            var teams = Row(left, 38); teamChoices[0] = Button(teams, "青軍で参戦", () => team(0)); teamChoices[1] = Button(teams, "橙軍で参戦", () => team(1));
            var decks = Row(left, 65);
            for (int i = 0; i < 4; i++) { int index = i; deckChoices[i] = Button(decks, "機体 " + (i + 1), () => deck(index)); DeckLabels[i] = deckChoices[i].GetComponentInChildren<TMP_Text>(); }
            string[] titles = { "FA汎用", "SA補強", "単発", "チャージ", "継続ビーム", "バズーカ", "重量機", "FA精密", "軽量機", "支援機" };
            RectTransform presets=null;
            for (int i = 0; i < titles.Length; i++) { if(i%5==0)presets=Row(left,38);int index = i; Button(presets, titles[i], () => preset(index)).GetComponentInChildren<TMP_Text>().fontSize=20; }
            var frameRow = Row(left, 40);
            var frameButton = Button(frameRow, "フレームを選択", frame); frameButton.name = "SelectFrame";
            FrameLabel = frameButton.GetComponentInChildren<TMP_Text>();
            var paintRow=Row(left,40);var paintButton=Button(paintRow,"塗装",paint);paintButton.name="SelectPaint";PaintLabel=paintButton.GetComponentInChildren<TMP_Text>();
            // User request 2026-10-16: left/right arms must match, so a single unified button
            // (bound to LeftArm; PlayableEntry.SyncMatchingRightArm keeps RightArm in step)
            // replaces the old two independent left/right buttons.
            var arms = Row(left, 40);
            var armButton = Button(arms, "腕部品", () => part(0));
            PartLabels[0] = PartLabels[1] = armButton.GetComponentInChildren<TMP_Text>();
            for (int i=2;i<5;i++) { int index=i; var row=Row(left,40); PartLabels[i]=Button(row,"装備を選択",()=>part(index)).GetComponentInChildren<TMP_Text>(); }
            Stats = FlowLabel(left, "", 156, 18);
            FlowLabel(left, "敵本拠点を攻撃し、敵の戦力ゲージを0にすると勝利。\n中継を占領すると前線から出撃できます。\n味方本拠点の近くでHP・シールド・弾薬を補給。", 60, 18);
            var actions = Row(left, 46); Button(actions, "構成を保存", save); Button(actions, "メインメニューへ戻る", toMainMenu, true).name = "ReturnToMainMenu";
            PreviewTitle = Label(Menu.transform, "標準フレーム", 1010, 145, 860, 50, 28, Accent);
            garagePreview = PreviewPanel(Menu.transform, 1000, 212, 870, 610);
            var controls = Box(Menu.transform, "Controls", 1000, 844, 870, 158, Panel);
            Label(controls, "WASD 移動 / マウス 照準 / 左クリック 使用\nSpace 短押しジャンプ・保持で上昇\nShift ダッシュ・滑空 / Ctrl しゃがみ / ホイール・1〜3 装備\n右クリック ロック / Tab 切替 / Q 格闘 / R 装填 / Esc 休止", 24, 18, 820, 130, 23, Color.white);
            Status = Label(Menu.transform, "", 50, 1010, 1820, 55, 21, Accent);

            BattlePrep = Screen(root, "BattlePrep", new Color(0.025f, 0.045f, 0.07f, 1));
            Label(BattlePrep.transform, "参戦準備", 50, 35, 900, 70, 46, Color.white);
            ButtonAt(BattlePrep.transform, "設定", 1620, 40, 250, 62, openSettings).name="OpenSettings";
            var prepPanel = Box(BattlePrep.transform, "PrepPanel", 48, 142, 700, 760, Panel);
            var prepColumn = prepPanel.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>(); prepColumn.padding = new RectOffset(24, 24, 18, 18);
            prepColumn.spacing = 10; prepColumn.childControlWidth = true; prepColumn.childControlHeight = true; prepColumn.childForceExpandHeight = false; prepColumn.childForceExpandWidth = true;
            FlowLabel(prepPanel, "機体とマップ上の出撃先を選択", 34, 24);
            var prepTeams = Row(prepPanel, 50); battleTeamChoices[0] = Button(prepTeams, "青軍で参戦", () => team(0)); battleTeamChoices[1] = Button(prepTeams, "橙軍で参戦", () => team(1));
            for (int i = 0; i < 4; i++)
            { int index = i; var row = Row(prepPanel, 80); battleDeckChoices[i] = Button(row, "機体 " + (i + 1), () => deck(index)); BattleDeckLabels[i] = battleDeckChoices[i].GetComponentInChildren<TMP_Text>(); }
            var prepLockRow=Row(prepPanel,50);var prepLockMode=Button(prepLockRow,"",toggleLockMode);prepLockMode.name="LockModeSetting";lockModeLabels[1]=prepLockMode.GetComponentInChildren<TMP_Text>();
            var prepActions = Row(prepPanel, 60); Button(prepActions, "機体制作へ戻る", toGarage).name = "OpenGarage"; Button(prepActions, "出撃する", start, true).name = "BeginMatch";
            var mapButton=ButtonAt(BattlePrep.transform,"マップを選択",790,145,1080,50,map);mapButton.name="SelectMap";MapLabel=mapButton.GetComponentInChildren<TMP_Text>();
            PrepDeploymentPanel = Box(BattlePrep.transform, "DeploymentMap", 790, 212, 1080, 610, Ink);
            battlePreview = PreviewPanel(BattlePrep.transform, 790, 844, 240, 160);
            PrepSpawnLabel = Label(BattlePrep.transform, "", 1060, 844, 810, 54, 26, Accent); PrepSpawnLabel.name = "SelectedSpawn";
            Label(BattlePrep.transform, "■ 本拠点 / ◆ 中継 / 黄枠: 選択中\n初回は自軍本拠点から出撃。中継は占領後に利用可能。", 1060, 906, 810, 80, 21, Color.white);
            BattlePrepStatus = Label(BattlePrep.transform, "", 50, 1012, 1820, 56, 21, Accent);

            Hud = Screen(root, "BattleHUD", Color.clear);
            BlueGauge = Bar(Hud.transform, 40, 73, 690, 14, new Color(0.2f, 0.48f, 1));
            OrangeGauge = Bar(Hud.transform, 1190, 73, 690, 14, new Color(1, 0.45f, 0.12f));
            Gauges = Label(Hud.transform, "", 40, 26, 690, 40, 26, Color.white);
            OrangeText = Label(Hud.transform, "", 1190, 26, 690, 40, 26, Color.white); OrangeText.alignment = TextAlignmentOptions.Right;
            MinimapPanel = Box(Hud.transform, "Minimap", 40, 125, 310, 190, Ink);
            Label(Hud.transform, "↑ 北　■ 本拠点　◆ 中継　白:自機\n赤:共有ロック　黄:レーダー", 40, 325, 560, 60, 19, Color.white);
            WeaponInfo=Label(Hud.transform,"",1390,510,490,70,19,Color.white); WeaponInfo.name="WeaponInfo";
            LoadoutStatus = Label(Hud.transform, "装備 / ホイール・1〜3", 1390, 590, 490, 40, 22, Color.white); LoadoutStatus.name="LoadoutStatus";
            for(int i=0;i<3;i++)
            { var card=Box(Hud.transform,"WeaponSlot"+(i+1),1390,635+i*56,490,50,Ink);weaponCards[i]=card.GetComponent<UnityEngine.UI.Image>();WeaponSlotLabels[i]=Label(card,"",14,7,462,38,22,Color.white); }
            LockStatus = Label(Hud.transform,"",670,585,580,70,20,Color.white); LockStatus.alignment=TextAlignmentOptions.Center; LockStatus.name="LockStatus";
            DeckStatus = Label(Hud.transform, "", 1390, 820, 490, 175, 21, Color.white);
            Timer = Label(Hud.transform, "", 770, 32, 380, 50, 28, Accent); Timer.alignment = TextAlignmentOptions.Center;
            Timer.rectTransform.anchorMin = Timer.rectTransform.anchorMax = new Vector2(0.5f, 1); Timer.rectTransform.pivot = new Vector2(0.5f, 1); Timer.rectTransform.anchoredPosition = new Vector2(0, -32);
            Objective = Label(Hud.transform, "", 1450, 110, 430, 260, 22, Color.white);
            var vitalPanel = Box(Hud.transform, "Vitals", 40, 793, 510, 245, Ink);
            Vitals = Label(vitalPanel, "", 18, 10, 480, 132, 25, Color.white);
            HealthFill = Bar(vitalPanel, 86, 153, 402, 12, new Color(0.3f, 0.87f, 0.64f));
            ShieldFill = Bar(vitalPanel, 86, 174, 402, 12, new Color(0.3f, 0.65f, 1));
            BoostFill = Bar(vitalPanel, 86, 195, 402, 10, Accent);
            DownFill = Bar(vitalPanel, 86, 216, 402, 10, new Color(1, 0.65f, 0.15f));
            string[] barNames={"HP","盾","BOOST","DP"};for(int i=0;i<4;i++)Label(vitalPanel,barNames[i],18,149+i*21,64,20,14,Color.white);
            var combat = Box(Hud.transform, "CombatStatus", 620, 842, 735, 150, Ink);
            CombatStatus = Label(combat, "", 18, 12, 700, 132, 23, Color.white);
            var ring=Rect(Hud.transform,"ReticleRing",0,0,160,160);ring.anchorMin=ring.anchorMax=new Vector2(0.5f,0.5f);ring.pivot=new Vector2(0.5f,0.5f);ring.anchoredPosition=Vector2.zero;ReticleRing=ring.gameObject.AddComponent<ReticleRingGraphic>();ReticleRing.raycastTarget=false;ReticleRing.color=new Color(1,1,1,0.75f);
            lockMarker=Rect(Hud.transform,"LockTargetMarker",0,0,94,110);lockMarker.pivot=new Vector2(0.5f,0.5f);
            markerDiamond=Label(lockMarker,"◇",0,17,94,76,62,Accent);markerDiamond.alignment=TextAlignmentOptions.Center;markerDiamond.fontStyle=FontStyles.Bold;
            markerDistance=Label(lockMarker,"",-95,78,284,64,19,Color.white);markerDistance.alignment=TextAlignmentOptions.Center;lockMarker.gameObject.SetActive(false);
            Crosshair = Label(Hud.transform, "+", 930, 510, 60, 60, 30, Color.white); Crosshair.alignment = TextAlignmentOptions.Center;
            Crosshair.rectTransform.anchorMin = Crosshair.rectTransform.anchorMax = new Vector2(0.5f, 0.5f); Crosshair.rectTransform.pivot = new Vector2(0.5f, 0.5f); Crosshair.rectTransform.anchoredPosition = Vector2.zero;
            DamageDirection=Label(Hud.transform,"▲",0,0,70,70,46,new Color(1,0.25f,0.2f,0));DamageDirection.alignment=TextAlignmentOptions.Center;DamageDirection.fontStyle=FontStyles.Bold;
            DamageDirection.rectTransform.anchorMin=DamageDirection.rectTransform.anchorMax=new Vector2(0.5f,0.5f);DamageDirection.rectTransform.pivot=new Vector2(0.5f,0.5f);DamageDirection.gameObject.SetActive(false);
            Label(Hud.transform, "左クリック 使用 / ホイール・1〜3 切替 / Space 保持で上昇 / Shift ダッシュ・滑空 / Ctrl しゃがみ / Q 格闘型", 620, 1010, 1250, 50, 19, Color.white);

            danger=Label(Hud.transform,"",650,110,620,45,24,new Color(1,0.6f,0.35f));danger.alignment=TextAlignmentOptions.Center;
            damageFlash=Screen(Hud.transform,"DamageFeedback",Color.clear).GetComponent<UnityEngine.UI.Image>();damageFlash.raycastTarget=false;
            var warning=Box(Hud.transform,"IncomingLockPanel",630,164,660,98,Ink);incomingLockPanel=warning.GetComponent<UnityEngine.UI.Image>();
            IncomingLockWarning=Label(warning,"",14,8,632,82,25,Color.white);IncomingLockWarning.name="IncomingLockWarning";IncomingLockWarning.alignment=TextAlignmentOptions.Center;IncomingLockWarning.fontStyle=FontStyles.Bold;warning.gameObject.SetActive(false);
            EventFeed=Label(Hud.transform,"",630,270,660,110,21,new Color(1,0.85f,0.45f));EventFeed.name="EventFeed";EventFeed.alignment=TextAlignmentOptions.Center;
            Respawn = Screen(root, "Redeploy", new Color(0.02f, 0.035f, 0.06f, 0.8f));
            var rp = Box(Respawn.transform, "RedeployPanel", 60, 80, 1800, 930, Panel);
            Label(rp, "再出撃 / 機体と出撃先を選択", 32, 22, 1736, 60, 36, Color.white);
            Waiting = Label(rp, "", 32, 94, 1736, 65, 23, Accent);
            for (int i = 0; i < 4; i++)
            {
                int index = i; RespawnChoices[i] = ButtonAt(rp, "", 32, 190 + i * 120, 400, 100, () => respawnDeck(index));
                RespawnChoices[i].name = "RespawnDeck" + i; RespawnLabels[i] = RespawnChoices[i].GetComponentInChildren<TMP_Text>();
            }
            RespawnDeploymentPanel = Box(rp, "DeploymentMap", 474, 172, 1286, 650, Ink);
            Label(rp, "拠点をクリックして選択\n■ 本拠点 / ◆ 中継\n黄枠: 選択中 / 点: 味方・共有索敵", 32, 698, 420, 120, 22, Color.white);
            SpawnLabel = Label(rp, "", 474, 832, 830, 70, 24, Accent); SpawnLabel.name = "SelectedSpawn";
            Deploy = ButtonAt(rp, "選択した場所へ出撃", 1330, 832, 430, 70, deploy, true); Deploy.name = "Deploy";

            Pause = Screen(root, "Pause", new Color(0.02f, 0.035f, 0.06f, 0.88f));
            Label(Pause.transform, "一時停止", 660, 315, 600, 90, 48, Color.white).alignment = TextAlignmentOptions.Center;
            Label(Pause.transform, "成績（上位）", 1300, 150, 560, 40, 24, Accent);
            var scoreboardPanel = Box(Pause.transform, "ScoreboardPanel", 1300, 196, 560, 780, Panel);
            Scoreboard = Label(scoreboardPanel, "", 16, 12, 530, 756, 18, Color.white); Scoreboard.name = "Scoreboard";
            ButtonAt(Pause.transform, "戦闘に戻る", 660, 450, 600, 70, resume, true);
            ButtonAt(Pause.transform, "参戦準備へ戻る", 660, 550, 600, 70, back);

            var pauseLockMode=ButtonAt(Pause.transform,"",660,650,600,60,toggleLockMode);pauseLockMode.name="LockModeSetting";lockModeLabels[2]=pauseLockMode.GetComponentInChildren<TMP_Text>();
            ButtonAt(Pause.transform,"設定",660,750,600,64,openSettings).name="OpenSettings";
            Results = Screen(root, "Results", new Color(0.025f, 0.045f, 0.07f, 0.95f));
            ResultTitle = Label(Results.transform, "", 360, 90, 1200, 110, 60, Accent); ResultTitle.alignment = TextAlignmentOptions.Center;
            Label(Results.transform, "試合全体", 70, 240, 560, 55, 32, Accent);
            ResultStats = Label(Results.transform, "", 70, 320, 560, 330, 28, Color.white);
            Label(Results.transform, "与ダメージ・被ダメージ：機体のHP＋盾の実損失\n回復・被回復：実際に増えたHP\n被回復には本拠点での補給も含みます。\n拠点ダメージ：敵本拠点へのダメージ", 70, 686, 560, 150, 20, Color.white);
            var personal = Box(Results.transform, "PersonalStats", 700, 240, 1150, 610, Panel);
            Label(personal, "あなたの試合成績 / 全出撃の合計", 32, 20, 1086, 52, 32, Accent);
            PersonalResultCombat = Label(personal, "", 32, 83, 1086, 42, 25, Color.white); PersonalResultCombat.name = "PersonalCombat";
            string[] resultNames={"与ダメージ","被ダメージ","回復量","被回復量","拠点ダメージ"};
            string[] resultKeys={"DamageDealt","DamageReceived","HealingDealt","HealingReceived","CoreDamageDealt"};
            for(int i=0;i<PersonalResultValues.Length;i++)
            {
                Label(personal,resultNames[i],32,142+i*80,580,62,29,Color.white);
                var value=Label(personal,"0",632,142+i*80,486,62,36,Accent);value.name="Personal"+resultKeys[i];value.alignment=TextAlignmentOptions.Right;
                PersonalResultValues[i]=value;
            }
            Label(personal,"再出撃しても累計を維持します。",32,552,1086,40,21,Color.white);
            ButtonAt(Results.transform, "同じデッキで再戦", 450, 920, 490, 75, start, true);
            ButtonAt(Results.transform, "機体構築へ戻る", 980, 920, 490, 75, toGarage);
            PartPicker=Screen(root,"PartPicker",new Color(0.01f,0.02f,0.04f,0.93f));
            var picker=Box(PartPicker.transform,"SelectionPanel",390,100,1140,870,Panel);
            pickerTitle=Label(picker,"",28,22,1080,55,32,Accent);
            for(int i=0;i<partOptions.Length;i++)
            { int option=i;partOptions[i]=ButtonAt(picker,"",28+(i%2)*548,90+(i/2)*92,532,82,()=>{var choose=pickPart;ClosePartPicker();choose?.Invoke(option);});partOptions[i].name="PartOption"+i;partOptions[i].GetComponentInChildren<TMP_Text>().fontSize=20; }
            pickerHelp=Label(picker,"",28,742,1080,50,18,Color.white);
            ButtonAt(picker,"戻る / Esc",28,798,1080,50,ClosePartPicker);
            Settings = new PlayableSettingsView(root, font, new[]{MainMenu,Menu,BattlePrep,Hud,Respawn,Pause,Results,PartPicker}, applySettings, cancelSettings);
            ShowMainMenu();
        }
        private void HideAllScreens()
        { if (Settings != null) Settings.Close(); if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null); ClosePartPicker(); MainMenu.SetActive(false); Menu.SetActive(false); BattlePrep.SetActive(false); Hud.SetActive(false); Respawn.SetActive(false); Pause.SetActive(false); Results.SetActive(false); }
        public void ShowMainMenu() { HideAllScreens(); MainMenu.SetActive(true); }
        public void ShowMenu() { HideAllScreens(); Menu.SetActive(true); }
        public void ShowBattlePrep() { HideAllScreens(); BattlePrep.SetActive(true); }
        public void ShowBattle(bool dead, bool paused)
        { HideAllScreens(); Hud.SetActive(true); Respawn.SetActive(dead && !paused); Pause.SetActive(paused); }
        public void ShowResults() { HideAllScreens(); Results.SetActive(true); }
        public void OpenPartPicker(string title, string[] options, int selected, Action<int> choose,
            string help = "装備を直接選択できます。装備2・3は「なし」も選択可。", bool[] available = null, bool detailed = false)
        {
            if(options.Length>partOptions.Length)throw new ArgumentException("Too many picker options.");
            if(available!=null && available.Length!=options.Length)throw new ArgumentException("Picker availability length mismatch.");
            pickerTitle.text=title;pickerHelp.text=help;pickPart=choose;
            float height=detailed?Mathf.Min(190,640f/Mathf.Ceil(options.Length/2f)-10):82;
            for(int i=0;i<partOptions.Length;i++)
            {
                var button=partOptions[i];button.gameObject.SetActive(i<options.Length);if(i>=options.Length)continue;
                button.interactable=available==null || available[i];
                var rect=(RectTransform)button.transform;rect.sizeDelta=new Vector2(532,height);rect.anchoredPosition=new Vector2(28+(i%2)*548,-90-(i/2)*(height+10));
                var label=button.GetComponentInChildren<TMP_Text>();label.fontSize=detailed?18:20;
                label.text=(i==selected?"[選択中] ":"")+options[i];
                button.GetComponent<UnityEngine.UI.Image>().color=i==selected?new Color(0.06f,0.43f,0.41f):new Color(0.13f,0.21f,0.29f);
            }
            PartPicker.SetActive(true);
        }
        public void ClosePartPicker(){if(PartPicker!=null)PartPicker.SetActive(false);pickPart=null;}
        public void HighlightPreparation(int team,int deck)
        {
            for(int i=0;i<teamChoices.Length;i++)teamChoices[i].GetComponent<UnityEngine.UI.Image>().color=i==team?new Color(0.06f,0.43f,0.41f):new Color(0.13f,0.21f,0.29f);
            for(int i=0;i<deckChoices.Length;i++)deckChoices[i].GetComponent<UnityEngine.UI.Image>().color=i==deck?new Color(0.06f,0.43f,0.41f):new Color(0.13f,0.21f,0.29f);
        }
        public void HighlightBattlePrep(int team,int deck)
        {
            for(int i=0;i<battleTeamChoices.Length;i++)battleTeamChoices[i].GetComponent<UnityEngine.UI.Image>().color=i==team?new Color(0.06f,0.43f,0.41f):new Color(0.13f,0.21f,0.29f);
            for(int i=0;i<battleDeckChoices.Length;i++)battleDeckChoices[i].GetComponent<UnityEngine.UI.Image>().color=i==deck?new Color(0.06f,0.43f,0.41f):new Color(0.13f,0.21f,0.29f);
        }
        public void SetPreviewTexture(Texture texture) { if (garagePreview != null) garagePreview.texture = texture; if (battlePreview != null) battlePreview.texture = texture; }
        public void SetNotice(string text) { Status.text = BattlePrepStatus.text = text; }
        private UnityEngine.UI.RawImage PreviewPanel(Transform parent, float x, float y, float width, float height)
        {
            var panel = Box(parent, "MechaPreviewPanel", x, y, width, height, new Color(0.045f, 0.075f, 0.105f, 1));
            var content = Rect(panel, "MechaPreviewImage", 0, 0, width, height);
            content.pivot = new Vector2(0.5f, 0.5f);
            var image = content.gameObject.AddComponent<UnityEngine.UI.RawImage>(); image.color = Color.white; image.raycastTarget = false;
            var fit = content.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>(); fit.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio = 4f / 3f;
            return image;
        }
        public void SetLockSelectionMode(string name){foreach(var label in lockModeLabels)label.text="ロック方式："+name+"  ›";}
        public void SetWeaponSlot(int slot,string text,bool equipped)
        {WeaponSlotLabels[slot].text=text;weaponCards[slot].color=equipped?new Color(0.05f,0.36f,0.35f,0.97f):Ink;}
        public void UpdateDamageFeedback(bool hit,float deltaTime)
        {damageTime=hit?0.24f:Mathf.Max(0,damageTime-deltaTime);damageFlash.color=new Color(0.8f,0.08f,0.04f,damageTime/0.24f*0.13f);}
        // relativeAngleDegrees: 0 = attacker directly ahead of the camera, increasing clockwise (matches the
        // game's yaw convention: atan2(dx,dz) with +Z forward). alpha<=0 hides the indicator.
        public void SetDamageDirection(float relativeAngleDegrees,float alpha)
        {
            bool visible=alpha>0.001f;DamageDirection.gameObject.SetActive(visible);if(!visible)return;
            float rad=relativeAngleDegrees*Mathf.Deg2Rad;
            DamageDirection.rectTransform.anchoredPosition=new Vector2(Mathf.Sin(rad),Mathf.Cos(rad))*230f;
            DamageDirection.rectTransform.localEulerAngles=new Vector3(0,0,-relativeAngleDegrees);
            DamageDirection.color=new Color(1,0.25f,0.2f,Mathf.Clamp01(alpha));
        }
        public void SetAimFeedback(bool centered,bool confident,bool marked,Vector3 viewport,float distance,bool red,string targetInfo="",bool friendly=false)
        {
            ReticleRing.color=confident?new Color(0.3f,1,0.55f,0.95f):centered?new Color(1,0.78f,0.25f,0.95f):new Color(1,1,1,0.72f);
            bool visible=marked&&viewport.z>0&&viewport.x>0&&viewport.x<1&&viewport.y>0&&viewport.y<1;lockMarker.gameObject.SetActive(visible);
            if(visible){lockMarker.anchorMin=lockMarker.anchorMax=new Vector2(viewport.x,viewport.y);lockMarker.anchoredPosition=Vector2.zero;markerDistance.text=distance.ToString("0")+"m / "+(friendly?(red?"回復ロック / 近距離":"味方ロック"):(red?"赤ロック":"黄ロック"))+(string.IsNullOrEmpty(targetInfo)?"":"\n"+targetInfo);markerDiamond.color=friendly?new Color(0.3f,1,0.65f):red?new Color(1,0.35f,0.2f):new Color(1,0.82f,0.2f);}
        }
        public void SetIncomingLockWarning(int count,bool recentlyHit,float clock)
        {
            incomingLockPanel.gameObject.SetActive(count>0 || recentlyHit);
            IncomingLockWarning.text=count>0?"被ロック ×"+count+"\n"+(recentlyHit?"被弾！ 射線を切って退避・回復":"敵に狙われています / 遮蔽物を利用"):recentlyHit?"ロック解除 / 被弾注意\n遮蔽物や自軍本拠点で回復":"";
            float pulse=0.72f+0.12f*Mathf.Sin(clock*6);
            incomingLockPanel.color=recentlyHit?new Color(0.62f,0.08f,0.035f,pulse):new Color(0.42f,0.20f,0.025f,pulse);
        }
        public void SetDanger(float health,bool disrupted,bool overheated=false)
        {danger.text=disrupted?"行動阻害中 / よろけ・転倒":overheated?"OVERHEAT / 満タンまでブースト不可":health<0.35f?"HP低下 / 遮蔽物へ退避・回復":"";}
        public static void Fill(UnityEngine.UI.Image image, float ratio)
        { image.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1); }
        private GameObject Screen(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image)); go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var image = go.GetComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = color.a > 0.1f; return go;
        }
        private RectTransform Box(Transform parent, string name, float x, float y, float width, float height, Color color)
        {
            var rt = Rect(parent, name, x, y, width, height);
            var image = rt.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = false; return rt;
        }
        private TMP_Text Label(Transform parent, string value, float x, float y, float width, float height, int size, Color color)
        {
            var rt = Rect(parent, "Label", x, y, width, height); var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font; text.text = value; text.fontSize = size; text.enableAutoSizing = false; text.color = color;
            text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Truncate; return text;
        }
        private TMP_Text FlowLabel(Transform parent, string value, float height, int size)
        { var t = Label(parent, value, 0, 0, 100, height, size, Color.white); t.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = height; return t; }
        private RectTransform Row(Transform parent, float height)
        {
            var rt = Rect(parent, "Row", 0, 0, 100, height); rt.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = height;
            var l = rt.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>(); l.spacing = 10; l.childControlWidth = true; l.childControlHeight = true;
            l.childForceExpandWidth = true; l.childForceExpandHeight = true; return rt;
        }
        private UnityEngine.UI.Button Button(Transform parent, string title, Action action, bool primary = false)
        { return ButtonAt(parent, title, 0, 0, 100, 50, action, primary); }
        private UnityEngine.UI.Button ButtonAt(Transform parent, string title, float x, float y, float width, float height, Action action, bool primary = false)
        {
            var rt = Rect(parent, "Button", x, y, width, height); var image = rt.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = primary ? new Color(0.06f, 0.43f, 0.41f) : new Color(0.13f, 0.21f, 0.29f); image.raycastTarget = true;
            var button = rt.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = new Color(0.65f, 0.95f, 0.95f); colors.pressedColor = new Color(0.45f, 0.8f, 0.8f); colors.disabledColor = new Color(0.35f, 0.4f, 0.45f); button.colors = colors;
            button.onClick.AddListener(() => action());
            var text = Label(rt, title, 0, 0, width, height, 23, Color.white);
            var tr = text.rectTransform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = new Vector2(8, 5); tr.offsetMax = new Vector2(-8, -5); text.alignment = TextAlignmentOptions.Center;
            return button;
        }
        private UnityEngine.UI.Image Bar(Transform parent, float x, float y, float width, float height, Color color)
        {
            var track = Box(parent, "BarTrack", x, y, width, height, new Color(0.09f, 0.14f, 0.20f));
            var fill = Box(track, "Fill", 0, 0, width, height, color); fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one; fill.offsetMin = fill.offsetMax = Vector2.zero;
            return fill.GetComponent<UnityEngine.UI.Image>();
        }
        private static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1); rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(width, height); return rt;
        }
    }
}
