using System;
using System.IO;
using Ironhorse.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ironhorse.Game
{
    /// <summary>Global access to the running game.</summary>
    public static class G
    {
        public static GameSession Session;
    }

    /// <summary>Wall-clock time in unix seconds, plus a debug offset for skipping ahead.</summary>
    public sealed class UnityClock : IClock
    {
        public double Offset;
        public double Now { get { return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0 + Offset; } }
    }

    /// <summary>JSON save file in the app's persistent data folder, written atomically.</summary>
    public static class SaveSystem
    {
        public static string PathFor(string name) { return Path.Combine(Application.persistentDataPath, name); }
        static string Main { get { return PathFor("ironhorse_save.json"); } }

        public static GameState Load()
        {
            foreach (var path in new[] { Main, Main + ".bak" })
            {
                try
                {
                    if (!File.Exists(path)) continue;
                    var s = JsonUtility.FromJson<GameState>(File.ReadAllText(path));
                    if (s != null && s.trains != null && s.trains.Count > 0) return s;
                }
                catch (Exception e) { Debug.LogWarning("Save load failed for " + path + ": " + e.Message); }
            }
            return null;
        }

        public static void Save(GameState s)
        {
            try
            {
                string json = JsonUtility.ToJson(s);
                string tmp = Main + ".tmp";
                File.WriteAllText(tmp, json);
                if (File.Exists(Main)) File.Copy(Main, Main + ".bak", true);
                if (File.Exists(Main)) File.Delete(Main);
                File.Move(tmp, Main);
            }
            catch (Exception e) { Debug.LogError("Save failed: " + e.Message); }
        }

        public static void Delete()
        {
            foreach (var p in new[] { Main, Main + ".bak", Main + ".tmp" })
                if (File.Exists(p)) File.Delete(p);
        }
    }

    /// <summary>
    /// Creates the entire game at runtime — no scene setup required. Works in an empty scene
    /// (or Unity's default SampleScene; its camera and light are replaced).
    /// </summary>
    public sealed class GameRoot : MonoBehaviour
    {
        public static GameRoot Instance { get; private set; }
        public UnityClock Clock { get; private set; }
        float _saveTimer, _tickTimer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded += (scene, mode) => { if (Instance == null) Create(); };
            if (Instance == null) Create();
        }

        static void Create()
        {
            // Replace the template scene's camera and light with our own.
            foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None)) Destroy(cam.gameObject);
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None)) Destroy(light.gameObject);
            new GameObject("Ironhorse Frontier").AddComponent<GameRoot>();
        }

        void Awake()
        {
            Instance = this;
            QualitySetup.Apply();
            Clock = new UnityClock();
            var state = SaveSystem.Load();
            bool fresh = state == null;
            if (fresh) state = GameSession.NewGame(Clock.Now, Environment.TickCount);
            G.Session = new GameSession(state, Clock);
            G.Session.UtcOffsetSeconds = TimeZoneInfo.Local.GetUtcOffset(DateTime.Now).TotalSeconds;
            AudioManager.MusicOn = state.settings.music;
            AudioManager.SfxOn = state.settings.sfx;
            Haptics.On = state.settings.haptics;

            _loading = LoadingScreen.Show();
            StartCoroutine(BuildWorld(state, fresh));
        }

        LoadingScreen _loading;

        /// <summary>Builds the world over several frames so the loading screen can draw and animate.</summary>
        System.Collections.IEnumerator BuildWorld(GameState state, bool fresh)
        {
            _loading.SetProgress(0.05f, "Surveying the valley...");
            yield return null;
            var world = new GameObject("World").transform;
            world.SetParent(transform, false);
            ValleyTerrain.Build(world);
            ValleyTerrain.BuildWater(world);
            _loading.SetProgress(0.3f, "Planting the pines...");
            yield return null;
            Scenery.Build(world);
            _loading.SetProgress(0.55f, "Laying track...");
            yield return null;
            Yard.Build(world);
            new GameObject("Platforms", typeof(StationPlatforms)).transform.SetParent(world, false);
            var loop = new MeshBuilder();
            TrackBuilder.Build(loop, WorldLayout.ScenicLoop, 0, -1, trestleOverWater: true);
            loop.ToObject("Scenic Loop", world);
            Child<Clouds>("Clouds");
            _loading.SetProgress(0.7f, "Firing up the boilers...");
            yield return null;

            Child<DayNightCycle>("Day & Night");
            Child<CameraRig>("Camera");
            Child<ProjectSites>("Project Sites");
            Child<Museum>("Museum");
            Child<PostFx>("Post FX");
            Child<Life>("Townsfolk");
            Child<AudioManager>("Audio");
            Child<TrainManager>("Trains");
            _loading.SetProgress(0.85f, "Raising the town...");
            yield return null;
            Child<TownView>("Town");
            Child<TownStreets>("Streets");
            Child<UIRoot>("UI");
            Child<PlatformServices>("Apple Services");

            yield return null; // let Start() run on everything before revealing the world
            if (PendingWelcome != null)
            {
                string msg = PendingWelcome;
                PendingWelcome = null;
                StartCoroutine(Delayed(1.5f, () => Celebrations.Instance.Play("project", "Welcome to " + state.townName + "!", msg, force: true)));
            }
            else if (!fresh) WelcomeBack(state);
            else Invoke(nameof(Intro), 2.2f);
            yield return _loading.Finish();
        }

        T Child<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.AddComponent<T>();
        }

        void Intro()
        {
            UIRoot.Instance.Toast("Welcome to " + G.Session.State.townName + "! Tap Trains to send Dusty out for timber.", true);
        }

        void WelcomeBack(GameState state)
        {
            double away = Clock.Now - state.lastSavedAt;
            UIRoot.Instance.QueueWelcome(); // the away summary and today's payday
            if (away < 60 || G.Session.Away != null) return;
            int arrived = 0;
            foreach (var t in state.trains) if (!t.IsIdle && Clock.Now >= t.arrivesAt) arrived++;
            string msg = "Welcome back! You were away " + Fmt.Time(away) + ".";
            if (arrived > 0) msg += " " + arrived + " train" + (arrived > 1 ? "s have" : " has") + " arrived.";
            StartCoroutine(Delayed(1f, () => UIRoot.Instance.Toast(msg, true)));
        }

        System.Collections.IEnumerator Delayed(float s, Action a) { yield return new WaitForSeconds(s); a(); }

        void Update()
        {
            _tickTimer -= Time.unscaledDeltaTime;
            if (_tickTimer <= 0) { _tickTimer = 0.25f; G.Session.Tick(); }
            _saveTimer -= Time.unscaledDeltaTime;
            if (_saveTimer <= 0) { _saveTimer = 20f; Save(); }
        }

        public void Save()
        {
            G.Session.State.lastSavedAt = Clock.Now;
            SaveSystem.Save(G.Session.State);
        }

        void OnApplicationPause(bool paused) { if (paused) Save(); }
        void OnApplicationQuit() { Save(); }

        /// <summary>Set before <see cref="Reload"/> to greet the player in the rebuilt world.</summary>
        public static string PendingWelcome;

        /// <summary>Saves and rebuilds the whole world from the save (after a new-territory charter).</summary>
        public void Reload()
        {
            Save();
            Instance = null;
            enabled = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void ResetGame()
        {
            SaveSystem.Delete();
            Instance = null;
            enabled = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    /// <summary>Rendering defaults tuned for modern iPhones.</summary>
    public static class QualitySetup
    {
        public static void Apply()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            QualitySettings.antiAliasing = 4;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.High;
            QualitySettings.shadowProjection = ShadowProjection.StableFit;
            QualitySettings.shadowDistance = 260;
            QualitySettings.shadowCascades = 2;
            QualitySettings.shadowCascade2Split = 0.3f;
            QualitySettings.pixelLightCount = 2;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
            QualitySettings.lodBias = 1.5f;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }
    }

    /// <summary>Puffy low-poly clouds that drift across the valley and cast soft shadows.</summary>
    public sealed class Clouds : MonoBehaviour
    {
        readonly System.Collections.Generic.List<Transform> _clouds = new System.Collections.Generic.List<Transform>();
        MaterialPropertyBlock _block;

        void Start()
        {
            var rng = new System.Random(11);
            // Soft painted cloud sprites (UI/Fx/clouds.png, 3 variants stacked) lying flat high over the
            // valley: from the high camera they read as drifting wisps, not solid lumps.
            var tex = Resources.Load<Texture2D>("UI/Fx/clouds");
            var mat = new Material(Shader.Find("Ironhorse/Smoke")) { mainTexture = tex, name = "Clouds", renderQueue = 3100 };
            for (int i = 0; i < 9; i++)
            {
                int v = rng.Next(3);
                float w = 70 + (float)rng.NextDouble() * 60, h = w * 0.5f;
                var mesh = new Mesh { name = "Cloud" };
                mesh.vertices = new[] { new Vector3(-w / 2, 0, -h / 2), new Vector3(w / 2, 0, -h / 2), new Vector3(w / 2, 0, h / 2), new Vector3(-w / 2, 0, h / 2) };
                float v0 = v / 3f, v1 = (v + 1) / 3f;
                mesh.uv = new[] { new Vector2(0, v0), new Vector2(1, v0), new Vector2(1, v1), new Vector2(0, v1) };
                var alpha = new Color(1, 1, 1, 0.4f + (float)rng.NextDouble() * 0.2f);
                mesh.colors = new[] { alpha, alpha, alpha, alpha };
                mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                mesh.RecalculateBounds();
                var go = new GameObject("Cloud");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                go.transform.position = new Vector3((float)rng.NextDouble() * 900 - 450, 150 + (float)rng.NextDouble() * 40, (float)rng.NextDouble() * 700 - 250);
                go.transform.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 40 - 20, 0);
                _clouds.Add(go.transform);
            }
        }

        void Update()
        {
            // Only part of the far, zoomed-out panorama, and kept off the middle of the view: a cloud fades
            // out as it drifts over the point the camera looks at.
            var rig = CameraRig.Instance;
            bool show = rig == null || rig.Distance > 190;
            Vector3 focus = rig != null ? rig.Focus : Vector3.zero;
            float zoomFade = rig == null ? 1 : Mathf.InverseLerp(190, 300, rig.Distance);
            foreach (var c in _clouds)
            {
                c.gameObject.SetActive(show);
                c.position += new Vector3(Fx.Wind.x, 0, Fx.Wind.z) * 1.6f * Time.deltaTime;
                if (c.position.x > 480) c.position -= new Vector3(960, 0, 0);
                if (c.position.z > 470) c.position -= new Vector3(0, 0, 740);
                if (!show) continue;
                float d = new Vector2(c.position.x - focus.x, c.position.z - focus.z).magnitude;
                var mr = c.GetComponent<MeshRenderer>();
                if (_block == null) _block = new MaterialPropertyBlock();
                _block.SetColor("_Color", new Color(1, 1, 1, zoomFade * Mathf.InverseLerp(60, 170, d)));
                mr.SetPropertyBlock(_block);
            }
        }
    }
}
