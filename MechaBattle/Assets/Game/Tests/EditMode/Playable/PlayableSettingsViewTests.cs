using System;
using System.Reflection;
using MechaBattle.Contracts.Settings;
using MechaBattle.Playable;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MechaBattle.Tests.Playable
{
    // Short native EditMode coverage: no playable scene generation or match is needed.
    public sealed class PlayableSettingsViewTests
    {
        private sealed class Fixture : IDisposable
        {
            private readonly Scene preview;
            private readonly TMP_FontAsset font;
            private readonly Type type;
            public readonly GameObject[] Screens;
            public readonly object View;
            public GameObject Panel => (GameObject)type.GetField("Panel").GetValue(View);
            public Fixture(bool inactive = false, bool existingGroups = false)
            {
                Assert.That(Application.isPlaying, Is.False, "Run this fixture in EditMode.");
                preview = EditorSceneManager.NewPreviewScene();
                try
                {
                    var root = EditorUtility.CreateGameObjectWithHideFlags("SettingsViewTest", HideFlags.HideAndDontSave, typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.GraphicRaycaster));
                    SceneManager.MoveGameObjectToScene(root, preview); root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                    string[] names = { "MainMenu", "Preparation", "BattlePrep", "BattleHUD", "Redeploy", "Pause", "Results", "PartPicker" };
                    Screens = new GameObject[names.Length];
                    for (int i = 0; i < Screens.Length; i++)
                    {
                        Screens[i] = new GameObject(names[i], typeof(RectTransform)); Screens[i].transform.SetParent(root.transform, false);
                        if (existingGroups) { var g = Screens[i].AddComponent<CanvasGroup>(); g.interactable = i % 2 == 0; g.blocksRaycasts = i % 3 == 0; g.alpha = 0.6f; }
                        Screens[i].SetActive(!inactive);
                    }
                    font = TMP_FontAsset.CreateFontAsset("Yu Gothic", "Regular", 48);
                    Assert.That(font != null, Is.True, "The Windows prototype needs Yu Gothic, as does the production UI.");
                    var shader = Shader.Find("TextMeshPro/Mobile/Distance Field"); Assert.That(shader != null, Is.True); font.material.shader = shader;
                    type = typeof(PlayableEntry).Assembly.GetType("MechaBattle.Playable.PlayableSettingsView", true);
                    View = Activator.CreateInstance(type, root.transform, font, Screens, (Action<PlayerPreferences>)(_ => { }), (Action)(() => { }));
                }
                catch { Dispose(); throw; }
            }
            public void Open() => type.GetMethod("Open").Invoke(View, new object[]
            { new PlayerPreferences { qualityLevel = 0, qualityName = "Test" }, new PlayerPreferences { qualityLevel = 0, qualityName = "Test" }, new[] { "Test" }, 0.01f, 1f });
            public void Close() => type.GetMethod("Close").Invoke(View, null);
            public void Dispose()
            {
                if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
                if (font != null)
                {
                    foreach (var atlas in font.atlasTextures) if (atlas != null) UnityEngine.Object.DestroyImmediate(atlas);
                    if (font.material != null) UnityEngine.Object.DestroyImmediate(font.material);
                    UnityEngine.Object.DestroyImmediate(font);
                }
            }
        }
        [TestCase(false)] [TestCase(true)]
        public void MissingCanvasGroupsAreAddedAndTheDialogCanBeReopened(bool inactiveScreens)
        {
            using (var f = new Fixture(inactiveScreens))
            {
                foreach (var screen in f.Screens)
                {
                    Assert.That(screen.GetComponents<CanvasGroup>().Length, Is.EqualTo(1), screen.name);
                    Assert.That(screen.GetComponent<CanvasGroup>() != null, Is.True, "Use Unity null semantics, not CLR Is.Not.Null.");
                }
                for (int round = 0; round < 3; round++)
                {
                    Assert.DoesNotThrow(f.Open); Assert.That(f.Panel.activeInHierarchy, Is.True);
                    foreach (var screen in f.Screens) { var g = screen.GetComponent<CanvasGroup>(); Assert.That(g.interactable, Is.False); Assert.That(g.blocksRaycasts, Is.False); }
                    Assert.DoesNotThrow(f.Close); Assert.That(f.Panel.activeSelf, Is.False);
                    foreach (var screen in f.Screens) { var g = screen.GetComponent<CanvasGroup>(); Assert.That(g.interactable, Is.True); Assert.That(g.blocksRaycasts, Is.True); }
                }
                LogAssert.NoUnexpectedReceived();
            }
        }
        [Test]
        public void ExistingGroupsAreReusedAndTheirFlagsAreRestored()
        {
            using (var f = new Fixture(existingGroups: true))
            {
                var original = new CanvasGroup[f.Screens.Length]; for (int i = 0; i < original.Length; i++) original[i] = f.Screens[i].GetComponent<CanvasGroup>();
                Assert.DoesNotThrow(f.Open); Assert.DoesNotThrow(f.Close);
                for (int i = 0; i < original.Length; i++)
                {
                    Assert.That(f.Screens[i].GetComponent<CanvasGroup>(), Is.SameAs(original[i])); Assert.That(f.Screens[i].GetComponents<CanvasGroup>().Length, Is.EqualTo(1));
                    Assert.That(original[i].interactable, Is.EqualTo(i % 2 == 0)); Assert.That(original[i].blocksRaycasts, Is.EqualTo(i % 3 == 0)); Assert.That(original[i].alpha, Is.EqualTo(0.6f));
                }
                LogAssert.NoUnexpectedReceived();
            }
        }
        [Test]
        public void ARemovedGroupIsRecreatedBeforeTheNextOpen()
        {
            using (var f = new Fixture())
            {
                UnityEngine.Object.DestroyImmediate(f.Screens[0].GetComponent<CanvasGroup>());
                Assert.DoesNotThrow(f.Open); Assert.That(f.Screens[0].GetComponents<CanvasGroup>().Length, Is.EqualTo(1));
                Assert.That(f.Screens[0].GetComponent<CanvasGroup>().interactable, Is.False); Assert.DoesNotThrow(f.Close);
                Assert.That(f.Screens[0].GetComponent<CanvasGroup>().interactable, Is.True); LogAssert.NoUnexpectedReceived();
            }
        }
        [Test]
        public void ClosingSurvivesAGroupRemovedWhileTheDialogWasOpen()
        {
            using (var f = new Fixture())
            {
                f.Open(); UnityEngine.Object.DestroyImmediate(f.Screens[0].GetComponent<CanvasGroup>());
                Assert.DoesNotThrow(f.Close); Assert.That(f.Panel.activeSelf, Is.False);
                for (int i = 1; i < f.Screens.Length; i++) Assert.That(f.Screens[i].GetComponent<CanvasGroup>().interactable, Is.True);
                LogAssert.NoUnexpectedReceived();
            }
        }
    }
}
