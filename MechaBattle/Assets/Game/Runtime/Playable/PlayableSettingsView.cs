using MechaBattle.Contracts.Settings;
using MechaBattle.Settings;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MechaBattle.Playable
{
    internal sealed class PlayableSettingsView
    {
        public readonly GameObject Panel;
        public bool IsOpen => Panel.activeSelf;
        private readonly TMP_FontAsset font;
        private readonly TMP_Text sensitivityValue, volumeValue, qualityValue, status,radarValue,interceptorValue,mapReaderValue;
        private readonly UnityEngine.UI.Slider sensitivity, volume,radarRate,interceptorRate,mapReaderRate;
        private readonly UnityEngine.UI.Button quality, cancel;
        private readonly CanvasGroup[] backgrounds;
        private readonly GameObject[] backgroundScreens;
        private readonly bool[] previousInteractable, previousRaycasts;
        private GameObject previousSelection;
        private PlayerPreferences draft, defaults;
        private string[] qualityNames = Array.Empty<string>();

        public PlayableSettingsView(Transform parent, TMP_FontAsset font, GameObject[] screens, Action<PlayerPreferences> apply, Action close)
        {
            this.font = font;
            backgrounds = new CanvasGroup[screens.Length]; previousInteractable = new bool[screens.Length]; previousRaycasts = new bool[screens.Length];
            backgroundScreens = (GameObject[])screens.Clone();
            for (int i = 0; i < screens.Length; i++) backgrounds[i] = GetOrAddCanvasGroup(backgroundScreens[i]);
            Panel = new GameObject("Settings", typeof(RectTransform), typeof(UnityEngine.UI.Image)); Panel.transform.SetParent(parent, false);
            var rt = Panel.GetComponent<RectTransform>(); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var backdrop = Panel.GetComponent<UnityEngine.UI.Image>(); backdrop.color = new Color(0.02f, 0.035f, 0.06f, 0.98f); backdrop.raycastTarget = true;
            var box = Rect(Panel.transform, "SettingsPanel", 390, 50, 1140, 980); Image(box, new Color(0.075f, 0.11f, 0.16f, 1));
            Text(box, "Title", "設定", 44, 30, 1052, 62, 40);
            Text(box, "Help", "「適用して保存」で反映。BOTの編成割合は次の試合から反映します。", 44, 105, 1052, 48, 23);
            Text(box, "SensitivityLabel", "マウス感度", 44, 180, 290, 54, 28);
            sensitivity = Slider(box, "Sensitivity", 350, 180, 620, 54);
            sensitivityValue = Text(box, "SensitivityValue", "", 985, 180, 120, 54, 26);
            Text(box, "SensitivityHelp", "低いほど細かく照準できます。上下・左右で共通です。", 350, 248, 750, 42, 21);
            Text(box, "VolumeLabel", "全体音量", 44, 310, 290, 54, 28);
            volume = Slider(box, "MasterVolume", 350, 310, 620, 54); volume.minValue = 0; volume.maxValue = 1;
            volumeValue = Text(box, "VolumeValue", "", 985, 310, 120, 54, 26);
            Text(box, "VolumeHelp", "0%で消音。射撃・被弾・ロック警告など全てに適用。", 350, 374, 750, 42, 21);
            Text(box, "QualityLabel", "グラフィック品質", 44, 440, 290, 54, 28);
            quality = Button(box, "Quality", "", 350, 440, 750, 62, () =>
            { if (draft != null && qualityNames.Length > 0) { draft.qualityLevel = (draft.qualityLevel + 1) % qualityNames.Length; draft.qualityName = qualityNames[draft.qualityLevel]; RefreshValues(); } });
            qualityValue = quality.GetComponentInChildren<TMP_Text>();
            Text(box, "QualityHelp", "クリックで切替。プロジェクトに用意された品質設定を使用します。", 350, 506, 750, 42, 21);
            Text(box,"RadarBotLabel","レーダー設置役",44,555,290,48,25);
            radarRate=Slider(box,"RadarBotRate",350,555,620,44);radarRate.minValue=0;radarRate.maxValue=100;radarRate.wholeNumbers=true;
            radarValue=Text(box,"RadarBotValue","",985,555,120,48,25);
            Text(box,"InterceptorBotLabel","迎撃・防衛役",44,625,290,48,25);
            interceptorRate=Slider(box,"InterceptorBotRate",350,625,620,44);interceptorRate.minValue=0;interceptorRate.maxValue=100;interceptorRate.wholeNumbers=true;
            interceptorValue=Text(box,"InterceptorBotValue","",985,625,120,48,25);
            Text(box,"MapReaderBotLabel","通常役の地図確認率",44,695,300,48,24);
            mapReaderRate=Slider(box,"MapReaderBotRate",350,695,620,44);mapReaderRate.minValue=0;mapReaderRate.maxValue=100;mapReaderRate.wholeNumbers=true;
            mapReaderValue=Text(box,"MapReaderBotValue","",985,695,120,48,25);
            status = Text(box, "Status", "", 44, 765, 1052, 78, 23);
            Button(box, "Defaults", "初期値に戻す", 44, 874, 290, 66, () => { draft = defaults.Copy(); SetControls(); });
            cancel = Button(box, "Cancel", "キャンセル / Esc", 390, 874, 300, 66, close);
            Button(box, "Apply", "適用して保存", 730, 874, 370, 66, () => { if (draft != null) apply(draft.Copy()); });
            sensitivity.onValueChanged.AddListener(value => { if (draft != null) { draft.mouseSensitivity = value; RefreshValues(); } });
            volume.onValueChanged.AddListener(value => { if (draft != null) { draft.masterVolume = value; RefreshValues(); } });
            radarRate.onValueChanged.AddListener(value=>{if(draft==null)return;draft.botMixConfigured=true;draft.botRadarFraction=value/100;draft.botInterceptorFraction=Mathf.Min(draft.botInterceptorFraction,1-draft.botRadarFraction);SetBotControls();RefreshValues();});
            interceptorRate.onValueChanged.AddListener(value=>{if(draft==null)return;draft.botMixConfigured=true;draft.botInterceptorFraction=value/100;draft.botRadarFraction=Mathf.Min(draft.botRadarFraction,1-draft.botInterceptorFraction);SetBotControls();RefreshValues();});
            mapReaderRate.onValueChanged.AddListener(value=>{if(draft==null)return;draft.botMixConfigured=true;draft.botMapReaderFraction=value/100;RefreshValues();});
            Panel.SetActive(false);
        }
        public void Open(PlayerPreferences current, PlayerPreferences initial, string[] names, float minimum, float maximum)
        {
            if (IsOpen) return;
            draft = null; defaults = initial.Copy(); qualityNames = names == null ? Array.Empty<string>() : (string[])names.Clone();
            sensitivity.minValue = minimum; sensitivity.maxValue = maximum; draft = current.Copy();
            SetControls();
            previousSelection = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
            for (int i = 0; i < backgrounds.Length; i++)
            {
                if (backgrounds[i] == null) backgrounds[i] = GetOrAddCanvasGroup(backgroundScreens[i]);
                previousInteractable[i] = backgrounds[i].interactable; previousRaycasts[i] = backgrounds[i].blocksRaycasts;
                backgrounds[i].interactable = backgrounds[i].blocksRaycasts = false;
            }
            Panel.SetActive(true); Panel.transform.SetAsLastSibling();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(cancel.gameObject);
        }
        public void Close()
        {
            if (!IsOpen) return;
            Panel.SetActive(false); draft = null;
            for (int i = 0; i < backgrounds.Length; i++)
            { if (backgrounds[i] != null) { backgrounds[i].interactable = previousInteractable[i]; backgrounds[i].blocksRaycasts = previousRaycasts[i]; } }
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
            previousSelection = null;
        }
        private static CanvasGroup GetOrAddCanvasGroup(GameObject screen)
        {
            var group = screen.GetComponent<CanvasGroup>();
            // Unity may return a managed missing-component wrapper in the Editor.
            // C# ?? only checks CLR null and skips AddComponent for that wrapper.
            if (group == null) group = screen.AddComponent<CanvasGroup>();
            return group;
        }
        public void ShowError(string message) { status.text = message; status.color = new Color(1, 0.65f, 0.4f); }
        private void SetControls()
        {
            sensitivity.SetValueWithoutNotify(draft.mouseSensitivity); volume.SetValueWithoutNotify(draft.masterVolume);
            SetBotControls(); quality.interactable = qualityNames.Length > 0;
            status.color = new Color(0.4f, 0.9f, 0.85f); RefreshValues();
        }
        private void SetBotControls(){radarRate.SetValueWithoutNotify(draft.botRadarFraction*100);interceptorRate.SetValueWithoutNotify(draft.botInterceptorFraction*100);mapReaderRate.SetValueWithoutNotify(draft.botMapReaderFraction*100);}
        private void RefreshValues()
        {
            sensitivityValue.text = draft.mouseSensitivity.ToString("0.000"); volumeValue.text = (draft.masterVolume * 100).ToString("0") + "%";
            qualityValue.text = qualityNames.Length == 0 ? "品質設定なし" : qualityNames[draft.qualityLevel] + "  ›";
            radarValue.text=(draft.botRadarFraction*100).ToString("0")+"%";interceptorValue.text=(draft.botInterceptorFraction*100).ToString("0")+"%";mapReaderValue.text=(draft.botMapReaderFraction*100).ToString("0")+"%";
            int r=Mathf.FloorToInt(50*draft.botRadarFraction+.5f),d=Mathf.Min(50-r,Mathf.FloorToInt(50*draft.botInterceptorFraction+.5f));
            status.text="各軍の目安：レーダー "+r+" / 迎撃 "+d+" / 通常 "+(50-r-d)+"\n通常役の地図確認 "+mapReaderValue.text+"。次の試合から編成へ反映。";
        }
        private UnityEngine.UI.Slider Slider(Transform parent, string name, float x, float y, float width, float height)
        {
            var rt = Rect(parent, name, x, y, width, height); var background = Image(rt, new Color(0.12f, 0.2f, 0.27f)); background.raycastTarget = true;
            var fillArea = Rect(rt, "FillArea", 14, height / 2 - 5, width - 28, 10);
            var fill = Rect(fillArea, "Fill", 0, 0, width - 28, 10); fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one; fill.offsetMin = fill.offsetMax = Vector2.zero; Image(fill, new Color(0.23f, 0.79f, 0.74f));
            var handleArea = Rect(rt, "HandleArea", 14, 0, width - 28, height);
            var handle = Rect(handleArea, "Handle", 0, 0, 26, height - 8); handle.anchorMin = Vector2.zero; handle.anchorMax = new Vector2(0, 1); handle.pivot = new Vector2(0.5f, 0.5f); handle.anchoredPosition = Vector2.zero; handle.sizeDelta = new Vector2(26, -8);
            var grip = Image(handle, new Color(0.8f, 0.98f, 0.95f)); grip.raycastTarget = true;
            var slider = rt.gameObject.AddComponent<UnityEngine.UI.Slider>(); slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.fillRect = fill; slider.handleRect = handle; slider.targetGraphic = grip; return slider;
        }
        private UnityEngine.UI.Button Button(Transform parent, string name, string value, float x, float y, float width, float height, Action click)
        {
            var rt = Rect(parent, name, x, y, width, height); var image = Image(rt, new Color(0.06f, 0.43f, 0.41f)); image.raycastTarget = true;
            var button = rt.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image; button.onClick.AddListener(() => click());
            var text = Text(rt, "Text", value, 8, 5, width - 16, height - 10, 24); text.alignment = TextAlignmentOptions.Center; return button;
        }
        private TMP_Text Text(Transform parent, string name, string value, float x, float y, float width, float height, int size)
        {
            var text = Rect(parent, name, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>(); text.font = font; text.text = value;
            text.fontSize = size; text.color = Color.white; text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Truncate; return text;
        }
        private static UnityEngine.UI.Image Image(RectTransform rect, Color color)
        { var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = false; return image; }
        private static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1); rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(width, height); return rt;
        }
    }
}
