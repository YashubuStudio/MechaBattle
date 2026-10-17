using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using MechaBattle.Contracts.Construction;
using MechaBattle.Construction;
using MechaBattle.EditorTools.Models;

namespace MechaBattle.Tests.Models
{
    public sealed class ModularMechaTests
    {
        [Serializable] private sealed class Root { public Prototype constructionPrototype = new Prototype(); }
        [Serializable] private sealed class Prototype { public ConstructionCatalog catalog = new ConstructionCatalog(); }
        private static ConstructionCatalog ReadCatalog()
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            return JsonUtility.FromJson<Root>(File.ReadAllText(Path.Combine(project, "config/prototype-defaults.json"))).constructionPrototype.catalog;
        }
        [Test]
        public void ModularBuild_ProducesSharedPersistentAssetsWithoutTouchingOpenScenes()
        {
            int count = SceneManager.sceneCount;
            var scenes = new Scene[count]; var dirty = new bool[count];
            for (int i = 0; i < count; i++) { scenes[i] = SceneManager.GetSceneAt(i); dirty[i] = scenes[i].isDirty; }
            SimpleMechaPrefabBuilder.Generate();
            string bluePath = SimpleMechaPrefabBuilder.OutputFolder + "/MB_Worker01_standard-carbine_Blue.prefab";
            var blue = AssetDatabase.LoadAssetAtPath<GameObject>(bluePath);
            var orange = AssetDatabase.LoadAssetAtPath<GameObject>(SimpleMechaPrefabBuilder.OutputFolder + "/MB_Worker01_standard-carbine_Orange.prefab");
            Assert.That(blue, Is.Not.Null); Assert.That(orange, Is.Not.Null);
            Assert.That(blue.GetComponentsInChildren<Renderer>(true).Length, Is.EqualTo(1));
            Assert.That(blue.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(0));
            var mesh = blue.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(mesh.subMeshCount, Is.EqualTo(3));
            Assert.That(mesh.triangles.Length / 3, Is.EqualTo(1676));
            Assert.That(mesh.bounds.max.y, Is.EqualTo(2.47f).Within(0.0001f));
            Assert.That(orange.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh));
            var bm = blue.GetComponent<MeshRenderer>().sharedMaterials;
            var om = orange.GetComponent<MeshRenderer>().sharedMaterials;
            Assert.That(bm[0], Is.Not.SameAs(om[0]));
            Assert.That(bm[1], Is.SameAs(om[1])); Assert.That(bm[2], Is.Not.SameAs(om[2]));
            BuildAppearance.TeamTint(0,out float br,out float bg,out float bb);BuildAppearance.TeamTint(1,out float or,out float og,out float ob);
            Assert.That(bm[2].GetColor("_BaseColor"),Is.EqualTo(new Color(br,bg,bb)));
            Assert.That(om[2].GetColor("_BaseColor"),Is.EqualTo(new Color(or,og,ob)));
            var muzzle = blue.transform.Find("frame.standard.v1/arm.right/arm.standard.right/weapon.primary/weapon.carbine/muzzle");
            Assert.That(muzzle, Is.Not.Null);
            Assert.That(Vector3.Distance(muzzle.position, new Vector3(0.65f, 1.27f, 0.57f)), Is.LessThan(0.0001f));
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(mesh));
            SimpleMechaPrefabBuilder.Generate();
            Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(mesh)), Is.EqualTo(guid));
            Assert.That(SceneManager.sceneCount, Is.EqualTo(count));
            for (int i = 0; i < count; i++)
            {
                Assert.That(SceneManager.GetSceneAt(i).handle, Is.EqualTo(scenes[i].handle));
                Assert.That(SceneManager.GetSceneAt(i).isDirty, Is.EqualTo(dirty[i]));
            }
        }
        [Test]
        public void ModularBuild_UnityJsonCatalogAndFourSlotProfileRoundTrip()
        {
            var catalog = ReadCatalog();
            var deck = DeckConstruction.CreateDefault(catalog, 4);
            var restored = JsonUtility.FromJson<SavedDeck>(JsonUtility.ToJson(deck));
            Assert.That(DeckConstruction.Validate(catalog, restored, 4).Length, Is.EqualTo(4));
            Assert.That(catalog.presets.Length, Is.EqualTo(10));
            var utilities = DeckConstruction.Copy(catalog.presets[0]);
            utilities.equipment[2].partId = "weapon.mine";
            utilities.equipment[3].partId = "weapon.grenade";
            utilities.equipment[4].partId = "weapon.repairpod";
            var mixed = ConstructionEvaluator.Evaluate(catalog, JsonUtility.FromJson<BuildDefinition>(JsonUtility.ToJson(utilities))).CombatLoadout;
            Assert.That(mixed.GetWeapon(0).Kind, Is.EqualTo(MechaBattle.Contracts.WeaponKind.Mine));
            Assert.That(mixed.GetWeapon(1).Kind, Is.EqualTo(MechaBattle.Contracts.WeaponKind.Grenade));
            Assert.That(mixed.GetWeapon(2).UtilityDuration, Is.EqualTo(180));
            for (int i = 0; i < catalog.presets.Length; i++)
            {
                var combat = ConstructionEvaluator.Evaluate(catalog, catalog.presets[i]).CombatLoadout;
                // i<5: carbine/longrifle/cannon/charge/beam map 1:1 to the FireMode enum order.
                // i==5,6: bazooka and heavy-cannon both happen to use a SingleShot (cannon-family)
                // weapon. i==7: the new assaultrifle preset is FullAutomatic, like the carbine.
                Assert.That(combat.FireMode, Is.EqualTo(i < 5 ? (MechaBattle.Contracts.WeaponFireMode)i
                    : i <= 6 ? MechaBattle.Contracts.WeaponFireMode.SingleShot : MechaBattle.Contracts.WeaponFireMode.FullAutomatic));
                Assert.That(combat.MagazineCapacity, Is.GreaterThan(0)); Assert.That(combat.FireDownPoints, Is.GreaterThan(0));
                var frame=Array.Find(catalog.frames,f=>f.id==catalog.presets[i].frameId);float expectedDp=frame.downPointCapacity;foreach(var e in catalog.presets[i].equipment)expectedDp+=Array.Find(catalog.parts,p=>p.id==e.partId).downPointCapacityDelta;
                Assert.That(combat.DownPointCapacity, Is.EqualTo(expectedDp));
                Assert.That(combat.WeaponCount, Is.EqualTo(3));
                for(int wi=1;wi<3;wi++){var id=Array.Find(catalog.presets[i].equipment,e=>(int)e.slot==wi+2).partId;var expected=Array.Find(catalog.parts,p=>p.id==id).weapon.kind;Assert.That(combat.GetWeapon(wi).Kind,Is.EqualTo(expected));}
                Assert.That(combat.RedLockRange, Is.LessThan(combat.FireRange));
                if (i == 5) { Assert.That(combat.BlastRadius, Is.EqualTo(5)); Assert.That(combat.ProjectileSpeed, Is.EqualTo(50)); }
            }
        }
        [Test]
        public void ModularBuild_OrdinarySavedBuildUsesMatchingArmsAndOverridesTeamPaint()
        {
            var catalog = ReadCatalog();
            var build = JsonUtility.FromJson<BuildDefinition>(JsonUtility.ToJson(catalog.presets[0]));
            build.id = "matched-arms-test"; build.appearance.paintId = "Orange";
            build.equipment[0].partId = "arm.reinforced.left";
            Assert.That(ConstructionEvaluator.TryEvaluate(catalog,build,out var invalid,out var errors),Is.False);
            Assert.That(string.Join(";",errors),Does.Contain("ARM_FAMILY_MISMATCH"));
            build.equipment[1].partId = "arm.reinforced.right";
            var resolved = ConstructionEvaluator.Evaluate(catalog, build);
            Assert.That(resolved.MaxHealth, Is.EqualTo(3200));
            string output = SimpleMechaPrefabBuilder.GenerateBuild(catalog, build, "Blue");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(output);
            Assert.That(prefab.transform.Find("frame.standard.v1/arm.left/arm.reinforced.left"), Is.Not.Null);
            Assert.That(prefab.transform.Find("frame.standard.v1/arm.right/arm.reinforced.right"), Is.Not.Null);
            Assert.That(prefab.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3, Is.EqualTo(1940));
            Assert.That(prefab.GetComponent<MeshRenderer>().sharedMaterials[0].name, Does.EndWith("Armor_Blue"));
            var restored = JsonUtility.FromJson<BuildDefinition>(JsonUtility.ToJson(build));
            Assert.That(ConstructionEvaluator.Evaluate(catalog, restored).Cost, Is.EqualTo(resolved.Cost));
        }
        [TestCase("White")] [TestCase("Sand")] [TestCase("Olive")] [TestCase("Graphite")] [TestCase("Red")] [TestCase("Cobalt")]
        public void ModularBuild_CustomPaintIsAcceptedWithoutChangingGeometry(string paint)
        {
            var catalog=ReadCatalog();var build=DeckConstruction.Copy(catalog.presets[0],"paint-test-"+paint);build.appearance.paintId=paint;
            var output=SimpleMechaPrefabBuilder.GenerateBuild(catalog,build);var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(output);
            var material=prefab.GetComponent<MeshRenderer>().sharedMaterials[0];BuildAppearance.Tint(BuildAppearance.PaintIndex(paint),out float r,out float g,out float b);
            Assert.That(material.GetColor("_BaseColor"),Is.EqualTo(new Color(r,g,b)));Assert.That(prefab.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3,Is.EqualTo(1676));
            foreach(var team in new[]{"Blue","Orange"})
            {
                var path=SimpleMechaPrefabBuilder.GenerateBuild(catalog,build,team);var marked=AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<MeshRenderer>().sharedMaterials;
                Assert.That(marked[0].GetColor("_BaseColor"),Is.EqualTo(new Color(r,g,b)),"Team selection must preserve the chosen hull paint.");
                BuildAppearance.TeamTint(team=="Blue"?0:1,out float tr,out float tg,out float tb);
                Assert.That(marked[2].GetColor("_BaseColor"),Is.EqualTo(new Color(tr,tg,tb)));
            }
        }
        [Test]
        public void ModularBuild_FrameChangesSurviveUnityJsonAndAssembleBothFrameModels()
        {
            var catalog=ReadCatalog();var source=DeckConstruction.Copy(catalog.presets[7],"frame-selection-test");
            foreach(var frame in catalog.frames)
            {
                Assert.That(FrameConstruction.TryChangeFrame(catalog,source,frame.id,out var changed,out var replacements,out var errors),Is.True,string.Join(";",errors));
                changed.id="frame-selection-test-"+frame.id;
                var restored=JsonUtility.FromJson<BuildDefinition>(JsonUtility.ToJson(changed));
                var resolved=ConstructionEvaluator.Evaluate(catalog,restored);
                Assert.That(resolved.FrameId,Is.EqualTo(frame.id));Assert.That(resolved.MaxHealth,Is.EqualTo(frame.maxHealth));
                string output=SimpleMechaPrefabBuilder.GenerateBuild(catalog,restored,"Blue");
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(output);
                Assert.That(prefab.transform.Find(frame.id),Is.Not.Null);
                Assert.That(prefab.GetComponent<MeshFilter>().sharedMesh.vertexCount,Is.GreaterThan(0));
                if(frame.id=="frame.heavy.v1")Assert.That(resolved.WeaponPartId(0),Is.EqualTo("weapon.cannon"));
            }
            Assert.That(source.frameId,Is.EqualTo("frame.standard.v1"));
        }
    }
}
