using System.Collections.Generic;
using Ironhorse.Core;
using UnityEngine;

namespace Ironhorse.Game
{
    /// <summary>One vehicle of a consist: built body + wheels to spin + effect anchors.</summary>
    public sealed class CarModel
    {
        public MeshBuilder Body = new MeshBuilder();
        public float Length;
        public readonly List<WheelSet> Wheels = new List<WheelSet>();
        /// <summary>Side rods that orbit with the drivers (steam only).</summary>
        public readonly List<RodSpec> Rods = new List<RodSpec>();
        public Vector3? Stack;
        public Vector3? Exhaust;
        public Vector3 CargoAnchor;
        public Vector3 CargoSize;
        public bool IsLoco;
    }

    public sealed class WheelSet
    {
        public MeshBuilder Mesh = new MeshBuilder();
        public Vector3 Position;
        public float Radius;
    }

    public sealed class RodSpec
    {
        public MeshBuilder Mesh = new MeshBuilder();
        public Vector3 Center;   // axle line center
        public float Crank;      // crank radius
    }

    /// <summary>
    /// Procedural locomotive and wagon models. Cars face +Z, origin at their center, y = 0 at rail top.
    /// </summary>
    public static class TrainFactory
    {
        public const float Gap = 0.7f;
        static readonly Color WheelColor = Palette.Hex(0x2A2624);
        static readonly Color Tyre = Palette.Hex(0x8F8A86);

        // ================================================================ locomotives

        public static CarModel Locomotive(LocomotiveDef def, int level)
        {
            Color primary = Palette.Hex(def.PrimaryRgb), trim = Palette.Hex(def.TrimRgb);
            switch (def.Style)
            {
                case LocoStyle.SteamSwitcher: return Steam(primary, trim, 0, 2, 0, 5.2f, tender: false, diamondStack: false, saddleTank: true, level: level);
                case LocoStyle.SteamAmerican: return Steam(primary, trim, 2, 2, 0, 6.5f, true, true, false, level);
                case LocoStyle.SteamMogul: return Steam(primary, trim, 1, 3, 0, 7f, true, true, false, level);
                case LocoStyle.SteamTenWheeler: return Steam(primary, trim, 2, 3, 0, 7.6f, true, false, false, level);
                case LocoStyle.SteamHeavy: return Steam(primary, trim, 1, 4, 1, 8.8f, true, false, false, level);
                case LocoStyle.SteamStreamlined: return Streamlined(primary, trim, level);
                case LocoStyle.DieselStreamliner: return Diesel(primary, trim, true, level);
                case LocoStyle.DieselTwin: return Diesel(primary, trim, true, level, twin: true);
            }
            return Steam(primary, trim, 2, 2, 0, 6.5f, true, true, false, level);
        }

        static CarModel Steam(Color primary, Color trim, int pilotAxles, int drivers, int trailing, float boilerLen,
            bool tender, bool diamondStack, bool saddleTank, int level)
        {
            var m = new CarModel { IsLoco = true };
            var mb = m.Body;
            float driverR = drivers >= 4 ? 0.72f : drivers == 3 ? 0.78f : saddleTank ? 0.62f : 0.86f;
            float spacing = driverR * 2 + 0.25f;
            float driversLen = spacing * (drivers - 1);
            float pilotLen = pilotAxles * 1.1f + (pilotAxles > 0 ? 0.6f : 0);
            float cabLen = 2.4f;
            float tenderLen = tender ? (drivers >= 4 ? 6.4f : 5.2f) : 0;
            float locoLen = Mathf.Max(pilotLen + driversLen + driverR * 2 + trailing * 1.2f + cabLen + 1.0f, boilerLen + cabLen + 0.9f);
            float total = locoLen + (tender ? tenderLen + 0.4f : 0);
            m.Length = total;
            float front = total / 2;
            float boilerY = driverR * 2 + 0.95f;
            float boilerR = saddleTank ? 0.82f : 0.92f + (drivers >= 4 ? 0.12f : 0);
            Color boilerCol = MeshBuilder.Lerp(Palette.Iron, primary, 0.25f);
            if (level >= 5) trim = MeshBuilder.Lerp(trim, Palette.Brass, 0.4f);

            // Frame / running board
            mb.Mat(0.35f, 0.4f);
            mb.Box(new Vector3(0, driverR + 0.25f, front - locoLen / 2), new Vector3(1.9f, 0.35f, locoLen - 0.4f), Palette.Iron);
            mb.Box(new Vector3(0, driverR * 2 + 0.25f, front - locoLen / 2 + 0.5f), new Vector3(2.5f, 0.08f, locoLen - 1.6f), Palette.Iron);

            // Boiler
            float boilerFront = front - 0.7f;
            float boilerBack = boilerFront - boilerLen;
            mb.Paint();
            mb.Cylinder(new Vector3(0, boilerY, boilerBack), new Vector3(0, boilerY, boilerFront - 1.1f), boilerR, 16, boilerCol);
            // Smokebox
            mb.Mat(0.25f, 0.3f);
            mb.Cylinder(new Vector3(0, boilerY, boilerFront - 1.15f), new Vector3(0, boilerY, boilerFront), boilerR + 0.04f, 16, Palette.Black);
            mb.Cylinder(new Vector3(0, boilerY, boilerFront), new Vector3(0, boilerY, boilerFront + 0.06f), boilerR - 0.15f, 16, MeshBuilder.Darken(Palette.Iron, 0.8f));
            // Brass bands
            mb.Shiny();
            for (int i = 1; i <= 3; i++)
            {
                float z = Mathf.Lerp(boilerBack, boilerFront - 1.15f, i / 4f);
                mb.Cylinder(new Vector3(0, boilerY, z - 0.05f), new Vector3(0, boilerY, z + 0.05f), boilerR + 0.025f, 16, trim, false);
            }
            if (saddleTank)
            {
                mb.Paint();
                mb.Box(new Vector3(0, boilerY + 0.15f, (boilerBack + boilerFront - 1.2f) / 2), new Vector3(boilerR * 2 + 0.5f, boilerR * 1.6f, boilerLen - 1.6f), primary);
                mb.Box(new Vector3(0, boilerY + boilerR * 0.95f, (boilerBack + boilerFront - 1.2f) / 2), new Vector3(boilerR * 2 + 0.3f, 0.12f, boilerLen - 1.8f), trim);
            }

            // Smokestack
            float stackZ = boilerFront - 0.55f;
            float stackBase = boilerY + boilerR - 0.1f;
            mb.Mat(0.3f, 0.4f);
            if (diamondStack)
            {
                mb.Cylinder(new Vector3(0, stackBase, stackZ), new Vector3(0, stackBase + 0.9f, stackZ), 0.28f, 0.32f, 10, Palette.Black);
                mb.Cylinder(new Vector3(0, stackBase + 0.9f, stackZ), new Vector3(0, stackBase + 1.5f, stackZ), 0.32f, 0.78f, 10, Palette.Black);
                mb.Cylinder(new Vector3(0, stackBase + 1.5f, stackZ), new Vector3(0, stackBase + 1.9f, stackZ), 0.78f, 0.35f, 10, Palette.Black);
                m.Stack = new Vector3(0, stackBase + 1.9f, stackZ);
            }
            else
            {
                mb.Cylinder(new Vector3(0, stackBase, stackZ), new Vector3(0, stackBase + 1.1f, stackZ), 0.34f, 0.38f, 12, Palette.Black);
                mb.Shiny();
                mb.Cylinder(new Vector3(0, stackBase + 1.05f, stackZ), new Vector3(0, stackBase + 1.2f, stackZ), 0.45f, 12, trim);
                m.Stack = new Vector3(0, stackBase + 1.2f, stackZ);
            }
            // Domes
            mb.Shiny();
            float domeZ = Mathf.Lerp(boilerBack, boilerFront - 1.1f, 0.55f);
            mb.Cylinder(new Vector3(0, boilerY + boilerR - 0.15f, domeZ), new Vector3(0, boilerY + boilerR + 0.45f, domeZ), 0.42f, 0.36f, 12, trim);
            mb.Sphere(new Vector3(0, boilerY + boilerR + 0.45f, domeZ), new Vector3(0.36f, 0.22f, 0.36f), 12, trim);
            float sandZ = Mathf.Lerp(boilerBack, boilerFront - 1.1f, 0.2f);
            mb.Paint();
            mb.Cylinder(new Vector3(0, boilerY + boilerR - 0.15f, sandZ), new Vector3(0, boilerY + boilerR + 0.3f, sandZ), 0.36f, 0.3f, 12, primary);
            mb.Sphere(new Vector3(0, boilerY + boilerR + 0.3f, sandZ), new Vector3(0.3f, 0.18f, 0.3f), 12, primary);
            // Bell & whistle
            mb.Shiny();
            mb.Cylinder(new Vector3(0, boilerY + boilerR + 0.15f, (domeZ + stackZ) / 2 + 0.3f), new Vector3(0, boilerY + boilerR + 0.5f, (domeZ + stackZ) / 2 + 0.3f), 0.2f, 0.12f, 8, Palette.Brass);

            // Headlamp (lit)
            if (diamondStack || !saddleTank)
            {
                mb.Paint();
                mb.Box(new Vector3(0, boilerY + boilerR + 0.35f, boilerFront - 0.2f), new Vector3(0.75f, 0.7f, 0.7f), primary);
                mb.Lit();
                mb.Box(new Vector3(0, boilerY + boilerR + 0.35f, boilerFront + 0.17f), new Vector3(0.5f, 0.45f, 0.06f), Palette.Window);
            }
            else
            {
                mb.Lit();
                mb.Cylinder(new Vector3(0, boilerY + 0.6f, boilerFront + 0.06f), new Vector3(0, boilerY + 0.6f, boilerFront + 0.14f), 0.2f, 8, Palette.Window);
            }

            // Pilot (cowcatcher)
            mb.Mat(0.3f, 0.3f);
            float pilotZ = front - 0.2f;
            mb.Box(new Vector3(0, 0.95f, pilotZ - 0.1f), new Vector3(2.4f, 0.3f, 0.3f), primary);
            for (int i = -4; i <= 4; i++)
            {
                mb.Push().Translate(i * 0.26f, 0.5f, pilotZ + 0.15f).Rotate(Quaternion.Euler(-35, i * 5, 0));
                mb.Box(Vector3.zero, new Vector3(0.08f, 0.08f, 1.2f - Mathf.Abs(i) * 0.08f), saddleTank ? Palette.Iron : Palette.PaintRed);
                mb.Pop();
            }

            // Cylinders + steam chests
            float cylZ = boilerFront - 1.0f - pilotLen * 0.2f;
            for (int sd = -1; sd <= 1; sd += 2)
            {
                mb.Mat(0.5f, 0.5f);
                mb.Cylinder(new Vector3(sd * 1.05f, driverR + 0.55f, cylZ - 0.6f), new Vector3(sd * 1.05f, driverR + 0.55f, cylZ + 0.6f), 0.36f, 10, boilerCol);
                mb.Shiny();
                mb.Cylinder(new Vector3(sd * 1.05f, driverR + 0.55f, cylZ + 0.6f), new Vector3(sd * 1.05f, driverR + 0.55f, cylZ + 0.68f), 0.38f, 10, trim, false);
            }

            // Cab
            float cabFront = boilerBack + 0.1f, cabBack = cabFront - cabLen;
            float cabTop = boilerY + boilerR + 1.25f;
            mb.Paint();
            mb.Box(new Vector3(0, (driverR * 2 + 0.3f + cabTop) / 2, (cabFront + cabBack) / 2), new Vector3(2.5f, cabTop - driverR * 2 - 0.3f, cabLen), primary);
            mb.Lit();
            for (int sd = -1; sd <= 1; sd += 2)
                mb.Box(new Vector3(sd * 1.26f, cabTop - 0.75f, (cabFront + cabBack) / 2 + 0.2f), new Vector3(0.04f, 0.7f, 0.9f), Palette.Window);
            mb.Box(new Vector3(-0.6f, cabTop - 0.7f, cabFront + 0.02f), new Vector3(0.5f, 0.5f, 0.04f), Palette.Window);
            mb.Box(new Vector3(0.6f, cabTop - 0.7f, cabFront + 0.02f), new Vector3(0.5f, 0.5f, 0.04f), Palette.Window);
            mb.Paint();
            mb.Push().Translate(0, cabTop + 0.12f, (cabFront + cabBack) / 2);
            mb.Box(Vector3.zero, new Vector3(2.9f, 0.16f, cabLen + 0.4f), MeshBuilder.Darken(primary, 0.7f));
            mb.Box(new Vector3(0, 0.12f, 0), new Vector3(2.2f, 0.12f, cabLen), MeshBuilder.Darken(primary, 0.6f));
            mb.Pop();
            mb.Shiny();
            mb.Box(new Vector3(0, driverR * 2 + 0.45f, (cabFront + cabBack) / 2), new Vector3(2.55f, 0.12f, cabLen + 0.02f), trim);

            // Tender
            if (tender)
            {
                float tFront = cabBack - 0.4f, tBack = tFront - tenderLen;
                float tc = (tFront + tBack) / 2;
                mb.Paint();
                mb.Box(new Vector3(0, 0.95f + 1.0f, tc), new Vector3(2.5f, 2.0f, tenderLen), primary);
                mb.Shiny();
                mb.Box(new Vector3(0, 2.95f, tc), new Vector3(2.56f, 0.12f, tenderLen + 0.04f), trim);
                mb.Box(new Vector3(0, 1.0f, tc), new Vector3(2.56f, 0.1f, tenderLen + 0.04f), trim);
                mb.Matte();
                mb.Sphere(new Vector3(0, 2.9f, tc + tenderLen * 0.2f), new Vector3(1.1f, 0.45f, tenderLen * 0.3f), 8, Palette.Black, false);
                mb.Mat(0.35f, 0.4f);
                mb.Box(new Vector3(0, 0.75f, tc), new Vector3(2f, 0.3f, tenderLen - 0.3f), Palette.Iron);
                for (int a = 0; a < 2; a++)
                    for (int k = 0; k < 2; k++)
                        AddWheel(m, new Vector3(0, 0.45f, tc + (a == 0 ? 1 : -1) * (tenderLen / 2 - 1.1f) + (k == 0 ? 0.55f : -0.55f)), 0.45f, false);
            }

            // Wheels
            float driversCenter = boilerBack + driversLen / 2 + driverR * 0.6f;
            if (saddleTank) driversCenter = (boilerBack + boilerFront) / 2 - 0.4f;
            float firstDriver = driversCenter + driversLen / 2;
            for (int i = 0; i < drivers; i++)
                AddWheel(m, new Vector3(0, driverR, firstDriver - i * spacing), driverR, true);
            for (int i = 0; i < pilotAxles; i++)
                AddWheel(m, new Vector3(0, 0.45f, firstDriver + driverR + 0.7f + (pilotAxles - 1 - i) * 1.1f), 0.45f, false);
            for (int i = 0; i < trailing; i++)
                AddWheel(m, new Vector3(0, 0.5f, firstDriver - (drivers - 1) * spacing - driverR - 0.8f - i * 1.1f), 0.5f, false);

            // Side rods linking all drivers
            if (drivers > 1)
                for (int sd = -1; sd <= 1; sd += 2)
                {
                    var rod = new RodSpec { Center = new Vector3(sd * 1.17f, driverR, driversCenter), Crank = driverR * 0.42f };
                    rod.Mesh.Mat(0.7f, 0.85f);
                    rod.Mesh.Box(Vector3.zero, new Vector3(0.08f, 0.16f, driversLen + 0.3f), Palette.Steel);
                    m.Rods.Add(rod);
                }
            return m;
        }

        static CarModel Streamlined(Color primary, Color trim, int level)
        {
            var m = Steam(primary, trim, 2, 3, 2, 8.6f, true, false, false, level);
            // Art-deco shroud over the boiler: a long smooth casing with a bullet nose.
            var mb = m.Body;
            float front = m.Length / 2;
            mb.Paint();
            float y = 2.65f;
            mb.Box(new Vector3(0, y + 0.1f, front - 5.5f), new Vector3(2.7f, 2.2f, 9.2f), primary);
            mb.Sphere(new Vector3(0, y + 0.1f, front - 0.95f), new Vector3(1.35f, 1.1f, 1.4f), 14, primary);
            mb.Shiny();
            for (int s = -1; s <= 1; s += 2)
                mb.Box(new Vector3(s * 1.36f, y - 0.25f, front - 5.3f), new Vector3(0.04f, 0.18f, 9f), trim);
            mb.Box(new Vector3(0, y + 1.22f, front - 5.5f), new Vector3(1.2f, 0.06f, 9.0f), trim);
            mb.Lit();
            mb.Cylinder(new Vector3(0, y + 0.3f, front + 0.38f), new Vector3(0, y + 0.3f, front + 0.46f), 0.3f, 12, Palette.Window);
            mb.Paint();
            mb.Box(new Vector3(0, 1.1f, front - 5.5f), new Vector3(2.75f, 0.7f, 9.6f), MeshBuilder.Darken(primary, 0.75f)); // skirt
            return m;
        }

        static CarModel Diesel(Color primary, Color trim, bool cab, int level, bool twin = false)
        {
            var m = new CarModel { IsLoco = true };
            var mb = m.Body;
            float unitLen = 15f;
            m.Length = twin ? unitLen * 2 + 0.6f : unitLen;
            for (int u = 0; u < (twin ? 2 : 1); u++)
            {
                float c = twin ? (u == 0 ? unitLen / 2 + 0.3f : -unitLen / 2 - 0.3f) : 0;
                float dir = u == 0 ? 1 : -1;
                mb.Push().Translate(0, 0, c).Rotate(Quaternion.Euler(0, u == 0 ? 0 : 180, 0));
                float h = 3.4f, y0 = 1.25f;
                mb.Paint();
                // Body
                mb.Box(new Vector3(0, y0 + h / 2, -1.2f), new Vector3(3f, h, unitLen - 2.4f), primary);
                // Nose: bulbous streamlined front
                mb.Sphere(new Vector3(0, y0 + h * 0.42f, unitLen / 2 - 2.4f), new Vector3(1.5f, h * 0.55f, 2.4f), 16, primary);
                mb.Box(new Vector3(0, y0 + h * 0.75f, unitLen / 2 - 3.2f), new Vector3(3f, h * 0.5f, 1.6f), primary);
                // Windshield
                mb.Lit();
                for (int s = -1; s <= 1; s += 2)
                {
                    mb.Push().Translate(s * 0.62f, y0 + h * 0.86f, unitLen / 2 - 2.35f).Rotate(Quaternion.Euler(-28, s * 18, 0));
                    mb.Box(Vector3.zero, new Vector3(1.0f, 0.62f, 0.06f), Palette.Hex(0x9EC3D6));
                    mb.Pop();
                }
                // Headlight
                mb.Cylinder(new Vector3(0, y0 + h * 0.48f, unitLen / 2 - 0.15f), new Vector3(0, y0 + h * 0.48f, unitLen / 2 - 0.05f), 0.28f, 12, Palette.Window);
                // Stripes and nose wings
                mb.Shiny();
                for (int s = -1; s <= 1; s += 2)
                {
                    mb.Box(new Vector3(s * 1.51f, y0 + h * 0.35f, -1.2f), new Vector3(0.03f, 0.35f, unitLen - 2.4f), trim);
                    mb.Box(new Vector3(s * 1.51f, y0 + h * 0.55f, -1.2f), new Vector3(0.03f, 0.12f, unitLen - 2.4f), trim);
                    mb.Push().Translate(s * 0.9f, y0 + h * 0.4f, unitLen / 2 - 0.8f).Rotate(Quaternion.Euler(0, s * -40, 0));
                    mb.Box(Vector3.zero, new Vector3(0.05f, 0.3f, 1.6f), trim);
                    mb.Pop();
                }
                // Side portholes
                mb.Lit();
                for (int s = -1; s <= 1; s += 2)
                    for (int w = 0; w < 4; w++)
                        mb.Cylinder(new Vector3(s * 1.5f, y0 + h * 0.75f, 2f - w * 2.4f), new Vector3(s * 1.53f, y0 + h * 0.75f, 2f - w * 2.4f), 0.3f, 10, Palette.Hex(0xBFD3D8));
                // Roof
                mb.Paint();
                mb.Box(new Vector3(0, y0 + h + 0.12f, -1.2f), new Vector3(2.6f, 0.25f, unitLen - 2.6f), MeshBuilder.Darken(primary, 0.85f));
                mb.Mat(0.4f, 0.6f);
                mb.Box(new Vector3(0, y0 + h + 0.3f, -2.5f), new Vector3(1.6f, 0.25f, 2.0f), Palette.Steel);
                // Skirt and fuel tank
                mb.Mat(0.35f, 0.4f);
                mb.Box(new Vector3(0, y0 - 0.25f, 0), new Vector3(2.6f, 0.5f, unitLen - 1.5f), Palette.Iron);
                mb.Box(new Vector3(0, 0.75f, 0), new Vector3(2.2f, 0.7f, 4f), MeshBuilder.Darken(primary, 0.5f));
                mb.Pop();
                for (int t = -1; t <= 1; t += 2)
                    for (int a = 0; a < 3; a++)
                        AddWheel(m, new Vector3(0, 0.5f, c + t * (unitLen / 2 - 2.8f) + (a - 1) * 1.15f), 0.5f, false);
                if (u == 0) m.Exhaust = new Vector3(0, y0 + h + 0.45f, c - 2.5f);
            }
            return m;
        }

        static void AddWheel(CarModel m, Vector3 pos, float r, bool driver)
        {
            var w = new WheelSet { Position = pos, Radius = r };
            var mb = w.Mesh;
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * 0.78f;
                mb.Mat(0.35f, 0.5f);
                mb.Cylinder(new Vector3(x - 0.09f * s, 0, 0), new Vector3(x + 0.09f * s, 0, 0), r, driver ? 18 : 12, WheelColor);
                mb.Mat(0.7f, 0.9f);
                mb.Cylinder(new Vector3(x + 0.1f * s, 0, 0), new Vector3(x + 0.12f * s, 0, 0), r * 0.97f, driver ? 18 : 12, Tyre, false);
                if (driver)
                {
                    // spokes + counterweight make rotation visible
                    mb.Mat(0.35f, 0.5f);
                    for (int k = 0; k < 10; k++)
                    {
                        mb.Push().Translate(x + 0.13f * s, 0, 0).Rotate(Quaternion.Euler(k * 36, 0, 0));
                        mb.Box(new Vector3(0, r * 0.5f, 0), new Vector3(0.05f, r * 0.85f, 0.07f), Palette.Hex(0x48403C));
                        mb.Pop();
                    }
                    mb.Box(new Vector3(x + 0.14f * s, -r * 0.45f, 0), new Vector3(0.06f, r * 0.45f, r * 0.9f), Palette.Hex(0x48403C));
                    mb.Shiny();
                    mb.Cylinder(new Vector3(x + 0.12f * s, 0, 0), new Vector3(x + 0.2f * s, 0, 0), 0.12f, 8, Palette.Brass);
                }
            }
            mb.Mat(0.3f, 0.4f);
            mb.Cylinder(new Vector3(-0.7f, 0, 0), new Vector3(0.7f, 0, 0), 0.08f, 6, Palette.Iron, false);
            m.Wheels.Add(w);
        }

        // ================================================================ wagons

        public static CarModel Wagon(WagonDef def)
        {
            var m = new CarModel { Length = def.Capacity >= 14 ? 10f : 8f };
            var mb = m.Body;
            Color col = Palette.Hex(def.ColorRgb);
            float L = m.Length;
            // Underframe + couplers
            mb.Mat(0.35f, 0.4f);
            mb.Box(new Vector3(0, 1.05f, 0), new Vector3(2.4f, 0.3f, L - 0.2f), Palette.Iron);
            mb.Box(new Vector3(0, 0.9f, L / 2), new Vector3(0.3f, 0.3f, 0.5f), Palette.Iron);
            mb.Box(new Vector3(0, 0.9f, -L / 2), new Vector3(0.3f, 0.3f, 0.5f), Palette.Iron);

            switch (def.Cargo)
            {
                case CargoClass.Flat:
                    mb.Matte();
                    mb.Box(new Vector3(0, 1.3f, 0), new Vector3(2.7f, 0.22f, L), col, MeshBuilder.Lighten(col, 0.15f));
                    for (float z = -L / 2 + 0.6f; z <= L / 2 - 0.5f; z += (L - 1.2f) / 3)
                        for (int s = -1; s <= 1; s += 2)
                            mb.Box(new Vector3(s * 1.28f, 1.9f, z), new Vector3(0.12f, 1.1f, 0.12f), Palette.WoodDark);
                    m.CargoAnchor = new Vector3(0, 1.42f, 0); m.CargoSize = new Vector3(2.3f, 1.3f, L - 0.8f);
                    break;
                case CargoClass.Bulk:
                    mb.Mat(0.3f, 0.3f);
                    mb.Box(new Vector3(0, 2.2f, 0), new Vector3(2.8f, 1.9f, L - 0.3f), col);
                    for (float z = -L / 2 + 0.6f; z < L / 2; z += 1.2f)
                        for (int s = -1; s <= 1; s += 2)
                            mb.Box(new Vector3(s * 1.42f, 2.2f, z), new Vector3(0.06f, 1.9f, 0.12f), MeshBuilder.Darken(col, 0.8f));
                    // Hopper chutes
                    mb.Push().Translate(0, 0.85f, L / 4).Rotate(Quaternion.Euler(45, 0, 0));
                    mb.Box(Vector3.zero, new Vector3(1.8f, 0.6f, 0.6f), MeshBuilder.Darken(col, 0.8f));
                    mb.Pop();
                    mb.Push().Translate(0, 0.85f, -L / 4).Rotate(Quaternion.Euler(45, 0, 0));
                    mb.Box(Vector3.zero, new Vector3(1.8f, 0.6f, 0.6f), MeshBuilder.Darken(col, 0.8f));
                    mb.Pop();
                    m.CargoAnchor = new Vector3(0, 3.0f, 0); m.CargoSize = new Vector3(2.5f, 0.8f, L - 0.8f);
                    break;
                case CargoClass.Box:
                    mb.Matte();
                    mb.Box(new Vector3(0, 2.6f, 0), new Vector3(2.8f, 2.8f, L), col);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        mb.Box(new Vector3(s * 1.41f, 2.5f, 0), new Vector3(0.05f, 2.4f, 1.8f), MeshBuilder.Darken(col, 0.8f)); // door
                        for (float z = -L / 2 + 0.4f; z < L / 2; z += 0.9f)
                            mb.Box(new Vector3(s * 1.41f, 2.6f, z), new Vector3(0.03f, 2.8f, 0.06f), MeshBuilder.Darken(col, 0.9f));
                    }
                    mb.Box(new Vector3(0, 4.05f, 0), new Vector3(2.95f, 0.14f, L + 0.1f), Palette.RoofTin);
                    mb.Box(new Vector3(0, 4.18f, 0), new Vector3(0.5f, 0.06f, L), Palette.WoodDark); // roof walk
                    if (def.Id == "reefer")
                    {
                        mb.Shiny();
                        mb.Box(new Vector3(0, 4.2f, L / 2 - 0.6f), new Vector3(0.7f, 0.25f, 0.7f), Palette.Steel);
                    }
                    break;
                case CargoClass.Livestock:
                    mb.Matte();
                    mb.Box(new Vector3(0, 1.3f, 0), new Vector3(2.8f, 0.2f, L), Palette.WoodDark);
                    for (float y = 1.6f; y < 3.8f; y += 0.45f)
                        for (int s = -1; s <= 1; s += 2)
                            mb.Box(new Vector3(s * 1.38f, y, 0), new Vector3(0.06f, 0.2f, L), col);
                    for (float z = -L / 2; z <= L / 2; z += L / 4)
                        for (int s = -1; s <= 1; s += 2)
                            mb.Box(new Vector3(s * 1.4f, 2.6f, z), new Vector3(0.1f, 2.6f, 0.15f), MeshBuilder.Darken(col, 0.7f));
                    mb.Box(new Vector3(0, 3.95f, 0), new Vector3(2.95f, 0.14f, L + 0.1f), Palette.RoofTin);
                    m.CargoAnchor = new Vector3(0, 1.4f, 0); m.CargoSize = new Vector3(2.4f, 1.5f, L - 0.6f);
                    break;
                case CargoClass.Coach:
                    mb.Paint();
                    mb.Box(new Vector3(0, 2.6f, 0), new Vector3(2.9f, 2.8f, L - 0.6f), col);
                    mb.Shiny();
                    mb.Box(new Vector3(0, 1.4f, 0), new Vector3(2.95f, 0.12f, L - 0.6f), Palette.Brass);
                    mb.Lit();
                    for (int s = -1; s <= 1; s += 2)
                        for (float z = -L / 2 + 1.2f; z < L / 2 - 0.8f; z += 1.1f)
                            mb.Box(new Vector3(s * 1.46f, 2.85f, z), new Vector3(0.04f, 0.9f, 0.7f), Palette.Window);
                    mb.Paint();
                    mb.Box(new Vector3(0, 4.1f, 0), new Vector3(3.0f, 0.2f, L - 0.4f), Palette.Hex(0x3A3A3A));
                    mb.Box(new Vector3(0, 4.35f, 0), new Vector3(1.4f, 0.4f, L - 1.4f), Palette.Hex(0x3A3A3A)); // clerestory
                    mb.Matte();
                    mb.Box(new Vector3(0, 2.6f, L / 2 - 0.2f), new Vector3(2.4f, 2.6f, 0.4f), MeshBuilder.Darken(col, 0.7f));
                    mb.Box(new Vector3(0, 2.6f, -L / 2 + 0.2f), new Vector3(2.4f, 2.6f, 0.4f), MeshBuilder.Darken(col, 0.7f));
                    break;
                case CargoClass.Tank:
                    mb.Mat(0.6f, 0.4f);
                    mb.Cylinder(new Vector3(0, 2.4f, -L / 2 + 0.3f), new Vector3(0, 2.4f, L / 2 - 0.3f), 1.3f, 16, col);
                    mb.Sphere(new Vector3(0, 2.4f, L / 2 - 0.3f), new Vector3(1.3f, 1.3f, 0.35f), 16, col);
                    mb.Sphere(new Vector3(0, 2.4f, -L / 2 + 0.3f), new Vector3(1.3f, 1.3f, 0.35f), 16, col);
                    mb.Cylinder(new Vector3(0, 3.5f, 0), new Vector3(0, 4.1f, 0), 0.55f, 12, col);
                    mb.Shiny();
                    mb.Box(new Vector3(0, 1.6f, 0), new Vector3(2.9f, 0.08f, L - 1), Palette.Steel);
                    break;
            }
            // Trucks
            for (int t = -1; t <= 1; t += 2)
            {
                float tz = t * (L / 2 - 1.4f);
                mb.Mat(0.35f, 0.4f);
                mb.Box(new Vector3(0, 0.65f, tz), new Vector3(2.0f, 0.3f, 2.2f), Palette.Iron);
                AddWheel(m, new Vector3(0, 0.45f, tz + 0.65f), 0.45f, false);
                AddWheel(m, new Vector3(0, 0.45f, tz - 0.65f), 0.45f, false);
            }
            return m;
        }

        /// <summary>Visible load for open wagons, colored by the resource.</summary>
        public static MeshBuilder Cargo(CarModel car, WagonDef def, ResourceDef res)
        {
            if (car.CargoSize == Vector3.zero) return null;
            var mb = new MeshBuilder();
            Color c = Palette.Hex(res.ColorRgb);
            var a = car.CargoAnchor; var s = car.CargoSize;
            switch (def.Cargo)
            {
                case CargoClass.Flat:
                    if (res.Id == "timber")
                    {
                        mb.Matte();
                        for (int row = 0; row < 3; row++)
                            for (int i = 0; i < 4 - row; i++)
                            {
                                float x = (i - (3 - row) / 2f) * 0.55f;
                                float y = a.y + 0.28f + row * 0.48f;
                                mb.Cylinder(new Vector3(x, y, -s.z / 2), new Vector3(x, y, s.z / 2), 0.27f, 7, Palette.Bark);
                            }
                    }
                    else if (res.Id == "steel")
                    {
                        mb.Mat(0.6f, 0.9f);
                        for (int i = 0; i < 6; i++) mb.Box(new Vector3((i % 3 - 1) * 0.7f, a.y + 0.2f + (i / 3) * 0.35f, 0), new Vector3(0.5f, 0.3f, s.z), c);
                    }
                    else
                    {
                        mb.Matte();
                        for (int i = 0; i < 5; i++) mb.Box(new Vector3(0, a.y + 0.15f + i * 0.22f, 0), new Vector3(s.x, 0.2f, s.z * 0.9f), i % 2 == 0 ? c : MeshBuilder.Darken(c, 0.9f));
                    }
                    break;
                case CargoClass.Bulk:
                    mb.Matte();
                    for (int i = 0; i < 3; i++)
                        mb.Sphere(new Vector3(0, a.y - 0.1f, (i - 1) * s.z / 3), new Vector3(s.x / 2, s.y, s.z / 5), 8, i == 1 ? c : MeshBuilder.Darken(c, 0.9f), false);
                    break;
                case CargoClass.Livestock:
                    mb.Matte();
                    for (int i = 0; i < 3; i++)
                    {
                        float z = (i - 1) * s.z / 3;
                        mb.Box(new Vector3(0, a.y + 0.9f, z), new Vector3(0.7f, 0.7f, 1.5f), c);
                        mb.Box(new Vector3(0, a.y + 1.05f, z + 0.9f), new Vector3(0.45f, 0.45f, 0.5f), c);
                    }
                    break;
                default:
                    return null;
            }
            return mb;
        }
    }
}
