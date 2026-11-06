using System.Collections.Generic;
using System.Linq;
using Ironhorse.Core;
using UnityEngine;

namespace Ironhorse.Game
{
    public sealed class BuildingView : MonoBehaviour, ISelectable
    {
        public int Uid;
        int _builtLevel, _plot;
        GameObject _body, _scaffold, _siteArt;
        readonly List<Spinner> _workSpinners = new List<Spinner>();
        readonly List<ParticleSystem> _smoke = new List<ParticleSystem>();
        ParticleSystem _dust;
        float _pop;
        bool _wasReady;
        BuildingDef _def;
        public float Height { get; private set; }

        public void Bind(BuildingState b)
        {
            Uid = b.uid;
            _def = GameDatabase.Building(b.defId);
            Rebuild(b);
        }

        void Rebuild(BuildingState b)
        {
            foreach (Transform c in transform) Destroy(c.gameObject);
            _workSpinners.Clear();
            _smoke.Clear();
            _builtLevel = b.level;
            _plot = b.plot;
            var plotPos = WorldLayout.Plots[b.plot];
            transform.position = plotPos;
            transform.rotation = Quaternion.identity;

            var model = BuildingFactory.Build(_def.Style, b.level, b.uid);
            Height = model.Height;
            var art = ModelRegistry.Spawn(ModelRegistry.Buildings, _def.Id, transform);
            if (art != null)
            {
                // Drop-in art: anchors named Smoke*/Spin* drive effects and animation.
                _body = art;
                Height = Mathf.Max(3f, ModelRegistry.RendererBounds(art).size.y);
                foreach (var t in ModelRegistry.Anchors(art, "Spin"))
                {
                    var sp = t.gameObject.AddComponent<Spinner>();
                    sp.Speed = new Vector3(0, 0, 90);
                    _workSpinners.Add(sp);
                }
                foreach (var t in ModelRegistry.Anchors(art, "Smoke"))
                    _smoke.Add(Fx.Create(_def.Style == BuildingStyle.Fountain ? FxKind.Spray : FxKind.Chimney, t, Vector3.zero));
                model.Spins.Clear();
                model.Smoke.Clear();
            }
            else _body = model.Body.ToObject(_def.Name, transform);
            foreach (var sp in model.Spins)
            {
                var go = sp.Mesh.ToObject("Spin", _body.transform);
                go.transform.localPosition = sp.Position * (1 + 0.035f * (b.level - 1));
                go.transform.localRotation = Quaternion.Euler(sp.Euler);
                var spinner = go.AddComponent<Spinner>();
                spinner.Speed = sp.Speed;
                if (sp.WhenWorking) _workSpinners.Add(spinner);
            }
            foreach (var p in model.Smoke)
                _smoke.Add(Fx.Create(_def.Style == BuildingStyle.Fountain ? FxKind.Spray : FxKind.Chimney, _body.transform, p));

            if (_def.Style == BuildingStyle.LampPost)
                NightLights.Attach(transform, new Vector3(0, Height * 0.9f, 0), LightType.Point, 20f, 12f, NightLights.Lamp);
            else
                NightLights.Attach(transform, new Vector3(0, Mathf.Min(3.2f, Height * 0.45f), -WorldLayout.PlotSize * 0.5f), LightType.Point, 13f, 7f, NightLights.Warm);
            // A real construction site (timber frame, scaffold, hoist, lumber) when the art exists; the building appears
            // whole when it's finished. Without the art, the old scaffold cage with the building growing inside.
            _siteArt = ModelRegistry.Spawn(ModelRegistry.Buildings, "construction_site", transform);
            if (_siteArt != null)
            {
                _scaffold = _siteArt;
                // Turn on top of the FBX root's own axis-conversion rotation (replacing it would tip the model over).
                _scaffold.transform.localRotation = Quaternion.Euler(0, (b.uid % 4) * 90, 0) * _scaffold.transform.localRotation;
            }
            else _scaffold = BuildingFactory.Scaffold(Mathf.Max(5, model.Height * 0.7f)).ToObject("Scaffold", transform);
            _dust = Fx.Create(FxKind.Dust, _scaffold.transform, new Vector3(0, 0.5f, 0));

            var col = gameObject.GetComponent<BoxCollider>();
            if (col == null) col = gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, Height / 2, 0);
            col.size = new Vector3(11, Mathf.Max(3, Height), 11);
            _wasReady = G.Session.IsReady(b);
            UpdateVisuals(b, true);
        }

        public void OnTapped()
        {
            AudioManager.Play(Sfx.Click);
            UIRoot.Instance.OpenBuilding(Uid);
        }

        void Update()
        {
            var b = G.Session.Building(Uid);
            if (b == null) { Destroy(gameObject); return; }
            if (b.level != _builtLevel || b.plot != _plot) { Rebuild(b); _pop = 1; AudioManager.Play(Sfx.Build); }
            UpdateVisuals(b, false);
        }

        void UpdateVisuals(BuildingState b, bool instant)
        {
            var s = G.Session;
            bool ready = s.IsReady(b);
            if (ready && !_wasReady) { _pop = 1; AudioManager.Play(Sfx.Build); Fx.Burst(transform.position + Vector3.up * Height * 0.6f, 40); }
            _wasReady = ready;
            _scaffold.SetActive(!ready);
            if (_siteArt != null) _body.SetActive(ready);
            if (!ready)
            {
                float p = s.ConstructionProgress(b);
                if (_siteArt == null) _body.transform.localScale = new Vector3(1, Mathf.Max(0.02f, p), 1);
            }
            else
            {
                _pop = Mathf.Max(0, _pop - Time.deltaTime * 2.5f);
                float bounce = 1 + Mathf.Sin(_pop * Mathf.PI * 2) * 0.06f * _pop;
                _body.transform.localScale = new Vector3(2 - bounce, bounce, 2 - bounce);
            }
            bool working = ready && (s.IsProducing(b) || _def.Recipe == null);
            foreach (var sp in _workSpinners) sp.Active = working;
            float rate = !ready ? 0 : _def.Category == BuildingCategory.Production ? (s.IsProducing(b) ? 6f : 0.4f) : 2.2f;
            foreach (var sm in _smoke) Fx.SetRate(sm, _def.Style == BuildingStyle.Fountain ? (ready ? 50 : 0) : rate);
        }
    }

    /// <summary>Visual for an empty plot: survey stakes for owned land, a "For Sale" sign for the next lot.</summary>
    public sealed class PlotMarker : MonoBehaviour, ISelectable
    {
        public int Index;
        GameObject _owned, _sale, _highlight, _wild;
        Renderer _hlRenderer;

        public static PlotMarker Create(int index, Transform parent)
        {
            var go = new GameObject("Plot " + index);
            go.transform.SetParent(parent, false);
            go.transform.position = WorldLayout.Plots[index];
            var m = go.AddComponent<PlotMarker>();
            m.Index = index;
            m.Build();
            return m;
        }

        void Build()
        {
            var a = new MeshBuilder();
            a.Matte();
            float h = WorldLayout.PlotSize / 2 - 0.3f;
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -h : h, z = i < 2 ? -h : h;
                a.Box(new Vector3(x, 0.4f, z), new Vector3(0.12f, 0.8f, 0.12f), Palette.WoodPale);
                a.Box(new Vector3(x, 0.75f, z), new Vector3(0.14f, 0.12f, 0.14f), Palette.PaintRed);
            }
            _owned = a.ToObject("Owned", transform, false);

            var s = new MeshBuilder();
            s.Matte();
            s.Box(new Vector3(-0.8f, 1.1f, -4.6f), new Vector3(0.14f, 2.2f, 0.14f), Palette.WoodDark);
            s.Box(new Vector3(0.8f, 1.1f, -4.6f), new Vector3(0.14f, 2.2f, 0.14f), Palette.WoodDark);
            s.Box(new Vector3(0, 1.8f, -4.65f), new Vector3(2.4f, 1.1f, 0.08f), Palette.Cream);
            s.Box(new Vector3(0, 1.95f, -4.7f), new Vector3(1.8f, 0.25f, 0.03f), Palette.PaintRed);
            s.Box(new Vector3(0, 1.6f, -4.7f), new Vector3(1.4f, 0.18f, 0.03f), Palette.WoodDark);
            _sale = s.ToObject("ForSale", transform);

            var hl = new MeshBuilder();
            hl.Lit();
            float e = WorldLayout.PlotSize / 2;
            hl.Box(new Vector3(0, 0.15f, -e), new Vector3(e * 2, 0.12f, 0.35f), Palette.Brass);
            hl.Box(new Vector3(0, 0.15f, e), new Vector3(e * 2, 0.12f, 0.35f), Palette.Brass);
            hl.Box(new Vector3(-e, 0.15f, 0), new Vector3(0.35f, 0.12f, e * 2), Palette.Brass);
            hl.Box(new Vector3(e, 0.15f, 0), new Vector3(0.35f, 0.12f, e * 2), Palette.Brass);
            _highlight = hl.ToObject("Highlight", transform, false);
            _hlRenderer = _highlight.GetComponent<Renderer>();

            // Lumber stacked ready on bought land.
            Prop(_owned.transform, ModelRegistry.Buildings, "logstack", new Vector3(h - 2.6f, 0, h - 2.2f), 90, 0.55f);

            // Land not yet bought is somebody's homestead lot: hay, a kitchen garden, a few head of cattle or a woodlot,
            // with trees and scrub, so the edge of town reads as lived-in rather than an empty grid.
            var w = new MeshBuilder();
            var rng = new System.Random(Index * 7919 + 13);
            System.Func<float, float, float> rr = (a, b) => a + (float)rng.NextDouble() * (b - a);
            var art = new List<(string kind, Vector3 pos, float scale)>();
            _wild = new GameObject("Wild");
            _wild.transform.SetParent(transform, false);
            int look = rng.Next(5);
            if (look == 0)
            {
                // Hay meadow: two or three stacks and a parked wagon.
                for (int i = 0; i < 2 + rng.Next(2); i++) Prop(_wild.transform, ModelRegistry.Buildings, "haystack", new Vector3(rr(-h + 2, h - 2), 0, rr(-h + 2, h - 2)), rr(0, 360), rr(0.7f, 0.95f));
                Prop(_wild.transform, "Props", "covered_wagon", new Vector3(rr(-2, 2), 0, -h + 2.5f), rr(60, 120), 0.9f);
            }
            else if (look == 1)
            {
                // Kitchen garden: tilled rows inside a picket fence, a tree in the corner.
                w.Matte();
                w.Surf = SurfaceKind.Dirt;
                w.Box(new Vector3(0, 0.06f, 0), new Vector3(h * 1.6f, 0.12f, h * 1.6f), Palette.Hex(0x6E5034));
                w.Surf = SurfaceKind.Auto;
                Color[] crop = { Palette.Hex(0x6E9440), Palette.Hex(0x84A444), Palette.Hex(0x9C8A3A) };
                var c = crop[rng.Next(crop.Length)];
                for (float x = -h * 0.7f; x <= h * 0.7f; x += 1.3f) w.Box(new Vector3(x, 0.25f, 0), new Vector3(0.55f, 0.4f, h * 1.45f), MeshBuilder.Darken(c, 0.85f + 0.15f * Mathf.Repeat(x, 2)));
                for (float t = -h * 0.85f; t <= h * 0.85f; t += 0.8f)
                    foreach (var q in new[] { new Vector3(t, 0, -h * 0.85f), new Vector3(t, 0, h * 0.85f), new Vector3(-h * 0.85f, 0, t), new Vector3(h * 0.85f, 0, t) })
                        w.Box(q + Vector3.up * 0.45f, new Vector3(0.08f, 0.9f, 0.08f), Palette.White);
                art.Add(("cottonwood", new Vector3(h - 1, 0, h - 1), rr(0.45f, 0.6f)));
            }
            else if (look == 2)
            {
                // A small pen with a few longhorns.
                w.Matte();
                for (float t = -h + 1; t <= h - 1; t += 2.2f)
                    foreach (var q in new[] { new Vector3(t, 0, -h + 1), new Vector3(t, 0, h - 1), new Vector3(-h + 1, 0, t), new Vector3(h - 1, 0, t) })
                        w.Box(q + Vector3.up * 0.6f, new Vector3(0.14f, 1.2f, 0.14f), Palette.WoodDark);
                foreach (float y in new[] { 0.55f, 1.0f })
                {
                    w.Box(new Vector3(0, y, -h + 1), new Vector3(h * 2 - 2, 0.1f, 0.07f), Palette.Wood);
                    w.Box(new Vector3(0, y, h - 1), new Vector3(h * 2 - 2, 0.1f, 0.07f), Palette.Wood);
                    w.Box(new Vector3(-h + 1, y, 0), new Vector3(0.07f, 0.1f, h * 2 - 2), Palette.Wood);
                    w.Box(new Vector3(h - 1, y, 0), new Vector3(0.07f, 0.1f, h * 2 - 2), Palette.Wood);
                }
                for (int i = 0; i < 2 + rng.Next(3); i++) Prop(_wild.transform, "Props", "longhorn", new Vector3(rr(-h + 3, h - 3), 0, rr(-h + 3, h - 3)), rr(0, 360), 1f);
                Prop(_wild.transform, ModelRegistry.Buildings, "haystack", new Vector3(-h + 2.6f, 0, h - 2.6f), rr(0, 360), 0.6f);
            }
            else if (look == 3)
            {
                // Woodlot: pines being felled and a stack of logs.
                for (int i = 0; i < 2 + rng.Next(2); i++) art.Add((rng.NextDouble() < 0.5 ? "pine" : "pine2", new Vector3(rr(-h + 1.5f, 0), 0, rr(-h + 1.5f, h - 1.5f)), rr(0.55f, 0.85f)));
                Prop(_wild.transform, ModelRegistry.Buildings, "logstack", new Vector3(h - 3.5f, 0, rr(-2, 2)), rr(80, 100), 0.8f);
            }
            else
            {
                // Open meadow with a big shade tree.
                art.Add(("cottonwood", new Vector3(rr(-2, 2), 0, rr(-2, 2)), rr(0.6f, 0.8f)));
                if (rng.NextDouble() < 0.5) Prop(_wild.transform, ModelRegistry.Buildings, "haystack", new Vector3(rr(-h + 2, h - 2), 0, -h + 2.5f), rr(0, 360), 0.75f);
            }
            for (int i = 0; i < 2 + rng.Next(3); i++)
                art.Add(("sage", new Vector3(rr(-h, h), 0, rr(-h, h)), rr(0.4f, 0.7f)));
            if (w.VertexCount > 0) w.ToObject("Lot", _wild.transform);
            foreach (var plant in art)
            {
                if (Vegetation.HasArt(plant.kind)) Vegetation.Spawn(plant.kind, _wild.transform, plant.pos, plant.scale);
                else if (plant.kind == "sage") { var sb = new MeshBuilder(); Scenery.Sage(sb, plant.pos, plant.scale); sb.ToObject("Sage", _wild.transform); }
            }

            var col = gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.5f, 0);
            col.size = new Vector3(WorldLayout.PlotSize, 1, WorldLayout.PlotSize);
        }

        /// <summary>A piece of art on the lot (local position on the plot's ground), or nothing when it isn't imported.</summary>
        static void Prop(Transform parent, string category, string id, Vector3 local, float yaw, float scale)
        {
            var go = ModelRegistry.Spawn(category, id, parent);
            if (go == null) return;
            var world = parent.TransformPoint(local);
            world.y = ValleyTerrain.Height(world.x, world.z);
            go.transform.position = world;
            go.transform.rotation = Quaternion.Euler(0, yaw, 0) * go.transform.rotation;
            go.transform.localScale *= scale;
        }

        public void Refresh(bool owned, bool forSale, bool occupied, bool placing)
        {
            _owned.SetActive(owned && !occupied);
            _wild.SetActive(!owned && !occupied);
            _sale.SetActive(forSale);
            _highlight.SetActive(placing && owned && !occupied);
            GetComponent<Collider>().enabled = !occupied && (owned || forSale);
        }

        void Update()
        {
            if (_highlight.activeSelf)
                _highlight.transform.localPosition = Vector3.up * (Mathf.Sin(Time.time * 4) * 0.08f + 0.05f);
        }

        public void OnTapped()
        {
            var town = TownView.Instance;
            if (town.PlacingDefId != null) { town.PlaceOn(Index); return; }
            AudioManager.Play(Sfx.Click);
            if (!G.Session.IsPlotUnlocked(Index)) UIRoot.Instance.OpenLand();
            else UIRoot.Instance.OpenBuildMenu();
        }
    }

    /// <summary>Owns all building and plot visuals, and the build-placement mode.</summary>
    public sealed class TownView : MonoBehaviour
    {
        public static TownView Instance { get; private set; }
        readonly Dictionary<int, BuildingView> _buildings = new Dictionary<int, BuildingView>();
        readonly List<PlotMarker> _plots = new List<PlotMarker>();

        public string PlacingDefId { get; private set; }
        public int MovingUid { get; private set; }

        void Awake() { Instance = this; }

        void Start()
        {
            for (int i = 0; i < Balance.TotalPlots; i++) _plots.Add(PlotMarker.Create(i, transform));
            G.Session.Changed += Sync;
            CameraRig.Instance.GroundTapped += OnGroundTapped;
            Sync();
        }

        void OnDestroy() { if (G.Session != null) G.Session.Changed -= Sync; }

        public void Sync()
        {
            var s = G.Session;
            foreach (var b in s.State.buildings)
            {
                if (_buildings.ContainsKey(b.uid)) continue;
                var go = new GameObject("Building " + b.uid);
                go.transform.SetParent(transform, false);
                var v = go.AddComponent<BuildingView>();
                v.Bind(b);
                _buildings[b.uid] = v;
            }
            foreach (var uid in _buildings.Keys.ToList())
                if (s.Building(uid) == null) { if (_buildings[uid] != null) Destroy(_buildings[uid].gameObject); _buildings.Remove(uid); }
            RefreshPlots();
        }

        void RefreshPlots()
        {
            var s = G.Session;
            for (int i = 0; i < _plots.Count; i++)
            {
                bool owned = s.IsPlotUnlocked(i);
                bool forSale = i == s.State.unlockedPlots && s.CanBuyPlot;
                bool occupied = s.BuildingOnPlot(i) != null;
                _plots[i].Refresh(owned, forSale, occupied, PlacingDefId != null);
            }
        }

        public BuildingView View(int uid) { BuildingView v; return _buildings.TryGetValue(uid, out v) ? v : null; }

        public void BeginPlacement(string defId)
        {
            PlacingDefId = defId;
            RefreshPlots();
            CameraRig.Instance.FlyTo(new Vector3(0, 0, 25), 120);
            UIRoot.Instance.ShowPlacementBanner(GameDatabase.Building(defId).Name);
        }

        public void CancelPlacement()
        {
            PlacingDefId = null;
            RefreshPlots();
            UIRoot.Instance.HidePlacementBanner();
        }

        public void PlaceOn(int plot)
        {
            if (PlacingDefId == null) return;
            var r = G.Session.Construct(PlacingDefId, plot);
            if (r.Ok)
            {
                AudioManager.Play(Sfx.Build);
                CancelPlacement();
            }
            else AudioManager.Play(Sfx.Error);
            UIRoot.Instance.Toast(r.Message, r.Ok);
        }

        void OnGroundTapped(Vector3 p)
        {
            if (PlacingDefId == null) return;
            // Snap to the nearest free plot if the tap was close.
            int best = -1; float bestD = 9f * 9f;
            for (int i = 0; i < G.Session.State.unlockedPlots; i++)
            {
                if (G.Session.BuildingOnPlot(i) != null) continue;
                float d = (WorldLayout.Plots[i] - new Vector3(p.x, 0, p.z)).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            if (best >= 0) PlaceOn(best);
        }
    }
}
