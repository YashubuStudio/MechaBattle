using MechaBattle.Battlefield;
using MechaBattle.Contracts;
using MechaBattle.Simulation;
using UnityEngine;
namespace MechaBattle.Playable
{
    internal sealed class PlayableMinimap
    {
        private readonly UnityEngine.UI.Image[] units = new UnityEngine.UI.Image[MatchLimits.TotalControllerSlots];
        private readonly UnityEngine.UI.Image[] bases, captures;
        private readonly MapDefinition map;
        private readonly Color[] teams = { new Color(0.3f, 0.6f, 1), new Color(1, 0.5f, 0.2f) };
        public PlayableMinimap(RectTransform panel, MapDefinition map)
        {
            this.map = map; bases = new UnityEngine.UI.Image[map.mainBases.Length]; captures = new UnityEngine.UI.Image[map.capturePoints.Length];
            for (int i = 0; i < bases.Length; i++) { var b = map.mainBases[i]; bases[i] = Dot(panel, "Base", 11, teams[b.team]); Position(bases[i], b.x, b.z); }
            for (int i = 0; i < captures.Length; i++) { var c = map.capturePoints[i]; captures[i] = Dot(panel, "Relay", 9, Color.gray); Position(captures[i], c.x, c.z); }
            for (int i = 0; i < units.Length; i++) units[i] = Dot(panel, "Unit", 4, Color.white);
        }
        public void Update(BattleSimulation world, int human)
        {
            var self = world.GetUnit(human); float range = world.GetLoadout(human, self.DeckIndex).FireRange;
            for (int i = 0; i < units.Length; i++)
            {
                var u = world.GetUnit(i); float dx = self.X - u.X, dz = self.Z - u.Z;
                // Allied telemetry and local visual contacts only. No full-map enemy radar.
                bool visible = u.IsAlive && (u.Team == self.Team || (self.IsAlive && dx * dx + dz * dz <= range * range));
                units[i].gameObject.SetActive(visible); if (!visible) continue;
                units[i].color = i == human ? Color.white : teams[u.Team]; Position(units[i], u.X, u.Z);
                units[i].rectTransform.sizeDelta = Vector2.one * (i == human ? 8 : 4);
            }
            for (int i = 0; i < captures.Length; i++) { int owner = world.GetCapturePoint(i).OwnerTeam; captures[i].color = owner < 0 ? Color.gray : teams[owner]; }
        }
        private void Position(UnityEngine.UI.Image dot, float x, float z)
        { dot.rectTransform.anchoredPosition = new Vector2(x / map.halfWidth * 145, z / map.halfDepth * 85); }
        private static UnityEngine.UI.Image Dot(Transform parent, string name, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f); rect.sizeDelta = Vector2.one * size;
            var image = go.GetComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = false; return image;
        }
    }
}
