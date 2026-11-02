using UnityEngine;
using UnityEngine.Rendering;

namespace Ironhorse.Game
{
    /// <summary>
    /// Procedural valley: flat prairie floor around the town and rails, rolling hills, a ridge of red
    /// mountains on the horizon, a river feeding a lake. Desert to the south, pine country to the north.
    /// </summary>
    public static class ValleyTerrain
    {
        public const float MinX = -340, MaxX = 340, MinZ = -300, MaxZ = 420;
        public const int Cells = 240;

        /// <summary>0 on the valley floor (town, rails), rising to 1 in open country.</summary>
        public static float Wildness(float x, float z)
        {
            var r = WorldLayout.TownRect;
            float dx = Mathf.Max(r.xMin - x, 0, x - r.xMax), dz = Mathf.Max(r.yMin - z, 0, z - r.yMax);
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            foreach (var extra in new[] { WorldLayout.YardRect, WorldLayout.RoundhouseRect })
            {
                float ex0 = Mathf.Max(extra.xMin - x, 0, x - extra.xMax), ez0 = Mathf.Max(extra.yMin - z, 0, z - extra.yMax);
                d = Mathf.Min(d, Mathf.Sqrt(ex0 * ex0 + ez0 * ez0));
            }
            // Rail corridor along the main line.
            float main = Mathf.Abs(z - WorldLayout.MainZ) - 22f;
            if (Mathf.Abs(x) < WorldLayout.PathEndX) d = Mathf.Min(d, Mathf.Max(0, main));
            // Scenic loop corridor.
            float ex = (x - WorldLayout.LoopCenter.x) / WorldLayout.LoopRx, ez = (z - WorldLayout.LoopCenter.z) / WorldLayout.LoopRz;
            float ell = Mathf.Abs(Mathf.Sqrt(ex * ex + ez * ez) - 1) * Mathf.Min(WorldLayout.LoopRx, WorldLayout.LoopRz);
            d = Mathf.Min(d, Mathf.Max(0, ell - 10));
            // Inside the loop stays gentle too (meadow).
            if (ex * ex + ez * ez < 1) d = Mathf.Min(d, 10);
            return Mathf.SmoothStep(0, 1, Mathf.Clamp01(d / 45f));
        }

        public static float WaterMask(float x, float z, out float depth)
        {
            float rd = Mathf.Sqrt(WorldLayout.River.SqrDistanceXZ(x, z));
            float river = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(5, 13, rd));
            float ld = Vector2.Distance(new Vector2(x, z), new Vector2(WorldLayout.LakeCenter.x, WorldLayout.LakeCenter.z));
            ld += (Mathf.PerlinNoise(x * 0.08f, z * 0.08f) - 0.5f) * 8;
            float lake = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(WorldLayout.LakeRadius - 6, WorldLayout.LakeRadius + 6, ld));
            float m = Mathf.Max(river, lake);
            depth = Mathf.Max(1 - rd / 9f, 1 - ld / WorldLayout.LakeRadius);
            return m;
        }

        public static float Height(float x, float z)
        {
            float wild = Wildness(x, z);
            float gentle = (Noise2.Fbm(x * 0.012f, z * 0.012f, 3) - 0.5f) * 3.5f;
            float hills = Noise2.Fbm(x * 0.007f + 40, z * 0.007f - 20, 4) * 26f - 6f;
            float radial = Vector2.Distance(new Vector2(x, z), new Vector2(0, 50));
            float rim = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(190, 330, radial));
            float mountains = Noise2.Ridged(x * 0.008f, z * 0.008f, 4) * 95f * rim;
            float h = Mathf.Lerp(gentle * 0.06f, gentle + Mathf.Max(0, hills) + mountains, wild);
            float depth;
            float water = WaterMask(x, z, out depth);
            if (water > 0) h = Mathf.Lerp(h, -2.6f - Mathf.Clamp01(depth) * 1.2f, water);
            return h;
        }

        public static Color ColorAt(float x, float z, float h, float slope)
        {
            float north = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-140, 160, z));
            float n1 = Mathf.PerlinNoise(x * 0.035f + 3, z * 0.035f - 9);
            float n2 = Mathf.PerlinNoise(x * 0.11f, z * 0.11f);
            Color desert = Color.Lerp(Palette.Sand, Palette.GrassDry, n1 * 0.8f);
            Color prairie = Color.Lerp(Palette.GrassDry, Palette.Grass, Mathf.SmoothStep(0.25f, 0.75f, n1));
            Color c = Color.Lerp(desert, prairie, north);
            c = Color.Lerp(c, c * 0.88f, n2);

            // Packed earth around town.
            var r = WorldLayout.TownRect;
            if (x > r.xMin - 6 && x < r.xMax + 6 && z > r.yMin + 40 && z < r.yMax + 6)
            {
                float edge = Mathf.Min(Mathf.Min(x - (r.xMin - 6), (r.xMax + 6) - x), Mathf.Min(z - (r.yMin + 40), (r.yMax + 6) - z));
                c = Color.Lerp(c, Color.Lerp(Palette.Dirt, Palette.GrassDry, n2 * 0.5f), Mathf.Clamp01(edge / 10f) * 0.85f);
            }

            // Rock on slopes and heights, with horizontal strata bands.
            float strata = Mathf.Sin(h * 0.55f + n1 * 3) * 0.5f + 0.5f;
            Color rock = Color.Lerp(Palette.RedRock, Palette.RockPale, strata * 0.6f);
            rock = Color.Lerp(rock, Palette.RockDark, Mathf.Clamp01((h - 40) / 60f) * 0.4f);
            float rockiness = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.28f, 0.6f, slope)) + Mathf.SmoothStep(0, 1, Mathf.InverseLerp(18, 40, h)) * 0.7f;
            c = Color.Lerp(c, rock, Mathf.Clamp01(rockiness));
            if (h > 70) c = Color.Lerp(c, Palette.Snow, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(70, 90, h)) * (1 - slope));

            // Banks and lake bed.
            if (h < 0.2f) c = Color.Lerp(c, Color.Lerp(Palette.Sand, Palette.Dirt, 0.5f), Mathf.Clamp01(-h * 0.8f + 0.2f));
            return c;
        }

        public static GameObject Build(Transform parent)
        {
            int n = Cells + 1;
            float sx = (MaxX - MinX) / Cells, sz = (MaxZ - MinZ) / Cells;
            var heights = new float[n * n];
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                    heights[j * n + i] = Height(MinX + i * sx, MinZ + j * sz);

            var verts = new Vector3[n * n];
            var normals = new Vector3[n * n];
            var colors = new Color32[n * n];
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    int k = j * n + i;
                    float x = MinX + i * sx, z = MinZ + j * sz;
                    float hl = heights[j * n + Mathf.Max(0, i - 1)], hr = heights[j * n + Mathf.Min(n - 1, i + 1)];
                    float hd = heights[Mathf.Max(0, j - 1) * n + i], hu = heights[Mathf.Min(n - 1, j + 1) * n + i];
                    var nrm = new Vector3(hl - hr, 2 * sx, hd - hu).normalized;
                    verts[k] = new Vector3(x, heights[k], z);
                    normals[k] = nrm;
                    var c = (Color32)ColorAt(x, z, heights[k], 1 - nrm.y * nrm.y > 0 ? Mathf.Sqrt(1 - nrm.y * nrm.y) : 0);
                    c.a = 255;
                    colors[k] = c;
                }
            var tris = new int[Cells * Cells * 6];
            int t = 0;
            for (int j = 0; j < Cells; j++)
                for (int i = 0; i < Cells; i++)
                {
                    int a = j * n + i, b = a + 1, c = a + n, d = c + 1;
                    tris[t++] = a; tris[t++] = c; tris[t++] = d;
                    tris[t++] = a; tris[t++] = d; tris[t++] = b;
                }
            var surf = new Vector2[n * n];
            for (int k = 0; k < surf.Length; k++) surf[k] = new Vector2(0.08f, 0);

            var mesh = new Mesh { name = "Valley", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.colors32 = colors;
            mesh.uv2 = surf;
            mesh.triangles = tris;
            mesh.RecalculateBounds();

            var go = new GameObject("Terrain");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Materials.Terrain;
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveShadows = true;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            go.layer = 0;
            return go;
        }

        /// <summary>Water surface: a grid covering river and lake, vertex color r = depth.</summary>
        public static GameObject BuildWater(Transform parent)
        {
            var mb = new MeshBuilder();
            const float step = 4f;
            for (float z = -40; z < 430; z += step)
                for (float x = -170; x < -30; x += step)
                {
                    float d0, d1, d2, d3;
                    float m0 = WaterMask(x, z, out d0), m1 = WaterMask(x + step, z, out d1), m2 = WaterMask(x + step, z + step, out d2), m3 = WaterMask(x, z + step, out d3);
                    if (Mathf.Max(Mathf.Max(m0, m1), Mathf.Max(m2, m3)) < 0.35f) continue;
                    float y = WorldLayout.WaterLevel;
                    mb.QuadSmooth(new Vector3(x, y, z), new Vector3(x, y, z + step), new Vector3(x + step, y, z + step), new Vector3(x + step, y, z),
                        Vector3.up, Vector3.up, Vector3.up, Vector3.up,
                        new Color(Mathf.Clamp01(d0), 0, 0), new Color(Mathf.Clamp01(d3), 0, 0), new Color(Mathf.Clamp01(d2), 0, 0), new Color(Mathf.Clamp01(d1), 0, 0));
                }
            var go = new GameObject("Water");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Water");
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Materials.Water;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }
    }
}
