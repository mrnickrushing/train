using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ironhorse.Game
{
    /// <summary>
    /// Draws the valley's trees and shrubs from drop-in art (Resources/Models/Trees/{kind}) with GPU
    /// instancing: instances are grouped into 80 m cells and only cells inside the camera frustum are drawn.
    /// Kinds without art fall back to the procedural meshes in <see cref="Scenery"/>.
    /// </summary>
    public sealed class Vegetation : MonoBehaviour
    {
        const float CellSize = 80f;

        sealed class Kind
        {
            public Mesh Mesh;
            public Material Material;
            public Matrix4x4 Local;
            public readonly Dictionary<Vector2Int, List<Matrix4x4>> Cells = new Dictionary<Vector2Int, List<Matrix4x4>>();
        }

        static readonly Dictionary<string, Kind> _kinds = new Dictionary<string, Kind>();
        static readonly Dictionary<Vector2Int, Bounds> _cellBounds = new Dictionary<Vector2Int, Bounds>();
        static bool _loaded;
        readonly Plane[] _planes = new Plane[6];

        static Kind Get(string kind)
        {
            Kind k;
            if (_kinds.TryGetValue(kind, out k)) return k;
            var prefab = ModelRegistry.Prefab("Trees", kind);
            var mf = prefab != null ? prefab.GetComponentInChildren<MeshFilter>() : null;
            var mat = ModelRegistry.Material("Trees", kind);
            if (mf != null && mf.sharedMesh != null && mat != null)
                k = new Kind { Mesh = mf.sharedMesh, Material = mat, Local = mf.transform.localToWorldMatrix }; // keeps the FBX root's axis-conversion rotation
            _kinds[kind] = k;
            return k;
        }

        /// <summary>True when drop-in art exists for this plant.</summary>
        public static bool HasArt(string kind) { return Get(kind) != null; }

        /// <summary>Registers one instance (position on the ground, uniform scale, random yaw from the position).</summary>
        public static void Add(string kind, Vector3 p, float scale)
        {
            var k = Get(kind);
            if (k == null) return;
            var cell = new Vector2Int(Mathf.FloorToInt(p.x / CellSize), Mathf.FloorToInt(p.z / CellSize));
            List<Matrix4x4> list;
            if (!k.Cells.TryGetValue(cell, out list)) k.Cells[cell] = list = new List<Matrix4x4>();
            float yaw = Mathf.Repeat(p.x * 37.1f + p.z * 11.3f, 360f);
            list.Add(Matrix4x4.TRS(p, Quaternion.Euler(0, yaw, 0), Vector3.one * scale) * k.Local);
            var b = new Bounds(p + Vector3.up * 6 * scale, new Vector3(12, 12, 12) * scale);
            Bounds cb;
            if (_cellBounds.TryGetValue(cell, out cb)) { cb.Encapsulate(b); _cellBounds[cell] = cb; }
            else _cellBounds[cell] = b;
            _loaded = true;
        }

        /// <summary>Spawns one plant as a normal object (for things that come and go, like wild lots).</summary>
        public static GameObject Spawn(string kind, Transform parent, Vector3 localPos, float scale)
        {
            var go = ModelRegistry.Spawn("Trees", kind, parent);
            if (go == null) return null;
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0, Mathf.Repeat(localPos.x * 53f + localPos.z * 29f, 360f), 0) * go.transform.localRotation;
            go.transform.localScale *= scale;
            return go;
        }

        void LateUpdate()
        {
            if (!_loaded) return;
            var cam = CameraRig.Instance != null ? CameraRig.Instance.Cam : Camera.main;
            if (cam == null) return;
            GeometryUtility.CalculateFrustumPlanes(cam, _planes);
            foreach (var k in _kinds.Values)
            {
                if (k == null) continue;
                var rp = new RenderParams(k.Material)
                {
                    shadowCastingMode = ShadowCastingMode.On,
                    receiveShadows = true,
                    layer = 0,
                };
                foreach (var kv in k.Cells)
                {
                    if (!GeometryUtility.TestPlanesAABB(_planes, _cellBounds[kv.Key])) continue;
                    var list = kv.Value;
                    for (int start = 0; start < list.Count; start += 1023)
                        Graphics.RenderMeshInstanced(rp, k.Mesh, 0, list, Mathf.Min(1023, list.Count - start), start);
                }
            }
        }

        void OnDestroy()
        {
            foreach (var k in _kinds.Values) if (k != null) k.Cells.Clear();
            _cellBounds.Clear();
            _loaded = false;
        }
    }
}
