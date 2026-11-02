using UnityEngine;
using UnityEngine.Rendering;

namespace Ironhorse.Game
{
    /// <summary>
    /// Drives the sun/moon light, sky gradient, ambient light, fog and the night glow of windows
    /// and lamps. One full day lasts <see cref="DayLengthSeconds"/>.
    /// </summary>
    public sealed class DayNightCycle : MonoBehaviour
    {
        public static DayNightCycle Instance { get; private set; }
        public float DayLengthSeconds = 720f;
        /// <summary>0..1, 0 = sunrise. Daylight lasts DayFraction of the cycle.</summary>
        public float Phase = 0.12f;
        public const float DayFraction = 0.68f;
        public float Night { get; private set; }

        Light _sun;
        ReflectionProbe _probe;
        float _probeTimer;

        static readonly int SkyTop = Shader.PropertyToID("_SkyTop"), SkyHorizon = Shader.PropertyToID("_SkyHorizon"),
            SkyGround = Shader.PropertyToID("_SkyGround"), SunColor = Shader.PropertyToID("_SunColor"), SunDir = Shader.PropertyToID("_SunDir"),
            StarAmount = Shader.PropertyToID("_StarAmount"), NightGlow = Shader.PropertyToID("_NightGlow"), GlowColor = Shader.PropertyToID("_GlowColor");

        void Awake()
        {
            Instance = this;
            var go = new GameObject("Sun");
            go.transform.SetParent(transform, false);
            _sun = go.AddComponent<Light>();
            _sun.type = LightType.Directional;
            _sun.shadows = LightShadows.Soft;
            _sun.shadowStrength = 0.9f;
            _sun.shadowBias = 0.04f;
            _sun.shadowNormalBias = 0.35f;
            RenderSettings.sun = _sun;

            RenderSettings.skybox = Materials.Sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0014f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;

            var p = new GameObject("Sky Reflection");
            p.transform.SetParent(transform, false);
            p.transform.position = new Vector3(0, 40, 0);
            _probe = p.AddComponent<ReflectionProbe>();
            _probe.mode = ReflectionProbeMode.Realtime;
            _probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            _probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            _probe.size = new Vector3(2000, 600, 2000);
            _probe.cullingMask = 0; // sky only — cheap
            _probe.resolution = 64;
            _probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            Apply();
            _probe.RenderProbe();
        }

        void Update()
        {
            Phase = Mathf.Repeat(Phase + Time.deltaTime / DayLengthSeconds, 1f);
            Apply();
            _probeTimer -= Time.deltaTime;
            if (_probeTimer <= 0) { _probeTimer = 4f; _probe.RenderProbe(); }
        }

        /// <summary>Sun elevation in degrees (negative at night).</summary>
        public float Elevation
        {
            get
            {
                if (Phase < DayFraction) return Mathf.Sin(Phase / DayFraction * Mathf.PI) * 64f - 2f;
                return -Mathf.Sin((Phase - DayFraction) / (1 - DayFraction) * Mathf.PI) * 35f - 2f;
            }
        }

        void Apply()
        {
            float e = Elevation;
            float azimuth = Phase * 360f + 120f;
            float day = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-6, 14, e));
            float golden = Mathf.Clamp01(1 - Mathf.Abs(e - 4) / 16f);
            Night = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-8, 6, e));

            // Light direction: sun by day, moon (opposite side, fixed altitude) by night.
            Vector3 toSun = -(Quaternion.Euler(e, azimuth, 0) * Vector3.forward);
            Quaternion lightRot = day > 0.02f ? Quaternion.Euler(Mathf.Max(e, 4f), azimuth, 0) : Quaternion.Euler(38, azimuth + 180, 0);
            _sun.transform.rotation = lightRot;

            Color noon = new Color(1f, 0.95f, 0.86f), gold = new Color(1f, 0.62f, 0.36f), moon = new Color(0.55f, 0.65f, 0.95f);
            Color lightCol = Color.Lerp(noon, gold, golden);
            _sun.color = day > 0.02f ? lightCol : moon;
            _sun.intensity = day > 0.02f ? Mathf.Lerp(0.15f, 1.85f, day) : 0.28f;

            Color topDay = new Color(0.30f, 0.53f, 0.84f), topSet = new Color(0.33f, 0.36f, 0.62f), topNight = new Color(0.02f, 0.03f, 0.09f);
            Color horDay = new Color(0.80f, 0.86f, 0.90f), horSet = new Color(1f, 0.6f, 0.38f), horNight = new Color(0.07f, 0.08f, 0.16f);
            Color top = Color.Lerp(Color.Lerp(topNight, topDay, day), topSet, golden * 0.7f);
            Color hor = Color.Lerp(Color.Lerp(horNight, horDay, day), horSet, golden * 0.85f);
            Color ground = Color.Lerp(new Color(0.05f, 0.04f, 0.04f), new Color(0.55f, 0.45f, 0.36f), day);

            Shader.SetGlobalColor(SkyTop, top);
            Shader.SetGlobalColor(SkyHorizon, hor);
            Shader.SetGlobalColor(SkyGround, ground);
            Shader.SetGlobalColor(SunColor, Color.Lerp(new Color(0.4f, 0.45f, 0.6f) * 0.3f, Color.Lerp(noon, gold, golden), day));
            Shader.SetGlobalVector(SunDir, toSun);
            Shader.SetGlobalFloat(StarAmount, Night * 0.9f);
            Shader.SetGlobalFloat(NightGlow, Mathf.Lerp(0f, 2.2f, Night) + golden * 0.2f);
            Shader.SetGlobalColor(GlowColor, new Color(1f, 0.72f, 0.38f));

            // Ambient kept low so the sun does the modeling: crisp, contrasty shadows like a sunny diorama.
            RenderSettings.ambientSkyColor = Color.Lerp(new Color(0.12f, 0.14f, 0.26f), Color.Lerp(top, Color.white, 0.25f) * 0.72f, day);
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(0.09f, 0.09f, 0.16f), Color.Lerp(hor, new Color(0.7f, 0.66f, 0.58f), 0.5f) * 0.6f, day);
            RenderSettings.ambientGroundColor = Color.Lerp(new Color(0.05f, 0.05f, 0.07f), new Color(0.3f, 0.26f, 0.2f), day);
            RenderSettings.fogColor = Color.Lerp(hor, top, 0.15f);
        }
    }
}
