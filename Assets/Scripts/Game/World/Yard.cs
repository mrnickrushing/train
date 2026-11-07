using System.Collections.Generic;
using Ironhorse.Core;
using UnityEngine;

namespace Ironhorse.Game
{
    /// <summary>
    /// Freight yard (sidings full of parked wagons, gantry cranes, goods shed, stacked cargo) and a
    /// roundhouse with a turntable: the dense, busy rail scenery that sells the railroad fantasy.
    /// </summary>
    public static partial class Yard
    {
        static System.Random _rng;
        static float R() { return (float)_rng.NextDouble(); }
        static float R(float a, float b) { return a + (b - a) * R(); }

        public static void Build(Transform root)
        {
            _rng = new System.Random(4242);
            BuildFreightYard(root);
            BuildRoundhouse(root);
        }

        // ================================================================ Freight yard

        static void BuildFreightYard(Transform root)
        {
            var yr = WorldLayout.YardRect;
            for (int i = 0; i < 3; i++)
                NightLights.AddPoint(new Vector3(yr.xMin + yr.width * (0.2f + 0.3f * i), 11f, yr.center.y), 40f, 22f, new Color(1f, 0.85f, 0.6f));
            var r = WorldLayout.YardRect;
            var mb = new MeshBuilder();
            mb.Matte();
            // Cinder ground pad
            mb.Surf = SurfaceKind.Gravel;
            mb.Box(new Vector3(r.center.x, 0.02f, r.center.y), new Vector3(r.width, 0.04f, r.height), Palette.Hex(0x4C4640));
            mb.Surf = SurfaceKind.Auto;

            float x0 = r.xMin + 4, x1 = r.xMax - 6;
            for (int i = 0; i < WorldLayout.Sidings; i++)
            {
                float z = WorldLayout.SidingZ(i);
                var path = new TrackPath().Add(new Vector3(x0, WorldLayout.RailTop, z)).Line(new Vector3(x1, WorldLayout.RailTop, z)).Build();
                TrackBuilder.Build(mb, path);
                // Buffer stop at the far end
                mb.Box(new Vector3(x1 + 0.6f, 0.7f, z), new Vector3(0.6f, 1.2f, 2.6f), Palette.WoodDark);
                mb.Box(new Vector3(x1 + 0.2f, 1.0f, z), new Vector3(0.3f, 0.4f, 2.8f), Palette.PaintRed);
            }
            var tracks = mb.ToObject("Freight Yard", root);
            tracks.isStatic = true;

            // Parked wagon strings: one combined mesh per siding.
            var wagonPool = new[] { "flatcar", "hopper", "boxcar", "stockcar", "longflat", "steelhopper", "reefer", "tankcar" };
            var resPool = new[] { "timber", "coal", "iron_ore", "steel", "cattle", "copper_ore" };
            for (int i = 0; i < WorldLayout.Sidings; i++)
            {
                if (i == 2) continue; // keep one road free under the cranes
                var cars = new MeshBuilder();
                float z = WorldLayout.SidingZ(i);
                float x = x1 - 2 - R(0, 12);
                float stop = x0 + R(10, 45);
                while (x > stop)
                {
                    var def = GameDatabase.Wagon(wagonPool[_rng.Next(wagonPool.Length)]);
                    float artLen = Vegetation.ModelLength(ModelRegistry.Wagons, def.Id);
                    if (artLen > 0)
                    {
                        // Art wagons are drawn instanced, with a light far version (Wagons/<id>_far).
                        Vegetation.AddModel(ModelRegistry.Wagons, def.Id, new Vector3(x - artLen / 2, WorldLayout.RailTop, z), Quaternion.Euler(0, R() < 0.5f ? 90 : -90, 0));
                        x -= artLen + TrainFactory.Gap;
                        if (R() < 0.12f) x -= R(6, 14);
                        continue;
                    }
                    var model = TrainFactory.Wagon(def);
                    float cx = x - model.Length / 2;
                    cars.Push().Translate(cx, WorldLayout.RailTop, z).RotateY(90);
                    cars.Append(model.Body);
                    foreach (var w in model.Wheels) { cars.Push().Translate(w.Position); cars.Append(w.Mesh); cars.Pop(); }
                    var res = GameDatabase.Resource(resPool[_rng.Next(resPool.Length)]);
                    if (res.Cargo == def.Cargo && R() < 0.7f)
                    {
                        var load = TrainFactory.Cargo(model, def, res);
                        if (load != null) cars.Append(load);
                    }
                    cars.Pop();
                    x -= model.Length + TrainFactory.Gap;
                    if (R() < 0.12f) x -= R(6, 14); // gaps between cuts of cars
                }
                cars.ToObject("Parked Wagons " + i, root).isStatic = true;
            }

            var props = new MeshBuilder();
            foreach (float gx in new[] { 108f, 146f })
            {
                var gc = new Vector3(gx, 0, WorldLayout.SidingZ(2));
                if (Vegetation.HasArt(ModelRegistry.Buildings, "gantrycrane")) Vegetation.AddModel(ModelRegistry.Buildings, "gantrycrane", gc, Quaternion.Euler(0, GantryYaw, 0));
                else GantryCrane(props, gc);
            }
            if (Vegetation.HasArt(ModelRegistry.Buildings, "goodsshed")) Vegetation.AddModel(ModelRegistry.Buildings, "goodsshed", new Vector3(96, 0, r.yMax - 1), Quaternion.Euler(0, GoodsShedYaw, 0), 1.25f);
            else GoodsShed(props, new Vector3(96, 0, r.yMax - 4));
            if (Vegetation.HasArt(ModelRegistry.Buildings, "coalheap")) Vegetation.AddModel(ModelRegistry.Buildings, "coalheap", new Vector3(160, 0, r.yMax - 6), Quaternion.Euler(0, 30, 0));
            else CoalHeap(props, new Vector3(160, 0, r.yMax - 6));
            for (int i = 0; i < 6; i++)
            {
                var lp = new Vector3(120 + i * 5.5f, 0, r.yMax - 5);
                if (Vegetation.HasArt(ModelRegistry.Buildings, "logstack")) { if (i % 2 == 0) Vegetation.AddModel(ModelRegistry.Buildings, "logstack", lp + Vector3.right * 2.75f, Quaternion.Euler(0, 90 + i * 7, 0), 1.1f); }
                else LogStack(props, lp);
            }
            for (int i = 0; i < 40; i++)
            {
                var p = new Vector3(R(x0 + 4, x1 - 8), 0, WorldLayout.SidingZ(2) + R(-1.2f, 1.2f) * 0 + (R() < 0.5f ? 2.7f : -2.7f));
                if (R() < 0.5f) Station.Crate(props, p, R(0, 40)); else Station.Barrel(props, p);
            }
            // Yard lamps
            for (float lx = x0 + 6; lx < x1; lx += 22) Station.Lamp(props, new Vector3(lx, 0, WorldLayout.SidingZ(2) + 2.75f));
            props.ToObject("Yard Props", root).isStatic = true;
        }

        /// <summary>Turn of the crane art so its girder spans the sidings.</summary>
        const float GantryYaw = 90f;
        /// <summary>Turn of the goods shed art so its long side and loading dock face the sidings.</summary>
        const float GoodsShedYaw = 90f;

        static void GantryCrane(MeshBuilder mb, Vector3 c)
        {
            Color steel = Palette.Hex(0x3E4A55), yellow = Palette.Hex(0x6E3B2E); // weathered oxide-red paint
            float span = 26f, h = 11f;
            mb.Mat(0.4f, 0.5f);
            for (int s = -1; s <= 1; s += 2)
            {
                // A-frame legs
                var foot = c + new Vector3(0, 0, s * span / 2);
                mb.Cylinder(foot + new Vector3(-3, 0, 0), foot + new Vector3(0, h, 0), 0.35f, 6, steel);
                mb.Cylinder(foot + new Vector3(3, 0, 0), foot + new Vector3(0, h, 0), 0.35f, 6, steel);
                mb.Box(foot + new Vector3(0, 0.3f, 0), new Vector3(7, 0.6f, 1.2f), steel);
                mb.Box(foot + new Vector3(0, h * 0.45f, 0), new Vector3(3.4f, 0.3f, 0.3f), steel);
            }
            // Girder with lattice
            mb.Box(c + new Vector3(0, h + 0.5f, 0), new Vector3(1.6f, 1.2f, span + 6), yellow);
            for (float z = -span / 2; z < span / 2; z += 2.2f)
            {
                mb.Push().Translate(c + new Vector3(0, h + 0.5f, z)).Rotate(Quaternion.Euler(45, 0, 0));
                mb.Box(Vector3.zero, new Vector3(1.7f, 0.12f, 1.5f), Palette.Hex(0x5A3226));
                mb.Pop();
            }
            // Trolley, operator cab, cable and hook
            float tz = R(-6, 6);
            mb.Box(c + new Vector3(0, h + 1.5f, tz), new Vector3(2.4f, 1.0f, 2.6f), steel);
            mb.Box(c + new Vector3(1.6f, h - 0.6f, tz + 1.5f), new Vector3(1.6f, 1.8f, 1.6f), yellow);
            mb.Lit();
            mb.Box(c + new Vector3(2.42f, h - 0.4f, tz + 1.5f), new Vector3(0.04f, 0.7f, 1.0f), Palette.Window);
            mb.Mat(0.4f, 0.6f);
            mb.Cylinder(c + new Vector3(0, h + 1f, tz), c + new Vector3(0, 4.5f, tz), 0.05f, 3, Palette.Iron, false);
            mb.Box(c + new Vector3(0, 4.2f, tz), new Vector3(1.2f, 0.5f, 0.4f), Palette.Iron);
            // A crate being lifted
            Station.Crate(mb, c + new Vector3(0, 2.6f, tz), 15);
            mb.Matte();
        }

        static void GoodsShed(MeshBuilder mb, Vector3 c)
        {
            mb.Push().Translate(c);
            mb.Matte();
            mb.Box(new Vector3(0, 0.7f, 0), new Vector3(44, 1.4f, 9), Palette.StoneDark, Palette.Stone);
            mb.Box(new Vector3(0, 4.2f, 0.8f), new Vector3(42, 5.6f, 7), Palette.Brick);
            mb.GableRoof(new Vector3(0, 7.0f, 0.8f), new Vector3(42, 2.6f, 7), Palette.RoofGreen, Palette.Brick, 0.6f);
            for (float x = -18; x <= 18; x += 6)
            {
                mb.Box(new Vector3(x, 3.6f, -2.72f), new Vector3(3.4f, 4f, 0.12f), Palette.Hex(0x5A3A24));
                mb.Lit();
                mb.Box(new Vector3(x, 6.2f, -2.75f), new Vector3(2.6f, 0.8f, 0.05f), Palette.Window);
                mb.Matte();
            }
            // Loading awning over the dock
            mb.Push().Translate(0, 5.9f, -4.2f).Rotate(Quaternion.Euler(12, 0, 0));
            mb.Box(Vector3.zero, new Vector3(42, 0.15f, 3.4f), Palette.RoofTin);
            mb.Pop();
            mb.Pop();
        }

        static void CoalHeap(MeshBuilder mb, Vector3 c)
        {
            mb.Matte();
            mb.Sphere(c, new Vector3(7, 3.2f, 5), 9, Palette.Hex(0x232326), false);
            mb.Sphere(c + new Vector3(5, 0, 2), new Vector3(4, 2.2f, 3.5f), 8, Palette.Hex(0x2C2C30), false);
        }

        static void LogStack(MeshBuilder mb, Vector3 c)
        {
            mb.Matte();
            for (int row = 0; row < 3; row++)
                for (int i = 0; i < 4 - row; i++)
                {
                    float z = c.z + (i - (3 - row) / 2f) * 0.75f;
                    float y = 0.38f + row * 0.65f;
                    float j = Mathf.Repeat(z * 13.7f + row * 3.3f, 1f) * 0.3f - 0.15f; // logs never line up exactly
                    mb.Surf = SurfaceKind.Bark;
                    mb.Cylinder(new Vector3(c.x - 2.4f + j, y, z), new Vector3(c.x + 2.4f + j, y, z), 0.36f, 9, Palette.Bark, false);
                    mb.Surf = SurfaceKind.None; // pale sawn ends
                    mb.Cylinder(new Vector3(c.x - 2.42f + j, y, z), new Vector3(c.x - 2.4f + j, y, z), 0.34f, 9, Palette.Hex(0xC9A777));
                    mb.Cylinder(new Vector3(c.x + 2.4f + j, y, z), new Vector3(c.x + 2.42f + j, y, z), 0.34f, 9, Palette.Hex(0xC9A777));
                    mb.Surf = SurfaceKind.Auto;
                }
        }

        /// <summary>Double-sided quad: visible from both sides regardless of winding.</summary>
        static void Q2(MeshBuilder mb, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
        {
            mb.Quad(a, b, c, d, col);
            mb.Quad(d, c, b, a, col);
        }

        static Vector3 WithY(this Vector3 v, float y) { return new Vector3(v.x, y, v.z); }
    }
}
