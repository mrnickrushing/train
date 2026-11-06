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
        static readonly List<Vector3> _outskirts = new List<Vector3>();

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
            foreach (var o in _outskirts)
                if ((o.x - x) * (o.x - x) + (o.z - z) * (o.z - z) < 9 * 9) return true;
            foreach (var f in Farms)
                if ((f.C.x - x) * (f.C.x - x) + (f.C.z - z) * (f.C.z - z) < 30 * 30) return true;
            return false;
        }

        public static void Build(Transform root)
        {
            var mb = new MeshBuilder();
            bool hay = Vegetation.HasArt(ModelRegistry.Buildings, "haystack");
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
                    bool golden = col.r > col.g; // ripe grain: cut and stooked in sheaves instead of standing rows
                    if (golden && hay)
                    {
                        for (int row = 0; row < 4; row++)
                            for (int j = 0; j < 4; j++)
                            {
                                var q = P(fx - 4.5f + j * 3f + (row % 2) * 1.2f, fz - 7 + row * 4.5f);
                                Vegetation.AddModel(ModelRegistry.Buildings, "haystack", q, rot * Quaternion.Euler(0, row * 47 + j * 31, 0), 0.38f);
                            }
                        continue;
                    }
                    for (int row = 0; row < 7; row++)
                    {
                        // Rounded ridges of crop with a darker furrow between, a little uneven along their length.
                        float along = sideways ? 11.5f : 18f;
                        for (int seg = 0; seg < 3; seg++)
                        {
                            float o = -along / 2 + (seg + 0.5f) * along / 3;
                            var c = sideways ? P(fx + o, fz - 9 + row * 2.75f) : P(fx - 5.4f + row * 1.8f, fz + o);
                            var dir = rot * (sideways ? Vector3.right : Vector3.forward);
                            float r = 0.42f + 0.06f * Mathf.Sin(row * 1.7f + seg * 2.3f + f.Seed);
                            mb.Cylinder(c + Vector3.up * 0.1f - dir * (along / 6), c + Vector3.up * 0.1f + dir * (along / 6), r, 6,
                                MeshBuilder.Darken(col, 0.84f + 0.16f * Mathf.Repeat(row * 0.37f + seg * 0.21f, 1f)), false);
                        }
                    }
                }
                // Haystacks and a split-rail fence along the fields' far edge.
                for (int k = 0; k < 3; k++)
                {
                    var c = P(-4 + k * 4.2f, 4 + (float)rng.NextDouble() * 2);
                    if (hay) { Vegetation.AddModel(ModelRegistry.Buildings, "haystack", c, Quaternion.Euler(0, k * 77 + f.Yaw, 0), 0.8f); continue; }
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
            Outskirts(mb);
            if (mb.VertexCount > 0) mb.ToObject("Countryside", root);
        }

        /// <summary>Houses, sheds and kitchen gardens on the edge of town, outside the building plots, so the town
        /// reads as lived-in from the first minute.</summary>
        static void Outskirts(MeshBuilder mb)
        {
            var rng = new System.Random(515);
            var t = WorldLayout.TownRect;
            float px0 = WorldLayout.PlotOriginX - WorldLayout.PlotPitchX, px1 = WorldLayout.PlotOriginX + WorldLayout.PlotCols * WorldLayout.PlotPitchX;
            int placed = 0;
            for (int i = 0; i < 400 && placed < 26; i++)
            {
                // A band 6-24 m outside the plot grid on the east, north and west.
                float x, z;
                int side = rng.Next(3);
                if (side == 0) { x = px1 + 4 + (float)rng.NextDouble() * 18; z = -4 + (float)rng.NextDouble() * 96; }
                else if (side == 1) { x = px0 - 2 - (float)rng.NextDouble() * 18; z = 30 + (float)rng.NextDouble() * 60; }
                else { x = px0 + (float)rng.NextDouble() * (px1 - px0); z = t.yMax + 2 + (float)rng.NextDouble() * 6; }
                if (ProjectSites.Blocks(x, z) || Museum.Blocks(x, z)) continue;
                if (WorldLayout.ScenicLoop.SqrDistanceXZ(x, z) < 12 * 12) continue;
                float h = ValleyTerrain.Height(x, z);
                if (Mathf.Abs(ValleyTerrain.Height(x + 4, z) - h) > 0.8f || Mathf.Abs(ValleyTerrain.Height(x, z + 4) - h) > 0.8f) continue;
                float d;
                if (ValleyTerrain.WaterMask(x, z, out d) > 0.01f) continue;
                bool near = false;
                foreach (var o in _outskirts) if ((o.x - x) * (o.x - x) + (o.z - z) * (o.z - z) < 13 * 13) { near = true; break; }
                if (near) continue;
                var p = new Vector3(x, h, z);
                _outskirts.Add(p);
                placed++;
                // Face the town.
                var toTown = new Vector3(t.center.x - x, 0, t.center.y - z);
                var rot = Quaternion.LookRotation(-toTown.normalized);
                Vegetation.AddModel("Buildings", rng.Next(3) == 0 ? "clapboard" : "cabin", p, rot);
                // A little garden plot and a picket fence beside it.
                var g = p + rot * new Vector3(7, 0, 0);
                g.y = ValleyTerrain.Height(g.x, g.z);
                mb.Matte();
                mb.Push().Translate(g + Vector3.up * 0.08f).Rotate(rot);
                mb.Surf = SurfaceKind.Dirt;
                mb.Box(Vector3.zero, new Vector3(4.5f, 0.12f, 6f), Palette.Hex(0x6E5034));
                mb.Surf = SurfaceKind.Auto;
                for (int r = 0; r < 4; r++) mb.Box(new Vector3(-1.6f + r * 1.05f, 0.2f, 0), new Vector3(0.5f, 0.35f, 5.4f), Palette.Hex(0x6E9440));
                for (float f = -3f; f <= 3.01f; f += 0.75f)
                {
                    mb.Box(new Vector3(-2.5f, 0.4f, f), new Vector3(0.08f, 0.8f, 0.08f), Palette.White);
                    mb.Box(new Vector3(2.5f, 0.4f, f), new Vector3(0.08f, 0.8f, 0.08f), Palette.White);
                }
                mb.Pop();
            }
        }
    }
}
