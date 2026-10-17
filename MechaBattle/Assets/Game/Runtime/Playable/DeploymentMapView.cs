using System;
using MechaBattle.Battlefield;
using MechaBattle.Contracts;
using MechaBattle.Simulation;
using TMPro;
using UnityEngine;

namespace MechaBattle.Playable
{
    // Public terrain/objectives plus allied positions and bounded team contact reports.
    // Constructed once: updates reuse the same graphics and button listeners.
    internal sealed class DeploymentMapView
    {
        private readonly MapDefinition map;
        private readonly RectTransform field;
        private readonly TMP_FontAsset font;
        private readonly TMP_Text north;
        private readonly UnityEngine.UI.Button[] bases, relays;
        private readonly TMP_Text[] baseLabels, relayLabels;
        private readonly UnityEngine.UI.Outline[] baseOutlines, relayOutlines;
        private readonly UnityEngine.UI.Image[] units = new UnityEngine.UI.Image[MatchLimits.TotalControllerSlots];
        private static readonly Color[] Teams = { new Color(0.3f, 0.65f, 1), new Color(1, 0.56f, 0.23f) };
        private static readonly Color Selected = new Color(1, 0.93f, 0.35f);

        public DeploymentMapView(RectTransform panel, MapDefinition map, TMP_FontAsset font, Action<int, int> choose)
        {
            this.map = map; this.font = font;
            field = Rect(panel, "MapField", Vector2.zero, MapTerrainBackground.FitSize(map, panel.rect.size - new Vector2(120, 100)));
            MapTerrainBackground.Draw(field, map);
            north=Text(panel, "North", map.surfaceY>0?"↑ 北 / 水色:"+map.LowerLayerName:"↑ 北", new Vector2(0, panel.rect.height / 2 - 21), new Vector2(map.surfaceY>0?320:120, 32), 22);
            for (int i = 0; i < units.Length; i++)
                units[i] = Image(Rect(field, "Contact" + i, Vector2.zero, new Vector2(6, 6)), Color.white);
            bases = new UnityEngine.UI.Button[map.mainBases.Length]; baseLabels = new TMP_Text[bases.Length]; baseOutlines = new UnityEngine.UI.Outline[bases.Length];
            relays = new UnityEngine.UI.Button[map.capturePoints.Length]; relayLabels = new TMP_Text[relays.Length]; relayOutlines = new UnityEngine.UI.Outline[relays.Length];
            for (int i = 0; i < bases.Length; i++)
            {
                int index = i; var b = map.mainBases[i];
                bases[i] = Marker("MainBase" + i, b.x, b.z, "■", () => choose(-1, index), out baseLabels[i], out baseOutlines[i]);
            }
            for (int i = 0; i < relays.Length; i++)
            {
                int index = i; var c = map.capturePoints[i];
                relays[i] = Marker("Relay" + i, c.x, c.z, "◆", () => choose(index, -1), out relayLabels[i], out relayOutlines[i]);
            }
        }

        public void Refresh(BattleSimulation world, int team, int selectedRelay, int selectedBase)
        {
            for (int i = 0; i < bases.Length; i++)
            {
                var b = map.mainBases[i]; bool available = b.team == team&&(world==null||!world.GetBase(i).Destroyed);
                bases[i].interactable = available;
                bases[i].targetGraphic.color = Teams[b.team];
                baseOutlines[i].enabled = selectedRelay < 0 && selectedBase == i;
                baseLabels[i].text = map.MainBaseName(i)+(map.surfaceY>0&&b.y<map.surfaceY?" "+map.LowerLayerName:"")+(world!=null&&world.GetBase(i).Destroyed?"\n陥落":world!=null?"\nHP "+world.GetBase(i).Health.ToString("0"):available?"":"\n敵本拠点");
                baseLabels[i].color = baseOutlines[i].enabled ? Selected : Color.white;
            }
            for (int i = 0; i < relays.Length; i++)
            {
                int owner = world == null ? -1 : world.GetCapturePoint(i).OwnerTeam;
                bool available = world != null && world.CanSpawnAtCapture(team, i);
                relays[i].interactable = available;
                relays[i].targetGraphic.color = owner < 0 ? Color.gray : Teams[owner];
                relayOutlines[i].enabled = selectedRelay == i;
                string status = available ? "出撃可" : owner == team ? "敵接近・不可" : owner < 0 ? "未占領" : "敵軍・不可";
                relayLabels[i].text = map.capturePoints[i].id+(map.surfaceY>0&&map.capturePoints[i].y<map.surfaceY?" "+map.LowerLayerName:"") + "\n" + status;
                relayLabels[i].color = relayOutlines[i].enabled ? Selected : available ? Color.white : new Color(0.72f, 0.76f, 0.8f);
            }
            for (int i = 0; i < units.Length; i++)
            {
                if (world == null) { units[i].gameObject.SetActive(false); continue; }
                var u = world.GetUnit(i); bool ally = u.Team == team;
                var contact = world.GetTeamContact(team, i);
                bool visible = ally ? u.IsAlive : contact.Valid;
                units[i].gameObject.SetActive(visible);
                if (!visible) continue;
                units[i].rectTransform.anchoredPosition = ally ? Position(u.X, u.Z) : Position(contact.X, contact.Z);
                units[i].color = ally ? Teams[team] : new Color(1, 0.32f, 0.3f);
            }
        }

        public void Dispose()
        {
            field.gameObject.SetActive(false);field.SetParent(null,false);UnityEngine.Object.Destroy(field.gameObject);
            north.gameObject.SetActive(false);north.transform.SetParent(null,false);UnityEngine.Object.Destroy(north.gameObject);
        }
        private Vector2 Position(float x, float z) => MapTerrainBackground.Position(field, map, x, z);
        private UnityEngine.UI.Button Marker(string name, float x, float z, string symbol, Action click, out TMP_Text caption, out UnityEngine.UI.Outline outline)
        {
            var rt = Rect(field, name, Position(x, z), new Vector2(52, 46));
            var image = Image(rt, Color.white); image.raycastTarget = true;
            var button = rt.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = new Color(0.75f, 1, 1); colors.disabledColor = new Color(0.45f, 0.45f, 0.45f); button.colors = colors;
            button.onClick.AddListener(() => click());
            outline = rt.gameObject.AddComponent<UnityEngine.UI.Outline>(); outline.effectColor = Selected; outline.effectDistance = new Vector2(4, -4); outline.enabled = false;
            Text(rt, "Symbol", symbol, Vector2.zero, new Vector2(48, 42), 28);
            caption = Text(rt, "Caption", "", new Vector2(0, -47), new Vector2(180, 54), 18);
            return button;
        }
        private TMP_Text Text(Transform parent, string name, string value, Vector2 position, Vector2 size, int fontSize)
        {
            var text = Rect(parent, name, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font; text.fontSize = fontSize; text.text = value; text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Truncate;
            return text;
        }
        private static UnityEngine.UI.Image Image(RectTransform rt, Color color)
        { var image = rt.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = false; return image; }
        private static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = position; rt.sizeDelta = size;
            return rt;
        }
    }
}
