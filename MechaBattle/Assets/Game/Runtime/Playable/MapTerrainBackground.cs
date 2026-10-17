using System;
using MechaBattle.Battlefield;
using UnityEngine;

namespace MechaBattle.Playable
{
    // The same public overhead terrain is used by deployment, HUD and tactical maps.
    // Built once per view; live contacts and objectives remain separate overlays.
    internal static class MapTerrainBackground
    {
        public static Vector2 FitSize(MapDefinition map, Vector2 available)
        {
            float scale = Mathf.Min(available.x / (2 * map.halfWidth), available.y / (2 * map.halfDepth));
            return new Vector2(2 * map.halfWidth * scale, 2 * map.halfDepth * scale);
        }

        public static Vector2 Position(RectTransform field, MapDefinition map, float x, float z)
            => new Vector2(x / map.halfWidth * field.rect.width / 2, z / map.halfDepth * field.rect.height / 2);

        public static void Draw(RectTransform field, MapDefinition map)
        {
            var background = Rect(field, "TerrainBackground", Vector2.zero, field.rect.size);
            background.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            Image(background, new Color(.07f, .13f, .18f));
            float scale = field.rect.width / (2 * map.halfWidth);
            for (int i = 1; i < 8; i++)
            {
                float t = i / 8f - .5f;
                Image(Rect(background, "Grid", new Vector2(t * field.rect.width, 0), new Vector2(1, field.rect.height)), new Color(.3f, .5f, .6f, .18f));
                Image(Rect(background, "Grid", new Vector2(0, t * field.rect.height), new Vector2(field.rect.width, 1)), new Color(.3f, .5f, .6f, .18f));
            }
            foreach (var obstacle in map.obstacles ?? Array.Empty<ObstacleDefinition>())
            {
                // Surface decks would cover the buildings; basement walls hide their routes.
                string id = obstacle.id ?? "";
                if (id.StartsWith("Street-deck-", StringComparison.Ordinal) || id.StartsWith("Basement-wall-", StringComparison.Ordinal) || id.StartsWith("Access-", StringComparison.Ordinal)) continue;
                var terrain = Rect(background, "Terrain_" + id, Position(field, map, obstacle.x, obstacle.z), new Vector2(obstacle.width * scale, obstacle.depth * scale));
                Image(terrain, obstacle.platform ? new Color(.22f, .32f, .36f) : new Color(.32f, .4f, .44f));
            }
            DrawUnderground(background, map);
        }

        private static void DrawUnderground(RectTransform field, MapDefinition map)
        {
            if (map.surfaceY <= 0 || map.navigationNodes == null) return;
            float thickness = Mathf.Clamp(field.rect.width / 200, 1.5f, 5);
            for (int i = 0; i < map.navigationNodes.Length; i++)
            foreach (int j in map.navigationNodes[i].links)
            {
                var a = map.navigationNodes[i]; var b = map.navigationNodes[j];
                if (j <= i || a.y >= map.surfaceY || b.y >= map.surfaceY) continue;
                Vector2 start = Position(field, map, a.x, a.z), finish = Position(field, map, b.x, b.z), delta = finish - start;
                var route = Rect(field, "UndergroundRoute", (start + finish) / 2, new Vector2(delta.magnitude, thickness));
                route.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                Image(route, new Color(.2f, .85f, .9f, .65f));
            }
        }

        private static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = position; rect.sizeDelta = size;
            return rect;
        }
        private static void Image(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color; image.raycastTarget = false;
        }
    }
}
