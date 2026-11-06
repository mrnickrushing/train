using System.Collections.Generic;
using UnityEngine;

namespace Ironhorse.Game
{
    /// <summary>The later territory projects: the Deep Shaft, the Fort Calder resupply depot, Silver Lake Dam and the Golden Spike.</summary>
    public sealed partial class ProjectSites
    {
        static readonly Rect MineRect = new Rect(-226, 24, 46, 42);
        static readonly Rect DepotRect = new Rect(100, 28, 66, 42);
        static readonly Rect DamRect = new Rect(-140, 52, 66, 40);
        static readonly Rect SpikeRect = new Rect(-148, -92, 52, 40);
        static readonly Rect[] MoreSiteRects = { MineRect, DepotRect, DamRect, SpikeRect };

        static float GY(float x, float z) { return Mathf.Max(Ground(x, z), 0); }
        static Vector3 At(float x, float z) { return new Vector3(x, GY(x, z), z); }

        // ================================================================ Black Hollow Deep Shaft

        const float MineX = -205, MineZ = 40, HeadH = 12f;

        Site Mine()
        {
            var s = new Site { Id = "mineshaft", Focus = new Vector3(MineX + 2, 5, MineZ - 2), Distance = 66 };
            float g = GY(MineX, MineZ);
            var foot = new Vector3(MineX, g, MineZ);
            s.Pieces.Add(new Piece(foot + new Vector3(-9, 0, 11), mb => Spoil(mb, new Vector3(MineX - 9, 0, MineZ + 11), 0.55f)));
            s.Pieces.Add(new Piece(foot, mb => { mb.Matte(); mb.Box(foot + Vector3.up * 0.3f, new Vector3(4.2f, 0.6f, 4.2f), Palette.WoodDark); mb.Box(foot + Vector3.up * 0.62f, new Vector3(2.4f, 0.05f, 2.4f), Palette.Black); }));
            s.Pieces.Add(new Piece(foot, mb => HeadLegs(mb, foot, -1)));
            s.Pieces.Add(new Piece(foot, mb => HeadLegs(mb, foot, 1)));
            s.Pieces.Add(new Piece(foot, mb => HeadBraces(mb, foot)));
            s.Pieces.Add(new Piece(foot + Vector3.up * HeadH, mb => Sheave(mb, foot)));
            var hoist = At(MineX + 11, MineZ - 1);
            s.Pieces.Add(new Piece(hoist, mb => { mb.Matte(); mb.Surf = SurfaceKind.Planks; mb.Box(hoist + new Vector3(0, 2, 0), new Vector3(6, 4, 5), Palette.Weathered); mb.Surf = SurfaceKind.Auto; mb.Box(hoist + new Vector3(-3.01f, 1.4f, 0), new Vector3(0.05f, 2.4f, 1.6f), Palette.WoodDark); }));
            s.Pieces.Add(new Piece(hoist + Vector3.up * 4, mb => { mb.GableRoof(hoist + Vector3.up * 4, new Vector3(6.4f, 1.8f, 5.4f), Palette.RoofTin, Palette.Weathered); mb.Cylinder(hoist + new Vector3(1.8f, 4, 1.4f), hoist + new Vector3(1.8f, 9.5f, 1.4f), 0.35f, 8, Palette.Iron); }));
            s.Pieces.Add(new Piece(hoist + Vector3.up * 3, mb => { mb.Mat(0.4f, 0.5f); mb.Cylinder(hoist + new Vector3(-3, 3.2f, 0), foot + new Vector3(0.9f, HeadH + 0.6f, 0), 0.07f, 4, Palette.Iron, false); }));
            var bin = At(MineX - 7, MineZ - 6);
            s.Pieces.Add(new Piece(bin, mb => { mb.Matte(); foreach (var o in new[] { new Vector3(-1.6f, 0, -1.6f), new Vector3(1.6f, 0, -1.6f), new Vector3(-1.6f, 0, 1.6f), new Vector3(1.6f, 0, 1.6f) }) mb.Cylinder(bin + o, bin + o + Vector3.up * 4.2f, 0.16f, 5, Palette.WoodDark); }));
            s.Pieces.Add(new Piece(bin + Vector3.up * 4, mb => { mb.Matte(); mb.Surf = SurfaceKind.Planks; mb.Box(bin + Vector3.up * 5.4f, new Vector3(4, 2.6f, 4), Palette.Wood); mb.Surf = SurfaceKind.Auto; mb.Box(bin + new Vector3(0, 4.0f, -2.6f), new Vector3(1.2f, 0.2f, 2.2f), Palette.WoodDark); }));
            s.Pieces.Add(new Piece(foot + new Vector3(-3, 0, -3), mb => OreCarts(mb, foot, bin)));
            s.Pieces.Add(new Piece(foot + new Vector3(-9, 0, 11), mb => Spoil(mb, new Vector3(MineX - 9, 0, MineZ + 11), 1f)));
            s.Pieces.Add(new Piece(At(MineX + 4, MineZ - 7), mb => { var p = At(MineX + 4, MineZ - 7); Lantern(mb, p); Lantern(mb, At(MineX - 3, MineZ - 4)); BoardSign(mb, At(MineX + 6, MineZ - 8)); }));

            s.DoneModel = "mineshaft_done"; s.DoneAt = At(MineX + 2, MineZ); s.DoneYaw = 90;
            s.Stakes = mb => { foreach (var o in new[] { new Vector2(-3, -3), new Vector2(3, -3), new Vector2(-3, 3), new Vector2(3, 3), new Vector2(11, -1), new Vector2(-7, -6) }) Stake(mb, At(MineX + o.x, MineZ + o.y)); };
            s.Camp = mb => Camp(mb, new Vector2(MineX + 14, MineZ + 12), CampKind.Timber);
            return s;
        }

        static void HeadLegs(MeshBuilder mb, Vector3 foot, int side)
        {
            mb.Matte();
            mb.Surf = SurfaceKind.WoodGrain;
            foreach (int k in new[] { -1, 1 })
                mb.Cylinder(foot + new Vector3(side * 2.2f, 0, k * 2.2f), foot + new Vector3(side * 0.9f, HeadH, k * 0.9f), 0.2f, 6, Palette.WoodDark);
            mb.Surf = SurfaceKind.Auto;
        }

        static void HeadBraces(MeshBuilder mb, Vector3 foot)
        {
            mb.Matte();
            foreach (int k in new[] { -1, 1 })
                mb.Cylinder(foot + new Vector3(7.5f, 0, k * 1.6f), foot + new Vector3(0.9f, HeadH - 1.2f, k * 0.9f), 0.18f, 6, Palette.WoodDark); // back legs toward the hoist
            for (float y = 3; y < HeadH - 1; y += 3)
            {
                float w = Mathf.Lerp(2.2f, 0.9f, y / HeadH) * 2 + 0.3f;
                mb.Box(foot + new Vector3(0, y, w / 2 - 0.15f), new Vector3(w, 0.18f, 0.18f), Palette.Wood);
                mb.Box(foot + new Vector3(0, y, -w / 2 + 0.15f), new Vector3(w, 0.18f, 0.18f), Palette.Wood);
            }
        }

        static void Sheave(MeshBuilder mb, Vector3 foot)
        {
            var top = foot + Vector3.up * HeadH;
            mb.Matte();
            mb.Box(top + Vector3.up * 0.15f, new Vector3(2.6f, 0.3f, 2.6f), Palette.WoodDark);
            mb.Mat(0.35f, 0.6f);
            var c = top + new Vector3(0, 1.3f, 0);
            mb.Cylinder(c + Vector3.forward * -0.12f, c + Vector3.forward * 0.12f, 1.15f, 16, Palette.Iron);
            mb.Cylinder(c + Vector3.forward * -0.14f, c + Vector3.forward * 0.14f, 0.9f, 16, Palette.Steel);
            mb.Matte();
        }

        static void OreCarts(MeshBuilder mb, Vector3 foot, Vector3 bin)
        {
            mb.Mat(0.3f, 0.5f);
            Vector3 a = foot + new Vector3(-1, 0.1f, -1), b = bin + new Vector3(0, 0.1f, 2.6f);
            foreach (float k in new[] { -0.4f, 0.4f }) mb.Cylinder(a + Vector3.right * k, b + Vector3.right * k, 0.04f, 4, Palette.Steel, false);
            mb.Matte();
            for (int i = 0; i < 2; i++)
            {
                var p = Vector3.Lerp(a, b, 0.3f + i * 0.35f) + Vector3.up * 0.55f;
                mb.Box(p, new Vector3(1.0f, 0.7f, 1.4f), Palette.Iron);
                mb.Box(p + Vector3.up * 0.32f, new Vector3(0.9f, 0.12f, 1.3f), Palette.Black); // ore heaped in the tub
            }
        }

        static void Spoil(MeshBuilder mb, Vector3 c, float k)
        {
            var p = new Vector3(c.x, GY(c.x, c.z) - 0.4f, c.z);
            mb.Matte();
            mb.Sphere(p, new Vector3(6.5f, 2.6f, 5.2f) * k, 10, Palette.RockDark);
            mb.Sphere(p + new Vector3(3 * k, 0, -2 * k), new Vector3(4f, 1.8f, 3.4f) * k, 8, Palette.RedRock);
        }

        static void Lantern(MeshBuilder mb, Vector3 foot)
        {
            mb.Matte();
            mb.Cylinder(foot, foot + Vector3.up * 3.2f, 0.07f, 5, Palette.WoodDark);
            mb.Lit();
            mb.Box(foot + Vector3.up * 3.3f, new Vector3(0.3f, 0.4f, 0.3f), Palette.Window);
            mb.Matte();
        }

        static void BoardSign(MeshBuilder mb, Vector3 foot)
        {
            mb.Matte();
            foreach (float k in new[] { -0.9f, 0.9f }) mb.Cylinder(foot + Vector3.right * k, foot + Vector3.right * k + Vector3.up * 2.2f, 0.08f, 5, Palette.WoodDark);
            mb.Box(foot + Vector3.up * 1.8f, new Vector3(2.4f, 0.7f, 0.08f), Palette.Cream);
            mb.Box(foot + new Vector3(0, 1.8f, -0.05f), new Vector3(1.9f, 0.12f, 0.02f), Palette.Black);
        }

        // ================================================================ Fort Calder Resupply depot

        const float DepotX = 128, DepotZ = 48;

        Site Depot()
        {
            var s = new Site { Id = "resupply", Focus = new Vector3(DepotX, 5, DepotZ - 4), Distance = 72 };
            float floor = 0;
            for (float x = -13; x <= 13; x += 6.5f) for (float z = -5; z <= 5; z += 5) floor = Mathf.Max(floor, GY(DepotX + x, DepotZ + z));
            floor += 0.7f;
            var c = new Vector3(DepotX, floor, DepotZ);
            s.Pieces.Add(new Piece(new Vector3(DepotX, floor - 2, DepotZ), mb => { mb.Matte(); mb.Surf = SurfaceKind.Stone; mb.Box(new Vector3(DepotX, floor - 1.6f, DepotZ), new Vector3(27, 3.2f, 11), Palette.StoneDark, Palette.Stone); mb.Surf = SurfaceKind.Auto; }));
            s.Pieces.Add(new Piece(c, mb => { mb.Matte(); mb.Surf = SurfaceKind.Planks; mb.Box(c + Vector3.up * 2.4f, new Vector3(25, 4.8f, 9), Palette.Hex(0x9A6A45)); mb.Surf = SurfaceKind.Auto; for (int i = -2; i <= 2; i++) mb.Box(c + new Vector3(i * 4.8f, 1.6f, -4.52f), new Vector3(2.6f, 3.2f, 0.06f), Palette.WoodDark); }));
            s.Pieces.Add(new Piece(c + Vector3.up * 4.8f, mb => { mb.GableRoof(c + Vector3.up * 4.8f, new Vector3(26, 2.6f, 10), Palette.RoofTin, Palette.Hex(0x9A6A45)); mb.Box(c + new Vector3(0, 6.2f, -5.06f), new Vector3(9, 0.9f, 0.08f), Palette.Cream); mb.Box(c + new Vector3(0, 6.2f, -5.11f), new Vector3(7.4f, 0.16f, 0.02f), Palette.Black); }));
            s.Pieces.Add(new Piece(c + new Vector3(0, -0.3f, -6.2f), mb => { mb.Matte(); mb.Surf = SurfaceKind.Planks; mb.Box(c + new Vector3(0, -0.15f, -6.4f), new Vector3(25, 0.3f, 3), Palette.WoodPale); mb.Surf = SurfaceKind.Auto; for (float x = -12; x <= 12; x += 4) mb.Box(new Vector3(DepotX + x, (floor + GY(DepotX + x, DepotZ - 7.6f)) / 2 - 0.3f, DepotZ - 7.6f), new Vector3(0.3f, floor - GY(DepotX + x, DepotZ - 7.6f), 0.3f), Palette.WoodDark); }));
            foreach (var o in new[] { new Vector2(-17, -4), new Vector2(17, -6), new Vector2(-16, 6) })
            {
                var p = At(DepotX + o.x, DepotZ + o.y);
                s.Pieces.Add(new Piece(p, mb => Crates(mb, p)));
            }
            foreach (var o in new[] { new Vector2(8, -10.5f), new Vector2(-6, -10.5f) })
            {
                var p = At(DepotX + o.x, DepotZ + o.y);
                s.Pieces.Add(new Piece(p, mb => Barrels(mb, p)));
            }
            var corral = At(DepotX + 26, DepotZ + 8);
            s.Pieces.Add(new Piece(corral, mb => Fence(mb, corral, 12, 10, true)));
            s.Pieces.Add(new Piece(corral, mb => Fence(mb, corral, 12, 10, false)));
            foreach (var o in new[] { new Vector2(-4, -14), new Vector2(6, -15) })
            {
                var p = At(DepotX + o.x, DepotZ + o.y);
                s.Pieces.Add(new Piece(p, mb => Wagon(mb, p)));
            }
            var flag = At(DepotX - 20, DepotZ - 10);
            s.Pieces.Add(new Piece(flag, mb => FlagPole(mb, flag, 11f)));

            s.DoneModel = "resupply_done"; s.DoneAt = At(DepotX, DepotZ); s.DoneYaw = 180;
            s.Stakes = mb => { foreach (var o in new[] { new Vector2(-13, -5), new Vector2(13, -5), new Vector2(-13, 5), new Vector2(13, 5), new Vector2(26, 8) }) Stake(mb, At(DepotX + o.x, DepotZ + o.y)); };
            s.Camp = mb => Camp(mb, new Vector2(DepotX - 26, DepotZ + 10), CampKind.Crates);
            return s;
        }

        static void Crates(MeshBuilder mb, Vector3 p)
        {
            mb.Matte();
            mb.Surf = SurfaceKind.Planks;
            for (int i = 0; i < 6; i++)
            {
                var o = new Vector3((i % 3) * 1.15f - 1.15f, 0.55f + (i / 3) * 1.1f, (i / 3) * 0.35f);
                if (i / 3 == 1 && i % 3 == 2) continue;
                mb.Box(p + o, new Vector3(1.05f, 1.05f, 1.05f), MeshBuilder.Darken(Palette.WoodPale, 0.82f + 0.06f * (i % 3)));
            }
            mb.Surf = SurfaceKind.Auto;
        }

        static void Barrels(MeshBuilder mb, Vector3 p)
        {
            mb.Matte();
            for (int i = 0; i < 5; i++)
            {
                var b = p + new Vector3((i % 3) * 0.9f - 0.9f, 0, (i / 3) * 0.9f);
                mb.Cylinder(b, b + Vector3.up * 1.1f, 0.42f, 10, Palette.Wood);
                mb.Cylinder(b + Vector3.up * 0.3f, b + Vector3.up * 0.36f, 0.44f, 10, Palette.Iron, false);
                mb.Cylinder(b + Vector3.up * 0.8f, b + Vector3.up * 0.86f, 0.44f, 10, Palette.Iron, false);
            }
        }

        static void Fence(MeshBuilder mb, Vector3 c, float w, float d, bool front)
        {
            mb.Matte();
            float z0 = front ? -d / 2 : d / 2;
            for (float x = -w / 2; x <= w / 2 + 0.01f; x += 2)
            {
                var p = new Vector3(c.x + x, GY(c.x + x, c.z + z0), c.z + z0);
                mb.Box(p + Vector3.up * 0.7f, new Vector3(0.16f, 1.4f, 0.16f), Palette.WoodDark);
            }
            foreach (float y in new[] { 0.6f, 1.2f })
                mb.Box(new Vector3(c.x, GY(c.x, c.z + z0) + y, c.z + z0), new Vector3(w, 0.12f, 0.08f), Palette.Wood);
            foreach (float sx in new[] { -w / 2, w / 2 })
            {
                float zs = front ? -d / 4 : d / 4;
                for (float z = 0; z <= d / 2 + 0.01f; z += 2)
                {
                    float zz = c.z + (front ? -z : z);
                    mb.Box(new Vector3(c.x + sx, GY(c.x + sx, zz) + 0.7f, zz), new Vector3(0.16f, 1.4f, 0.16f), Palette.WoodDark);
                }
                foreach (float y in new[] { 0.6f, 1.2f })
                    mb.Box(new Vector3(c.x + sx, GY(c.x + sx, c.z + zs) + y, c.z + zs), new Vector3(0.08f, 0.12f, d / 2), Palette.Wood);
            }
        }

        static void Wagon(MeshBuilder mb, Vector3 p)
        {
            mb.Matte();
            mb.Box(p + Vector3.up * 1.1f, new Vector3(1.8f, 0.6f, 3.6f), Palette.Wood);
            foreach (var w in new[] { new Vector3(-1, 0.6f, -1.2f), new Vector3(1, 0.6f, -1.2f), new Vector3(-1, 0.6f, 1.2f), new Vector3(1, 0.6f, 1.2f) })
                mb.Cylinder(p + w + Vector3.right * -0.06f * Mathf.Sign(w.x), p + w + Vector3.right * 0.06f * Mathf.Sign(w.x), 0.6f, 10, Palette.WoodDark);
            for (int i = 0; i < 4; i++)
            {
                float z = -1.4f + i * 0.93f;
                mb.Cylinder(p + new Vector3(-0.9f, 1.4f, z), p + new Vector3(0, 2.7f, z), 0.04f, 4, Palette.WoodDark, false);
                mb.Cylinder(p + new Vector3(0, 2.7f, z), p + new Vector3(0.9f, 1.4f, z), 0.04f, 4, Palette.WoodDark, false);
            }
            mb.GableRoof(p + Vector3.up * 1.6f, new Vector3(1.95f, 1.15f, 3.2f), Palette.Cream, Palette.Cream, 0.05f); // canvas bonnet
        }

        static void FlagPole(MeshBuilder mb, Vector3 foot, float h)
        {
            mb.Matte();
            mb.Cylinder(foot, foot + Vector3.up * h, 0.12f, 0.06f, 6, Palette.White, true);
            var top = foot + Vector3.up * (h - 0.3f);
            for (int i = 0; i < 6; i++) // red and white stripes with a blue canton
                mb.Box(top + new Vector3(1.4f, -i * 0.25f - 0.12f, 0), new Vector3(2.6f, 0.25f, 0.03f), i % 2 == 0 ? Palette.PaintRed : Palette.White);
            mb.Box(top + new Vector3(0.65f, -0.5f, -0.02f), new Vector3(1.1f, 0.75f, 0.03f), Palette.PaintBlue);
        }

        // ================================================================ Silver Lake Dam

        const float DamZ = 70, DamW = -122, DamE = -89, DamBase = -4.4f, DamTop = 6.2f;

        Site Dam()
        {
            var s = new Site { Id = "dam", Focus = new Vector3(-104, 2, DamZ - 6), Distance = 72 };
            foreach (float cx in new[] { -110f, -101f })
            {
                float x = cx;
                s.Pieces.Add(new Piece(new Vector3(x, WorldLayout.WaterLevel - 1, DamZ + 6), mb => Crib(mb, new Vector3(x, 0, DamZ + 6))));
            }
            float[] xs = { DamW, -111, -100, DamE };
            float[] ys = { DamBase, -1.8f, 0.8f, 3.4f, DamTop };
            // The footing course is under water, so it goes in as one piece; the visible courses rise section by section.
            s.Pieces.Add(new Piece(new Vector3((DamW + DamE) / 2, ys[0], DamZ), mb => { for (int k = 0; k < 3; k++) DamCourse(mb, xs[k], xs[k + 1], ys[0], ys[1]); }));
            for (int c = 1; c < 4; c++)
                for (int k = 0; k < 3; k++)
                {
                    int cc = c, kk = k; // the middle section of the top course is the spillway, a little lower
                    s.Pieces.Add(new Piece(new Vector3((xs[k] + xs[k + 1]) / 2, ys[c], DamZ), mb => DamCourse(mb, xs[kk], xs[kk + 1], ys[cc], ys[cc + 1] - (kk == 1 && cc == 3 ? 0.9f : 0))));
                }
            s.Pieces.Add(new Piece(new Vector3(-105.5f, DamTop, DamZ), mb => Piers(mb)));
            s.Pieces.Add(new Piece(new Vector3(-105.5f, DamTop, DamZ), mb => DamRail(mb)));
            var ph = At(-82, DamZ - 9);
            s.Pieces.Add(new Piece(ph, mb => { mb.Matte(); mb.Surf = SurfaceKind.Brick; mb.Box(ph + Vector3.up * 2.5f, new Vector3(7, 5, 6), Palette.Brick); mb.Surf = SurfaceKind.Auto; mb.Lit(); for (int i = -1; i <= 1; i++) mb.Box(ph + new Vector3(i * 2, 2.8f, -3.02f), new Vector3(1.1f, 1.8f, 0.04f), Palette.Window); mb.Matte(); }));
            s.Pieces.Add(new Piece(ph + Vector3.up * 5, mb => { mb.HipRoof(ph + Vector3.up * 5, new Vector3(7.6f, 1.6f, 6.6f), Palette.RoofTin); mb.Mat(0.35f, 0.6f); mb.Cylinder(new Vector3(-91, 2.5f, DamZ - 1.5f), ph + new Vector3(-3.5f, 1.6f, 1), 0.55f, 10, Palette.Steel); mb.Matte(); }));
            var poles = new List<Vector3> { ph + new Vector3(3, 0, -3) };
            for (int i = 1; i <= 4; i++) poles.Add(At(Mathf.Lerp(ph.x + 3, -60, i / 4f), Mathf.Lerp(ph.z - 3, 46, i / 4f)));
            for (int i = 1; i < poles.Count; i++)
            {
                Vector3 a = poles[i - 1], b = poles[i];
                s.Pieces.Add(new Piece(b, mb => PowerSpan(mb, a, b)));
            }
            s.Pieces.Add(new Piece(new Vector3(-105.5f, DamBase, DamZ - 3), mb => Spill(mb)));

            s.Stakes = mb => { for (float x = DamW - 4; x <= DamE + 4; x += 5) if (GY(x, DamZ) > 1.5f) Stake(mb, At(x, DamZ - 1)); Stake(mb, ph); };
            s.Camp = mb => Camp(mb, new Vector2(-131, DamZ + 12), CampKind.Stone);
            return s;
        }

        static void Crib(MeshBuilder mb, Vector3 c)
        {
            mb.Matte();
            float y0 = WorldLayout.WaterLevel - 2.2f;
            for (int i = 0; i < 4; i++)
            {
                float y = y0 + i * 0.6f;
                mb.Box(new Vector3(c.x, y, c.z - 1.4f), new Vector3(5, 0.5f, 0.5f), Palette.WoodDark);
                mb.Box(new Vector3(c.x, y, c.z + 1.4f), new Vector3(5, 0.5f, 0.5f), Palette.WoodDark);
                mb.Box(new Vector3(c.x - 2.3f, y + 0.3f, c.z), new Vector3(0.5f, 0.5f, 3.3f), Palette.WoodDark);
                mb.Box(new Vector3(c.x + 2.3f, y + 0.3f, c.z), new Vector3(0.5f, 0.5f, 3.3f), Palette.WoodDark);
            }
            mb.Box(new Vector3(c.x, y0 + 1.2f, c.z), new Vector3(4.2f, 2.3f, 2.4f), Palette.RockDark); // rock fill
        }

        static void DamCourse(MeshBuilder mb, float x0, float x1, float y0, float y1)
        {
            float t0 = Mathf.Lerp(6f, 2.4f, (y0 - DamBase) / (DamTop - DamBase)), t1 = Mathf.Lerp(6f, 2.4f, (y1 - DamBase) / (DamTop - DamBase));
            float t = (t0 + t1) / 2;
            mb.Matte();
            mb.Surf = SurfaceKind.Stone;
            // Upstream face plumb at DamZ + 1.2; the downstream face (toward the lake) battered.
            mb.Box(new Vector3((x0 + x1) / 2, (y0 + y1) / 2, DamZ + 1.2f - t / 2), new Vector3(x1 - x0 + 0.02f, y1 - y0, t), MeshBuilder.Darken(Palette.Stone, 0.9f + 0.04f * Mathf.Repeat(y0, 3f)), Palette.Stone);
            mb.Box(new Vector3((x0 + x1) / 2, y1 - 0.04f, DamZ + 1.2f - t1 - 0.02f), new Vector3(x1 - x0, 0.08f, 0.1f), Palette.StoneDark); // course joint
            mb.Surf = SurfaceKind.Auto;
        }

        static void Piers(MeshBuilder mb)
        {
            mb.Matte();
            mb.Surf = SurfaceKind.Stone;
            foreach (float x in new[] { -111f, -100f })
                mb.Box(new Vector3(x, DamTop + 0.9f, DamZ), new Vector3(1.2f, 1.8f, 2.6f), Palette.StoneDark);
            mb.Surf = SurfaceKind.Auto;
            mb.Mat(0.35f, 0.6f);
            mb.Box(new Vector3(-105.5f, DamTop + 1.7f, DamZ), new Vector3(11, 0.3f, 0.5f), Palette.Iron); // gate beam
            for (float x = -109.5f; x <= -101.5f; x += 2) mb.Box(new Vector3(x, DamTop + 0.2f, DamZ + 0.6f), new Vector3(1.8f, 1.4f, 0.12f), Palette.Steel); // sluice gates
            mb.Matte();
        }

        static void DamRail(MeshBuilder mb)
        {
            mb.Mat(0.35f, 0.6f);
            foreach (float z in new[] { DamZ + 0.1f, DamZ - 1.1f })
            {
                for (float x = DamW + 1; x <= DamE - 1; x += 2.2f) mb.Box(new Vector3(x, DamTop + 0.5f, z), new Vector3(0.07f, 1f, 0.07f), Palette.Iron);
                mb.Box(new Vector3((DamW + DamE) / 2, DamTop + 0.98f, z), new Vector3(DamE - DamW - 2, 0.06f, 0.06f), Palette.Iron);
            }
            mb.Matte();
        }

        static void PowerSpan(MeshBuilder mb, Vector3 a, Vector3 b)
        {
            Vector3 f = b - a;
            f.y = 0;
            var r = Vector3.Cross(Vector3.up, f.normalized);
            Pole(mb, b, r);
            mb.Mat(0.4f, 0.5f);
            foreach (float k in new[] { -1.05f, 1.05f })
            {
                Vector3 p0 = a + Vector3.up * (PoleH + 0.06f) + r * k, p1 = b + Vector3.up * (PoleH + 0.06f) + r * k;
                Vector3 prev = p0;
                for (int i = 1; i <= 6; i++)
                {
                    float t = i / 6f;
                    var q = Vector3.Lerp(p0, p1, t) + Vector3.down * (0.6f * 4 * t * (1 - t));
                    mb.Cylinder(prev, q, 0.05f, 3, Palette.Iron, false);
                    prev = q;
                }
            }
            mb.Matte();
        }

        static void Spill(MeshBuilder mb)
        {
            // Water sheeting over the spillway and down the battered face into the river.
            mb.Mat(0.85f);
            var col = new Color(0.82f, 0.9f, 0.95f);
            float top = DamTop - 0.9f;
            for (int i = 0; i < 6; i++)
            {
                float y0 = Mathf.Lerp(top, DamBase, i / 6f), y1 = Mathf.Lerp(top, DamBase, (i + 1) / 6f);
                float z0 = DamZ + 1.2f - Mathf.Lerp(6f, 2.4f, (y0 - DamBase) / (DamTop - DamBase)) - 0.12f;
                float z1 = DamZ + 1.2f - Mathf.Lerp(6f, 2.4f, (y1 - DamBase) / (DamTop - DamBase)) - 0.12f;
                mb.Quad(new Vector3(-110.6f, y0, z0), new Vector3(-100.4f, y0, z0), new Vector3(-100.4f, y1, z1), new Vector3(-110.6f, y1, z1), MeshBuilder.Lerp(col, Color.white, 0.25f * (i % 2)));
            }
            mb.Matte();
        }

        // ================================================================ The Golden Spike

        // In the open ground east of the Coppertop spur (the railhead), between the main line and the mission road.
        const float SpikeX = -122, SpikeZ = -70;

        Site Spike()
        {
            var s = new Site { Id = "goldenspike", Focus = new Vector3(SpikeX, 4, SpikeZ + 2), Distance = 70 };
            float g = GY(SpikeX, SpikeZ) + 0.05f;
            var c = new Vector3(SpikeX, g, SpikeZ);
            foreach (float half in new[] { -1f, 1f })
            {
                float h = half;
                s.Pieces.Add(new Piece(c + new Vector3(h * 5, 0, 0), mb => { mb.Matte(); mb.Surf = SurfaceKind.Stone; mb.Box(c + new Vector3(h * 5, 0.08f, 0), new Vector3(10, 0.16f, 16), Palette.Stone); mb.Surf = SurfaceKind.Auto; }));
            }
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                s.Pieces.Add(new Piece(c + Vector3.up * (0.16f + k * 0.45f), mb => { mb.Matte(); mb.Surf = SurfaceKind.Stone; mb.Box(c + Vector3.up * (0.38f + k * 0.45f), new Vector3(6.4f - k * 1.6f, 0.45f, 6.4f - k * 1.6f), MeshBuilder.Darken(Palette.Stone, 0.92f - k * 0.04f)); mb.Surf = SurfaceKind.Auto; }));
            }
            float baseY = g + 1.5f;
            for (int i = 0; i < 4; i++)
            {
                int k = i;
                s.Pieces.Add(new Piece(new Vector3(SpikeX, baseY + k * 2f, SpikeZ), mb => Obelisk(mb, c, baseY, k)));
            }
            s.Pieces.Add(new Piece(new Vector3(SpikeX, baseY + 8, SpikeZ), mb => GoldSpike(mb, new Vector3(SpikeX, baseY + 8, SpikeZ))));
            var flags = new[] { At(SpikeX - 9, SpikeZ - 7), At(SpikeX + 9, SpikeZ - 7), At(SpikeX - 9, SpikeZ + 7), At(SpikeX + 9, SpikeZ + 7) };
            s.Pieces.Add(new Piece(flags[0], mb => { FlagPole(mb, flags[0], 9); FlagPole(mb, flags[1], 9); }));
            s.Pieces.Add(new Piece(flags[2], mb => { FlagPole(mb, flags[2], 9); FlagPole(mb, flags[3], 9); }));
            s.Pieces.Add(new Piece(flags[0] + Vector3.up * 7, mb => { Bunting(mb, flags[0] + Vector3.up * 7.5f, flags[1] + Vector3.up * 7.5f); Bunting(mb, flags[2] + Vector3.up * 7.5f, flags[3] + Vector3.up * 7.5f); }));
            s.Pieces.Add(new Piece(flags[0] + Vector3.up * 7, mb => { Bunting(mb, flags[0] + Vector3.up * 7.5f, flags[2] + Vector3.up * 7.5f); Bunting(mb, flags[1] + Vector3.up * 7.5f, flags[3] + Vector3.up * 7.5f); }));
            var stand = At(SpikeX, SpikeZ - 13);
            s.Pieces.Add(new Piece(stand, mb => Grandstand(mb, stand, false)));
            s.Pieces.Add(new Piece(stand + Vector3.up * 2, mb => Grandstand(mb, stand, true)));
            s.Pieces.Add(new Piece(c, mb => { foreach (var o in new[] { new Vector3(-4.6f, 0, -4.6f), new Vector3(4.6f, 0, -4.6f), new Vector3(-4.6f, 0, 4.6f), new Vector3(4.6f, 0, 4.6f) }) Lantern(mb, c + o); }));

            s.DoneModel = "goldenspike_done"; s.DoneAt = At(SpikeX, SpikeZ); s.DoneYaw = 0;
            s.Stakes = mb => { foreach (var o in new[] { new Vector2(-10, -8), new Vector2(10, -8), new Vector2(-10, 8), new Vector2(10, 8), new Vector2(0, 0) }) Stake(mb, At(SpikeX + o.x, SpikeZ + o.y)); };
            s.Camp = mb => Camp(mb, new Vector2(SpikeX + 17, SpikeZ + 6), CampKind.Stone);
            return s;
        }

        static void Obelisk(MeshBuilder mb, Vector3 c, float baseY, int k)
        {
            float w0 = Mathf.Lerp(1.9f, 1.1f, k / 4f), w1 = Mathf.Lerp(1.9f, 1.1f, (k + 1) / 4f);
            mb.Matte();
            mb.Surf = SurfaceKind.Stone;
            mb.Box(new Vector3(c.x, baseY + k * 2 + 1, c.z), new Vector3((w0 + w1) / 2, 2.02f, (w0 + w1) / 2), MeshBuilder.Darken(MeshBuilder.Lighten(Palette.Stone, 0.25f), 0.95f - k * 0.02f));
            mb.Surf = SurfaceKind.Auto;
            if (k == 0)
            {
                mb.Mat(0.6f, 0.8f);
                mb.Box(new Vector3(c.x, baseY + 1.2f, c.z - (w0 + w1) / 4 - 0.02f), new Vector3(1.1f, 0.6f, 0.04f), Palette.Brass); // bronze plaque
                mb.Matte();
            }
        }

        static void GoldSpike(MeshBuilder mb, Vector3 b)
        {
            mb.Mat(0.85f, 0.95f);
            mb.Box(b + Vector3.up * 0.25f, new Vector3(1.3f, 0.5f, 1.3f), Palette.Brass); // spike head
            mb.Cylinder(b + Vector3.up * 0.5f, b + Vector3.up * 3.6f, 0.42f, 0.04f, 8, Palette.PaintYellow, true);
            mb.Matte();
        }

        static void Bunting(MeshBuilder mb, Vector3 a, Vector3 b)
        {
            mb.Matte();
            var cols = new[] { Palette.PaintRed, Palette.White, Palette.PaintBlue };
            int n = Mathf.Max(4, Mathf.RoundToInt(Vector3.Distance(a, b) / 0.9f));
            var dir = (b - a).normalized;
            for (int i = 0; i < n; i++)
            {
                float t = (i + 0.5f) / n;
                var p = Vector3.Lerp(a, b, t) + Vector3.down * (1.2f * 4 * t * (1 - t));
                mb.Tri(p - dir * 0.3f, p + dir * 0.3f, p + Vector3.down * 0.6f, cols[i % 3]);
                mb.Tri(p + dir * 0.3f, p - dir * 0.3f, p + Vector3.down * 0.6f, cols[i % 3]);
            }
        }

        static void Grandstand(MeshBuilder mb, Vector3 p, bool roof)
        {
            mb.Matte();
            if (!roof)
            {
                mb.Surf = SurfaceKind.Planks;
                for (int i = 0; i < 4; i++)
                    mb.Box(p + new Vector3(0, 0.3f + i * 0.5f, -i * 0.8f), new Vector3(12, 0.6f + i * 1.0f, 0.8f), MeshBuilder.Darken(Palette.WoodPale, 0.9f + 0.03f * i));
                mb.Surf = SurfaceKind.Auto;
                return;
            }
            foreach (float x in new[] { -5.8f, 0f, 5.8f }) mb.Cylinder(p + new Vector3(x, 0, -3f), p + new Vector3(x, 4.6f, -3f), 0.12f, 6, Palette.White);
            for (int i = 0; i < 6; i++) // striped awning
                mb.Box(p + new Vector3(-5 + i * 2, 4.7f, -1.8f), new Vector3(2, 0.12f, 3.2f), i % 2 == 0 ? Palette.PaintRed : Palette.White);
        }
    }
}
