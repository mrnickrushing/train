using System.Collections.Generic;
using UnityEngine;

namespace Ironhorse.Game
{
    /// <summary>Static set dressing: mesas and tunnels, buttes, vegetation, roads, telegraph lines, windmills, the station.</summary>
    public static class Scenery
    {
        static System.Random _rng;
        static float R() { return (float)_rng.NextDouble(); }
        static float R(float a, float b) { return a + (b - a) * R(); }

        public static void Build(Transform root)
        {
            _rng = new System.Random(1869);
            BuildMesas(root);
            BuildVegetation(root);
            BuildRoads(root);
            BuildTelegraph(root);
            BuildWindmills(root);
            Station.Build(root);
        }

        // ---------------------------------------------------------------- Mesas & tunnels

        static void BuildMesas(Transform root)
        {
            var mb = new MeshBuilder();
            float mz = WorldLayout.MainZ;
            Mesa(mb, new Vector2(WorldLayout.TunnelX + 52, mz), 52, 38, 180f, 7);
            Mesa(mb, new Vector2(-WorldLayout.TunnelX - 52, mz), 52, 34, 0f, 8);
            Portal(mb, WorldLayout.TunnelX - 0.2f, mz, -1);
            Portal(mb, -WorldLayout.TunnelX + 0.2f, mz, 1);

            // Desert buttes and mesas scattered to the south and on the rim.
            int placed = 0, guard = 0;
            var spots = new List<Vector2>();
            while (placed < 14 && guard++ < 400)
            {
                var p = new Vector2(R(-320, 320), R(-290, -95));
                if (Mathf.Abs(p.x) > 170 && Mathf.Abs(p.y - mz) < 70) continue;
                bool near = false;
                foreach (var s in spots) if (Vector2.Distance(s, p) < 70) near = true;
                if (near) continue;
                spots.Add(p);
                float radius = R(14, 40);
                float height = R(18, 48) * (radius < 22 ? 1.4f : 1f);
                Mesa(mb, p, radius, height, -999, 1000 + placed);
                placed++;
            }
            mb.ToObject("Mesas", root);
        }

        /// <summary>Layered sandstone mesa. faceDeg = angle (degrees) of a flat cliff face for a tunnel, or -999.</summary>
        static void Mesa(MeshBuilder mb, Vector2 c, float radius, float height, float faceDeg, int seed)
        {
            const int sides = 28;
            int layers = Mathf.Clamp(Mathf.RoundToInt(height / 6f), 3, 9);
            float lh = height / layers;
            float baseY = -3;
            Color[] bands = { Palette.RedRock, Palette.RockPale, Palette.RockDark, Palette.RedRock, Palette.Hex(0xC7845A), Palette.RockPale, Palette.RedRock, Palette.RockDark, Palette.RockPale };
            float shrink = 1f;
            for (int l = 0; l < layers; l++)
            {
                var poly = new List<Vector2>();
                for (int i = 0; i < sides; i++)
                {
                    float a = i / (float)sides * Mathf.PI * 2;
                    float n = Mathf.PerlinNoise(seed * 3.1f + Mathf.Cos(a) * 1.4f, seed * 1.7f + Mathf.Sin(a) * 1.4f + l * 0.15f);
                    float r = radius * shrink * (0.82f + n * 0.36f);
                    if (faceDeg > -900)
                    {
                        float d = Mathf.DeltaAngle(a * Mathf.Rad2Deg, faceDeg);
                        if (Mathf.Abs(d) < 32) r = radius * shrink / Mathf.Cos(d * Mathf.Deg2Rad);
                    }
                    poly.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
                }
                float y0 = baseY + l * lh, y1 = baseY + (l + 1) * lh;
                Color top = l == layers - 1 ? Palette.GrassDry : bands[(l + seed) % bands.Length];
                mb.Matte();
                mb.Prism(poly, y0, y1, bands[(l + seed) % bands.Length], top, 0.025f);
                shrink *= l < 2 ? 1f : 0.965f;
            }
        }

        static void Portal(MeshBuilder mb, float x, float z, int facing)
        {
            // facing: +1 = opening faces +x (west mesa), -1 = faces -x (east mesa)
            mb.Push().SetMatrix(Matrix4x4.TRS(new Vector3(x, 0, z), Quaternion.Euler(0, facing > 0 ? 90 : -90, 0), Vector3.one));
            mb.Matte();
            Color stone = Palette.Stone, dark = Palette.StoneDark;
            // Local frame: +z points out of the mountain toward the valley, x across the track.
            mb.Box(new Vector3(-4.2f, 3.5f, 0.6f), new Vector3(1.8f, 7f, 1.6f), stone);
            mb.Box(new Vector3(4.2f, 3.5f, 0.6f), new Vector3(1.8f, 7f, 1.6f), stone);
            // Arch voussoirs.
            for (int i = 0; i <= 8; i++)
            {
                float a = Mathf.PI * i / 8f;
                var p = new Vector3(Mathf.Cos(a) * 3.6f, 6.6f + Mathf.Sin(a) * 2.4f, 0.6f);
                mb.Push().Translate(p).Rotate(Quaternion.Euler(0, 0, a * Mathf.Rad2Deg - 90));
                mb.Box(Vector3.zero, new Vector3(1.0f, 1.3f, 1.7f), i % 2 == 0 ? stone : dark);
                mb.Pop();
            }
            mb.Box(new Vector3(0, 9.8f, 0.6f), new Vector3(10.5f, 1.4f, 1.8f), dark);
            // Dark opening
            mb.Mat(0, 0);
            mb.Quad(new Vector3(3.3f, 0, 0.1f), new Vector3(3.3f, 6.6f, 0.1f), new Vector3(-3.3f, 6.6f, 0.1f), new Vector3(-3.3f, 0, 0.1f), new Color(0.03f, 0.02f, 0.02f));
            for (int i = 0; i < 8; i++)
            {
                float a0 = Mathf.PI * i / 8f, a1 = Mathf.PI * (i + 1) / 8f;
                mb.Tri(new Vector3(0, 6.6f, 0.1f), new Vector3(Mathf.Cos(a0) * 3.3f, 6.6f + Mathf.Sin(a0) * 2.1f, 0.1f), new Vector3(Mathf.Cos(a1) * 3.3f, 6.6f + Mathf.Sin(a1) * 2.1f, 0.1f), new Color(0.03f, 0.02f, 0.02f));
            }
            mb.Matte();
            mb.Pop();
        }

        // ---------------------------------------------------------------- Vegetation

        static void BuildVegetation(Transform root)
        {
            const float chunk = 120f;
            var chunks = new Dictionary<Vector2Int, MeshBuilder>();
            MeshBuilder Chunk(Vector3 p)
            {
                var key = new Vector2Int(Mathf.FloorToInt(p.x / chunk), Mathf.FloorToInt(p.z / chunk));
                MeshBuilder b;
                if (!chunks.TryGetValue(key, out b)) chunks[key] = b = new MeshBuilder();
                return b;
            }

            for (int i = 0; i < 5200; i++)
            {
                float x = R(ValleyTerrain.MinX + 10, ValleyTerrain.MaxX - 10), z = R(ValleyTerrain.MinZ + 10, ValleyTerrain.MaxZ - 10);
                float wild = ValleyTerrain.Wildness(x, z);
                float depth;
                float water = ValleyTerrain.WaterMask(x, z, out depth);
                if (water > 0.25f) continue;
                float h = ValleyTerrain.Height(x, z);
                float slope = Mathf.Abs(ValleyTerrain.Height(x + 2, z) - h) + Mathf.Abs(ValleyTerrain.Height(x, z + 2) - h);
                if (slope > 3.2f || h > 75) continue;
                float north = Mathf.InverseLerp(-120, 180, z);
                float clump = Mathf.PerlinNoise(x * 0.02f + 100, z * 0.02f);
                var p = new Vector3(x, h - 0.1f, z);
                bool nearRiver = water > 0.02f || Mathf.Sqrt(WorldLayout.River.SqrDistanceXZ(x, z)) < 22;

                if (wild < 0.12f)
                {
                    // Valley floor: keep it open, just a few shrubs well away from tracks and town.
                    if (wild > 0.04f && R() < 0.25f) Sage(Chunk(p), p, R(0.6f, 1.1f));
                    continue;
                }
                if (nearRiver && R() < 0.6f) { Cottonwood(Chunk(p), p, R(0.9f, 1.4f)); continue; }
                if (north > 0.55f && clump > 0.45f && R() < 0.85f) { Pine(Chunk(p), p, R(0.8f, 1.5f) * (h > 20 ? 1.15f : 1f)); continue; }
                if (north < 0.45f)
                {
                    float r = R();
                    if (r < 0.12f) Saguaro(Chunk(p), p, R(0.8f, 1.3f));
                    else if (r < 0.55f) Sage(Chunk(p), p, R(0.6f, 1.2f));
                    else if (r < 0.7f) Rock(Chunk(p), p, R(0.6f, 2.4f));
                    continue;
                }
                float q = R();
                if (q < 0.3f) Sage(Chunk(p), p, R(0.7f, 1.2f));
                else if (q < 0.4f) Rock(Chunk(p), p, R(0.5f, 1.8f));
                else if (q < 0.55f && clump > 0.35f) Pine(Chunk(p), p, R(0.8f, 1.3f));
            }

            foreach (var kv in chunks)
            {
                var go = kv.Value.ToObject("Vegetation " + kv.Key, root);
                go.isStatic = true;
            }
        }

        public static void Pine(MeshBuilder mb, Vector3 p, float s)
        {
            mb.Matte();
            float hue = R(-0.04f, 0.04f);
            Color c1 = Shift(Palette.Pine, hue), c2 = Shift(Palette.PineDark, hue);
            mb.Cylinder(p, p + Vector3.up * 2.2f * s, 0.28f * s, 0.2f * s, 5, Palette.Bark, false);
            mb.Cylinder(p + Vector3.up * 1.4f * s, p + Vector3.up * 5.2f * s, 2.3f * s, 0, 7, c2, true);
            mb.Cylinder(p + Vector3.up * 3.3f * s, p + Vector3.up * 7.0f * s, 1.8f * s, 0, 7, c1, true);
            mb.Cylinder(p + Vector3.up * 5.1f * s, p + Vector3.up * 8.6f * s, 1.25f * s, 0, 7, Color.Lerp(c1, Palette.Grass, 0.15f), true);
        }

        public static void Cottonwood(MeshBuilder mb, Vector3 p, float s)
        {
            mb.Matte();
            mb.Cylinder(p, p + Vector3.up * 3.4f * s, 0.4f * s, 0.25f * s, 6, Palette.Bark, false);
            Color leaf = Shift(Palette.Hex(0x6E8C3E), R(-0.05f, 0.05f));
            mb.Sphere(p + new Vector3(0, 4.6f, 0) * s, new Vector3(2.6f, 2.0f, 2.6f) * s, 7, leaf, false);
            mb.Sphere(p + new Vector3(1.3f, 5.4f, 0.5f) * s, new Vector3(1.8f, 1.5f, 1.8f) * s, 6, MeshBuilder.Lighten(leaf, 0.1f), false);
            mb.Sphere(p + new Vector3(-1.1f, 5.2f, -0.7f) * s, new Vector3(1.7f, 1.4f, 1.7f) * s, 6, MeshBuilder.Darken(leaf, 0.9f), false);
        }

        public static void Saguaro(MeshBuilder mb, Vector3 p, float s)
        {
            mb.Mat(0.2f);
            Color c = Shift(Palette.Cactus, R(-0.03f, 0.03f));
            float h = 5.5f * s;
            mb.Cylinder(p, p + Vector3.up * h, 0.38f * s, 0.34f * s, 8, c, false);
            mb.Sphere(p + Vector3.up * h, new Vector3(0.34f, 0.3f, 0.34f) * s, 8, c);
            float ang = R(0, 360);
            for (int arm = 0; arm < 2; arm++)
            {
                var dir = Quaternion.Euler(0, ang + arm * R(140, 220), 0) * Vector3.right;
                float ay = h * R(0.35f, 0.6f);
                var a = p + Vector3.up * ay;
                var b = a + dir * 1.1f * s;
                var top = b + Vector3.up * R(1.2f, 2.2f) * s;
                mb.Cylinder(a, b, 0.24f * s, 0.24f * s, 6, c, false);
                mb.Cylinder(b - Vector3.up * 0.2f * s, top, 0.24f * s, 0.22f * s, 6, c, false);
                mb.Sphere(top, new Vector3(0.22f, 0.2f, 0.22f) * s, 6, c);
            }
        }

        public static void Sage(MeshBuilder mb, Vector3 p, float s)
        {
            mb.Matte();
            Color c = Shift(Palette.Sage, R(-0.05f, 0.05f));
            mb.Sphere(p + Vector3.up * 0.35f * s, new Vector3(0.9f, 0.6f, 0.8f) * s, 6, c, false);
            mb.Sphere(p + new Vector3(0.5f, 0.3f, 0.3f) * s, new Vector3(0.6f, 0.45f, 0.6f) * s, 5, MeshBuilder.Darken(c, 0.9f), false);
        }

        public static void Rock(MeshBuilder mb, Vector3 p, float s)
        {
            mb.Matte();
            Color c = Color.Lerp(Palette.RedRock, Palette.StoneDark, R());
            mb.Push().Translate(p).RotateY(R(0, 360));
            mb.Sphere(new Vector3(0, 0.3f * s, 0), new Vector3(1.1f, 0.7f, 0.9f) * s, 5, c, false);
            mb.Pop();
        }

        static Color Shift(Color c, float amount) { return new Color(Mathf.Clamp01(c.r + amount), Mathf.Clamp01(c.g + amount * 0.6f), Mathf.Clamp01(c.b + amount * 0.3f)); }

        // ---------------------------------------------------------------- Roads

        static void BuildRoads(Transform root)
        {
            var mb = new MeshBuilder();
            mb.Matte();
            float x0 = WorldLayout.PlotOriginX - WorldLayout.PlotPitchX / 2, x1 = WorldLayout.PlotOriginX + (WorldLayout.PlotCols - 0.5f) * WorldLayout.PlotPitchX;
            float z0 = WorldLayout.PlotOriginZ - WorldLayout.PlotPitchZ / 2, z1 = WorldLayout.PlotOriginZ + (WorldLayout.PlotRows - 0.5f) * WorldLayout.PlotPitchZ;
            Color road = Palette.Road;
            for (int r = 0; r <= WorldLayout.PlotRows; r++)
            {
                float z = z0 + r * WorldLayout.PlotPitchZ;
                float w = r == 0 ? 7f : 3.6f; // Main Street is wide
                RoadStrip(mb, new Vector3(x0 - 2, 0, z), new Vector3(x1 + 2, 0, z), w, road);
            }
            for (int c = 0; c <= WorldLayout.PlotCols; c++)
            {
                float x = x0 + c * WorldLayout.PlotPitchX;
                RoadStrip(mb, new Vector3(x, 0, z0), new Vector3(x, 0, z1), 3.6f, road);
            }
            // Station plaza between Main Street and the depot.
            mb.Box(new Vector3(0, 0.04f, -14.5f), new Vector3(36, 0.08f, 6), MeshBuilder.Darken(road, 0.95f));
            mb.ToObject("Roads", root, false);
        }

        static void RoadStrip(MeshBuilder mb, Vector3 a, Vector3 b, float width, Color c)
        {
            var dir = (b - a).normalized;
            var right = Vector3.Cross(Vector3.up, dir) * width / 2;
            float len = Vector3.Distance(a, b);
            int n = Mathf.Max(1, Mathf.CeilToInt(len / 6f));
            for (int i = 0; i < n; i++)
            {
                var p0 = Vector3.Lerp(a, b, i / (float)n) + Vector3.up * 0.06f;
                var p1 = Vector3.Lerp(a, b, (i + 1) / (float)n) + Vector3.up * 0.06f;
                var tint = Color.Lerp(c, MeshBuilder.Darken(c, 0.88f), Mathf.PerlinNoise(p0.x * 0.1f, p0.z * 0.1f));
                mb.Quad(p0 - right, p1 - right, p1 + right, p0 + right, tint);
            }
        }

        // ---------------------------------------------------------------- Telegraph line

        static void BuildTelegraph(Transform root)
        {
            var mb = new MeshBuilder();
            float z = WorldLayout.FirstPlatformZ + 14f;
            Vector3? prev = null;
            for (float x = -WorldLayout.TunnelX + 12; x <= WorldLayout.TunnelX - 12; x += 24)
            {
                // Run the line south of the tracks wherever town, yard or roundhouse occupy the north side.
                bool inTown = Mathf.Abs(x) < 64 || (x > 50 && x < 186) || (x > -186 && x < -112);
                float pz = inTown ? WorldLayout.PlatformZ(5) - 8 : z;
                var top = new Vector3(x, 7.2f, pz);
                mb.Matte();
                mb.Cylinder(new Vector3(x, 0, pz), top + Vector3.up * 0.3f, 0.16f, 0.13f, 5, Palette.WoodDark, true);
                mb.Box(top + Vector3.up * -0.2f, new Vector3(0.2f, 0.18f, 1.8f), Palette.WoodDark);
                mb.Mat(0.6f);
                mb.Box(top + new Vector3(0, 0, -0.7f), new Vector3(0.12f, 0.2f, 0.12f), Palette.Hex(0x7FB3A6));
                mb.Box(top + new Vector3(0, 0, 0.7f), new Vector3(0.12f, 0.2f, 0.12f), Palette.Hex(0x7FB3A6));
                if (prev.HasValue && Mathf.Abs(prev.Value.z - pz) < 0.1f)
                {
                    for (int k = -1; k <= 1; k += 2)
                    {
                        var a = prev.Value + new Vector3(0, 0, 0.7f * k);
                        var b = top + new Vector3(0, 0, 0.7f * k);
                        var mid = (a + b) / 2 + Vector3.down * 0.5f;
                        Wire(mb, a, mid); Wire(mb, mid, b);
                    }
                }
                prev = top;
            }
            mb.ToObject("Telegraph", root);
        }

        static void Wire(MeshBuilder mb, Vector3 a, Vector3 b)
        {
            mb.Mat(0.3f, 0.5f);
            mb.Cylinder(a, b, 0.03f, 3, Palette.Iron, false);
        }

        // ---------------------------------------------------------------- Farm windmills (animated)

        static void BuildWindmills(Transform root)
        {
            var spots = new[] { new Vector3(92, 0, 40), new Vector3(70, 0, 230), new Vector3(-150, 0, -10), new Vector3(150, 0, -110) };
            foreach (var s in spots)
            {
                var p = new Vector3(s.x, ValleyTerrain.Height(s.x, s.z), s.z);
                var mb = new MeshBuilder();
                mb.Matte();
                // Lattice tower
                for (int i = 0; i < 4; i++)
                {
                    float a = (i * 90 + 45) * Mathf.Deg2Rad;
                    var foot = new Vector3(Mathf.Cos(a) * 1.6f, 0, Mathf.Sin(a) * 1.6f);
                    mb.Cylinder(foot, new Vector3(foot.x * 0.25f, 10, foot.z * 0.25f), 0.09f, 4, Palette.WoodDark);
                }
                for (float y = 2; y < 10; y += 2.5f)
                {
                    float w = Mathf.Lerp(3.2f, 0.8f, y / 10f);
                    mb.Box(new Vector3(0, y, 0), new Vector3(w, 0.1f, 0.1f), Palette.Wood);
                    mb.Box(new Vector3(0, y, 0), new Vector3(0.1f, 0.1f, w), Palette.Wood);
                }
                mb.Box(new Vector3(0, 10.1f, 0), new Vector3(1, 0.3f, 1), Palette.WoodDark);
                mb.Box(new Vector3(0, 10.5f, -1.4f), new Vector3(0.1f, 1.2f, 2.4f), Palette.PaintRed); // tail vane
                // Water trough
                mb.Box(new Vector3(2.5f, 0.35f, 0), new Vector3(1.2f, 0.7f, 3), Palette.WoodDark);
                mb.Mat(0.9f);
                mb.Box(new Vector3(2.5f, 0.66f, 0), new Vector3(1.0f, 0.05f, 2.8f), Palette.Hex(0x4C7A86));
                var go = mb.ToObject("Windmill", root);
                go.transform.position = p;
                go.transform.rotation = Quaternion.Euler(0, R(0, 360), 0);

                var wheel = new MeshBuilder();
                wheel.Mat(0.4f, 0.3f);
                for (int b = 0; b < 16; b++)
                {
                    wheel.Push().Rotate(Quaternion.Euler(0, 0, b * 22.5f));
                    wheel.Push().Translate(0, 1.4f, 0).Rotate(Quaternion.Euler(0, 20, 0));
                    wheel.Box(Vector3.zero, new Vector3(0.45f, 1.9f, 0.04f), Palette.Weathered);
                    wheel.Pop();
                    wheel.Pop();
                }
                wheel.Cylinder(new Vector3(0, 0, -0.1f), new Vector3(0, 0, 0.3f), 0.25f, 8, Palette.Iron);
                var w2 = wheel.ToObject("Wheel", go.transform);
                w2.transform.localPosition = new Vector3(0, 10.6f, 0.9f);
                w2.AddComponent<Spinner>().Speed = new Vector3(0, 0, R(60, 110));
            }
        }
    }

    public sealed class Spinner : MonoBehaviour
    {
        public Vector3 Speed;
        public bool Active = true;
        void Update() { if (Active) transform.Rotate(Speed * Time.deltaTime, Space.Self); }
    }
}
