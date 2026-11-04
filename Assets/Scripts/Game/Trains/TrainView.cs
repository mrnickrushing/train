using System.Collections.Generic;
using System.Linq;
using Ironhorse.Core;
using UnityEngine;

namespace Ironhorse.Game
{
    /// <summary>Runtime pieces of one built car.</summary>
    public sealed class CarRuntime
    {
        public Transform Root;
        public float Length;
        public readonly List<(Transform t, float r)> Wheels = new List<(Transform, float)>();
        public readonly List<(Transform t, RodSpec spec)> Rods = new List<(Transform, RodSpec)>();
        public GameObject Cargo;
        /// <summary>A "Cargo" child inside drop-in art, toggled instead of the procedural load.</summary>
        public GameObject ArtCargo;
        public bool IsArt;
        public WagonDef Wagon;
        public CarModel Model;
    }

    /// <summary>
    /// Shows a train consist moving along track paths. The view is a pure function of the
    /// simulation's timestamps: departing → away (hidden) → arriving → parked, so it stays
    /// correct across rushes, reloads and offline progress.
    /// </summary>
    public class ConsistView : MonoBehaviour
    {
        protected readonly List<CarRuntime> Cars = new List<CarRuntime>();
        protected ParticleSystem Smoke;
        protected bool IsSteam;
        protected float Speed;
        float _wheelAngle;
        float _lastS = float.NaN;

        protected void BuildConsist(LocomotiveDef loco, int level, IList<WagonDef> wagons)
        {
            foreach (Transform c in transform) Destroy(c.gameObject);
            Cars.Clear();
            var lm = TrainFactory.Locomotive(loco, level);
            IsSteam = loco.Style != LocoStyle.DieselStreamliner && loco.Style != LocoStyle.DieselTwin;
            var art = AddCar(lm, null, "Loco", ModelRegistry.Locomotives, loco.Id);
            var lead = Cars[0].Root;
            var artSmoke = art != null ? ModelRegistry.Anchors(art, "Smoke") : null;
            if (artSmoke != null && artSmoke.Count > 0) Smoke = Fx.Create(IsSteam ? FxKind.Steam : FxKind.Exhaust, artSmoke[0], Vector3.zero);
            else if (art == null && lm.Stack.HasValue) Smoke = Fx.Create(FxKind.Steam, lead, lm.Stack.Value);
            else if (art == null && lm.Exhaust.HasValue) Smoke = Fx.Create(FxKind.Exhaust, lead, lm.Exhaust.Value);
            NightLights.Attach(lead, new Vector3(0, 3.2f, Cars[0].Length * 0.5f + 0.5f), LightType.Spot, 45f, 25f, new Color(1f, 0.88f, 0.65f), Quaternion.Euler(12, 0, 0), 55f);
            foreach (var w in wagons) AddCar(TrainFactory.Wagon(w), w, w.Name, ModelRegistry.Wagons, w.Id);
            var col = lead.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 2.5f, 0);
            col.size = new Vector3(4.5f, 6f, Cars[0].Length + 2);
        }

        /// <summary>Adds a car; uses drop-in art when a prefab exists (returned), else the procedural model.</summary>
        GameObject AddCar(CarModel model, WagonDef wagon, string name, string category, string id)
        {
            var artGo = ModelRegistry.Spawn(category, id, transform);
            if (artGo != null)
            {
                // Wrap the art so the car root sits at the art's center, facing +Z.
                var holder = new GameObject(name);
                holder.transform.SetParent(transform, false);
                var b = ModelRegistry.RendererBounds(artGo);
                artGo.transform.SetParent(holder.transform, true);
                artGo.transform.localPosition -= new Vector3(b.center.x, 0, b.center.z);
                var art = new CarRuntime { Root = holder.transform, Length = Mathf.Max(2f, b.size.z), Wagon = wagon, Model = model, IsArt = true };
                foreach (var w in ModelRegistry.Anchors(artGo, "Wheel"))
                {
                    var wb = ModelRegistry.RendererBounds(w.gameObject);
                    art.Wheels.Add((w, Mathf.Max(0.2f, wb.size.y / 2)));
                }
                // Loads for art wagons: bulk heaps sit just inside the art's rim; closed cars show none.
                if (wagon != null && model != null)
                {
                    var top = b.max.y - holder.transform.position.y;
                    if (wagon.Cargo == CargoClass.Bulk)
                    {
                        model.CargoAnchor = new Vector3(0, top - 0.12f, 0);
                        model.CargoSize = new Vector3(b.size.x * 0.82f, 0.32f, b.size.z * 0.82f);
                    }
                    else if (wagon.Cargo != CargoClass.Flat) model.CargoSize = Vector3.zero;
                }
                var cargo = ModelRegistry.Anchors(artGo, "Cargo");
                if (cargo.Count > 0) { art.ArtCargo = cargo[0].gameObject; art.ArtCargo.SetActive(false); }
                Cars.Add(art);
                return artGo;
            }

            var go = model.Body.ToObject(name, transform);
            var rt = new CarRuntime { Root = go.transform, Length = model.Length, Wagon = wagon, Model = model };
            foreach (var w in model.Wheels)
            {
                var wo = w.Mesh.ToObject("Wheel", go.transform);
                wo.transform.localPosition = w.Position;
                rt.Wheels.Add((wo.transform, w.Radius));
            }
            foreach (var r in model.Rods)
            {
                var ro = r.Mesh.ToObject("Rod", go.transform);
                ro.transform.localPosition = r.Center;
                rt.Rods.Add((ro.transform, r));
            }
            Cars.Add(rt);
            return null;
        }

        public float ConsistLength { get { float l = 0; foreach (var c in Cars) l += c.Length + TrainFactory.Gap; return l; } }

        /// <summary>Places the consist with the locomotive's nose at distance s along the path.</summary>
        protected void Place(TrackPath path, float s, float dt)
        {
            float cursor = s;
            foreach (var car in Cars)
            {
                float center = cursor - car.Length / 2;
                float half = car.Length * 0.36f;
                var front = path.PositionAt(center + half);
                var back = path.PositionAt(center - half);
                var fwd = front - back;
                car.Root.position = (front + back) / 2 + Vector3.up * 0.0f;
                if (fwd.sqrMagnitude > 1e-6f) car.Root.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
                cursor -= car.Length + TrainFactory.Gap;
            }
            float ds = float.IsNaN(_lastS) ? 0 : s - _lastS;
            _lastS = s;
            Speed = dt > 0 ? Mathf.Lerp(Speed, ds / dt, 0.25f) : 0;
            _wheelAngle += ds;
            foreach (var car in Cars)
            {
                foreach (var w in car.Wheels)
                {
                    if (car.IsArt)
                        w.t.Rotate(Vector3.right, ds / w.r * Mathf.Rad2Deg, Space.Self); // art wheels keep their own base rotation
                    else
                        w.t.localRotation = Quaternion.Euler(_wheelAngle / w.r * Mathf.Rad2Deg, 0, 0);
                }
                foreach (var r in car.Rods)
                {
                    float a = _wheelAngle / r.spec.Center.y;
                    r.t.localPosition = r.spec.Center + new Vector3(0, Mathf.Sin(-a) * r.spec.Crank, Mathf.Cos(-a) * r.spec.Crank);
                }
            }
            if (Smoke != null)
                Fx.SetRate(Smoke, IsSteam ? 3f + Mathf.Abs(Speed) * 1.4f : 2f + Mathf.Abs(Speed) * 0.4f);
            AudioManager.Chuff(this, Mathf.Abs(Speed), IsSteam);
        }

        protected void ResetMotion() { _lastS = float.NaN; Speed = 0; }

        public Vector3 LeadPosition { get { return Cars.Count > 0 ? Cars[0].Root.position : transform.position; } }

        /// <summary>The second or third car back (the lead when there are no wagons).</summary>
        public Vector3 MidPosition { get { return Cars.Count > 1 ? Cars[Mathf.Min(Cars.Count - 1, 2)].Root.position : LeadPosition; } }
    }

    public sealed class TrainView : ConsistView, ISelectable
    {
        public int Uid;
        string _signature;
        int _platform;
        TrackPath _path;
        float _park;
        bool _visible = true;
        bool _cargoShown;
        string _cargoRes;

        public enum Phase { Parked, Departing, Away, Arriving, Arrived }
        public Phase Current { get; private set; }

        bool _dirty;
        void MarkDirty() { _dirty = true; }

        public void Bind(int uid)
        {
            Uid = uid;
            if (G.Session != null) { G.Session.Changed -= MarkDirty; G.Session.Changed += MarkDirty; }
            Rebuild();
        }

        void OnDestroy() { if (G.Session != null) G.Session.Changed -= MarkDirty; }

        string Signature(TrainState t, GameSession s)
        {
            return t.defId + "|" + t.level + "|" + t.platform + "|" + string.Join(",", s.WagonsOf(t).Select(w => w.defId));
        }

        void Rebuild()
        {
            var s = G.Session;
            var t = s.Train(Uid);
            if (t == null) return;
            _signature = Signature(t, s);
            _platform = t.platform;
            _path = WorldLayout.PlatformPath(_platform);
            _park = WorldLayout.ParkDistance(_platform);
            var wagons = s.WagonsOf(t).Select(w => GameDatabase.Wagon(w.defId)).ToList();
            BuildConsist(GameDatabase.Locomotive(t.defId), t.level, wagons);
            _cargoShown = false;
            _cargoRes = null;
            ResetMotion();
        }

        public void OnTapped()
        {
            AudioManager.Play(Sfx.Click);
            UIRoot.Instance.OpenTrain(Uid);
        }

        void Update()
        {
            var s = G.Session;
            var t = s?.Train(Uid);
            if (t == null) { Destroy(gameObject); return; }
            // The consist only changes with the game state, so the signature (string + LINQ) isn't rebuilt every frame.
            if (_dirty) { _dirty = false; if (Signature(t, s) != _signature) Rebuild(); }

            float pos = _park;
            var phase = Phase.Parked;
            if (!t.IsIdle)
            {
                double now = s.Now;
                double total = t.arrivesAt - t.departedAt;
                double elapsed = now - t.departedAt, remaining = t.arrivesAt - now;
                float depDur = (float)System.Math.Min(24, total * 0.45);
                float arrDur = (float)System.Math.Min(28, total * 0.45);
                float depLen = _path.Length - _park + 10;
                if (remaining <= 0) phase = Phase.Arrived;
                else if (elapsed < depDur)
                {
                    phase = Phase.Departing;
                    float u = (float)(elapsed / depDur);
                    pos = _park + depLen * u * u;
                }
                else if (remaining > arrDur) phase = Phase.Away;
                else
                {
                    phase = Phase.Arriving;
                    float u = 1 - (float)(remaining / arrDur);
                    float start = -10;
                    pos = start + (_park - start) * (1 - (1 - u) * (1 - u));
                }
            }
            if (phase != Current)
            {
                if (phase == Phase.Departing && Current == Phase.Parked) { AudioManager.Whistle(LeadPosition); if (Smoke != null) Smoke.Emit(12); }
                if (phase == Phase.Arriving) ResetMotion();
                if (phase == Phase.Arrived && Current == Phase.Arriving) AudioManager.Whistle(LeadPosition, 0.6f);
                Current = phase;
            }

            bool visible = phase != Phase.Away;
            if (visible != _visible)
            {
                _visible = visible;
                foreach (var c in Cars) c.Root.gameObject.SetActive(visible);
                if (!visible) ResetMotion();
            }
            if (!visible) return;

            // Cargo shows while the train is coming home loaded or waiting to unload.
            bool loaded = (phase == Phase.Arriving || phase == Phase.Arrived) && t.cargoAmount > 0;
            if (loaded != _cargoShown || (loaded && _cargoRes != t.cargoId)) SetCargo(loaded ? GameDatabase.Resource(t.cargoId) : null);
            Place(_path, pos, Time.deltaTime);
        }

        void SetCargo(ResourceDef res)
        {
            _cargoShown = res != null;
            _cargoRes = res?.Id;
            foreach (var car in Cars)
            {
                if (car.Cargo != null) { Destroy(car.Cargo); car.Cargo = null; }
                bool match = res != null && car.Wagon != null && car.Wagon.Cargo == res.Cargo;
                if (car.ArtCargo != null) { car.ArtCargo.SetActive(match); continue; }
                if (!match) continue;
                var mb = TrainFactory.Cargo(car.Model, car.Wagon, res);
                if (mb == null) continue;
                car.Cargo = mb.ToObject("Cargo", car.Root);
            }
        }

        public bool IsShownAtStation { get { return Current == Phase.Arrived || Current == Phase.Parked; } }
    }

    /// <summary>Keeps a TrainView for every train in the fleet.</summary>
    public sealed class TrainManager : MonoBehaviour
    {
        public static TrainManager Instance { get; private set; }
        readonly Dictionary<int, TrainView> _views = new Dictionary<int, TrainView>();

        void Awake() { Instance = this; }

        void Start()
        {
            G.Session.Changed += Sync;
            Sync();
            var ex = new GameObject("Excursion Train").AddComponent<ExcursionTrain>();
            ex.transform.SetParent(transform, false);
        }

        void OnDestroy() { if (G.Session != null) G.Session.Changed -= Sync; }

        void Sync()
        {
            foreach (var t in G.Session.State.trains)
            {
                if (_views.ContainsKey(t.uid)) continue;
                var go = new GameObject("Train " + t.uid);
                go.transform.SetParent(transform, false);
                var v = go.AddComponent<TrainView>();
                v.Bind(t.uid);
                _views[t.uid] = v;
            }
            foreach (var uid in _views.Keys.ToList())
                if (G.Session.Train(uid) == null) { if (_views[uid] != null) Destroy(_views[uid].gameObject); _views.Remove(uid); }
        }

        public TrainView View(int uid) { TrainView v; return _views.TryGetValue(uid, out v) ? v : null; }
    }

    /// <summary>
    /// Ambient passenger excursion circling the scenic loop, pulled by the best locomotive you own —
    /// a constant showcase of how far your railroad has come.
    /// </summary>
    public sealed class ExcursionTrain : ConsistView
    {
        string _locoId;
        float _s;
        TrackPath _path;

        void Start()
        {
            _path = WorldLayout.ScenicLoop;
            _s = Random.Range(0, _path.Length);
        }

        void Update()
        {
            var best = G.Session.State.trains.Select(t => GameDatabase.Locomotive(t.defId))
                .OrderByDescending(d => (int)d.Rarity * 100 + d.Speed).FirstOrDefault();
            if (best == null) return;
            if (best.Id != _locoId)
            {
                _locoId = best.Id;
                var coach = G.Session.IsGoldenAge ? GameDatabase.Wagon("pullman") : GameDatabase.Wagon("mailcoach");
                BuildConsist(best, 1, new[] { coach, coach, coach });
                foreach (var c in GetComponentsInChildren<Collider>()) Destroy(c);
                ResetMotion();
            }
            _s += 9.5f * Time.deltaTime;
            Place(_path, _s, Time.deltaTime);
        }
    }
}
