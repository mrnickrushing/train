using System.Collections.Generic;
using UnityEngine;

namespace Ironhorse.Game
{
    /// <summary>
    /// Townsfolk milling about the platforms and Main Street, and horse-drawn wagons driving the town
    /// roads. Small touches of movement everywhere make the diorama feel alive.
    /// </summary>
    public sealed class Life : MonoBehaviour
    {
        sealed class Walker
        {
            public Transform T;
            public Rect Area;
            public float Y;
            public Vector3 Target;
            public float Speed, Wait, Phase;
            public Animation Anim;   // drop-in art: Walk / Idle clips
            public float GroundTimer;
        }

        sealed class Cart
        {
            public Transform T, Horse;
            public TrackPath Path;
            public float S, Speed;
            public readonly List<Animation> Team = new List<Animation>(); // art horses (Walk clip)
        }

        readonly List<Walker> _walkers = new List<Walker>();
        readonly List<Cart> _carts = new List<Cart>();
        readonly List<Mesh> _people = new List<Mesh>();
        System.Random _rng = new System.Random(77);
        float R() { return (float)_rng.NextDouble(); }
        float R(float a, float b) { return a + (b - a) * R(); }

        static readonly string[] Folk = { "gent", "lady", "cowboy", "worker" };
        readonly List<string> _art = new List<string>();

        void Start()
        {
            for (int i = 0; i < 10; i++) _people.Add(PersonMesh(i));
            foreach (var id in Folk) if (ModelRegistry.Has("People", id)) _art.Add(id);

            float island1 = (WorldLayout.PlatformZ(1) + WorldLayout.PlatformZ(2)) / 2;
            float island2 = (WorldLayout.PlatformZ(3) + WorldLayout.PlatformZ(4)) / 2;
            float half = WorldLayout.PlatformHalfLength - 4;
            Spawn(new Rect(-half, -26.6f, half * 2, 3.2f), 0.9f, 26);           // depot side platform
            Spawn(new Rect(-half + 4, island1 - 0.7f, half * 2 - 8, 1.4f), 0.9f, 12);
            Spawn(new Rect(-half + 4, island2 - 0.7f, half * 2 - 8, 1.4f), 0.9f, 10);
            Spawn(new Rect(-16, -17.4f, 32, 5.5f), 0.08f, 12);                  // station plaza
            Spawn(new Rect(-50, -12.5f, 100, 4.5f), 0.08f, 22);                 // Main Street
            Spawn(new Rect(WorldLayout.YardRect.xMin + 6, WorldLayout.SidingZ(2) + 2.2f, 90, 1.2f), 0.08f, 8); // yard hands

            float z0 = WorldLayout.PlotOriginZ - WorldLayout.PlotPitchZ / 2;
            float xl = WorldLayout.PlotOriginX - WorldLayout.PlotPitchX / 2, xr = xl + WorldLayout.PlotCols * WorldLayout.PlotPitchX;
            float zt = z0 + 4 * WorldLayout.PlotPitchZ;
            var loop = new TrackPath();
            loop.Add(new Vector3(xl, 0.1f, z0)).Line(new Vector3(xr, 0.1f, z0)).Line(new Vector3(xr, 0.1f, zt)).Line(new Vector3(xl, 0.1f, zt)).Line(new Vector3(xl, 0.1f, z0 + 0.5f));
            loop.Build();
            for (int i = 0; i < 4; i++) SpawnCart(loop, loop.Length * i / 4f + R(0, 10), i);
        }

        // ---------------------------------------------------------------- people

        Mesh PersonMesh(int variant)
        {
            Color[] coats = { Palette.Hex(0x3C4A5E), Palette.Hex(0x6B3A2A), Palette.Hex(0x4F6B45), Palette.Hex(0x2B2B2E), Palette.Hex(0x8C6A3A), Palette.Hex(0x7A2E3A) };
            Color[] dresses = { Palette.Hex(0xA8382B), Palette.Hex(0x3C5A7A), Palette.Hex(0xD9C49A), Palette.Hex(0x7E4BA6) };
            Color[] skins = { Palette.Hex(0xE8C4A0), Palette.Hex(0xC8946A), Palette.Hex(0x8C5A3A), Palette.Hex(0xF0D2B4) };
            var mb = new MeshBuilder();
            mb.Matte();
            bool dress = variant % 3 == 1;
            Color body = dress ? dresses[variant % dresses.Length] : coats[variant % coats.Length];
            Color skin = skins[variant % skins.Length];
            if (dress) mb.Cylinder(Vector3.zero, Vector3.up * 1.05f, 0.42f, 0.2f, 8, body);
            else
            {
                mb.Box(new Vector3(-0.11f, 0.42f, 0), new Vector3(0.17f, 0.84f, 0.2f), Palette.Hex(0x3A3028));
                mb.Box(new Vector3(0.11f, 0.42f, 0), new Vector3(0.17f, 0.84f, 0.2f), Palette.Hex(0x3A3028));
            }
            mb.Box(new Vector3(0, 1.2f, 0), new Vector3(0.48f, 0.72f, 0.28f), body);
            mb.Sphere(new Vector3(0, 1.72f, 0), new Vector3(0.17f, 0.2f, 0.17f), 8, skin);
            // Hats: stetson, bowler or bonnet
            Color hat = variant % 2 == 0 ? Palette.Hex(0x5C4030) : Palette.Hex(0x2A2420);
            if (dress) mb.Sphere(new Vector3(0, 1.82f, -0.02f), new Vector3(0.22f, 0.16f, 0.24f), 8, MeshBuilder.Lighten(body, 0.3f));
            else
            {
                mb.Cylinder(new Vector3(0, 1.84f, 0), new Vector3(0, 1.87f, 0), 0.32f, 10, hat);
                mb.Cylinder(new Vector3(0, 1.86f, 0), new Vector3(0, 2.02f, 0), 0.16f, 0.13f, 8, hat);
            }
            return mb.ToMesh("Person " + variant);
        }

        void Spawn(Rect area, float y, int count)
        {
            for (int i = 0; i < count; i++)
            {
                GameObject go;
                Animation anim = null;
                if (_art.Count > 0)
                {
                    go = ModelRegistry.Spawn("People", _art[_rng.Next(_art.Count)], transform);
                    anim = go.GetComponentInChildren<Animation>();
                    foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        smr.updateWhenOffscreen = false;
                        smr.quality = SkinQuality.Bone2;
                    }
                }
                else
                {
                    go = new GameObject("Person");
                    go.transform.SetParent(transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = _people[_rng.Next(_people.Count)];
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = Materials.World;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }
                var w = new Walker { T = go.transform, Area = area, Y = y, Speed = R(0.9f, 1.5f), Phase = R(0, 10), Anim = anim };
                if (anim != null)
                {
                    foreach (AnimationState st in anim) { st.wrapMode = WrapMode.Loop; st.time = R(0, st.length); }
                    Play(w, "Idle");
                }
                w.T.position = RandomIn(area, y);
                w.Y = Ground(w.T.position, y);
                w.T.position = new Vector3(w.T.position.x, w.Y, w.T.position.z);
                w.Target = w.T.position;
                w.Wait = R(0, 8);
                go.transform.localScale = Vector3.one * R(0.92f, 1.06f);
                _walkers.Add(w);
            }
        }

        Vector3 RandomIn(Rect r, float y) { return new Vector3(R(r.xMin, r.xMax), y, R(r.yMin, r.yMax)); }

        const int GroundMask = (1 << 0) | (1 << Station.WalkLayer);

        /// <summary>Height of whatever is underfoot (terrain, streets, platforms, the station floor).</summary>
        static float Ground(Vector3 p, float hint)
        {
            // Start just above head height so canopies and roofs overhead are never mistaken for the floor.
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(p.x, hint + 1.6f, p.z), Vector3.down, out hit, 4f, GroundMask, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return hint;
        }

        static void Play(Walker w, string clip)
        {
            if (w.Anim == null) return;
            foreach (AnimationState st in w.Anim)
                if (st.name.EndsWith(clip)) { if (!w.Anim.IsPlaying(st.name)) w.Anim.CrossFade(st.name, 0.25f); return; }
        }

        // ---------------------------------------------------------------- carts

        void SpawnCart(TrackPath path, float s, int i)
        {
            if (ModelRegistry.Has("Props", "covered_wagon") && ModelRegistry.Has("Animals", "horse")) { SpawnArtCart(path, s); return; }
            var mb = new MeshBuilder();
            mb.Matte();
            Color wood = i % 2 == 0 ? Palette.Wood : Palette.PaintRed;
            mb.Box(new Vector3(0, 1.0f, -0.6f), new Vector3(1.6f, 0.5f, 3.2f), wood);
            mb.Box(new Vector3(0, 0.75f, 1.4f), new Vector3(1.0f, 0.2f, 1.2f), Palette.WoodDark); // tongue
            if (i % 2 == 0)
            {
                // Canvas cover
                for (int k = 0; k < 8; k++)
                {
                    float a0 = Mathf.PI * k / 8, a1 = Mathf.PI * (k + 1) / 8;
                    Vector3 p0 = new Vector3(Mathf.Cos(a0) * 0.85f, 1.25f + Mathf.Sin(a0) * 1.0f, 0), p1 = new Vector3(Mathf.Cos(a1) * 0.85f, 1.25f + Mathf.Sin(a1) * 1.0f, 0);
                    mb.Quad(p0 + Vector3.forward * 0.8f, p1 + Vector3.forward * 0.8f, p1 + Vector3.back * 2.0f, p0 + Vector3.back * 2.0f, Palette.Cream);
                    mb.Quad(p0 + Vector3.back * 2.0f, p1 + Vector3.back * 2.0f, p1 + Vector3.forward * 0.8f, p0 + Vector3.forward * 0.8f, Palette.Cream);
                }
            }
            else
            {
                Station.Barrel(mb, new Vector3(-0.35f, 1.25f, -1.2f));
                Station.Crate(mb, new Vector3(0.3f, 1.25f, -0.2f), 10);
            }
            for (int s2 = -1; s2 <= 1; s2 += 2)
                for (int a = 0; a < 2; a++)
                    mb.Cylinder(new Vector3(s2 * 0.85f, 0.55f, a == 0 ? 0.4f : -1.7f), new Vector3(s2 * 0.95f, 0.55f, a == 0 ? 0.4f : -1.7f), 0.5f, 10, Palette.WoodDark);
            var cart = mb.ToObject("Cart", transform);

            var hb = new MeshBuilder();
            hb.Matte();
            Color coat = i % 3 == 0 ? Palette.Hex(0x5A3A24) : i % 3 == 1 ? Palette.Hex(0x2B2420) : Palette.Hex(0xA87A50);
            hb.Box(new Vector3(0, 1.45f, 0), new Vector3(0.6f, 0.75f, 1.8f), coat);
            hb.Push().Translate(0, 1.85f, 1.05f).Rotate(Quaternion.Euler(-40, 0, 0));
            hb.Box(Vector3.zero, new Vector3(0.36f, 0.9f, 0.45f), coat);
            hb.Pop();
            hb.Box(new Vector3(0, 2.2f, 1.45f), new Vector3(0.3f, 0.32f, 0.65f), coat);
            for (int l = 0; l < 4; l++) hb.Box(new Vector3(l % 2 == 0 ? -0.2f : 0.2f, 0.55f, l < 2 ? 0.7f : -0.7f), new Vector3(0.14f, 1.1f, 0.14f), MeshBuilder.Darken(coat, 0.7f));
            var horse = hb.ToObject("Horse", cart.transform);
            horse.transform.localPosition = new Vector3(0, 0, 3.1f);

            _carts.Add(new Cart { T = cart.transform, Horse = horse.transform, Path = path, S = s, Speed = R(2.6f, 3.6f) });
        }

        /// <summary>Covered wagon drawn by a walking team of two draft horses, one each side of the pole.</summary>
        void SpawnArtCart(TrackPath path, float s)
        {
            var cart = new GameObject("Cart");
            cart.transform.SetParent(transform, false);
            var wagon = ModelRegistry.Spawn("Props", "covered_wagon", cart.transform);
            var b = ModelRegistry.RendererBounds(wagon);
            wagon.transform.localPosition -= new Vector3(b.center.x, 0, b.center.z);
            var c = new Cart { T = cart.transform, Path = path, S = s, Speed = R(1.6f, 2.1f) };
            float poleTip = b.size.z / 2;
            foreach (float x in new[] { -0.56f, 0.56f })
            {
                var horse = ModelRegistry.Spawn("Animals", "horse", cart.transform);
                horse.transform.localPosition += new Vector3(x, 0, poleTip - 1.1f);
                foreach (var smr in horse.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    smr.updateWhenOffscreen = false;
                    smr.quality = SkinQuality.Bone2;
                }
                var anim = horse.GetComponentInChildren<Animation>();
                if (anim == null) continue;
                foreach (AnimationState st in anim) { st.wrapMode = WrapMode.Loop; st.time = R(0, st.length); }
                anim.Play();
                c.Team.Add(anim);
            }
            _carts.Add(c);
        }

        // ---------------------------------------------------------------- update

        void Update()
        {
            float dt = Time.deltaTime;
            foreach (var w in _walkers)
            {
                var p = w.T.position;
                var to = w.Target - p;
                to.y = 0;
                if (to.sqrMagnitude < 0.05f)
                {
                    Play(w, "Idle");
                    w.Wait -= dt;
                    if (w.Wait <= 0)
                    {
                        w.Target = RandomIn(w.Area, w.Y);
                        w.Wait = R(2, 12);
                    }
                    // Idle sway
                    w.T.rotation = Quaternion.Euler(0, w.T.eulerAngles.y + Mathf.Sin(Time.time + w.Phase) * 0.2f, 0);
                    continue;
                }
                var step = to.normalized * w.Speed * dt;
                if (step.sqrMagnitude > to.sqrMagnitude) step = to;
                p += step;
                w.GroundTimer -= dt;
                if (w.GroundTimer <= 0) { w.GroundTimer = 0.2f; w.Y = Ground(p, w.Y); }
                if (w.Anim != null)
                {
                    Play(w, "Walk");
                    foreach (AnimationState st in w.Anim) if (st.name.EndsWith("Walk")) st.speed = w.Speed / 1.3f; // one stride ≈ 1.3 m
                    p.y = w.Y;
                }
                else p.y = w.Y + Mathf.Abs(Mathf.Sin((Time.time + w.Phase) * 7f)) * 0.05f; // walking bob
                w.T.position = p;
                w.T.rotation = Quaternion.Slerp(w.T.rotation, Quaternion.LookRotation(to.normalized), dt * 6f);
            }

            foreach (var c in _carts)
            {
                c.S += c.Speed * dt;
                Vector3 pos, fwd;
                c.Path.Sample(c.S % c.Path.Length, out pos, out fwd);
                Vector3 ahead = c.Path.PositionAt((c.S + 3f) % c.Path.Length);
                c.T.position = pos;
                var dir = ahead - pos;
                dir.y = 0;
                if (dir.sqrMagnitude > 0.001f) c.T.rotation = Quaternion.Slerp(c.T.rotation, Quaternion.LookRotation(dir.normalized), dt * 4f);
                if (c.Horse != null) c.Horse.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(c.S * 2.2f)) * 0.08f, 3.1f);
                foreach (var a in c.Team)
                    foreach (AnimationState st in a) st.speed = c.Speed / 1.4f; // one stride ≈ 1.4 m
            }
        }
    }
}
