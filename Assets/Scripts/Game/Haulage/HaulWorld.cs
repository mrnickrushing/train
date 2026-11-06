using Ironhorse.Core;
using UnityEngine;

namespace Ironhorse.Game
{
    /// <summary>
    /// The haulage landscape: dirt wagon roads painted into the terrain (soft verges, two wheel ruts, grass down the
    /// middle on the quiet stretches), the teamsters' freight yard, the boat landing, the timber bridge over the river
    /// and the plank level crossing at the station throat.
    /// </summary>
    public static class HaulWorld
    {
        const float PxPerM = 2f;

        public static void Build(Transform root)
        {
            // Running water along the river (heard when the camera is down by the banks).
            var river = WorldLayout.River;
            for (float d = 40; d < river.Length; d += 110) MapSound.At(root, river.PositionAt(d), "amb_river", 70, 0.35f);
            PaintRoads();
            var mb = new MeshBuilder();
            // The freight barn stands along the yard's north side, its wagon doors opening onto the yard (south).
            bool barn = PlaceArt(root, "freightbarn", new Vector3(HaulRoutes.Yard.x - 2, 0, HaulRoutes.Yard.z + 16.5f), 0);
            BuildYard(mb, !barn);
            // The wharf warehouse on the bank beside the landing, its loading side facing the river (west).
            PlaceArt(root, "wharf", HaulRoutes.PierRoot + new Vector3(4, 0, 9), 90);
            BuildLanding(mb);
            BuildBridge(mb);
            BuildCrossing(mb);
            Station.Walkable(mb.ToObject("Haulage", root));
        }

        // ---------------------------------------------------------------- roads

        static Texture2D _mask;

        static void PaintRoads()
        {
            float x0 = ValleyTerrain.MinX, z0 = ValleyTerrain.MinZ;
            float wM = ValleyTerrain.MaxX - x0, hM = ValleyTerrain.MaxZ - z0;
            int w = Mathf.CeilToInt(wM * PxPerM), h = Mathf.CeilToInt(hM * PxPerM);
            var dirt = new float[w * h];
            var rut = new float[w * h];
            foreach (var id in HaulRoutes.RoadIds)
            {
                var road = HaulRoutes.Road(id);
                Vector3 prev = road.PositionAt(0);
                for (float s = 1.5f; s <= road.Length + 0.01f; s += 1.5f)
                {
                    var p = road.PositionAt(Mathf.Min(s, road.Length));
                    Stamp(dirt, rut, w, h, x0, z0, prev, p);
                    prev = p;
                }
            }
            // The yard is hard-packed earth.
            Blob(dirt, w, h, x0, z0, HaulRoutes.Yard, 11f, 8f);
            _mask = new Texture2D(w, h, TextureFormat.RG16, true, true) { name = "RoadMask", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            var px = new byte[w * h * 2];
            for (int i = 0; i < w * h; i++) { px[i * 2] = (byte)(Mathf.Clamp01(dirt[i]) * 255); px[i * 2 + 1] = (byte)(Mathf.Clamp01(rut[i]) * 255); }
            _mask.SetPixelData(px, 0);
            _mask.Apply(true, true);
            Shader.SetGlobalTexture("_RoadMask", _mask);
            Shader.SetGlobalVector("_RoadMaskRect", new Vector4(x0, z0, 1f / wM, 1f / hM));
        }

        /// <summary>Paints one road segment: a 4.4 m dirt track with ragged verges and a pair of wheel ruts.</summary>
        static void Stamp(float[] dirt, float[] rut, int w, int h, float x0, float z0, Vector3 a, Vector3 b)
        {
            const float half = 2.2f, reach = 3.6f;
            int i0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, b.x) - reach - x0) * PxPerM));
            int i1 = Mathf.Min(w - 1, Mathf.CeilToInt((Mathf.Max(a.x, b.x) + reach - x0) * PxPerM));
            int j0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.z, b.z) - reach - z0) * PxPerM));
            int j1 = Mathf.Min(h - 1, Mathf.CeilToInt((Mathf.Max(a.z, b.z) + reach - z0) * PxPerM));
            var ab = new Vector2(b.x - a.x, b.z - a.z);
            float len2 = Mathf.Max(1e-4f, ab.sqrMagnitude);
            for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                {
                    float x = x0 + (i + 0.5f) / PxPerM, z = z0 + (j + 0.5f) / PxPerM;
                    var ap = new Vector2(x - a.x, z - a.z);
                    float t = Mathf.Clamp01(Vector2.Dot(ap, ab) / len2);
                    float d = (ap - ab * t).magnitude;
                    float n = Mathf.PerlinNoise(x * 0.3f, z * 0.3f);
                    float edge = half + (n - 0.5f) * 1.1f;
                    float v = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(edge - 0.6f, edge + 0.7f, d));
                    // Grass survives along the crown between the wheel tracks.
                    v *= 1 - 0.45f * Mathf.Clamp01(1 - d / 0.45f) * Mathf.SmoothStep(0.35f, 0.75f, Mathf.PerlinNoise(x * 1.3f + 50, z * 1.3f));
                    int k = j * w + i;
                    if (v > dirt[k]) dirt[k] = v;
                    float r = Mathf.Clamp01(1 - Mathf.Abs(d - 0.95f) / 0.32f) * (0.55f + Mathf.PerlinNoise(x * 1.9f, z * 1.9f) * 0.45f);
                    if (r > rut[k]) rut[k] = r;
                }
        }

        static void Blob(float[] dirt, int w, int h, float x0, float z0, Vector3 c, float rx, float rz)
        {
            for (int j = Mathf.Max(0, (int)((c.z - rz - 3 - z0) * PxPerM)); j < Mathf.Min(h, (int)((c.z + rz + 3 - z0) * PxPerM)); j++)
                for (int i = Mathf.Max(0, (int)((c.x - rx - 3 - x0) * PxPerM)); i < Mathf.Min(w, (int)((c.x + rx + 3 - x0) * PxPerM)); i++)
                {
                    float x = x0 + (i + 0.5f) / PxPerM, z = z0 + (j + 0.5f) / PxPerM;
                    float d = Mathf.Max(Mathf.Abs(x - c.x) - rx, Mathf.Abs(z - c.z) - rz) + (Mathf.PerlinNoise(x * 0.25f, z * 0.25f) - 0.5f) * 2.5f;
                    float v = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-1, 2, d));
                    int k = j * w + i;
                    if (v > dirt[k]) dirt[k] = v;
                }
        }

        // ---------------------------------------------------------------- the freight yard

        static float G(float x, float z) { return ValleyTerrain.Height(x, z); }

        /// <summary>Places AI building art (Models/Buildings/{id}) on the ground; false when it isn't imported.</summary>
        static bool PlaceArt(Transform root, string id, Vector3 at, float yaw)
        {
            if (!ModelRegistry.Has(ModelRegistry.Buildings, id)) return false;
            var art = ModelRegistry.Spawn(ModelRegistry.Buildings, id, root);
            at.y = G(at.x, at.z);
            // Turn on top of the model's own import rotation (single-mesh FBX files carry their stand-up turn on the root).
            art.transform.SetPositionAndRotation(at, Quaternion.Euler(0, yaw, 0) * art.transform.rotation);
            Station.Walkable(art);
            return true;
        }

        static void BuildYard(MeshBuilder mb, bool stable = true)
        {
            var c = HaulRoutes.Yard;
            float y = G(c.x, c.z);
            Color wood = Palette.Wood, dark = Palette.WoodDark, weathered = Palette.Weathered;
            mb.Matte();
            // Split-rail fence around three sides (open to the gate on the east).
            for (int side = 0; side < 3; side++)
            {
                if (side == 2 && !stable) continue; // the barn closes the north side
                Vector3 a, b;
                if (side == 0) { a = new Vector3(c.x - 11, 0, c.z - 8); b = new Vector3(c.x + 11, 0, c.z - 8); }
                else if (side == 1) { a = new Vector3(c.x - 11, 0, c.z - 8); b = new Vector3(c.x - 11, 0, c.z + 8); }
                else { a = new Vector3(c.x - 11, 0, c.z + 8); b = new Vector3(c.x + 11, 0, c.z + 8); }
                Fence(mb, a, b, weathered);
            }
            // Stable and wagon shed along the north side (the freight barn's art replaces it when imported).
            if (stable) StableShed(mb, c, y, wood, dark);
            // Hay, feed troughs, crates and barrels waiting to be loaded.
            Clutter(mb, c, y, weathered, dark);
        }

        static void StableShed(MeshBuilder mb, Vector3 c, float y, Color wood, Color dark)
        {
            mb.Push().Translate(c.x - 3, y, c.z + 5);
            mb.Box(new Vector3(0, 1.7f, 0), new Vector3(12, 3.4f, 4.6f), wood, wood);
            mb.GableRoof(new Vector3(0, 3.4f, 0), new Vector3(12.6f, 1.8f, 5.4f), Palette.RoofShingle, wood, 0.35f);
            for (int i = 0; i < 4; i++) mb.Box(new Vector3(-4.5f + i * 3, 1.25f, -2.33f), new Vector3(2.1f, 2.5f, 0.08f), dark);
            mb.Box(new Vector3(0, 4.25f, -2.75f), new Vector3(5.5f, 0.7f, 0.1f), Palette.Cream); // sign board
            mb.Pop();
        }

        static void Clutter(MeshBuilder mb, Vector3 c, float y, Color weathered, Color dark)
        {
            for (int i = 0; i < 3; i++)
                mb.Cylinder(new Vector3(c.x + 6 + i * 1.5f, y + 0.55f, c.z + 5.5f), new Vector3(c.x + 6 + i * 1.5f, y + 0.55f, c.z + 6.7f), 0.6f, 10, Palette.GrassDry);
            mb.Box(new Vector3(c.x - 9, y + 0.35f, c.z - 3), new Vector3(0.8f, 0.6f, 4), dark);
            var rng = new System.Random(31);
            for (int i = 0; i < 14; i++)
            {
                float px = c.x + 2 + (float)rng.NextDouble() * 7, pz = c.z - 6.5f + (float)rng.NextDouble() * 3;
                var p = new Vector3(px, G(px, pz), pz);
                if (i % 3 == 0) mb.Cylinder(p, p + Vector3.up * 1.0f, 0.38f, 10, Palette.Wood);
                else BoxAt(mb, p, Quaternion.Euler(0, (float)rng.NextDouble() * 40, 0), new Vector3(0, 0.45f, 0), new Vector3(0.9f, 0.9f, 0.9f), weathered, Palette.WoodPale);
            }
        }

        static void BoxAt(MeshBuilder mb, Vector3 at, Quaternion rot, Vector3 c, Vector3 size, Color side, Color top)
        {
            mb.Push().Translate(at).Rotate(rot);
            mb.Box(c, size, side, top);
            mb.Pop();
        }

        static void Fence(MeshBuilder mb, Vector3 a, Vector3 b, Color col)
        {
            float len = Vector3.Distance(a, b);
            int n = Mathf.Max(1, Mathf.RoundToInt(len / 2.75f));
            for (int i = 0; i <= n; i++)
            {
                var p = Vector3.Lerp(a, b, i / (float)n);
                p.y = G(p.x, p.z);
                mb.Box(p + Vector3.up * 0.6f, new Vector3(0.16f, 1.2f, 0.16f), MeshBuilder.Darken(col, 0.8f));
                if (i == n) break;
                var q = Vector3.Lerp(a, b, (i + 1) / (float)n);
                q.y = G(q.x, q.z);
                for (int r = 0; r < 2; r++)
                {
                    float hy = 0.45f + r * 0.45f;
                    mb.Cylinder(p + Vector3.up * hy, q + Vector3.up * hy, 0.06f, 5, col, false);
                }
            }
        }

        // ---------------------------------------------------------------- landing, bridge, crossing

        static void BuildLanding(MeshBuilder mb)
        {
            var a = HaulRoutes.PierRoot;
            var b = HaulRoutes.Landing;
            float deck = WorldLayout.WaterLevel + 1.3f;
            a.y = Mathf.Max(G(a.x, a.z), deck);
            b.y = deck;
            var dir = (b - a); dir.y = 0;
            float len = dir.magnitude; dir /= len;
            var right = Vector3.Cross(Vector3.up, dir);
            mb.Matte();
            mb.Surf = SurfaceKind.Planks;
            // Plank deck on pilings; a wider head where the boats tie up.
            for (float s = 0; s < len; s += 0.6f)
            {
                var p = a + dir * s; p.y = Mathf.Lerp(a.y, deck, Mathf.Clamp01(s / 4f));
                float wd = s > len - 6 ? 6.5f : 2.8f;
                var plank = MeshBuilder.Darken(Palette.WoodPale, 0.85f + 0.15f * Mathf.PerlinNoise(s, 0));
                BoxAt(mb, p, Quaternion.LookRotation(dir), new Vector3(0, 0, 0.3f), new Vector3(wd, 0.14f, 0.55f), plank, plank);
            }
            mb.Surf = SurfaceKind.Auto;
            for (float s = 2; s < len; s += 3f)
                for (int side = -1; side <= 1; side += 2)
                {
                    float wd = s > len - 6 ? 3.1f : 1.3f;
                    var p = a + dir * s + right * side * wd;
                    mb.Cylinder(new Vector3(p.x, WorldLayout.WaterLevel - 2.5f, p.z), new Vector3(p.x, deck + 0.6f, p.z), 0.16f, 6, Palette.WoodDark);
                }
            // Bollards, a cargo crane and stacked freight on the pier head.
            var head = b - dir * 2.5f;
            mb.Cylinder(head + right * 2.6f, head + right * 2.6f + Vector3.up * 0.6f, 0.2f, 8, Palette.Iron);
            mb.Cylinder(head - right * 2.6f, head - right * 2.6f + Vector3.up * 0.6f, 0.2f, 8, Palette.Iron);
            var crane = head - right * 2.2f - dir * 2f;
            mb.Box(crane + Vector3.up * 2.4f, new Vector3(0.35f, 4.8f, 0.35f), Palette.WoodDark);
            mb.Cylinder(crane + Vector3.up * 4.6f, crane + Vector3.up * 4.2f + right * 3.2f, 0.12f, 6, Palette.WoodDark);
            for (int i = 0; i < 4; i++) mb.Box(head + right * (0.6f + (i % 2) * 1.0f) - dir * (i / 2) * 1.0f + Vector3.up * 0.5f, new Vector3(0.9f, 0.9f, 0.9f), Palette.Weathered, Palette.WoodPale);
        }

        static void BuildBridge(MeshBuilder mb)
        {
            var e = HaulRoutes.BridgeEast; var w = HaulRoutes.BridgeWest;
            float ye = G(e.x, e.z), yw = G(w.x, w.z);
            float deck = Mathf.Max(Mathf.Max(ye, yw), WorldLayout.WaterLevel + 1.6f) + 0.25f;
            var dir = (w - e).normalized;
            float len = Vector3.Distance(e, w);
            var rot = Quaternion.LookRotation(dir);
            mb.Matte();
            mb.Surf = SurfaceKind.Planks;
            for (float s = -1.5f; s <= len + 1.5f; s += 0.5f)
            {
                var p = e + dir * s; p.y = deck;
                var plank = MeshBuilder.Darken(Palette.Wood, 0.85f + 0.15f * Mathf.PerlinNoise(s * 2, 3));
                BoxAt(mb, p, rot, Vector3.zero, new Vector3(4.6f, 0.16f, 0.46f), plank, plank);
            }
            mb.Surf = SurfaceKind.Auto;
            var right = Vector3.Cross(Vector3.up, dir);
            // Railings and trestle bents.
            for (int side = -1; side <= 1; side += 2)
            {
                var a = e + right * side * 2.2f + Vector3.up * (deck + 0.9f);
                var b = w + right * side * 2.2f + Vector3.up * (deck + 0.9f);
                a.y = b.y = deck + 0.9f;
                mb.Cylinder(a - dir * 1.5f, b + dir * 1.5f, 0.07f, 5, Palette.WoodDark, false);
            }
            for (float s = 0; s <= len; s += 4f)
            {
                var p = e + dir * s;
                float ground = Mathf.Min(G(p.x, p.z), WorldLayout.WaterLevel - 1.5f);
                for (int side = -1; side <= 1; side += 2)
                {
                    var q = p + right * side * 2.2f;
                    mb.Cylinder(new Vector3(q.x, ground, q.z), new Vector3(q.x, deck + 1.0f, q.z), 0.14f, 6, Palette.WoodDark);
                }
                BoxAt(mb, new Vector3(p.x, deck - 0.35f, p.z), rot, Vector3.zero, new Vector3(4.9f, 0.3f, 0.3f), Palette.WoodDark, Palette.WoodDark);
            }
        }

        static void BuildCrossing(MeshBuilder mb)
        {
            float x = HaulRoutes.CrossingX;
            mb.Matte();
            mb.Surf = SurfaceKind.Planks;
            for (int i = 0; i < Balance.Platforms; i++)
            {
                float z = WorldLayout.PlatformZ(i);
                var path = WorldLayout.PlatformPath(i);
                float s = path.Project(new Vector3(x, 0, z));
                var p = path.PositionAt(s);
                for (int k = -2; k <= 2; k++)
                    mb.Box(new Vector3(x + k * 0.92f, WorldLayout.RailTop + 0.02f, p.z), new Vector3(0.85f, 0.12f, 2.1f), MeshBuilder.Darken(Palette.Wood, 0.8f));
            }
            mb.Surf = SurfaceKind.Auto;
            // A crossbuck either side of the tracks.
            foreach (float z in new[] { WorldLayout.PlatformZ(0) + 4.5f, WorldLayout.PlatformZ(Balance.Platforms - 1) - 4.5f })
            {
                var p = new Vector3(x + 3.2f, G(x + 3.2f, z), z);
                mb.Cylinder(p, p + Vector3.up * 3.4f, 0.09f, 6, Palette.White);
                mb.Push().Translate(p + Vector3.up * 3.0f).RotateY(90);
                BoxAt(mb, Vector3.zero, Quaternion.Euler(0, 0, 35), Vector3.zero, new Vector3(1.7f, 0.22f, 0.05f), Palette.White, Palette.White);
                BoxAt(mb, Vector3.zero, Quaternion.Euler(0, 0, -35), Vector3.zero, new Vector3(1.7f, 0.22f, 0.05f), Palette.White, Palette.White);
                mb.Pop();
            }
        }
    }
}
