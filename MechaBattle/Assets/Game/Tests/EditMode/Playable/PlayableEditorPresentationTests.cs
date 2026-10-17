using System;
using System.Reflection;
using MechaBattle.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MechaBattle.Tests.Playable
{
    public sealed class PlayableEditorPresentationTests
    {
        [TestCase(true)]
        [TestCase(false)]
        public void GameViewGizmosAreTemporarilyHiddenAndRestoredWithoutChangingSceneOrFocus(bool original)
        {
            Assert.That(PlayableGizmoScope.Supported, Is.True, "Unity 6000.3 GameView adapter must remain supported.");
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            var property = type.GetProperty("drawGizmos", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var view = (EditorWindow)ScriptableObject.CreateInstance(type);
            var focus = EditorWindow.focusedWindow; var scene = SceneView.lastActiveSceneView; bool sceneGizmos = scene != null && scene.drawGizmos;
            try
            {
                property.SetValue(view, original);
                using (var scope = new PlayableGizmoScope())
                {
                    scope.Suppress(view); Assert.That((bool)property.GetValue(view), Is.False);
                    if (scene != null) { scope.Suppress(scene); Assert.That(scene.drawGizmos, Is.EqualTo(sceneGizmos)); }
                    Assert.That(EditorWindow.focusedWindow, Is.SameAs(focus));
                }
                Assert.That((bool)property.GetValue(view), Is.EqualTo(original));
            }
            finally { UnityEngine.Object.DestroyImmediate(view); }
        }
        [Test]
        public void ExplicitlyReenabledGizmosAreNotContinuouslyOverridden()
        {
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            var property = type.GetProperty("drawGizmos", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var view = (EditorWindow)ScriptableObject.CreateInstance(type);
            try
            {
                property.SetValue(view, true);
                using (var scope = new PlayableGizmoScope())
                {
                    scope.Suppress(view); property.SetValue(view, true); scope.Suppress(view);
                    Assert.That((bool)property.GetValue(view), Is.True);
                }
                Assert.That((bool)property.GetValue(view), Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(view); }
        }
    }
}
