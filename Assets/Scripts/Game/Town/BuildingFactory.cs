using System.Collections.Generic;
using Ironhorse.Core;
using UnityEngine;

namespace Ironhorse.Game
{
    /// <summary>A procedurally generated building: static body + animated parts + effect anchors.</summary>
    public sealed class BuildingModel
    {
        public readonly MeshBuilder Body = new MeshBuilder();
        public readonly List<Vector3> Smoke = new List<Vector3>();
        public readonly List<SpinPart> Spins = new List<SpinPart>();
        public float Height = 6f;
    }

    public sealed class SpinPart
    {
        public MeshBuilder Mesh = new MeshBuilder();
        public Vector3 Position;
        public Vector3 Euler;
        public Vector3 Speed;
        /// <summary>Only spins while the building is producing.</summary>
        public bool WhenWorking = true;
    }

    /// <summary>
    /// Builds every BuildingStyle from primitives. Footprint is a 12×12 plot centered on the origin,
    /// front facing -Z (toward the street).
    /// </summary>
    public static class BuildingFactory
    {
        static System.Random _rng;
        static float R() { return (float)_rng.NextDouble(); }
        static T Pick<T>(params T[] items) { return items[_rng.Next(items.Length)]; }

        static readonly Color[] HousePaints =
        {
            Palette.Hex(0xE8DCC0), Palette.Hex(0xB9CBD6), Palette.Hex(0xD7B98E), Palette.Hex(0xA7B89A),
            Palette.Hex(0xE3C4B2), Palette.Hex(0xC9A86A), Palette.Hex(0xF0E8D8), Palette.Hex(0x9FB3A6),
        };

        public static BuildingModel Build(BuildingStyle style, int level, int seed)
        {
            _rng = new System.Random(seed * 7919 + (int)style);
            var m = new BuildingModel();
            var mb = m.Body;
            mb.Matte();
            float lv = 1 + 0.035f * (level - 1);
            mb.Scale(lv);
            switch (style)
            {
                case BuildingStyle.Cabin: Cabin(m); break;
                case BuildingStyle.ClapboardHouse: Clapboard(m); break;
                case BuildingStyle.BoardingHouse: Boarding(m); break;
                case BuildingStyle.BrickRowhouse: Rowhouse(m); break;
                case BuildingStyle.GrandHotel: Hotel(m); break;
                case BuildingStyle.Sawmill: Sawmill(m); break;
                case BuildingStyle.Smelter: Smelter(m); break;
                case BuildingStyle.GristMill: GristMill(m); break;
                case BuildingStyle.PackingHouse: Packing(m); break;
                case BuildingStyle.ToolWorks: ToolWorks(m); break;
                case BuildingStyle.WireWorks: WireWorks(m); break;
                case BuildingStyle.AssayOffice: Assay(m); break;
                case BuildingStyle.Refinery: Refinery(m); break;
                case BuildingStyle.WaterTower: WaterTower(m); break;
                case BuildingStyle.Saloon: Saloon(m); break;
                case BuildingStyle.Schoolhouse: School(m); break;
                case BuildingStyle.TelegraphOffice: Telegraph(m); break;
                case BuildingStyle.Bank: Bank(m); break;
                case BuildingStyle.ClockTower: ClockTower(m); break;
                case BuildingStyle.LampPost: LampPost(m); break;
                case BuildingStyle.Garden: Garden(m); break;
                case BuildingStyle.Fountain: Fountain(m); break;
                case BuildingStyle.Garage: Garage(m); break;
            }
            if (level >= 3 && style != BuildingStyle.LampPost && style != BuildingStyle.Garden && style != BuildingStyle.Fountain)
                Flag(mb, new Vector3(5.2f, 0, 4.8f), m.Height * 0.6f + 3, level >= 5 ? Palette.Brass : Palette.PaintRed);
            for (int i = 0; i < m.Smoke.Count; i++) m.Smoke[i] *= lv;
            m.Height *= lv;
            return m;
        }

        // ================================================================ helpers

        static void Foundation(MeshBuilder mb, float w, float d, float h = 0.5f)
        {
            mb.Matte();
            mb.Box(new Vector3(0, h / 2, 0), new Vector3(w, h, d), Palette.StoneDark, Palette.Stone);
        }

        static void Walls(MeshBuilder mb, Vector3 c, Vector3 size, Color col) { mb.Matte(); mb.Box(c + Vector3.up * size.y / 2, size, col); }

        static void Clapboards(MeshBuilder mb, Vector3 c, Vector3 size, Color col)
        {
            Walls(mb, c, size, col);
            // horizontal siding lines on the front
            var dark = MeshBuilder.Darken(col, 0.85f);
            for (float y = 0.5f; y < size.y; y += 0.5f)
                mb.Box(c + new Vector3(0, y, -size.z / 2 - 0.02f), new Vector3(size.x, 0.05f, 0.03f), dark);
        }

        static void LogWalls(MeshBuilder mb, Vector3 c, Vector3 size)
        {
            mb.Matte();
            int rows = Mathf.RoundToInt(size.y / 0.42f);
            for (int r = 0; r < rows; r++)
            {
                float y = c.y + 0.21f + r * 0.42f;
                Color log = r % 2 == 0 ? Palette.Wood : MeshBuilder.Darken(Palette.Wood, 0.88f);
                mb.Cylinder(new Vector3(c.x - size.x / 2 - 0.3f, y, c.z - size.z / 2), new Vector3(c.x + size.x / 2 + 0.3f, y, c.z - size.z / 2), 0.22f, 6, log);
                mb.Cylinder(new Vector3(c.x - size.x / 2 - 0.3f, y, c.z + size.z / 2), new Vector3(c.x + size.x / 2 + 0.3f, y, c.z + size.z / 2), 0.22f, 6, log);
                mb.Cylinder(new Vector3(c.x - size.x / 2, y + 0.21f, c.z - size.z / 2 - 0.3f), new Vector3(c.x - size.x / 2, y + 0.21f, c.z + size.z / 2 + 0.3f), 0.22f, 6, log);
                mb.Cylinder(new Vector3(c.x + size.x / 2, y + 0.21f, c.z - size.z / 2 - 0.3f), new Vector3(c.x + size.x / 2, y + 0.21f, c.z + size.z / 2 + 0.3f), 0.22f, 6, log);
            }
            mb.Box(c + Vector3.up * size.y / 2, size - new Vector3(0.2f, 0, 0.2f), Palette.WoodDark);
        }

        static void Win(MeshBuilder mb, float x, float y, float z, float w = 1f, float h = 1.4f)
        {
            Station.Window(mb, new Vector3(x, y, z), -1, w, h);
        }

        static void Door(MeshBuilder mb, float x, float z, Color col, float w = 1.1f, float h = 2.2f, float y0 = 0.5f)
        {
            mb.Matte();
            mb.Box(new Vector3(x, y0 + h / 2, z - 0.04f), new Vector3(w + 0.25f, h + 0.15f, 0.1f), Palette.WoodDark);
            mb.Box(new Vector3(x, y0 + h / 2, z - 0.09f), new Vector3(w, h, 0.06f), col);
            mb.Mat(0.7f, 0.8f);
            mb.Box(new Vector3(x + w * 0.32f, y0 + h * 0.48f, z - 0.14f), new Vector3(0.08f, 0.08f, 0.06f), Palette.Brass);
            mb.Matte();
        }

        static void Porch(MeshBuilder mb, float w, float zFront, float depth, float h, Color post, Color roof)
        {
            mb.Matte();
            mb.Box(new Vector3(0, 0.3f, zFront - depth / 2), new Vector3(w, 0.2f, depth), Palette.WoodPale, Palette.Wood);
            for (float x = -w / 2 + 0.2f; x <= w / 2; x += w / 3f - 0.13f)
                mb.Box(new Vector3(x, 0.4f + h / 2, zFront - depth + 0.15f), new Vector3(0.16f, h, 0.16f), post);
            mb.Push().Translate(0, h + 0.4f, zFront - depth / 2).Rotate(Quaternion.Euler(10, 0, 0));
            mb.Box(Vector3.zero, new Vector3(w + 0.4f, 0.14f, depth + 0.4f), roof);
            mb.Pop();
            // railing
            mb.Box(new Vector3(-w / 4 - 0.5f, 1.2f, zFront - depth + 0.15f), new Vector3(w / 2 - 1.2f, 0.08f, 0.08f), post);
            mb.Box(new Vector3(w / 4 + 0.5f, 1.2f, zFront - depth + 0.15f), new Vector3(w / 2 - 1.2f, 0.08f, 0.08f), post);
        }

        static void Chimney(BuildingModel m, Vector3 basePos, float h, Color col, bool smoke = true)
        {
            m.Body.Matte();
            m.Body.Box(basePos + Vector3.up * h / 2, new Vector3(0.8f, h, 0.8f), col);
            m.Body.Box(basePos + Vector3.up * (h + 0.1f), new Vector3(1.0f, 0.2f, 1.0f), MeshBuilder.Darken(col, 0.8f));
            if (smoke) m.Smoke.Add(basePos + Vector3.up * (h + 0.3f));
        }

        static void Sign(MeshBuilder mb, Vector3 p, float w, float h, Color board, Color text)
        {
            mb.Matte();
            mb.Box(p, new Vector3(w, h, 0.12f), board);
            // stylised lettering: little blocks
            int letters = Mathf.Max(3, Mathf.RoundToInt(w / 0.55f) - 1);
            float lw = (w - 0.5f) / letters;
            for (int i = 0; i < letters; i++)
            {
                float lh = h * (0.35f + 0.2f * R());
                mb.Box(p + new Vector3(-w / 2 + 0.25f + lw * (i + 0.5f), 0, -0.07f), new Vector3(lw * 0.6f, lh, 0.03f), text);
            }
        }

        static void FalseFront(MeshBuilder mb, float w, float h, float z, Color col, Color trim)
        {
            mb.Matte();
            mb.Box(new Vector3(0, h / 2, z), new Vector3(w, h, 0.25f), col);
            mb.Box(new Vector3(0, h + 0.15f, z), new Vector3(w + 0.4f, 0.3f, 0.45f), trim);
            // stepped top
            mb.Box(new Vector3(0, h + 0.7f, z), new Vector3(w * 0.45f, 0.9f, 0.25f), col);
            mb.Box(new Vector3(0, h + 1.2f, z), new Vector3(w * 0.5f, 0.2f, 0.4f), trim);
        }

        static void Flag(MeshBuilder mb, Vector3 p, float h, Color c)
        {
            mb.Mat(0.6f, 0.6f);
            mb.Cylinder(p, p + Vector3.up * h, 0.06f, 5, Palette.Steel);
            mb.Matte();
            mb.Box(p + new Vector3(0.65f, h - 0.45f, 0), new Vector3(1.2f, 0.7f, 0.03f), c);
        }

        static void Fence(MeshBuilder mb, Vector3 a, Vector3 b, Color c)
        {
            mb.Matte();
            float len = Vector3.Distance(a, b);
            int posts = Mathf.Max(2, Mathf.RoundToInt(len / 1.5f) + 1);
            for (int i = 0; i < posts; i++)
            {
                var p = Vector3.Lerp(a, b, i / (float)(posts - 1));
                mb.Box(p + Vector3.up * 0.5f, new Vector3(0.12f, 1f, 0.12f), c);
            }
            var dir = (b - a).normalized;
            mb.Push().Translate((a + b) / 2).Rotate(Quaternion.LookRotation(dir));
            mb.Box(new Vector3(0, 0.75f, 0), new Vector3(0.06f, 0.1f, len), c);
            mb.Box(new Vector3(0, 0.4f, 0), new Vector3(0.06f, 0.1f, len), c);
            mb.Pop();
        }

        static void Log(MeshBuilder mb, Vector3 a, Vector3 b, float r)
        {
            mb.Matte();
            mb.Cylinder(a, b, r, 7, Palette.Bark);
        }

        // ================================================================ residential

        static void Cabin(BuildingModel m)
        {
            var mb = m.Body;
            Foundation(mb, 7.4f, 5.6f, 0.4f);
            LogWalls(mb, new Vector3(0, 0.4f, 0.5f), new Vector3(6.4f, 2.9f, 4.6f));
            mb.GableRoof(new Vector3(0, 3.5f, 0.5f), new Vector3(6.4f, 2.2f, 4.6f), Palette.RoofShingle, Palette.Wood, 0.5f);
            Door(mb, -1.2f, -1.82f, Palette.WoodDark);
            Win(mb, 1.4f, 1.9f, -1.86f, 1f, 1.1f);
            Chimney(m, new Vector3(2.6f, 0, 1.6f), 5.8f, Palette.StoneDark);
            Porch(mb, 6.6f, -1.8f, 1.8f, 2.3f, Palette.WoodDark, Palette.RoofShingle);
            // woodpile
            for (int i = 0; i < 6; i++) Log(mb, new Vector3(-4.3f, 0.2f + (i / 3) * 0.36f, -1 + (i % 3) * 0.38f), new Vector3(-3.6f, 0.2f + (i / 3) * 0.36f, -1 + (i % 3) * 0.38f), 0.18f);
            Fence(mb, new Vector3(-5.5f, 0, -5.5f), new Vector3(-1.5f, 0, -5.5f), Palette.Weathered);
            Fence(mb, new Vector3(1.5f, 0, -5.5f), new Vector3(5.5f, 0, -5.5f), Palette.Weathered);
            m.Height = 6;
        }

        static void Clapboard(BuildingModel m)
        {
            var mb = m.Body;
            var paint = Pick(HousePaints);
            Foundation(mb, 8.4f, 6.6f);
            Clapboards(mb, new Vector3(0, 0.5f, 0.5f), new Vector3(7.6f, 5.4f, 5.8f), paint);
            mb.GableRoof(new Vector3(0, 5.9f, 0.5f), new Vector3(7.6f, 2.8f, 5.8f), Pick(Palette.RoofShingle, Palette.RoofGreen, Palette.RoofRed), paint, 0.5f);
            Door(mb, 0, -2.4f, Pick(Palette.PaintRed, Palette.PaintBlue, Palette.PaintGreen));
            Win(mb, -2.4f, 2.1f, -2.44f); Win(mb, 2.4f, 2.1f, -2.44f);
            Win(mb, -2.4f, 4.6f, -2.44f); Win(mb, 0, 4.6f, -2.44f); Win(mb, 2.4f, 4.6f, -2.44f);
            Porch(mb, 7.8f, -2.4f, 2f, 2.4f, Palette.White, Palette.RoofTin);
            Chimney(m, new Vector3(-2.6f, 0, 2.2f), 8.8f, Palette.Brick);
            Fence(mb, new Vector3(-5.6f, 0, -5.6f), new Vector3(-1.2f, 0, -5.6f), Palette.White);
            Fence(mb, new Vector3(1.2f, 0, -5.6f), new Vector3(5.6f, 0, -5.6f), Palette.White);
            m.Height = 8.7f;
        }

        static void Boarding(BuildingModel m)
        {
            var mb = m.Body;
            var paint = Pick(Palette.Hex(0xC8A27A), Palette.Hex(0x9FB3A6), Palette.Hex(0xD9C49A));
            Foundation(mb, 10.4f, 8.4f);
            Clapboards(mb, new Vector3(0, 0.5f, 0.6f), new Vector3(9.6f, 7.6f, 7.4f), paint);
            mb.GableRoof(new Vector3(0, 8.1f, 0.6f), new Vector3(9.6f, 2.4f, 7.4f), Palette.RoofShingle, paint, 0.3f);
            FalseFront(mb, 10f, 9.6f, -3.2f, paint, Palette.WoodDark);
            for (int f = 0; f < 3; f++)
                for (int i = -1; i <= 1; i++)
                    if (!(f == 0 && i == 0)) Win(mb, i * 3f, 2f + f * 2.6f, -3.35f);
            Door(mb, 0, -3.3f, Palette.PaintGreen, 1.4f);
            // balcony on 2nd floor
            mb.Box(new Vector3(0, 3.6f, -4.1f), new Vector3(9.6f, 0.18f, 1.6f), Palette.Wood);
            for (float x = -4.6f; x <= 4.7f; x += 2.3f) mb.Box(new Vector3(x, 1.8f, -4.8f), new Vector3(0.16f, 3.6f, 0.16f), Palette.WoodDark);
            mb.Box(new Vector3(0, 4.6f, -4.85f), new Vector3(9.6f, 0.1f, 0.1f), Palette.WoodDark);
            Sign(mb, new Vector3(0, 9.2f, -3.4f), 6, 1, Palette.Cream, Palette.WoodDark);
            Chimney(m, new Vector3(3.6f, 0, 3f), 10.5f, Palette.Brick);
            m.Height = 11;
        }

        static void Rowhouse(BuildingModel m)
        {
            var mb = m.Body;
            Foundation(mb, 11.6f, 8.2f);
            for (int u = -1; u <= 1; u++)
            {
                var brick = u == 0 ? Palette.BrickDark : Palette.Brick;
                float x = u * 3.7f;
                mb.Matte();
                mb.Box(new Vector3(x, 0.5f + 4.5f, 0.5f), new Vector3(3.7f, 9f, 7f), brick);
                mb.Box(new Vector3(x, 9.7f, -3.05f), new Vector3(3.8f, 0.5f, 0.3f), Palette.Stone); // cornice
                for (int f = 0; f < 3; f++)
                {
                    Win(mb, x - 0.8f, 2.2f + f * 2.8f, -3.02f, 0.9f, 1.6f);
                    Win(mb, x + 0.8f, 2.2f + f * 2.8f, -3.02f, 0.9f, 1.6f);
                    mb.Box(new Vector3(x - 0.8f, 3.15f + f * 2.8f, -3.1f), new Vector3(1.1f, 0.15f, 0.15f), Palette.Stone);
                    mb.Box(new Vector3(x + 0.8f, 3.15f + f * 2.8f, -3.1f), new Vector3(1.1f, 0.15f, 0.15f), Palette.Stone);
                }
                // stoop
                mb.Box(new Vector3(x, 0.4f, -3.6f), new Vector3(1.6f, 0.8f, 1.2f), Palette.Stone);
                Chimney(m, new Vector3(x + 1.2f, 9.5f, 2.6f), 1.8f, Palette.BrickDark, u == 0);
            }
            mb.Box(new Vector3(0, 9.6f, 0.5f), new Vector3(11, 0.2f, 7), Palette.RoofTin);
            m.Height = 11;
        }

        static void Hotel(BuildingModel m)
        {
            var mb = m.Body;
            Foundation(mb, 11.8f, 11f, 0.8f);
            mb.Matte();
            mb.Box(new Vector3(0, 0.8f + 2f, 0.4f), new Vector3(11f, 4f, 10f), Palette.Stone);
            mb.Box(new Vector3(0, 4.8f + 4.5f, 0.4f), new Vector3(11f, 9f, 10f), Palette.Brick);
            for (int f = 0; f < 3; f++)
                for (int i = -2; i <= 2; i++)
                    Win(mb, i * 2.1f, 6.4f + f * 2.8f, -4.62f, 1f, 1.7f);
            for (int i = -2; i <= 2; i++) if (i != 0) Win(mb, i * 2.1f, 2.6f, -4.62f, 1.4f, 2.2f);
            Door(mb, 0, -4.6f, Palette.Hex(0x3A2418), 2f, 2.8f, 0.8f);
            // Mansard roof
            mb.HipRoof(new Vector3(0, 13.8f, 0.4f), new Vector3(11, 3.2f, 10), Palette.Hex(0x3F4A52), 0.3f, 0.75f);
            // Awning
            mb.Push().Translate(0, 4.4f, -5.6f).Rotate(Quaternion.Euler(14, 0, 0));
            mb.Box(Vector3.zero, new Vector3(6, 0.12f, 2.2f), Palette.PaintRed);
            mb.Pop();
            // Corner turret
            mb.Matte();
            mb.Cylinder(new Vector3(4.8f, 0.8f, -4.0f), new Vector3(4.8f, 15.5f, -4.0f), 1.5f, 12, Palette.BrickDark);
            mb.Cylinder(new Vector3(4.8f, 15.5f, -4.0f), new Vector3(4.8f, 19.5f, -4.0f), 1.9f, 0, 12, Palette.Hex(0x3F4A52));
            mb.Mat(0.7f, 0.8f);
            mb.Sphere(new Vector3(4.8f, 19.7f, -4.0f), Vector3.one * 0.3f, 8, Palette.Brass);
            Sign(mb, new Vector3(-1.5f, 13.6f, -4.7f), 6, 1.1f, Palette.Black, Palette.Brass);
            Chimney(m, new Vector3(-4, 14, 3), 2.5f, Palette.BrickDark);
            m.Height = 19;
        }

        // ================================================================ production

        static void Sawmill(BuildingModel m)
        {
            var mb = m.Body;
            Foundation(mb, 11, 8, 0.3f);
            // Open-sided shed
            for (float x = -5; x <= 5; x += 2.5f)
                for (int z = -1; z <= 1; z += 2)
                    mb.Box(new Vector3(x, 2.3f, z * 3.4f + 0.5f), new Vector3(0.3f, 4.2f, 0.3f), Palette.WoodDark);
            mb.GableRoof(new Vector3(0, 4.4f, 0.5f), new Vector3(10.4f, 2.2f, 6.8f), Palette.RoofTin, Palette.Weathered, 0.6f);
            // Back wall
            mb.Box(new Vector3(0, 2.3f, 3.9f), new Vector3(10.4f, 4.2f, 0.2f), Palette.Weathered);
            // Saw table
            mb.Box(new Vector3(0, 1.0f, 0), new Vector3(7, 0.3f, 1.4f), Palette.Wood);
            Log(mb, new Vector3(-3, 1.45f, 0), new Vector3(0.5f, 1.45f, 0), 0.32f);
            // Log pile and planks
            for (int i = 0; i < 9; i++)
            {
                int row = i < 4 ? 0 : i < 7 ? 1 : 2;
                float off = (i - (row == 0 ? 0 : row == 1 ? 4 : 7)) * 0.62f + row * 0.31f;
                Log(mb, new Vector3(-5.5f, 0.32f + row * 0.55f, -5.2f + off), new Vector3(-0.8f, 0.32f + row * 0.55f, -5.2f + off), 0.3f);
            }
            for (int i = 0; i < 5; i++) mb.Box(new Vector3(3.5f, 0.15f + i * 0.2f, -4.5f), new Vector3(4, 0.16f, 1.6f), i % 2 == 0 ? Palette.WoodPale : MeshBuilder.Darken(Palette.WoodPale, 0.92f));
            // Boiler stack
            mb.Mat(0.4f, 0.5f);
            mb.Cylinder(new Vector3(4.6f, 0.3f, 2.8f), new Vector3(4.6f, 8f, 2.8f), 0.45f, 8, Palette.Iron);
            m.Smoke.Add(new Vector3(4.6f, 8.2f, 2.8f));
            // Spinning blade
            var blade = new SpinPart { Position = new Vector3(0.9f, 1.45f, 0), Speed = new Vector3(0, 0, -720) };
            blade.Mesh.Mat(0.8f, 0.9f);
            blade.Mesh.Cylinder(new Vector3(0, 0, -0.03f), new Vector3(0, 0, 0.03f), 0.9f, 18, Palette.Hex(0xC9CED3));
            for (int t = 0; t < 12; t++)
            {
                blade.Mesh.Push().Rotate(Quaternion.Euler(0, 0, t * 30)).Translate(0, 0.92f, 0);
                blade.Mesh.Box(Vector3.zero, new Vector3(0.12f, 0.12f, 0.05f), Palette.Steel);
                blade.Mesh.Pop();
            }
            m.Spins.Add(blade);
            m.Height = 8;
        }

        static void Smelter(BuildingModel m)
        {
            var mb = m.Body;
            Foundation(mb, 11, 9, 0.4f);
            mb.Matte();
            mb.Box(new Vector3(-1, 0.4f + 3, 0.8f), new Vector3(8, 6, 7), Palette.BrickDark);
            mb.GableRoof(new Vector3(-1, 6.4f, 0.8f), new Vector3(8, 2, 7), Palette.RoofTin, Palette.BrickDark, 0.4f);
            // Furnace mouth (glows)
            mb.Box(new Vector3(-1, 1.6f, -2.75f), new Vector3(3, 2.4f, 0.2f), Palette.Black);
            mb.Lit();
            mb.Box(new Vector3(-1, 1.4f, -2.86f), new Vector3(2.2f, 1.5f, 0.05f), Palette.Hex(0xFF7A2A));
            mb.Matte();
            // Tall stack
            mb.Cylinder(new Vector3(4, 0.4f, 2.8f), new Vector3(4, 15f, 2.8f), 1.1f, 0.8f, 10, Palette.Brick);
            mb.Cylinder(new Vector3(4, 14.6f, 2.8f), new Vector3(4, 15.2f, 2.8f), 1.0f, 10, Palette.Iron);
            m.Smoke.Add(new Vector3(4, 15.4f, 2.8f));
            // Ore & coal heaps
            mb.Sphere(new Vector3(3.5f, 0.2f, -3.8f), new Vector3(1.8f, 1.1f, 1.5f), 7, Palette.Hex(0x8E4B3A), false);
            mb.Sphere(new Vector3(-4.2f, 0.2f, -4.3f), new Vector3(1.6f, 1f, 1.3f), 7, Palette.Black, false);
            // Ingot stack
            mb.Mat(0.6f, 0.8f);
            for (int i = 0; i < 6; i++) mb.Box(new Vector3(0.8f + (i % 3) * 0.7f, 0.5f + (i / 3) * 0.3f, -4.5f), new Vector3(0.6f, 0.25f, 1.2f), Palette.Steel);
            m.Height = 15;
        }

        static void GristMill(BuildingModel m)
        {
            var mb = m.Body;
            Foundation(mb, 9, 9, 0.5f);
            mb.Matte();
            // Octagonal tapered tower
            var poly = new List<Vector2>();
            for (int i = 0; i < 8; i++) { float a = i / 8f * Mathf.PI * 2; poly.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 3.6f); }
            mb.Prism(poly, 0.5f, 9.5f, Palette.Cream, Palette.Cream, 0.3f);
            mb.Cylinder(new Vector3(0, 9.4f, 0), new Vector3(0, 12.4f, 0), 2.7f, 0.3f, 8, Palette.RoofShingle);
            Door(mb, 0, -3.4f, Palette.PaintGreen);
            Win(mb, 0, 5f, -3.0f, 0.8f, 1.1f);
            // Sacks
            for (int i = 0; i < 5; i++) mb.Sphere(new Vector3(-3.6f + i * 0.7f, 0.85f, -4.6f), new Vector3(0.35f, 0.4f, 0.3f), 6, Palette.Hex(0xE5D9B8), false);
            var sails = new SpinPart { Position = new Vector3(0, 10.2f, -3.2f), Speed = new Vector3(0, 0, 45) };
            sails.Mesh.Matte();
            sails.Mesh.Cylinder(new Vector3(0, 0, -0.2f), new Vector3(0, 0, 0.8f), 0.3f, 8, Palette.WoodDark);
            for (int s = 0; s < 4; s++)
            {
                sails.Mesh.Push().Rotate(Quaternion.Euler(0, 0, s * 90));
                sails.Mesh.Box(new Vector3(0, 3.2f, -0.25f), new Vector3(0.2f, 6.4f, 0.12f), Palette.WoodDark);
                sails.Mesh.Box(new Vector3(0.6f, 3.6f, -0.3f), new Vector3(1.1f, 5f, 0.05f), Palette.Hex(0xEDE3CF));
                sails.Mesh.Pop();
            }
            m.Spins.Add(sails);
            m.Height = 13;
        }

        static void Packing(BuildingModel m)
        {
            var mb = m.Body;
            Foundation(mb, 11, 6.5f, 0.4f);
            Clapboards(mb, new Vector3(0, 0.4f, 2.2f), new Vector3(10, 4.6f, 5.8f), Palette.PaintRed);
            mb.GableRoof(new Vector3(0, 5f, 2.2f), new Vector3(10, 2.6f, 5.8f), Palette.RoofTin, Palette.PaintRed, 0.4f);
            mb.Box(new Vector3(0, 2.3f, -0.72f), new Vector3(3, 3.6f, 0.1f), Palette.White);
            mb.Box(new Vector3(0, 2.3f, -0.78f), new Vector3(2.6f, 3.2f, 0.06f), Palette.Hex(0x6E2018));
            // Corral
            Fence(mb, new Vector3(-5.6f, 0, -5.6f), new Vector3(5.6f, 0, -5.6f), Palette.Weathered);
            Fence(mb, new Vector3(-5.6f, 0, -5.6f), new Vector3(-5.6f, 0, -1.2f), Palette.Weathered);
            Fence(mb, new Vector3(5.6f, 0, -5.6f), new Vector3(5.6f, 0, -1.2f), Palette.Weathered);
            for (int i = 0; i < 4; i++) Cow(mb, new Vector3(-4 + i * 2.4f + R() * 0.6f, 0, -3.6f + R()), R() * 360);
            Chimney(m, new Vector3(3.5f, 4.5f, 3.8f), 2.6f, Palette.Brick);
            m.Height = 8;
        }

        static void Cow(MeshBuilder mb, Vector3 p, float rot)
        {
            mb.Push().Translate(p).RotateY(rot);
            mb.Matte();
            var hide = Pick(Palette.Hex(0x7A4A2A), Palette.Hex(0x3A2A20), Palette.Hex(0xC9A27A));
            mb.Box(new Vector3(0, 1.0f, 0), new Vector3(0.7f, 0.7f, 1.6f), hide);
            mb.Box(new Vector3(0, 1.15f, 0.95f), new Vector3(0.45f, 0.45f, 0.55f), hide);
            mb.Box(new Vector3(0, 1.35f, 1.0f), new Vector3(1.1f, 0.08f, 0.08f), Palette.Cream); // longhorns
            for (int i = 0; i < 4; i++) mb.Box(new Vector3(i < 2 ? -0.25f : 0.25f, 0.33f, i % 2 == 0 ? -0.6f : 0.6f), new Vector3(0.14f, 0.66f, 0.14f), MeshBuilder.Darken(hide, 0.8f));
            mb.Pop();
        }

        static void ToolWorks(BuildingModel m)
        {
            var mb = m.Body;
            Foundation(mb, 11, 9, 0.4f);
            mb.Matte();
            mb.Box(new Vector3(0, 0.4f + 2.6f, 0.6f), new Vector3(10, 5.2f, 8), Palette.Hex(0x7A6A58));
            // Sawtooth roof
            for (int i = 0; i < 4; i++)
            {
                float x = -3.75f + i * 2.5f;
                mb.Push().Translate(x, 5.6f, 0.6f);
                mb.Quad(new Vector3(-1.25f, 0, -4), new Vector3(1.25f, 1.6f, -4), new Vector3(1.25f, 1.6f, 4), new Vector3(-1.25f, 0, 4), Palette.RoofTin);
                mb.Lit();
                mb.Quad(new Vector3(1.25f, 0, 4), new Vector3(1.25f, 1.6f, 4), new Vector3(1.25f, 1.6f, -4), new Vector3(1.25f, 0, -4), Palette.Hex(0xBFD3D8));
                mb.Matte();
                mb.Pop();
            }
            for (int i = -1; i <= 1; i++) Win(mb, i * 3f, 2.6f, -3.42f, 1.8f, 1.6f);
            Door(mb, -4f, -3.4f, Palette.PaintBlue, 1.4f, 2.6f, 0.4f);
            Chimney(m, new Vector3(4.2f, 0.4f, 3.8f), 10, Palette.Brick);
            var gear = new SpinPart { Position = new Vector3(0, 5f, -3.6f), Speed = new Vector3(0, 0, 40) };
            gear.Mesh.Mat(0.6f, 0.8f);
            gear.Mesh.Cylinder(new Vector3(0, 0, -0.05f), new Vector3(0, 0, 0.05f), 0.8f, 16, Palette.Brass);
            for (int t = 0; t < 10; t++)
            {
                gear.Mesh.Push().Rotate(Quaternion.Euler(0, 0, t * 36)).Translate(0, 0.9f, 0);
                gear.Mesh.Box(Vector3.zero, new Vector3(0.3f, 0.3f, 0.1f), Palette.Brass);
                gear.Mesh.Pop();
            }
            m.Spins.Add(gear);
            m.Height = 10;
        }

        static void WireWorks(BuildingModel m)
        {
            var mb = m.Body;
            Foundation(mb, 11, 9, 0.4f);
            mb.Matte();
            mb.Box(new Vector3(-1.2f, 0.4f + 3, 1), new Vector3(8, 6, 7), Palette.Hex(0x8C6A54));
            mb.GableRoof(new Vector3(-1.2f, 6.4f, 1), new Vector3(8, 2.4f, 7), Palette.RoofGreen, Palette.Hex(0x8C6A54), 0.4f);
            for (int i = -1; i <= 1; i++) Win(mb, -1.2f + i * 2.4f, 3, -2.52f, 1.2f, 1.8f);
            // Copper spools
            for (int i = 0; i < 3; i++)
            {
                var c = new Vector3(3.6f, 1.0f, -3.5f + i * 2.2f);
                mb.Matte();
                mb.Cylinder(c + new Vector3(-0.7f, 0, 0), c + new Vector3(-0.55f, 0, 0), 1.0f, 12, Palette.Wood);
                mb.Cylinder(c + new Vector3(0.55f, 0, 0), c + new Vector3(0.7f, 0, 0), 1.0f, 12, Palette.Wood);
                mb.Mat(0.7f, 0.9f);
                mb.Cylinder(c + new Vector3(-0.55f, 0, 0), c + new Vector3(0.55f, 0, 0), 0.75f, 12, Palette.Copper);
            }
            Chimney(m, new Vector3(-4.5f, 0.4f, 3.6f), 9, Palette.Brick);
            m.Height = 9;
        }

        static void Assay(BuildingModel m)
        {
            var mb = m.Body;
            Foundation(mb, 9, 8, 0.6f);
            mb.Matte();
            mb.Box(new Vector3(0, 0.6f + 2.6f, 0.5f), new Vector3(8, 5.2f, 7), Palette.Stone);
            mb.Box(new Vector3(0, 6, 0.5f), new Vector3(8.4f, 0.6f, 7.4f), Palette.StoneDark);
            Door(mb, 0, -3.02f, Palette.Hex(0x2A1A10), 1.4f, 2.6f, 0.6f);
            Win(mb, -2.6f, 2.8f, -3.02f, 1.2f, 1.8f); Win(mb, 2.6f, 2.8f, -3.02f, 1.2f, 1.8f);
            mb.Mat(0.8f, 0.9f);
            Sign(mb, new Vector3(0, 5.2f, -3.1f), 5, 0.8f, Palette.Black, Palette.Brass);
            mb.Matte();
            mb.Cylinder(new Vector3(3, 6.3f, 2.6f), new Vector3(3, 9f, 2.6f), 0.4f, 8, Palette.Brick);
            m.Smoke.Add(new Vector3(3, 9.2f, 2.6f));
            // bars of gold on a cart
            mb.Box(new Vector3(-3.5f, 0.6f, -4.5f), new Vector3(1.6f, 0.3f, 1.2f), Palette.Wood);
            mb.Mat(0.85f, 1f);
            for (int i = 0; i < 4; i++) mb.Box(new Vector3(-3.9f + (i % 2) * 0.8f, 0.9f + (i / 2) * 0.22f, -4.5f), new Vector3(0.6f, 0.2f, 0.9f), Palette.Hex(0xFFD34D));
            m.Height = 9;
        }

        static void Refinery(BuildingModel m)
        {
            var mb = m.Body;
            Foundation(mb, 11.5f, 11.5f, 0.3f);
            mb.Mat(0.5f, 0.5f);
            // storage tanks
            mb.Cylinder(new Vector3(-3, 0.3f, 2.5f), new Vector3(-3, 5f, 2.5f), 2.4f, 18, Palette.Hex(0xD9D4C8));
            mb.Cylinder(new Vector3(-3, 5f, 2.5f), new Vector3(-3, 5.6f, 2.5f), 2.4f, 0.3f, 18, Palette.Hex(0xBFB9AC));
            mb.Cylinder(new Vector3(2.8f, 0.3f, 3.2f), new Vector3(2.8f, 4f, 3.2f), 1.8f, 16, Palette.Hex(0xA8382B));
            // cracking tower
            mb.Cylinder(new Vector3(2.5f, 0.3f, -2.5f), new Vector3(2.5f, 14f, -2.5f), 0.9f, 0.7f, 12, Palette.Steel);
            for (float y = 3; y < 14; y += 2.5f) mb.Cylinder(new Vector3(2.5f, y, -2.5f), new Vector3(2.5f, y + 0.25f, -2.5f), 1.15f, 12, Palette.Iron);
            // pipes
            mb.Cylinder(new Vector3(-3, 2, 0.1f), new Vector3(2.5f, 2, -1.6f), 0.18f, 6, Palette.Iron);
            mb.Cylinder(new Vector3(-0.6f, 4, 3.2f), new Vector3(2.5f, 9, -2.2f), 0.14f, 6, Palette.Iron);
            mb.Matte();
            mb.Box(new Vector3(-3.5f, 1.6f, -3.5f), new Vector3(3.6f, 2.6f, 3f), Palette.Hex(0x6B6359));
            Win(mb, -3.5f, 1.8f, -5.02f, 1.2f, 1f);
            m.Smoke.Add(new Vector3(2.5f, 14.3f, -2.5f));
            m.Height = 14;
        }

        // ================================================================ civic

        static void WaterTower(BuildingModel m)
        {
            var mb = m.Body;
            mb.Matte();
            for (int i = 0; i < 4; i++)
            {
                float a = (i * 90 + 45) * Mathf.Deg2Rad;
                var foot = new Vector3(Mathf.Cos(a) * 2.8f, 0, Mathf.Sin(a) * 2.8f);
                mb.Box(foot + Vector3.up * 0.2f, new Vector3(0.8f, 0.4f, 0.8f), Palette.Stone);
                mb.Cylinder(foot, new Vector3(foot.x * 0.8f, 7, foot.z * 0.8f), 0.22f, 5, Palette.WoodDark);
            }
            mb.Box(new Vector3(0, 3.5f, 0), new Vector3(4.4f, 0.15f, 0.15f), Palette.Wood);
            mb.Box(new Vector3(0, 3.5f, 0), new Vector3(0.15f, 0.15f, 4.4f), Palette.Wood);
            mb.Cylinder(new Vector3(0, 7, 0), new Vector3(0, 11.5f, 0), 3f, 16, Palette.Wood);
            for (float y = 7.6f; y < 11.5f; y += 1.3f) { mb.Mat(0.4f, 0.6f); mb.Cylinder(new Vector3(0, y, 0), new Vector3(0, y + 0.12f, 0), 3.05f, 16, Palette.Iron, false); }
            mb.Matte();
            mb.Cylinder(new Vector3(0, 11.5f, 0), new Vector3(0, 13.6f, 0), 3.3f, 0.2f, 16, Palette.RoofShingle);
            mb.Cylinder(new Vector3(0, 8f, -2.9f), new Vector3(0, 6.5f, -4.8f), 0.25f, 6, Palette.Iron);
            m.Height = 13.5f;
        }

        static void Saloon(BuildingModel m)
        {
            var mb = m.Body;
            var paint = Pick(Palette.Hex(0xA8763E), Palette.Hex(0x8F5B3A), Palette.Hex(0xB98A5A));
            Foundation(mb, 10.6f, 9.4f, 0.5f);
            Clapboards(mb, new Vector3(0, 0.5f, 0.8f), new Vector3(10, 6.8f, 8), paint);
            mb.GableRoof(new Vector3(0, 7.3f, 0.8f), new Vector3(10, 2f, 8), Palette.RoofShingle, paint, 0.2f);
            FalseFront(mb, 10.4f, 9f, -3.25f, paint, Palette.WoodDark);
            Sign(mb, new Vector3(0, 8.2f, -3.45f), 7, 1.4f, Palette.Hex(0x3A2418), Palette.Brass);
            // Batwing doors
            mb.Box(new Vector3(-0.4f, 1.8f, -3.45f), new Vector3(0.75f, 1.3f, 0.08f), Palette.WoodPale);
            mb.Box(new Vector3(0.4f, 1.8f, -3.45f), new Vector3(0.75f, 1.3f, 0.08f), Palette.WoodPale);
            mb.Lit();
            mb.Box(new Vector3(0, 1.6f, -3.38f), new Vector3(1.6f, 2.4f, 0.04f), Palette.Window);
            mb.Matte();
            Win(mb, -3, 1.9f, -3.42f, 1.8f, 1.6f); Win(mb, 3, 1.9f, -3.42f, 1.8f, 1.6f);
            Win(mb, -3, 5.0f, -3.42f); Win(mb, 0, 5.0f, -3.42f); Win(mb, 3, 5.0f, -3.42f);
            // Balcony
            mb.Box(new Vector3(0, 3.8f, -4.4f), new Vector3(10.4f, 0.2f, 2f), Palette.Wood);
            for (float x = -5f; x <= 5.1f; x += 2.5f) mb.Box(new Vector3(x, 1.9f, -5.2f), new Vector3(0.18f, 3.8f, 0.18f), Palette.WoodDark);
            for (float x = -4.8f; x <= 4.8f; x += 0.5f) mb.Box(new Vector3(x, 4.35f, -5.3f), new Vector3(0.06f, 0.9f, 0.06f), Palette.WoodPale);
            mb.Box(new Vector3(0, 4.85f, -5.3f), new Vector3(10.4f, 0.12f, 0.12f), Palette.WoodDark);
            // Hitching post + barrels
            mb.Box(new Vector3(-3, 0.9f, -6.2f), new Vector3(3, 0.1f, 0.1f), Palette.WoodDark);
            mb.Box(new Vector3(-4.4f, 0.5f, -6.2f), new Vector3(0.15f, 1f, 0.15f), Palette.WoodDark);
            mb.Box(new Vector3(-1.6f, 0.5f, -6.2f), new Vector3(0.15f, 1f, 0.15f), Palette.WoodDark);
            Station.Barrel(mb, new Vector3(4, 0, -6.2f));
            m.Height = 10;
        }

        static void School(BuildingModel m)
        {
            var mb = m.Body;
            Foundation(mb, 9, 7, 0.6f);
            Clapboards(mb, new Vector3(0, 0.6f, 0.5f), new Vector3(8.4f, 4.4f, 6.4f), Palette.PaintRed);
            mb.GableRoof(new Vector3(0, 5f, 0.5f), new Vector3(8.4f, 2.8f, 6.4f), Palette.RoofShingle, Palette.White, 0.4f);
            mb.Matte();
            // Trim corners
            mb.Box(new Vector3(-4.2f, 2.8f, -2.7f), new Vector3(0.25f, 4.4f, 0.25f), Palette.White);
            mb.Box(new Vector3(4.2f, 2.8f, -2.7f), new Vector3(0.25f, 4.4f, 0.25f), Palette.White);
            Door(mb, 0, -2.72f, Palette.White, 1.2f, 2.4f, 0.6f);
            Win(mb, -2.6f, 2.7f, -2.76f, 1f, 1.8f); Win(mb, 2.6f, 2.7f, -2.76f, 1f, 1.8f);
            // Bell tower
            mb.Box(new Vector3(0, 8.2f, -1.6f), new Vector3(1.6f, 1.6f, 1.6f), Palette.White);
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90 + 45;
                var off = Quaternion.Euler(0, a, 0) * new Vector3(0.7f, 0, 0);
                mb.Box(new Vector3(off.x, 9.6f, -1.6f + off.z), new Vector3(0.15f, 1.4f, 0.15f), Palette.White);
            }
            mb.Mat(0.7f, 0.8f);
            mb.Cylinder(new Vector3(0, 9.9f, -1.6f), new Vector3(0, 9.3f, -1.6f), 0.15f, 0.42f, 10, Palette.Brass);
            mb.Matte();
            mb.HipRoof(new Vector3(0, 10.3f, -1.6f), new Vector3(1.6f, 1.6f, 1.6f), Palette.RoofShingle, 0.2f, 0f);
            Flag(mb, new Vector3(-4.8f, 0, -4.5f), 7, Palette.PaintBlue);
            m.Height = 12;
        }

        static void Telegraph(BuildingModel m)
        {
            var mb = m.Body;
            Foundation(mb, 7, 6, 0.4f);
            Clapboards(mb, new Vector3(0, 0.4f, 0.6f), new Vector3(6, 4, 5), Palette.Hex(0xD9C49A));
            mb.GableRoof(new Vector3(0, 4.4f, 0.6f), new Vector3(6, 1.8f, 5), Palette.RoofGreen, Palette.Hex(0xD9C49A), 0.4f);
            Door(mb, -1.2f, -1.92f, Palette.PaintGreen);
            Win(mb, 1.4f, 2.2f, -1.96f, 1.4f, 1.4f);
            Sign(mb, new Vector3(0, 3.7f, -2.0f), 5, 0.6f, Palette.Cream, Palette.Black);
            mb.Matte();
            mb.Cylinder(new Vector3(4.5f, 0, 0), new Vector3(4.5f, 10, 0), 0.18f, 6, Palette.WoodDark);
            mb.Box(new Vector3(4.5f, 9.4f, 0), new Vector3(0.2f, 0.18f, 2.4f), Palette.WoodDark);
            mb.Box(new Vector3(4.5f, 8.6f, 0), new Vector3(0.2f, 0.18f, 2.4f), Palette.WoodDark);
            mb.Mat(0.3f, 0.5f);
            mb.Cylinder(new Vector3(4.5f, 9.4f, 1), new Vector3(0, 4.6f, 1), 0.03f, 3, Palette.Iron, false);
            mb.Cylinder(new Vector3(4.5f, 9.4f, -1), new Vector3(15, 7.5f, -1), 0.03f, 3, Palette.Iron, false);
            m.Height = 10;
        }

        static void Bank(BuildingModel m)
        {
            var mb = m.Body;
            Foundation(mb, 11, 10, 0.9f);
            mb.Matte();
            mb.Box(new Vector3(0, 0.9f + 3.6f, 1), new Vector3(10, 7.2f, 8), Palette.Stone);
            // Portico columns
            for (int i = 0; i < 4; i++)
            {
                float x = -3.6f + i * 2.4f;
                mb.Cylinder(new Vector3(x, 0.9f, -4.2f), new Vector3(x, 7.4f, -4.2f), 0.42f, 0.36f, 12, Palette.Cream);
                mb.Box(new Vector3(x, 7.5f, -4.2f), new Vector3(1, 0.3f, 1), Palette.Cream);
            }
            mb.Box(new Vector3(0, 0.6f, -4.4f), new Vector3(10, 0.3f, 1.8f), Palette.Stone);
            mb.Box(new Vector3(0, 7.9f, -3.2f), new Vector3(10.2f, 0.8f, 2.6f), Palette.Stone);
            // Pediment
            mb.Push().Translate(0, 8.3f, -2.9f);
            mb.GableRoof(Vector3.zero, new Vector3(2.6f, 2f, 10.2f), Palette.StoneDark, Palette.Cream, 0.1f);
            mb.Pop();
            Door(mb, 0, -3.02f, Palette.Hex(0x2A1A10), 1.8f, 3.2f, 0.9f);
            Win(mb, -3, 4, -3.02f, 1.2f, 2.4f); Win(mb, 3, 4, -3.02f, 1.2f, 2.4f);
            mb.Box(new Vector3(0, 8.4f, 1), new Vector3(10, 0.2f, 8), Palette.RoofTin);
            Sign(mb, new Vector3(0, 7.9f, -4.55f), 6, 0.6f, Palette.Cream, Palette.Brass);
            m.Height = 11;
        }

        static void ClockTower(BuildingModel m)
        {
            var mb = m.Body;
            Foundation(mb, 7, 7, 0.8f);
            mb.Matte();
            mb.Box(new Vector3(0, 0.8f + 8, 0), new Vector3(5, 16, 5), Palette.Brick);
            mb.Box(new Vector3(0, 17.3f, 0), new Vector3(5.6f, 1, 5.6f), Palette.Stone);
            mb.Box(new Vector3(0, 19.6f, 0), new Vector3(4.6f, 3.6f, 4.6f), Palette.Stone);
            for (int s = 0; s < 4; s++)
            {
                mb.Push().RotateY(s * 90);
                mb.Mat(0.7f, 0.8f);
                mb.Cylinder(new Vector3(0, 19.6f, -2.3f), new Vector3(0, 19.6f, -2.4f), 1.5f, 20, Palette.Brass);
                mb.Lit();
                mb.Cylinder(new Vector3(0, 19.6f, -2.4f), new Vector3(0, 19.6f, -2.45f), 1.3f, 20, Palette.Cream);
                mb.Matte();
                mb.Box(new Vector3(0, 20.0f, -2.5f), new Vector3(0.1f, 0.9f, 0.03f), Palette.Black);
                mb.Box(new Vector3(0.3f, 19.6f, -2.5f), new Vector3(0.7f, 0.1f, 0.03f), Palette.Black);
                for (int f = 0; f < 3; f++) Station.Window(mb, new Vector3(0, 4 + f * 4, -2.52f), -1, 0.9f, 2f);
                mb.Pop();
            }
            mb.Matte();
            mb.HipRoof(new Vector3(0, 21.4f, 0), new Vector3(4.6f, 5.5f, 4.6f), Palette.Hex(0x3F5E6A), 0.4f, 0f);
            Flag(mb, new Vector3(0, 26.6f, 0), 2.4f, Palette.PaintRed);
            Door(mb, 0, -2.52f, Palette.Hex(0x2A1A10), 1.4f, 2.8f, 0.8f);
            m.Height = 28;
        }

        // ================================================================ decoration

        static void LampPost(BuildingModel m)
        {
            var mb = m.Body;
            mb.Matte();
            mb.Box(new Vector3(0, 0.05f, 0), new Vector3(4, 0.1f, 4), Palette.Stone);
            Station.Lamp(mb, new Vector3(0, 0.1f, 0));
            Station.Lamp(mb, new Vector3(-3.5f, 0, -3.5f));
            Station.Lamp(mb, new Vector3(3.5f, 0, -3.5f));
            mb.Box(new Vector3(0, 0.5f, -3.5f), new Vector3(2.2f, 0.12f, 0.6f), Palette.Wood);
            m.Height = 5;
        }

        static void Garden(BuildingModel m)
        {
            var mb = m.Body;
            mb.Matte();
            mb.Box(new Vector3(0, 0.06f, 0), new Vector3(10, 0.12f, 10), Palette.Hex(0x7E9A4A));
            Color[] flowers = { Palette.Hex(0xE04F5F), Palette.Hex(0xF2C14E), Palette.Hex(0xA05EC4), Palette.Hex(0xF5F0E1), Palette.Hex(0xEE7D3B) };
            for (int bed = 0; bed < 4; bed++)
            {
                var c = new Vector3(bed % 2 == 0 ? -2.5f : 2.5f, 0.12f, bed < 2 ? -2.5f : 2.5f);
                mb.Box(c + Vector3.up * 0.15f, new Vector3(3.6f, 0.3f, 3.6f), Palette.Hex(0x5A3A24));
                for (int f = 0; f < 22; f++)
                {
                    var p = c + new Vector3(R() * 3.2f - 1.6f, 0.45f, R() * 3.2f - 1.6f);
                    mb.Sphere(p, Vector3.one * (0.22f + R() * 0.12f), 5, flowers[(bed + f) % flowers.Length], false);
                }
            }
            Fence(mb, new Vector3(-5, 0, -5), new Vector3(-0.8f, 0, -5), Palette.White);
            Fence(mb, new Vector3(0.8f, 0, -5), new Vector3(5, 0, -5), Palette.White);
            Fence(mb, new Vector3(-5, 0, 5), new Vector3(5, 0, 5), Palette.White);
            Fence(mb, new Vector3(-5, 0, -5), new Vector3(-5, 0, 5), Palette.White);
            Fence(mb, new Vector3(5, 0, -5), new Vector3(5, 0, 5), Palette.White);
            m.Height = 2;
        }

        static void Fountain(BuildingModel m)
        {
            var mb = m.Body;
            mb.Matte();
            mb.Box(new Vector3(0, 0.05f, 0), new Vector3(10, 0.1f, 10), Palette.Stone);
            mb.Cylinder(new Vector3(0, 0.1f, 0), new Vector3(0, 1.0f, 0), 3.4f, 24, Palette.StoneDark);
            mb.Mat(0.95f);
            mb.Cylinder(new Vector3(0, 0.9f, 0), new Vector3(0, 0.95f, 0), 3.0f, 24, Palette.Hex(0x4C8A99));
            mb.Matte();
            mb.Cylinder(new Vector3(0, 0.9f, 0), new Vector3(0, 3.2f, 0), 0.4f, 0.3f, 10, Palette.Stone);
            mb.Cylinder(new Vector3(0, 3.0f, 0), new Vector3(0, 3.4f, 0), 1.4f, 0.4f, 16, Palette.Stone);
            mb.Mat(0.95f);
            mb.Cylinder(new Vector3(0, 3.35f, 0), new Vector3(0, 3.4f, 0), 1.1f, 16, Palette.Hex(0x4C8A99));
            mb.Mat(0.8f, 0.9f);
            mb.Sphere(new Vector3(0, 3.9f, 0), Vector3.one * 0.4f, 10, Palette.Brass);
            for (int i = 0; i < 4; i++)
            {
                var p = Quaternion.Euler(0, i * 90 + 45, 0) * new Vector3(4.3f, 0, 0);
                mb.Matte();
                mb.Box(p + Vector3.up * 0.45f, new Vector3(1.8f, 0.12f, 0.6f), Palette.Wood);
            }
            m.Smoke.Add(new Vector3(0, 3.9f, 0)); // used as a spray emitter
            m.Height = 4;
        }

        /// <summary>Brick motor garage with three open bays at the back of the lot; trucks park on the apron in front.</summary>
        static void Garage(BuildingModel m)
        {
            var mb = m.Body;
            mb.Matte();
            // Concrete apron where the trucks stand.
            mb.Surf = SurfaceKind.Stone;
            mb.Box(new Vector3(0, 0.05f, -3.2f), new Vector3(11.6f, 0.1f, 5.4f), Palette.Hex(0x9A948A));
            mb.Surf = SurfaceKind.Auto;
            Foundation(mb, 11.6f, 6.4f, 0.3f);
            const float z0 = -0.4f, depth = 6f, h = 5.2f;
            float zc = z0 + depth / 2;
            mb.Box(new Vector3(0, 0.3f + h / 2, zc), new Vector3(11.4f, h, depth), Palette.Brick);
            // Three bay openings, dark inside, with timber lintels and a roll-up door half open.
            for (int i = -1; i <= 1; i++)
            {
                float x = i * 3.7f;
                mb.Box(new Vector3(x, 0.3f + 1.6f, z0 - 0.02f), new Vector3(2.9f, 3.2f, 0.06f), Palette.Hex(0x1E1A17));
                mb.Box(new Vector3(x, 0.3f + 3.45f, z0 - 0.08f), new Vector3(3.3f, 0.4f, 0.16f), Palette.Stone);
                mb.Box(new Vector3(x, 0.3f + 2.95f, z0 - 0.06f), new Vector3(2.9f, 0.6f, 0.08f), Palette.Hex(0x6E7B6A));
            }
            for (int i = 0; i < 4; i++) mb.Box(new Vector3(-5.55f + i * 3.7f, 0.3f + h / 2, z0 - 0.1f), new Vector3(0.5f, h, 0.2f), Palette.BrickDark);
            // Stepped false front with the sign.
            mb.Box(new Vector3(0, 0.3f + h + 0.6f, z0 + 0.1f), new Vector3(11.4f, 1.2f, 0.3f), Palette.Brick);
            mb.Box(new Vector3(0, 0.3f + h + 1.5f, z0 + 0.1f), new Vector3(6, 0.8f, 0.3f), Palette.Brick);
            mb.Box(new Vector3(0, 0.3f + h + 1.95f, z0 + 0.1f), new Vector3(6.3f, 0.15f, 0.4f), Palette.Stone);
            mb.Box(new Vector3(0, 0.3f + h + 1.25f, z0 + 0.1f), new Vector3(11.7f, 0.15f, 0.4f), Palette.Stone);
            Sign(mb, new Vector3(0, 0.3f + h + 0.75f, z0 - 0.1f), 7.4f, 0.9f, Palette.Cream, Palette.PaintRed);
            mb.Box(new Vector3(0, 0.3f + h + 0.05f, zc + 0.1f), new Vector3(11.2f, 0.16f, depth - 0.2f), Palette.RoofTin);
            // Clerestory windows down the side.
            for (int i = 0; i < 3; i++) mb.Box(new Vector3(5.72f, 3.6f, z0 + 1.2f + i * 1.8f), new Vector3(0.06f, 1.0f, 1.2f), Palette.Window);
            // A gas pump with a glass globe, oil drums and a tyre stack.
            mb.Paint();
            mb.Box(new Vector3(5.3f, 0.1f + 1.1f, -4.9f), new Vector3(0.7f, 2.2f, 0.55f), Palette.PaintRed);
            mb.Mat(0.85f, 0, true);
            mb.Sphere(new Vector3(5.3f, 2.6f, -4.9f), new Vector3(0.36f, 0.42f, 0.36f), 10, Palette.Hex(0xF4EEDC));
            mb.Matte();
            mb.Cylinder(new Vector3(5.0f, 1.6f, -5.2f), new Vector3(4.8f, 0.9f, -5.4f), 0.05f, 4, Palette.Black, false);
            Station.Barrel(mb, new Vector3(-5.4f, 0.1f, -1.4f));
            Station.Barrel(mb, new Vector3(-5.3f, 0.1f, -2.4f));
            for (int i = 0; i < 3; i++)
                mb.Cylinder(new Vector3(-5.2f, 0.1f + i * 0.32f, -5.0f), new Vector3(-5.2f, 0.4f + i * 0.32f, -5.0f), 0.55f, 12, Palette.Hex(0x232323));
            m.Height = 8;
        }

        // ================================================================ construction site

        public static MeshBuilder Scaffold(float height)
        {
            var mb = new MeshBuilder();
            mb.Matte();
            mb.Box(new Vector3(0, 0.08f, 0), new Vector3(10, 0.16f, 10), Palette.Dirt);
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -4.6f : 4.6f, z = i < 2 ? -4.6f : 4.6f;
                mb.Box(new Vector3(x, height / 2, z), new Vector3(0.2f, height, 0.2f), Palette.WoodPale);
            }
            for (float y = 2; y < height; y += 2.4f)
            {
                mb.Box(new Vector3(0, y, -4.6f), new Vector3(9.4f, 0.16f, 0.16f), Palette.WoodPale);
                mb.Box(new Vector3(0, y, 4.6f), new Vector3(9.4f, 0.16f, 0.16f), Palette.WoodPale);
                mb.Box(new Vector3(-4.6f, y, 0), new Vector3(0.16f, 0.16f, 9.4f), Palette.WoodPale);
                mb.Box(new Vector3(4.6f, y, 0), new Vector3(0.16f, 0.16f, 9.4f), Palette.WoodPale);
                mb.Box(new Vector3(0, y - 0.1f, -5.0f), new Vector3(9.4f, 0.08f, 0.7f), Palette.Wood);
            }
            Station.Crate(mb, new Vector3(-5.2f, 0, -5.3f), 10);
            Station.Crate(mb, new Vector3(-4.3f, 0, -5.4f), 30);
            for (int i = 0; i < 3; i++) mb.Box(new Vector3(4.5f, 0.15f + i * 0.18f, -5.6f), new Vector3(3, 0.15f, 1), Palette.WoodPale);
            return mb;
        }
    }
}
