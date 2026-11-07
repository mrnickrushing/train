using Ironhorse.Core;
using UnityEngine;

namespace Ironhorse.Game
{
    /// <summary>
    /// Procedural 1910s–20s motor trucks (until AI models drop in under Models/Trucks). They face +Z with the
    /// origin at the centre and y = 0 on the road, like the trains.
    /// </summary>
    public static class TruckFactory
    {
        static readonly Color Rubber = Palette.Hex(0x1E1E1E), Hub = Palette.Hex(0x6B6560), Glass = Palette.Hex(0x22303A);

        public static CarModel Truck(TruckDef d)
        {
            Color body = Palette.Hex(d.PrimaryRgb), trim = Palette.Hex(d.TrimRgb);
            switch (d.Id)
            {
                case "runabout": return Build(body, trim, scale: 0.85f, bedLen: 2.2f, stakes: false, van: false, twinRear: false);
                case "bulldog": return Build(body, trim, 1.1f, 3.0f, stakes: true, van: false, twinRear: false, high: true);
                case "cometvan": return Build(body, trim, 1f, 3.0f, stakes: false, van: true, twinRear: false);
                case "hauler": return Build(body, trim, 1.1f, 4.0f, stakes: true, van: false, twinRear: true);
                default: return Build(body, trim, 1f, 3.0f, stakes: true, van: false, twinRear: false);
            }
        }

        static CarModel Build(Color body, Color trim, float scale, float bedLen, bool stakes, bool van, bool twinRear, bool high = false)
        {
            var m = new CarModel();
            var mb = m.Body;
            mb.Scale(scale);
            const float wheelR = 0.48f, hoodLen = 1.4f, cabLen = 1.3f;
            float front = (hoodLen + cabLen + bedLen) / 2 + 0.2f;
            float hoodZ = front - hoodLen / 2 - 0.1f, cabZ = front - hoodLen - cabLen / 2 - 0.1f, bedZ = cabZ - cabLen / 2 - bedLen / 2 - 0.05f;
            float frontAxle = front - 0.55f, rearAxle = bedZ - bedLen * 0.15f;
            m.Length = (front + Mathf.Abs(bedZ - bedLen / 2)) * scale;

            // Chassis and running boards
            mb.Mat(0.3f, 0.4f);
            mb.Box(new Vector3(0, 0.62f, (front + bedZ - bedLen / 2) / 2), new Vector3(1.3f, 0.22f, front - (bedZ - bedLen / 2)), Palette.Black);
            mb.Box(new Vector3(0, 0.5f, cabZ), new Vector3(2.1f, 0.07f, cabLen + 0.9f), Palette.Black);

            // Hood, radiator and lamps
            mb.Paint();
            mb.Box(new Vector3(0, 1.12f, hoodZ), new Vector3(1.22f, 0.78f, hoodLen), body);
            mb.Box(new Vector3(0, 1.52f, hoodZ), new Vector3(1.0f, 0.08f, hoodLen - 0.05f), MeshBuilder.Darken(body, 0.9f));
            mb.Shiny();
            mb.Box(new Vector3(0, 1.12f, front - 0.06f), new Vector3(1.22f, 0.92f, 0.12f), trim);
            mb.Mat(0.4f, 0.2f);
            mb.Box(new Vector3(0, 1.1f, front + 0.005f), new Vector3(0.92f, 0.66f, 0.04f), Palette.Hex(0x2B2B2B));
            mb.Mat(0.9f, 0, true);
            mb.Sphere(new Vector3(-0.68f, 1.42f, front - 0.05f), Vector3.one * 0.17f, 8, NightLights.Lamp);
            mb.Sphere(new Vector3(0.68f, 1.42f, front - 0.05f), Vector3.one * 0.17f, 8, NightLights.Lamp);

            // Fenders over the wheels
            mb.Paint();
            foreach (float side in new[] { -1f, 1f })
            {
                mb.Box(new Vector3(side * 0.9f, 1.0f, frontAxle), new Vector3(0.42f, 0.1f, 1.1f), MeshBuilder.Darken(body, 0.55f));
                mb.Box(new Vector3(side * 0.9f, 0.78f, frontAxle - 0.62f), new Vector3(0.42f, 0.4f, 0.12f), MeshBuilder.Darken(body, 0.55f));
            }

            // Cab: open sides with a roof, or a closed van cab
            mb.Paint();
            float cabTop = 2.45f;
            mb.Box(new Vector3(0, 0.95f, cabZ), new Vector3(1.9f, 0.6f, cabLen), body);                         // lower body
            mb.Box(new Vector3(0, 1.62f, cabZ - cabLen / 2 + 0.06f), new Vector3(1.9f, 0.75f, 0.12f), body);       // back wall
            mb.Box(new Vector3(0, 1.27f, cabZ + 0.2f), new Vector3(1.5f, 0.12f, 0.6f), Palette.Hex(0x5A3A24));    // bench
            mb.Mat(0.85f, 0.1f);
            mb.Box(new Vector3(0, 1.82f, cabZ + cabLen / 2 - 0.06f), new Vector3(1.6f, 0.7f, 0.05f), Glass);     // windscreen
            mb.Matte();
            foreach (float side in new[] { -1f, 1f })
            {
                mb.Box(new Vector3(side * 0.9f, 1.85f, cabZ + cabLen / 2 - 0.08f), new Vector3(0.08f, 1.15f, 0.08f), Palette.Black);
                mb.Box(new Vector3(side * 0.9f, 1.85f, cabZ - cabLen / 2 + 0.06f), new Vector3(0.08f, 1.15f, 0.08f), Palette.Black);
            }
            mb.Box(new Vector3(0, cabTop, cabZ), new Vector3(2.0f, 0.1f, cabLen + 0.25f), van ? body : Palette.Black);
            mb.Box(new Vector3(0, 1.62f, cabZ + 0.38f), new Vector3(0.05f, 0.4f, 0.05f), Palette.Black);
            mb.Shiny();
            mb.Cylinder(new Vector3(0, 1.86f, cabZ + 0.32f), new Vector3(0, 1.9f, cabZ + 0.36f), 0.2f, 10, Palette.Black, false); // wheel

            // Load bed
            float bedTop = 1.08f;
            if (van)
            {
                mb.Paint();
                mb.Box(new Vector3(0, 1.75f, bedZ), new Vector3(2.0f, 1.6f, bedLen), body);
                mb.Box(new Vector3(0, 2.6f, bedZ), new Vector3(1.9f, 0.12f, bedLen - 0.1f), MeshBuilder.Lighten(body, 0.15f));
                mb.Box(new Vector3(0, 1.9f, bedZ), new Vector3(2.03f, 0.3f, bedLen * 0.8f), trim);                 // livery band
                mb.Shiny();
                mb.Box(new Vector3(0, 1.75f, bedZ - bedLen / 2 - 0.03f), new Vector3(1.6f, 1.4f, 0.04f), MeshBuilder.Darken(body, 0.85f));
                m.CargoSize = Vector3.zero;
            }
            else
            {
                mb.Matte();
                mb.Box(new Vector3(0, 0.88f, bedZ), new Vector3(2.0f, 0.18f, bedLen), Palette.WoodPale);
                float sideH = high ? 0.7f : 0.32f;
                mb.Box(new Vector3(-0.97f, bedTop + sideH / 2 - 0.1f, bedZ), new Vector3(0.06f, sideH, bedLen), Palette.Wood);
                mb.Box(new Vector3(0.97f, bedTop + sideH / 2 - 0.1f, bedZ), new Vector3(0.06f, sideH, bedLen), Palette.Wood);
                mb.Box(new Vector3(0, bedTop + sideH / 2 - 0.1f, bedZ - bedLen / 2 + 0.03f), new Vector3(2.0f, sideH, 0.06f), Palette.Wood);
                if (stakes)
                {
                    int n = Mathf.RoundToInt(bedLen / 0.75f);
                    for (int i = 0; i <= n; i++)
                    {
                        float z = bedZ - bedLen / 2 + 0.05f + (bedLen - 0.1f) * i / n;
                        mb.Box(new Vector3(-0.99f, bedTop + 0.35f, z), new Vector3(0.08f, 0.9f, 0.08f), Palette.WoodDark);
                        mb.Box(new Vector3(0.99f, bedTop + 0.35f, z), new Vector3(0.08f, 0.9f, 0.08f), Palette.WoodDark);
                    }
                    mb.Box(new Vector3(-0.99f, bedTop + 0.75f, bedZ), new Vector3(0.06f, 0.08f, bedLen), Palette.WoodDark);
                    mb.Box(new Vector3(0.99f, bedTop + 0.75f, bedZ), new Vector3(0.06f, 0.08f, bedLen), Palette.WoodDark);
                }
                m.CargoAnchor = new Vector3(0, bedTop - 0.1f, bedZ) * scale;
                m.CargoSize = new Vector3(1.8f, high ? 0.8f : 0.6f, bedLen - 0.2f) * scale;
            }

            // Wheels (separate meshes so they can turn)
            AddWheel(m, new Vector3(0, wheelR, frontAxle) * scale, wheelR * scale, false);
            AddWheel(m, new Vector3(0, wheelR, rearAxle) * scale, wheelR * scale, true);
            if (twinRear) AddWheel(m, new Vector3(0, wheelR, rearAxle - 1.05f) * scale, wheelR * scale, true);
            return m;
        }

        /// <summary>One axle: a wheel each side, turning around the X axis at its own origin.</summary>
        static void AddWheel(CarModel m, Vector3 axle, float r, bool dual)
        {
            var w = new WheelSet { Position = axle, Radius = r };
            var mb = w.Mesh;
            float half = dual ? 0.92f : 0.88f, width = dual ? 0.36f : 0.24f;
            foreach (float side in new[] { -1f, 1f })
            {
                mb.Mat(0.2f);
                mb.Cylinder(new Vector3(side * (half - width / 2), 0, 0), new Vector3(side * (half + width / 2), 0, 0), r, 14, Rubber);
                mb.Mat(0.4f, 0.5f);
                mb.Cylinder(new Vector3(side * (half + width / 2), 0, 0), new Vector3(side * (half + width / 2 + 0.02f), 0, 0), r * 0.55f, 10, Hub);
            }
            m.Wheels.Add(w);
        }

        /// <summary>A load of goods for the bed, coloured like the resource: crates, sacks or bars.</summary>
        public static MeshBuilder Cargo(string resourceId, Vector3 size)
        {
            var mb = new MeshBuilder();
            var res = GameDatabase.Resource(resourceId);
            if (res == null || size == Vector3.zero) return mb;
            var col = Palette.Hex(res.ColorRgb);
            mb.Matte();
            int rows = Mathf.Max(2, Mathf.RoundToInt(size.z / 0.7f));
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < 2; j++)
                {
                    float z = -size.z / 2 + (i + 0.5f) * size.z / rows, x = (j - 0.5f) * size.x * 0.5f;
                    float h = size.y * (0.75f + 0.25f * ((i + j) % 2));
                    var c = (i + j) % 3 == 0 ? MeshBuilder.Darken(col, 0.85f) : col;
                    if (res.Cargo == CargoClass.Flat)
                        mb.Box(new Vector3(x, h / 2, z), new Vector3(size.x * 0.46f, h, size.z / rows * 0.96f), c);       // stacked planks / steel
                    else
                    {
                        mb.Box(new Vector3(x, h / 2, z), new Vector3(size.x * 0.44f, h, size.z / rows * 0.86f), MeshBuilder.Lerp(Palette.WoodPale, c, 0.55f));
                        mb.Box(new Vector3(x, h + 0.01f, z), new Vector3(size.x * 0.4f, 0.04f, size.z / rows * 0.6f), c);   // label
                    }
                }
            return mb;
        }
    }
}
