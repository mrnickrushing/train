using System.Collections.Generic;
using UnityEngine;

namespace Ironhorse.Game
{
    /// <summary>
    /// Real light sources for the night: platform and street lamps, porch/window light on buildings,
    /// locomotive headlights, yard floodlights. They fade in at dusk and are switched off entirely by day
    /// (zero cost), relying on URP Forward+ to keep dozens of small lights cheap.
    /// </summary>
    public sealed class NightLights : MonoBehaviour
    {
        public static NightLights Instance { get; private set; }

        public static readonly Color Warm = new Color(1f, 0.72f, 0.42f);
        public static readonly Color Lamp = new Color(1f, 0.8f, 0.52f);

        struct Pending { public Vector3 Pos; public float Range, Intensity; public Color Color; }
        static readonly List<Pending> _pending = new List<Pending>();
        readonly List<(Light light, float intensity)> _lights = new List<(Light, float)>();
        bool _on = true;

        /// <summary>A fixed point light (lamp heads built into static scenery meshes).</summary>
        public static void AddPoint(Vector3 pos, float range = 17f, float intensity = 9f, Color? color = null)
        {
            var p = new Pending { Pos = pos, Range = range, Intensity = intensity, Color = color ?? Lamp };
            if (Instance != null) Instance.Make(p, null);
            else _pending.Add(p);
        }

        /// <summary>A light that belongs to a moving or removable object (destroyed with it).</summary>
        public static Light Attach(Transform owner, Vector3 localPos, LightType type, float range, float intensity, Color color, Quaternion? localRot = null, float spotAngle = 70f)
        {
            var go = new GameObject("Night Light");
            go.transform.SetParent(owner, false);
            go.transform.localPosition = localPos;
            if (localRot.HasValue) go.transform.localRotation = localRot.Value;
            var l = go.AddComponent<Light>();
            l.type = type;
            l.range = range;
            l.color = color;
            l.spotAngle = spotAngle;
            l.innerSpotAngle = spotAngle * 0.55f;
            l.shadows = LightShadows.None;
            l.renderMode = LightRenderMode.ForcePixel;
            l.intensity = 0;
            l.enabled = false;
            if (Instance != null) Instance._lights.Add((l, intensity));
            else go.AddComponent<RegisterLater>().Intensity = intensity;
            return l;
        }

        void Awake()
        {
            Instance = this;
            foreach (var p in _pending) Make(p, null);
            _pending.Clear();
        }

        void Make(Pending p, Transform parent)
        {
            var go = new GameObject("Lamp Light");
            go.transform.SetParent(parent != null ? parent : transform, false);
            go.transform.position = p.Pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = p.Range;
            l.color = p.Color;
            l.shadows = LightShadows.None;
            l.renderMode = LightRenderMode.ForcePixel;
            l.intensity = 0;
            l.enabled = false;
            _lights.Add((l, p.Intensity));
        }

        void Update()
        {
            var dn = DayNightCycle.Instance;
            float n = dn != null ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.05f, 0.6f, dn.Night)) : 0;
            bool on = n > 0.01f;
            for (int i = _lights.Count - 1; i >= 0; i--)
            {
                var (l, k) = _lights[i];
                if (l == null) { _lights.RemoveAt(i); continue; }
                if (on != _on || on) { l.enabled = on; l.intensity = k * n; }
            }
            _on = on;
        }

        /// <summary>Lights created before the manager exists register themselves once it does.</summary>
        sealed class RegisterLater : MonoBehaviour
        {
            public float Intensity;
            void Update()
            {
                if (Instance == null) return;
                Instance._lights.Add((GetComponent<Light>(), Intensity));
                Destroy(this);
            }
        }
    }
}
