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
        public float halfWidth, halfDepth, surfaceY;
        public string lowerLayerName = "地下";
        public string LowerLayerName => string.IsNullOrWhiteSpace(lowerLayerName) ? "地下" : lowerLayerName;
        public NavigationNodeDefinition[] navigationNodes = Array.Empty<NavigationNodeDefinition>();
        public string MainBaseName(int index)
        {
            var target=mainBases[index];int rank=0;
            for(int i=0;i<mainBases.Length;i++)if(mainBases[i].team==target.team && (mainBases[i].z>target.z || mainBases[i].z==target.z&&i<index))rank++;
            return ((char)((target.team==0?'A':'X')+rank)).ToString();
        }
        public MainBaseDefinition[] mainBases;
        public CapturePointDefinition[] capturePoints;
        public ObstacleDefinition[] obstacles = Array.Empty<ObstacleDefinition>();
    }

    [Serializable]
    public sealed class NavigationNodeDefinition
    {
        public string id="";public float x,y,z;public int[] links=Array.Empty<int>();
    }

    [Serializable]
    public sealed class ObstacleDefinition
    {
        public string id = "";
        public float x, y, z, width, height, depth;
        public bool platform;
        public ObstacleDefinition Copy() { return (ObstacleDefinition)MemberwiseClone(); }
    }

    [Serializable]
    public sealed class MainBaseDefinition
    {
        public string id;
        public int team;
        public float x, y, z, spawnRadius, attackRadius, maxHealth;
        public bool spawnOverride;public float spawnX,spawnY,spawnZ;
        public float SpawnX=>spawnOverride?spawnX:x;public float SpawnY=>spawnOverride?spawnY:y;public float SpawnZ=>spawnOverride?spawnZ:z;
        public string approachRoute;

        // Optional "core must be entered to hit" radius (user request 2026-10-15): the
        // core (attackRadius) can only be damaged by an attacker standing inside this
        // larger building footprint, and the base's own counter-fire reaches exactly
        // this far too - one shared number instead of two independently tuned radii
        // that can drift out of sync. 0 (unset) preserves the old unrestricted-range
        // behaviour, so existing maps/tests that don't set this are unaffected.
        public float buildingRadius;
    }

    [Serializable]
    public sealed class CapturePointDefinition
    {
        public string id;
        public float x, y, z, radius;
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
            if(!Finite(map.surfaceY)||map.surfaceY<0)errors.Add("Invalid surface height.");
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
                if(!Finite(b.y)||b.y<0||!Finite(b.maxHealth)||b.maxHealth<0||!Finite(b.SpawnY)||b.SpawnY<0)errors.Add("Invalid base height/health: "+b.id);
                CheckPosition(map,b.id+" spawn",b.SpawnX,b.SpawnZ,errors);
                if (!Positive(b.spawnRadius) || !Positive(b.attackRadius))
                    errors.Add("Main base radii must be finite and positive: " + b.id);
                if (b.buildingRadius != 0 && (!Finite(b.buildingRadius) || b.buildingRadius <= 0 || b.buildingRadius < b.attackRadius))
                    errors.Add("Main base buildingRadius, when set, must be finite, positive, and at least attackRadius (the core sits inside the building): " + b.id);
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
                if(!Finite(c.y)||c.y<0)errors.Add("Invalid capture height: "+c.id);
                if (!Positive(c.radius)) errors.Add("Capture radius must be finite and positive: " + c.id);
            }
            foreach (var o in map.obstacles ?? Array.Empty<ObstacleDefinition>())
            {
                if (o == null) { errors.Add("Null obstacle."); continue; } CheckId(o.id, ids, errors);
                if (!Finite(o.x) || !Finite(o.y) || !Finite(o.z) || !Positive(o.width) || !Positive(o.height) || !Positive(o.depth)
                    || o.y - o.height / 2 < -0.001f || Math.Abs(o.x) + o.width / 2 > map.halfWidth || Math.Abs(o.z) + o.depth / 2 > map.halfDepth)
                    errors.Add("Obstacle volume is invalid or outside map: " + o.id);
                if (map.mainBases != null) foreach (var b in map.mainBases)
                    if (b != null && o.y+o.height/2>b.SpawnY+0.001f && o.y-o.height/2<b.SpawnY+2.5f && MapGeometry.CircleOverlaps(o, b.SpawnX, b.SpawnZ, b.spawnRadius)) errors.Add("Obstacle overlaps a base spawn area: " + o.id);
            }
            var nodes=map.navigationNodes??Array.Empty<NavigationNodeDefinition>();
            for(int i=0;i<nodes.Length;i++)
            {
                var n=nodes[i];if(n==null){errors.Add("Null navigation node.");continue;}CheckId(n.id,ids,errors);CheckPosition(map,n.id,n.x,n.z,errors);
                if(!Finite(n.y)||n.y<0||n.links==null)errors.Add("Invalid navigation height/links: "+n.id);
                else foreach(int link in n.links)if(link<0||link>=nodes.Length||link==i)errors.Add("Invalid navigation link: "+n.id);
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
