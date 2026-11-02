using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ironhorse.EditorTools
{
    /// <summary>
    /// One-time project configuration so a fresh clone opens ready to play and build:
    /// creates the main scene, adds it to Build Settings, and applies iOS player settings.
    /// Runs automatically on editor load (once) and from the Ironhorse menu.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";
        public const string BundleId = "com.rushingtech.ironhorsefrontier";
        public const string ProductName = "Ironhorse Frontier";
        const string DoneKey = "Ironhorse.ProjectSetup.v2"; // bump to re-run setup after upgrades

        static ProjectSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (Application.isBatchMode) return;
                if (File.Exists(ScenePath) && EditorPrefs.GetBool(DoneKey + Application.dataPath, false)) return;
                Run();
                EditorPrefs.SetBool(DoneKey + Application.dataPath, true);
            };
        }

        [MenuItem("Ironhorse/Set Up Project")]
        public static void Run()
        {
            EnsureScene();
            ApplyPlayerSettings();
#if IRONHORSE_URP
            UrpSetup.Run();
#endif
            AppIcon.Generate();
            AssetDatabase.SaveAssets();
            Debug.Log("[Ironhorse] Project set up. Open " + ScenePath + " and press Play.");
        }

        public static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                // Empty scene: GameRoot builds the entire world at runtime.
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.Refresh();
            }
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ScenePath))
            {
                scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }

        public static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "Rushing Technologies";
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
            PlayerSettings.bundleVersion = "0.1.0";
            // Linear lighting for URP; the world shader converts sRGB vertex colors itself.
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.statusBarHidden = true;
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.iOS.appleEnableAutomaticSigning = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.iOS, ManagedStrippingLevel.Low);
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.backgroundColor = new Color(0.23f, 0.15f, 0.09f);
        }
    }

    /// <summary>Paints a placeholder app icon (brass medallion with a locomotive) until real art exists.</summary>
    public static class AppIcon
    {
        public const string Path_ = "Assets/Art/AppIcon.png";

        public static void Generate()
        {
            if (!File.Exists(Path_))
            {
                const int n = 1024;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                var px = new Color32[n * n];
                Color wood = new Color(0.23f, 0.15f, 0.09f), brass = new Color(0.85f, 0.66f, 0.25f), sky = new Color(0.98f, 0.62f, 0.33f), red = new Color(0.66f, 0.2f, 0.15f), ink = new Color(0.12f, 0.08f, 0.05f);
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float u = x / (float)n, v = y / (float)n;
                        float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f));
                        Color c = wood * (0.85f + 0.15f * Mathf.PerlinNoise(u * 40, v * 3));
                        if (d < 0.44f) c = brass;
                        if (d < 0.40f) c = Color.Lerp(sky, new Color(0.98f, 0.85f, 0.5f), v);
                        if (d < 0.40f && v < 0.38f) c = new Color(0.72f, 0.4f, 0.24f); // mesa ground
                        if (d < 0.40f && v > 0.36f && v < 0.38f) c = ink; // rail
                        // locomotive silhouette
                        bool boiler = u > 0.3f && u < 0.62f && v > 0.43f && v < 0.53f;
                        bool cab = u > 0.56f && u < 0.7f && v > 0.43f && v < 0.62f;
                        bool stack = u > 0.34f && u < 0.4f && v > 0.53f && v < 0.63f;
                        bool cow = u > 0.24f && u < 0.31f && v > 0.39f && v < 0.45f && (v - 0.39f) < (u - 0.24f) * 0.9f;
                        bool wheel = Vector2.Distance(new Vector2(u, v), new Vector2(0.4f, 0.42f)) < 0.045f || Vector2.Distance(new Vector2(u, v), new Vector2(0.52f, 0.42f)) < 0.045f || Vector2.Distance(new Vector2(u, v), new Vector2(0.64f, 0.41f)) < 0.035f;
                        if (d < 0.40f && (boiler || cab || stack || cow)) c = cab ? red : ink;
                        if (d < 0.40f && wheel) c = ink;
                        px[y * n + x] = c;
                    }
                tex.SetPixels32(px);
                Directory.CreateDirectory(Path.GetDirectoryName(Path_));
                File.WriteAllBytes(Path_, tex.EncodeToPNG());
                AssetDatabase.ImportAsset(Path_);
            }
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(Path_);
            if (icon != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        }
    }
}
