using Ironhorse.Core;
using UnityEngine;

namespace Ironhorse.Game
{
    /// <summary>
    /// The town's streets grow up with the story: each claimed chapter upgrades them.
    ///   0-1 chapters: rutted dirt tracks and hitching posts
    ///   2+: gravel streets and gas street lamps
    ///   4+: plank boardwalks along Main Street
    ///   5+: cobblestone Main Street
    ///   6+: every street paved
    /// </summary>
    public sealed class TownStreets : MonoBehaviour
    {
        public static TownStreets Instance { get; private set; }
        int _built = -1;

        public static int Stage { get { return G.Session != null ? G.Session.State.chaptersClaimed.Count : 0; } }

        void Awake() { Instance = this; }

        void Start()
        {
            Rebuild();
            G.Session.ChapterCompleted += c => Invoke(nameof(Upgrade), 0.4f);
        }

        void Upgrade()
        {
            int before = _built;
            Rebuild();
            string news = StageNews(before, _built);
            if (news != null && UIRoot.Instance != null) UIRoot.Instance.Toast(news, true);
            Fx.Burst(new Vector3(0, 6, 20), 60);
        }

        static string StageNews(int from, int to)
        {
            if (from < 2 && to >= 2) return "Gas lamps now light the streets of " + G.Session.State.townName + "!";
            if (from < 4 && to >= 4) return "Boardwalks laid along Main Street!";
            if (from < 5 && to >= 5) return "Main Street is paved with cobblestones!";
            if (from < 6 && to >= 6) return "Every street in town is paved!";
            return null;
        }

        public void Rebuild()
        {
            int stage = Stage;
            if (stage == _built) return;
            _built = stage;
            foreach (Transform c in transform) Destroy(c.gameObject);

            float x0 = WorldLayout.PlotOriginX - WorldLayout.PlotPitchX / 2, x1 = WorldLayout.PlotOriginX + (WorldLayout.PlotCols - 0.5f) * WorldLayout.PlotPitchX;
            float z0 = WorldLayout.PlotOriginZ - WorldLayout.PlotPitchZ / 2, z1 = WorldLayout.PlotOriginZ + (WorldLayout.PlotRows - 0.5f) * WorldLayout.PlotPitchZ;

            var mb = new MeshBuilder();
            mb.Matte();

            // Dirt and gravel are painted into the terrain (PaintGround); only paved streets are meshes.
            PaintGround(stage, x0, x1, z0, z1);
            for (int r = 0; r <= WorldLayout.PlotRows; r++)
            {
                bool main = r == 0;
                if (!Paved(main, stage)) continue;
                float z = z0 + r * WorldLayout.PlotPitchZ;
                Surface(mb, main, stage, out var col);
                Scenery.RoadStrip(mb, new Vector3(x0 - 2, 0, z), new Vector3(x1 + 2, 0, z), main ? 7f : 3.2f, col);
            }
            if (stage >= 6)
                for (int c = 0; c <= WorldLayout.PlotCols; c++)
                {
                    float x = x0 + c * WorldLayout.PlotPitchX;
                    Surface(mb, false, stage, out var col);
                    Scenery.RoadStrip(mb, new Vector3(x, 0, z0), new Vector3(x, 0, z1), 3.2f, col);
                }
            if (stage >= 5)
            {
                Surface(mb, true, stage, out var plaza);
                mb.Box(new Vector3(0, 0.04f, -14.5f), new Vector3(36, 0.08f, 6), MeshBuilder.Darken(plaza, 0.95f));
            }

            // Boardwalks: raised plank sidewalks on both sides of Main Street.
            if (stage >= 4)
            {
                mb.Surf = SurfaceKind.Planks;
                mb.Matte();
                foreach (float side in new[] { -1f, 1f })
                {
                    float z = z0 + side * 4.6f;
                    mb.Box(new Vector3((x0 + x1) / 2, 0.18f, z), new Vector3(x1 - x0 + 2, 0.22f, 2.0f), Palette.WoodPale);
                    for (float x = x0; x <= x1; x += 4f)
                        mb.Box(new Vector3(x, 0.08f, z + side * 1.0f), new Vector3(0.18f, 0.16f, 0.18f), Palette.WoodDark);
                }
            }

            mb.Surf = SurfaceKind.Auto;
            if (stage < 2)
            {
                // Hitching posts and rails along Main Street.
                for (int c = 0; c <= WorldLayout.PlotCols; c++)
                {
                    float x = x0 + c * WorldLayout.PlotPitchX + 5f;
                    mb.Box(new Vector3(x - 1.1f, 0.55f, z0 + 4.4f), new Vector3(0.16f, 1.1f, 0.16f), Palette.WoodDark);
                    mb.Box(new Vector3(x + 1.1f, 0.55f, z0 + 4.4f), new Vector3(0.16f, 1.1f, 0.16f), Palette.WoodDark);
                    mb.Box(new Vector3(x, 1.0f, z0 + 4.4f), new Vector3(2.4f, 0.12f, 0.12f), Palette.Wood);
                }
            }
            var go = mb.ToObject("Streets", transform, false);
            Station.Walkable(go);

            if (stage >= 2)
            {
                var lamps = new MeshBuilder();
                for (int c = 0; c <= WorldLayout.PlotCols; c++)
                {
                    float x = x0 + c * WorldLayout.PlotPitchX;
                    Lamp(lamps, go.transform, new Vector3(x + 2.4f, 0, z0 - 4.2f));
                    for (int r = 2; r <= WorldLayout.PlotRows; r += 2)
                        if ((c + r / 2) % 2 == 0) Lamp(lamps, go.transform, new Vector3(x + 1.9f, 0, z0 + r * WorldLayout.PlotPitchZ + 1.9f));
                }
                lamps.ToObject("Street Lamps", go.transform);
            }
        }

        static void Lamp(MeshBuilder mb, Transform owner, Vector3 p)
        {
            Station.Lamp(mb, p, light: false);
            NightLights.Attach(owner, p + Vector3.up * 3.9f, LightType.Point, 17f, 9f, NightLights.Lamp);
        }

        static bool Paved(bool main, int stage) { return stage >= 6 || (main && stage >= 5); }

        // ------------------------------------------------------------------ ground mask

        // Mask covers the town grid plus the station plaza, at 4 px per meter.
        const float MaskX0 = -64, MaskZ0 = -24, MaskW = 128, MaskH = 128, PxPerM = 4;
        static Texture2D _mask;

        /// <summary>Shader-style smoothstep: 0 below e0, 1 above e1 (Mathf.SmoothStep interpolates values instead).</summary>
        static float Smooth(float e0, float e1, float x) { float t = Mathf.Clamp01((x - e0) / (e1 - e0)); return t * t * (3 - 2 * t); }

        /// <summary>Paints dirt tracks / gravel streets, worn lots and wheel ruts into the terrain shader's town mask.</summary>
        static void PaintGround(int stage, float x0, float x1, float z0, float z1)
        {
            int w = (int)(MaskW * PxPerM), h = (int)(MaskH * PxPerM);
            if (_mask == null)
                _mask = new Texture2D(w, h, TextureFormat.RGBA32, true, true) { name = "TownMask", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            var px = new Color32[w * h];
            bool gravel = stage >= 2;
            float pitchX = WorldLayout.PlotPitchX, pitchZ = WorldLayout.PlotPitchZ;
            for (int j = 0; j < h; j++)
            {
                float z = MaskZ0 + (j + 0.5f) / PxPerM;
                for (int i = 0; i < w; i++)
                {
                    float x = MaskX0 + (i + 0.5f) / PxPerM;
                    float n = Mathf.PerlinNoise(x * 0.35f, z * 0.35f), n2 = Mathf.PerlinNoise(x * 1.7f + 30, z * 1.7f);
                    float road = 0, rut = 0;
                    bool inGrid = x > x0 - 3 && x < x1 + 3 && z > z0 - 5 && z < z1 + 2;
                    if (inGrid)
                    {
                        // Distance to the nearest east-west street (row 0 = Main Street, wider) and north-south street.
                        float rz = Mathf.Repeat(z - z0 + pitchZ / 2, pitchZ) - pitchZ / 2;
                        bool mainRow = Mathf.Abs(z - z0) < pitchZ / 2;
                        float halfZ = mainRow ? 3.6f : 1.6f;
                        float cx = Mathf.Repeat(x - x0 + pitchX / 2, pitchX) - pitchX / 2;
                        float dz = Mathf.Abs(rz) - halfZ, dx = Mathf.Abs(cx) - 1.6f;
                        float edge = 0.9f + n * 1.4f;                      // ragged, grassy verges
                        float rEW = 1 - Smooth(-edge * 0.5f, edge * 0.5f, dz + (n - 0.5f) * 1.2f);
                        float rNS = z > z1 - 1 ? 0 : 1 - Smooth(-edge * 0.5f, edge * 0.5f, dx + (n - 0.5f) * 1.2f);
                        road = Mathf.Max(rEW, rNS);
                        // Two wagon-wheel ruts along each street.
                        float along = rEW >= rNS ? Mathf.Abs(rz) : Mathf.Abs(cx);
                        float rutAt = mainRow && rEW >= rNS ? 1.8f : 0.75f;
                        rut = road * Mathf.Clamp01(1 - Mathf.Abs(along - rutAt) / 0.28f) * (0.6f + n2 * 0.4f);
                    }
                    // Station plaza in front of the depot.
                    if (x > -19 && x < 19 && z > -18 && z < -11)
                        road = Mathf.Max(road, 1 - Smooth(-0.8f, 0.8f, Mathf.Max(Mathf.Abs(x) - 18, Mathf.Abs(z + 14.5f) - 3.3f) + (n - 0.5f)));
                    // Worn earth on lots (patchy, grass survives at the corners).
                    float lot = 0;
                    if (inGrid && road < 0.95f)
                    {
                        float lx = Mathf.Repeat(x - WorldLayout.PlotOriginX + pitchX / 2, pitchX) - pitchX / 2;
                        float lz = Mathf.Repeat(z - WorldLayout.PlotOriginZ + pitchZ / 2, pitchZ) - pitchZ / 2;
                        float d = Mathf.Max(Mathf.Abs(lx), Mathf.Abs(lz)) / (WorldLayout.PlotSize / 2);
                        lot = Mathf.Clamp01((0.9f - d) * 3f) * Mathf.Clamp01((Mathf.PerlinNoise(x * 0.22f + 7, z * 0.22f) - 0.55f) * 3f) * 0.45f; // a few bare patches
                    }
                    road = Mathf.Clamp01(road);
                    float dirt = gravel ? road * 0.25f : road, grav = gravel ? road * 0.75f : 0;
                    px[j * w + i] = new Color32((byte)(dirt * 255), (byte)(grav * 255), (byte)(Mathf.Clamp01(lot) * (1 - road) * 255), (byte)(rut * 255));
                }
            }
            _mask.SetPixels32(px);
            _mask.Apply(true);
            Shader.SetGlobalTexture("_TownMask", _mask);
            Shader.SetGlobalVector("_TownMaskRect", new Vector4(MaskX0, MaskZ0, 1f / MaskW, 1f / MaskH));
        }

        static void Surface(MeshBuilder mb, bool main, int stage, out Color c)
        {
            bool paved = Paved(main, stage);
            if (paved) { mb.Surf = SurfaceKind.Stone; c = Palette.Hex(0x8C857A); }
            else if (stage >= 2) { mb.Surf = SurfaceKind.Gravel; c = MeshBuilder.Darken(Palette.Road, 0.82f); }
            else { mb.Surf = SurfaceKind.Dirt; c = MeshBuilder.Darken(Palette.Dirt, 0.92f); }
        }
    }
}
