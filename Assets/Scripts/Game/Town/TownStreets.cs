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

            // Main Street (row 0) and the side streets.
            for (int r = 0; r <= WorldLayout.PlotRows; r++)
            {
                bool main = r == 0;
                float z = z0 + r * WorldLayout.PlotPitchZ;
                Surface(mb, main, stage, out var col);
                Scenery.RoadStrip(mb, new Vector3(x0 - 2, 0, z), new Vector3(x1 + 2, 0, z), main ? 7f : (stage >= 2 ? 3.2f : 2.6f), col);
            }
            for (int c = 0; c <= WorldLayout.PlotCols; c++)
            {
                float x = x0 + c * WorldLayout.PlotPitchX;
                Surface(mb, false, stage, out var col);
                Scenery.RoadStrip(mb, new Vector3(x, 0, z0), new Vector3(x, 0, z1), stage >= 2 ? 3.2f : 2.6f, col);
            }
            // Station plaza
            Surface(mb, true, stage, out var plaza);
            mb.Box(new Vector3(0, 0.04f, -14.5f), new Vector3(36, 0.08f, 6), MeshBuilder.Darken(plaza, 0.95f));

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

        static void Surface(MeshBuilder mb, bool main, int stage, out Color c)
        {
            bool paved = stage >= 6 || (main && stage >= 5);
            if (paved) { mb.Surf = SurfaceKind.Stone; c = Palette.Hex(0x8C857A); }
            else if (stage >= 2) { mb.Surf = SurfaceKind.Gravel; c = MeshBuilder.Darken(Palette.Road, 0.82f); }
            else { mb.Surf = SurfaceKind.Dirt; c = MeshBuilder.Darken(Palette.Dirt, 0.92f); }
        }
    }
}
