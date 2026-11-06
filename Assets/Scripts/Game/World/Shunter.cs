using System.Collections;
using UnityEngine;

namespace Ironhorse.Game
{
    /// <summary>
    /// The engine terminal's yard engine at work: it pulls out of its track onto the turntable, is turned to the lead,
    /// runs down to the main line and waits at the points, then backs up, is turned again and backs into another free
    /// stall or storage track. It never enters the main line, so it can't meet the player's trains.
    /// </summary>
    public sealed class Shunter : MonoBehaviour
    {
        Transform _engine;
        float _len;
        ParticleSystem _smoke;
        readonly System.Collections.Generic.List<(Transform t, float r)> _wheels = new System.Collections.Generic.List<(Transform, float)>();

        void Start()
        {
            var art = ModelRegistry.Spawn(ModelRegistry.Locomotives, Yard.Shunter_Id, transform);
            if (art == null || Turntable.Instance == null) { Destroy(gameObject); return; }
            _engine = new GameObject("Yard Engine").transform;
            _engine.SetParent(transform, false);
            var b = ModelRegistry.RendererBounds(art);
            art.transform.SetParent(_engine, true);
            art.transform.localPosition -= new Vector3(b.center.x, 0, b.center.z);
            _len = Mathf.Max(6, b.size.z);
            var stacks = ModelRegistry.Anchors(art, "Smoke");
            if (stacks.Count > 0) _smoke = Fx.Create(FxKind.Steam, stacks[0], Vector3.zero);
            foreach (var w in ModelRegistry.Anchors(art, "Wheel")) _wheels.Add((w, Mathf.Max(0.2f, ModelRegistry.RendererBounds(w.gameObject).size.y / 2)));
            StartCoroutine(Work());
        }

        void Smoke(bool on)
        {
            if (_smoke == null) return;
            var em = _smoke.emission;
            em.rateOverTimeMultiplier = on ? 14 : 3;
        }

        void Roll(float metres)
        {
            foreach (var (t, r) in _wheels) t.Rotate(metres / r * Mathf.Rad2Deg, 0, 0, Space.Self);
        }

        void Put(Vector3 pos, Vector3 nose) { _engine.SetPositionAndRotation(pos, Quaternion.LookRotation(nose.normalized)); }

        IEnumerator Work()
        {
            var free = Yard.FreeTracks();
            if (free.Count == 0) yield break;
            var at = free[Random.Range(0, free.Count)];
            Vector3 c = Yard.Centre;
            // Parked nose to the turntable, like the others.
            Put(Yard.TrackPoint(Yard.ParkRadius(at.stall, _len), at.ang), -Yard.Direction(at.ang));
            var tt = Turntable.Instance;
            while (true)
            {
                Smoke(false);
                yield return new WaitForSeconds(Random.Range(12f, 26f));
                // Line the bridge up with our track and pull forward onto it.
                tt.Manual = true;
                tt.Target = 90f - at.ang;
                while (!tt.Aligned) yield return null;
                Smoke(true);
                yield return Drive(_engine.position, c, 1);
                // Turn on the bridge until the nose points down the lead.
                yield return Turn(Yard.Direction(Yard.LeadAngle));
                // Out along the lead to just short of the points, wait for the road, come back.
                var lead = Yard.Lead;
                float end = Yard.LeadStop - _len / 2 - 5;
                yield return Drive(c, lead.PositionAt(0), 1);
                yield return Along(lead, 0, end, 1);
                Smoke(false);
                yield return new WaitForSeconds(Random.Range(6f, 11f));
                Smoke(true);
                yield return Along(lead, end, 0, -1);
                yield return Drive(lead.PositionAt(0), c, -1);
                // Turned to back into a different free track.
                free = Yard.FreeTracks();
                var next = free[Random.Range(0, free.Count)];
                if (free.Count > 1) while (Mathf.Abs(Mathf.DeltaAngle(next.ang, at.ang)) < 1) next = free[Random.Range(0, free.Count)];
                yield return Turn(-Yard.Direction(next.ang));
                yield return Drive(c, Yard.TrackPoint(Yard.ParkRadius(next.stall, _len), next.ang), -1);
                at = next;
                tt.Manual = false;
            }
        }

        /// <summary>Straight run between two points; dir 1 nose first, -1 backing (the nose keeps its heading).</summary>
        IEnumerator Drive(Vector3 from, Vector3 to, int dir)
        {
            float len = Vector3.Distance(from, to);
            if (len < 0.01f) yield break;
            var nose = (to - from).normalized * dir;
            float s = 0;
            while (s < len)
            {
                float step = Speed(s, len) * Time.deltaTime;
                s = Mathf.Min(len, s + step);
                Roll(step * dir);
                Put(Vector3.Lerp(from, to, s / len), nose);
                yield return null;
            }
        }

        IEnumerator Along(TrackPath p, float from, float to, int dir)
        {
            float len = Mathf.Abs(to - from), s = 0, sign = Mathf.Sign(to - from);
            while (s < len)
            {
                float step = Speed(s, len) * Time.deltaTime;
                s = Mathf.Min(len, s + step);
                Roll(step * dir);
                Vector3 pos, fwd;
                p.Sample(from + sign * s, out pos, out fwd);
                Put(pos, fwd); // the nose keeps pointing down the lead, out and back
                yield return null;
            }
        }

        static float Speed(float s, float len)
        {
            // Eased: creeps away, about 4 m/s at speed, eases to a stop.
            float edge = Mathf.Min(s, len - s);
            return Mathf.Lerp(0.6f, 4f, Mathf.Clamp01(edge / 6f));
        }

        /// <summary>Rides the bridge round until the engine's nose points along <paramref name="nose"/>.</summary>
        IEnumerator Turn(Vector3 nose)
        {
            var tt = Turntable.Instance;
            _engine.SetParent(tt.transform, true);
            float delta = Vector3.SignedAngle(_engine.forward.WithY0(), nose.WithY0(), Vector3.up);
            tt.Target = tt.transform.eulerAngles.y + delta;
            yield return null;
            while (!tt.Aligned) yield return null;
            _engine.SetParent(transform, true);
            Put(Yard.Centre, nose);
        }
    }

    static class VecExt
    {
        public static Vector3 WithY0(this Vector3 v) { return new Vector3(v.x, 0, v.z); }
    }
}
