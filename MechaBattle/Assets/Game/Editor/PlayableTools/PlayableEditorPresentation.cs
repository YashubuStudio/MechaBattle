using System;
using System.Collections.Generic;
using System.Reflection;
using MechaBattle.Playable;
using UnityEditor;
using UnityEngine;

namespace MechaBattle.EditorTools
{
    // Unity 6000.3 GameView exposes this per-window switch internally.
    // Keep reflection here, with no runtime/player dependency or persistent preference write.
    public sealed class PlayableGizmoScope : IDisposable
    {
        private static readonly Type GameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        private static readonly PropertyInfo Switch = GameViewType?.GetProperty("drawGizmos", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private readonly HashSet<EditorWindow> seen = new HashSet<EditorWindow>();
        private readonly List<EditorWindow> changed = new List<EditorWindow>();
        public static bool Supported => Switch != null && Switch.CanRead && Switch.CanWrite;
        public void Suppress(EditorWindow window)
        {
            if (!Supported || window == null || !GameViewType.IsInstanceOfType(window) || !seen.Add(window)) return;
            if (!(bool)Switch.GetValue(window)) return;
            Switch.SetValue(window, false); changed.Add(window); window.Repaint();
        }
        public void SuppressOpenGameViews()
        {
            if (!Supported) return;
            foreach (var window in Resources.FindObjectsOfTypeAll(GameViewType)) Suppress((EditorWindow)window);
        }
        public void Dispose()
        {
            foreach (var window in changed)
                if (window != null && !(bool)Switch.GetValue(window)) { Switch.SetValue(window, true); window.Repaint(); }
            changed.Clear(); seen.Clear();
        }
    }

    [InitializeOnLoad]
    internal static class PlayableEditorPresentation
    {
        private static readonly PlayableGizmoScope Scope = new PlayableGizmoScope();
        private static bool active = EditorApplication.isPlaying;
        private static double nextCheck;
        static PlayableEditorPresentation()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += Changed;
            AssemblyReloadEvents.beforeAssemblyReload += Scope.Dispose;
            EditorApplication.quitting += Scope.Dispose;
        }
        private static void Changed(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode) { active = true; nextCheck = 0; Update(); }
            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode) { active = false; Scope.Dispose(); }
        }
        private static void Update()
        {
            if (!active || !EditorApplication.isPlaying || EditorApplication.timeSinceStartup < nextCheck) return;
            nextCheck = EditorApplication.timeSinceStartup + 0.25;
            if (UnityEngine.Object.FindFirstObjectByType<PlayableEntry>() == null) { Scope.Dispose(); return; }
            Scope.SuppressOpenGameViews();
        }
    }
}
