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

        /// <summary>The big moment of sending a train: a chuff of smoke, steam and dust, and a nudge you can feel.</summary>
        void PullOut()
        {
            if (Smoke != null) Smoke.Emit(30);
            // Direction of travel from the consist itself; the cylinders sit under the smokestack.
            var fwd = Cars.Count > 1 ? Cars[0].Root.position - Cars[1].Root.position : (Cars.Count > 0 ? Cars[0].Root.forward : transform.forward);
            var lead = LeadPosition;
            var at = Smoke != null ? new Vector3(Smoke.transform.position.x, lead.y, Smoke.transform.position.z) : lead;
            Fx.Departure(at, fwd, IsSteam);
            var rig = CameraRig.Instance;
            if (rig == null) return;
            float d = Vector3.Distance(new Vector3(LeadPosition.x, 0, LeadPosition.z), rig.Focus);
            if (d < rig.Distance * 1.2f) rig.Shake(Mathf.Lerp(0.5f, 0.15f, d / (rig.Distance * 1.2f)), 0.45f);
            Haptics.Play(Haptic.Medium);
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
                if (phase == Phase.Departing && Current == Phase.Parked)
                {
                    AudioManager.Whistle(LeadPosition);
                    if (Smoke != null) Smoke.Emit(12);
                    if (s.Now - t.departedAt < 2) PullOut(); // just sent, not a trip resumed on launch
                }
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
    /// The Scenic Excursion route: every train sent to "excursion" leaves its platform like any other, and while it is
    /// away it runs laps of the scenic loop north of town with its own locomotive and wagons, spaced apart when several
    /// are out. The loop is empty when nobody is on an excursion.
    /// </summary>
    public sealed class ExcursionTrain : MonoBehaviour
    {
        public const string RouteId = "excursion";
        const float Speed = 9.5f;
        readonly Dictionary<int, ExcursionView> _views = new Dictionary<int, ExcursionView>();

        void Update()
        {
            var s = G.Session;
            if (s == null) return;
            var loop = WorldLayout.ScenicLoop;
            var want = new HashSet<int>();
            int n = 0;
            foreach (var t in s.State.trains)
            {
                if (t.IsIdle || s.HasArrived(t)) continue;
                bool excursion = t.destinationId == RouteId;
                if (!excursion && !DestinationRoutes.Has(t.destinationId)) continue;
                var tv = TrainManager.Instance.View(t.uid);
                if (tv == null || tv.Current != TrainView.Phase.Away) continue;
                ExcursionView v;
                if (!_views.TryGetValue(t.uid, out v) || v == null)
                {
                    v = new GameObject("Out " + t.uid).AddComponent<ExcursionView>();
                    v.transform.SetParent(transform, false);
                    _views[t.uid] = v;
                }
                if (excursion)
                {
                    // Laps from the moment it left, each excursion train a third of the loop behind the one before.
                    float sAt = (float)(s.Now - t.departedAt) * Speed + n * loop.Length / 3f;
                    v.gameObject.SetActive(true);
                    v.Show(t, s);
                    v.Run(loop, Mathf.Repeat(sAt, loop.Length));
                    n++;
                    want.Add(t.uid);
                    continue;
                }
                // A spur trip: run out to the depot, stand at the platform loading, run back to the junction.
                double total = t.arrivesAt - t.departedAt;
                double awayFrom = t.departedAt + System.Math.Min(24, total * 0.45), awayTo = t.arrivesAt - System.Math.Min(28, total * 0.45);
                double span = awayTo - awayFrom, at = s.Now - awayFrom;
                var route = DestinationRoutes.Route(t.destinationId);
                double run = System.Math.Min(route.Length / 11.0, span * 0.42);
                want.Add(t.uid);
                v.gameObject.SetActive(true);
                v.Show(t, s);
                if (at < run) v.Run(route, (float)(route.Length * Ease(at / run)));
                else if (at > span - run) { var back = DestinationRoutes.Back(t.destinationId); v.Run(back, (float)(back.Length * Ease((at - (span - run)) / run))); }
                else v.Run(route, route.Length - 6f);
            }
            foreach (var uid in _views.Keys.ToList())
                if (!want.Contains(uid)) { if (_views[uid] != null) Destroy(_views[uid].gameObject); _views.Remove(uid); }
        }

        /// <summary>Smooth start and stop for a run along a spur.</summary>
        static double Ease(double u) { u = System.Math.Max(0, System.Math.Min(1, u)); return u * u * (3 - 2 * u); }
    }

    /// <summary>One train on the scenic loop: the real consist of the train on the excursion.</summary>
    public sealed class ExcursionView : ConsistView
    {
        string _sig;

        public void Show(TrainState t, GameSession s)
        {
            var wagons = s.WagonsOf(t).Select(w => GameDatabase.Wagon(w.defId)).ToList();
            string sig = t.defId + ":" + t.level + ":" + string.Join(",", wagons.Select(w => w.Id));
            if (sig == _sig) return;
            _sig = sig;
            BuildConsist(GameDatabase.Locomotive(t.defId), t.level, wagons);
            foreach (var c in GetComponentsInChildren<Collider>()) Destroy(c);
            ResetMotion();
        }

        public void Run(TrackPath path, float s) { Place(path, s, Time.deltaTime); }
    }
}
