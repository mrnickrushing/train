using System.Collections.Generic;
using System.Linq;
using Ironhorse.Core;
using UnityEngine;

namespace Ironhorse.Game
{
    /// <summary>
    /// One truck in the world. In the garage it stands in its bay (three on the apron, three nosing out of the
    /// doors). On a delivery it pulls out loaded, drives down its street and out of town to the north, and comes
    /// back empty just before the trip ends; while it's away it isn't drawn.
    /// </summary>
    public sealed class TruckView : MonoBehaviour, ISelectable
    {
        public int Uid;
        GameObject _body, _cargo;
        readonly List<Transform> _wheels = new List<Transform>();
        float _wheelR = 0.48f;
        string _cargoId;
        Vector3 _cargoAnchor, _cargoSize;
        int _plot = -1, _bay = -1;
        List<Vector3> _path;
        float[] _dist;
        float _roll;
        Vector3 _lastPos;
        ParticleSystem _exhaust;

        const float DriveSpeed = 9f;     // metres per second through town
        const float MaxDriveShare = 0.42f;

        public bool Visible { get { return _body != null && _body.activeSelf; } }
        public Vector3 Top { get { return transform.position + Vector3.up * 3.6f; } }

        public void Bind(TruckState t)
        {
            Uid = t.uid;
            var d = Motor.Truck(t.defId);
            var art = ModelRegistry.Spawn("Trucks", d.Id, transform);
            if (art != null)
            {
                _body = art;
                _wheels.AddRange(ModelRegistry.Anchors(art, "Wheel"));
                _cargoAnchor = new Vector3(0, 1f, -1.2f);
                _cargoSize = new Vector3(1.7f, 0.6f, 2.6f);
            }
            else
            {
                var m = TruckFactory.Truck(d);
                _body = m.Body.ToObject(d.Name, transform);
                foreach (var w in m.Wheels)
                {
                    var go = w.Mesh.ToObject("Wheel", _body.transform);
                    go.transform.localPosition = w.Position;
                    _wheels.Add(go.transform);
                    _wheelR = w.Radius;
                }
                _cargoAnchor = m.CargoAnchor;
                _cargoSize = m.CargoSize;
            }
            var col = gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 1.3f, 0);
            col.size = new Vector3(2.4f, 2.6f, 6f);
            _exhaust = Fx.Create(FxKind.Dust, transform, new Vector3(0, 0.3f, -2.6f));
            Fx.SetRate(_exhaust, 0);
        }

        public void OnTapped()
        {
            AudioManager.Play(Sfx.Click);
            UIRoot.Instance.OpenGarage();
        }

        void Update()
        {
            var s = G.Session;
            var t = s.Truck(Uid);
            if (t == null) { Destroy(gameObject); return; }
            var garage = s.Garage;
            if (garage == null || !s.IsReady(garage)) { Show(false); return; }
            if (garage.plot != _plot || t.bay != _bay) { _plot = garage.plot; _bay = t.bay; _path = null; }
            if (_path == null) BuildPath();

            float driving = 0;
            if (!t.OnJob || s.TruckBack(t))
            {
                Show(true);
                Place(0, false);
                SetCargo(null);
            }
            else
            {
                double total = t.returnsAt - t.departedAt, gone = s.Now - t.departedAt, left = t.returnsAt - s.Now;
                float length = _dist[_dist.Length - 1];
                double drive = System.Math.Min(length / DriveSpeed, total * MaxDriveShare);
                if (gone < drive)
                {
                    Show(true);
                    SetCargo(t.job.resourceId);
                    Place((float)(gone / drive) * length, false);
                    driving = 1;
                }
                else if (left < drive)
                {
                    Show(true);
                    SetCargo(null);
                    Place((float)(left / drive) * length, true);
                    driving = 1;
                }
                else Show(false);
            }
            // Wheels turn with the distance actually covered.
            float moved = (transform.position - _lastPos).magnitude;
            _lastPos = transform.position;
            if (moved < 5f) _roll += moved / _wheelR * Mathf.Rad2Deg;
            foreach (var w in _wheels) w.localRotation = Quaternion.Euler(_roll, 0, 0);
            Fx.SetRate(_exhaust, driving * 6f);
        }

        void Show(bool on)
        {
            if (_body.activeSelf != on) _body.SetActive(on);
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = on;
        }

        void SetCargo(string resourceId)
        {
            if (resourceId == _cargoId) return;
            _cargoId = resourceId;
            if (_cargo != null) Destroy(_cargo);
            _cargo = null;
            if (resourceId == null || _cargoSize == Vector3.zero) return;
            _cargo = TruckFactory.Cargo(resourceId, _cargoSize).ToObject("Cargo", _body.transform);
            _cargo.transform.localPosition = _cargoAnchor;
        }

        /// <summary>Puts the truck <paramref name="along"/> metres down its route; on the way home it faces the garage.</summary>
        void Place(float along, bool homeward)
        {
            var p = At(along);
            Vector3 dir = homeward ? At(along - 1.5f) - At(along + 1.5f) : At(along + 1.5f) - At(along - 1.5f);
            if (along <= 0.01f) dir = BayForward();
            dir.y = 0;
            transform.position = p;
            if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        Vector3 At(float d)
        {
            d = Mathf.Clamp(d, 0, _dist[_dist.Length - 1]);
            int i = 1;
            while (i < _dist.Length - 1 && _dist[i] < d) i++;
            float seg = _dist[i] - _dist[i - 1];
            float u = seg <= 0 ? 0 : (d - _dist[i - 1]) / seg;
            return Vector3.Lerp(_path[i - 1], _path[i], u);
        }

        /// <summary>Bays 0–2 stand on the apron, 3–5 just inside the doors; all face the street (-Z).</summary>
        public static Vector3 BayPosition(int plot, int bay)
        {
            var c = WorldLayout.Plots[plot];
            int col = bay % 3;
            return c + new Vector3((col - 1) * 3.7f, 0, bay < 3 ? -3.2f : 2.3f);
        }

        Vector3 BayForward() { return Vector3.back; }

        /// <summary>Out of the bay to the street in front, along it to the cross street, then north out of town.</summary>
        void BuildPath()
        {
            var c = WorldLayout.Plots[_plot];
            var start = BayPosition(_plot, _bay);
            float street = c.z - WorldLayout.PlotPitchZ / 2;
            float cross = c.x + WorldLayout.PlotPitchX / 2 + (start.x - c.x) * 0.05f;
            float north = WorldLayout.PlotOriginZ + (WorldLayout.PlotRows - 0.5f) * WorldLayout.PlotPitchZ + 18;
            var pts = new List<Vector3>
            {
                start,
                new Vector3(start.x, 0, street + 1.2f),
                new Vector3(start.x + 1.5f, 0, street),
                new Vector3(cross - 1.5f, 0, street),
                new Vector3(cross, 0, street + 1.5f),
                new Vector3(cross, 0, north),
            };
            _path = Smooth(pts, 2);
            for (int i = 0; i < _path.Count; i++) _path[i] = Ground(_path[i]);
            _dist = new float[_path.Count];
            for (int i = 1; i < _path.Count; i++) _dist[i] = _dist[i - 1] + Vector3.Distance(_path[i - 1], _path[i]);
            _lastPos = start;
        }

        static Vector3 Ground(Vector3 p)
        {
            return new Vector3(p.x, Mathf.Max(0, ValleyTerrain.Height(p.x, p.z)) + 0.02f, p.z);
        }

        /// <summary>Chaikin corner cutting; keeps the two ends.</summary>
        static List<Vector3> Smooth(List<Vector3> pts, int passes)
        {
            for (int k = 0; k < passes; k++)
            {
                var o = new List<Vector3> { pts[0] };
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    o.Add(Vector3.Lerp(pts[i], pts[i + 1], 0.25f));
                    o.Add(Vector3.Lerp(pts[i], pts[i + 1], 0.75f));
                }
                o.Add(pts[pts.Count - 1]);
                pts = o;
            }
            return pts;
        }
    }

    /// <summary>Keeps a <see cref="TruckView"/> for every truck in the garage.</summary>
    public sealed class TruckManager : MonoBehaviour
    {
        public static TruckManager Instance { get; private set; }
        readonly Dictionary<int, TruckView> _views = new Dictionary<int, TruckView>();

        void Awake() { Instance = this; }

        void Start()
        {
            G.Session.Changed += Sync;
            Sync();
        }

        void OnDestroy() { if (G.Session != null) G.Session.Changed -= Sync; }

        public TruckView View(int uid) { TruckView v; return _views.TryGetValue(uid, out v) ? v : null; }

        void Sync()
        {
            foreach (var t in G.Session.State.trucks)
            {
                if (_views.ContainsKey(t.uid)) continue;
                var go = new GameObject("Truck " + t.uid);
                go.transform.SetParent(transform, false);
                var v = go.AddComponent<TruckView>();
                v.Bind(t);
                _views[t.uid] = v;
            }
            foreach (var uid in _views.Keys.ToList())
                if (G.Session.Truck(uid) == null) { if (_views[uid] != null) Destroy(_views[uid].gameObject); _views.Remove(uid); }
        }
    }
}
