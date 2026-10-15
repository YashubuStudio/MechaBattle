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
        public readonly GameObject Menu, Hud, Respawn, Pause, Results;
        public readonly TMP_Text Status, Stats, PreviewTitle, Gauges, OrangeText, Timer, Vitals, Objective, DeckStatus, Waiting, ResultTitle, ResultStats;
        public readonly RectTransform MinimapPanel;
        public readonly TMP_Text[] DeckLabels = new TMP_Text[4], PartLabels = new TMP_Text[3], RespawnLabels = new TMP_Text[4];
        public readonly UnityEngine.UI.Button[] RespawnChoices = new UnityEngine.UI.Button[4];
        public readonly UnityEngine.UI.Button Deploy;
        public readonly TMP_Text SpawnLabel;
        public readonly UnityEngine.UI.Image HealthFill, ShieldFill, BoostFill, BlueGauge, OrangeGauge;
        public readonly TMP_Text Crosshair;
        private readonly TMP_FontAsset font;
        private static readonly Color Ink = new Color(0.035f, 0.06f, 0.09f, 0.97f);
        private static readonly Color Panel = new Color(0.075f, 0.11f, 0.16f, 0.97f);
        private static readonly Color Accent = new Color(0.23f, 0.79f, 0.74f);

        public PlayableUI(Transform parent, TMP_FontAsset font, Action<int> team, Action<int> deck, Action<int> preset,
            Action<int> part, Action save, Action start, Action resume, Action back, Action<int> respawnDeck, Action spawnPoint, Action deploy)
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
            Menu = Screen(root, "Preparation", new Color(0.025f, 0.045f, 0.07f, 0.86f));
            // The right side stays transparent to the live assembled-mecha preview.
            Menu.GetComponent<UnityEngine.UI.Image>().color = new Color(0, 0, 0, 0);
            Label(Menu.transform, "MECHABATTLE", 50, 35, 900, 70, 46, Color.white);
            Label(Menu.transform, "50 vs 50  /  OFFLINE", 1390, 56, 480, 40, 22, Accent);
            var left = Box(Menu.transform, "Hangar", 48, 142, 886, 850, Panel);
            var column = left.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>(); column.padding = new RectOffset(24, 24, 18, 18);
            column.spacing = 8; column.childControlWidth = true; column.childControlHeight = true; column.childForceExpandHeight = false; column.childForceExpandWidth = true;
            FlowLabel(left, "出撃準備・機体構築", 48, 34);
            var teams = Row(left, 45); Button(teams, "青軍で参戦", () => team(0)); Button(teams, "橙軍で参戦", () => team(1));
            var decks = Row(left, 69);
            for (int i = 0; i < 4; i++) { int index = i; DeckLabels[i] = Button(decks, "機体 " + (i + 1), () => deck(index)).GetComponentInChildren<TMP_Text>(); }
            var presets = Row(left, 42); Button(presets, "汎用テンプレート", () => preset(0)); Button(presets, "補強テンプレート", () => preset(1));
            for (int i = 0; i < 3; i++) { int index = i; var row = Row(left, 47); PartLabels[i] = Button(row, "部品を選択", () => part(index)).GetComponentInChildren<TMP_Text>(); }
            Stats = FlowLabel(left, "", 145, 23);
            FlowLabel(left, "敵本拠点を攻撃し、敵の戦力ゲージを0にすると勝利。\n中継を占領すると前線から出撃できます。\n味方本拠点の近くでHP・シールド・弾薬を補給。", 96, 21);
            var actions = Row(left, 55); Button(actions, "構成を保存", save); Button(actions, "このデッキで出撃", start, true);
            PreviewTitle = Label(Menu.transform, "標準フレーム", 1010, 145, 860, 50, 28, Accent);
            var controls = Box(Menu.transform, "Controls", 1000, 844, 870, 158, Panel);
            Label(controls, "WASD  移動    マウス  照準    左クリック  射撃\nShift  ブースト    E  格闘    右クリック  照準拡大\nEsc  一時停止    撃破後に機体と出撃先を選択", 24, 18, 820, 130, 23, Color.white);
            Status = Label(Menu.transform, "", 50, 1010, 1820, 55, 21, Accent);

            Hud = Screen(root, "BattleHUD", Color.clear);
            BlueGauge = Bar(Hud.transform, 40, 73, 690, 14, new Color(0.2f, 0.48f, 1));
            OrangeGauge = Bar(Hud.transform, 1190, 73, 690, 14, new Color(1, 0.45f, 0.12f));
            Gauges = Label(Hud.transform, "", 40, 26, 690, 40, 26, Color.white);
            OrangeText = Label(Hud.transform, "", 1190, 26, 690, 40, 26, Color.white); OrangeText.alignment = TextAlignmentOptions.Right;
            MinimapPanel = Box(Hud.transform, "Minimap", 40, 125, 310, 190, Ink);
            Label(Hud.transform, "↑ 北　■ 本拠点　◆ 中継　白:自機", 40, 325, 560, 40, 19, Color.white);
            DeckStatus = Label(Hud.transform, "", 1390, 805, 490, 190, 23, Color.white);
            Timer = Label(Hud.transform, "", 770, 32, 380, 50, 28, Accent); Timer.alignment = TextAlignmentOptions.Center;
            Timer.rectTransform.anchorMin = Timer.rectTransform.anchorMax = new Vector2(0.5f, 1); Timer.rectTransform.pivot = new Vector2(0.5f, 1); Timer.rectTransform.anchoredPosition = new Vector2(0, -32);
            Objective = Label(Hud.transform, "", 1450, 110, 430, 260, 22, Color.white);
            var vitalPanel = Box(Hud.transform, "Vitals", 40, 813, 510, 225, Ink);
            Vitals = Label(vitalPanel, "", 18, 10, 480, 132, 25, Color.white);
            HealthFill = Bar(vitalPanel, 18, 153, 470, 12, new Color(0.3f, 0.87f, 0.64f));
            ShieldFill = Bar(vitalPanel, 18, 174, 470, 12, new Color(0.3f, 0.65f, 1));
            BoostFill = Bar(vitalPanel, 18, 195, 470, 10, Accent);
            Crosshair = Label(Hud.transform, "+", 930, 510, 60, 60, 42, Color.white); Crosshair.alignment = TextAlignmentOptions.Center;
            Crosshair.rectTransform.anchorMin = Crosshair.rectTransform.anchorMax = new Vector2(0.5f, 0.5f); Crosshair.rectTransform.pivot = new Vector2(0.5f, 0.5f); Crosshair.rectTransform.anchoredPosition = Vector2.zero;
            Label(Hud.transform, "WASD 移動   左クリック 射撃   Shift ブースト   E 格闘   Esc メニュー", 620, 1010, 1250, 50, 21, Color.white);

            Respawn = Screen(root, "Redeploy", new Color(0.02f, 0.035f, 0.06f, 0.8f));
            var rp = Box(Respawn.transform, "RedeployPanel", 430, 280, 1060, 510, Panel);
            Label(rp, "機体が撃破されました", 32, 25, 990, 60, 38, Color.white);
            Waiting = Label(rp, "", 32, 96, 990, 75, 24, Accent);
            var choiceRow = Box(rp, "Choices", 32, 187, 990, 95, Color.clear); var layout = choiceRow.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 10; layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandWidth = true;
            for (int i = 0; i < 4; i++) { int index = i; RespawnChoices[i] = Button(choiceRow, "", () => respawnDeck(index)); RespawnLabels[i] = RespawnChoices[i].GetComponentInChildren<TMP_Text>(); }
            SpawnLabel = ButtonAt(rp, "出撃先: 本拠点", 32, 310, 990, 58, spawnPoint).GetComponentInChildren<TMP_Text>();
            Deploy = ButtonAt(rp, "出撃する", 32, 395, 990, 70, deploy, true);

            Pause = Screen(root, "Pause", new Color(0.02f, 0.035f, 0.06f, 0.88f));
            Label(Pause.transform, "一時停止", 660, 315, 600, 90, 48, Color.white).alignment = TextAlignmentOptions.Center;
            ButtonAt(Pause.transform, "戦闘に戻る", 660, 450, 600, 70, resume, true);
            ButtonAt(Pause.transform, "参戦準備へ戻る", 660, 550, 600, 70, back);

            Results = Screen(root, "Results", new Color(0.025f, 0.045f, 0.07f, 0.95f));
            ResultTitle = Label(Results.transform, "", 360, 230, 1200, 110, 60, Accent); ResultTitle.alignment = TextAlignmentOptions.Center;
            ResultStats = Label(Results.transform, "", 530, 390, 860, 265, 28, Color.white);
            ButtonAt(Results.transform, "同じデッキで再戦", 450, 720, 490, 75, start, true);
            ButtonAt(Results.transform, "機体構築へ戻る", 980, 720, 490, 75, back);
            ShowMenu();
        }
        public void ShowMenu() { Menu.SetActive(true); Hud.SetActive(false); Respawn.SetActive(false); Pause.SetActive(false); Results.SetActive(false); }
        public void ShowBattle(bool dead, bool paused)
        { Menu.SetActive(false); Hud.SetActive(true); Respawn.SetActive(dead && !paused); Pause.SetActive(paused); Results.SetActive(false); }
        public void ShowResults() { Menu.SetActive(false); Hud.SetActive(false); Respawn.SetActive(false); Pause.SetActive(false); Results.SetActive(true); }
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
