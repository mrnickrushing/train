using System.Collections;
using System.Collections.Generic;
using Ironhorse.Core;
using UnityEngine;

namespace Ironhorse.Game
{
    /// <summary>
    /// A thick bank of cloud over Eagle Lake country (the north of the map) until its key storyline is finished: the
    /// player sees the edge of something big beyond the valley. When the region opens the camera flies over and the
    /// clouds part and drift away, revealing the lake and its new storylines.
    /// </summary>
    public sealed class RegionFog : MonoBehaviour
    {
        public static RegionFog Instance { get; private set; }

        /// <summary>The region's southern edge (the clouds start here and cover everything north of it).</summary>
        public const float EdgeZ = 445f;

        sealed class Puff { public Transform T; public Vector3 Home; public Vector3 Away; public MeshRenderer R; }

        readonly List<Puff> _puffs = new List<Puff>();
        MaterialPropertyBlock _block;
        bool _shown, _lifting;
        float _poll;

        void Awake() { Instance = this; }

        /// <summary>True while the north is still hidden (map pins and scenery there stay quiet).</summary>
        public static bool Hides(float z) { return z > EdgeZ - 10 && G.Session != null && !G.Session.NorthOpen; }

        void Start()
        {
            if (G.Session.NorthOpen) return;
            Build();
        }

        void Build()
        {
            var tex = Resources.Load<Texture2D>("UI/Fx/clouds");
            var mat = new Material(Shader.Find("Ironhorse/Smoke")) { mainTexture = tex, name = "Region Fog", renderQueue = 3060 };
            var rng = new System.Random(4242);
            // Rows of big flat cloud sprites from the edge to the map's end, densest and lowest at the edge, a soft
            // ragged front facing the valley, and a few tall ones so it reads as a cloud bank from any angle.
            for (float z = EdgeZ; z < ValleyTerrain.MaxZ + 40; z += 22)
                for (float x = ValleyTerrain.MinX - 30; x < ValleyTerrain.MaxX + 30; x += 30)
                {
                    float jx = x + (float)rng.NextDouble() * 24 - 12, jz = z + (float)rng.NextDouble() * 16 - 8;
                    if (z == EdgeZ && rng.NextDouble() < 0.25) jz -= 14; // ragged front
                    for (int layer = 0; layer < 2; layer++)
                        _puffs.Add(MakePuff(mat, rng, new Vector3(jx, 26 + layer * 9 + (float)rng.NextDouble() * 6, jz), 58 + (float)rng.NextDouble() * 34));
                }
            _shown = true;
        }

        Puff MakePuff(Material mat, System.Random rng, Vector3 at, float w)
        {
            float h = w * 0.62f;
            int v = rng.Next(3);
            var mesh = new Mesh { name = "Fog" };
            mesh.vertices = new[] { new Vector3(-w / 2, 0, -h / 2), new Vector3(w / 2, 0, -h / 2), new Vector3(w / 2, 0, h / 2), new Vector3(-w / 2, 0, h / 2) };
            float v0 = v / 3f, v1 = (v + 1) / 3f;
            mesh.uv = new[] { new Vector2(0, v0), new Vector2(1, v0), new Vector2(1, v1), new Vector2(0, v1) };
            var a = new Color(1, 1, 1, 0.92f);
            mesh.colors = new[] { a, a, a, a };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            var go = new GameObject("Fog Puff");
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0, (float)rng.NextDouble() * 360, 0));
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var dir = new Vector3(at.x, 0, at.z - EdgeZ + 40).normalized;
            return new Puff { T = go.transform, Home = at, Away = at + dir * 160 + Vector3.up * 40, R = mr };
        }

        void Update()
        {
            if (!_shown || _lifting) return;
            // A gentle drift so the bank feels alive.
            float t = Time.time;
            for (int i = 0; i < _puffs.Count; i++)
            {
                var p = _puffs[i];
                p.T.position = p.Home + new Vector3(Mathf.Sin(t * 0.05f + i) * 3f, 0, Mathf.Cos(t * 0.04f + i * 0.7f) * 2f);
            }
            _poll -= Time.unscaledDeltaTime;
            if (_poll > 0) return;
            _poll = 0.5f;
            var ui = UIRoot.Instance;
            bool busy = ui == null || ui.WindowOpen || ui.SiteOpen || (Celebrations.Instance != null && Celebrations.Instance.Busy)
                || (ProjectSites.Instance != null && ProjectSites.Instance.Animating);
            if (G.Session.NorthOpen && !busy) StartCoroutine(Lift());
        }

        IEnumerator Lift()
        {
            _lifting = true;
            CameraRig.Instance.FlyTo(new Vector3(-20, 0, 560), 320);
            yield return new WaitForSecondsRealtime(1.2f);
            if (UIRoot.Instance != null) UIRoot.Instance.Toast("The clouds part over Eagle Lake country: new storylines await!", true);
            AudioManager.Play(Sfx.LevelUp);
            if (_block == null) _block = new MaterialPropertyBlock();
            for (float k = 0; k < 1; k += Time.unscaledDeltaTime / 3.5f)
            {
                float e = k * k * (3 - 2 * k);
                foreach (var p in _puffs)
                {
                    p.T.position = Vector3.Lerp(p.Home, p.Away, e);
                    _block.SetColor("_Color", new Color(1, 1, 1, 1 - e));
                    p.R.SetPropertyBlock(_block);
                }
                yield return null;
            }
            foreach (var p in _puffs) Destroy(p.T.gameObject);
            _puffs.Clear();
            _shown = false;
        }
    }
}
