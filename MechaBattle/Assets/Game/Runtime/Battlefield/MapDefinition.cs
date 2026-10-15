using System;
using System.Collections.Generic;
using MechaBattle.Contracts;

namespace MechaBattle.Battlefield
{
    // Field names are the JSON contract used by config/standard-map.json.
    [Serializable]
    public sealed class MapDefinition
    {
        public string id;
        public bool prototypeOnly;
        public float halfWidth, halfDepth;
        public MainBaseDefinition[] mainBases;
        public CapturePointDefinition[] capturePoints;
    }

    [Serializable]
    public sealed class MainBaseDefinition
    {
        public string id;
        public int team;
        public float x, z, spawnRadius, attackRadius;
        public string approachRoute;
    }

    [Serializable]
    public sealed class CapturePointDefinition
    {
        public string id;
        public float x, z, radius;
    }

    public static class MapValidator
    {
        // This validates declared topology only. NavMesh reachability and physical
        // attack colliders must be verified by the later Editor Generator/PlayMode tests.
        public static IReadOnlyList<string> GetErrors(MapDefinition map)
        {
            var errors = new List<string>();
            if (map == null) { errors.Add("Map is null."); return errors; }
            if (string.IsNullOrWhiteSpace(map.id)) errors.Add("Map id is required.");
            if (!Positive(map.halfWidth) || !Positive(map.halfDepth))
                errors.Add("Map bounds must be finite and positive.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var counts = new int[MatchLimits.TeamCount];
            var routes = new HashSet<string>[MatchLimits.TeamCount];
            for (int t = 0; t < routes.Length; t++) routes[t] = new HashSet<string>(StringComparer.Ordinal);
            if (map.mainBases == null) errors.Add("Main bases are required.");
            else foreach (var b in map.mainBases)
            {
                if (b == null) { errors.Add("Null main base."); continue; }
                CheckId(b.id, ids, errors);
                bool validTeam = b.team >= 0 && b.team < MatchLimits.TeamCount;
                if (!validTeam) errors.Add("Invalid main base team: " + b.id);
                else counts[b.team]++;
                CheckPosition(map, b.id, b.x, b.z, errors);
                if (!Positive(b.spawnRadius) || !Positive(b.attackRadius))
                    errors.Add("Main base radii must be finite and positive: " + b.id);
                if (string.IsNullOrWhiteSpace(b.approachRoute))
                    errors.Add("Main base approach route is required: " + b.id);
                else if (validTeam) routes[b.team].Add(b.approachRoute);
            }
            for (int team = 0; team < MatchLimits.TeamCount; team++)
            {
                if (counts[team] < MatchLimits.MinimumMainBasesPerTeam)
                    errors.Add("FR-108: team " + team + " requires at least two MAIN bases.");
                if (routes[team].Count < 2)
                    errors.Add("FR-110: team " + team + " requires distinct declared approach routes.");
            }
            if (map.capturePoints == null) errors.Add("Capture point array must be explicit (may be empty).");
            else foreach (var c in map.capturePoints)
            {
                if (c == null) { errors.Add("Null capture point."); continue; }
                CheckId(c.id, ids, errors);
                CheckPosition(map, c.id, c.x, c.z, errors);
                if (!Positive(c.radius)) errors.Add("Capture radius must be finite and positive: " + c.id);
            }
            return errors;
        }

        public static void ThrowIfInvalid(MapDefinition map)
        {
            var errors = GetErrors(map);
            if (errors.Count > 0) throw new ArgumentException(string.Join("\n", errors), nameof(map));
        }

        private static bool Finite(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }
        private static bool Positive(float v) { return Finite(v) && v > 0; }
        private static void CheckId(string id, HashSet<string> ids, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(id)) errors.Add("Objective id is required.");
            else if (!ids.Add(id)) errors.Add("Duplicate objective id: " + id);
        }
        private static void CheckPosition(MapDefinition map, string id, float x, float z, List<string> errors)
        {
            if (!Finite(x) || !Finite(z) || Math.Abs(x) > map.halfWidth || Math.Abs(z) > map.halfDepth)
                errors.Add("Objective position is non-finite or outside map bounds: " + id);
        }
    }
}
