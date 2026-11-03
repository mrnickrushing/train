#if IRONHORSE_URP
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ironhorse.EditorTools
{
    /// <summary>
    /// Creates the project's URP pipeline asset + renderer (with screen-space ambient occlusion) and
    /// assigns it to Graphics and every Quality level. Runs from Ironhorse → Set Up Project and before
    /// iOS builds. Safe to run repeatedly.
    /// </summary>
    public static class UrpSetup
    {
        const string Folder = "Assets/Settings";
        const string RendererPath = Folder + "/IronhorseRenderer.asset";
        const string AssetPath = Folder + "/IronhorseURP.asset";

        [MenuItem("Ironhorse/Set Up URP Graphics")]
        public static void Run()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
                if (renderer == null)
                {
                    renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                    var post = AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
                    if (post != null) renderer.postProcessData = post;
                    AssetDatabase.CreateAsset(renderer, RendererPath);
                    AddAmbientOcclusion(renderer);
                }

                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetPath);
                if (asset == null)
                {
                    asset = UniversalRenderPipelineAsset.Create(renderer);
                    AssetDatabase.CreateAsset(asset, AssetPath);
                }
                renderer.renderingMode = RenderingMode.ForwardPlus; // dozens of night lamps without per-object limits
                EditorUtility.SetDirty(renderer);
                // Sharp cascade reaches past the default camera distance (95 m); soft (PCF) edges so
                // shadow outlines don't crawl as pixel stairs.
                asset.shadowDistance = 200f;
                asset.shadowCascadeCount = 2;
                asset.cascade2Split = 0.55f;
                var so = new SerializedObject(asset);
                so.FindProperty("m_SoftShadowsSupported").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                asset.msaaSampleCount = 4;
                asset.supportsHDR = true;
                asset.renderScale = 1f;
                asset.supportsCameraDepthTexture = true;
                EditorUtility.SetDirty(asset);

                GraphicsSettings.defaultRenderPipeline = asset;
                int current = QualitySettings.GetQualityLevel();
                for (int i = 0; i < QualitySettings.names.Length; i++)
                {
                    QualitySettings.SetQualityLevel(i, false);
                    QualitySettings.renderPipeline = asset;
                }
                QualitySettings.SetQualityLevel(current, false);
                AssetDatabase.SaveAssets();
                Debug.Log("[Ironhorse] URP graphics configured: " + AssetPath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ironhorse] URP setup failed, the game will use the built-in renderer instead: " + e.Message);
            }
        }

        /// <summary>SSAO is an internal URP type in some versions, so it's added by reflection.</summary>
        static void AddAmbientOcclusion(UniversalRendererData renderer)
        {
            try
            {
                var type = typeof(UniversalRendererData).Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion");
                if (type == null) return;
                var feature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(type);
                feature.name = "Ambient Occlusion";
                AssetDatabase.AddObjectToAsset(feature, renderer);
                renderer.rendererFeatures.Add(feature);
                renderer.SetDirty();
                EditorUtility.SetDirty(renderer);
            }
            catch (Exception e) { Debug.LogWarning("[Ironhorse] Could not add SSAO: " + e.Message); }
        }
    }
}
#endif
