using System.Collections.Generic;
using UnityEngine;

namespace Ironhorse.Game
{
    /// <summary>
    /// A densely sampled polyline with arc-length parameterisation. Trains are placed by distance along
    /// the path, so every car follows the rails exactly through curves.
    /// </summary>
    public sealed class TrackPath
    {
        public readonly List<Vector3> Points = new List<Vector3>();
        readonly List<float> _dist = new List<float>();
        public bool Closed { get; private set; }
        public float Length { get { return _dist.Count == 0 ? 0 : _dist[_dist.Count - 1]; } }

        public TrackPath Add(Vector3 p)
        {
            if (Points.Count > 0 && (Points[Points.Count - 1] - p).sqrMagnitude < 1e-6f) return this;
            Points.Add(p);
            return this;
        }

        public TrackPath Line(Vector3 to, float step = 2f)
        {
            if (Points.Count == 0) { Add(to); return this; }
            var from = Points[Points.Count - 1];
            int n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(from, to) / step));
            for (int i = 1; i <= n; i++) Add(Vector3.Lerp(from, to, i / (float)n));
            return this;
        }

        /// <summary>Cubic Bezier from the current end point.</summary>
        public TrackPath Bezier(Vector3 c1, Vector3 c2, Vector3 to, int steps = 32)
        {
            var p0 = Points[Points.Count - 1];
            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps, u = 1 - t;
                Add(u * u * u * p0 + 3 * u * u * t * c1 + 3 * u * t * t * c2 + t * t * t * to);
            }
            return this;
        }

        public TrackPath Ellipse(Vector3 center, float rx, float rz, int steps = 160)
        {
            for (int i = 0; i < steps; i++)
            {
                float a = i / (float)steps * Mathf.PI * 2;
                Add(center + new Vector3(Mathf.Cos(a) * rx, 0, Mathf.Sin(a) * rz));
            }
            Closed = true;
            return this;
        }

        public TrackPath Build()
        {
            _dist.Clear();
            float d = 0;
            for (int i = 0; i < Points.Count; i++)
            {
                if (i > 0) d += Vector3.Distance(Points[i - 1], Points[i]);
                _dist.Add(d);
            }
            if (Closed && Points.Count > 1)
            {
                d += Vector3.Distance(Points[Points.Count - 1], Points[0]);
                Points.Add(Points[0]);
                _dist.Add(d);
            }
            return this;
        }

        /// <summary>Position and forward direction at distance s. Extrapolates linearly past the ends.</summary>
        public void Sample(float s, out Vector3 pos, out Vector3 fwd)
        {
            int n = Points.Count;
            if (Closed) { s %= Length; if (s < 0) s += Length; }
            if (s <= 0)
            {
                fwd = (Points[1] - Points[0]).normalized;
                pos = Points[0] + fwd * s;
                return;
            }
            if (s >= Length)
            {
                fwd = (Points[n - 1] - Points[n - 2]).normalized;
                pos = Points[n - 1] + fwd * (s - Length);
                return;
            }
            int lo = 0, hi = n - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (_dist[mid] <= s) lo = mid; else hi = mid;
            }
            float seg = _dist[hi] - _dist[lo];
            float t = seg > 0 ? (s - _dist[lo]) / seg : 0;
            pos = Vector3.Lerp(Points[lo], Points[hi], t);
            fwd = (Points[hi] - Points[lo]).normalized;
        }

        public Vector3 PositionAt(float s) { Vector3 p, f; Sample(s, out p, out f); return p; }

        /// <summary>Distance along the path of the sample closest to p.</summary>
        public float Project(Vector3 p)
        {
            float best = float.MaxValue, bestS = 0;
            for (int i = 0; i < Points.Count; i++)
            {
                float d = (Points[i] - p).sqrMagnitude;
                if (d < best) { best = d; bestS = _dist[i]; }
            }
            return bestS;
        }

        /// <summary>Squared horizontal distance from (x,z) to the polyline.</summary>
        public float SqrDistanceXZ(float x, float z)
        {
            float best = float.MaxValue;
            for (int i = 1; i < Points.Count; i++)
            {
                Vector2 a = new Vector2(Points[i - 1].x, Points[i - 1].z), b = new Vector2(Points[i].x, Points[i].z), p = new Vector2(x, z);
                var ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                float d = (a + ab * t - p).sqrMagnitude;
                if (d < best) best = d;
            }
            return best;
        }
    }
}
