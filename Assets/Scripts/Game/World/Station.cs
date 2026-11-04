using Ironhorse.Core;
using UnityEngine;

namespace Ironhorse.Game
{
    /// <summary>The railway depot, platforms with canopies, freight shed and yard clutter.</summary>
    public static class Station
    {
        public static Vector3 DepotPosition { get { return new Vector3(0, 0, -19f); } }

        public static void Build(Transform root)
        {
            var mb = new MeshBuilder();

            // Main lines; the platform tracks (laid one at a time) belong to StationPlatforms.
            TrackBuilder.Build(mb, WorldLayout.MainLineWest);
            TrackBuilder.Build(mb, WorldLayout.MainLineEast);

            // Side platform in front of the depot + the first island platform (the second comes with platform 4).
            float half = WorldLayout.PlatformHalfLength;
            TrackBuilder.Platform(mb, -24.9f, 4.6f, half);
            float island1 = (WorldLayout.PlatformZ(1) + WorldLayout.PlatformZ(2)) / 2;
            TrackBuilder.Platform(mb, island1, 2.4f, half - 4);
            Canopy(mb, island1, half - 10);

            // Drop-in art (Resources/Models/Buildings/station, freightshed) replaces the procedural versions.
            var depotArt = ModelRegistry.Spawn(ModelRegistry.Buildings, "station", root);
            if (depotArt != null) { depotArt.transform.position = DepotPosition; Walkable(depotArt); }
            else Depot(mb);
            var shedArt = ModelRegistry.Spawn(ModelRegistry.Buildings, "freightshed", root);
            if (shedArt != null) shedArt.transform.position = new Vector3(-36, 0, -16);
            else FreightShed(mb, new Vector3(-36, 0, -16));
            WaterCrane(mb, new Vector3(47, 0, (WorldLayout.PlatformZ(0) + WorldLayout.PlatformZ(1)) / 2));
            WaterCrane(mb, new Vector3(47, 0, (WorldLayout.PlatformZ(4) + WorldLayout.PlatformZ(5)) / 2));
            CoalStage(mb, new Vector3(-58, 0, WorldLayout.PlatformZ(5) - 9));

            // Lamps along the platforms (glow at night).
            for (float x = -40; x <= 40; x += 16)
            {
                Lamp(mb, new Vector3(x, 0.9f, -26.5f));
                Lantern(mb, new Vector3(x + 8, 3.9f, island1));
            }
            // Crates & barrels
            var rng = new System.Random(7);
            for (int i = 0; i < 26; i++)
            {
                var p = new Vector3(-28 + (float)rng.NextDouble() * 8, 0, -21 + (float)rng.NextDouble() * 5);
                if (i % 2 == 0) Crate(mb, p, (float)rng.NextDouble() * 40);
                else Barrel(mb, p);
            }
            Walkable(mb.ToObject("Station", root));
        }

        /// <summary>Layer for surfaces townsfolk stand on (Ignore Raycast: taps and camera rays skip it).</summary>
        public const int WalkLayer = 2;

        /// <summary>Gives every mesh under go a collider on <see cref="WalkLayer"/> so people can find the floor.</summary>
        public static void Walkable(GameObject go)
        {
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null || mf.GetComponent<Collider>() != null) continue;
                var c = mf.gameObject.AddComponent<MeshCollider>();
                c.sharedMesh = mf.sharedMesh;
                mf.gameObject.layer = WalkLayer;
            }
        }

        static void Depot(MeshBuilder mb)
        {
            var c = DepotPosition;
            Color wall = Palette.Hex(0xD9C49A), trim = Palette.Hex(0x6B3E2A), roof = Palette.RoofRed;
            mb.Push().Translate(c);
            mb.Matte();
            // Stone foundation
            mb.Box(new Vector3(0, 0.5f, 0), new Vector3(36, 1f, 8), Palette.StoneDark, Palette.Stone);
            // Wings
            mb.Box(new Vector3(-11, 3.5f, 0), new Vector3(14, 5, 7), wall);
            mb.Box(new Vector3(11, 3.5f, 0), new Vector3(14, 5, 7), wall);
            mb.HipRoof(new Vector3(-11, 6, 0), new Vector3(14, 2.6f, 7), roof, 0.6f, 0.6f);
            mb.HipRoof(new Vector3(11, 6, 0), new Vector3(14, 2.6f, 7), roof, 0.6f, 0.6f);
            // Central tower block
            mb.Box(new Vector3(0, 5, 0), new Vector3(8, 8, 8), MeshBuilder.Lighten(wall, 0.1f));
            mb.GableRoof(new Vector3(0, 9, 0), new Vector3(8, 3.6f, 8), roof, wall, 0.5f);
            mb.RotateY(0);
            // Clock tower cupola
            mb.Box(new Vector3(0, 12.6f, 0), new Vector3(3, 3, 3), wall);
            mb.HipRoof(new Vector3(0, 14.1f, 0), new Vector3(3, 2.6f, 3), Palette.RoofGreen, 0.3f, 0.05f);
            mb.Mat(0.6f, 0.6f);
            mb.Cylinder(new Vector3(0, 12.8f, -1.55f), new Vector3(0, 12.8f, -1.6f), 1.05f, 16, Palette.Brass);
            mb.Matte();
            mb.Cylinder(new Vector3(0, 12.8f, -1.61f), new Vector3(0, 12.8f, -1.66f), 0.9f, 16, Palette.Cream);
            mb.Box(new Vector3(0, 13.05f, -1.7f), new Vector3(0.08f, 0.6f, 0.04f), Palette.Black);
            mb.Box(new Vector3(0.2f, 12.8f, -1.7f), new Vector3(0.45f, 0.08f, 0.04f), Palette.Black);
            // Trim bands
            mb.Box(new Vector3(0, 1.2f, -3.55f), new Vector3(34.2f, 0.4f, 0.12f), trim);
            mb.Box(new Vector3(0, 5.9f, -3.55f), new Vector3(34.2f, 0.25f, 0.12f), trim);
            // Windows and doors (south = platform side, north = plaza side)
            for (int side = -1; side <= 1; side += 2)
            {
                float z = side * 3.52f;
                for (float x = -16; x <= 16; x += 2.8f)
                {
                    if (Mathf.Abs(x) < 3.5f) continue;
                    Window(mb, new Vector3(x, 3.4f, z), side);
                }
                Window(mb, new Vector3(-2, 7.4f, side * 4.02f), side);
                Window(mb, new Vector3(2, 7.4f, side * 4.02f), side);
                mb.Box(new Vector3(0, 2.6f, side * 4.02f), new Vector3(2.4f, 3.2f, 0.12f), trim);
                mb.Lit();
                mb.Box(new Vector3(0, 3.6f, side * 4.06f), new Vector3(1.8f, 0.8f, 0.05f), Palette.Window);
                mb.Matte();
            }
            // Platform-side canopy over the side platform
            for (float x = -16; x <= 16; x += 4)
                mb.Cylinder(new Vector3(x, 1f, -6.9f), new Vector3(x, 4.6f, -6.9f), 0.13f, 6, trim);
            mb.Push().Translate(0, 4.6f, -5.3f).Rotate(Quaternion.Euler(-8, 0, 0));
            mb.Box(Vector3.zero, new Vector3(34, 0.18f, 4.2f), Palette.RoofTin, Palette.Hex(0xA9B1B5));
            mb.Pop();
            // Station sign boards on the canopy fascia
            mb.Box(new Vector3(-10, 5.25f, -7.35f), new Vector3(7, 0.9f, 0.1f), Palette.Black);
            mb.Box(new Vector3(-10, 5.25f, -7.42f), new Vector3(6.6f, 0.6f, 0.05f), Palette.Cream);
            mb.Box(new Vector3(10, 5.25f, -7.35f), new Vector3(7, 0.9f, 0.1f), Palette.Black);
            mb.Box(new Vector3(10, 5.25f, -7.42f), new Vector3(6.6f, 0.6f, 0.05f), Palette.Cream);
            // Chimneys
            mb.Box(new Vector3(-14, 8.2f, 1), new Vector3(0.9f, 2.4f, 0.9f), Palette.Brick);
            mb.Box(new Vector3(14, 8.2f, 1), new Vector3(0.9f, 2.4f, 0.9f), Palette.Brick);
            // Benches on platform
            for (float x = -12; x <= 12; x += 8) Bench(mb, new Vector3(x, 1f, -5.2f));
            mb.Pop();
        }

        public static void Window(MeshBuilder mb, Vector3 p, int side, float w = 1.1f, float h = 1.6f)
        {
            mb.Matte();
            mb.Box(p, new Vector3(w + 0.3f, h + 0.3f, 0.1f), Palette.Hex(0x4A2F20));
            mb.Lit();
            mb.Box(p + new Vector3(0, 0, side * 0.04f), new Vector3(w, h, 0.06f), Palette.Window);
            mb.Matte();
            mb.Box(p + new Vector3(0, 0, side * 0.08f), new Vector3(0.08f, h, 0.03f), Palette.Hex(0x4A2F20));
            mb.Box(p + new Vector3(0, 0, side * 0.08f), new Vector3(w, 0.08f, 0.03f), Palette.Hex(0x4A2F20));
        }

        internal static void Canopy(MeshBuilder mb, float z, float half)
        {
            Color iron = Palette.Hex(0x34483D), oxide = Palette.Hex(0x5A4A42), valance = Palette.Hex(0xD8CBB0);
            float len = half * 2 + 3;
            mb.Mat(0.3f, 0.3f);
            mb.Surf = SurfaceKind.None;
            for (float x = -half; x <= half; x += 6)
            {
                // Cast-iron column with a bracket on each side holding the eaves.
                mb.Cylinder(new Vector3(x, 0.9f, z), new Vector3(x, 4.5f, z), 0.12f, 0.1f, 8, iron);
                mb.Box(new Vector3(x, 1.05f, z), new Vector3(0.36f, 0.3f, 0.36f), iron);
                for (int s = -1; s <= 1; s += 2)
                    mb.Cylinder(new Vector3(x, 3.6f, z), new Vector3(x, 4.45f, z + s * 1.7f), 0.05f, 4, iron, false);
            }
            mb.Box(new Vector3(0, 4.5f, z), new Vector3(len, 0.22f, 0.22f), iron); // ridge girder
            // Pitched, corrugated tin roof, weathered to a rusty grey-brown.
            mb.Matte();
            mb.Surf = SurfaceKind.Tin;
            mb.GableRoof(new Vector3(0, 4.45f, z), new Vector3(len, 0.55f, 4.4f), oxide, oxide, 0.05f);
            // Painted dag-edge valance boards along both eaves.
            mb.Surf = SurfaceKind.Planks;
            for (int s = -1; s <= 1; s += 2)
            {
                mb.Box(new Vector3(0, 4.3f, z + s * 2.27f), new Vector3(len + 0.1f, 0.34f, 0.05f), valance);
                for (float x = -len / 2 + 0.15f; x < len / 2; x += 0.3f)
                    mb.Box(new Vector3(x, 4.08f, z + s * 2.27f), new Vector3(0.14f, 0.12f, 0.05f), valance);
            }
            mb.Surf = SurfaceKind.Auto;
            for (float x = -half + 3; x <= half; x += 12) Bench(mb, new Vector3(x, 0.9f, z));
        }

        static void Bench(MeshBuilder mb, Vector3 p)
        {
            mb.Matte();
            mb.Box(p + new Vector3(0, 0.45f, 0), new Vector3(2, 0.1f, 0.5f), Palette.Wood);
            mb.Box(p + new Vector3(0, 0.85f, 0.22f), new Vector3(2, 0.5f, 0.08f), Palette.Wood);
            mb.Box(p + new Vector3(-0.9f, 0.22f, 0), new Vector3(0.08f, 0.45f, 0.45f), Palette.Iron);
            mb.Box(p + new Vector3(0.9f, 0.22f, 0), new Vector3(0.08f, 0.45f, 0.45f), Palette.Iron);
        }

        /// <summary>Lantern hung from a canopy's ridge girder.</summary>
        internal static void Lantern(MeshBuilder mb, Vector3 p)
        {
            mb.Mat(0.4f, 0.4f);
            mb.Cylinder(p + Vector3.up * 0.3f, p + Vector3.up * 0.6f, 0.02f, 4, Palette.Iron, false);
            mb.Cylinder(p + Vector3.up * 0.22f, p + Vector3.up * 0.34f, 0.2f, 0.05f, 6, Palette.Iron);
            mb.Lit();
            mb.Box(p, new Vector3(0.26f, 0.42f, 0.26f), Palette.Window);
            mb.Matte();
            NightLights.AddPoint(p);
        }

        public static void Lamp(MeshBuilder mb, Vector3 p, bool light = true)
        {
            mb.Mat(0.4f, 0.4f);
            mb.Cylinder(p, p + Vector3.up * 3.6f, 0.08f, 6, Palette.Iron);
            mb.Box(p + Vector3.up * 3.7f, new Vector3(0.4f, 0.1f, 0.4f), Palette.Iron);
            mb.Lit();
            mb.Box(p + Vector3.up * 4.0f, new Vector3(0.32f, 0.5f, 0.32f), Palette.Window);
            if (light) NightLights.AddPoint(p + Vector3.up * 3.9f);
            mb.Mat(0.4f, 0.4f);
            mb.Cylinder(p + Vector3.up * 4.25f, p + Vector3.up * 4.55f, 0.28f, 0f, 6, Palette.Iron);
            mb.Matte();
        }

        static void FreightShed(MeshBuilder mb, Vector3 p)
        {
            mb.Push().Translate(p);
            mb.Matte();
            mb.Box(new Vector3(0, 0.6f, 0), new Vector3(14, 1.2f, 7), Palette.WoodDark, Palette.WoodPale);
            mb.Box(new Vector3(0, 3.2f, 0), new Vector3(12, 4, 6), Palette.PaintRed);
            mb.GableRoof(new Vector3(0, 5.2f, 0), new Vector3(12, 2.2f, 6), Palette.RoofTin, Palette.PaintRed, 0.5f);
            for (int s = -1; s <= 1; s += 2)
            {
                mb.Box(new Vector3(-3, 2.8f, s * 3.03f), new Vector3(3, 3.2f, 0.1f), Palette.Hex(0x5A1F18));
                mb.Box(new Vector3(3, 2.8f, s * 3.03f), new Vector3(3, 3.2f, 0.1f), Palette.Hex(0x5A1F18));
            }
            mb.Pop();
        }

        static void WaterCrane(MeshBuilder mb, Vector3 p)
        {
            mb.Mat(0.35f, 0.4f);
            mb.Cylinder(p, p + Vector3.up * 4.2f, 0.25f, 0.2f, 8, Palette.Iron);
            mb.Cylinder(p + Vector3.up * 4f, p + new Vector3(0, 4.4f, 1.8f), 0.16f, 6, Palette.Iron);
            mb.Cylinder(p + new Vector3(0, 4.4f, 1.8f), p + new Vector3(0, 3.0f, 2.1f), 0.18f, 0.12f, 6, Palette.Hex(0x3A3A3A));
            mb.Matte();
        }

        static void CoalStage(MeshBuilder mb, Vector3 p)
        {
            mb.Push().Translate(p);
            mb.Matte();
            for (int i = 0; i < 4; i++)
                for (int k = -1; k <= 1; k += 2)
                    mb.Cylinder(new Vector3(-6 + i * 4, 0, k * 2.4f), new Vector3(-6 + i * 4, 6, k * 2.4f), 0.2f, 5, Palette.WoodDark);
            mb.Box(new Vector3(0, 7, 0), new Vector3(14, 2.2f, 6), Palette.Weathered);
            mb.Box(new Vector3(0, 8.3f, 0), new Vector3(13, 0.6f, 5), Palette.Black);
            mb.Pop();
        }

        public static void Crate(MeshBuilder mb, Vector3 p, float rot)
        {
            // Plank box with darker corner posts and rims, weathered to a mid brown.
            mb.Matte();
            float h = Mathf.Repeat(p.x * 7.3f + p.z * 3.1f, 1f);
            Color panel = MeshBuilder.Lerp(Palette.Hex(0x8E6E4C), Palette.Hex(0x9A8A72), h), frame = Palette.Hex(0x5A4130);
            mb.Push().Translate(p).RotateY(rot);
            mb.Surf = SurfaceKind.Planks;
            mb.Box(new Vector3(0, 0.45f, 0), new Vector3(0.86f, 0.86f, 0.86f), panel);
            mb.Surf = SurfaceKind.WoodGrain;
            for (int i = 0; i < 4; i++)
            {
                float x = (i & 1) == 0 ? -0.43f : 0.43f, z = (i & 2) == 0 ? -0.43f : 0.43f;
                mb.Box(new Vector3(x, 0.45f, z), new Vector3(0.1f, 0.9f, 0.1f), frame);
            }
            foreach (float y in new[] { 0.05f, 0.85f })
            {
                mb.Box(new Vector3(0, y, -0.43f), new Vector3(0.9f, 0.1f, 0.08f), frame);
                mb.Box(new Vector3(0, y, 0.43f), new Vector3(0.9f, 0.1f, 0.08f), frame);
                mb.Box(new Vector3(-0.43f, y, 0), new Vector3(0.08f, 0.1f, 0.9f), frame);
                mb.Box(new Vector3(0.43f, y, 0), new Vector3(0.08f, 0.1f, 0.9f), frame);
            }
            mb.Surf = SurfaceKind.Auto;
            mb.Pop();
        }

        public static void Barrel(MeshBuilder mb, Vector3 p)
        {
            // Bulging oak staves, iron hoops, a darker lid.
            mb.Matte();
            float h = Mathf.Repeat(p.x * 5.1f + p.z * 9.7f, 1f);
            Color oak = MeshBuilder.Lerp(Palette.Hex(0x6E4A2E), Palette.Hex(0x7E6A52), h);
            mb.Surf = SurfaceKind.WoodGrain;
            mb.Cylinder(p, p + Vector3.up * 0.55f, 0.36f, 0.43f, 12, oak, false);
            mb.Cylinder(p + Vector3.up * 0.55f, p + Vector3.up * 1.1f, 0.43f, 0.36f, 12, oak, false);
            mb.Surf = SurfaceKind.None;
            mb.Cylinder(p + Vector3.up * 1.06f, p + Vector3.up * 1.08f, 0.33f, 12, MeshBuilder.Darken(oak, 0.7f));
            mb.Mat(0.35f, 0.5f);
            foreach (var (y, r) in new[] { (0.12f, 0.38f), (0.36f, 0.42f), (0.74f, 0.42f), (0.98f, 0.38f) })
                mb.Cylinder(p + Vector3.up * (y - 0.03f), p + Vector3.up * (y + 0.03f), r + 0.01f, 12, Palette.Hex(0x2E2A26), false);
            mb.Matte();
            mb.Surf = SurfaceKind.Auto;
        }
    }
}
