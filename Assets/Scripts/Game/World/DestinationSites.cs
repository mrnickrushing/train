using System.Collections.Generic;
using Ironhorse.Core;
using UnityEngine;

namespace Ironhorse.Game
{
    /// <summary>
    /// Every outpost and town a train can be sent to is a real place on the map: a rail spur from the main line (the
    /// southern ones) or the scenic loop (the northern ones) runs out to a little depot beside a cluster of buildings
    /// (a logging camp, a mine, farms, a ranch, an oil field, a fort, a boomtown, a town). The terrain is levelled along
    /// each spur and at each site, scenery keeps clear, and trains on a trip visibly run out, stop and come back.
    /// Destinations the player hasn't unlocked sit under drifting cloud.
    /// </summary>
    public static class DestinationRoutes
    {
        static readonly float M = WorldLayout.MainZ;
        static Vector3 V(float x, float z) { return new Vector3(x, 0, z); }

        /// <summary>Waypoints from the junction to the depot at the site (the last point is the site's centre).</summary>
        static readonly Dictionary<string, Vector3[]> Points = new Dictionary<string, Vector3[]>
        {
            // Every spur starts ON its parent line and peels off it gradually, like a real turnout (never at a right
            // angle); spurs heading the same way share a trunk and fork further out, so no two lines cross.
            // South-west, off the west throat (diverging for trains heading west).
            { "blackhollow", new[] { V(-118, M), V(-132, M), V(-152, -52), V(-170, -68), V(-188, -95), V(-212, -135), V(-228, -172) } },
            { "coppertop",   new[] { V(-118, M), V(-132, M), V(-152, -52), V(-156, -72), V(-144, -96), V(-128, -118), V(-108, -160), V(-92, -212) } },
            // South-east, off the east throat (diverging for trains heading east).
            { "rattlesnake", new[] { V(122, M), V(136, M), V(156, -52), V(166, -70), V(178, -95), V(194, -128), V(205, -160) } },
            { "blackgold",   new[] { V(122, M), V(136, M), V(156, -52), V(166, -70), V(178, -95), V(215, -125), V(248, -160), V(266, -195) } },
            { "fortcalder",  new[] { V(122, M), V(136, M), V(156, -52), V(158, -72), V(144, -96), V(120, -126), V(96, -160), V(62, -210), V(38, -236) } },
            { "capitaljct",  new[] { V(122, M), V(136, M), V(156, -52), V(158, -72), V(144, -96), V(124, -126), V(115, -160), V(128, -215), V(150, -250) } },
            // North, off the scenic loop: the first two points lie on the loop, the third already a little outside it.
            { "willowflats", new[] { V(144.9f, 138.1f), V(148.7f, 144f), V(150, 150), V(154, 166), V(170, 170), V(186, 156), V(200, 132), V(216, 108) } },
            { "prosperity",  new[] { V(144.9f, 138.1f), V(148.7f, 144f), V(150, 150), V(154, 166), V(170, 186), V(198, 204), V(224, 224) } },
            { "ironback",    new[] { V(63.4f, 191.7f), V(92.3f, 186.2f), V(116, 192), V(130, 222), V(140, 260), V(148, 300), V(152, 335) } },
            { "silversprings", new[] { V(-51.3f, 193.2f), V(-13.1f, 195.8f), V(14, 206), V(30, 240), V(44, 276), V(58, 296) } },
            { "pinecrest",   new[] { V(-144.9f, 138.1f), V(-148.7f, 144f), V(-150, 150), V(-154, 166), V(-170, 177), V(-200, 186), V(-230, 190) } },
            { "eldorado",    new[] { V(-144.9f, 138.1f), V(-148.7f, 144f), V(-150, 150), V(-154, 166), V(-163, 186), V(-180, 210), V(-195, 235), V(-218, 282), V(-236, 322) } },
        };

        public static IEnumerable<string> Ids { get { return Points.Keys; } }

        /// <summary>True while a point is still on the main line or the scenic loop (within a rail's width).</summary>
        public static bool OnParent(Vector3 p)
        {
            if (Mathf.Abs(p.z - M) < 0.15f && Mathf.Abs(p.x) > 110) return true;
            return WorldLayout.ScenicLoop.SqrDistanceXZ(p.x, p.z) < 0.15f * 0.15f;
        }
        public static bool Has(string id) { return Points.ContainsKey(id); }
        public static Vector3 Site(string id) { var p = Points[id]; return p[p.Length - 1]; }

        static readonly Dictionary<string, TrackPath> _routes = new Dictionary<string, TrackPath>();
        static readonly Dictionary<string, TrackPath> _back = new Dictionary<string, TrackPath>();

        /// <summary>The spur out to a destination (rail height), or null for places without one.</summary>
        public static TrackPath Route(string id)
        {
            TrackPath p;
            if (_routes.TryGetValue(id, out p)) return p;
            Vector3[] pts;
            if (!Points.TryGetValue(id, out pts)) return null;
            p = Smooth(pts, false);
            _routes[id] = p;
            return p;
        }

        /// <summary>The same spur driven back toward the junction.</summary>
        public static TrackPath Back(string id)
        {
            TrackPath p;
            if (_back.TryGetValue(id, out p)) return p;
            Vector3[] pts;
            if (!Points.TryGetValue(id, out pts)) return null;
            p = Smooth(pts, true);
            _back[id] = p;
            return p;
        }

        static HashSet<Vector3> _shared;
        /// <summary>True for a waypoint used by more than one spur.</summary>
        static bool Shared(Vector3 p)
        {
            if (_shared == null)
            {
                _shared = new HashSet<Vector3>();
                var seen = new HashSet<Vector3>();
                foreach (var pts in Points.Values)
                    foreach (var q in new HashSet<Vector3>(pts))
                        if (!seen.Add(q)) _shared.Add(q);
            }
            return _shared.Contains(p);
        }

        static TrackPath Smooth(Vector3[] src, bool reverse)
        {
            var pts = (Vector3[])src.Clone();
            if (reverse) System.Array.Reverse(pts);
            var path = new TrackPath();
            float y = WorldLayout.RailTop;
            path.Add(new Vector3(pts[0].x, y, pts[0].z));
            // Catmull-Rom tangents, except where a point sits on the parent line next to another one that does: there
            // the tangent runs along the parent, so the spur leaves it like a turnout instead of bulging across its rails.
            int last = pts.Length - 1;
            var m = new Vector3[pts.Length];
            for (int i = 0; i <= last; i++)
            {
                Vector3 prev = pts[Mathf.Max(0, i - 1)], next = pts[Mathf.Min(last, i + 1)];
                m[i] = (next - prev) * 0.5f;
                bool on = OnParent(pts[i]);
                if (on && i > 0 && OnParent(pts[i - 1]) && i < last && !OnParent(pts[i + 1]))
                    m[i] = (pts[i] - pts[i - 1]).normalized * (pts[i + 1] - pts[i]).magnitude;
                else if (on && i < last && OnParent(pts[i + 1]) && i > 0 && !OnParent(pts[i - 1]))
                    m[i] = (pts[i + 1] - pts[i]).normalized * (pts[i] - pts[i - 1]).magnitude;
                else if (on && ((i > 0 && OnParent(pts[i - 1])) || (i < last && OnParent(pts[i + 1]))))
                    m[i] = (next - prev).normalized * Vector3.Distance(pts[i], next == pts[i] ? prev : next);
                // A point shared with another spur (a common trunk and its fork): its tangent may only depend on the
                // trunk, so both spurs lay exactly the same rails up to where they part.
                else if (Shared(pts[i]) && i > 0 && i < last)
                {
                    bool fwd = Shared(prev);
                    m[i] = fwd ? pts[i] - prev : next - pts[i];
                }
            }
            for (int i = 0; i < last; i++)
            {
                Vector3 p1 = pts[i], p2 = pts[i + 1];
                int n = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(p1, p2) / 2f));
                for (int k = 1; k <= n; k++)
                {
                    float t = k / (float)n, t2 = t * t, t3 = t2 * t;
                    var q = (2 * t3 - 3 * t2 + 1) * p1 + (t3 - 2 * t2 + t) * m[i] + (-2 * t3 + 3 * t2) * p2 + (t3 - t2) * m[i + 1];
                    path.Line(new Vector3(q.x, y, q.z), 1000);
                }
            }
            return path.Build();
        }

        // ---------------------------------------------------------------- level ground and clearance

        const float Cell = 4f, X0 = ValleyTerrain.MinX, Z0 = ValleyTerrain.MinZ;
        const float SiteRadius = 30f;
        static float[] _dist;
        static int _w, _h;

        /// <summary>Distance (m) to the nearest spur or site edge, saturating at 60: the terrain levels toward 0.</summary>
        public static float Distance(float x, float z)
        {
            if (_dist == null) Build();
            int i = Mathf.Clamp(Mathf.FloorToInt((x - X0) / Cell), 0, _w - 1), j = Mathf.Clamp(Mathf.FloorToInt((z - Z0) / Cell), 0, _h - 1);
            return _dist[j * _w + i];
        }

        /// <summary>True on a spur's bed or a site (scenery and the random mesas stay off).</summary>
        public static bool Blocks(float x, float z) { return Distance(x, z) < 7f; }

        static void Build()
        {
            _w = Mathf.CeilToInt((ValleyTerrain.MaxX - X0) / Cell) + 1;
            _h = Mathf.CeilToInt((ValleyTerrain.MaxZ - Z0) / Cell) + 1;
            _dist = new float[_w * _h];
            for (int k = 0; k < _dist.Length; k++) _dist[k] = 60f;
            foreach (var id in Ids)
            {
                var r = Route(id);
                for (float s = 0; s <= r.Length; s += 2f) Stamp(r.PositionAt(s), 0);
                Stamp(Site(id), SiteRadius);
            }
            foreach (var kv in ProjectSites.Landmarks) Stamp(kv.Value.at, kv.Value.half + 8); // storyline lots are level too
        }

        static void Stamp(Vector3 c, float radius)
        {
            float reach = radius + 60f;
            int i0 = Mathf.Max(0, Mathf.FloorToInt((c.x - reach - X0) / Cell)), i1 = Mathf.Min(_w - 1, Mathf.CeilToInt((c.x + reach - X0) / Cell));
            int j0 = Mathf.Max(0, Mathf.FloorToInt((c.z - reach - Z0) / Cell)), j1 = Mathf.Min(_h - 1, Mathf.CeilToInt((c.z + reach - Z0) / Cell));
            for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                {
                    float x = X0 + (i + 0.5f) * Cell, z = Z0 + (j + 0.5f) * Cell;
                    float d = Mathf.Max(0, Mathf.Sqrt((x - c.x) * (x - c.x) + (z - c.z) * (z - c.z)) - radius);
                    int k = j * _w + i;
                    if (d < _dist[k]) _dist[k] = d;
                }
        }
    }

    /// <summary>Builds the spurs and the places at their ends; drifts cloud over the ones still locked.</summary>
    public sealed class DestinationSites : MonoBehaviour
    {
        public static DestinationSites Instance { get; private set; }

        enum Look { Logging, Mine, Farm, Ranch, IronMine, Fort, Boomtown, GoldCamp, OilField, Town, City, Capital }

        static readonly Dictionary<string, Look> Looks = new Dictionary<string, Look>
        {
            { "pinecrest", Look.Logging }, { "blackhollow", Look.Mine }, { "willowflats", Look.Farm }, { "rattlesnake", Look.Ranch },
            { "ironback", Look.IronMine }, { "fortcalder", Look.Fort }, { "coppertop", Look.Boomtown }, { "eldorado", Look.GoldCamp },
            { "blackgold", Look.OilField }, { "silversprings", Look.Town }, { "prosperity", Look.City }, { "capitaljct", Look.Capital },
        };

        readonly Dictionary<string, GameObject> _clouds = new Dictionary<string, GameObject>();
        Material _cloudMat;
        float _poll;

        void Awake() { Instance = this; }

        /// <summary>Where a destination's pin floats.</summary>
        public static Vector3? PinAt(string id)
        {
            if (!DestinationRoutes.Has(id)) return null;
            var c = DestinationRoutes.Site(id);
            return new Vector3(c.x, ValleyTerrain.Height(c.x, c.z) + 16f, c.z);
        }

        void Start()
        {
            var rails = new MeshBuilder();
            foreach (var id in DestinationRoutes.Ids)
            {
                var r = DestinationRoutes.Route(id);
                // Draw from where the spur's rails leave the parent line's, so the two never overlap.
                float from = 0;
                while (from < r.Length - 1 && DestinationRoutes.OnParent(r.PositionAt(from))) from += 0.25f;
                TrackBuilder.Build(rails, r, Mathf.Max(0, from - 0.25f), -1, trestleOverWater: true);
                Depot(rails, r);
                if (DepotArt)
                {
                    // The station building (art) beside the end of the line, its platform and front to the track.
                    Vector3 e, f;
                    r.Sample(r.Length - 0.5f, out e, out f);
                    f.y = 0; f.Normalize();
                    var rt = Vector3.Cross(Vector3.up, f);
                    PlaceAt("outpostdepot", e - f * 14 + rt * 7.5f, Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg + 90);
                }
            }
            rails.ToObject("Destination Spurs", transform);
            foreach (var id in DestinationRoutes.Ids) BuildSite(id);

            var tex = Resources.Load<Texture2D>("UI/Fx/clouds");
            _cloudMat = new Material(Shader.Find("Ironhorse/Smoke")) { mainTexture = tex, name = "Fog Clouds", renderQueue = 3050 };
        }

        void Update()
        {
            _poll -= Time.unscaledDeltaTime;
            if (_poll > 0 || Ironhorse.Game.G.Session == null) return;
            _poll = 0.5f;
            foreach (var id in DestinationRoutes.Ids)
            {
                var d = GameDatabase.Destination(id);
                bool locked = d != null && !Ironhorse.Game.G.Session.IsDestinationUnlocked(d);
                GameObject c;
                _clouds.TryGetValue(id, out c);
                if (locked && c == null) _clouds[id] = Fog(id);
                else if (!locked && c != null) { Destroy(c); _clouds.Remove(id); }
            }
        }

        // ---------------------------------------------------------------- fog of war

        GameObject Fog(string id)
        {
            var c = DestinationRoutes.Site(id);
            var root = new GameObject("Fog " + id);
            root.transform.SetParent(transform, false);
            var rng = new System.Random(id.GetHashCode());
            for (int i = 0; i < 16; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2, d = Mathf.Sqrt((float)rng.NextDouble()) * 40f;
                float w = 40 + (float)rng.NextDouble() * 40, h = w * 0.62f;
                int v = rng.Next(3);
                var mesh = new Mesh { name = "Fog" };
                mesh.vertices = new[] { new Vector3(-w / 2, 0, -h / 2), new Vector3(w / 2, 0, -h / 2), new Vector3(w / 2, 0, h / 2), new Vector3(-w / 2, 0, h / 2) };
                float v0 = v / 3f, v1 = (v + 1) / 3f;
                mesh.uv = new[] { new Vector2(0, v0), new Vector2(1, v0), new Vector2(1, v1), new Vector2(0, v1) };
                var alpha = new Color(1, 1, 1, 0.5f + (float)rng.NextDouble() * 0.25f);
                mesh.colors = new[] { alpha, alpha, alpha, alpha };
                mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                mesh.RecalculateBounds();
                var go = new GameObject("Puff");
                go.transform.SetParent(root.transform, false);
                go.transform.position = new Vector3(c.x + Mathf.Cos(a) * d, 12 + i * 1.6f, c.z + Mathf.Sin(a) * d);
                go.transform.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360, 0);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _cloudMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
            return root;
        }

        // ---------------------------------------------------------------- the depot at the end of the line

        static bool DepotArt { get { return ModelRegistry.Has(ModelRegistry.Buildings, "outpostdepot"); } }

        static void Depot(MeshBuilder mb, TrackPath r)
        {
            Vector3 end, fwd;
            r.Sample(r.Length - 0.5f, out end, out fwd);
            fwd.y = 0; fwd.Normalize();
            var right = Vector3.Cross(Vector3.up, fwd);
            var rot = Quaternion.LookRotation(fwd);
            mb.Matte();
            // Buffer stop.
            mb.Push().Translate(end + fwd * 1.5f).Rotate(rot);
            mb.Box(new Vector3(0, 0.7f, 0), new Vector3(2.6f, 0.5f, 0.5f), Palette.PaintRed);
            mb.Box(new Vector3(-0.9f, 0.4f, -0.6f), new Vector3(0.3f, 0.8f, 1.6f), Palette.WoodDark);
            mb.Box(new Vector3(0.9f, 0.4f, -0.6f), new Vector3(0.3f, 0.8f, 1.6f), Palette.WoodDark);
            mb.Pop();
            if (DepotArt) return;
            // A plank platform and a small depot shed beside the last car lengths.
            var pc = end - fwd * 14 + right * 4.2f;
            mb.Surf = SurfaceKind.Planks;
            mb.Push().Translate(pc).Rotate(rot);
            mb.Box(new Vector3(0, 0.45f, 0), new Vector3(3.4f, 0.9f, 24f), Palette.WoodPale);
            mb.Pop();
            mb.Surf = SurfaceKind.Auto;
            mb.Push().Translate(pc + right * 4.6f).Rotate(rot);
            mb.Box(new Vector3(0, 1.8f, 0), new Vector3(4.6f, 3.6f, 8f), Palette.Hex(0xB08A5A), Palette.Hex(0xB08A5A));
            mb.GableRoof(new Vector3(0, 3.6f, 0), new Vector3(5.4f, 1.6f, 8.8f), Palette.RoofRed, Palette.Hex(0xB08A5A), 0.3f);
            mb.Box(new Vector3(-2.33f, 1.3f, 0), new Vector3(0.06f, 2.4f, 1.4f), Palette.WoodDark);
            mb.Pop();
        }

        // ---------------------------------------------------------------- the places

        static float G(float x, float z) { return ValleyTerrain.Height(x, z); }

        void Place(string model, Vector3 c, float dx, float dz, float yaw)
        {
            if (!ModelRegistry.Has(ModelRegistry.Buildings, model)) return;
            var art = ModelRegistry.Spawn(ModelRegistry.Buildings, model, transform);
            var p = new Vector3(c.x + dx, 0, c.z + dz);
            p.y = G(p.x, p.z);
            art.transform.SetPositionAndRotation(p, Quaternion.Euler(0, yaw, 0) * art.transform.rotation);
        }

        void BuildSite(string id)
        {
            Look look;
            if (!Looks.TryGetValue(id, out look)) return;
            var c = DestinationRoutes.Site(id);
            var r = DestinationRoutes.Route(id);
            Vector3 end, fwd;
            r.Sample(r.Length - 1, out end, out fwd);
            fwd.y = 0; fwd.Normalize();
            float yaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
            // Buildings stand on the far side of the depot from the track, facing it; offsets are in the spur's frame.
            var right = Vector3.Cross(Vector3.up, fwd);
            System.Func<float, float, Vector3> At = (side, along) => c + right * side + fwd * along;
            var mb = new MeshBuilder();
            var rng = new System.Random(id.GetHashCode());
            // The place's signature building (Models/Buildings/dest_{id}) stands beside the depot, its front to the
            // track; the procedural buildings make way for it, the props and outbuildings stay around it.
            bool hero = ModelRegistry.Has(ModelRegistry.Buildings, "dest_" + id);
            _skipMain = hero;
            if (hero) PlaceAt("dest_" + id, At(26, 0), yaw + 90);
            switch (look)
            {
                case Look.Logging:
                    if (!hero) PlaceAt("sawmill", At(18, -10), yaw + 90);
                    PlaceAt("cabin", At(-14, -18), yaw + 180);
                    PlaceAt("cabin", At(-16, 2), yaw + 160);
                    for (int i = 0; i < 6; i++) LogPile(mb, At(8 + (i % 3) * 5f, 8 + (i / 3) * 6f), yaw, rng);
                    break;
                case Look.Mine:
                    if (!hero) Headframe(mb, At(16, 6), yaw);
                    if (!hero) PlaceAt("freightshed", At(17, -14), yaw + 90);
                    for (int i = 0; i < 4; i++) Heap(mb, At(-12 + (i % 2) * 8, -10 + (i / 2) * 12), 5 + i, Palette.Hex(0x232326));
                    PlaceAt("cabin", At(-18, 14), yaw);
                    break;
                case Look.Farm:
                    if (!hero) PlaceAt("gristmill", At(16, -6), yaw + 90);
                    if (!hero) PlaceAt("freightbarn", At(-18, 6), yaw - 90);
                    PlaceAt("clapboard", At(18, 16), yaw + 90);
                    Fields(mb, c + right * 2 + fwd * 34, fwd, right, 3);
                    PlaceAt("watertower", At(-6, -20), yaw);
                    break;
                case Look.Ranch:
                    if (!hero) PlaceAt("clapboard", At(16, 10), yaw + 90);
                    if (!hero) PlaceAt("freightbarn", At(18, -14), yaw + 90);
                    Corral(mb, At(-14, 0), yaw, 22, 16);
                    Corral(mb, At(-14, 20), yaw, 14, 12);
                    PlaceAt("watertower", At(4, 18), yaw);
                    break;
                case Look.IronMine:
                    if (!hero) PlaceAt("smelter", At(18, -6), yaw + 90);
                    Headframe(mb, At(-14, 8), yaw);
                    for (int i = 0; i < 4; i++) Heap(mb, At(-16 + (i % 2) * 9, -12 + (i / 2) * 10), 5 + i, Palette.Hex(0x8E4B3A));
                    break;
                case Look.Fort:
                    if (!hero) Fort(mb, At(16, 0), yaw);
                    PlaceAt("telegraph", At(-14, 12), yaw - 90);
                    break;
                case Look.Boomtown:
                    if (!hero) PlaceAt("wireworks", At(18, -6), yaw + 90);
                    PlaceAt("saloon", At(-14, 12), yaw - 90);
                    for (int i = 0; i < 8; i++) Tent(mb, At(-12 + (i % 4) * 6.5f, -18 + (i / 4) * 7), yaw + (float)rng.NextDouble() * 30);
                    for (int i = 0; i < 3; i++) Heap(mb, At(10 + i * 7, 18), 4, Palette.Hex(0xC8743A));
                    break;
                case Look.GoldCamp:
                    if (!hero) PlaceAt("assay", At(16, -4), yaw + 90);
                    PlaceAt("saloon", At(-15, 10), yaw - 90);
                    for (int i = 0; i < 6; i++) Tent(mb, At(-14 + (i % 3) * 7, -16 + (i / 3) * 7), yaw + (float)rng.NextDouble() * 40);
                    Sluice(mb, At(8, 18), yaw);
                    break;
                case Look.OilField:
                    if (!hero) PlaceAt("refinery", At(18, -4), yaw + 90);
                    for (int i = 0; i < 7; i++) Derrick(mb, At(-22 + (i % 4) * 10, -16 + (i / 4) * 22 + (i % 2) * 4));
                    for (int i = 0; i < 3; i++) Tank(mb, At(14 + i * 6, 18));
                    break;
                case Look.Town:
                    Town(c, right, fwd, yaw, new[] { "boarding", "clapboard", "saloon", "schoolhouse", "clapboard", "cabin", "bank" }, rng);
                    break;
                case Look.City:
                    Town(c, right, fwd, yaw, new[] { "grandhotel", "rowhouse", "rowhouse", "bank", "boarding", "clocktower", "saloon", "clapboard", "rowhouse" }, rng);
                    break;
                case Look.Capital:
                    Town(c, right, fwd, yaw, new[] { "clocktower", "grandhotel", "bank", "rowhouse", "rowhouse", "grandhotel", "boarding", "rowhouse", "fountain", "schoolhouse", "rowhouse" }, rng);
                    break;
            }
            if (mb.VertexCount > 0) Station.Walkable(mb.ToObject(id + " site", transform));
        }

        bool _skipMain;

        void PlaceAt(string model, Vector3 p, float yaw) { Place(model, p, 0, 0, yaw); }

        /// <summary>Blocks of buildings either side of a main street running beside the depot.</summary>
        void Town(Vector3 c, Vector3 right, Vector3 fwd, float yaw, string[] models, System.Random rng)
        {
            int n = models.Length;
            for (int i = 0; i < n; i++)
            {
                int row = i % 2, col = i / 2;
                float side = (row == 0 ? 16 : 34) * (_skipMain ? -1 : 1) + (_skipMain ? -4 : 0);
                float along = -22 + col * 14 + (float)rng.NextDouble() * 2;
                var p = c + right * side + fwd * along;
                PlaceAt(models[i], p, yaw + 90 + (row == 1 ? 180 : 0) + (_skipMain ? 180 : 0)); // fronts toward the main street
            }
            if (!_skipMain) for (int i = 0; i < 4; i++) PlaceAt(i % 2 == 0 ? "clapboard" : "cabin", c - right * 16 + fwd * (-18 + i * 13), yaw - 90);
        }

        // ---------------------------------------------------------------- props

        static void LogPile(MeshBuilder mb, Vector3 p, float yaw, System.Random rng)
        {
            p.y = G(p.x, p.z);
            mb.Matte();
            var rot = Quaternion.Euler(0, yaw + 90, 0);
            for (int layer = 0; layer < 3; layer++)
                for (int k = 0; k < 4 - layer; k++)
                {
                    var o = rot * new Vector3((k - (3 - layer) / 2f) * 0.75f, 0.38f + layer * 0.62f, 0);
                    var a = p + o + rot * Vector3.forward * -2.4f; var b = p + o + rot * Vector3.forward * 2.4f;
                    mb.Cylinder(a, b, 0.36f, 8, Color.Lerp(Palette.Bark, Palette.Wood, (float)rng.NextDouble() * 0.5f));
                }
        }

        static void Heap(MeshBuilder mb, Vector3 p, float r, Color col)
        {
            // Coal (dark) or ore heaps from the art when there is some, at a random turn.
            string art = col.r + col.g + col.b < 0.6f ? "coalheap" : "oreheap";
            if (Vegetation.HasArt(ModelRegistry.Buildings, art))
            {
                p.y = G(p.x, p.z);
                Vegetation.AddModel(ModelRegistry.Buildings, art, p, Quaternion.Euler(0, Mathf.Repeat(p.x * 37 + p.z * 11, 360), 0));
                return;
            }
            p.y = G(p.x, p.z) - 0.3f;
            mb.Matte();
            mb.Sphere(p, new Vector3(r, r * 0.45f, r * 0.85f), 10, col, false);
        }

        static void Headframe(MeshBuilder mb, Vector3 p, float yaw)
        {
            p.y = G(p.x, p.z);
            mb.Matte();
            mb.Push().Translate(p).RotateY(yaw);
            mb.Box(new Vector3(0, 1.5f, 0), new Vector3(7, 3, 6), Palette.Weathered, Palette.WoodDark);
            mb.GableRoof(new Vector3(0, 3, 0), new Vector3(7.4f, 1.4f, 6.4f), Palette.RoofTin, Palette.Weathered, 0.2f);
            foreach (var s in new[] { -1.6f, 1.6f })
            {
                mb.Cylinder(new Vector3(s, 3, -1.6f), new Vector3(s * 0.4f, 13, 0), 0.22f, 6, Palette.WoodDark);
                mb.Cylinder(new Vector3(s, 3, 1.6f), new Vector3(s * 0.4f, 13, 0), 0.22f, 6, Palette.WoodDark);
            }
            mb.Cylinder(new Vector3(-1.2f, 13.2f, 0), new Vector3(1.2f, 13.2f, 0), 1.3f, 14, Palette.Iron, true);
            mb.Pop();
        }

        static void Fields(MeshBuilder mb, Vector3 c, Vector3 fwd, Vector3 right, int n)
        {
            mb.Matte();
            Color[] crops = { Palette.Hex(0xD9B44A), Palette.Hex(0x7FA548), Palette.Hex(0xC29A52) };
            for (int i = 0; i < n; i++)
            {
                var fc = c + right * ((i - (n - 1) / 2f) * 20);
                var rot = Quaternion.LookRotation(fwd);
                for (int row = 0; row < 7; row++)
                {
                    var rc = fc + right * (row - 3) * 2.4f;
                    rc.y = G(rc.x, rc.z) + 0.25f;
                    mb.Push().Translate(rc).Rotate(rot);
                    mb.Box(Vector3.zero, new Vector3(1.6f, 0.5f, 22f), MeshBuilder.Darken(crops[i % crops.Length], 0.9f + 0.1f * (row % 2)));
                    mb.Pop();
                }
            }
        }

        static void Corral(MeshBuilder mb, Vector3 c, float yaw, float w, float d)
        {
            mb.Matte();
            mb.Push().Translate(new Vector3(c.x, G(c.x, c.z), c.z)).RotateY(yaw);
            for (float x = -w / 2; x <= w / 2 + 0.01f; x += 2.5f)
                foreach (float z in new[] { -d / 2, d / 2 }) mb.Box(new Vector3(x, 0.7f, z), new Vector3(0.16f, 1.4f, 0.16f), Palette.WoodDark);
            for (float z = -d / 2; z <= d / 2 + 0.01f; z += 2.5f)
                foreach (float x in new[] { -w / 2, w / 2 }) mb.Box(new Vector3(x, 0.7f, z), new Vector3(0.16f, 1.4f, 0.16f), Palette.WoodDark);
            foreach (float y in new[] { 0.6f, 1.2f })
            {
                mb.Box(new Vector3(0, y, -d / 2), new Vector3(w, 0.12f, 0.08f), Palette.Wood);
                mb.Box(new Vector3(0, y, d / 2), new Vector3(w, 0.12f, 0.08f), Palette.Wood);
                mb.Box(new Vector3(-w / 2, y, 0), new Vector3(0.08f, 0.12f, d), Palette.Wood);
                mb.Box(new Vector3(w / 2, y, 0), new Vector3(0.08f, 0.12f, d), Palette.Wood);
            }
            // Cattle: brown and white bodies grazing.
            var rng = new System.Random((int)(c.x * 13 + c.z));
            bool longhorns = Vegetation.HasArt("Props", "longhorn");
            var toWorld = mb.Matrix;
            for (int i = 0; i < (int)(w * d / 30); i++)
            {
                var p = new Vector3(((float)rng.NextDouble() - 0.5f) * (w - 3), 0, ((float)rng.NextDouble() - 0.5f) * (d - 3));
                if (longhorns)
                {
                    var wp = toWorld.MultiplyPoint3x4(p);
                    wp.y = G(wp.x, wp.z);
                    Vegetation.AddModel("Props", "longhorn", wp, Quaternion.Euler(0, (float)rng.NextDouble() * 360, 0));
                    continue;
                }
                mb.Push().Translate(p).RotateY((float)rng.NextDouble() * 360);
                var coat = rng.Next(3) == 0 ? Palette.Cream : Palette.Hex(0x7A4A2A);
                mb.Box(new Vector3(0, 0.95f, 0), new Vector3(0.75f, 0.7f, 1.7f), coat);
                mb.Box(new Vector3(0, 1.05f, 1.0f), new Vector3(0.45f, 0.45f, 0.5f), coat);
                for (int l = 0; l < 4; l++) mb.Box(new Vector3(l % 2 == 0 ? -0.25f : 0.25f, 0.3f, l < 2 ? 0.6f : -0.6f), new Vector3(0.14f, 0.6f, 0.14f), MeshBuilder.Darken(coat, 0.7f));
                mb.Pop();
            }
            mb.Pop();
        }

        static void Fort(MeshBuilder mb, Vector3 c, float yaw)
        {
            mb.Matte();
            mb.Push().Translate(new Vector3(c.x, G(c.x, c.z), c.z)).RotateY(yaw);
            const float s = 26;
            // Pointed-log palisade with corner blockhouses and a gate facing the depot.
            for (float t = -s / 2; t <= s / 2; t += 0.7f)
                foreach (var e in new[] { new Vector3(t, 0, -s / 2), new Vector3(t, 0, s / 2), new Vector3(-s / 2, 0, t), new Vector3(s / 2, 0, t) })
                {
                    if (e.x < -s / 2 + 0.1f && Mathf.Abs(e.z) < 2.4f) continue; // gate
                    mb.Cylinder(e, e + Vector3.up * 4.2f, 0.34f, 6, Palette.Hex(0x7A5636));
                }
            foreach (var k in new[] { new Vector3(-s / 2, 0, -s / 2), new Vector3(s / 2, 0, -s / 2), new Vector3(-s / 2, 0, s / 2), new Vector3(s / 2, 0, s / 2) })
            {
                mb.Box(k + Vector3.up * 3.2f, new Vector3(4, 6.4f, 4), Palette.Hex(0x6B4A30));
                mb.HipRoof(k + Vector3.up * 6.4f, new Vector3(4.6f, 1.8f, 4.6f), Palette.RoofShingle, 0.3f);
            }
            mb.Box(new Vector3(4, 1.8f, 0), new Vector3(10, 3.6f, 6), Palette.Hex(0x8C6A44));
            mb.GableRoof(new Vector3(4, 3.6f, 0), new Vector3(10.6f, 1.6f, 6.6f), Palette.RoofShingle, Palette.Hex(0x8C6A44), 0.3f);
            mb.Cylinder(new Vector3(-4, 0, 4), new Vector3(-4, 13, 4), 0.1f, 6, Palette.White);
            for (int i = 0; i < 6; i++) mb.Box(new Vector3(-2.6f, 12.6f - i * 0.25f, 4), new Vector3(2.6f, 0.25f, 0.03f), i % 2 == 0 ? Palette.PaintRed : Palette.White);
            mb.Box(new Vector3(-3.35f, 12.35f, 3.98f), new Vector3(1.1f, 0.75f, 0.03f), Palette.PaintBlue);
            mb.Pop();
        }

        static void Tent(MeshBuilder mb, Vector3 p, float yaw)
        {
            p.y = G(p.x, p.z);
            mb.Matte();
            mb.Push().Translate(p).RotateY(yaw);
            mb.GableRoof(new Vector3(0, 0, 0), new Vector3(3.2f, 2.4f, 4f), Palette.Cream, Palette.Hex(0xD8CBB0), 0.05f);
            mb.Pop();
        }

        static void Sluice(MeshBuilder mb, Vector3 p, float yaw)
        {
            p.y = G(p.x, p.z);
            mb.Matte();
            mb.Push().Translate(p).RotateY(yaw);
            for (int i = 0; i < 5; i++)
            {
                mb.Box(new Vector3(0, 1.2f - i * 0.18f, i * 3f), new Vector3(0.9f, 0.4f, 3f), Palette.WoodPale);
                mb.Box(new Vector3(0, 0.5f - i * 0.09f, i * 3f), new Vector3(0.15f, 1f, 0.15f), Palette.WoodDark);
            }
            mb.Pop();
        }

        static void Derrick(MeshBuilder mb, Vector3 p)
        {
            p.y = G(p.x, p.z);
            mb.Matte();
            for (int k = 0; k < 4; k++)
            {
                float a = k * Mathf.PI / 2 + Mathf.PI / 4;
                mb.Cylinder(p + new Vector3(Mathf.Cos(a) * 2.4f, 0, Mathf.Sin(a) * 2.4f), p + new Vector3(Mathf.Cos(a) * 0.4f, 14, Mathf.Sin(a) * 0.4f), 0.14f, 5, Palette.WoodDark);
            }
            for (float y = 2; y < 13; y += 2.6f)
            {
                float r = Mathf.Lerp(2.4f, 0.4f, y / 14f) * 1.41f;
                mb.Box(p + Vector3.up * y, new Vector3(r, 0.1f, 0.1f), Palette.WoodDark);
                mb.Box(p + Vector3.up * y, new Vector3(0.1f, 0.1f, r), Palette.WoodDark);
            }
            mb.Box(p + new Vector3(0, 0.8f, 3.4f), new Vector3(1.8f, 1.6f, 2.4f), Palette.Weathered); // engine house
        }

        static void Tank(MeshBuilder mb, Vector3 p)
        {
            p.y = G(p.x, p.z);
            mb.Paint();
            mb.Cylinder(p, p + Vector3.up * 4.2f, 2.6f, 16, Palette.Hex(0x3A3A3C));
            mb.Cylinder(p + Vector3.up * 4.2f, p + Vector3.up * 4.6f, 2.6f, 0.4f, 16, Palette.Hex(0x2A2A2C), true);
        }
    }
}
