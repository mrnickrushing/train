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
            return go;
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
