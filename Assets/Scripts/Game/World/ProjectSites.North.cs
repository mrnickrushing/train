using UnityEngine;

namespace Ironhorse.Game
{
    /// <summary>The Eagle Lake projects in the map's north region: the lighthouse on Eagle Point (boats only) and the
    /// lakeshore lodge (by the river road or by boat).</summary>
    public sealed partial class ProjectSites
    {
        static Rect LighthouseRect { get { var c = WorldLayout.IslandCenter; return new Rect(c.x - 11, c.z - 9, 22, 18); } }
        static readonly Rect LodgeRect = new Rect(LodgeX - 26, LodgeZ - 16, 52, 34);

        // ================================================================ Eagle Point Lighthouse

        Site Lighthouse()
        {
            var ic = WorldLayout.IslandCenter;
            var s = new Site { Id = "lighthouse", Focus = new Vector3(ic.x, 8, ic.z - 6), Distance = 78 };
            var top = At(ic.x - 2, ic.z + 1);
            float y = top.y;
            var foot = new Vector3(top.x, y, top.z);
            Color white = Palette.White, red = Palette.PaintRed, stone = Palette.Stone;

            // Rock-built base and the first courses of the tower.
            s.Pieces.Add(new Piece(foot, mb => { mb.Matte(); mb.Surf = SurfaceKind.Stone; mb.Cylinder(foot - Vector3.up * 1.2f, foot + Vector3.up * 0.9f, 4.6f, 4.2f, 10, Palette.StoneDark, true); mb.Surf = SurfaceKind.Auto; }));
            const int segs = 6;
            for (int i = 0; i < segs; i++)
            {
                float y0 = 0.9f + i * 3f, y1 = y0 + 3f;
                float r0 = Mathf.Lerp(2.7f, 1.8f, i / (float)segs), r1 = Mathf.Lerp(2.7f, 1.8f, (i + 1) / (float)segs);
                var col = i % 2 == 0 ? white : red;
                int k = i;
                s.Pieces.Add(new Piece(foot + Vector3.up * y0, mb =>
                {
                    mb.Paint();
                    mb.Cylinder(foot + Vector3.up * y0, foot + Vector3.up * y1, r0, r1, 14, col, true);
                    if (k % 2 == 1) { mb.Lit(); mb.Box(foot + new Vector3(0, y0 + 1.5f, -r0 + 0.02f), new Vector3(0.5f, 0.8f, 0.08f), Palette.Window); }
                }));
            }
            float gy = 0.9f + segs * 3f;
            // Gallery, lantern room and the cap.
            s.Pieces.Add(new Piece(foot + Vector3.up * gy, mb =>
            {
                mb.Paint();
                mb.Cylinder(foot + Vector3.up * gy, foot + Vector3.up * (gy + 0.3f), 2.7f, 12, Palette.Iron);
                for (int i = 0; i < 16; i++)
                {
                    float a = i * Mathf.PI * 2 / 16;
                    var p = foot + new Vector3(Mathf.Cos(a) * 2.55f, gy + 0.3f, Mathf.Sin(a) * 2.55f);
                    mb.Cylinder(p, p + Vector3.up * 0.9f, 0.04f, 4, Palette.Iron, false);
                }
                mb.Cylinder(foot + Vector3.up * (gy + 1.15f), foot + Vector3.up * (gy + 1.22f), 2.6f, 16, Palette.Iron, false);
            }));
            s.Pieces.Add(new Piece(foot + Vector3.up * (gy + 0.3f), mb =>
            {
                mb.Lit();
                mb.Cylinder(foot + Vector3.up * (gy + 0.3f), foot + Vector3.up * (gy + 2.4f), 1.5f, 10, Palette.Hex(0xFFE7A0));
                mb.Paint();
                mb.Cylinder(foot + Vector3.up * (gy + 2.4f), foot + Vector3.up * (gy + 3.3f), 1.75f, 0.25f, 12, red, true);
                mb.Cylinder(foot + Vector3.up * (gy + 3.3f), foot + Vector3.up * (gy + 4.1f), 0.08f, 6, Palette.Iron);
            }));
            // Keeper's cottage, oil store and the jetty where the boats tie up.
            var cot = At(ic.x + 7, ic.z - 3);
            s.Pieces.Add(new Piece(cot, mb =>
            {
                mb.Matte();
                mb.Box(cot + Vector3.up * 1.5f, new Vector3(6, 3, 4.2f), white, white);
                mb.GableRoof(cot + Vector3.up * 3f, new Vector3(6.6f, 1.7f, 4.8f), red, white, 0.3f);
                mb.Box(cot + new Vector3(1.8f, 3.6f, 0.6f), new Vector3(0.6f, 1.6f, 0.6f), Palette.Brick);
                mb.Box(cot + new Vector3(0, 1.1f, -2.13f), new Vector3(0.9f, 2.0f, 0.06f), Palette.WoodDark);
                mb.Lit();
                mb.Box(cot + new Vector3(-1.8f, 1.7f, -2.13f), new Vector3(0.9f, 0.9f, 0.05f), Palette.Window);
            }));
            var shed = At(ic.x - 8, ic.z - 5);
            s.Pieces.Add(new Piece(shed, mb => { mb.Matte(); mb.Box(shed + Vector3.up * 1f, new Vector3(2.6f, 2f, 2.2f), Palette.Weathered); mb.GableRoof(shed + Vector3.up * 2f, new Vector3(3f, 0.9f, 2.6f), Palette.RoofTin, Palette.Weathered, 0.2f); }));
            var jetty = HaulRoutes.LighthouseDock;
            s.Pieces.Add(new Piece(jetty, mb => Jetty(mb, new Vector3(ic.x + 6, 0, ic.z - 10), jetty)));

            s.Stakes = mb => { foreach (var o in new[] { new Vector2(-4, -2), new Vector2(0, 4), new Vector2(7, -3), new Vector2(-8, -5) }) Stake(mb, At(ic.x + o.x, ic.z + o.y)); };
            s.Camp = mb => Camp(mb, new Vector2(ic.x + 2, ic.z - 9), CampKind.Stone);
            return s;
        }

        /// <summary>A plank jetty on pilings from the shore out to a boat landing.</summary>
        static void Jetty(MeshBuilder mb, Vector3 from, Vector3 to)
        {
            float deck = WorldLayout.WaterLevel + 1.2f;
            var dir = to - from; dir.y = 0;
            float len = dir.magnitude; dir /= len;
            var right = Vector3.Cross(Vector3.up, dir);
            var rot = Quaternion.LookRotation(dir);
            mb.Matte();
            mb.Surf = SurfaceKind.Planks;
            for (float s = 0; s < len; s += 0.6f)
            {
                var p = from + dir * s; p.y = deck;
                mb.Push().Translate(p).Rotate(rot);
                mb.Box(Vector3.zero, new Vector3(s > len - 4 ? 5f : 2.4f, 0.14f, 0.55f), MeshBuilder.Darken(Palette.WoodPale, 0.84f + 0.14f * Mathf.PerlinNoise(s, 7)));
                mb.Pop();
            }
            mb.Surf = SurfaceKind.Auto;
            for (float s = 1.5f; s < len; s += 2.8f)
                for (int side = -1; side <= 1; side += 2)
                {
                    var p = from + dir * s + right * side * (s > len - 4 ? 2.3f : 1.1f);
                    mb.Cylinder(new Vector3(p.x, WorldLayout.WaterLevel - 2.4f, p.z), new Vector3(p.x, deck + 0.5f, p.z), 0.15f, 6, Palette.WoodDark);
                }
        }

        // ================================================================ Eagle Lake Lodge

        const float LodgeX = 46, LodgeZ = 494;

        Site Lodge()
        {
            var s = new Site { Id = "lodge", Focus = new Vector3(LodgeX, 6, LodgeZ - 2), Distance = 80 };
            float floor = 0;
            for (float x = -10; x <= 10; x += 5) for (float z = -5; z <= 5; z += 5) floor = Mathf.Max(floor, GY(LodgeX + x, LodgeZ + z));
            floor += 0.6f;
            var c = new Vector3(LodgeX, floor, LodgeZ);
            Color logs = Palette.Hex(0x7A5034), logsDark = Palette.Hex(0x5A3A24), roof = Palette.RoofGreen, trim = Palette.Cream;

            s.Pieces.Add(new Piece(new Vector3(LodgeX, floor - 2, LodgeZ), mb => { mb.Matte(); mb.Surf = SurfaceKind.Stone; mb.Box(new Vector3(LodgeX, floor - 1.4f, LodgeZ), new Vector3(22, 2.8f, 11), Palette.StoneDark, Palette.Stone); mb.Surf = SurfaceKind.Auto; }));
            // Log walls, two storeys, then the big roof with dormers.
            s.Pieces.Add(new Piece(c, mb =>
            {
                mb.Matte();
                mb.Surf = SurfaceKind.WoodGrain;
                for (int i = 0; i < 9; i++) mb.Box(c + new Vector3(0, 0.2f + i * 0.4f, 0), new Vector3(20, 0.42f, 9), i % 2 == 0 ? logs : logsDark);
                mb.Surf = SurfaceKind.Auto;
                mb.Lit();
                for (int i = -3; i <= 3; i++) if (i != 0) mb.Box(c + new Vector3(i * 2.6f, 1.8f, -4.53f), new Vector3(1.1f, 1.4f, 0.06f), Palette.Window);
                mb.Matte();
                mb.Box(c + new Vector3(0, 1.3f, -4.53f), new Vector3(1.6f, 2.6f, 0.06f), Palette.WoodDark);
            }));
            s.Pieces.Add(new Piece(c + Vector3.up * 3.7f, mb =>
            {
                mb.Matte();
                mb.Surf = SurfaceKind.WoodGrain;
                for (int i = 0; i < 7; i++) mb.Box(c + new Vector3(0, 3.9f + i * 0.4f, 0), new Vector3(19, 0.42f, 8.4f), i % 2 == 0 ? logs : logsDark);
                mb.Surf = SurfaceKind.Auto;
                mb.Lit();
                for (int i = -3; i <= 3; i++) mb.Box(c + new Vector3(i * 2.6f, 5.2f, -4.23f), new Vector3(1.0f, 1.2f, 0.06f), Palette.Window);
            }));
            s.Pieces.Add(new Piece(c + Vector3.up * 6.6f, mb =>
            {
                mb.Matte();
                mb.Surf = SurfaceKind.Tin;
                mb.GableRoof(c + Vector3.up * 6.6f, new Vector3(21, 4.2f, 10.6f), roof, logs, 0.6f);
                mb.Surf = SurfaceKind.Auto;
                foreach (float x in new[] { -5.5f, 0f, 5.5f })
                {
                    mb.Box(c + new Vector3(x, 7.9f, -3.6f), new Vector3(1.8f, 1.6f, 1.8f), logs);
                    mb.GableRoof(c + new Vector3(x, 8.7f, -3.6f), new Vector3(2.2f, 0.9f, 2.2f), roof, logs, 0.15f);
                    mb.Lit(); mb.Box(c + new Vector3(x, 7.9f, -4.52f), new Vector3(0.9f, 0.8f, 0.05f), Palette.Window); mb.Matte();
                }
            }));
            foreach (float x in new[] { -8.5f, 8.5f })
            {
                var ch = c + new Vector3(x, 0, 2);
                s.Pieces.Add(new Piece(ch, mb => { mb.Matte(); mb.Surf = SurfaceKind.Stone; mb.Box(ch + Vector3.up * 5.5f, new Vector3(1.6f, 11f, 1.6f), Palette.Stone); mb.Surf = SurfaceKind.Auto; }));
            }
            // Veranda across the lake front.
            s.Pieces.Add(new Piece(c + new Vector3(0, 0, -5.6f), mb =>
            {
                mb.Matte();
                mb.Surf = SurfaceKind.Planks;
                mb.Box(c + new Vector3(0, -0.1f, -6.2f), new Vector3(20, 0.25f, 3.2f), Palette.WoodPale);
                mb.Surf = SurfaceKind.Auto;
                for (float x = -9.5f; x <= 9.5f; x += 2.375f) mb.Cylinder(c + new Vector3(x, 0, -7.6f), c + new Vector3(x, 3.4f, -7.6f), 0.14f, 6, trim);
                mb.Box(c + new Vector3(0, 3.5f, -6.4f), new Vector3(20.6f, 0.2f, 3.6f), roof);
                mb.Box(c + new Vector3(0, 0.9f, -7.6f), new Vector3(19.5f, 0.1f, 0.1f), trim);
            }));
            // Boathouse and dock on the water, a flag and a gazebo on the lawn.
            var bh = new Vector3(LodgeX - 14, Mathf.Max(GY(LodgeX - 14, LodgeZ + 13), WorldLayout.WaterLevel + 1.2f), LodgeZ + 13);
            s.Pieces.Add(new Piece(bh, mb =>
            {
                mb.Matte();
                mb.Box(bh + Vector3.up * 1.6f, new Vector3(6, 3.2f, 7), Palette.Hex(0x8C3A2A), Palette.Hex(0x8C3A2A));
                mb.GableRoof(bh + Vector3.up * 3.2f, new Vector3(6.6f, 1.8f, 7.6f), roof, Palette.Hex(0x8C3A2A), 0.3f);
                mb.Box(bh + new Vector3(0, 1.2f, 3.53f), new Vector3(3.6f, 2.4f, 0.06f), Palette.WoodDark);
            }));
            var dock = HaulRoutes.LodgeDock;
            s.Pieces.Add(new Piece(dock, mb => Jetty(mb, new Vector3(LodgeX - 2, 0, LodgeZ + 9), dock)));
            var flag = At(LodgeX + 14, LodgeZ - 8);
            s.Pieces.Add(new Piece(flag, mb => FlagPole(mb, flag, 10f)));
            var gz = At(LodgeX + 16, LodgeZ + 6);
            s.Pieces.Add(new Piece(gz, mb =>
            {
                mb.Paint();
                for (int i = 0; i < 6; i++) { float a = i * Mathf.PI / 3; mb.Cylinder(gz + new Vector3(Mathf.Cos(a) * 2, 0, Mathf.Sin(a) * 2), gz + new Vector3(Mathf.Cos(a) * 2, 2.6f, Mathf.Sin(a) * 2), 0.1f, 6, trim); }
                mb.Cylinder(gz + Vector3.up * 0.15f, gz + Vector3.up * 0.3f, 2.3f, 12, Palette.WoodPale);
                mb.Cylinder(gz + Vector3.up * 2.6f, gz + Vector3.up * 3.8f, 2.6f, 0.1f, 12, roof, true);
            }));
            foreach (var o in new[] { new Vector2(-12, -9), new Vector2(12, -9) })
            {
                var p = At(LodgeX + o.x, LodgeZ + o.y);
                s.Pieces.Add(new Piece(p, mb => Crates(mb, p)));
            }

            s.Stakes = mb => { foreach (var o in new[] { new Vector2(-10, -5), new Vector2(10, -5), new Vector2(-10, 5), new Vector2(10, 5), new Vector2(-14, 13) }) Stake(mb, At(LodgeX + o.x, LodgeZ + o.y)); };
            s.Camp = mb => Camp(mb, new Vector2(LodgeX + 18, LodgeZ - 10), CampKind.Timber);
            return s;
        }
    }
}
