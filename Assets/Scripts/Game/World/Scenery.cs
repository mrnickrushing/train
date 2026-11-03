using System.Collections.Generic;
using UnityEngine;

namespace Ironhorse.Game
{
    /// <summary>Static set dressing: mesas and tunnels, buttes, vegetation, roads, telegraph lines, windmills, the station.</summary>
    public static class Scenery
    {
        static System.Random _rng;
        static float R() { if (_rng == null) _rng = new System.Random(7); return (float)_rng.NextDouble(); }
        static float R(float a, float b) { return a + (b - a) * R(); }

        public static void Build(Transform root)
        {
            _rng = new System.Random(1869);
            BuildMesas(root);
            BuildVegetation(root);
            // Streets come from TownStreets (they change with each chapter).
            BuildTelegraph(root);
            BuildWindmills(root);
            Station.Build(root);
        }

        // ---------------------------------------------------------------- Mesas & tunnels

        /// <summary>Mesa tops (centre, usable radius, height) for scrub planted on them later.</summary>
        static readonly List<(Vector2 c, float r, float y)> _mesaTops = new List<(Vector2, float, float)>();

        static void BuildMesas(Transform root)
        {
            _mesaTops.Clear();
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
            // Weathered sandstone strata: muted reds, buffs and browns (the rock texture adds the detail).
            Color[] bands = { Palette.Hex(0x9A5A3C), Palette.Hex(0xB98762), Palette.Hex(0x7A4630), Palette.Hex(0xA4664A), Palette.Hex(0xA8784F), Palette.Hex(0xC19A74), Palette.Hex(0x8E5034), Palette.Hex(0x6E4230), Palette.Hex(0xB08460) };
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
                bool topLayer = l == layers - 1;
                Color top = topLayer ? Palette.Hex(0x8C7A4E) : MeshBuilder.Darken(bands[(l + seed) % bands.Length], 0.92f);
                mb.Matte();
                mb.Surf = SurfaceKind.Stone;
                if (topLayer) { mb.Prism(poly, y0, y1, bands[(l + seed) % bands.Length], top, 0.025f, skipTop: true); MesaTop(mb, poly, y1, seed); }
                else mb.Prism(poly, y0, y1, bands[(l + seed) % bands.Length], top, 0.025f);
                mb.Surf = SurfaceKind.Auto;
                if (topLayer) _mesaTops.Add((c, radius * shrink * 0.7f, y1));
                shrink *= l < 2 ? 1f : 0.965f;
            }
        }

        /// <summary>
        /// Mesa cap: concentric rings over the top polygon, gently domed, each facet coloured from world-space noise
        /// so it breaks into scrub, bare dirt and rock outcrops instead of one flat slab.
        /// </summary>
        static void MesaTop(MeshBuilder mb, List<Vector2> poly, float y, int seed)
        {
            const int rings = 6;
            int n = poly.Count;
            Vector2 center = Vector2.zero;
            foreach (var p in poly) center += p;
            center /= n;
            Color scrub = Palette.Hex(0x7A7A44), dry = Palette.Hex(0x9C8656), dirt = Palette.Hex(0x9A6E4A), rock = Palette.Hex(0x86705E);
            Vector3 P(int ring, int i)
            {
                float t = 1f - ring / (float)rings * 0.97f; // ring 0 = rim
                var q = Vector2.Lerp(center, Vector2.Lerp(poly[i % n], center, 0.025f), t);
                float bump = (Mathf.PerlinNoise(q.x * 0.09f + seed, q.y * 0.09f) - 0.5f) * 1.6f + (1 - t) * 1.2f;
                return new Vector3(q.x, y + (ring == 0 ? 0 : bump), q.y);
            }
            Color Col(Vector3 a)
            {
                float k = Mathf.PerlinNoise(a.x * 0.07f + seed * 2.3f, a.z * 0.07f), j = Mathf.PerlinNoise(a.x * 0.31f, a.z * 0.31f + seed);
                Color c = k < 0.38f ? Color.Lerp(scrub, dry, k / 0.38f) : k < 0.62f ? Color.Lerp(dry, dirt, (k - 0.38f) / 0.24f) : Color.Lerp(dirt, rock, (k - 0.62f) / 0.38f);
                return MeshBuilder.Darken(c, 0.86f + j * 0.22f);
            }
            var keep = mb.Surf;
            mb.Surf = SurfaceKind.Dirt;
            for (int r = 0; r < rings; r++)
                for (int i = 0; i < n; i++)
                {
                    Vector3 a = P(r, i), b = P(r, i + 1), c = P(r + 1, i + 1), d = P(r + 1, i);
                    if (r == rings - 1) c = d = new Vector3(center.x, P(rings, 0).y, center.y);
                    // Smooth colours blend the scrub/dirt/rock patches into each other.
                    mb.QuadSmooth(a, d, c, b, Vector3.up, Vector3.up, Vector3.up, Vector3.up, Col(a), Col(d), Col(c), Col(b));
                }
            mb.Surf = keep;
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

        /// <summary>While true, plants with drop-in art are registered for instanced drawing instead of meshed.</summary>
        static bool _instancing;

        static bool Instanced(string kind, Vector3 p, float scale)
        {
            if (!_instancing || !Vegetation.HasArt(kind)) return false;
            Vegetation.Add(kind, p, scale);
            return true;
        }

        static void BuildVegetation(Transform root)
        {
            if (root.GetComponent<Vegetation>() == null) root.gameObject.AddComponent<Vegetation>();
            _instancing = true;
            const float chunk = 120f;
            var chunks = new Dictionary<Vector2Int, MeshBuilder>();
            MeshBuilder Chunk(Vector3 p)
            {
                var key = new Vector2Int(Mathf.FloorToInt(p.x / chunk), Mathf.FloorToInt(p.z / chunk));
                MeshBuilder b;
                if (!chunks.TryGetValue(key, out b)) chunks[key] = b = new MeshBuilder();
                return b;
            }

            for (int i = 0; i < 9000; i++)
            {
                float x = R(ValleyTerrain.MinX + 10, ValleyTerrain.MaxX - 10), z = R(ValleyTerrain.MinZ + 10, ValleyTerrain.MaxZ - 10);
                if (ProjectSites.Blocks(x, z) || Museum.Blocks(x, z)) continue; // the trestle cut-off and the Fort Calder wire
                float wild = ValleyTerrain.Wildness(x, z);
                float depth;
                float water = ValleyTerrain.WaterMask(x, z, out depth);
                if (water > 0.25f) continue;
                float h = ValleyTerrain.Height(x, z);
                float slope = Mathf.Abs(ValleyTerrain.Height(x + 2, z) - h) + Mathf.Abs(ValleyTerrain.Height(x, z + 2) - h);
                if (slope > 3.2f || h > 75) continue;
                float north = Mathf.InverseLerp(-230, 60, z);
                float clump = Mathf.PerlinNoise(x * 0.02f + 100, z * 0.02f);
                var p = new Vector3(x, h - 0.1f, z);
                bool nearRiver = water > 0.02f || Mathf.Sqrt(WorldLayout.River.SqrDistanceXZ(x, z)) < 22;

                // Stands of pines frame the valley floor (just outside the town, rails and loop).
                if (wild > 0.1f && wild < 0.6f && north > 0.3f && clump > 0.5f && R() < 0.8f)
                {
                    Pine(Chunk(p), p, R(0.75f, 1.35f));
                    continue;
                }
                if (wild < 0.12f)
                {
                    // Valley floor: mostly open; a few shade trees and shrubs well away from tracks and town.
                    if (wild > 0.012f && clump > 0.5f && R() < 0.55f)
                    {
                        if (north > 0.45f && R() < 0.7f) Pine(Chunk(p), p, R(0.7f, 1.1f));
                        else Cottonwood(Chunk(p), p, R(0.8f, 1.2f));
                    }
                    else if (wild > 0.04f && R() < 0.25f) Sage(Chunk(p), p, R(0.6f, 1.1f));
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

            // Scrub, junipers and the odd saguaro on the mesa tops.
            foreach (var (mc, mr, my) in _mesaTops)
            {
                int n = Mathf.RoundToInt(mr * mr * 0.16f);
                for (int i = 0; i < n; i++)
                {
                    float a = R(0, Mathf.PI * 2), d = Mathf.Sqrt(R()) * mr;
                    var p = new Vector3(mc.x + Mathf.Cos(a) * d, my + 0.4f + (1 - d / mr) * 1.0f, mc.y + Mathf.Sin(a) * d);
                    float k = R();
                    if (k < 0.55f) Sage(Chunk(p), p, R(0.8f, 1.5f));
                    else if (k < 0.8f) Pine(Chunk(p), p, R(0.55f, 0.85f));
                    else if (k < 0.9f) Saguaro(Chunk(p), p, R(0.7f, 1.0f));
                    else Rock(Chunk(p), p, R(0.8f, 2.2f));
                }
            }
            _instancing = false;
            foreach (var kv in chunks)
            {
                var go = kv.Value.ToObject("Vegetation " + kv.Key, root);
                go.isStatic = true;
            }
        }

        /// <summary>Conifer: stacked star-shaped tiers (jagged silhouette), dark inside, sunlit tips.</summary>
        public static void Pine(MeshBuilder mb, Vector3 p, float s)
        {
            if (Instanced(Mathf.Repeat(p.x * 0.37f + p.z * 0.71f, 1f) < 0.6f ? "pine" : "pine2", p, s * 0.95f)) return;
            mb.Matte();
            float hue = R(-0.03f, 0.03f);
            Color dark = Shift(Palette.PineDark, hue - 0.03f), mid = Shift(Palette.Pine, hue), tip = Shift(Color.Lerp(Palette.Pine, Palette.Grass, 0.25f), hue);
            mb.Cylinder(p, p + Vector3.up * 2.0f * s, 0.26f * s, 0.18f * s, 5, Palette.Bark, false);
            float height = R(8.5f, 11f) * s;
            int tiers = 5;
            float twist = R(0, 360);
            for (int t = 0; t < tiers; t++)
            {
                float f = t / (float)(tiers - 1);
                float y0 = Mathf.Lerp(1.3f * s, height * 0.62f, f);
                float y1 = y0 + Mathf.Lerp(3.4f, 2.6f, f) * s;
                float r = Mathf.Lerp(2.5f, 0.9f, f) * s * R(0.9f, 1.1f);
                SpikyCone(mb, p + Vector3.up * y0, y1 - y0, r, 9, twist + t * 23, Color.Lerp(dark, mid, f * 0.6f), Color.Lerp(mid, tip, f));
            }
            SpikyCone(mb, p + Vector3.up * (height * 0.62f + 1.6f * s), height * 0.38f, 0.55f * s, 6, twist, mid, tip);
        }

        /// <summary>Cone whose rim alternates long and short points; normals lean outward so it shades softly.</summary>
        static void SpikyCone(MeshBuilder mb, Vector3 b, float h, float r, int points, float rotDeg, Color under, Color top)
        {
            int n = points * 2;
            var apex = b + Vector3.up * h;
            float droop = h * 0.18f;
            var rim = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float a = (rotDeg + i * 360f / n) * Mathf.Deg2Rad;
                bool spike = i % 2 == 0;
                float rr = spike ? r * R(0.95f, 1.15f) : r * 0.62f;
                rim[i] = b + new Vector3(Mathf.Cos(a) * rr, spike ? -droop : droop * 0.4f, Mathf.Sin(a) * rr);
            }
            for (int i = 0; i < n; i++)
            {
                var a = rim[i]; var c = rim[(i + 1) % n];
                var outA = (a - b); outA.y = 0; outA.Normalize();
                var outC = (c - b); outC.y = 0; outC.Normalize();
                var na = (outA + Vector3.up * 0.9f).normalized; var nc = (outC + Vector3.up * 0.9f).normalized;
                mb.QuadSmooth(a, apex, apex, c, na, Vector3.up, Vector3.up, nc, i % 2 == 0 ? top : Color.Lerp(top, under, 0.5f), top, top, i % 2 == 1 ? top : Color.Lerp(top, under, 0.5f));
                mb.Tri(a, c, b + Vector3.up * droop * 0.3f, under); // underside, in shadow
            }
        }

        public static void Cottonwood(MeshBuilder mb, Vector3 p, float s)
        {
            if (Instanced("cottonwood", p, s * 0.75f)) return;
            mb.Matte();
            mb.Cylinder(p, p + Vector3.up * 3.4f * s, 0.4f * s, 0.25f * s, 6, Palette.Bark, false);
            Color leaf = Shift(Palette.Hex(0x6E8C3E), R(-0.05f, 0.05f));
            mb.Sphere(p + new Vector3(0, 4.6f, 0) * s, new Vector3(2.6f, 2.0f, 2.6f) * s, 7, leaf, false);
            mb.Sphere(p + new Vector3(1.3f, 5.4f, 0.5f) * s, new Vector3(1.8f, 1.5f, 1.8f) * s, 6, MeshBuilder.Lighten(leaf, 0.1f), false);
            mb.Sphere(p + new Vector3(-1.1f, 5.2f, -0.7f) * s, new Vector3(1.7f, 1.4f, 1.7f) * s, 6, MeshBuilder.Darken(leaf, 0.9f), false);
        }

        public static void Saguaro(MeshBuilder mb, Vector3 p, float s)
        {
            if (Instanced("saguaro", p, s * 0.95f)) return;
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
            if (Instanced("sage", p, s * 0.75f)) return;
            mb.Matte();
            Color c = Shift(Palette.Sage, R(-0.05f, 0.05f));
            mb.Sphere(p + Vector3.up * 0.35f * s, new Vector3(0.9f, 0.6f, 0.8f) * s, 6, c, false);
            mb.Sphere(p + new Vector3(0.5f, 0.3f, 0.3f) * s, new Vector3(0.6f, 0.45f, 0.6f) * s, 5, MeshBuilder.Darken(c, 0.9f), false);
        }

        public static void Rock(MeshBuilder mb, Vector3 p, float s)
        {
            mb.Matte();
            Color c = Color.Lerp(Palette.Hex(0x7E5A44), Palette.Hex(0x8A8278), R());
            mb.Surf = SurfaceKind.Stone;
            mb.Push().Translate(p - Vector3.up * 0.15f * s).RotateY(R(0, 360));
            mb.Sphere(new Vector3(0, 0.25f * s, 0), new Vector3(1.1f, 0.55f, 0.9f) * s, 7, c, false);
            mb.Pop();
            mb.Surf = SurfaceKind.Auto;
        }

        static Color Shift(Color c, float amount) { return new Color(Mathf.Clamp01(c.r + amount), Mathf.Clamp01(c.g + amount * 0.6f), Mathf.Clamp01(c.b + amount * 0.3f)); }

        // ---------------------------------------------------------------- Roads

        public static void RoadStrip(MeshBuilder mb, Vector3 a, Vector3 b, float width, Color c)
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
