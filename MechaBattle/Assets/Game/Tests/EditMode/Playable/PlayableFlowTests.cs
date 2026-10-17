using System;
using System.Collections;
using System.Linq;
using System.IO;
using NUnit.Framework;
using MechaBattle.Construction;
using MechaBattle.Contracts;
using MechaBattle.Playable;
using MechaBattle.EditorTools;
using MechaBattle.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MechaBattle.Tests.Playable
{
    public sealed class PlayableFlowTests
    {
        private const string TestScene = "Assets/Game/Tests/Generated/PlayableFlow.unity";
        private const string TestConfig = "Assets/Game/Tests/Generated/PlayableFlowConfig.asset";
        private string originalScene;
        private bool generated;
        [UnityTest]
        public IEnumerator PreparationConstructionBattlePauseResultsAndReplaySurviveSceneReload()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) Assert.Ignore("Save open scenes to run scene-generating integration tests.");
            originalScene = SceneManager.GetActiveScene().path;
            PlayableSceneBuilder.Generate();
            if (!AssetDatabase.IsValidFolder("Assets/Game/Tests/Generated")) AssetDatabase.CreateFolder("Assets/Game/Tests", "Generated");
            var config = JsonUtility.FromJson<PlayableConfiguration>(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Game/Content/Playable/PrototypeConfig.json").text);
            config.p0Simulation.maxDurationSeconds = 0.5f;
            AssetDatabase.CreateAsset(new TextAsset(JsonUtility.ToJson(config)), TestConfig); generated = true;
            var entry = UnityEngine.Object.FindFirstObjectByType<PlayableEntry>();
            entry.Configure(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Game/Content/Playable/StandardMap.json"),
                AssetDatabase.LoadAssetAtPath<TextAsset>(TestConfig),
                AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Game/Content/Models/MB_Worker01/module-kit.json"),
                Shader.Find("Universal Render Pipeline/Lit"), Shader.Find("Universal Render Pipeline/Unlit"), Shader.Find("TextMeshPro/Mobile/Distance Field"), false);
            Assert.That(EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), TestScene), Is.True);
            EditorSceneManager.OpenScene(TestScene, OpenSceneMode.Single);
            yield return new EnterPlayMode();
            entry = UnityEngine.Object.FindFirstObjectByType<PlayableEntry>();
            Assert.That(entry, Is.Not.Null); Assert.That(entry.IsInPreparation, Is.True);
            Assert.That(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            var lockModeButton=entry.transform.Find("PlayableCanvas/MainMenu/LockModeSetting").GetComponent<UnityEngine.UI.Button>();
            Assert.That(entry.TargetLockMode,Is.EqualTo(LockSelectionMode.ReticleCenter));lockModeButton.onClick.Invoke();Assert.That(entry.TargetLockMode,Is.EqualTo(LockSelectionMode.NearestCycle));
            Assert.That(entry.transform.Find("PlayableCanvas/Pause/LockModeSetting").GetComponentInChildren<TMPro.TMP_Text>().text,Does.Contain("距離順切替"));
            entry.SetLockSelectionMode(LockSelectionMode.ReticleCenter);
            AssertFrontend(entry, "MainMenu", false);
            yield return AssertSettingsModal(entry, "MainMenu");
            Click(entry, "MainMenu/OpenGarage"); AssertFrontend(entry, "Preparation", true);
            yield return AssertSettingsModal(entry, "Preparation");
            yield return null;
            AssertPreviewPixels(entry);
            Click(entry, "Preparation/Hangar/Row/ReturnToMainMenu"); AssertFrontend(entry, "MainMenu", false);
            Click(entry, "MainMenu/OpenBattlePrep"); AssertFrontend(entry, "BattlePrep", true);
            yield return AssertSettingsModal(entry, "BattlePrep");
            entry.SelectDeckSlot(2); Assert.That(entry.SelectedDeckSlot, Is.EqualTo(2));
            Click(entry, "BattlePrep/PrepPanel/Row/OpenGarage"); AssertFrontend(entry, "Preparation", true);
            entry.SelectDeckSlot(0);
            Canvas.ForceUpdateCanvases();
            var canvas = entry.GetComponentInChildren<Canvas>(); var bounds = new Vector3[4]; canvas.GetComponent<RectTransform>().GetWorldCorners(bounds);
            foreach (var button in entry.GetComponentsInChildren<UnityEngine.UI.Button>())
            {
                var corners = new Vector3[4]; button.GetComponent<RectTransform>().GetWorldCorners(corners);
                Assert.That(corners[0].x, Is.GreaterThanOrEqualTo(bounds[0].x - 1), button.name);
                Assert.That(corners[0].y, Is.GreaterThanOrEqualTo(bounds[0].y - 1), button.name);
                Assert.That(corners[2].x, Is.LessThanOrEqualTo(bounds[2].x + 1), button.name);
                Assert.That(corners[2].y, Is.LessThanOrEqualTo(bounds[2].y + 1), button.name);
            }
            AssertFrameSelection(entry);
            AssertExpandedMechaChoices(entry,config.constructionPrototype.catalog);
            entry.OpenPartPicker(3);
            var picker=entry.transform.Find("PlayableCanvas/PartPicker");Assert.That(picker.gameObject.activeSelf,Is.True);
            PickerChoice(picker, "地雷").onClick.Invoke();
            Assert.That(picker.gameObject.activeSelf,Is.False);
            Assert.That(ConstructionEvaluator.Evaluate(config.constructionPrototype.catalog,entry.CopyProfile().deck[0]).CombatLoadout.GetWeapon(1).Kind,Is.EqualTo(WeaponKind.Mine));
            var modelLibrary=(MechaMeshLibrary)typeof(PlayableEntry).GetField("library",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(entry);
            foreach(var kind in new[]{WeaponKind.RepairPod,WeaponKind.Radar,WeaponKind.Mine})
            {var visual=modelLibrary.CreateDeployable(kind,0,entry.transform);Assert.That(visual.GetComponent<MeshFilter>().sharedMesh.vertexCount,Is.GreaterThan(0));Assert.That(visual.GetComponent<Collider>(),Is.Null);UnityEngine.Object.DestroyImmediate(visual);}
            var before = entry.CopyProfile().deck[0].equipment[0].partId;
            entry.CyclePart(0); Assert.That(entry.CopyProfile().deck[0].equipment[0].partId, Is.Not.EqualTo(before));
            for (int i = 0; i < config.constructionPrototype.catalog.presets.Length; i++)
            {
                entry.ApplyPreset(i); var choice = entry.CopyProfile().deck[0];
                Assert.That(ConstructionEvaluator.Evaluate(config.constructionPrototype.catalog, choice).CombatLoadout.MagazineCapacity, Is.GreaterThan(0));
                Assert.That(entry.GetComponentsInChildren<MeshRenderer>().Length, Is.GreaterThan(0), "Weapon preview must assemble.");
            }
            entry.OpenPartPicker(3);
            PickerChoice(picker, "HP回復FA").onClick.Invoke();
            Assert.That(ConstructionEvaluator.Evaluate(config.constructionPrototype.catalog,entry.CopyProfile().deck[0]).CombatLoadout.GetWeapon(1).Kind,Is.EqualTo(WeaponKind.RepairGun));
            entry.OpenPartPicker(4);
            Canvas.ForceUpdateCanvases();
            var radarChoice=PickerChoice(picker, "設置レーダー");
            var radarCorners=new Vector3[4];radarChoice.GetComponent<RectTransform>().GetWorldCorners(radarCorners);
            Assert.That(radarCorners[0].y,Is.GreaterThan(bounds[0].y));Assert.That(radarCorners[2].y,Is.LessThan(bounds[2].y));
            radarChoice.onClick.Invoke();
            var supportBuild=ConstructionEvaluator.Evaluate(config.constructionPrototype.catalog,entry.CopyProfile().deck[0]).CombatLoadout;
            Assert.That(supportBuild.GetWeapon(2).Kind,Is.EqualTo(WeaponKind.Radar));Assert.That(supportBuild.GetWeapon(2).AmmoResupplyDisabled,Is.True);
            entry.ApplyPreset(6);
            entry.SelectTeam(1); entry.SelectDeckSlot(1); entry.ApplyPreset(5);
            AssertFriendlySupportHud(entry);
            entry.ReturnToPreparation(); AssertFrontend(entry, "BattlePrep", true);
            AssertExpandedMapChoices(entry);
            var deployment = entry.transform.Find("PlayableCanvas/BattlePrep/DeploymentMap/MapField");
            Assert.That(deployment, Is.Not.Null);
            Assert.That(deployment.Find("MainBase0").GetComponent<UnityEngine.UI.Button>().interactable, Is.False);
            Assert.That(deployment.Find("Relay0").GetComponent<UnityEngine.UI.Button>().interactable, Is.False);
            deployment.Find("MainBase5").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(entry.transform.Find("PlayableCanvas/BattlePrep/SelectedSpawn").GetComponent<TMPro.TMP_Text>().text, Does.Contain("Orange-C"));
            Assert.That(deployment.Find("MainBase5").GetComponent<UnityEngine.UI.Outline>().enabled, Is.True);
            Canvas.ForceUpdateCanvases();
            foreach (var marker in deployment.GetComponentsInChildren<UnityEngine.UI.Button>())
            {
                var markerCorners = new Vector3[4]; var panelCorners = new Vector3[4];
                marker.GetComponent<RectTransform>().GetWorldCorners(markerCorners); ((RectTransform)deployment.parent).GetWorldCorners(panelCorners);
                Assert.That(markerCorners[0].x, Is.GreaterThan(panelCorners[0].x)); Assert.That(markerCorners[2].x, Is.LessThan(panelCorners[2].x));
                Assert.That(markerCorners[0].y, Is.GreaterThan(panelCorners[0].y)); Assert.That(markerCorners[2].y, Is.LessThan(panelCorners[2].y));
            }
            Click(entry, "BattlePrep/PrepPanel/Row/BeginMatch"); Assert.That(entry.Simulation.GetUnit(50).Z, Is.EqualTo(80).Within(12));
            Assert.That(entry.Simulation.AliveCount, Is.EqualTo(100)); Assert.That(entry.HumanSlotId, Is.EqualTo(50));
            AssertTacticalMapFlow(entry);
            Assert.That(entry.Simulation.GetUnit(50).Controller, Is.EqualTo(ControllerType.Human));
            var custom = entry.CopyProfile().deck[1];
            Assert.That(entry.Simulation.GetUnit(50).DeckIndex, Is.EqualTo(1));
            Assert.That(entry.transform.Find("Sun").gameObject.activeSelf, Is.True);
            Assert.That(Preview(entry).Visible, Is.False);
            Assert.That(entry.transform.Find("PlayerCamera").GetComponent<Camera>().cullingMask & HangarPreview.PreviewMask, Is.Zero);
            var expected = ConstructionEvaluator.Evaluate(config.constructionPrototype.catalog, custom);
            Assert.That(entry.Simulation.GetUnit(50).Health, Is.EqualTo(expected.MaxHealth));
            Assert.That(entry.Simulation.GetLoadout(50, 1).BlastRadius, Is.EqualTo(5));
            Assert.That(entry.transform.Find("PlayableCanvas/BattleHUD/CombatStatus"), Is.Not.Null);
            Assert.That(entry.transform.Find("PlayableCanvas/BattleHUD/LoadoutStatus"), Is.Not.Null);
            Assert.That(entry.transform.Find("PlayableCanvas/BattleHUD/LockStatus"), Is.Not.Null);
            var warning=entry.transform.Find("PlayableCanvas/BattleHUD/IncomingLockPanel");
            Assert.That(warning,Is.Not.Null);Assert.That(warning.gameObject.activeSelf,Is.False);
            Assert.That(warning.GetComponent<UnityEngine.UI.Image>().raycastTarget,Is.False);
            Assert.That(warning.Find("IncomingLockWarning").GetComponent<TMPro.TMP_Text>().raycastTarget,Is.False);
            foreach(var name in new[]{"ConfirmedHitMarker","ConfirmedHitAmount","ConfirmedKillNotice","RecoveryNotice"})
            {var element=entry.transform.Find("PlayableCanvas/BattleHUD/"+name);Assert.That(element,Is.Not.Null);Assert.That(element.GetComponent<TMPro.TMP_Text>().raycastTarget,Is.False);}
            Assert.That(entry.CapturePointCount, Is.EqualTo(3));
            var map = JsonUtility.FromJson<MechaBattle.Battlefield.MapDefinition>(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Game/Content/Playable/StandardMap.json").text);
            Assert.That(map.obstacles.Length, Is.EqualTo(83));
            foreach (var obstacle in map.obstacles) Assert.That(GameObject.Find("Obstacle_" + obstacle.id), Is.Not.Null);
            AssertRedeployMap(entry, map);
            // Actual uGUI rebuild catches missing CanvasRenderer on dynamically created radar ranges.
            var radarConfig=JsonUtility.FromJson<PlayableConfiguration>(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Game/Content/Playable/PrototypeConfig.json").text);
            radarConfig.playablePrototype.Apply(radarConfig.p0Simulation);radarConfig.p0Simulation.maxDurationSeconds=10;
            var radarWeapon=new UnitLoadout("radar-ui-test",2400,900,12,2,1,3,0.3f,120,10,WeaponFireMode.SingleShot,1,0.5f,kind:WeaponKind.Radar,utilityRadius:90,utilityDuration:1,ammoResupplyDisabled:true);
            var radarDecks=new UnitLoadout[100][];for(int i=0;i<100;i++)radarDecks[i]=new[]{radarWeapon,radarWeapon,radarWeapon,radarWeapon};
            var radarWorld=new BattleSimulation(map,radarConfig.p0Simulation,50,radarDecks);
            var radarSelf=radarWorld.GetUnit(50);radarWorld.Submit(50,new UnitCommand(radarSelf.UnitId,radarWorld.CurrentTick,1,0,0,-90,0,false,true,false,false,false));radarWorld.Step();
            for(int i=0;i<39;i++)radarWorld.Step();
            var minimapField=typeof(PlayableEntry).GetField("minimap",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);var radarMap=minimapField.GetValue(entry);
            var updateMap=radarMap.GetType().GetMethod("Update");updateMap.Invoke(radarMap,new object[]{radarWorld,50,-1,0});Canvas.ForceUpdateCanvases();
            var radarRing=entry.transform.Find("PlayableCanvas/BattleHUD/Minimap/MapField/RadarCoverage/RadarRange0");Assert.That(radarRing,Is.Not.Null);Assert.That(radarRing.GetComponent<CanvasRenderer>(),Is.Not.Null);Assert.That(radarRing.gameObject.activeSelf,Is.True);
            radarWorld.Submit(50,new UnitCommand(radarSelf.UnitId,radarWorld.CurrentTick,2,0,0,-90,0,false,true,false,false,false));radarWorld.Step();updateMap.Invoke(radarMap,new object[]{radarWorld,50,-1,0});Canvas.ForceUpdateCanvases();
            for(int i=0;i<100;i++)radarWorld.Step();updateMap.Invoke(radarMap,new object[]{radarWorld,50,-1,0});Canvas.ForceUpdateCanvases();Assert.That(radarRing.gameObject.activeSelf,Is.False);LogAssert.NoUnexpectedReceived();
            entry.SelectWeapon(1); entry.AdvanceTick();
            Assert.That(entry.Simulation.GetActiveWeapon(50).Kind, Is.EqualTo(WeaponKind.Melee));
            entry.SelectWeapon(2); entry.AdvanceTick();
            Assert.That(entry.Simulation.GetActiveWeapon(50).Kind, Is.EqualTo(WeaponKind.RepairPod));
            entry.SelectWeapon(0); entry.AdvanceTick();
            Assert.That(entry.Simulation.GetActiveWeapon(50).BlastRadius, Is.EqualTo(5));
            var heavyLeg=GameObject.Find("Slot_006/Mecha_heavy-cannon/LeftLeg");Assert.That(heavyLeg,Is.Not.Null);Assert.That(heavyLeg.GetComponent<MeshFilter>().sharedMesh.vertexCount,Is.GreaterThan(0));Assert.That(heavyLeg.transform.Find("Knee").GetComponent<MeshFilter>().sharedMesh.vertexCount,Is.GreaterThan(0));
            Assert.That(GameObject.Find("Slot_050/LeftArmShield"), Is.Not.Null);
            Assert.That(GameObject.Find("Slot_050/Mecha_player-2/RightArm"), Is.Not.Null);
            Assert.That(GameObject.Find("Slot_050/Mecha_player-2/LeftLeg/Knee"), Is.Not.Null);
            Assert.That(GameObject.Find("Slot_050/Mecha_player-2/RightLeg/Knee"), Is.Not.Null);
            entry.PauseMatch();
            yield return AssertSettingsModal(entry, "Pause"); long tick = entry.Simulation.CurrentTick; entry.AdvanceTick(); Assert.That(entry.Simulation.CurrentTick, Is.EqualTo(tick));
            entry.ResumeMatch();
            for (int i = 0; i < 30; i++) entry.AdvanceTick();
            Assert.That(entry.Simulation.Phase, Is.EqualTo(MatchPhase.Finished));
            Assert.That(entry.transform.Find("PlayableCanvas/Results").gameObject.activeSelf, Is.True);
            AssertPersonalResults(entry);
            entry.BeginMatch();
            var freshStats=entry.Simulation.GetSlotStats(entry.HumanSlotId);
            Assert.That(freshStats.DamageDealt+freshStats.DamageReceived+freshStats.HealingDealt+freshStats.HealingReceived+freshStats.CoreDamageDealt,Is.Zero); Assert.That(entry.Simulation.CurrentTick, Is.Zero); Assert.That(entry.Simulation.AliveCount, Is.EqualTo(100));
            entry.ReturnToPreparation(); Assert.That(entry.IsInPreparation, Is.True);
            Assert.That(entry.transform.Find("PlayableCanvas/BattlePrep").gameObject.activeSelf, Is.True);
            AssertFrontend(entry, "BattlePrep", true);
            entry.ReturnToGarage(); AssertFrontend(entry, "Preparation", true);
            Click(entry, "Preparation/Hangar/Row/ReturnToMainMenu"); AssertFrontend(entry, "MainMenu", false);
            LogAssert.NoUnexpectedReceived();
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator PortableNuclearAndReturnKeepTheirInputsAndReserveScreenConnected()
        {
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)Assert.Ignore("Save open scenes before generated scene integration tests.");
            originalScene=SceneManager.GetActiveScene().path;PlayableSceneBuilder.Generate();
            if(!AssetDatabase.IsValidFolder("Assets/Game/Tests/Generated"))AssetDatabase.CreateFolder("Assets/Game/Tests","Generated");
            var config=JsonUtility.FromJson<PlayableConfiguration>(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Game/Content/Playable/PrototypeConfig.json").text);
            config.p0Simulation.maxDurationSeconds=20;config.playablePrototype.emergencyReturnSeconds=.1f;config.playablePrototype.strategy.initialPoints=1000;
            AssetDatabase.CreateAsset(new TextAsset(JsonUtility.ToJson(config)),TestConfig);generated=true;
            var entry=UnityEngine.Object.FindFirstObjectByType<PlayableEntry>();
            entry.Configure(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Game/Content/Playable/StandardMap.json"),AssetDatabase.LoadAssetAtPath<TextAsset>(TestConfig),AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Game/Content/Models/MB_Worker01/module-kit.json"),Shader.Find("Universal Render Pipeline/Lit"),Shader.Find("Universal Render Pipeline/Unlit"),Shader.Find("TextMeshPro/Mobile/Distance Field"),false);
            Assert.That(EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),TestScene),Is.True);EditorSceneManager.OpenScene(TestScene,OpenSceneMode.Single);yield return new EnterPlayMode();
            entry=UnityEngine.Object.FindFirstObjectByType<PlayableEntry>();entry.ReturnToPreparation();entry.BeginMatch();var world=entry.Simulation;var initial=world.GetUnit(entry.HumanSlotId);
            entry.OpenTacticalMap();entry.SelectStrategy(StrategyKind.Nuclear);entry.SelectStrategyPoint(initial.X,initial.Z);entry.ConfirmStrategy();
            Assert.That(world.GetStrategy(0,0).NuclearPhase,Is.EqualTo(NuclearPhase.AwaitingPickup));entry.CloseTacticalMap();entry.InteractNuclear();entry.AdvanceTick();
            Assert.That(world.IsCarryingNuclear(0),Is.True);entry.RequestEmergencyReturn();entry.AdvanceTick();Assert.That(world.GetEmergencyReturn(0).Channeling,Is.False);
            yield return null;Assert.That(entry.transform.Find("StrategyEffects/StrategySignal0/PortableNuclearDevice/ReactorCase").gameObject.activeInHierarchy,Is.True);
            Assert.That(entry.transform.Find("PlayableCanvas/BattleHUD/ReturnAndCarryStatus").GetComponent<TMPro.TMP_Text>().text,Does.Contain("核運搬中"));
            entry.InteractNuclear();entry.AdvanceTick();entry.RequestEmergencyReturn();entry.AdvanceTick();
            Assert.That(world.GetUnit(0).Crouching&&world.GetUnit(0).ReturnChanneling,Is.True);
            var view=typeof(PlayableEntry).GetField("view",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(entry);
            var visuals=(GameObject[])view.GetType().GetField("visuals",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(view);
            for(int pose=0;pose<10;pose++)view.GetType().GetMethod("Sync").Invoke(view,new object[]{world});
            Assert.That(visuals[0].transform.localPosition.y,Is.LessThan(-.01f));Assert.That(visuals[0].transform.Find("LeftLeg/Knee").localEulerAngles.x,Is.GreaterThan(10));
            for(int i=0;i<6;i++)entry.AdvanceTick();
            Assert.That(world.GetEmergencyReturn(0).InReserve,Is.True);Assert.That(entry.transform.Find("PlayableCanvas/Redeploy").gameObject.activeSelf,Is.True);
            Assert.That(entry.transform.Find("PlayableCanvas/Redeploy/RedeployPanel").GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.text.Contains("帰投補給中")),Is.True);
            Assert.That(world.GetSlotStats(0).Deaths,Is.Zero);entry.Deploy();entry.AdvanceTick();Assert.That(world.GetUnit(0).IsAlive,Is.True);Assert.That(world.GetUnit(0).DeckIndex,Is.EqualTo(initial.DeckIndex));
            Canvas.ForceUpdateCanvases();LogAssert.NoUnexpectedReceived();yield return new ExitPlayMode();
        }
        private static void AssertTacticalMapFlow(PlayableEntry entry)
        {
            entry.OpenTacticalMap();entry.SelectStrategy(StrategyKind.Nuclear);entry.SelectStrategyPoint(0,0);
            Assert.That(entry.transform.Find("PlayableCanvas/TacticalMap/ConfirmStrategy").GetComponent<UnityEngine.UI.Button>().interactable,Is.False,"A map-designated nuclear strike must not be possible.");
            Assert.That(entry.IsTacticalMapOpen,Is.True);Assert.That(Cursor.lockState,Is.EqualTo(CursorLockMode.None));
            var panel=entry.transform.Find("PlayableCanvas/TacticalMap");Assert.That(panel,Is.Not.Null);
            panel.Find("StrategyWideRadar").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();entry.SelectStrategyPoint(0,0);
            var confirm=panel.Find("ConfirmStrategy").GetComponent<UnityEngine.UI.Button>();Assert.That(confirm.IsInteractable(),Is.True);
            int points=entry.Simulation.GetStrategyTeam(entry.Team).Points;confirm.onClick.Invoke();
            Assert.That(entry.Simulation.GetStrategyTeam(entry.Team).Points,Is.EqualTo(points-100));
            Assert.That(panel.Find("StrategyMessage").GetComponent<TMPro.TMP_Text>().text,Does.Contain("要請"));
            Canvas.ForceUpdateCanvases();foreach(var range in panel.GetComponentsInChildren<ReticleRingGraphic>(true))Assert.That(range.GetComponent<CanvasRenderer>(),Is.Not.Null);
            Assert.That(entry.transform.Find("PlayableCanvas/BattleHUD/StrategyStatus"),Is.Not.Null);
            long tick=entry.Simulation.CurrentTick;entry.AdvanceTick();Assert.That(entry.Simulation.CurrentTick,Is.EqualTo(tick+1),"The map must not pause the match.");
            entry.PauseMatch();Assert.That(entry.IsTacticalMapOpen,Is.False);entry.ResumeMatch();
            entry.OpenTacticalMap();entry.CloseTacticalMap();Assert.That(entry.IsTacticalMapOpen,Is.False);
        }
        private static void AssertExpandedMechaChoices(PlayableEntry entry,MechaBattle.Contracts.Construction.ConstructionCatalog catalog)
        {
            var picker=entry.transform.Find("PlayableCanvas/PartPicker");entry.OpenFramePicker();PickerChoice(picker,"軽量フレーム").onClick.Invoke();
            entry.OpenPartPicker(0);PickerChoice(picker,"軽量アーム").onClick.Invoke();entry.OpenPaintPicker();PickerChoice(picker,"サンド").onClick.Invoke();
            var build=entry.CopyProfile().deck[0];Assert.That(build.frameId,Is.EqualTo("frame.light.v1"));Assert.That(build.appearance.paintId,Is.EqualTo("Sand"));
            Assert.That(Array.Find(build.equipment,e=>(int)e.slot==1).partId,Is.EqualTo("arm.mobile.right"));
            var resolved=ConstructionEvaluator.Evaluate(catalog,build);Assert.That(resolved.MoveSpeed,Is.EqualTo(14.8f).Within(.001));Assert.That(resolved.MaxShield,Is.EqualTo(500));
            var preview=Preview(entry).Model.GetComponent<MeshRenderer>();BuildAppearance.Tint(BuildAppearance.PaintIndex("Sand"),out float r,out float g,out float b);Assert.That(preview.sharedMaterials[0].GetColor("_BaseColor"),Is.EqualTo(new Color(r,g,b)));
            var library=(MechaMeshLibrary)typeof(PlayableEntry).GetField("library",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(entry);
            var blue=library.Create(resolved,0,entry.transform);var orange=library.Create(resolved,1,entry.transform);
            Assert.That(blue.GetComponent<MeshFilter>().sharedMesh,Is.SameAs(orange.GetComponent<MeshFilter>().sharedMesh));
            Assert.That(blue.GetComponent<MeshRenderer>().sharedMaterials[2].GetColor("_BaseColor"),Is.Not.EqualTo(orange.GetComponent<MeshRenderer>().sharedMaterials[2].GetColor("_BaseColor")));
            var ordinaryBlue=library.Create(resolved,0,entry.transform,allowSpecialPaint:false);var ordinaryOrange=library.Create(resolved,1,entry.transform,allowSpecialPaint:false);
            Assert.That(blue.GetComponent<MeshRenderer>().sharedMaterials[0].name,Is.EqualTo("SandArmor"));
            Assert.That(ordinaryBlue.GetComponent<MeshRenderer>().sharedMaterials[0].name,Is.EqualTo("TeamArmor_0"));Assert.That(ordinaryOrange.GetComponent<MeshRenderer>().sharedMaterials[0].name,Is.EqualTo("TeamArmor_1"));
            Assert.That(build.appearance.paintId,Is.EqualTo("Sand"),"Battle restrictions must preserve saved garage paint.");
            UnityEngine.Object.DestroyImmediate(ordinaryBlue);UnityEngine.Object.DestroyImmediate(ordinaryOrange);
            UnityEngine.Object.DestroyImmediate(blue);UnityEngine.Object.DestroyImmediate(orange);entry.ApplyPreset(0);
        }
        private static void AssertExpandedMapChoices(PlayableEntry entry)
        {
            string[] titles={"荒地渓谷","立体市街・連絡橋","二層都心・地下回廊","再生工廠・三帯戦線"};string[] ids={"canyon-cross","urban-overpass","layered-city","reclamation-yard"};
            for(int i=0;i<ids.Length;i++)
            {
                Click(entry,"BattlePrep/SelectMap");var picker=entry.transform.Find("PlayableCanvas/PartPicker");PickerChoice(picker,titles[i]).onClick.Invoke();Assert.That(entry.SelectedMapId,Is.EqualTo(ids[i]));
                var mapPanel=entry.transform.Find("PlayableCanvas/BattlePrep/DeploymentMap");Assert.That(mapPanel.childCount,Is.EqualTo(2));Assert.That(mapPanel.Find("MapField/MainBase5"),Is.Not.Null);
                Assert.That(entry.transform.Find("PlayableCanvas/BattleHUD/Minimap").GetComponents<UnityEngine.UI.RectMask2D>().Length,Is.EqualTo(1));
                entry.BeginMatch();Assert.That(entry.Simulation.AliveCount,Is.EqualTo(100));
                var battleView=typeof(PlayableEntry).GetField("view",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(entry);
                var models=(GameObject[])battleView.GetType().GetField("visuals",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(battleView);
                for(int slot=0;slot<100;slot++)if(slot%50!=0&&slot%50!=4)foreach(var renderer in models[slot].GetComponentsInChildren<MeshRenderer>(true))Assert.That(renderer.sharedMaterials[0].name,Is.EqualTo("TeamArmor_"+(slot/50)),"Only appointed squad leaders may keep special armor paint.");
                entry.SelectMap("p0-three-lanes");Assert.That(entry.SelectedMapId,Is.EqualTo(ids[i]),"Map changes must be blocked during a match.");entry.ReturnToPreparation();
            }
            Click(entry,"BattlePrep/SelectMap");PickerChoice(entry.transform.Find("PlayableCanvas/PartPicker"),"荒地渓谷").onClick.Invoke();Assert.That(entry.SelectedMapId,Is.EqualTo("canyon-cross"));entry.SelectMap("terraced-works");Assert.That(entry.SelectedMapId,Is.EqualTo("canyon-cross"));
        }
        private static void AssertPersonalResults(PlayableEntry entry)
        {
            var stats=entry.Simulation.GetSlotStats(entry.HumanSlotId);
            var panel=entry.transform.Find("PlayableCanvas/Results/PersonalStats");Assert.That(panel,Is.Not.Null);
            string[] names={"DamageDealt","DamageReceived","HealingDealt","HealingReceived","CoreDamageDealt"};
            double[] expected={stats.DamageDealt,stats.DamageReceived,stats.HealingDealt,stats.HealingReceived,stats.CoreDamageDealt};
            Canvas.ForceUpdateCanvases();var bounds=new Vector3[4];((RectTransform)panel).GetWorldCorners(bounds);
            for(int i=0;i<names.Length;i++)
            {
                var value=panel.Find("Personal"+names[i]).GetComponent<TMPro.TMP_Text>();
                Assert.That(value.text,Is.EqualTo(expected[i].ToString("N0")),names[i]);Assert.That(value.raycastTarget,Is.False);
                var corners=new Vector3[4];value.rectTransform.GetWorldCorners(corners);
                Assert.That(corners[0].x,Is.GreaterThanOrEqualTo(bounds[0].x));Assert.That(corners[0].y,Is.GreaterThanOrEqualTo(bounds[0].y));
                Assert.That(corners[2].x,Is.LessThanOrEqualTo(bounds[2].x));Assert.That(corners[2].y,Is.LessThanOrEqualTo(bounds[2].y));
            }
            Assert.That(panel.Find("PersonalCombat").GetComponent<TMPro.TMP_Text>().text,Is.EqualTo("撃破 "+stats.Kills+"    被撃破 "+stats.Deaths));
        }
        private static void AssertFriendlySupportHud(PlayableEntry entry)
        {
            // Native-only coverage of eligibility -> manual selection -> existing Canvas presentation.
            entry.SelectPart(4, "weapon.repairgun"); entry.BeginMatch();
            entry.SelectWeapon(2); entry.AdvanceTick();
            var world = entry.Simulation; int human = entry.HumanSlotId; var self = world.GetUnit(human);
            Assert.That(world.GetActiveWeapon(human).Kind, Is.EqualTo(WeaponKind.RepairGun));
            Assert.That(world.GetFriendlyLockRange(human), Is.EqualTo(60));
            int target = -1; float farthest = 0;
            for (int i = 0; i < 100; i++)
            {
                if (!world.IsFriendlyYellowLocked(human, i)) continue;
                var ally = world.GetUnit(i); float dx = ally.X-self.X, dz = ally.Z-self.Z, distance = dx*dx+dz*dz;
                if (distance > farthest) { farthest = distance; target = i; }
            }
            Assert.That(target, Is.GreaterThanOrEqualTo(0), "The standard spawn must have a visible ally within support range.");
            var camera = entry.transform.Find("PlayerCamera").GetComponent<Camera>(); var t = world.GetUnit(target);
            camera.transform.position = new Vector3(self.X, self.Y+world.ShotHeight, self.Z);
            camera.transform.LookAt(new Vector3(t.X, t.Y+world.ShotHeight, t.Z));
            entry.CycleTargetLock(); target = entry.LockedTargetSlot;
            Assert.That(target, Is.GreaterThanOrEqualTo(0));
            Assert.That(world.GetUnit(target).Team, Is.EqualTo(self.Team));
            Assert.That(world.IsManualUnitYellowLocked(human, target), Is.True);
            Assert.That(world.IsYellowLocked(human, target), Is.False, "Support locks must stay outside hostile threat/spotting APIs.");
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var feedback = typeof(PlayableEntry).GetField("combatFeedback", flags).GetValue(entry);
            feedback.GetType().GetMethod("Update").Invoke(feedback, new object[] { world, human, camera, target });
            typeof(PlayableEntry).GetMethod("RefreshBattle", flags).Invoke(entry, null); Canvas.ForceUpdateCanvases();
            var bar = entry.transform.Find("PlayableCanvas/BattleHUD/SharedTargetHP"+target);
            Assert.That(bar, Is.Not.Null); Assert.That(bar.gameObject.activeSelf, Is.True);
            Assert.That(bar.GetComponent<CanvasGroup>().alpha, Is.EqualTo(0.5f));
            Assert.That(bar.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            Assert.That(bar.GetComponentInChildren<TMPro.TMP_Text>().text, Does.Contain("味方ロック"));
            foreach (var graphic in bar.GetComponentsInChildren<UnityEngine.UI.Graphic>()) Assert.That(graphic.raycastTarget, Is.False);
            Assert.That(entry.transform.Find("PlayableCanvas/BattleHUD/LockStatus").GetComponent<TMPro.TMP_Text>().text, Does.Contain("味方"));
            entry.ReturnToGarage(); entry.SelectPart(4, "weapon.repairpod");
        }
        private static IEnumerator AssertSettingsModal(PlayableEntry entry, string origin)
        {
            var before = entry.Preferences; var names = QualitySettings.names;
            long tick = entry.Simulation == null ? 0 : entry.Simulation.CurrentTick;
            var originScreen = entry.transform.Find("PlayableCanvas/" + origin);
            Assert.That(originScreen.GetComponents<CanvasGroup>().Length, Is.EqualTo(1), "Modal input groups must be real native components before the click.");
            Assert.That(originScreen.GetComponent<CanvasGroup>() != null, Is.True);
            Assert.That(originScreen.Find("OpenSettings").GetComponent<UnityEngine.UI.Button>().IsInteractable(), Is.True);
            Click(entry, origin + "/OpenSettings");
            Assert.That(entry.IsSettingsOpen, Is.True);
            var screen = entry.transform.Find("PlayableCanvas/Settings"); var panel = screen.Find("SettingsPanel");
            Assert.That(entry.transform.Find("PlayableCanvas/" + origin).GetComponent<CanvasGroup>().interactable, Is.False);
            Assert.That(screen.GetComponent<UnityEngine.UI.Image>().raycastTarget, Is.True);
            Assert.That(panel.Find("Help").GetComponent<TMPro.TMP_Text>().raycastTarget, Is.False);
            var slider = panel.Find("Sensitivity").GetComponent<UnityEngine.UI.Slider>();
            var volume = panel.Find("MasterVolume").GetComponent<UnityEngine.UI.Slider>();
            Assert.That(slider.value, Is.EqualTo(before.mouseSensitivity).Within(0.0001));
            Canvas.ForceUpdateCanvases();
            Assert.That(slider.handleRect.rect.height, Is.LessThanOrEqualTo(slider.GetComponent<RectTransform>().rect.height));
            Assert.That(slider.fillRect.rect.width, Is.EqualTo(((RectTransform)slider.fillRect.parent).rect.width * slider.normalizedValue).Within(0.1));
            var radarRate=panel.Find("RadarBotRate").GetComponent<UnityEngine.UI.Slider>();var defenseRate=panel.Find("InterceptorBotRate").GetComponent<UnityEngine.UI.Slider>();var mapRate=panel.Find("MapReaderBotRate").GetComponent<UnityEngine.UI.Slider>();
            radarRate.value=6;defenseRate.value=12;mapRate.value=25;
            slider.value = 0.23f; volume.value = 0.35f;
            Assert.That(entry.Preferences.botRadarFraction,Is.EqualTo(before.botRadarFraction));
            Assert.That(entry.Preferences.mouseSensitivity, Is.EqualTo(before.mouseSensitivity), "A draft must not change live controls.");
            panel.Find("Cancel").GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); Assert.That(entry.IsSettingsOpen, Is.False);
            Assert.That(entry.Preferences.masterVolume, Is.EqualTo(before.masterVolume));
            Click(entry, origin + "/OpenSettings"); Assert.That(slider.value, Is.EqualTo(before.mouseSensitivity).Within(0.0001));
            radarRate.value=6;defenseRate.value=12;mapRate.value=25;
            slider.value = 0.23f; volume.value = 0.35f;
            var quality = panel.Find("Quality").GetComponent<UnityEngine.UI.Button>();
            Assert.That(quality.interactable, Is.EqualTo(names.Length > 0)); if (names.Length > 0) quality.onClick.Invoke();
            panel.Find("Apply").GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); Assert.That(entry.IsSettingsOpen, Is.False);
            Assert.That(entry.Preferences.botRadarFraction,Is.EqualTo(.06f).Within(.0001));Assert.That(entry.Preferences.botInterceptorFraction,Is.EqualTo(.12f).Within(.0001));Assert.That(entry.Preferences.botMapReaderFraction,Is.EqualTo(.25f).Within(.0001));
            Assert.That(entry.Preferences.mouseSensitivity, Is.EqualTo(0.23f).Within(0.0001)); Assert.That(AudioListener.volume, Is.EqualTo(0.35f).Within(0.0001));
            if (names.Length > 0) Assert.That(QualitySettings.GetQualityLevel(), Is.EqualTo((before.qualityLevel + 1) % names.Length));
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var parse = typeof(PlayableEntry).GetMethod("TryParsePreferences", flags);
            var args = new object[] { JsonUtility.ToJson(entry.Preferences), null }; Assert.That(parse.Invoke(entry, args), Is.EqualTo(true));
            Assert.That(((MechaBattle.Contracts.Settings.PlayerPreferences)args[1]).mouseSensitivity, Is.EqualTo(0.23f).Within(0.0001));
            Assert.That(parse.Invoke(entry, new object[] { "{\"schemaVersion\":99}", null }), Is.EqualTo(false));
            Assert.That(parse.Invoke(entry, new object[] { "{}", null }), Is.EqualTo(false));
            Assert.That(parse.Invoke(entry, new object[] { "[1]", null }), Is.EqualTo(false));
            Assert.That(parse.Invoke(entry, new object[] { "{broken", null }), Is.EqualTo(false));
            Assert.That(entry.ApplyPlayerPreferences(before), Is.True);
            Click(entry, origin + "/OpenSettings"); panel.Find("Defaults").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            entry.CancelSettings(); Assert.That(entry.IsSettingsOpen, Is.False);
            if (origin == "Pause") Assert.That(entry.IsPaused, Is.True);
            yield return null; yield return null;
            if (entry.Simulation != null) Assert.That(entry.Simulation.CurrentTick, Is.EqualTo(tick), "Settings must not resume a paused match.");
            Assert.That(entry.transform.Find("PlayableCanvas/" + origin).GetComponent<CanvasGroup>().interactable, Is.True);
            Canvas.ForceUpdateCanvases(); LogAssert.NoUnexpectedReceived();
        }
        private static void AssertRedeployMap(PlayableEntry entry, MechaBattle.Battlefield.MapDefinition map)
        {
            var config = JsonUtility.FromJson<PlayableConfiguration>(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Game/Content/Playable/PrototypeConfig.json").text);
            config.playablePrototype.Apply(config.p0Simulation); var c = config.p0Simulation;
            c.initialGauge = 100000000; c.maxDurationSeconds = 30; c.respawnDelaySeconds = 0.04f;
            c.baseCounterFireDamage = 1000000; c.baseCounterFireRadius = 10000; c.baseCounterFireIntervalSeconds = 100;
            var testMap = JsonUtility.FromJson<MechaBattle.Battlefield.MapDefinition>(JsonUtility.ToJson(map));
            testMap.obstacles = Array.Empty<MechaBattle.Battlefield.ObstacleDefinition>(); foreach (var b in testMap.mainBases) b.buildingRadius = 0;
            var world = new BattleSimulation(testMap, c, 50); world.SetAutomaticRespawn(50, false); world.Step();
            Assert.That(world.GetUnit(50).IsAlive, Is.False);
            var original = entry.Simulation; var simulation = typeof(PlayableEntry).GetProperty("Simulation");
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var selectedBase = typeof(PlayableEntry).GetField("selectedMainBase", flags); var respawnDeck = typeof(PlayableEntry).GetField("respawnDeck", flags);
            object oldBase = selectedBase.GetValue(entry), oldDeck = respawnDeck.GetValue(entry);
            var ui = typeof(PlayableEntry).GetField("ui", flags).GetValue(entry); var show = ui.GetType().GetMethod("ShowBattle");
            try
            {
                simulation.SetValue(entry, world); show.Invoke(ui, new object[] { true, false });
                typeof(PlayableEntry).GetMethod("RefreshBattle", flags).Invoke(entry, null); Canvas.ForceUpdateCanvases();
                var panel = entry.transform.Find("PlayableCanvas/Redeploy/RedeployPanel"); var field = panel.Find("DeploymentMap/MapField");
                Assert.That(field.Find("MainBase0").GetComponent<UnityEngine.UI.Button>().interactable, Is.False);
                Assert.That(field.Find("Relay0").GetComponent<UnityEngine.UI.Button>().interactable, Is.False);
                field.Find("MainBase4").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Assert.That(panel.Find("SelectedSpawn").GetComponent<TMPro.TMP_Text>().text, Does.Contain("Orange-B"));
                Assert.That(field.Find("MainBase4").GetComponent<UnityEngine.UI.Outline>().enabled, Is.True);
                panel.Find("RespawnDeck1").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                for (int i = 0; i < 4; i++) world.Step();
                typeof(PlayableEntry).GetMethod("RefreshBattle", flags).Invoke(entry, null);
                var deploy = panel.Find("Deploy").GetComponent<UnityEngine.UI.Button>(); Assert.That(deploy.interactable, Is.True);
                deploy.onClick.Invoke(); world.Step(); Assert.That(world.GetUnit(50).IsAlive, Is.True);
                Assert.That(world.GetUnit(50).Z, Is.EqualTo(testMap.mainBases[4].z).Within(testMap.mainBases[4].spawnRadius));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                simulation.SetValue(entry, original); selectedBase.SetValue(entry, oldBase); respawnDeck.SetValue(entry, oldDeck);
                show.Invoke(ui, new object[] { false, false }); typeof(PlayableEntry).GetMethod("RefreshBattle", flags).Invoke(entry, null);
            }
        }
        private static HangarPreview Preview(PlayableEntry entry)
        { return (HangarPreview)typeof(PlayableEntry).GetField("hangarPreview", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(entry); }
        private static UnityEngine.UI.Button PickerChoice(Transform picker, string title)
        {
            foreach (var button in picker.GetComponentsInChildren<UnityEngine.UI.Button>())
                if (button.name.StartsWith("PartOption", StringComparison.Ordinal) && button.GetComponentInChildren<TMPro.TMP_Text>().text.Contains(title)) return button;
            Assert.Fail("Missing picker choice: " + title); return null;
        }
        private static void AssertFrameSelection(PlayableEntry entry)
        {
            entry.ApplyPreset(7);
            var picker=entry.transform.Find("PlayableCanvas/PartPicker");
            entry.OpenPartPicker(2);
            PickerChoice(picker,"FAアサルトライフル").onClick.Invoke();
            Click(entry,"Preparation/Hangar/Row/SelectFrame");
            Assert.That(picker.gameObject.activeSelf,Is.True);
            Assert.That(entry.CopyProfile().deck[0].frameId,Is.EqualTo("frame.standard.v1"),"Opening the comparison must not apply it.");
            var heavy=PickerChoice(picker,"重量フレーム");
            Assert.That(heavy.GetComponentInChildren<TMPro.TMP_Text>().text,Does.Contain("武器変更: 装備 1→単発キャノン"));
            Assert.That(heavy.IsInteractable(),Is.True);heavy.onClick.Invoke();
            Assert.That(picker.gameObject.activeSelf,Is.False);
            Assert.That(entry.CopyProfile().deck[0].frameId,Is.EqualTo("frame.heavy.v1"));
            entry.SelectPart(2,"weapon.assaultrifle");
            Assert.That(Array.Find(entry.CopyProfile().deck[0].equipment,e=>(int)e.slot==2).partId,Is.EqualTo("weapon.cannon"));
            entry.OpenPartPicker(2);
            foreach(var option in picker.GetComponentsInChildren<UnityEngine.UI.Button>())
            {if(!option.name.StartsWith("PartOption",StringComparison.Ordinal))continue;var title=option.GetComponentInChildren<TMPro.TMP_Text>().text;Assert.That(title,Does.Not.Contain("FAアサルトライフル"));Assert.That(title,Does.Not.Contain("FAカービン"));}
            PickerChoice(picker,"HP回復FA").onClick.Invoke();
            AssertFrontend(entry,"Preparation",true);
            Click(entry,"Preparation/Hangar/Row/SelectFrame");PickerChoice(picker,"標準フレーム").onClick.Invoke();
            Assert.That(Array.Find(entry.CopyProfile().deck[0].equipment,e=>(int)e.slot==3).partId,Is.EqualTo("weapon.repairgun"));
            entry.ApplyPreset(0);
        }
        private static void Click(PlayableEntry entry, string relativePath)
        {
            // Layout rows share a name, so find the unique action below its known screen.
            var pieces = relativePath.Split('/'); var screen = entry.transform.Find("PlayableCanvas/" + pieces[0]);
            var action = pieces[pieces.Length - 1];
            foreach (var button in screen.GetComponentsInChildren<UnityEngine.UI.Button>())
                if (button.name == action) { button.onClick.Invoke(); return; }
            Assert.Fail("Missing active button: " + relativePath);
        }
        private static void AssertFrontend(PlayableEntry entry, string screen, bool previewVisible)
        {
            Canvas.ForceUpdateCanvases();
            foreach (var name in new[] { "MainMenu", "Preparation", "BattlePrep", "BattleHUD", "Pause", "Results" })
                Assert.That(entry.transform.Find("PlayableCanvas/" + name).gameObject.activeSelf, Is.EqualTo(name == screen), name);
            Assert.That(entry.transform.Find("PlayableCanvas/" + screen).GetComponent<UnityEngine.UI.Image>().color.a, Is.EqualTo(1));
            var camera = entry.transform.Find("PlayerCamera").GetComponent<Camera>();
            Assert.That(camera.rect, Is.EqualTo(new Rect(0, 0, 1, 1))); Assert.That(camera.cullingMask, Is.Zero);
            Assert.That(camera.targetTexture, Is.Null); Assert.That(entry.transform.Find("Sun").gameObject.activeSelf, Is.False);
            var studio = Preview(entry); Assert.That(studio.Visible, Is.EqualTo(previewVisible));
            if (!previewVisible) return;
            Assert.That(studio.Model, Is.Not.Null); Assert.That(studio.Model.activeInHierarchy, Is.True);
            Assert.That(studio.Camera.targetTexture, Is.SameAs(studio.Texture)); Assert.That(studio.Camera.cullingMask, Is.EqualTo(HangarPreview.PreviewMask));
            Assert.That(studio.Texture.IsCreated(), Is.True); Assert.That(studio.Camera.GetComponent<AudioListener>(), Is.Null);
            Assert.That(studio.Model.GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.GreaterThan(0));
            foreach (var renderer in studio.Model.GetComponentsInChildren<Renderer>()) Assert.That(renderer.gameObject.layer, Is.EqualTo(HangarPreview.PreviewLayer));
            var image = entry.transform.Find("PlayableCanvas/" + screen + "/MechaPreviewPanel/MechaPreviewImage").GetComponent<UnityEngine.UI.RawImage>();
            Assert.That(image.texture, Is.SameAs(studio.Texture)); Assert.That(image.raycastTarget, Is.False);
            Assert.That(image.rectTransform.rect.width / image.rectTransform.rect.height, Is.EqualTo(4f / 3f).Within(0.01));
            var corners = new Vector3[4]; var parentCorners = new Vector3[4]; image.rectTransform.GetWorldCorners(corners); ((RectTransform)image.transform.parent).GetWorldCorners(parentCorners);
            Assert.That(corners[0].x, Is.GreaterThanOrEqualTo(parentCorners[0].x - 1)); Assert.That(corners[0].y, Is.GreaterThanOrEqualTo(parentCorners[0].y - 1));
            Assert.That(corners[2].x, Is.LessThanOrEqualTo(parentCorners[2].x + 1)); Assert.That(corners[2].y, Is.LessThanOrEqualTo(parentCorners[2].y + 1));
        }
        private static void AssertPreviewPixels(PlayableEntry entry)
        {
            // Native graphics assertion; static compilation cannot exercise this.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var target = Preview(entry).Texture; var previous = RenderTexture.active;
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
                var data = pixels.GetPixels32(); var background = data[0]; int different = 0;
                foreach (var p in data) if (Math.Abs(p.r - background.r) + Math.Abs(p.g - background.g) + Math.Abs(p.b - background.b) > 35) different++;
                Assert.That(different, Is.GreaterThan(data.Length / 100), "The preview texture must contain the model, not just the clear color.");
            }
            finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(pixels); }
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            if (!string.IsNullOrEmpty(originalScene) && File.Exists(originalScene)) EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);
            else if (generated) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (generated) { AssetDatabase.DeleteAsset(TestScene); AssetDatabase.DeleteAsset(TestConfig); }
        }
    }
}
