using System.Collections.Generic;
using UnityEngine;

namespace Ironhorse.Game
{
    /// <summary>
    /// Homesteads dotted over the open country between the lines: a farmhouse and barn (instanced models with far
    /// LODs), a patchwork of crop fields in rows, a fence, haystacks and the odd water tower. They fill the map the way
    /// a busy tycoon map is filled. Spots are chosen once, on gentle open land clear of the rails, roads, sites, town
    /// and water; scenery keeps off them.
    /// </summary>
    public static class Countryside
    {
        struct Farm { public Vector3 C; public float Yaw; public int Seed; }

        static List<Farm> _farms;

        static List<Farm> Farms
        {
            get
            {
                if (_farms != null) return _farms;
                _farms = new List<Farm>();
                var rng = new System.Random(808);
                for (int tries = 0; tries < 6000 && _farms.Count < 34; tries++)
                {
                    float x = -300 + (float)rng.NextDouble() * 600, z = -230 + (float)rng.NextDouble() * 650;
                    if (!Good(x, z)) continue;
                    bool near = false;
                    foreach (var f in _farms) if ((f.C.x - x) * (f.C.x - x) + (f.C.z - z) * (f.C.z - z) < 75 * 75) { near = true; break; }
                    if (near) continue;
                    _farms.Add(new Farm { C = new Vector3(x, ValleyTerrain.Height(x, z), z), Yaw = (float)rng.NextDouble() * 360, Seed = rng.Next() });
                }
                return _farms;
            }
        }

        static bool Good(float x, float z)
        {
            if (z > RegionFog.EdgeZ - 20) return false;
            float w = ValleyTerrain.Wildness(x, z);
            if (w < 0.06f || w > 0.6f) return false;
            float h = ValleyTerrain.Height(x, z);
            if (h > 24 || h < 0.3f) return false;
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4, px = x + Mathf.Cos(a) * 24, pz = z + Mathf.Sin(a) * 24;
                if (Mathf.Abs(ValleyTerrain.Height(px, pz) - h) > 2.6f) return false;
                float d;
                if (ValleyTerrain.WaterMask(px, pz, out d) > 0.02f) return false;
                if (ProjectSites.BlocksCore(px, pz) || Museum.Blocks(px, pz)) return false;
            }
            if (DestinationRoutes.Distance(x, z) < 38) return false;
            if (Mathf.Abs(z - WorldLayout.MainZ) < 34) return false;
            if (WorldLayout.ScenicLoop.SqrDistanceXZ(x, z) < 34 * 34) return false;
            foreach (var r in new[] { WorldLayout.TownRect, WorldLayout.YardRect, WorldLayout.RoundhouseRect })
                if (x > r.xMin - 30 && x < r.xMax + 30 && z > r.yMin - 30 && z < r.yMax + 30) return false;
            return true;
        }

        /// <summary>True on a homestead's ground (scenery keeps off).</summary>
        public static bool Blocks(float x, float z)
        {
            foreach (var f in Farms)
                if ((f.C.x - x) * (f.C.x - x) + (f.C.z - z) * (f.C.z - z) < 30 * 30) return true;
            return false;
        }

        public static void Build(Transform root)
        {
            var mb = new MeshBuilder();
            Color[] crops = { Palette.Hex(0xB8963E), Palette.Hex(0x6E9440), Palette.Hex(0xA0884A), Palette.Hex(0x84A444), Palette.Hex(0x9C7A3A) };
            foreach (var f in Farms)
            {
                var rng = new System.Random(f.Seed);
                var rot = Quaternion.Euler(0, f.Yaw, 0);
                System.Func<float, float, Vector3> P = (ax, az) =>
                {
                    var q = f.C + rot * new Vector3(ax, 0, az);
                    q.y = ValleyTerrain.Height(q.x, q.z);
                    return q;
                };
                // Homestead: farmhouse and barn face the yard; a water tower on some.
                Vegetation.AddModel("Buildings", rng.Next(3) == 0 ? "clapboard" : "cabin", P(-7, 12), rot * Quaternion.Euler(0, 180, 0));
                Vegetation.AddModel("Buildings", "freightbarn", P(9, 13), rot * Quaternion.Euler(0, 180, 0));
                if (rng.Next(3) == 0) Vegetation.AddModel("Buildings", "watertower", P(-15, 2), rot);

                // A patchwork of two to four fields in rows.
                int fields = 2 + rng.Next(3);
                for (int k = 0; k < fields; k++)
                {
                    var col = crops[rng.Next(crops.Length)];
                    float fx = -18 + k * 13 + (float)rng.NextDouble() * 2, fz = -10 - (float)rng.NextDouble() * 4;
                    bool sideways = rng.Next(2) == 0;
                    mb.Matte();
                    // Tilled earth under the rows.
                    var bed = P(fx, fz - (sideways ? 1 : 0));
                    mb.Surf = SurfaceKind.Dirt;
                    mb.Push().Translate(bed + Vector3.up * 0.05f).Rotate(rot);
                    mb.Box(Vector3.zero, new Vector3(sideways ? 12.5f : 13f, 0.12f, 19f), Palette.Hex(0x6E5034));
                    mb.Pop();
                    mb.Surf = SurfaceKind.Auto;
                    for (int row = 0; row < 6; row++)
                    {
                        var c = sideways ? P(fx, fz - 9 + row * 3.2f) : P(fx - 5 + row * 2.1f, fz);
                        mb.Push().Translate(c + Vector3.up * 0.18f).Rotate(rot * Quaternion.Euler(0, sideways ? 90 : 0, 0));
                        mb.Box(Vector3.zero, new Vector3(1.6f, 0.3f, sideways ? 11.5f : 18f), MeshBuilder.Darken(col, 0.88f + 0.12f * (row % 2)));
                        mb.Pop();
                    }
                }
                // Haystacks and a split-rail fence along the fields' far edge.
                for (int k = 0; k < 3; k++)
                {
                    var c = P(-4 + k * 3.4f, 4 + (float)rng.NextDouble() * 2);
                    mb.Matte();
                    mb.Cylinder(c, c + Vector3.up * 1.6f, 1.25f, 0.5f, 10, Palette.GrassDry, true);
                }
                for (float t = -26; t <= 26; t += 3.5f)
                {
                    var a = P(t, -22);
                    mb.Box(a + Vector3.up * 0.55f, new Vector3(0.14f, 1.1f, 0.14f), Palette.WoodDark);
                    if (t + 3.5f <= 26.01f)
                    {
                        var b = P(t + 3.5f, -22);
                        mb.Cylinder(a + Vector3.up * 0.75f, b + Vector3.up * 0.75f, 0.05f, 4, Palette.Weathered, false);
                    }
                }
            }
            if (mb.VertexCount > 0) mb.ToObject("Countryside", root);
        }
    }
}
