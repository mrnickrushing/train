using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Ironhorse.EditorTools
{
    /// <summary>
    /// Command-line build entry points (used by Codemagic):
    ///   Unity -batchmode -quit -projectPath . -executeMethod Ironhorse.EditorTools.BuildScript.BuildIos
    /// Produces an Xcode project in ./ios which is then signed and archived on the Mac build machine.
    /// You can also run it from the editor menu on Windows (needs the iOS Build Support module).
    /// </summary>
    public static class BuildScript
    {
        public const string IosOutput = "ios";

        [MenuItem("Ironhorse/Build iOS Xcode Project")]
        public static void BuildIos()
        {
            ProjectSetup.EnsureScene();
            ProjectSetup.ApplyPlayerSettings();
            AppIcon.Generate();

            var buildNumber = Environment.GetEnvironmentVariable("BUILD_NUMBER");
            if (!string.IsNullOrEmpty(buildNumber)) PlayerSettings.iOS.buildNumber = buildNumber;
            var version = Environment.GetEnvironmentVariable("APP_VERSION");
            if (!string.IsNullOrEmpty(version)) PlayerSettings.bundleVersion = version;

            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = IosOutput,
                target = BuildTarget.iOS,
                targetGroup = BuildTargetGroup.iOS,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log("[Ironhorse] iOS build " + report.summary.result + " in " + report.summary.totalTime + ", " + report.summary.totalErrors + " errors");
            if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
