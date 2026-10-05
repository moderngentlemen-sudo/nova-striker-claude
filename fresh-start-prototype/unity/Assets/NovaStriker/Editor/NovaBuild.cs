// Command-line builds (the GitHub workflow, or a terminal):
//   Unity -batchmode -quit -projectPath <unity folder> -buildTarget StandaloneWindows64
//         -executeMethod NovaStriker.EditorTools.NovaBuild.Build [-customBuildPath <output file>]
// It runs the project setup first (the repository holds only code and shaders, so a fresh checkout has no
// materials, pipeline asset or scene yet), then builds the scene for the editor's active build target. With no
// -customBuildPath it writes to Builds/<target>/. The menu Nova Striker > Build for This Platform does the same.
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NovaStriker.EditorTools
{
    public static class NovaBuild
    {
        const string NAME = "NovaStriker";

        [MenuItem("Nova Striker/Build for This Platform", priority = 20)]
        public static void BuildFromMenu() => Run(false);

        // Entry point for -executeMethod: exits with 0 on success, 1 on failure
        public static void Build() => Run(true);

        static void Run(bool batch)
        {
            try
            {
                NovaSetup.SetUp();
                PlayerSettings.productName = "Nova Striker";
                PlayerSettings.companyName = "Nova Striker prototype";
                PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
                PlayerSettings.resizableWindow = true;
                PlayerSettings.runInBackground = true;
                AssetDatabase.SaveAssets();

                var target = EditorUserBuildSettings.activeBuildTarget;
                string path = Arg("-customBuildPath") ?? DefaultPath(target);
                // (GameCI names the macOS and Linux builds without an extension: give them the usual ones)
                if (target == BuildTarget.StandaloneOSX && !path.EndsWith(".app")) path += ".app";
                if (target == BuildTarget.StandaloneLinux64 && Path.GetExtension(path) == "") path += ".x86_64";
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                var opts = new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/NovaStriker/Scenes/NovaStriker.unity" },
                    locationPathName = path,
                    target = target,
                    options = BuildOptions.None,
                };
                Debug.Log($"Nova Striker: building {target} to {path}");
                var report = BuildPipeline.BuildPlayer(opts);
                var s = report.summary;
                Debug.Log($"Nova Striker: build {s.result}: {s.totalErrors} errors, {s.totalWarnings} warnings, {s.totalSize / (1024 * 1024)} MB, {s.totalTime}");
                if (batch) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
                else if (s.result == BuildResult.Succeeded) EditorUtility.RevealInFinder(path);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (batch) EditorApplication.Exit(1);
            }
        }

        static string DefaultPath(BuildTarget t)
        {
            string ext = t == BuildTarget.StandaloneWindows64 || t == BuildTarget.StandaloneWindows ? ".exe" : t == BuildTarget.StandaloneOSX ? ".app" : t == BuildTarget.StandaloneLinux64 ? ".x86_64" : "";
            return Path.Combine("Builds", t.ToString(), NAME + ext);
        }

        static string Arg(string name)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return null;
        }
    }
}
