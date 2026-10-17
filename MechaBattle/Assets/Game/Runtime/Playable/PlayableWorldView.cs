using System;
using System.Collections.Generic;
using MechaBattle.Battlefield;
using MechaBattle.Construction;
using MechaBattle.Contracts;
using MechaBattle.Simulation;
using TMPro;
using UnityEngine;

namespace MechaBattle.Playable
{
    internal sealed class PlayableWorldView : IDisposable
    {
        public readonly GameObject Root;
        public readonly Transform[] Units = new Transform[MatchLimits.TotalControllerSlots];
        private readonly GameObject[] visuals = new GameObject[MatchLimits.TotalControllerSlots];
        private readonly int[] weaponIndices = new int[MatchLimits.TotalControllerSlots];
        private int hiddenSlot = -1, hiddenUnitId, hiddenWeapon;
        private readonly int[] ids = new int[MatchLimits.TotalControllerSlots];
        private readonly long[] lastShots = new long[MatchLimits.TotalControllerSlots];
        private readonly ResolvedBuild[][] builds;
        private readonly MechaMeshLibrary library;
        private readonly BotSquadCoordinator squads;
        private readonly List<Material> materials = new List<Material>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<Transform> labels = new List<Transform>();
        private readonly Renderer[] relays;private readonly Renderer[] baseCores;private readonly TMP_Text[] baseHealth;
        private readonly LineRenderer[] basePulses;
        private readonly long[] baseShotTicks;
        private readonly float[] basePulseUntil;
        private readonly Material[] teamMaterials;
        private readonly LineRenderer[] trails = new LineRenderer[48];
        private readonly float[] trailUntil = new float[48];
        private int nextTrail;
        private readonly Camera camera;
        private readonly Material[] shotMaterials;
        private readonly CombatWorldView combat;

        public PlayableWorldView(MapDefinition map, BattleSimulation world, ResolvedBuild[][] builds,
            MechaMeshLibrary library, Camera camera, TMP_FontAsset font, Shader lit, Shader unlit, float legacyDefenseRadius, string theme = "industrial", BotSquadCoordinator squads = null)
        {
            this.builds = builds; this.library = library; this.camera = camera;this.squads=squads;
            Root = new GameObject("PlayableBattlefield");
            bool canyon=theme=="canyon",works=theme=="works",urban=theme=="urban",yard=map.id=="reclamation-yard";
            Material ground = NewMaterial(lit, "Ground", canyon?new Color(.26f,.20f,.14f):works?new Color(.12f,.15f,.19f):new Color(0.10f, 0.145f, 0.16f));
            Material structure = NewMaterial(lit, "Structure", new Color(0.2f, 0.25f, 0.28f), 0.4f);
            Material neutral = NewMaterial(lit, "NeutralRelay", new Color(0.48f, 0.54f, 0.47f));
            teamMaterials = new[] { NewMaterial(lit, "Blue", new Color(0.16f, 0.37f, 0.85f)), NewMaterial(lit, "Orange", new Color(0.95f, 0.40f, 0.10f)), neutral };
            shotMaterials = new[] { NewMaterial(unlit, "BlueTrace", new Color(0.3f, 0.75f, 1)), NewMaterial(unlit, "OrangeTrace", new Color(1, 0.65f, 0.2f)) };
            Primitive("Ground", PrimitiveType.Plane, Vector3.zero, new Vector3(map.halfWidth / 5, 1, map.halfDepth / 5), ground);
            var coverMaterial = NewMaterial(lit,"SolidCover",canyon?new Color(.46f,.32f,.20f):works?new Color(.33f,.37f,.44f):new Color(0.26f,0.29f,0.32f),0.3f);
            var stepMaterial = NewMaterial(lit,"TraversalSteps",canyon?new Color(.40f,.31f,.21f):works?new Color(.23f,.36f,.40f):new Color(0.25f,0.40f,0.38f),0.25f);
            var concrete=urban?NewMaterial(lit,"UrbanConcrete",new Color(.48f,.48f,.43f),.15f):coverMaterial;
            var floors=urban?NewMaterial(lit,"UrbanFloors",new Color(.22f,.28f,.32f),.3f):stepMaterial;
            var stairs=urban||yard?NewMaterial(lit,"UrbanAccess",new Color(.68f,.46f,.16f),.2f):stepMaterial;
            var scrap=yard?NewMaterial(lit,"RecycledSteel",new Color(.48f,.26f,.13f),.35f):coverMaterial;
            var factory=yard?NewMaterial(lit,"FactoryWall",new Color(.43f,.48f,.51f),.25f):coverMaterial;
            var service=yard?NewMaterial(lit,"LowerService",new Color(.19f,.32f,.30f),.2f):coverMaterial;
            foreach(var o in map.obstacles ?? Array.Empty<ObstacleDefinition>())
            {
                var material=urban?(o.id.Contains("stair")||o.id.Contains("landing")||o.id.Contains("balcony")?stairs:o.platform?floors:concrete):o.platform?stepMaterial:coverMaterial;
                if(yard)material=o.id.StartsWith("Factory",StringComparison.Ordinal)?factory:o.id.StartsWith("Scrap",StringComparison.Ordinal)?scrap:o.id.StartsWith("Service",StringComparison.Ordinal)?service:o.id.StartsWith("YardAccess",StringComparison.Ordinal)?stairs:stepMaterial;
                Primitive("Obstacle_"+o.id,PrimitiveType.Cube,new Vector3(o.x,o.y,o.z),new Vector3(o.width,o.height,o.depth),material);
            }
            if(urban)
            {
                var road=NewMaterial(lit,"UrbanRoad",new Color(.075f,.085f,.10f));
                Primitive("CentralBoulevard",PrimitiveType.Cube,new Vector3(0,.012f,0),new Vector3(map.halfWidth*2,.02f,38),road);
                foreach(float x in new[]{-115f,115f})Primitive("CrossStreet",PrimitiveType.Cube,new Vector3(x,.012f,0),new Vector3(22,.02f,330),road);
                var lanePaint=NewMaterial(unlit,"RoadMarkings",new Color(.8f,.73f,.42f));
                for(float x=-250;x<=250;x+=24)Primitive("LaneMark",PrimitiveType.Cube,new Vector3(x,.03f,0),new Vector3(8,.02f,.25f),lanePaint);
            }
            if(map.surfaceY>0&&map.navigationNodes!=null)
            {
                int lightIndex=0;foreach(var node in map.navigationNodes)if(node.y==0&&node.id.StartsWith("nav-under-",StringComparison.Ordinal)&&lightIndex++%2==0)
                {var lamp=new GameObject("UnderpassLight",typeof(Light));lamp.transform.SetParent(Root.transform,false);lamp.transform.position=new Vector3(node.x,6,node.z);var light=lamp.GetComponent<Light>();light.type=LightType.Point;light.range=72;light.intensity=3;light.color=new Color(.65f,.8f,1);light.shadows=LightShadows.None;}
            }
            var grid = new List<Vector3>();
            for (float x = -map.halfWidth; x <= map.halfWidth; x += 20) { grid.Add(new Vector3(x, 0.015f, -map.halfDepth)); grid.Add(new Vector3(x, 0.015f, map.halfDepth)); }
            for (float z = -map.halfDepth; z <= map.halfDepth; z += 20) { grid.Add(new Vector3(-map.halfWidth, 0.015f, z)); grid.Add(new Vector3(map.halfWidth, 0.015f, z)); }
            var gridMesh = new Mesh { name = "GroundGrid" }; gridMesh.SetVertices(grid); var index = new int[grid.Count]; for (int i = 0; i < index.Length; i++) index[i] = i;
            gridMesh.SetIndices(index, MeshTopology.Lines, 0); gridMesh.RecalculateBounds(); meshes.Add(gridMesh);
            var gridGo = new GameObject("Grid", typeof(MeshFilter), typeof(MeshRenderer)); gridGo.transform.SetParent(Root.transform, false);
            gridGo.GetComponent<MeshFilter>().sharedMesh = gridMesh; gridGo.GetComponent<MeshRenderer>().sharedMaterial = NewMaterial(unlit, "Grid", new Color(0.2f, 0.29f, 0.3f));
            baseCores=new Renderer[map.mainBases.Length];baseHealth=new TMP_Text[map.mainBases.Length];
            basePulses = new LineRenderer[map.mainBases.Length]; baseShotTicks = new long[map.mainBases.Length]; basePulseUntil = new float[map.mainBases.Length];
            int baseIndex = 0;
            foreach (var b in map.mainBases)
            {
                var color = teamMaterials[b.team]; var center = new Vector3(b.x, b.y, b.z);
                Primitive("BasePlatform_" + b.id, PrimitiveType.Cylinder, center + Vector3.up * 0.03f, new Vector3(b.attackRadius * 2, 0.03f, b.attackRadius * 2), structure);
                baseCores[baseIndex]=Primitive("BaseCore_" + b.id, PrimitiveType.Cylinder, center + Vector3.up * 1.8f, new Vector3(b.attackRadius*2, 1.8f, b.attackRadius*2), color).GetComponent<Renderer>();
                Primitive("BaseTower_" + b.id, PrimitiveType.Cube, center + Vector3.up * 10, new Vector3(2, 8, 2), structure);
                Primitive("BaseBeacon_" + b.id, PrimitiveType.Sphere, center + Vector3.up * 14, Vector3.one * 2.5f, color);
                float building = b.buildingRadius > 0 ? b.buildingRadius : legacyDefenseRadius;
                Ring(center, building, color);
                if (b.buildingRadius > 0)
                {
                    for (int side = 0; side < 8; side++)
                    {
                        float a = side * Mathf.PI / 4; Vector3 p = center + new Vector3(Mathf.Cos(a) * building, 3.5f, Mathf.Sin(a) * building);
                        Primitive("HangarPillar", PrimitiveType.Cube, p, new Vector3(0.6f, 7, 0.6f), structure);
                    }
                    Ring(center + Vector3.up * 7, building, structure);
                }
                basePulses[baseIndex] = Ring(center + Vector3.up * 1.3f, building, shotMaterials[b.team]);
                basePulses[baseIndex].widthMultiplier = 0.35f; basePulses[baseIndex].enabled = false; baseShotTicks[baseIndex] = -1; baseIndex++;
                baseHealth[baseIndex-1]=WorldLabel(map.MainBaseName(baseIndex-1)+" 本拠点", center + new Vector3(0, map.surfaceY>0&&b.y<map.surfaceY?7:17, 0), font, 3.5f);
            }
            relays = new Renderer[map.capturePoints.Length];
            for (int i = 0; i < relays.Length; i++)
            {
                var c = map.capturePoints[i]; float surface=map.surfaceY>0?c.y:world.GetSurfaceHeight(c.x,c.z);
                var disk = Primitive("Relay_" + c.id, PrimitiveType.Cylinder, new Vector3(c.x, surface+0.06f, c.z), new Vector3(c.radius * 2, 0.05f, c.radius * 2), neutral);
                relays[i] = disk.GetComponent<Renderer>(); Ring(new Vector3(c.x, surface+0.08f, c.z), c.radius, neutral);
                Primitive("RelayBeacon_" + c.id, PrimitiveType.Cube, new Vector3(c.x, surface+2, c.z), new Vector3(0.5f, 4, 0.5f), neutral);
                WorldLabel(c.id + " 中継", new Vector3(c.x, surface+6, c.z), font, 2.3f);
            }
            for (int i = 0; i < Units.Length; i++)
            {
                Units[i] = new GameObject("Slot_" + i.ToString("D3")).transform; Units[i].SetParent(Root.transform, false); ids[i] = -1; lastShots[i] = -1;
            }
            for (int i = 0; i < trails.Length; i++)
            {
                var go = new GameObject("ShotTrace_" + i, typeof(LineRenderer)); go.transform.SetParent(Root.transform, false);
                trails[i] = go.GetComponent<LineRenderer>(); trails[i].positionCount = 2; trails[i].useWorldSpace = true;
                trails[i].startWidth = 0.055f; trails[i].endWidth = 0.025f; trails[i].enabled = false;
                trails[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; trails[i].receiveShadows = false;
            }
            combat = new CombatWorldView(Root.transform, Units, world, teamMaterials, shotMaterials, NewMaterial(unlit, "ShieldHitFlash", Color.white), NewMaterial(unlit,"RepairBeam",new Color(0.2f,1,0.6f)),library);
            Sync(world);
        }
        public void Sync(BattleSimulation world)
        {
            for (int i = 0; i < Units.Length; i++)
            {
                var unit = world.GetUnit(i);
                if (unit.IsAlive && (ids[i] != unit.UnitId || weaponIndices[i] != unit.ActiveWeaponIndex))
                {
                    if (visuals[i] != null) UnityEngine.Object.Destroy(visuals[i]);
                    visuals[i] = library.Create(builds[i][unit.DeckIndex], unit.Team, Units[i], true, unit.ActiveWeaponIndex, squads!=null&&squads.IsLeader(i));
                    ids[i] = unit.UnitId; weaponIndices[i] = unit.ActiveWeaponIndex; lastShots[i] = unit.LastShotTick;
                }
                Units[i].gameObject.SetActive(unit.IsAlive);
                Units[i].position = new Vector3(unit.X, unit.Y, unit.Z); Units[i].rotation = Quaternion.Euler(0, unit.AimYaw, 0);
                if (unit.IsAlive && world.GetActiveWeapon(i).Kind == WeaponKind.Ranged && world.GetActiveWeapon(i).FireMode != WeaponFireMode.ContinuousBeam && world.GetActiveWeapon(i).ProjectileSpeed <= 0 && unit.LastShotTick > lastShots[i])
                {
                    lastShots[i] = unit.LastShotTick;
                    if ((camera.transform.position - Units[i].position).sqrMagnitude < 180 * 180)
                    {
                        int t = nextTrail++ % trails.Length;
                        trails[t].sharedMaterial = shotMaterials[unit.Team]; trails[t].SetPosition(0, new Vector3(unit.X, unit.Y + world.ShotHeight, unit.Z));
                        trails[t].SetPosition(1, new Vector3(unit.ShotEndX, unit.ShotEndY, unit.ShotEndZ)); trails[t].enabled = true; trailUntil[t] = Time.unscaledTime + 0.09f;
                    }
                }
            }
            combat.Sync(world, visuals);
            for (int i = 0; i < basePulses.Length; i++)
            {
                var b=world.GetBase(i);long shot = b.LastCounterShotTick;
                baseHealth[i].text=b.Name+(b.Destroyed?" 本拠点 陥落":b.MaxHealth>0?"  HP "+b.Health.ToString("0")+"/"+b.MaxHealth.ToString("0"):" 本拠点");
                baseCores[i].sharedMaterial=teamMaterials[b.Destroyed?2:b.Team];
                if (shot > baseShotTicks[i]) { baseShotTicks[i] = shot; basePulses[i].enabled = true; basePulseUntil[i] = Time.unscaledTime + 0.3f; }
            }
            for (int i = 0; i < relays.Length; i++)
            {
                int owner = world.GetCapturePoint(i).OwnerTeam; relays[i].sharedMaterial = teamMaterials[owner < 0 ? 2 : owner];
            }
        }
        public void SetLocalBodyVisible(int slot, bool visible)
        {
            if(visible)
            {
                if(hiddenSlot>=0) foreach(var renderer in Units[hiddenSlot].GetComponentsInChildren<Renderer>(true)) renderer.enabled=true;
                hiddenSlot=-1; return;
            }
            if(hiddenSlot==slot && hiddenUnitId==ids[slot] && hiddenWeapon==weaponIndices[slot]) return;
            hiddenSlot=slot; hiddenUnitId=ids[slot]; hiddenWeapon=weaponIndices[slot];
            foreach(var renderer in Units[slot].GetComponentsInChildren<Renderer>(true)) renderer.enabled=false;
        }
        public void LateUpdate()
        {
            foreach (var label in labels) label.rotation = camera.transform.rotation;
            for (int i = 0; i < basePulses.Length; i++) if (basePulses[i].enabled && Time.unscaledTime > basePulseUntil[i]) basePulses[i].enabled = false;
            for (int i = 0; i < trails.Length; i++) if (trails[i].enabled && Time.unscaledTime > trailUntil[i]) trails[i].enabled = false;
        }
        private GameObject Primitive(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(Root.transform, false);
            go.transform.position = position; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.Destroy(go.GetComponent<Collider>()); return go;
        }
        private LineRenderer Ring(Vector3 center, float radius, Material material)
        {
            var go = new GameObject("ZoneRing", typeof(LineRenderer)); go.transform.SetParent(Root.transform, false);
            var line = go.GetComponent<LineRenderer>(); line.sharedMaterial = material; line.loop = true; line.positionCount = 64; line.widthMultiplier = 0.16f;
            for (int i = 0; i < 64; i++) { float a = i * Mathf.PI * 2 / 64; line.SetPosition(i, center + new Vector3(Mathf.Cos(a) * radius, 0.04f, Mathf.Sin(a) * radius)); }
            return line;
        }
        private TMP_Text WorldLabel(string value, Vector3 position, TMP_FontAsset font, float width)
        {
            var go = new GameObject("ObjectiveLabel", typeof(TextMeshPro)); go.transform.SetParent(Root.transform, false); go.transform.position = position;
            var text = go.GetComponent<TextMeshPro>(); text.font = font; text.text = value; text.fontSize = 10; text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = new Vector2(36, 6); text.color = Color.white; go.transform.localScale = Vector3.one * width / 10;
            labels.Add(go.transform);return text;
        }
        private Material NewMaterial(Shader shader, string name, Color color, float metallic = 0)
        { var material = MechaMeshLibrary.Material(shader, name, color, metallic); materials.Add(material); return material; }
        public void Dispose()
        {
            if (Root != null) { Root.SetActive(false); UnityEngine.Object.Destroy(Root); }
            foreach (var material in materials) UnityEngine.Object.Destroy(material);
            foreach (var mesh in meshes) UnityEngine.Object.Destroy(mesh);
        }
    }
}
