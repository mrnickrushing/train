using System.Collections.Generic;
using Ironhorse.Core;
using UnityEngine;

namespace Ironhorse.Game
{
    /// <summary>
    /// Where the haulers drive and sail. Wagons leave the freight yard on the meadow between town and Silver Lake and
    /// follow dirt roads out to each project; boats cast off from the landing on the lake and steam up the river.
    /// Roads are smoothed through hand-placed waypoints and laid on the terrain, so the painted road, the vehicles
    /// and the scenery clearance all come from the same curve.
    /// </summary>
    public static class HaulRoutes
    {
        /// <summary>The teamsters' freight yard (wagons park here) and its gate onto the roads.</summary>
        public static readonly Vector3 Yard = new Vector3(-73, 0, 46);
        public static readonly Vector3 YardGate = new Vector3(-64, 0, 46);
        /// <summary>The boat landing: the end of the pier on the lake's north-east shore.</summary>
        public static readonly Vector3 Landing = new Vector3(-93, 0, 33);
        public static readonly Vector3 PierRoot = new Vector3(-83, 0, 40);
        /// <summary>The timber road bridge over the river below the dam (the road west to the mine).</summary>
        public static readonly Vector3 BridgeEast = new Vector3(-90, 0, 54), BridgeWest = new Vector3(-114, 0, 54);
        /// <summary>The level crossing over the station throat, west of the platforms.</summary>
        public const float CrossingX = -55f;

        static readonly Dictionary<string, Vector3[]> RoadPoints = new Dictionary<string, Vector3[]>
        {
            // North along the river's east bank, over the scenic loop, to the trestle camp.
            { "trestle", new[] { V(-64, 46), V(-62, 64), V(-56, 100), V(-50, 140), V(-46, 178), V(-52, 206), V(-64, 220) } },
            // A short spur up to the dam's east abutment.
            { "dam", new[] { V(-64, 46), V(-72, 58), V(-84, 66) } },
            // West over the river bridge and along the north side of the roundhouse to the mine.
            { "mineshaft", new[] { V(-64, 46), V(-76, 52), V(-90, 54), V(-114, 54), V(-138, 50), V(-166, 46), V(-192, 43) } },
            // East through town along the 38 m street, then out past the freight yard to the fort's depot.
            { "resupply", new[] { V(-64, 46), V(-56, 38), V(-20, 38), V(20, 38), V(56, 38), V(80, 42), V(106, 46), V(120, 50) } },
            // South down the west street, over the tracks at the station throat, across the badlands to the wire camp.
            { "telegraph", new[] { V(-64, 46), V(-56, 34), V(-50, 14), V(-50, -8), V(-55, -20), V(-55, -64), V(-40, -80), V(0, -88), V(36, -98), V(56, -108) } },
            // The same crossing, then west along the old wagon trail to the spike plaza.
            { "goldenspike", new[] { V(-64, 46), V(-56, 34), V(-50, 14), V(-50, -8), V(-55, -20), V(-55, -64), V(-72, -76), V(-110, -80), V(-140, -78) } },
        };

        static Vector3 V(float x, float z) { return new Vector3(x, 0, z); }

        static readonly Dictionary<string, TrackPath> _roads = new Dictionary<string, TrackPath>();
        static readonly Dictionary<string, TrackPath> _water = new Dictionary<string, TrackPath>();

        public static IEnumerable<string> RoadIds { get { return RoadPoints.Keys; } }

        /// <summary>The road from the yard gate to a project (null when no road goes there).</summary>
        public static TrackPath Road(string projectId)
        {
            TrackPath p;
            if (_roads.TryGetValue(projectId, out p)) return p;
            Vector3[] pts;
            if (!RoadPoints.TryGetValue(projectId, out pts)) return null;
            p = Smooth(pts, 2f, true);
            _roads[projectId] = p;
            return p;
        }

        /// <summary>The boat route from the landing to a project on the water (null when boats can't go there).</summary>
        public static TrackPath Water(string projectId)
        {
            TrackPath p;
            if (_water.TryGetValue(projectId, out p)) return p;
            var route = Haulage.Route(projectId);
            if (route == null || route.WaterMeters <= 0) return null;
            // Out of the landing into the lake, then up the river (River runs from its source down to the lake).
            var pts = new List<Vector3> { Landing, new Vector3(-100, 0, 30) };
            var river = WorldLayout.River;
            float stopZ = projectId == "dam" ? 60f : 214f; // below the dam wall / under the trestle
            for (float s = river.Length; s >= 0; s -= 6f)
            {
                var q = river.PositionAt(s);
                if (q.z < 36) continue;
                if (q.z > stopZ) break;
                pts.Add(new Vector3(q.x, 0, q.z));
            }
            p = Smooth(pts.ToArray(), 2f, false);
            _water[projectId] = p;
            return p;
        }

        /// <summary>The route a hauler takes to a project, or null.</summary>
        public static TrackPath For(HaulerDef d, string projectId) { return d.OnWater ? Water(projectId) : Road(projectId); }

        /// <summary>Catmull-Rom through the waypoints, sampled every <paramref name="step"/> metres; on land the
        /// curve sits on the terrain, on water at the surface.</summary>
        static TrackPath Smooth(Vector3[] pts, float step, bool onLand)
        {
            var path = new TrackPath();
            bool first = true;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                Vector3 p0 = pts[Mathf.Max(0, i - 1)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[Mathf.Min(pts.Length - 1, i + 2)];
                float len = Vector3.Distance(p1, p2);
                int n = Mathf.Max(2, Mathf.CeilToInt(len / step));
                for (int k = first ? 0 : 1; k <= n; k++)
                {
                    float t = k / (float)n;
                    var q = CatmullRom(p0, p1, p2, p3, t);
                    q.y = onLand ? ValleyTerrain.Height(q.x, q.z) : WorldLayout.WaterLevel;
                    if (first) { path.Add(q); first = false; }
                    else path.Line(q, 1000);
                }
            }
            return path.Build();
        }

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2 * p1 + (p2 - p0) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (3 * p1 - p0 - 3 * p2 + p3) * t3);
        }

        // ---------------------------------------------------------------- clearance

        const float Cell = 4f;
        static HashSet<long> _blocked;

        /// <summary>True on or beside a haul road, the yard or the landing (scenery stays off).</summary>
        public static bool Blocks(float x, float z)
        {
            if (_blocked == null) BuildBlocked();
            return _blocked.Contains(Key(Mathf.FloorToInt(x / Cell), Mathf.FloorToInt(z / Cell)));
        }

        static long Key(int cx, int cz) { return ((long)cx << 32) ^ (uint)cz; }

        static void BuildBlocked()
        {
            _blocked = new HashSet<long>();
            foreach (var id in RoadIds)
            {
                var road = Road(id);
                for (float s = 0; s <= road.Length; s += 1.5f) Mark(road.PositionAt(s), 5.5f);
            }
            Mark(Yard, 14f);
            for (float t = 0; t <= 1; t += 0.1f) Mark(Vector3.Lerp(PierRoot, Landing, t), 5f);
        }

        static void Mark(Vector3 p, float r)
        {
            int x0 = Mathf.FloorToInt((p.x - r) / Cell), x1 = Mathf.FloorToInt((p.x + r) / Cell);
            int z0 = Mathf.FloorToInt((p.z - r) / Cell), z1 = Mathf.FloorToInt((p.z + r) / Cell);
            for (int i = x0; i <= x1; i++)
                for (int j = z0; j <= z1; j++) _blocked.Add(Key(i, j));
        }
    }
}
