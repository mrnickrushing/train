using UnityEngine;

namespace Ironhorse.Game
{
    /// <summary>
    /// A colour map over the whole land, read by the terrain shader on grass only: hillside-sized patches of sunny
    /// yellow-green, deep green and olive, and darker, cooler forest floor wherever the trees stand (blurred, so groves
    /// sit in their own shade). From the top of the zoom it gives the meadows the rich, painted variety of a tycoon map.
    /// Built once after the scenery is placed.
    /// </summary>
    public static class GroundTint
    {
        const float MPerPx = 1.5f;

        public static void Build()
        {
            float x0 = ValleyTerrain.MinX, z0 = ValleyTerrain.MinZ;
            float wM = ValleyTerrain.MaxX - x0, hM = ValleyTerrain.MaxZ - z0;
            int w = Mathf.CeilToInt(wM / MPerPx), h = Mathf.CeilToInt(hM / MPerPx);

            // Tree cover: splat each plant's crown, then blur twice.
            var cover = new float[w * h];
            foreach (var p in Vegetation.Plants())
            {
                float r = 6f; // crown shade radius (the stored scale includes the model import scale)
                int ci = (int)((p.x - x0) / MPerPx), cj = (int)((p.z - z0) / MPerPx), rr = Mathf.CeilToInt(r / MPerPx);
                for (int j = Mathf.Max(0, cj - rr); j <= Mathf.Min(h - 1, cj + rr); j++)
                    for (int i = Mathf.Max(0, ci - rr); i <= Mathf.Min(w - 1, ci + rr); i++)
                    {
                        float d = Mathf.Sqrt((i - ci) * (i - ci) + (j - cj) * (j - cj)) * MPerPx / r;
                        if (d < 1) cover[j * w + i] = Mathf.Min(1, cover[j * w + i] + (1 - d) * 0.55f);
                    }
            }
            cover = Blur(Blur(cover, w, h, 4), w, h, 4);

            Color sunny = new Color(1.4f, 1.25f, 0.6f), deep = new Color(0.52f, 0.78f, 0.46f), olive = new Color(1.1f, 0.98f, 0.55f), shade = new Color(0.4f, 0.58f, 0.36f);
            var px = new Color32[w * h];
            for (int j = 0; j < h; j++)
            {
                float z = z0 + (j + 0.5f) * MPerPx;
                for (int i = 0; i < w; i++)
                {
                    float x = x0 + (i + 0.5f) * MPerPx;
                    float a = Mathf.PerlinNoise(x * 0.0055f + 31, z * 0.0055f - 7) * 0.7f + Mathf.PerlinNoise(x * 0.017f, z * 0.017f + 50) * 0.3f;
                    float b = Mathf.PerlinNoise(x * 0.009f - 20, z * 0.009f + 4);
                    var c = a > 0.5f ? Color.Lerp(Color.white, sunny, Mathf.SmoothStep(0, 1, (a - 0.5f) * 3.2f)) : Color.Lerp(Color.white, deep, Mathf.SmoothStep(0, 1, (0.5f - a) * 3.2f));
                    c = Color.Lerp(c, olive, Mathf.SmoothStep(0, 1, (b - 0.62f) * 4f) * 0.8f);
                    c = Color.Lerp(c, shade, Mathf.SmoothStep(0, 1, Mathf.Clamp01(cover[j * w + i] * 3f)) * 0.85f);
                    px[j * w + i] = new Color32((byte)(Mathf.Clamp01(c.r * 0.5f) * 255), (byte)(Mathf.Clamp01(c.g * 0.5f) * 255), (byte)(Mathf.Clamp01(c.b * 0.5f) * 255), 255);
                }
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true, true) { name = "GroundTint", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            tex.SetPixels32(px);
            tex.Apply(true, true);
            Shader.SetGlobalTexture("_GroundTint", tex);
            Shader.SetGlobalVector("_GroundTintRect", new Vector4(x0, z0, 1f / wM, 1f / hM));
        }

        static float[] Blur(float[] src, int w, int h, int r)
        {
            var tmp = new float[w * h];
            var dst = new float[w * h];
            for (int j = 0; j < h; j++)
            {
                float sum = 0; int n = 0;
                for (int i = -r; i < w; i++)
                {
                    if (i + r < w) { sum += src[j * w + i + r]; n++; }
                    if (i - r - 1 >= 0) { sum -= src[j * w + i - r - 1]; n--; }
                    if (i >= 0) tmp[j * w + i] = sum / n;
                }
            }
            for (int i = 0; i < w; i++)
            {
                float sum = 0; int n = 0;
                for (int j = -r; j < h; j++)
                {
                    if (j + r < h) { sum += tmp[(j + r) * w + i]; n++; }
                    if (j - r - 1 >= 0) { sum -= tmp[(j - r - 1) * w + i]; n--; }
                    if (j >= 0) dst[j * w + i] = sum / n;
                }
            }
            return dst;
        }
    }
}
