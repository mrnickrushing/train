using UnityEngine;

namespace Ironhorse.Game
{
    /// <summary>Generates ballast, sleepers and rails along a TrackPath, plus trestles over water.</summary>
    public static class TrackBuilder
    {
        public const float Gauge = 1.5f;
        static readonly Color RailColor = Palette.Hex(0x5A4438);  // rusty web and foot
        static readonly Color RailHead = Palette.Hex(0x96918B);   // wheel-polished top, dulled
        static readonly Color Sleeper = Palette.Hex(0x54402F);
        static readonly Color BallastTop = Palette.Hex(0x716960), BallastSide = Palette.Hex(0x655D54), OilStain = Palette.Hex(0x4A433C);

        /// <summary>Stable 0..1 hash of a distance along a track, for per-sleeper variation.</summary>
        static float Hash(float s) { return Mathf.Repeat(Mathf.Sin(s * 12.9898f + 78.233f) * 43758.5453f, 1f); }

        public static void Build(MeshBuilder mb, TrackPath path, float from = 0, float to = -1, bool trestleOverWater = false)
        {
            if (to < 0) to = path.Length;
            float y = WorldLayout.RailTop;
            // Ballast bed: a trapezoid strip.
            const float step = 2f;
            Color ballastA = BallastTop, ballastB = BallastSide;
            for (float s = from; s < to - 0.01f; s += step)
            {
                float s1 = Mathf.Min(to, s + step);
                Vector3 p0, f0, p1, f1;
                path.Sample(s, out p0, out f0);
                path.Sample(s1, out p1, out f1);
                Vector3 r0 = Vector3.Cross(Vector3.up, f0).normalized, r1 = Vector3.Cross(Vector3.up, f1).normalized;
                float gy0 = GroundY(p0, trestleOverWater), gy1 = GroundY(p1, trestleOverWater);
                Vector3 b0 = new Vector3(p0.x, gy0, p0.z), b1 = new Vector3(p1.x, gy1, p1.z);
                float topY = y - 0.2f;
                bool deck = trestleOverWater && (gy0 < -0.5f || gy1 < -0.5f);
                if (!deck)
                {
                    mb.Matte();
                    mb.Surf = SurfaceKind.Gravel;
                    // top, with a darker oil-stained band between the rails where engines drip
                    mb.Quad(new Vector3(b0.x, topY, b0.z) - r0 * 1.6f, new Vector3(b1.x, topY, b1.z) - r1 * 1.6f, new Vector3(b1.x, topY, b1.z) + r1 * 1.6f, new Vector3(b0.x, topY, b0.z) + r0 * 1.6f, ballastA);
                    float oy = topY + 0.006f;
                    mb.Quad(new Vector3(b0.x, oy, b0.z) - r0 * 0.55f, new Vector3(b1.x, oy, b1.z) - r1 * 0.55f, new Vector3(b1.x, oy, b1.z) + r1 * 0.55f, new Vector3(b0.x, oy, b0.z) + r0 * 0.55f, OilStain);
                    // slopes
                    mb.Quad(b0 - r0 * 2.6f + Vector3.up * -0.05f, b1 - r1 * 2.6f + Vector3.up * -0.05f, new Vector3(b1.x, topY, b1.z) - r1 * 1.6f, new Vector3(b0.x, topY, b0.z) - r0 * 1.6f, ballastB);
                    mb.Quad(new Vector3(b0.x, topY, b0.z) + r0 * 1.6f, new Vector3(b1.x, topY, b1.z) + r1 * 1.6f, b1 + r1 * 2.6f + Vector3.up * -0.05f, b0 + r0 * 2.6f + Vector3.up * -0.05f, ballastB);
                    mb.Surf = SurfaceKind.Auto;
                }
                else
                {
                    // Timber deck and trestle bents.
                    mb.Matte();
                    mb.Quad(new Vector3(p0.x, topY, p0.z) - r0 * 1.8f, new Vector3(p1.x, topY, p1.z) - r1 * 1.8f, new Vector3(p1.x, topY, p1.z) + r1 * 1.8f, new Vector3(p0.x, topY, p0.z) + r0 * 1.8f, Palette.WoodDark);
                    if (Mathf.Repeat(s, 6f) < step)
                        Trestle(mb, new Vector3(p0.x, topY, p0.z), f0, Mathf.Min(gy0, -2.5f));
                }
            }

            // Sleepers: hand-laid, so each sits a little askew and has weathered differently.
            mb.Matte();
            mb.Surf = SurfaceKind.WoodGrain;
            for (float s = from + 0.4f; s < to; s += 0.75f)
            {
                Vector3 p, f;
                path.Sample(s, out p, out f);
                float h = Hash(s), h2 = Hash(s + 0.37f);
                var rot = Quaternion.LookRotation(f, Vector3.up) * Quaternion.Euler(0, (h - 0.5f) * 3f, 0);
                mb.Push().SetMatrix(Matrix4x4.TRS(new Vector3(p.x, y - 0.17f, p.z) + f * (h2 - 0.5f) * 0.06f, rot, Vector3.one));
                var col = MeshBuilder.Lerp(MeshBuilder.Darken(Sleeper, 0.8f + 0.35f * h), Palette.Hex(0x6E655B), h2 > 0.8f ? 0.45f : 0f); // a few silvered old ties
                mb.Box(Vector3.zero, new Vector3(2.5f + (h2 - 0.5f) * 0.12f, 0.14f, 0.3f), col);
                mb.Pop();
            }
            mb.Surf = SurfaceKind.None;

            // Rails: continuous extrusions.
            for (int side = -1; side <= 1; side += 2)
            {
                float off = side * Gauge * 0.5f;
                for (float s = from; s < to - 0.01f; s += step)
                {
                    float s1 = Mathf.Min(to, s + step);
                    Vector3 p0, f0, p1, f1;
                    path.Sample(s, out p0, out f0);
                    path.Sample(s1, out p1, out f1);
                    Vector3 r0 = Vector3.Cross(Vector3.up, f0).normalized, r1 = Vector3.Cross(Vector3.up, f1).normalized;
                    Vector3 c0 = new Vector3(p0.x, y, p0.z) + r0 * off, c1 = new Vector3(p1.x, y, p1.z) + r1 * off;
                    const float w = 0.09f, h = 0.16f;
                    mb.Mat(0.38f, 0.7f); // worn bright by wheels, but dull enough not to shimmer as thin white lines
                    mb.Quad(c0 - r0 * w, c1 - r1 * w, c1 + r1 * w, c0 + r0 * w, RailHead); // head
                    mb.Mat(0.2f, 0.2f);
                    mb.Quad(c0 - r0 * w - Vector3.up * h, c1 - r1 * w - Vector3.up * h, c1 - r1 * w, c0 - r0 * w, RailColor);
                    mb.Quad(c0 + r0 * w, c1 + r1 * w, c1 + r1 * w - Vector3.up * h, c0 + r0 * w - Vector3.up * h, RailColor);
                }
            }
            mb.Matte();
            mb.Surf = SurfaceKind.Auto;
        }

        static float GroundY(Vector3 p, bool water)
        {
            if (!water) return 0;
            return ValleyTerrain.Height(p.x, p.z);
        }

        static void Trestle(MeshBuilder mb, Vector3 top, Vector3 fwd, float groundY)
        {
            var right = Vector3.Cross(Vector3.up, fwd).normalized;
            float h = top.y - groundY;
            mb.Matte();
            for (int k = -1; k <= 1; k += 2)
            {
                Vector3 a = top + right * k * 0.9f, b = new Vector3(a.x, groundY, a.z) + right * k * 0.9f;
                mb.Cylinder(b, a, 0.14f, 5, Palette.WoodDark);
            }
            mb.Cylinder(new Vector3(top.x, groundY, top.z), top, 0.14f, 5, Palette.WoodDark);
            // cross bracing
            for (float yy = groundY + 1.2f; yy < top.y - 0.4f; yy += 1.4f)
            {
                mb.Push().SetMatrix(Matrix4x4.TRS(new Vector3(top.x, yy, top.z), Quaternion.LookRotation(fwd), Vector3.one));
                mb.Box(Vector3.zero, new Vector3(3.8f, 0.14f, 0.14f), Palette.Wood);
                mb.Pop();
            }
        }

        /// <summary>Raised platform between tracks: dressed-stone walls, coping stones and a weathered plank deck.</summary>
        public static void Platform(MeshBuilder mb, float z, float width, float halfLength)
        {
            mb.Matte();
            mb.Surf = SurfaceKind.Stone;
            mb.Box(new Vector3(0, 0.42f, z), new Vector3(halfLength * 2, 0.84f, width), Palette.StoneDark);
            // Coping stones, overhanging the wall a little, in slightly mismatched blocks.
            for (int side = -1; side <= 1; side += 2)
                for (float x = -halfLength; x < halfLength - 0.01f; x += 1.5f)
                {
                    float len = Mathf.Min(1.5f, halfLength - x);
                    var c = MeshBuilder.Darken(Palette.Stone, 0.86f + 0.14f * Hash(x * 3.1f + side));
                    mb.Box(new Vector3(x + len / 2, 0.88f, z + side * (width / 2 - 0.22f)), new Vector3(len - 0.03f, 0.1f, 0.56f), c);
                }
            // Plank deck in sections laid at different times.
            mb.Surf = SurfaceKind.Planks;
            for (float x = -halfLength; x < halfLength - 0.01f; x += 3f)
            {
                float len = Mathf.Min(3f, halfLength - x), h = Hash(x + z);
                var c = MeshBuilder.Lerp(Palette.Hex(0x8E7A62), Palette.Hex(0x9A948A), h * 0.6f);
                mb.Box(new Vector3(x + len / 2, 0.87f, z), new Vector3(len - 0.02f, 0.06f, width - 0.96f), MeshBuilder.Darken(c, 0.9f + 0.1f * Hash(x * 7.7f)));
            }
            mb.Surf = SurfaceKind.Auto;
        }
    }
}
