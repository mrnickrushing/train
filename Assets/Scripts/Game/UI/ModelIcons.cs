using System.Collections.Generic;
using Ironhorse.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
#if IRONHORSE_URP
using UnityEngine.Rendering.Universal;
#endif

namespace Ironhorse.Game
{
    /// <summary>
    /// Renders the real 3D locomotive and wagon models (procedural or drop-in art) into cached
    /// transparent textures for cards and lists, so the UI shows the actual trains, not swatches.
    /// </summary>
    public static class ModelIcons
    {
        const int Layer = 31;
        const int Width = 512, Height = 320;
        static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();
        static Camera _cam;
        static Light _key, _fill;
        static Transform _stage;

        public static Texture Locomotive(LocomotiveDef d)
        {
            return Get("loco:" + d.Id, root =>
            {
                var art = ModelRegistry.Spawn(ModelRegistry.Locomotives, d.Id, root);
                if (art == null) Build(TrainFactory.Locomotive(d, 1), root);
            });
        }

        public static Texture Wagon(WagonDef d)
        {
            return Get("wagon:" + d.Id, root =>
            {
                var art = ModelRegistry.Spawn(ModelRegistry.Wagons, d.Id, root);
                if (art == null) Build(TrainFactory.Wagon(d), root);
            });
        }

        public static Texture Truck(TruckDef d)
        {
            return Get("truck:" + d.Id, root =>
            {
                var art = ModelRegistry.Spawn("Trucks", d.Id, root);
                if (art == null) Build(TruckFactory.Truck(d), root);
            });
        }

        public static Texture Building(BuildingDef d)
        {
            if (!ModelRegistry.Has(ModelRegistry.Buildings, d.Id)) return null;
            return Get("bldg:" + d.Id, root => ModelRegistry.Spawn(ModelRegistry.Buildings, d.Id, root), building: true);
        }

        public static Texture Hauler(HaulerDef d)
        {
            return Get("haul:" + d.Id, root => HaulerViews.BuildModel(d, root));
        }

        /// <summary>A RawImage showing the model, sized w × h, for use inside layouts.</summary>
        public static RawImage Image(Transform parent, Texture tex, float w, float h)
        {
            var go = UIKit.Node("Model", parent);
            var img = go.AddComponent<RawImage>();
            img.texture = tex;
            img.raycastTarget = false;
            UIKit.Size(go, w, h);
            return img;
        }

        static void Build(CarModel m, Transform root)
        {
            var go = m.Body.ToObject("Body", root);
            foreach (var w in m.Wheels) w.Mesh.ToObject("Wheel", go.transform).transform.localPosition = w.Position;
            foreach (var r in m.Rods) r.Mesh.ToObject("Rod", go.transform).transform.localPosition = r.Center;
        }

        static Texture Get(string key, System.Action<Transform> build, bool building = false)
        {
            Texture2D tex;
            if (Cache.TryGetValue(key, out tex) && tex != null) return tex;
            Setup();
            var root = new GameObject("Icon " + key).transform;
            root.SetParent(_stage, false);
            try
            {
                build(root);
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
                foreach (var c in root.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
                var rt = RenderTexture.GetTemporary(new RenderTextureDescriptor(Width, Height, RenderTextureFormat.ARGB32, 24) { msaaSamples = 4, sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear });
                Frame(root, building);
                Render(rt);
                // Keep a plain texture; the multisampled render target goes back to the pool.
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false) { name = key, wrapMode = TextureWrapMode.Clamp };
                tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
                tex.Apply(false, true);
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                Cache[key] = tex;
            }
            finally
            {
                root.gameObject.SetActive(false);
                Object.Destroy(root.gameObject);
            }
            return tex;
        }

        static void Setup()
        {
            if (_cam != null) return;
            _stage = new GameObject("Model Icons").transform;
            Object.DontDestroyOnLoad(_stage.gameObject);
            _stage.position = new Vector3(0, -3000, 0); // far below the valley

            _cam = new GameObject("Icon Camera").AddComponent<Camera>();
            _cam.transform.SetParent(_stage, false);
            _cam.enabled = false;
            _cam.cullingMask = 1 << Layer;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0, 0, 0, 0);
            _cam.fieldOfView = 22;
            _cam.nearClipPlane = 0.5f;
            _cam.farClipPlane = 200;
            _cam.allowHDR = false;
#if IRONHORSE_URP
            var data = _cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = false;
#endif
            _key = MakeLight("Key", new Vector3(32, -140, 0), 1.6f, new Color(1f, 0.96f, 0.9f));
            _fill = MakeLight("Fill", new Vector3(10, 60, 0), 0.5f, new Color(0.75f, 0.82f, 1f));
        }

        static Light MakeLight(string name, Vector3 euler, float intensity, Color c)
        {
            var l = new GameObject(name).AddComponent<Light>();
            l.transform.SetParent(_stage, false);
            l.transform.rotation = Quaternion.Euler(euler);
            l.type = LightType.Directional;
            l.intensity = intensity;
            l.color = c;
            l.shadows = LightShadows.None;
            l.cullingMask = 1 << Layer;
            l.enabled = false;
            return l;
        }

        /// <summary>Three-quarter view from the front, fitted to the model's bounds.</summary>
        static void Frame(Transform root, bool building = false)
        {
            var b = ModelRegistry.RendererBounds(root.gameObject);
            // Vehicles: look at the nose (+Z) from front-left. Buildings: look at the street side (-Z) from above.
            var dir = building ? Quaternion.Euler(30, 28, 0) * Vector3.forward : Quaternion.Euler(14, -128, 0) * Vector3.forward;
            float radius = b.extents.magnitude;
            float dist = radius / Mathf.Sin(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 0.54f;
            _cam.transform.position = b.center - dir * dist;
            _cam.transform.rotation = Quaternion.LookRotation(dir);
        }

        static void Render(RenderTexture rt)
        {
            // The world's sun and fog would otherwise light the icon (and darken it at night).
            var sun = RenderSettings.sun;
            bool sunOn = sun != null && sun.enabled, fog = RenderSettings.fog;
            var ambient = RenderSettings.ambientMode;
            Color sky = RenderSettings.ambientSkyColor, eq = RenderSettings.ambientEquatorColor, gr = RenderSettings.ambientGroundColor;
            if (sun != null) sun.enabled = false;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.58f, 0.64f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.43f, 0.4f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.22f, 0.2f);
            RenderSettings.sun = _key;
            _key.enabled = _fill.enabled = true;
            try
            {
                _cam.targetTexture = rt;
#if IRONHORSE_URP
                var req = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                if (GraphicsSettings.currentRenderPipeline != null && RenderPipeline.SupportsRenderRequest(_cam, req))
                    RenderPipeline.SubmitRenderRequest(_cam, req);
                else
#endif
                    _cam.Render();
            }
            finally
            {
                _cam.targetTexture = null;
                _key.enabled = _fill.enabled = false;
                RenderSettings.sun = sun;
                if (sun != null) sun.enabled = sunOn;
                RenderSettings.fog = fog;
                RenderSettings.ambientMode = ambient;
                RenderSettings.ambientSkyColor = sky; RenderSettings.ambientEquatorColor = eq; RenderSettings.ambientGroundColor = gr;
            }
        }
    }
}
