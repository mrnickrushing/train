using System;
using System.Collections;
using System.Collections.Generic;
using Ironhorse.Core;
using UnityEngine;

namespace Ironhorse.Game
{
    /// <summary>
    /// Territory projects as places in the valley that grow with every delivery: surveyor's stakes before
    /// work starts, a work camp and a part-built structure while it is under way, the finished landmark at
    /// the end. Each site is an ordered list of pieces; the share of the project's goods delivered decides
    /// how many stand. New pieces rise into place (camera flies over, dust, a hammer blow) once the player
    /// is back on the map.
    /// </summary>
    public sealed partial class ProjectSites : MonoBehaviour
    {
        public static ProjectSites Instance { get; private set; }

        struct Piece
        {
            public Vector3 Pivot;
            public Action<MeshBuilder> Build;
            public Piece(Vector3 pivot, Action<MeshBuilder> build) { Pivot = pivot; Build = build; }
        }

        sealed class Site
        {
            public string Id;
            public Vector3 Focus;
            public Func<int, Vector3> FocusAt; // where the work front is with n pieces up (long sites)
            public float Distance;
            public Vector3 FocusFor(int n) { return FocusAt != null ? FocusAt(n) : Focus; }
            public readonly List<Piece> Pieces = new List<Piece>();
            public Action<MeshBuilder> Stakes, Camp;
            public GameObject Built, Extras;
            public int Shown = -1;
            public int Extra = -1; // 0 none, 1 stakes, 2 camp
        }

        readonly List<Site> _sites = new List<Site>();
        ParticleSystem _dust;
        bool _animating;
        float _poll;

        // ---------------------------------------------------------------- layout (static: Scenery keeps trees off these)

        /// <summary>Crooked River Trestle: a cut-off line west across the river gorge, joining the scenic loop.</summary>
        const float TrestleZ = 225f, DeckY = 4.6f, SpanW = -99f, SpanE = -70f;
        static TrackPath _cutoff, _wire;

        static TrackPath Cutoff
        {
            get
            {
                if (_cutoff != null) return _cutoff;
                float loopX = -25f, loopZ = WorldLayout.LoopCenter.z + WorldLayout.LoopRz * Mathf.Sqrt(1 - loopX * loopX / (WorldLayout.LoopRx * WorldLayout.LoopRx));
                return _cutoff = new TrackPath()
                    .Add(new Vector3(-180, 0, TrestleZ))
                    .Line(new Vector3(-58, 0, TrestleZ))
                    .Bezier(new Vector3(-44, 0, TrestleZ), new Vector3(-46, 0, loopZ - 0.6f), new Vector3(loopX, 0, loopZ))
                    .Build();
            }
        }

        /// <summary>Telegraph to Fort Calder: south from the station's line through the badlands between the mesas.</summary>
        static TrackPath WireRoute
        {
            get
            {
                return _wire ?? (_wire = new TrackPath()
                    .Add(new Vector3(66, 0, -70))
                    .Line(new Vector3(60, 0, -112), 1f)
                    .Line(new Vector3(72, 0, -172), 1f)
                    .Build());
            }
        }

        /// <summary>True where a project site stands (no trees, cacti or rocks there).</summary>
        public static bool Blocks(float x, float z)
        {
            if (HaulRoutes.Blocks(x, z)) return true; // haul roads, the freight yard and the landing
            if (Cutoff.SqrDistanceXZ(x, z) < 8 * 8) return true;
            if (Mathf.Abs(z - TrestleZ) < 14 && x > SpanW - 8 && x < SpanE + 8) return true; // the gorge under the span
            if (WireRoute.SqrDistanceXZ(x, z) < 5 * 5) return true;
            if (CampRect.Contains(new Vector2(x, z)) || WireCampRect.Contains(new Vector2(x, z))) return true;
            foreach (var r in MoreSiteRects) if (r.Contains(new Vector2(x, z))) return true;
            return false;
        }

        static readonly Rect CampRect = new Rect(-118, 229, 14, 12);     // trestle work camp, north-west bank
        static readonly Rect WireCampRect = new Rect(70, -96, 12, 12);   // telegraph camp, east of the line

        // ---------------------------------------------------------------- lifecycle

        void Awake()
        {
            Instance = this;
            _dust = Fx.Create(FxKind.Dust, transform, Vector3.zero);
            var em = _dust.emission;
            em.rateOverTime = 0;
            var sh = _dust.shape;
            sh.scale = new Vector3(2.5f, 2.5f, 0.5f);
            _sites.Add(Trestle());
            _sites.Add(Telegraph());
            _sites.Add(Mine());
            _sites.Add(Depot());
            _sites.Add(Dam());
            _sites.Add(Spike());
        }

        void Start()
        {
            foreach (var s in _sites) Refresh(s, false);
        }

        void Update()
        {
            if (_animating) return;
            _poll -= Time.unscaledDeltaTime;
            if (_poll > 0) return;
            _poll = 0.4f;
            var ui = UIRoot.Instance;
            bool busy = ui == null || ui.WindowOpen || (Celebrations.Instance != null && Celebrations.Instance.Busy);
            foreach (var s in _sites)
            {
                int want = Target(s);
                if (want == s.Shown) { SetExtras(s); continue; }
                if (want < s.Shown || s.Shown < 0) { Refresh(s, false); continue; } // a reset: no fanfare
                if (busy) continue;                                                   // wait until the player is back on the map
                StartCoroutine(Raise(s, want));
                return;
            }
        }

        /// <summary>Fraction of a project's goods delivered, 0..1.</summary>
        public static float Built(string projectId)
        {
            var def = DepthContent.Project(projectId);
            if (def == null || G.Session == null) return 0;
            var pr = G.Session.Progress(projectId);
            if (pr.done) return 1;
            int total = 0, done = 0;
            for (int i = 0; i < def.Stages.Length; i++)
            {
                total += def.Stages[i].Amount;
                if (i < pr.stage) done += def.Stages[i].Amount;
            }
            done += pr.delivered;
            return total > 0 ? Mathf.Clamp01(done / (float)total) : 0;
        }

        int Target(Site s)
        {
            float f = Built(s.Id);
            if (f >= 1) return s.Pieces.Count;
            return Mathf.Min(s.Pieces.Count - 1, Mathf.FloorToInt(f * s.Pieces.Count + 0.0001f));
        }

        /// <summary>Flies the camera to a project's site (the Projects board's "View site").</summary>
        public void Show(string projectId)
        {
            var s = _sites.Find(x => x.Id == projectId);
            if (s != null) CameraRig.Instance.FlyTo(s.FocusFor(Mathf.Max(1, s.Shown)), s.Distance);
        }

        public bool HasSite(string projectId) { return _sites.Exists(x => x.Id == projectId); }

        /// <summary>Where a project's map pin floats (above the site), or null.</summary>
        public Vector3? PinAt(string projectId)
        {
            var s = _sites.Find(x => x.Id == projectId);
            return s == null ? (Vector3?)null : s.Focus + Vector3.up * 12f;
        }

        void Refresh(Site s, bool keepExtras)
        {
            s.Shown = Target(s);
            if (s.Built != null) Destroy(s.Built);
            var mb = new MeshBuilder();
            for (int i = 0; i < s.Shown; i++) s.Pieces[i].Build(mb);
            s.Built = mb.VertexCount > 0 ? mb.ToObject(s.Id + " (built)", transform) : null;
            if (!keepExtras) s.Extra = -1;
            SetExtras(s);
        }

        void SetExtras(Site s)
        {
            float f = Built(s.Id);
            int want = f >= 1 ? 0 : s.Shown == 0 ? 1 : 2;
            if (want == s.Extra) return;
            s.Extra = want;
            if (s.Extras != null) Destroy(s.Extras);
            s.Extras = null;
            if (want == 0) return;
            var mb = new MeshBuilder();
            (want == 1 ? s.Stakes : s.Camp)(mb);
            s.Extras = mb.ToObject(s.Id + (want == 1 ? " (stakes)" : " (camp)"), transform);
        }

        IEnumerator Raise(Site s, int want)
        {
            _animating = true;
            int from = s.Shown;
            if (want - from > 14) { Refresh(s, true); _animating = false; yield break; }
            CameraRig.Instance.FlyTo(s.FocusFor(want), s.Distance);
            yield return new WaitForSecondsRealtime(1.1f);
            SetExtras(s);
            var fresh = new List<Transform>();
            for (int i = from; i < want; i++)
            {
                var p = s.Pieces[i];
                var mb = new MeshBuilder();
                p.Build(mb);
                for (int v = 0; v < mb.Verts.Count; v++) mb.Verts[v] -= p.Pivot;
                var go = mb.ToObject(s.Id + " piece " + i, transform);
                go.transform.position = p.Pivot;
                fresh.Add(go.transform);
                StartCoroutine(Rise(go.transform));
                _dust.transform.position = p.Pivot;
                _dust.Emit(8);
                if (i - from < 6) AudioManager.Play(Sfx.Build, 0.55f);
                yield return new WaitForSecondsRealtime(0.16f);
            }
            yield return new WaitForSecondsRealtime(0.5f);
            s.Shown = want;
            if (s.Built != null) Destroy(s.Built);
            var all = new MeshBuilder();
            for (int i = 0; i < want; i++) s.Pieces[i].Build(all);
            s.Built = all.ToObject(s.Id + " (built)", transform);
            foreach (var t in fresh) Destroy(t.gameObject);
            SetExtras(s);
            var def = DepthContent.Project(s.Id);
            float f = Built(s.Id);
            if (UIRoot.Instance != null && def != null)
                UIRoot.Instance.Toast(f >= 1 ? def.Name + " is finished!" : def.Name + ": " + Mathf.RoundToInt(f * 100) + "% built", true);
            _animating = false;
        }

        static IEnumerator Rise(Transform t)
        {
            for (float k = 0; k < 1; k += Time.unscaledDeltaTime / 0.38f)
            {
                float y = 1 + 2.2f * Mathf.Pow(k - 1, 3) + 1.2f * Mathf.Pow(k - 1, 2); // ease out with a little overshoot
                t.localScale = new Vector3(1, Mathf.Max(0.02f, y), 1);
                yield return null;
            }
            t.localScale = Vector3.one;
        }

        // ================================================================ Crooked River Trestle

        static float Ground(float x, float z) { return ValleyTerrain.Height(x, z); }

        /// <summary>Rail height along the cut-off: on the deck over the gorge, easing to the ground either side.</summary>
        static float RailY(Vector3 p)
        {
            if (Mathf.Abs(p.z - TrestleZ) < 0.5f && p.x >= SpanW - 1.5f && p.x <= SpanE + 1.5f) return DeckY;
            float d = Mathf.Abs(p.z - TrestleZ) < 0.5f ? Mathf.Min(Mathf.Abs(p.x - (SpanW - 1.5f)), Mathf.Abs(p.x - (SpanE + 1.5f))) : 99;
            float g = Mathf.Max(Ground(p.x, p.z), 0) + WorldLayout.RailTop;
            return Mathf.Lerp(DeckY, g, Mathf.SmoothStep(0, 1, d / 14f));
        }

        Site Trestle()
        {
            var s = new Site { Id = "trestle", Focus = new Vector3(-84, 3, TrestleZ - 4), Distance = 72 };
            var path = Cutoff;
            float spanS0 = path.Project(new Vector3(SpanW - 1.5f, 0, TrestleZ)), spanS1 = path.Project(new Vector3(SpanE + 1.5f, 0, TrestleZ));

            // Stone abutments on each bank.
            foreach (float ax in new[] { SpanW - 1.5f, SpanE + 1.5f })
            {
                float x = ax;
                s.Pieces.Add(new Piece(new Vector3(x, Ground(x, TrestleZ) - 0.5f, TrestleZ), mb => Abutment(mb, x)));
            }
            // Timber bents across the gorge, west to east.
            var bentXs = new List<float>();
            for (float x = SpanW + 1.8f; x < SpanE - 1f; x += 3.6f) bentXs.Add(x);
            foreach (float bx in bentXs)
            {
                float x = bx;
                s.Pieces.Add(new Piece(new Vector3(x, Mathf.Max(Ground(x, TrestleZ), WorldLayout.WaterLevel - 2.6f), TrestleZ), mb => Bent(mb, x)));
            }
            // Stringers and deck planks between supports.
            var sup = new List<float> { SpanW - 1.5f };
            sup.AddRange(bentXs);
            sup.Add(SpanE + 1.5f);
            for (int i = 0; i + 1 < sup.Count; i++)
            {
                float a = sup[i], b = sup[i + 1];
                s.Pieces.Add(new Piece(new Vector3((a + b) / 2, DeckY - 0.6f, TrestleZ), mb => Deck(mb, a, b)));
            }
            // Rails over the span, then out along both approaches.
            s.Pieces.Add(new Piece(new Vector3((SpanW + SpanE) / 2, DeckY - 0.2f, TrestleZ), mb => { Rails(mb, path, spanS0, spanS1, true); GuardRails(mb); }));
            const int west = 3, east = 4;
            for (int i = 0; i < west; i++)
            {
                float a = spanS0 - (spanS0 / west) * (i + 1), b = spanS0 - (spanS0 / west) * i;
                Vector3 c = path.PositionAt((a + b) / 2);
                s.Pieces.Add(new Piece(new Vector3(c.x, RailY(c) - 0.6f, c.z), mb => Rails(mb, path, a, b, false)));
            }
            float rest = path.Length - spanS1;
            for (int i = 0; i < east; i++)
            {
                float a = spanS1 + rest / east * i, b = spanS1 + rest / east * (i + 1);
                Vector3 c = path.PositionAt((a + b) / 2);
                s.Pieces.Add(new Piece(new Vector3(c.x, RailY(c) - 0.6f, c.z), mb => Rails(mb, path, a, b, false)));
            }
            // Finishing touches: a water barrel and the name board at the west end of the span.
            s.Pieces.Add(new Piece(new Vector3(SpanW - 4, Ground(SpanW - 4, TrestleZ + 3), TrestleZ + 3), mb => NameBoard(mb, new Vector3(SpanW - 4, 0, TrestleZ + 3.4f))));

            s.Stakes = mb =>
            {
                for (float d = 0; d < path.Length; d += 9)
                {
                    var p = path.PositionAt(d);
                    if (p.x > SpanW - 1 && p.x < SpanE + 1 && Mathf.Abs(p.z - TrestleZ) < 1) continue; // nothing to stake in the river
                    Stake(mb, new Vector3(p.x, Mathf.Max(Ground(p.x, p.z), 0), p.z) + Vector3.forward * 2.2f);
                }
            };
            s.Camp = mb => Camp(mb, new Vector2(CampRect.center.x, CampRect.center.y), true);
            return s;
        }

        static void Abutment(MeshBuilder mb, float x)
        {
            // Founded down the gorge wall, so the river side shows a dressed-stone pier, not a slab on the bank.
            float toward = x < (SpanW + SpanE) / 2 ? 1 : -1;
            float g = Mathf.Min(Ground(x, TrestleZ), Ground(x + toward * 3f, TrestleZ)) - 0.8f;
            mb.Matte();
            mb.Surf = SurfaceKind.Stone;
            float h = DeckY - 0.25f - g;
            mb.Box(new Vector3(x, g + h / 2, TrestleZ), new Vector3(3.2f, h, 5.4f), Palette.StoneDark, Palette.StoneDark);
            for (float y = g + 1.2f; y < DeckY - 0.6f; y += 1.2f) // coursed joints
                mb.Box(new Vector3(x + toward * 1.61f, y, TrestleZ), new Vector3(0.02f, 0.06f, 5.4f), MeshBuilder.Darken(Palette.StoneDark, 0.7f));
            mb.Box(new Vector3(x, DeckY - 0.3f, TrestleZ), new Vector3(3.5f, 0.22f, 5.8f), Palette.Stone); // coping
            mb.Surf = SurfaceKind.Auto;
        }

        static void Bent(MeshBuilder mb, float x)
        {
            float g = Mathf.Max(Ground(x, TrestleZ), WorldLayout.WaterLevel - 2.6f) - 0.3f;
            float top = DeckY - 0.55f;
            mb.Matte();
            mb.Surf = SurfaceKind.WoodGrain;
            float[] topZ = { -1.5f, -0.5f, 0.5f, 1.5f };
            float[] botZ = { -2.7f, -0.6f, 0.6f, 2.7f }; // outer posts battered out for stability
            for (int i = 0; i < 4; i++)
                mb.Cylinder(new Vector3(x, g, TrestleZ + botZ[i]), new Vector3(x, top, TrestleZ + topZ[i]), 0.17f, 6, Palette.WoodDark);
            mb.Box(new Vector3(x, top + 0.12f, TrestleZ), new Vector3(0.36f, 0.3f, 3.8f), Palette.WoodDark); // cap
            mb.Box(new Vector3(x, g + 0.15f, TrestleZ), new Vector3(0.4f, 0.3f, 6f), Palette.WoodDark);      // sill
            // Sway bracing in tiers: X braces between the outer posts, alternating sides of the bent.
            int tier = 0;
            for (float y = g + 0.6f; y < top - 1.2f; y += 2.4f, tier++)
            {
                float y1 = Mathf.Min(top - 0.3f, y + 2.4f);
                float za = Mathf.Lerp(botZ[0], topZ[0], (y - g) / (top - g)), zb = Mathf.Lerp(botZ[0], topZ[0], (y1 - g) / (top - g));
                float off = (tier % 2 == 0 ? 0.2f : -0.2f);
                mb.Cylinder(new Vector3(x + off, y, TrestleZ + za), new Vector3(x + off, y1, TrestleZ - zb), 0.08f, 5, Palette.Wood);
                mb.Cylinder(new Vector3(x + off, y, TrestleZ - za), new Vector3(x + off, y1, TrestleZ + zb), 0.08f, 5, Palette.Wood);
                mb.Box(new Vector3(x, y1, TrestleZ), new Vector3(0.12f, 0.16f, Mathf.Abs(zb) * 2 + 0.6f), Palette.Wood); // girt
            }
            mb.Surf = SurfaceKind.Auto;
        }

        static void Deck(MeshBuilder mb, float a, float b)
        {
            mb.Matte();
            mb.Surf = SurfaceKind.WoodGrain;
            float len = b - a, cx = (a + b) / 2;
            foreach (float dz in new[] { -0.75f, 0.75f })
                mb.Box(new Vector3(cx, DeckY - 0.42f, TrestleZ + dz), new Vector3(len + 0.3f, 0.36f, 0.32f), Palette.WoodDark); // stringers
            mb.Box(new Vector3(cx, DeckY - 0.27f, TrestleZ + 2.05f), new Vector3(len + 0.1f, 0.06f, 0.6f), Palette.Weathered);  // walkway
            mb.Surf = SurfaceKind.Auto;
        }

        static void GuardRails(MeshBuilder mb)
        {
            mb.Matte();
            float x0 = SpanW - 1.5f, x1 = SpanE + 1.5f;
            for (float x = x0; x <= x1 + 0.01f; x += 2.4f)
                mb.Box(new Vector3(x, DeckY + 0.3f, TrestleZ + 2.35f), new Vector3(0.1f, 1.1f, 0.1f), Palette.WoodDark);
            mb.Box(new Vector3((x0 + x1) / 2, DeckY + 0.8f, TrestleZ + 2.35f), new Vector3(x1 - x0, 0.1f, 0.1f), Palette.Wood);
        }

        static readonly Color RailColor = Palette.Hex(0x5A4438), RailHead = Palette.Hex(0x96918B), Sleeper = Palette.Hex(0x54402F);

        /// <summary>Ballast (off the deck), sleepers and rails from s0 to s1 along the cut-off, at RailY.</summary>
        static void Rails(MeshBuilder mb, TrackPath path, float s0, float s1, bool onDeck)
        {
            const float step = 2f;
            if (!onDeck)
            {
                mb.Matte();
                mb.Surf = SurfaceKind.Gravel;
                for (float s = s0; s < s1 - 0.01f; s += step)
                {
                    float e = Mathf.Min(s1, s + step);
                    Vector3 p0, f0, p1, f1;
                    path.Sample(s, out p0, out f0);
                    path.Sample(e, out p1, out f1);
                    Vector3 r0 = Vector3.Cross(Vector3.up, f0).normalized, r1 = Vector3.Cross(Vector3.up, f1).normalized;
                    float y0 = RailY(p0) - 0.2f, y1 = RailY(p1) - 0.2f;
                    Vector3 a = new Vector3(p0.x, y0, p0.z), b = new Vector3(p1.x, y1, p1.z);
                    mb.Quad(a - r0 * 1.6f, b - r1 * 1.6f, b + r1 * 1.6f, a + r0 * 1.6f, Palette.Ballast);
                    mb.Quad(a - r0 * 2.7f + Vector3.down * 0.9f, b - r1 * 2.7f + Vector3.down * 0.9f, b - r1 * 1.6f, a - r0 * 1.6f, Palette.Hex(0x655D54));
                    mb.Quad(a + r0 * 1.6f, b + r1 * 1.6f, b + r1 * 2.7f + Vector3.down * 0.9f, a + r0 * 2.7f + Vector3.down * 0.9f, Palette.Hex(0x655D54));
                }
            }
            mb.Matte();
            mb.Surf = SurfaceKind.WoodGrain;
            for (float s = s0 + 0.35f; s < s1; s += 0.75f)
            {
                Vector3 p, f;
                path.Sample(s, out p, out f);
                var r = Vector3.Cross(Vector3.up, f).normalized;
                float y = RailY(p) - 0.17f;
                var c = new Vector3(p.x, y, p.z);
                float h = Mathf.Repeat(Mathf.Sin(s * 12.9898f) * 43758.5453f, 1f);
                float w = onDeck ? 3.0f : 2.5f;
                // A sleeper as a quad box aligned with the track: top face plus the two long sides.
                Vector3 hf = f * 0.15f, hr = r * (w / 2), up = Vector3.up * 0.14f;
                var col = MeshBuilder.Darken(Sleeper, 0.8f + 0.35f * h);
                mb.Quad(c - hr - hf + up * 0.5f, c - hr + hf + up * 0.5f, c + hr + hf + up * 0.5f, c + hr - hf + up * 0.5f, col);
                mb.Quad(c - hr + hf - up * 0.5f, c + hr + hf - up * 0.5f, c + hr + hf + up * 0.5f, c - hr + hf + up * 0.5f, col);
                mb.Quad(c + hr - hf - up * 0.5f, c - hr - hf - up * 0.5f, c - hr - hf + up * 0.5f, c + hr - hf + up * 0.5f, col);
            }
            mb.Surf = SurfaceKind.None;
            for (int side = -1; side <= 1; side += 2)
            {
                float off = side * TrackBuilder.Gauge * 0.5f;
                for (float s = s0; s < s1 - 0.01f; s += step)
                {
                    float e = Mathf.Min(s1, s + step);
                    Vector3 p0, f0, p1, f1;
                    path.Sample(s, out p0, out f0);
                    path.Sample(e, out p1, out f1);
                    Vector3 r0 = Vector3.Cross(Vector3.up, f0).normalized, r1 = Vector3.Cross(Vector3.up, f1).normalized;
                    Vector3 c0 = new Vector3(p0.x, RailY(p0), p0.z) + r0 * off, c1 = new Vector3(p1.x, RailY(p1), p1.z) + r1 * off;
                    const float w = 0.09f, h = 0.16f;
                    mb.Mat(0.38f, 0.7f);
                    mb.Quad(c0 - r0 * w, c1 - r1 * w, c1 + r1 * w, c0 + r0 * w, RailHead);
                    mb.Mat(0.2f, 0.2f);
                    mb.Quad(c0 - r0 * w - Vector3.up * h, c1 - r1 * w - Vector3.up * h, c1 - r1 * w, c0 - r0 * w, RailColor);
                    mb.Quad(c0 + r0 * w, c1 + r1 * w, c1 + r1 * w - Vector3.up * h, c0 + r0 * w - Vector3.up * h, RailColor);
                }
            }
            mb.Matte();
            mb.Surf = SurfaceKind.Auto;
        }

        static void NameBoard(MeshBuilder mb, Vector3 at)
        {
            float g = Ground(at.x, at.z);
            mb.Matte();
            mb.Cylinder(new Vector3(at.x - 1.1f, g, at.z), new Vector3(at.x - 1.1f, g + 2.4f, at.z), 0.1f, 5, Palette.WoodDark);
            mb.Cylinder(new Vector3(at.x + 1.1f, g, at.z), new Vector3(at.x + 1.1f, g + 2.4f, at.z), 0.1f, 5, Palette.WoodDark);
            mb.Box(new Vector3(at.x, g + 2.0f, at.z), new Vector3(2.8f, 0.8f, 0.1f), Palette.Cream);
            mb.Box(new Vector3(at.x, g + 2.0f, at.z + 0.06f), new Vector3(2.3f, 0.14f, 0.02f), Palette.Black); // painted lettering, read as a stripe
            // Water barrel for the engines' fire crews.
            mb.Cylinder(new Vector3(at.x + 2.4f, g, at.z - 0.4f), new Vector3(at.x + 2.4f, g + 1.1f, at.z - 0.4f), 0.45f, 10, Palette.Wood);
            mb.Cylinder(new Vector3(at.x + 2.4f, g + 0.25f, at.z - 0.4f), new Vector3(at.x + 2.4f, g + 0.32f, at.z - 0.4f), 0.47f, 10, Palette.Iron, false);
            mb.Cylinder(new Vector3(at.x + 2.4f, g + 0.85f, at.z - 0.4f), new Vector3(at.x + 2.4f, g + 0.92f, at.z - 0.4f), 0.47f, 10, Palette.Iron, false);
        }

        // ================================================================ Telegraph to Fort Calder

        const float PolePitch = 12f, PoleH = 8.4f;

        Site Telegraph()
        {
            var s = new Site { Id = "telegraph", Focus = new Vector3(64, 2, -112), Distance = 95 };
            var route = WireRoute;
            var poles = new List<Vector3>();
            for (float d = 0; d <= route.Length + 0.01f; d += PolePitch)
            {
                var p = route.PositionAt(d);
                poles.Add(new Vector3(p.x, Mathf.Max(Ground(p.x, p.z), 0), p.z));
            }
            for (int i = 0; i < poles.Count; i++)
            {
                var p = poles[i];
                Vector3 fwd = i + 1 < poles.Count ? poles[i + 1] - p : p - poles[i - 1];
                fwd.y = 0;
                var right = Vector3.Cross(Vector3.up, fwd.normalized);
                s.Pieces.Add(new Piece(p, mb => Pole(mb, p, right)));
                if (i > 0)
                {
                    var a = poles[i - 1];
                    Vector3 fa = p - a;
                    fa.y = 0;
                    var ra = Vector3.Cross(Vector3.up, fa.normalized);
                    s.Pieces.Add(new Piece((a + p) / 2 + Vector3.up * (PoleH - 1), mb => Wires(mb, a, p, ra)));
                }
            }
            s.FocusAt = n =>
            {
                int pole = Mathf.Clamp((n + 1) / 2, 0, poles.Count - 1); // pieces alternate pole, wire
                return Vector3.Lerp(poles[Mathf.Max(0, pole - 2)], poles[pole], 0.5f);
            };
            var first = poles[0];
            s.Pieces.Add(new Piece(first + new Vector3(3, 0, -2), mb => MileSign(mb, first + new Vector3(3, 0, -2))));

            s.Stakes = mb => { foreach (var p in poles) Stake(mb, p + Vector3.right * 0.8f); };
            s.Camp = mb => Camp(mb, new Vector2(WireCampRect.center.x, WireCampRect.center.y), false);
            return s;
        }

        static void Pole(MeshBuilder mb, Vector3 foot, Vector3 right)
        {
            mb.Matte();
            mb.Surf = SurfaceKind.WoodGrain;
            var top = foot + Vector3.up * PoleH;
            var pole = Palette.Hex(0x6E5A44); // weathered pine, not black: reads as a pole from the game camera
            mb.Cylinder(foot + Vector3.down * 0.3f, top + Vector3.up * 0.3f, 0.27f, 0.19f, 7, pole, true);
            foreach (float y in new[] { -0.2f, -1.0f })
            {
                var c = top + Vector3.up * y;
                mb.Cylinder(c - right * 1.25f, c + right * 1.25f, 0.12f, 5, MeshBuilder.Darken(pole, 0.9f));
                mb.Mat(0.6f);
                foreach (float k in new[] { -1.05f, -0.4f, 0.4f, 1.05f })
                    mb.Cylinder(c + right * k, c + right * k + Vector3.up * 0.26f, 0.07f, 0.05f, 6, Palette.Hex(0x7FB3A6), true); // glass insulators
                mb.Matte();
            }
            mb.Surf = SurfaceKind.Auto;
        }

        static void Wires(MeshBuilder mb, Vector3 a, Vector3 b, Vector3 right)
        {
            mb.Mat(0.4f, 0.5f);
            foreach (float y in new[] { -0.2f, -1.0f })
                foreach (float k in new[] { -1.05f, 1.05f })
                {
                    Vector3 p0 = a + Vector3.up * (PoleH + y + 0.26f) + right * k, p1 = b + Vector3.up * (PoleH + y + 0.26f) + right * k;
                    Vector3 prev = p0;
                    for (int i = 1; i <= 6; i++)
                    {
                        float t = i / 6f;
                        var q = Vector3.Lerp(p0, p1, t) + Vector3.down * (0.7f * 4 * t * (1 - t)); // sag
                        mb.Cylinder(prev, q, 0.05f, 3, Palette.Iron, false);
                        prev = q;
                    }
                }
            mb.Matte();
        }

        static void MileSign(MeshBuilder mb, Vector3 foot)
        {
            float g = Mathf.Max(Ground(foot.x, foot.z), 0);
            mb.Matte();
            mb.Cylinder(new Vector3(foot.x, g, foot.z), new Vector3(foot.x, g + 2.6f, foot.z), 0.1f, 5, Palette.WoodDark);
            mb.Push().SetMatrix(Matrix4x4.TRS(new Vector3(foot.x, g + 2.2f, foot.z), Quaternion.Euler(0, 15, 0), Vector3.one));
            mb.Box(new Vector3(0.7f, 0, 0), new Vector3(1.8f, 0.45f, 0.08f), Palette.Cream); // "FORT CALDER" arrow board
            mb.Box(new Vector3(1.75f, 0, 0), new Vector3(0.3f, 0.3f, 0.08f), Palette.Cream);
            mb.Box(new Vector3(0.6f, 0, 0.05f), new Vector3(1.3f, 0.1f, 0.02f), Palette.Black);
            mb.Pop();
        }

        // ================================================================ shared set dressing

        static void Stake(MeshBuilder mb, Vector3 foot)
        {
            mb.Matte();
            mb.Box(foot + Vector3.up * 1.0f, new Vector3(0.16f, 2.0f, 0.16f), Palette.WoodPale);
            mb.Box(foot + new Vector3(0.34f, 1.75f, 0), new Vector3(0.6f, 0.4f, 0.03f), Palette.PaintRed);   // survey flag
            mb.Box(foot + new Vector3(0.34f, 1.75f, 0.02f), new Vector3(0.6f, 0.12f, 0.02f), Palette.White);
        }

        /// <summary>Work camp: a canvas tent, the material pile for the job, a tool crate and a flag.</summary>
        static void Camp(MeshBuilder mb, Vector2 c, bool timber) { Camp(mb, c, timber ? CampKind.Timber : CampKind.Poles); }

        enum CampKind { Timber, Poles, Stone, Crates }

        static void Camp(MeshBuilder mb, Vector2 c, CampKind kind)
        {
            float g = Mathf.Max(Ground(c.x, c.y), 0);
            var o = new Vector3(c.x, g, c.y);
            mb.Matte();
            // Wall tent.
            mb.Box(o + new Vector3(-2.5f, 0.6f, 0), new Vector3(3.2f, 1.2f, 2.6f), Palette.Cream);
            mb.GableRoof(o + new Vector3(-2.5f, 1.2f, 0), new Vector3(3.4f, 1.1f, 2.8f), Palette.Hex(0xE6DCC4), Palette.Cream, 0.1f);
            // Material pile: squared timbers, poles and a cable drum, dressed stone, or freight crates.
            if (kind == CampKind.Stone)
            {
                for (int row = 0; row < 2; row++)
                    for (int i = 0; i < 3; i++)
                        for (int j = 0; j < 2; j++)
                            mb.Box(o + new Vector3(1.4f + i * 1.25f, 0.4f + row * 0.8f, -1.2f + j * 1.05f), new Vector3(1.15f, 0.75f, 0.95f), MeshBuilder.Darken(Palette.Stone, 0.85f + 0.06f * ((i + j + row) % 3)));
                mb.Cylinder(o + new Vector3(4.9f, 0, 1.6f), o + new Vector3(4.9f, 4.2f, 1.6f), 0.12f, 5, Palette.WoodDark); // derrick mast
                mb.Cylinder(o + new Vector3(4.9f, 3.9f, 1.6f), o + new Vector3(1.8f, 2.6f, 1.6f), 0.08f, 5, Palette.WoodDark);
            }
            else if (kind == CampKind.Crates)
            {
                for (int i = 0; i < 5; i++)
                    mb.Box(o + new Vector3(1.4f + (i % 3) * 1.1f, 0.5f + (i / 3) * 1.0f, -1.0f + (i % 2) * 0.3f), new Vector3(1.0f, 0.95f, 1.0f), MeshBuilder.Darken(Palette.WoodPale, 0.85f + 0.05f * i));
            }
            else if (kind == CampKind.Timber)
            {
                for (int row = 0; row < 3; row++)
                    for (int i = 0; i < 4 - row; i++)
                        mb.Box(o + new Vector3(2.2f, 0.2f + row * 0.36f, -1.2f + i * 0.42f + row * 0.21f), new Vector3(5.5f, 0.34f, 0.36f), MeshBuilder.Darken(Palette.Wood, 0.85f + 0.05f * i));
            }
            else
            {
                for (int row = 0; row < 3; row++)
                    for (int i = 0; i < 4 - row; i++)
                    {
                        var p = o + new Vector3(0.6f, 0.2f + row * 0.36f, -1.6f + i * 0.4f + row * 0.2f);
                        mb.Cylinder(p, p + Vector3.right * 6.5f, 0.18f, 6, Palette.WoodDark);
                    }
                mb.Cylinder(o + new Vector3(3.6f, 0.7f, 1.9f), o + new Vector3(3.6f, 0.7f, 2.6f), 0.7f, 12, Palette.Wood);   // cable drum
                mb.Cylinder(o + new Vector3(3.6f, 0.7f, 1.95f), o + new Vector3(3.6f, 0.7f, 2.55f), 0.55f, 12, Palette.Copper, false);
            }
            mb.Box(o + new Vector3(-0.2f, 0.3f, 1.9f), new Vector3(0.9f, 0.6f, 0.6f), Palette.WoodDark); // tool crate
            mb.Cylinder(o + new Vector3(-4.6f, 0, 1.6f), o + new Vector3(-4.6f, 3.4f, 1.6f), 0.05f, 4, Palette.WoodDark);
            mb.Box(o + new Vector3(-4.2f, 3.1f, 1.6f), new Vector3(0.75f, 0.45f, 0.02f), Palette.PaintRed); // company flag
        }
    }
}
