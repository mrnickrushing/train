using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ironhorse.Game
{
    /// <summary>
    /// Draws the valley's trees and shrubs from drop-in art (Resources/Models/Trees/{kind}) with GPU
    /// instancing: instances are grouped into 80 m cells and only cells inside the camera frustum are drawn.
    /// Kinds without art fall back to the procedural meshes in <see cref="Scenery"/>. Static scenery
    /// models (parked yard wagons) use the same path via <see cref="AddModel"/>.
    /// </summary>
    public sealed class Vegetation : MonoBehaviour
    {
        const float CellSize = 80f;
        const float NearDistance = 150f;

        sealed class Kind
        {
            public Mesh Mesh, FarMesh;
            public Material Material, FarMaterial;
            public Matrix4x4 Local, FarLocal;
            public bool SmallPlant, FarShadows;
            public Bounds LocalBounds; // mesh bounds after Local
            public readonly Dictionary<Vector2Int, List<Matrix4x4>> Cells = new Dictionary<Vector2Int, List<Matrix4x4>>();
            // Far-mesh matrices when the far mesh has its own import transform (models); trees share Cells.
            public readonly Dictionary<Vector2Int, List<Matrix4x4>> FarCells = new Dictionary<Vector2Int, List<Matrix4x4>>();
        }

        static readonly Dictionary<string, Kind> _kinds = new Dictionary<string, Kind>();
        static readonly Dictionary<Vector2Int, Bounds> _cellBounds = new Dictionary<Vector2Int, Bounds>();
        static bool _loaded;
        readonly Plane[] _planes = new Plane[6];

        static Kind Get(string kind) { return Get("Trees", kind); }

        /// <summary>Where every instanced tree and shrub stands, with its scale (for the ground tint under groves).</summary>
        public static IEnumerable<Vector4> Plants()
        {
            foreach (var kv in _kinds)
            {
                if (kv.Value == null || !kv.Key.StartsWith("Trees/")) continue;
                foreach (var cell in kv.Value.Cells.Values)
                    foreach (var m in cell)
                    {
                        var p = m.GetColumn(3);
                        yield return new Vector4(p.x, p.y, p.z, m.GetColumn(0).magnitude);
                    }
            }
        }

        static Kind Get(string category, string kind)
        {
            Kind k;
            string key = category + "/" + kind;
            if (_kinds.TryGetValue(key, out k)) return k;
            var prefab = ModelRegistry.Prefab(category, kind);
            var mf = prefab != null ? prefab.GetComponentInChildren<MeshFilter>() : null;
            var mat = ModelRegistry.Material(category, kind);
            if (mf != null && mf.sharedMesh != null && mat != null)
            {
                k = new Kind { Mesh = mf.sharedMesh, Material = mat, Local = mf.transform.localToWorldMatrix }; // keeps the FBX root's axis-conversion rotation
                k.FarMesh = k.Mesh; k.FarMaterial = mat; k.FarLocal = k.Local;
                k.SmallPlant = kind == "sage";
                k.FarShadows = category != "Trees" && kind != "boulders"; // distant rocks skip the shadow pass
                k.LocalBounds = Transform(k.Local, k.Mesh.bounds);
                var far = ModelRegistry.Prefab(category, kind + "_far");
                var fmf = far != null ? far.GetComponentInChildren<MeshFilter>() : null;
                var fmat = ModelRegistry.Material(category, kind + "_far") ?? mat; // a far mesh may share the near textures
                if (fmf != null) { k.FarMesh = fmf.sharedMesh; k.FarMaterial = fmat; k.FarLocal = fmf.transform.localToWorldMatrix; }
            }
            _kinds[key] = k;
            return k;
        }

        /// <summary>True when drop-in art exists for this plant.</summary>
        public static bool HasArt(string kind) { return Get(kind) != null; }
        public static bool HasArt(string category, string kind) { return Get(category, kind) != null; }

        /// <summary>Registers one instance (position on the ground, uniform scale, random yaw from the position).</summary>
        public static void Add(string kind, Vector3 p, float scale) { Add("Trees", kind, p, scale); }

        public static void Add(string category, string kind, Vector3 p, float scale)
        {
            var k = Get(category, kind);
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

        /// <summary>
        /// Registers one static model instance (e.g. a parked wagon) centred on <paramref name="center"/> at ground
        /// height <paramref name="center"/>.y. Returns the model's length along its local Z (0 when there is no art).
        /// </summary>
        public static float AddModel(string category, string id, Vector3 center, Quaternion rot)
        {
            var k = Get(category, id);
            if (k == null) return 0;
            var lb = k.LocalBounds;
            var place = Matrix4x4.TRS(center, rot, Vector3.one) * Matrix4x4.Translate(new Vector3(-lb.center.x, 0, -lb.center.z));
            var cell = new Vector2Int(Mathf.FloorToInt(center.x / CellSize), Mathf.FloorToInt(center.z / CellSize));
            List<Matrix4x4> list;
            if (!k.Cells.TryGetValue(cell, out list)) k.Cells[cell] = list = new List<Matrix4x4>();
            list.Add(place * k.Local);
            if (k.FarMesh != k.Mesh)
            {
                if (!k.FarCells.TryGetValue(cell, out list)) k.FarCells[cell] = list = new List<Matrix4x4>();
                list.Add(place * k.FarLocal);
            }
            var b = Transform(place, lb);
            Bounds cb;
            if (_cellBounds.TryGetValue(cell, out cb)) { cb.Encapsulate(b); _cellBounds[cell] = cb; }
            else _cellBounds[cell] = b;
            _loaded = true;
            return lb.size.z;
        }

        /// <summary>Length along local Z of a model's art, or 0 when it has none.</summary>
        public static float ModelLength(string category, string id) { var k = Get(category, id); return k != null ? k.LocalBounds.size.z : 0; }

        static Bounds Transform(Matrix4x4 m, Bounds b)
        {
            var r = new Bounds(m.MultiplyPoint3x4(b.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
                r.Encapsulate(m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) * 2 - 1, (i & 2) - 1, (i & 4) / 2f - 1))));
            return r;
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
            float zoom = CameraRig.Instance != null ? CameraRig.Instance.Distance : 95f;
            float thin = Mathf.Lerp(1f, 0.7f, Mathf.InverseLerp(180f, 300f, zoom));
            foreach (var k in _kinds.Values)
            {
                if (k == null) continue;
                var near = new RenderParams(k.Material) { shadowCastingMode = ShadowCastingMode.On, receiveShadows = true, layer = 0 };
                var far = new RenderParams(k.FarMaterial) { shadowCastingMode = k.FarShadows ? ShadowCastingMode.On : ShadowCastingMode.Off, receiveShadows = true, layer = 0 };
                var camPos = cam.transform.position;
                foreach (var kv in k.Cells)
                {
                    var b = _cellBounds[kv.Key];
                    if (!GeometryUtility.TestPlanesAABB(_planes, b)) continue;
                    // Cells close to the camera draw the detailed mesh with shadows; distant ones the light mesh.
                    float d2 = b.SqrDistance(camPos);
                    bool close = d2 < NearDistance * NearDistance;
                    if (k.SmallPlant && d2 > 200f * 200f) continue; // shrubs vanish into the terrain texture
                    var list = kv.Value;
                    List<Matrix4x4> farList;
                    if (!close && k.FarCells.TryGetValue(kv.Key, out farList)) list = farList;
                    // Zoomed far out, distant cells draw a random share of their instances (the lists are shuffled
                    // once), so the whole map stays light on phones while forests still read as forests.
                    int count = list.Count;
                    if (!close && thin < 1)
                    {
                        if (!_shuffled.Contains(list)) { Shuffle(list); _shuffled.Add(list); }
                        count = Mathf.CeilToInt(count * thin);
                    }
                    for (int start = 0; start < count; start += 1023)
                        Graphics.RenderMeshInstanced(close ? near : far, close ? k.Mesh : k.FarMesh, 0, list, Mathf.Min(1023, count - start), start);
                }
            }
        }

        readonly HashSet<List<Matrix4x4>> _shuffled = new HashSet<List<Matrix4x4>>();

        static void Shuffle(List<Matrix4x4> list)
        {
            var rng = new System.Random(list.Count * 7919);
            for (int i = list.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); var t = list[i]; list[i] = list[j]; list[j] = t; }
        }

        void OnDestroy()
        {
            foreach (var k in _kinds.Values) if (k != null) { k.Cells.Clear(); k.FarCells.Clear(); }
            _cellBounds.Clear();
            _loaded = false;
        }
    }
}
