using System;
using System.IO;
using System.Linq;
using MechaBattle.Contracts;
using MechaBattle.Battlefield;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace MechaBattle.EditorTools
{
    public static class PreparationCommands
    {
        private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        [MenuItem("MechaBattle/Preparation/Validate Foundation")]
        public static void Validate()
        {
            string file = Path.Combine(ProjectRoot, "config/standard-map.json");
            var map = JsonUtility.FromJson<MapDefinition>(File.ReadAllText(file));
            MapValidator.ThrowIfInvalid(map);
            var roster = new MatchRoster();
            Debug.Log("MechaBattle foundation valid: " + roster.Controllers.Count +
                " controller slots, " + roster.Commanders.Count + " commanders, " +
                map.mainBases.Length + " main bases. Gameplay and performance are not verified.");
        }

        // Builds the CURRENT enabled scenes for toolchain verification only.
        // This must not be reported as a P0 gameplay/benchmark build.
        [MenuItem("MechaBattle/Preparation/Build Windows Foundation")]
        public static void BuildWindows()
        {
            Validate();
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) throw new BuildFailedException("No enabled scenes. Generate/configure scenes first.");
            string folder = Path.Combine(ProjectRoot, "Build/Windows");
            Directory.CreateDirectory(folder);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes, locationPathName = Path.Combine(folder, "MechaBattle.exe"),
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
            });
            string reports = Path.Combine(ProjectRoot, "Build/Reports");
            Directory.CreateDirectory(reports);
            File.WriteAllText(Path.Combine(reports, "windows-build.json"), JsonUtility.ToJson(new BuildRecord
            {
                scope = "foundation-toolchain-only-p0-not-complete", result = report.summary.result.ToString(),
                unityVersion = Application.unityVersion, scenes = scenes,
                errors = report.summary.totalErrors, warnings = report.summary.totalWarnings,
                output = report.summary.outputPath
            }, true));
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Windows build failed: " + report.summary.result);
        }

        [Serializable]
        private sealed class BuildRecord
        {
            public string scope, result, unityVersion, output;
            public string[] scenes;
            public int errors, warnings;
        }
    }
}
