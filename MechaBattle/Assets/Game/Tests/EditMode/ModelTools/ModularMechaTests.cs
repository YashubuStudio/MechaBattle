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
            Assert.That(bm[1], Is.SameAs(om[1])); Assert.That(bm[2], Is.SameAs(om[2]));
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
        public void ModularBuild_OrdinarySavedBuildCanMixArmTypesAndOverrideTeamPaint()
        {
            var catalog = ReadCatalog();
            var build = JsonUtility.FromJson<BuildDefinition>(JsonUtility.ToJson(catalog.presets[0]));
            build.id = "mixed-arms-test"; build.appearance.paintId = "Orange";
            build.equipment[0].partId = "arm.reinforced.left";
            var resolved = ConstructionEvaluator.Evaluate(catalog, build);
            Assert.That(resolved.MaxHealth, Is.EqualTo(120));
            string output = SimpleMechaPrefabBuilder.GenerateBuild(catalog, build, "Blue");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(output);
            Assert.That(prefab.transform.Find("frame.standard.v1/arm.left/arm.reinforced.left"), Is.Not.Null);
            Assert.That(prefab.transform.Find("frame.standard.v1/arm.right/arm.standard.right"), Is.Not.Null);
            Assert.That(prefab.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3, Is.EqualTo(1808));
            Assert.That(prefab.GetComponent<MeshRenderer>().sharedMaterials[0].name, Does.EndWith("Armor_Blue"));
            var restored = JsonUtility.FromJson<BuildDefinition>(JsonUtility.ToJson(build));
            Assert.That(ConstructionEvaluator.Evaluate(catalog, restored).Cost, Is.EqualTo(resolved.Cost));
        }
    }
}
