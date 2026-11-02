using UnityEngine;

namespace Ironhorse.Game
{
    /// <summary>Runtime-created materials and generated textures (shaders live in Resources/Shaders).</summary>
    public static class Materials
    {
        static Material _world, _terrain, _water, _smoke, _sky, _cloud;
        static Texture2D _softCircle, _noise;

        public static Material World { get { return _world ?? (_world = Make("Ironhorse/VertexLit", true)); } }

        public static Material Terrain
        {
            get
            {
                if (_terrain == null)
                {
                    _terrain = Make("Ironhorse/VertexLit", false);
                    _terrain.SetTexture("_MainTex", Noise);
                    _terrain.SetFloat("_DetailScale", 0.045f);
                    _terrain.SetFloat("_DetailStrength", 0.35f);
                }
                return _terrain;
            }
        }

        public static Material Water
        {
            get
            {
                if (_water == null)
                {
                    _water = Make("Ironhorse/Water", false);
                    _water.SetTexture("_WaveTex", Noise);
                }
                return _water;
            }
        }

        public static Material Smoke
        {
            get
            {
                if (_smoke == null)
                {
                    _smoke = Make("Ironhorse/Smoke", false);
                    _smoke.mainTexture = SoftCircle;
                }
                return _smoke;
            }
        }

        public static Material Cloud { get { return _cloud ?? (_cloud = Make("Ironhorse/VertexLit", true)); } }

        public static Material Sky { get { return _sky ?? (_sky = Make("Ironhorse/Sky", false)); } }

        static Material Make(string shader, bool instancing)
        {
            var s = Shader.Find(shader);
            if (s == null)
            {
                Debug.LogError("Shader not found: " + shader + " — falling back to Standard.");
                s = Shader.Find("Standard");
            }
            var m = new Material(s) { name = shader, enableInstancing = instancing };
            return m;
        }

        public static Texture2D SoftCircle
        {
            get
            {
                if (_softCircle != null) return _softCircle;
                const int n = 64;
                _softCircle = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "SoftCircle" };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + 0.5f) / n * 2 - 1, dy = (y + 0.5f) / n * 2 - 1;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float noise = Mathf.PerlinNoise(x * 0.15f, y * 0.15f) * 0.35f + 0.65f;
                        float a = Mathf.Clamp01(1 - d);
                        a = a * a * (3 - 2 * a) * noise;
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                _softCircle.SetPixels32(px);
                _softCircle.Apply();
                return _softCircle;
            }
        }

        /// <summary>Tileable grayscale value noise used for terrain grain and water ripples.</summary>
        public static Texture2D Noise
        {
            get
            {
                if (_noise != null) return _noise;
                const int n = 256;
                _noise = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = "Noise", anisoLevel = 2 };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float v = Noise2.Tileable(x, y, n, 8) * 0.55f + Noise2.Tileable(x, y, n, 32) * 0.3f + Noise2.Tileable(x, y, n, 64) * 0.15f;
                        byte b = (byte)(Mathf.Clamp01(v) * 255);
                        px[y * n + x] = new Color32(b, b, b, 255);
                    }
                _noise.SetPixels32(px);
                _noise.Apply(true);
                return _noise;
            }
        }
    }

    public static class Noise2
    {
        /// <summary>Tileable Perlin noise over an n×n texture with the given frequency (cells per tile).</summary>
        public static float Tileable(int x, int y, int n, int freq)
        {
            float u = (float)x / n, v = (float)y / n;
            // Blend four offset samples so the edges wrap.
            float s = freq;
            float a = Mathf.PerlinNoise(u * s, v * s);
            float b = Mathf.PerlinNoise((u - 1) * s, v * s);
            float c = Mathf.PerlinNoise(u * s, (v - 1) * s);
            float d = Mathf.PerlinNoise((u - 1) * s, (v - 1) * s);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        public static float Fbm(float x, float z, int octaves = 4, float lacunarity = 2f, float gain = 0.5f)
        {
            float sum = 0, amp = 0.5f, freq = 1;
            for (int i = 0; i < octaves; i++)
            {
                sum += amp * Mathf.PerlinNoise(x * freq + 13.1f * i, z * freq - 7.7f * i);
                freq *= lacunarity;
                amp *= gain;
            }
            return sum;
        }

        public static float Ridged(float x, float z, int octaves = 4)
        {
            float sum = 0, amp = 0.5f, freq = 1;
            for (int i = 0; i < octaves; i++)
            {
                float n = 1 - Mathf.Abs(Mathf.PerlinNoise(x * freq + 5.3f * i, z * freq + 9.1f * i) * 2 - 1);
                sum += amp * n * n;
                freq *= 2;
                amp *= 0.5f;
            }
            return sum;
        }

        public static float Hash(int x, int y)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }
    }

    public static class Palette
    {
        public static Color Hex(uint rgb) { return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1); }

        // Landscape
        public static readonly Color Grass = Hex(0x9AA454);
        public static readonly Color GrassDry = Hex(0xC4B06A);
        public static readonly Color Dirt = Hex(0xB08A5A);
        public static readonly Color Sand = Hex(0xD9B98A);
        public static readonly Color RedRock = Hex(0xB5643C);
        public static readonly Color RockDark = Hex(0x8A4B2E);
        public static readonly Color RockPale = Hex(0xD99A6C);
        public static readonly Color Snow = Hex(0xF2F0EA);
        public static readonly Color Ballast = Hex(0x8C8378);
        public static readonly Color Road = Hex(0xA98A62);
        public static readonly Color Pine = Hex(0x355E3B);
        public static readonly Color PineDark = Hex(0x26472C);
        public static readonly Color Bark = Hex(0x5B3A24);
        public static readonly Color Cactus = Hex(0x5E8A4A);
        public static readonly Color Sage = Hex(0x8E9C78);

        // Materials
        public static readonly Color Wood = Hex(0x9C6B3F);
        public static readonly Color WoodDark = Hex(0x5C3A21);
        public static readonly Color WoodPale = Hex(0xC8A27A);
        public static readonly Color Weathered = Hex(0x8F877A);
        public static readonly Color Brick = Hex(0xA4472F);
        public static readonly Color BrickDark = Hex(0x7C3322);
        public static readonly Color Stone = Hex(0xB9AE9C);
        public static readonly Color StoneDark = Hex(0x857A6A);
        public static readonly Color Iron = Hex(0x2C2C30);
        public static readonly Color Steel = Hex(0x6E7681);
        public static readonly Color Brass = Hex(0xD4A64A);
        public static readonly Color Copper = Hex(0xB87333);
        public static readonly Color RoofShingle = Hex(0x6B4A3A);
        public static readonly Color RoofTin = Hex(0x8C9499);
        public static readonly Color RoofRed = Hex(0x8E3B2E);
        public static readonly Color RoofGreen = Hex(0x3F5E4A);
        public static readonly Color Window = Hex(0xFFD98A);
        public static readonly Color Cream = Hex(0xEFE3C8);
        public static readonly Color White = Hex(0xF4F1EA);
        public static readonly Color PaintRed = Hex(0xA8382B);
        public static readonly Color PaintBlue = Hex(0x3C5A7A);
        public static readonly Color PaintGreen = Hex(0x4F6B45);
        public static readonly Color PaintYellow = Hex(0xD9B44A);
        public static readonly Color Black = Hex(0x1B1B1D);
    }
}
