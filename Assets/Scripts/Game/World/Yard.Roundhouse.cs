using System.Collections.Generic;
using System.Linq;
using Ironhorse.Core;
using UnityEngine;

namespace Ironhorse.Game
{
    /// <summary>
    /// The engine terminal west of town: a turntable in a stone pit with tracks radiating all round it, a brick
    /// roundhouse of arched stalls on the far side, the machine shop with its tall chimney, open storage tracks with
    /// engines parked on them, and a lead curving down to the main line. Laid out like a real terminal so it reads at a
    /// glance from the map.
    /// </summary>
    public static partial class Yard
    {
        public const float PitR = 10f;
        const float Inner = 17f, Outer = 37f, HIn = 8.2f, HOut = 7.0f;
        const int Stalls = 8;
        const float StallStart = 95f, StallStep = 15f;
        /// <summary>Open storage tracks off the turntable (angle in degrees from +X toward +Z, length from the centre).</summary>
        static readonly (float ang, float len)[] OpenTracks = { (40, 26), (25, 28), (10, 34), (-5, 40), (-20, 38), (-35, 34), (-72, 22), (-87, 22) };
        const float LeadAng = -55f, ShopAng = 68f;
        /// <summary>Where the lead meets the main line (west of the station throat at x = -120).</summary>
        const float LeadMergeX = -124f;
        const float CoalStageYaw = 90f;
        /// <summary>The lead from the turntable rim to the main line, and the distance along it where its rails join the main line's.</summary>
        public static TrackPath Lead; public static float LeadStop;
        /// <summary>Outer edge of the turntable's stone rim (tracks run onto it).</summary>
        const float RimR = PitR + 1.8f;

        static Vector3 C { get { return WorldLayout.TurntableCenter; } }
        static float _gy;
        static Vector3 Dir(float deg) { float a = deg * Mathf.Deg2Rad; return new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); }
        static Vector3 Pt(float r, float deg, float y = 0) { return C + Dir(deg) * r + Vector3.up * (_gy + y); }

        /// <summary>Every direction a track leaves the turntable (the bridge lines up with these).</summary>
        public static IEnumerable<float> TrackAngles()
        {
            for (int i = 0; i < Stalls; i++) yield return StallStart + (i + 0.5f) * StallStep;
            foreach (var t in OpenTracks) yield return t.ang;
            yield return LeadAng;
            yield return ShopAng;
            yield return DestinationRoutes.LinkAngle;
        }

        /// <summary>True on the terminal's ground: the pit apron (the terrain shows worn cinders there).</summary>
        public static float Apron(float x, float z)
        {
            float d = Mathf.Sqrt((x - C.x) * (x - C.x) + (z - C.z) * (z - C.z));
            return 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(PitR + 3, PitR + 8, d));
        }

        static void BuildRoundhouse(Transform root)
        {
            _gy = ValleyTerrain.Height(C.x, C.z);
            for (int k = 0; k < 16; k++)  // the pit floor must sit above the ground everywhere under it
                for (float r = 3; r <= PitR; r += 3.5f) _gy = Mathf.Max(_gy, ValleyTerrain.Height(C.x + Mathf.Cos(k * 0.3927f) * r, C.z + Mathf.Sin(k * 0.3927f) * r));
            var mb = new MeshBuilder();
            float rail = WorldLayout.RailTop;

            // ---- tracks
            for (int i = 0; i < Stalls; i++)
            {
                float a = StallStart + (i + 0.5f) * StallStep;
                TrackBuilder.Build(mb, new TrackPath().Add(Pt(PitR + 0.1f, a, rail)).Line(Pt(Outer - 1.5f, a, rail)).Build());
            }
            foreach (var t in OpenTracks)
            {
                TrackBuilder.Build(mb, new TrackPath().Add(Pt(PitR + 0.1f, t.ang, rail)).Line(Pt(t.len, t.ang, rail)).Build());
                var end = Pt(t.len + 0.6f, t.ang);
                mb.Push().Translate(end).Rotate(Quaternion.LookRotation(Dir(t.ang)));
                mb.Matte();
                mb.Box(new Vector3(0, 0.7f, 0), new Vector3(2.6f, 1.0f, 0.6f), Palette.WoodDark); // buffer stop
                mb.Box(new Vector3(0, 1.0f, -0.35f), new Vector3(2.8f, 0.35f, 0.25f), Palette.PaintRed);
                mb.Pop();
            }
            // Two roads into the machine shop.
            foreach (float off in new[] { -2.6f, 2.6f })
            {
                var side = Vector3.Cross(Vector3.up, Dir(ShopAng)) * off;
                float r0 = Mathf.Sqrt(PitR * PitR - off * off) + 0.3f;
                TrackBuilder.Build(mb, new TrackPath().Add(Pt(r0, ShopAng, rail) + side).Line(Pt(30, ShopAng, rail) + side).Build());
            }
            // Lead: leaves the turntable to the south-east and swings into a shallow turnout on the north side of the
            // main line in open ground, between the west tunnel and the station throat, with a switch stand at the points.
            float mz = WorldLayout.MainZ;
            Lead = LeadPath(out LeadStop);
            TrackBuilder.Build(mb, Lead, 0, LeadStop);
            // The loop link north to the scenic loop (work trains stand on it).
            TrackBuilder.Build(mb, DestinationRoutes.LoopLink, 0, DestinationRoutes.LinkJoin);
            SwitchStand(mb, new Vector3(LeadMergeX + 6, ValleyTerrain.Height(LeadMergeX + 6, mz + 3.4f), mz + 3.4f));
            Pit(mb);
            RoundhouseBuilding(mb);
            Props(mb);
            mb.ToObject("Roundhouse", root).isStatic = true;

            Bridge(root);
            MachineShop(root);
            ParkedEngines(root);
            if (ShunterId != null) new GameObject("Shunter", typeof(Shunter)).transform.SetParent(root, false);
        }

        static TrackPath LeadPath(out float stop)
        {
            float mz = WorldLayout.MainZ, my = WorldLayout.RailTop;
            var ls = Pt(PitR + 0.1f, LeadAng, WorldLayout.RailTop);
            var lead = new TrackPath().Add(ls).Bezier(ls + Dir(LeadAng) * 10, new Vector3(LeadMergeX - 12, my, mz), new Vector3(LeadMergeX, my, mz)).Build();
            stop = lead.Length;
            for (float sl = lead.Length; sl > 0; sl -= 0.25f) { if (Mathf.Abs(lead.PositionAt(sl).z - mz) > 0.12f) { stop = sl + 0.25f; break; } }
            return lead;
        }

        // ---------------------------------------------------------------- turntable

        static void Pit(MeshBuilder mb)
        {
            // A broad dressed-stone rim flush with the track bed, its inner wall dropping into a dark pit floor with
            // worn concentric rings, the circle rail the bridge wheels run on and the centre pivot.
            Color floor = Palette.Hex(0x2E2D2A), ring = Palette.Hex(0x3C3A36), rim = Palette.Hex(0x9C978D), wall = Palette.Hex(0x5E5A53);
            mb.Matte();
            mb.Surf = SurfaceKind.Gravel;
            mb.Cylinder(Pt(0, 0, -0.4f), Pt(0, 0, 0.06f), PitR, 56, floor);
            mb.Cylinder(Pt(0, 0, 0.06f), Pt(0, 0, 0.07f), PitR * 0.8f, 48, ring, true);
            mb.Cylinder(Pt(0, 0, 0.07f), Pt(0, 0, 0.08f), PitR * 0.74f, 48, floor, true);
            mb.Cylinder(Pt(0, 0, 0.08f), Pt(0, 0, 0.09f), PitR * 0.42f, 40, ring, true);
            mb.Cylinder(Pt(0, 0, 0.09f), Pt(0, 0, 0.1f), PitR * 0.36f, 40, floor, true);
            mb.Surf = SurfaceKind.Auto;
            mb.Mat(0.55f, 0.7f);
            mb.Cylinder(Pt(0, 0, 0.06f), Pt(0, 0, 0.16f), PitR - 0.7f, 56, Palette.Steel, false);
            mb.Cylinder(Pt(0, 0, 0.06f), Pt(0, 0, 0.165f), PitR - 0.85f, 56, Palette.Hex(0x2A2A2A), false);
            mb.Matte();
            mb.Cylinder(Pt(0, 0, 0), Pt(0, 0, 0.35f), 1.1f, 14, Palette.Hex(0x6A665E));
            mb.Cylinder(Pt(0, 0, 0.35f), Pt(0, 0, 0.5f), 0.6f, 12, Palette.Iron);
            const int segs = 64;
            float w = RimR - PitR, mid = (RimR + PitR) / 2, top = WorldLayout.RailTop - 0.17f;
            for (int k = 0; k < segs; k++)
            {
                float a = (k + 0.5f) * 360f / segs;
                mb.Push().Translate(Pt(mid, a, (top - 0.5f) / 2)).Rotate(Quaternion.LookRotation(Dir(a)));
                float arc = 2 * Mathf.PI * RimR / segs + 0.06f;
                mb.Surf = SurfaceKind.Concrete;
                mb.Box(Vector3.zero, new Vector3(arc, top + 0.5f, w), wall, rim);
                mb.Surf = SurfaceKind.Auto;
                // a dark joint between the coping stones
                mb.Box(new Vector3(arc / 2 - 0.03f, (top + 0.5f) / 2 + 0.005f, 0), new Vector3(0.05f, 0.01f, w), Palette.Hex(0x6E6A62));
                mb.Pop();
            }
        }

        static void SwitchStand(MeshBuilder mb, Vector3 p)
        {
            mb.Matte();
            mb.Box(p + new Vector3(0, 0.15f, 0), new Vector3(1.2f, 0.3f, 0.6f), Palette.WoodDark);
            mb.Cylinder(p + Vector3.up * 0.3f, p + Vector3.up * 2.0f, 0.06f, 6, Palette.Iron);
            mb.Cylinder(p + new Vector3(-0.28f, 1.85f, 0), p + new Vector3(0.28f, 1.85f, 0), 0.32f, 12, Palette.PaintRed);
            mb.Cylinder(p + new Vector3(-0.3f, 1.85f, 0), p + new Vector3(0.3f, 1.85f, 0), 0.12f, 8, Palette.White);
            mb.Box(p + new Vector3(0.5f, 0.55f, 0), new Vector3(0.08f, 0.6f, 0.08f), Palette.Iron); // throw lever
            // Points rods across to the main line.
            mb.Box(p + new Vector3(0, 0.2f, 2.0f), new Vector3(0.08f, 0.06f, 3.2f), Palette.Iron);
        }

        static void Bridge(Transform root)
        {
            // A broad deck girder bridge spanning the pit: deep dark side girders, a plated deck level with the rim,
            // ties and rails lined up with the radiating tracks, handrails along both edges.
            var b = new MeshBuilder();
            float len = PitR * 2 - 0.3f, rt = WorldLayout.RailTop;
            Color girder = Palette.Hex(0x2E3236), deck = Palette.Hex(0x6A6C6C);
            b.Mat(0.35f, 0.4f);
            b.Box(new Vector3(0, rt - 0.3f, 0), new Vector3(3.4f, 0.1f, len), deck);
            foreach (float x in new[] { -1.55f, 1.55f })
            {
                b.Box(new Vector3(x, (rt - 0.35f - 0.4f) / 2, 0), new Vector3(0.35f, rt - 0.35f + 0.4f, len - 0.4f), girder);
                for (float z = -len / 2 + 1f; z < len / 2 - 0.5f; z += 1.8f)
                    b.Box(new Vector3(x * 1.07f, (rt - 0.75f) / 2, z), new Vector3(0.08f, rt - 0.35f + 0.4f, 0.18f), Palette.Hex(0x3A3F44)); // stiffeners
                // handrail
                for (float z = -len / 2 + 0.4f; z <= len / 2 - 0.3f; z += 2.4f)
                    b.Box(new Vector3(x * 1.08f, rt + 0.2f, z), new Vector3(0.06f, 1.0f, 0.06f), Palette.Hex(0xC9C2B0));
                b.Box(new Vector3(x * 1.08f, rt + 0.68f, 0), new Vector3(0.06f, 0.06f, len - 0.6f), Palette.Hex(0xC9C2B0));
            }
            // End carriages with wheels on the circle rail.
            foreach (float z in new[] { -len / 2 + 0.5f, len / 2 - 0.5f })
                b.Box(new Vector3(0, rt - 0.45f, z), new Vector3(3.6f, 0.5f, 0.8f), girder);
            b.Matte();
            for (float z = -len / 2 + 0.3f; z < len / 2; z += 0.65f)
                b.Box(new Vector3(0, rt - 0.2f, z), new Vector3(2.3f, 0.1f, 0.24f), Palette.Hex(0x4A3A2C));
            b.Mat(0.6f, 0.8f);
            foreach (float x in new[] { -0.72f, 0.72f })
                b.Box(new Vector3(x, rt - 0.08f, 0), new Vector3(0.09f, 0.14f, len), Palette.Steel);
            // Operator's hut at one end.
            b.Matte();
            b.Box(new Vector3(2.6f, rt + 0.8f, len / 2 - 1.4f), new Vector3(1.5f, 2.2f, 1.6f), Palette.PaintGreen);
            b.Lit();
            b.Box(new Vector3(2.6f, rt + 1.2f, len / 2 - 0.59f), new Vector3(1.0f, 0.6f, 0.04f), Palette.Window);
            b.Matte();
            b.GableRoof(new Vector3(2.6f, rt + 1.9f, len / 2 - 1.4f), new Vector3(1.7f, 0.7f, 1.8f), Palette.RoofRed, Palette.PaintGreen, 0.15f);
            var go = b.ToObject("Turntable", root);
            go.transform.position = C + Vector3.up * _gy;
            go.AddComponent<Turntable>();
            var snd = MapSound.Attach(go, "loop_turntable", 60, 0.45f);
            if (snd != null) go.GetComponent<Turntable>().Sound = snd;
        }

        // ---------------------------------------------------------------- roundhouse

        static void RoundhouseBuilding(MeshBuilder mb)
        {
            Color brick = Palette.Brick, inside = Palette.Hex(0x2A221E), slate = Palette.Hex(0x3F454C), cream = Palette.Cream;
            float depth = Outer - Inner, pitch = Mathf.Atan2(HIn - HOut, depth) * Mathf.Rad2Deg;
            float end = StallStart + Stalls * StallStep;
            for (int i = 0; i < Stalls; i++)
            {
                float a0 = StallStart + i * StallStep, a1 = a0 + StallStep, am = a0 + StallStep / 2;
                Vector3 i0 = Pt(Inner, a0), i1 = Pt(Inner, a1), o0 = Pt(Outer, a0), o1 = Pt(Outer, a1);
                var up = Vector3.up;

                // Dark interior: floor, back and ceiling so engines inside sit in shadow.
                mb.Matte();
                Q2(mb, i0 + up * 0.03f, o0 + up * 0.03f, o1 + up * 0.03f, i1 + up * 0.03f, Palette.Hex(0x3A3430));
                Q2(mb, o0 - Dir(a0) * 0.3f, o0 - Dir(a0) * 0.3f + up * HOut, o1 - Dir(a1) * 0.3f + up * HOut, o1 - Dir(a1) * 0.3f, inside);
                Q2(mb, i0 + up * (HIn - 0.3f), o0 + up * (HOut - 0.3f), o1 + up * (HOut - 0.3f), i1 + up * (HIn - 0.3f), inside);

                // Back wall with two tall windows.
                Q2(mb, o0, o0 + up * HOut, o1 + up * HOut, o1, MeshBuilder.Darken(brick, 0.92f));
                var bm = (o0 + o1) / 2;
                var outward = Dir(am);
                float bw = (o1 - o0).magnitude;
                mb.Push().Translate(bm).Rotate(Quaternion.LookRotation(outward));
                for (int k = -1; k <= 1; k += 2)
                {
                    mb.Box(new Vector3(k * bw * 0.24f, 3.6f, 0.06f), new Vector3(1.9f, 3.4f, 0.1f), cream);
                    mb.Lit();
                    mb.Box(new Vector3(k * bw * 0.24f, 3.7f, 0.12f), new Vector3(1.5f, 3.0f, 0.06f), Palette.Window);
                    mb.Matte();
                }
                mb.Pop();

                // Facade: brick jambs and spandrels around a tall arched door, cream arch trim, a glazed fanlight.
                var mid = (i0 + i1) / 2;
                float w = (i1 - i0).magnitude, dw = w - 1.3f, spring = 5.0f, apex = spring + dw / 2;
                mb.Push().Translate(mid).Rotate(Quaternion.LookRotation(-Dir(am)));
                mb.Surf = SurfaceKind.Brick;
                Q2(mb, new Vector3(-w / 2, 0, 0), new Vector3(-w / 2, HIn, 0), new Vector3(-dw / 2, HIn, 0), new Vector3(-dw / 2, 0, 0), brick);
                Q2(mb, new Vector3(dw / 2, 0, 0), new Vector3(dw / 2, HIn, 0), new Vector3(w / 2, HIn, 0), new Vector3(w / 2, 0, 0), brick);
                Q2(mb, new Vector3(-dw / 2, apex, 0), new Vector3(-dw / 2, HIn, 0), new Vector3(dw / 2, HIn, 0), new Vector3(dw / 2, apex, 0), brick);
                const int arcs = 10;
                for (int k = 0; k < arcs; k++)
                {
                    float t0 = Mathf.PI * (1 - k / (float)arcs), t1 = Mathf.PI * (1 - (k + 1) / (float)arcs);
                    var p0 = new Vector3(Mathf.Cos(t0) * dw / 2, spring + Mathf.Sin(t0) * dw / 2, 0);
                    var p1 = new Vector3(Mathf.Cos(t1) * dw / 2, spring + Mathf.Sin(t1) * dw / 2, 0);
                    Q2(mb, p0, new Vector3(p0.x, apex, 0), new Vector3(p1.x, apex, 0), p1, brick);
                    mb.Surf = SurfaceKind.Auto;
                    // fanlight glazing behind the arch
                    mb.Lit();
                    var q0 = new Vector3(p0.x, p0.y, -0.25f); var q1 = new Vector3(p1.x, p1.y, -0.25f); var qc = new Vector3(0, spring, -0.25f);
                    mb.Tri(qc, q0, q1, Palette.Window); mb.Tri(qc, q1, q0, Palette.Window);
                    mb.Matte();
                    // cream voussoirs
                    var pm = (p0 + p1) / 2;
                    mb.Push().Translate(pm + new Vector3(0, 0, 0.08f)).Rotate(Quaternion.Euler(0, 0, Mathf.Atan2(p1.y - p0.y, p1.x - p0.x) * Mathf.Rad2Deg));
                    mb.Box(new Vector3(0, 0.18f, 0), new Vector3((p1 - p0).magnitude + 0.06f, 0.4f, 0.22f), cream);
                    mb.Pop();
                    mb.Surf = SurfaceKind.Brick;
                }
                mb.Surf = SurfaceKind.Auto;
                mb.Box(new Vector3(0, spring, -0.2f), new Vector3(dw, 0.14f, 0.12f), Palette.WoodDark); // transom bar
                mb.Box(new Vector3(0, apex + 0.25f, 0.12f), new Vector3(0.5f, 0.6f, 0.26f), cream);     // keystone
                mb.Box(new Vector3(0, HIn - 0.3f, 0.2f), new Vector3(w + 0.1f, 0.6f, 0.5f), cream);     // cornice
                // Doors folded back against the jambs.
                foreach (int k in new[] { -1, 1 })
                {
                    mb.Push().Translate(k * dw / 2, 0, -0.1f).RotateY(k * -80);
                    mb.Box(new Vector3(k * 0.8f, 2.5f, 0), new Vector3(1.6f, 5.0f, 0.12f), Palette.Hex(0x5B4A3A));
                    mb.Pop();
                }
                mb.Pop();

                // Roof: slate with a long skylight and a smoke jack over each stall.
                mb.Surf = SurfaceKind.Shingles;
                Vector3 r0 = Pt(Inner - 0.7f, a0, HIn + 0.05f), r1 = Pt(Inner - 0.7f, a1, HIn + 0.05f);
                Q2(mb, r0, o0 + up * HOut, o1 + up * HOut, r1, slate);
                mb.Surf = SurfaceKind.Auto;
                System.Func<float, float, Vector3> R = (r, ang) => Pt(r, ang, Mathf.Lerp(HIn, HOut, (r - Inner) / depth) + 0.09f);
                float sw = StallStep * 0.2f;
                mb.Mat(0.8f, 0.3f);
                mb.Quad(R(Inner + 3, am - sw), R(Outer - 3, am - sw), R(Outer - 3, am + sw), R(Inner + 3, am + sw), Palette.Hex(0x5F7480));
                mb.Quad(R(Inner + 3, am + sw), R(Outer - 3, am + sw), R(Outer - 3, am - sw), R(Inner + 3, am - sw), Palette.Hex(0x5F7480));
                mb.Matte();
                var jack = R(Inner + 4.5f, am + sw * 2.2f);
                mb.Cylinder(jack, jack + Vector3.up * 1.8f, 0.4f, 0.55f, 8, Palette.Iron);
                mb.Cylinder(jack + Vector3.up * 1.8f, jack + Vector3.up * 2.1f, 0.75f, 0.2f, 8, Palette.Iron);
            }

            // Pilasters on the facade and firewalls over the roof at every stall edge; brick end walls.
            for (int i = 0; i <= Stalls; i++)
            {
                float e = StallStart + i * StallStep;
                var pi = Pt(Inner, e);
                mb.Push().Translate(pi).Rotate(Quaternion.LookRotation(-Dir(e)));
                mb.Box(new Vector3(0, (HIn + 0.6f) / 2, 0.3f), new Vector3(0.9f, HIn + 0.6f, 0.7f), cream);
                mb.Box(new Vector3(0, HIn + 0.7f, 0.3f), new Vector3(1.1f, 0.25f, 0.9f), MeshBuilder.Darken(cream, 0.9f));
                mb.Pop();
                var fm = (Pt(Inner, e) + Pt(Outer, e)) / 2 + Vector3.up * ((HIn + HOut) / 2 + 0.25f);
                mb.Push().Translate(fm).Rotate(Quaternion.LookRotation(Dir(e)) * Quaternion.Euler(pitch, 0, 0));
                mb.Surf = SurfaceKind.Brick;
                mb.Box(Vector3.zero, new Vector3(0.5f, 0.9f, depth + 0.4f), MeshBuilder.Darken(brick, 0.95f));
                mb.Surf = SurfaceKind.Auto;
                mb.Box(new Vector3(0, 0.48f, 0), new Vector3(0.6f, 0.1f, depth + 0.5f), Palette.Hex(0xA8A296));
                mb.Pop();
                bool endWall = i == 0 || i == Stalls;
                Color wall = endWall ? brick : inside;
                if (endWall) mb.Surf = SurfaceKind.Brick;
                Q2(mb, Pt(Inner, e), Pt(Inner, e, HIn), Pt(Outer, e, HOut), Pt(Outer, e), wall);
                mb.Surf = SurfaceKind.Auto;
                if (endWall)
                {
                    var em = (Pt(Inner, e) + Pt(Outer, e)) / 2;
                    var side = Vector3.Cross(Vector3.up, Dir(e)) * (i == 0 ? -1 : 1);
                    mb.Push().Translate(em).Rotate(Quaternion.LookRotation(side));
                    for (int k = -2; k <= 2; k++)
                    {
                        mb.Box(new Vector3(k * 3.6f, 3.4f, 0.06f), new Vector3(1.6f, 3.0f, 0.1f), cream);
                        mb.Lit();
                        mb.Box(new Vector3(k * 3.6f, 3.5f, 0.12f), new Vector3(1.2f, 2.6f, 0.06f), Palette.Window);
                        mb.Matte();
                    }
                    mb.Pop();
                }
            }
            NightLights.AddPoint(Pt(Inner - 3, StallStart + Stalls * StallStep / 2, 7), 36f, 18f, new Color(1f, 0.85f, 0.6f));
        }

        // ---------------------------------------------------------------- the works and its clutter

        static void MachineShop(Transform root)
        {
            var art = ModelRegistry.Spawn(ModelRegistry.Buildings, "machineshop", root);
            if (art == null) return;
            // The doors (the model's front, +Z) face the turntable; set back so the two shop roads run in.
            var b = ModelRegistry.RendererBounds(art);
            float half = b.extents.z;
            art.transform.rotation = Quaternion.LookRotation(-Dir(ShopAng)) * art.transform.rotation;
            art.transform.position = Pt(23 + half, ShopAng);
            Station.Walkable(art);
        }

        static void Props(MeshBuilder mb)
        {
            var rng = new System.Random(77);
            System.Func<float> R = () => (float)rng.NextDouble();
            // Cable drums, crates and barrels by the stall doors and the shop.
            foreach (var (r, a) in new[] { (Inner - 2.5f, 106f), (Inner - 2.2f, 152f), (Inner - 2.6f, 204f), (21f, 52f), (22f, 84f), (16f, -120f) })
            {
                var c = Pt(r, a);
                for (int k = 0; k < 4; k++)
                {
                    var p = c + new Vector3(R() * 3 - 1.5f, 0, R() * 3 - 1.5f);
                    int kind = rng.Next(3);
                    if (kind == 0) Station.Crate(mb, p, R() * 90);
                    else if (kind == 1) Station.Barrel(mb, p);
                    else Spool(mb, p, R() * 180);
                }
            }
            // Water tank and coaling stage by the west tracks.
            Vegetation.AddModel(ModelRegistry.Buildings, "watertower", Pt(31, 222), Quaternion.Euler(0, 40, 0));
            var coal = Pt(17, -87); // straddling the coaling track
            if (Vegetation.HasArt(ModelRegistry.Buildings, "coalstage"))
            {
                // The coaling stage beside the coaling track, chutes over it.
                var side = Vector3.Cross(Vector3.up, Dir(-87));
                Vegetation.AddModel(ModelRegistry.Buildings, "coalstage", coal + side * 5.5f, Quaternion.LookRotation(Dir(-87)) * Quaternion.Euler(0, CoalStageYaw, 0));
            }
            else
            {
            mb.Matte();
            mb.Push().Translate(coal).Rotate(Quaternion.LookRotation(Dir(-87)));
            for (int k = -1; k <= 1; k += 2)
                for (int j = -1; j <= 1; j++)
                    mb.Box(new Vector3(k * 2.2f, 2.0f, j * 2.4f), new Vector3(0.4f, 4f, 0.4f), Palette.WoodDark);
            mb.Box(new Vector3(0, 4.3f, 0), new Vector3(5.2f, 0.6f, 6f), Palette.Wood);
            mb.Box(new Vector3(0, 5.3f, 0), new Vector3(4.6f, 1.4f, 5.4f), Palette.WoodDark);
            mb.Sphere(new Vector3(0, 6.0f, 0), new Vector3(2.0f, 0.8f, 2.4f), 8, Palette.Hex(0x232326), false);
            mb.Pop();
            }
            Station.Lamp(mb, Pt(RimR + 1.5f, 18));
            Station.Lamp(mb, Pt(RimR + 1.5f, 218));
            Station.Lamp(mb, Pt(RimR + 1.5f, -100));
        }

        static void Spool(MeshBuilder mb, Vector3 p, float yaw)
        {
            var d = Quaternion.Euler(0, yaw, 0) * Vector3.right;
            mb.Matte();
            mb.Cylinder(p + Vector3.up * 0.75f - d * 0.5f, p + Vector3.up * 0.75f - d * 0.42f, 0.75f, 12, Palette.Wood);
            mb.Cylinder(p + Vector3.up * 0.75f + d * 0.42f, p + Vector3.up * 0.75f + d * 0.5f, 0.75f, 12, Palette.Wood);
            mb.Cylinder(p + Vector3.up * 0.75f - d * 0.42f, p + Vector3.up * 0.75f + d * 0.42f, 0.55f, 12, Palette.Hex(0x2E2A26));
        }

        /// <summary>Engines resting in the stalls and on the open tracks: ones the player doesn't own yet first, so
        /// the terminal doubles as a showroom and never duplicates a train on the platforms.</summary>
        /// <summary>Tracks with an engine parked on them (the shunter keeps off these).</summary>
        static string ShunterId;

        /// <summary>Free tracks the shunter can park on: (angle, true for a roundhouse stall).</summary>
        public static List<(float ang, bool stall)> FreeTracks()
        {
            var l = new List<(float, bool)>();
            for (int i = 0; i < Stalls; i++) { float a = StallStart + (i + 0.5f) * StallStep; if (!ParkedAt.Exists(p => Mathf.Abs(Mathf.DeltaAngle(p, a)) < 1)) l.Add((a, true)); }
            foreach (var t in OpenTracks) if (t.len >= 26 && !ParkedAt.Exists(p => Mathf.Abs(Mathf.DeltaAngle(p, t.ang)) < 1)) l.Add((t.ang, false));
            return l;
        }

        public static string Shunter_Id { get { return ShunterId; } }
        public static Vector3 Centre { get { return Pt(0, 0, WorldLayout.RailTop); } }
        public static Vector3 TrackPoint(float r, float ang) { return Pt(r, ang, WorldLayout.RailTop); }
        public static Vector3 Direction(float ang) { return Dir(ang); }
        public static float ParkRadius(bool stall, float len) { return stall ? Inner + len / 2 - 3.5f : PitR + 3 + len / 2; }
        public const float LeadAngle = LeadAng;

        static readonly List<float> ParkedAt = new List<float>();

        static void ParkedEngines(Transform root)
        {
            var owned = new HashSet<string>(G.Session != null ? G.Session.State.trains.Select(t => t.defId) : Enumerable.Empty<string>());
            var ids = GameDatabase.Locomotives.Select(l => l.Id).Where(id => ModelRegistry.Has(ModelRegistry.Locomotives, id))
                .OrderBy(id => owned.Contains(id) ? 1 : 0).ToList();
            if (ids.Count == 0) return;
            ParkedAt.Clear();
            ShunterId = ids[5 % ids.Count];
            var spots = new List<(float ang, bool stall)>
            {
                (StallStart + 1.5f * StallStep, true), (StallStart + 4.5f * StallStep, true), (StallStart + 6.5f * StallStep, true),
                (10, false), (-20, false),
            };
            for (int k = 0; k < spots.Count; k++)
            {
                var (ang, stall) = spots[k];
                ParkedAt.Add(ang);
                var go = ModelRegistry.Spawn(ModelRegistry.Locomotives, ids[k % ids.Count], root);
                if (go == null) continue;
                var b = ModelRegistry.RendererBounds(go);
                float len = b.size.z;
                var dir = Dir(ang);
                // Nose to the turntable: in a stall the front pokes out of the door, outside it waits by the pit.
                float r = stall ? Inner + len / 2 - 3.5f : PitR + 3 + len / 2;
                var rot = Quaternion.LookRotation(-dir);
                var local = new Vector3(b.center.x, 0, b.center.z);
                go.transform.rotation = rot * go.transform.rotation;
                go.transform.position = Pt(r, ang, WorldLayout.RailTop) - rot * local;
                foreach (var t in go.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
            }
        }
    }

    /// <summary>Swings the turntable bridge to a new track now and then, or where the shunter asks.</summary>
    public sealed class Turntable : MonoBehaviour
    {
        public static Turntable Instance { get; private set; }
        /// <summary>True while the shunter drives the bridge (no idle swinging).</summary>
        public bool Manual;
        public float Target;
        public MapSound Sound;
        float _wait = 4;
        float[] _angles;
        void Awake() { Instance = this; Target = transform.eulerAngles.y; }
        public bool Aligned { get { return Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, Target)) < 0.05f; } }
        void Update()
        {
            if (!Manual)
            {
                if (_angles == null) _angles = Yard.TrackAngles().ToArray();
                _wait -= Time.deltaTime;
                if (_wait <= 0) { _wait = Random.Range(10f, 22f); Target = 90f - _angles[Random.Range(0, _angles.Length)]; }
            }
            var e = transform.eulerAngles;
            e.y = Mathf.MoveTowardsAngle(e.y, Target, (Manual ? 11f : 7f) * Time.deltaTime);
            transform.eulerAngles = e;
            if (Sound != null) Sound.Active = !Aligned;
        }
    }
}
