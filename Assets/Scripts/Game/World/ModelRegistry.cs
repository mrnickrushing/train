using System.Collections.Generic;
using UnityEngine;

namespace Ironhorse.Game
{
    /// <summary>
    /// Drop-in art: if a prefab exists at Resources/Models/{Category}/{id}, the game uses it instead of the
    /// procedural model. Categories: Locomotives, Wagons, Buildings. See docs/ART_PIPELINE.md for the
    /// exact conventions (scale, facing, pivot, named anchor children).
    /// </summary>
    public static class ModelRegistry
    {
        public const string Locomotives = "Locomotives", Wagons = "Wagons", Buildings = "Buildings";
        static readonly Dictionary<string, GameObject> _cache = new Dictionary<string, GameObject>();

        public static GameObject Prefab(string category, string id)
        {
            string key = category + "/" + id;
            GameObject prefab;
            if (!_cache.TryGetValue(key, out prefab))
            {
                prefab = Resources.Load<GameObject>("Models/" + key);
                _cache[key] = prefab;
            }
            return prefab;
        }

        public static bool Has(string category, string id) { return Prefab(category, id) != null; }

        /// <summary>Instantiates the prefab (or returns null). Colliders inside art are removed; the game adds its own.</summary>
        public static GameObject Spawn(string category, string id, Transform parent)
        {
            var prefab = Prefab(category, id);
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab, parent, false);
            go.name = id;
            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            var mat = Material(category, id);
            if (mat != null)
                foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
            return go;
        }

        static readonly Dictionary<string, Material> _mats = new Dictionary<string, Material>();

        /// <summary>
        /// URP Lit material from the model's maps next to it in Resources (id_albedo, id_ms with metallic
        /// in R and smoothness in A, id_normal). Null if the model ships no albedo map.
        /// </summary>
        public static Material Material(string category, string id)
        {
            string key = category + "/" + id;
            Material m;
            if (_mats.TryGetValue(key, out m)) return m;
            var albedo = Resources.Load<Texture2D>("Models/" + key + "_albedo");
            var template = Resources.Load<Material>("Models/ArtLit");
            if (albedo != null && template != null)
            {
                m = new Material(template) { name = id };
                m.SetTexture("_BaseMap", albedo);
                m.SetColor("_BaseColor", new Color(1.14f, 1.1f, 1.04f)); // AI albedo maps run a bit dark next to the sunlit world
                // Per-model corrections: the boulders' sandstone came out hot orange in the sun, and the new lumber
                // on construction sites a little too yellow next to the weathered town.
                if (id == "boulders") m.SetColor("_BaseColor", new Color(0.8f, 0.68f, 0.62f));
                if (id == "construction_site") m.SetColor("_BaseColor", new Color(1.0f, 0.92f, 0.84f));
                // AI metal/gloss maps vary patch by patch, so painted wood and iron show as a patchwork
                // of sky reflections. Only locomotives (real brass and steel) keep theirs.
                var ms = Resources.Load<Texture2D>("Models/" + key + "_ms");
                m.SetFloat("_Metallic", 0f);
                m.SetFloat("_Smoothness", category == Locomotives ? 0.42f : category == "People" ? 0.15f : 0.22f); // even finish: varnished paint / matte
                if (ms != null)
                {
                    m.SetTexture("_MetallicGlossMap", ms);
                    m.SetFloat("_Smoothness", 0.6f);
                    m.EnableKeyword("_METALLICSPECGLOSSMAP");
                }
                var n = Resources.Load<Texture2D>("Models/" + key + "_normal");
                if (n != null)
                {
                    m.SetTexture("_BumpMap", n);
                    m.SetFloat("_BumpScale", 1f);
                    m.EnableKeyword("_NORMALMAP");
                }
                m.enableInstancing = true;
            }
            _mats[key] = m;
            return m;
        }

        /// <summary>World-space bounds of all renderers under a model.</summary>
        public static Bounds RendererBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        /// <summary>Children whose names start with the prefix (e.g. "Wheel", "Smoke", "Spin").</summary>
        public static List<Transform> Anchors(GameObject go, string prefix)
        {
            var list = new List<Transform>();
            foreach (var t in go.GetComponentsInChildren<Transform>())
                if (t != go.transform && t.name.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase)) list.Add(t);
            return list;
        }
    }
}
