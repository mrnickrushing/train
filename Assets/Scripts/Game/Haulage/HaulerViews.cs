using System.Collections.Generic;
using Ironhorse.Core;
using UnityEngine;

namespace Ironhorse.Game
{
    /// <summary>
    /// Every hauler on the map: wagons parked in the freight yard and boats tied up at the landing when idle; out on a
    /// delivery they travel the road or river to the project at their real speed, unload, and come back. Timing comes
    /// straight from the simulation (<see cref="GameSession.HaulProgress"/>), so the map always agrees with the timers.
    /// </summary>
    public sealed class HaulerViews : MonoBehaviour
    {
        public static HaulerViews Instance { get; private set; }

        /// <summary>Share of a trip spent unloading at the site (the rest is split between going out and coming back).</summary>
        const float Unload = 0.08f;

        sealed class View : MonoBehaviour, ISelectable
        {
            public int Uid;
            public HaulerDef Def;
            public readonly List<Animation> Team = new List<Animation>();
            public Transform Paddle;
            /// <summary>A work train's cars, engine first (centre-pivoted, length along +Z).</summary>
            public readonly List<(Transform t, float len)> Cars = new List<(Transform, float)>();
            public float LastS = float.NaN;
            public void OnTapped()
            {
                AudioManager.Play(Sfx.Click);
                var h = G.Session.Hauler(Uid);
                if (h != null && !h.IsIdle) UIRoot.Instance.OpenProjectSite(h.projectId);
                else UIRoot.Instance.OpenDepot("haulers");
            }
        }

        readonly Dictionary<int, View> _views = new Dictionary<int, View>();

        void Awake() { Instance = this; }

        void Update()
        {
            var s = G.Session;
            if (s == null) return;
            foreach (var h in s.State.haulers)
                if (!_views.ContainsKey(h.uid)) _views[h.uid] = Spawn(h);
            var gone = new List<int>();
            foreach (var kv in _views)
                if (s.Hauler(kv.Key) == null) { Destroy(kv.Value.gameObject); gone.Add(kv.Key); }
            foreach (var id in gone) _views.Remove(id);

            int parked = 0, moored = 0, sided = 0;
            foreach (var h in s.State.haulers)
            {
                var v = _views[h.uid];
                if (h.IsIdle) Park(v, v.Def.OnWater ? moored++ : v.Def.OnRail ? sided++ : parked++);
                else Travel(v, h, s.HaulProgress(h));
            }
        }

        /// <summary>Where a hauler on a delivery is right now (for the map pins), or null when it is home.</summary>
        public Vector3? Position(int uid)
        {
            View v;
            return _views.TryGetValue(uid, out v) && v != null ? v.transform.position : (Vector3?)null;
        }

        void Park(View v, int slot)
        {
            Vector3 pos; Quaternion rot;
            if (v.Def.OnRail)
            {
                // Standing on the loop link north of the engine terminal, engine toward the loop, one behind another.
                var link = DestinationRoutes.LoopLink;
                float nose = HaulRoutes.RailPark - slot * 75f;
                PlaceCars(v, d =>
                {
                    Vector3 q, f;
                    link.Sample(Mathf.Clamp(d, 0, link.Length), out q, out f);
                    if (d < 0) q += f * d;
                    return q;
                }, Vector3.back, nose);
                SetPace(v, 0);
                v.LastS = float.NaN;
                return;
            }
            if (v.Def.OnWater)
            {
                // Tied up alongside the pier head, bow upstream, one behind another down the river.
                var river = WorldLayout.River;
                Vector3 at, down;
                river.Sample(river.Project(HaulRoutes.Landing), out at, out down); // River runs downstream
                down.y = 0; down.Normalize();
                var out_ = HaulRoutes.Landing - HaulRoutes.PierRoot; out_.y = 0; out_.Normalize();
                pos = HaulRoutes.Landing + down * (9f + slot * 17f) + out_ * 1.5f;
                pos.y = WorldLayout.WaterLevel + Mathf.Sin(Time.time * 0.9f + slot) * 0.06f;
                rot = Quaternion.LookRotation(-down);
            }
            else
            {
                // Side by side in lanes across the yard, noses toward the gate (east); a second rank behind when full.
                var c = HaulRoutes.Yard;
                pos = new Vector3(c.x + 3 - (slot / 4) * 10f, 0, c.z - 5.5f + (slot % 4) * 3.6f);
                pos.y = ValleyTerrain.Height(pos.x, pos.z);
                rot = Quaternion.LookRotation(Vector3.right);
            }
            v.transform.SetPositionAndRotation(pos, rot);
            SetPace(v, 0);
            v.LastS = float.NaN;
        }

        void Travel(View v, HaulerState h, float p)
        {
            var path = HaulRoutes.For(v.Def, h.projectId);
            if (path == null) { Park(v, 0); return; }
            float half = (1 - Unload) / 2;
            float s; bool back;
            if (p < half) { s = path.Length * (p / half); back = false; }
            else if (p < half + Unload) { s = path.Length; back = false; }
            else { s = path.Length * (1 - (p - half - Unload) / half); back = true; }
            Vector3 pos, fwd;
            if (v.Def.OnRail)
            {
                // The engine leads out and backs the train home (no run-round at a railhead).
                float sp = float.IsNaN(v.LastS) ? 0 : Mathf.Abs(s - v.LastS) / Mathf.Max(Time.deltaTime, 1e-4f);
                v.LastS = s;
                PlaceCars(v, d =>
                {
                    Vector3 q, f;
                    path.Sample(Mathf.Clamp(d, 0, path.Length), out q, out f);
                    if (d < 0) { path.Sample(0, out q, out f); q += f * d; }
                    return q;
                }, Vector3.zero, s);
                SetPace(v, sp);
                return;
            }
            path.Sample(Mathf.Clamp(s, 0, path.Length), out pos, out fwd);
            if (back) fwd = -fwd;
            fwd.y = 0;
            if (v.Def.OnWater) pos.y = WorldLayout.WaterLevel + Mathf.Sin(Time.time * 1.1f + h.uid) * 0.05f;
            float speed = float.IsNaN(v.LastS) ? 0 : Mathf.Abs(s - v.LastS) / Mathf.Max(Time.deltaTime, 1e-4f);
            v.LastS = s;
            v.transform.position = pos;
            if (fwd.sqrMagnitude > 0.001f)
                v.transform.rotation = Quaternion.Slerp(v.transform.rotation, Quaternion.LookRotation(fwd.normalized), Time.deltaTime * 5f);
            SetPace(v, speed);
        }

        /// <summary>Lines a work train's cars up behind the engine: <paramref name="at"/> maps distance along the
        /// track (the engine's nose at <paramref name="head"/>, the cars at smaller distances) to a point on it. A car
        /// faces from its rear wheels to its front ones, so curves bend the train.</summary>
        static void PlaceCars(View v, System.Func<float, Vector3> at, Vector3 dirHint, float head)
        {
            float d = head;
            for (int i = 0; i < v.Cars.Count; i++)
            {
                var (t, len) = v.Cars[i];
                Vector3 front = at(d), rear = at(d - len);
                var mid = (front + rear) / 2;
                var f = front - rear; f.y = 0;
                if (f.sqrMagnitude < 1e-4f) f = dirHint.sqrMagnitude > 0 ? -dirHint : Vector3.forward;
                if (i == 0) v.transform.position = mid;
                t.SetPositionAndRotation(mid, Quaternion.LookRotation(f.normalized));
                d -= len + 0.6f;
            }
        }

        static void SetPace(View v, float speed)
        {
            foreach (var a in v.Team)
                foreach (AnimationState st in a) st.speed = speed / 1.4f; // one stride ≈ 1.4 m; standing still when parked
            if (v.Paddle != null) v.Paddle.Rotate(Vector3.right, speed * 40f * Time.deltaTime, Space.Self);
        }

        // ---------------------------------------------------------------- models

        View Spawn(HaulerState h)
        {
            var d = Haulage.Hauler(h.defId);
            var go = new GameObject("Hauler " + d.Name);
            go.transform.SetParent(transform, false);
            var v = go.AddComponent<View>();
            v.Uid = h.uid;
            v.Def = d;
            v.Paddle = BuildModel(d, go.transform, v.Team, v.Cars);
            var b = ModelRegistry.RendererBounds(go);
            var col = go.AddComponent<BoxCollider>();
            col.center = go.transform.InverseTransformPoint(b.center);
            col.size = b.size + Vector3.one * 1.5f;
            return v;
        }

        /// <summary>Builds a hauler's model under <paramref name="root"/> (also used for the Depot's card art). Horse
        /// animations go into <paramref name="team"/> when given; returns the paddle wheel of a steamer, if any.</summary>
        public static Transform BuildModel(HaulerDef d, Transform root, List<Animation> team = null, List<(Transform t, float len)> cars = null)
        {
            if (d.OnRail) { WorkTrain(root, d.Id == "ballasttrain" ? "redmesa" : "dusty", cars == null ? 1 : d.Id == "ballasttrain" ? 4 : 2, cars); return null; } // a card shows the engine and one loaded car
            // AI-made models (Resources/Models/Haulers) when imported; boats sit down into the water to their waterline.
            if (ModelRegistry.Has("Haulers", d.Id))
            {
                var art = ModelRegistry.Spawn("Haulers", d.Id, root);
                if (d.OnWater) art.transform.localPosition += Vector3.down * (d.Id == "steamer" ? 1.3f : 0.8f);
                return null;
            }
            switch (d.Id)
            {
                case "freightwagon": TeamWagon(root, team, 1f, 1); return null;
                case "conestoga": TeamWagon(root, team, 1.3f, 3); return null;
                case "muletrain": MuleTrain(root, team); return null;
                case "sternwheeler": return Sternwheeler(root, 1f, Palette.PaintRed);
                case "steamer": return Sternwheeler(root, 1.45f, Palette.PaintBlue);
                default: Truck(root, d.Id == "expressvan"); return null;
            }
        }

        /// <summary>An engine and a string of flatcars loaded with ties, rails and stone, in a row along -Z.</summary>
        static void WorkTrain(Transform root, string loco, int flats, List<(Transform t, float len)> cars)
        {
            float z = 0;
            for (int i = 0; i <= flats; i++)
            {
                var art = i == 0 ? ModelRegistry.Spawn(ModelRegistry.Locomotives, loco, root) : ModelRegistry.Spawn(ModelRegistry.Wagons, "flatcar", root);
                if (art == null) continue;
                var holder = new GameObject(i == 0 ? "Engine" : "Flatcar").transform;
                holder.SetParent(root, false);
                var b = ModelRegistry.RendererBounds(art);
                art.transform.SetParent(holder, true);
                art.transform.localPosition -= new Vector3(b.center.x, 0, b.center.z);
                float len = Mathf.Max(4, b.size.z);
                if (i > 0) Load(holder, b.size.y, len, i);
                if (i == 0 && cars != null) // steam on the map only (it would throw off a card picture's framing)
                {
                    var stacks = ModelRegistry.Anchors(art, "Smoke");
                    if (stacks.Count > 0) Fx.Create(FxKind.Steam, stacks[0], Vector3.zero);
                }
                holder.localPosition = new Vector3(0, 0, z - len / 2);
                z -= len + 0.6f;
                if (cars != null) cars.Add((holder, len));
            }
        }

        /// <summary>A flatcar's load: stacked ties, a bundle of rails, or a heap of ballast stone.</summary>
        static void Load(Transform car, float deck, float len, int kind)
        {
            var mb = new MeshBuilder();
            mb.Matte();
            float y = deck * 0.55f;
            if (kind % 3 == 1)
                for (int row = 0; row < 3; row++)
                    for (float z = -len / 2 + 1.2f; z < len / 2 - 1; z += 2.6f)
                        mb.Box(new Vector3(0, y + 0.12f + row * 0.24f, z), new Vector3(2.4f, 0.22f, 2.3f), MeshBuilder.Darken(Palette.Hex(0x5A4430), 0.9f + 0.1f * (row % 2)));
            else if (kind % 3 == 2)
            {
                mb.Mat(0.5f, 0.7f);
                for (int r = 0; r < 6; r++) mb.Box(new Vector3(-0.75f + (r % 3) * 0.75f, y + 0.12f + (r / 3) * 0.2f, 0), new Vector3(0.14f, 0.18f, len - 1.4f), Palette.Steel);
                mb.Matte();
                foreach (float z in new[] { -len / 3, 0, len / 3 }) mb.Box(new Vector3(0, y + 0.05f, z), new Vector3(2.4f, 0.12f, 0.3f), Palette.WoodDark);
            }
            else mb.Sphere(new Vector3(0, y, 0), new Vector3(1.2f, 0.7f, len / 2 - 0.8f), 10, Palette.Hex(0x7A746A), false);
            mb.ToObject("Load", car);
        }

        static void Horse(Transform root, List<Animation> team, Vector3 at, float scale) { Animal(root, team, "horse", at, scale); }

        static void Animal(Transform root, List<Animation> team, string id, Vector3 at, float scale)
        {
            var horse = ModelRegistry.Spawn("Animals", id, root);
            if (horse == null) return;
            horse.transform.localPosition += at;
            horse.transform.localScale *= scale;
            foreach (var smr in horse.GetComponentsInChildren<SkinnedMeshRenderer>()) { smr.updateWhenOffscreen = false; smr.quality = SkinQuality.Bone2; }
            var anim = horse.GetComponentInChildren<Animation>();
            if (anim == null) return;
            foreach (AnimationState st in anim) { st.wrapMode = WrapMode.Loop; st.time = Random.value * st.length; }
            anim.Play();
            if (team != null) team.Add(anim);
        }

        /// <summary>Covered wagon behind <paramref name="pairs"/> pairs of draft horses.</summary>
        static void TeamWagon(Transform root, List<Animation> team, float scale, int pairs)
        {
            var wagon = ModelRegistry.Spawn("Props", "covered_wagon", root);
            if (wagon == null) { Truck(root, false); return; }
            wagon.transform.localScale *= scale;
            var b = ModelRegistry.RendererBounds(wagon);
            wagon.transform.localPosition -= new Vector3(b.center.x, 0, b.center.z);
            float tip = b.size.z / 2;
            for (int p = 0; p < pairs; p++)
                foreach (float x in new[] { -0.56f, 0.56f })
                    Horse(root, team, new Vector3(x, 0, tip - 1.1f + p * 2.7f), 1f);
        }

        /// <summary>Three pack mules in a string, crates and sacks lashed on.</summary>
        static void MuleTrain(Transform root, List<Animation> team)
        {
            bool mules = ModelRegistry.Has("Animals", "mule");
            for (int i = 0; i < 3; i++)
            {
                if (mules) { Animal(root, team, "mule", new Vector3(0, 0, -i * 2.5f), 1f); continue; }
                Horse(root, team, new Vector3(0, 0, -i * 2.6f), 0.82f);
                var mb = new MeshBuilder();
                mb.Matte();
                mb.Box(new Vector3(-0.38f, 1.45f, -i * 2.6f), new Vector3(0.42f, 0.5f, 0.7f), Palette.Weathered, Palette.WoodPale);
                mb.Box(new Vector3(0.38f, 1.45f, -i * 2.6f), new Vector3(0.42f, 0.5f, 0.7f), Palette.Weathered, Palette.WoodPale);
                mb.Sphere(new Vector3(0, 1.75f, -i * 2.6f - 0.1f), new Vector3(0.32f, 0.22f, 0.4f), 8, Palette.Cream);
                mb.ToObject("Packs", root);
            }
        }

        /// <summary>A paddle steamer: white hull and decks, twin stacks, a red stern wheel that turns as she moves.</summary>
        static Transform Sternwheeler(Transform root, float s, Color trim)
        {
            var mb = new MeshBuilder();
            mb.Paint();
            Color white = Palette.White, hull = Palette.Hex(0x2F2B28);
            mb.Box(new Vector3(0, 0.25f, 0) * s, new Vector3(4.6f, 0.9f, 13f) * s, hull, Palette.WoodPale);   // hull
            mb.Box(new Vector3(0, 0.75f, 5.6f) * s, new Vector3(3.2f, 0.4f, 2.6f) * s, hull, Palette.WoodPale); // bow
            mb.Box(new Vector3(0, 1.45f, -0.6f) * s, new Vector3(4.2f, 1.4f, 9f) * s, white);                   // main deck cabin
            mb.Box(new Vector3(0, 2.2f, -0.6f) * s, new Vector3(4.8f, 0.12f, 9.8f) * s, trim);                  // deck rail trim
            mb.Box(new Vector3(0, 2.85f, -1.2f) * s, new Vector3(3.4f, 1.2f, 6.4f) * s, white);                 // upper deck
            mb.Box(new Vector3(0, 3.5f, -1.2f) * s, new Vector3(3.8f, 0.12f, 7f) * s, trim);
            mb.Box(new Vector3(0, 3.95f, 1.0f) * s, new Vector3(1.6f, 0.9f, 1.6f) * s, white);                  // pilot house
            mb.Lit();
            for (int i = 0; i < 6; i++)
                for (int side = -1; side <= 1; side += 2)
                    mb.Box(new Vector3(side * 2.12f, 1.55f, -4.2f + i * 1.4f) * s, new Vector3(0.05f, 0.5f, 0.6f) * s, Palette.Window);
            mb.Paint();
            foreach (float x in new[] { -0.8f, 0.8f })
            {
                mb.Cylinder(new Vector3(x, 3.4f, 2.4f) * s, new Vector3(x, 7.2f, 2.4f) * s, 0.32f * s, 10, Palette.Iron);
                mb.Cylinder(new Vector3(x, 7.0f, 2.4f) * s, new Vector3(x, 7.4f, 2.4f) * s, 0.5f * s, 10, Palette.Iron);
            }
            // Cargo on the fore deck.
            for (int i = 0; i < 4; i++) mb.Box(new Vector3(-0.9f + (i % 2) * 1.8f, 1.2f, 4.0f + (i / 2) * 0.9f) * s, new Vector3(0.8f, 0.7f, 0.8f) * s, Palette.Weathered, Palette.WoodPale);
            mb.ToObject("Boat", root);

            // Stern wheel on its own transform so it can turn.
            var wheel = new GameObject("Paddle").transform;
            wheel.SetParent(root, false);
            wheel.localPosition = new Vector3(0, 1.1f, -7.6f) * s;
            var wb = new MeshBuilder();
            wb.Paint();
            for (int i = 0; i < 10; i++)
            {
                var r = Quaternion.Euler(i * 36, 0, 0);
                wb.Push().Rotate(r);
                wb.Box(new Vector3(0, 1.1f, 0) * s, new Vector3(4.0f, 0.9f, 0.12f) * s, Palette.PaintRed);
                wb.Pop();
            }
            wb.Cylinder(new Vector3(-2.1f, 0, 0) * s, new Vector3(2.1f, 0, 0) * s, 0.22f * s, 8, Palette.Iron);
            wb.ToObject("Wheel", wheel);
            return wheel;
        }

        /// <summary>An early motor truck: cab, slatted bed with crates, spoked wheels.</summary>
        static void Truck(Transform root, bool van)
        {
            var mb = new MeshBuilder();
            mb.Paint();
            Color body = van ? Palette.Hex(0x7A1F1F) : Palette.Hex(0x2F4A3A), trim = van ? Palette.Brass : Palette.Hex(0x1C1C1C);
            mb.Box(new Vector3(0, 0.75f, 0), new Vector3(1.9f, 0.25f, 5.4f), Palette.Iron);           // chassis
            mb.Box(new Vector3(0, 1.25f, 2.1f), new Vector3(1.7f, 0.9f, 1.3f), body);                  // bonnet
            mb.Box(new Vector3(0, 1.8f, 1.05f), new Vector3(1.9f, 1.6f, 1.1f), body);                  // cab
            mb.Box(new Vector3(0, 2.65f, 1.05f), new Vector3(2.0f, 0.1f, 1.3f), trim);
            mb.Lit();
            mb.Box(new Vector3(0, 2.05f, 1.62f), new Vector3(1.5f, 0.6f, 0.04f), Palette.Hex(0x9FB4C0));
            mb.Paint();
            if (van) mb.Box(new Vector3(0, 1.95f, -1.15f), new Vector3(2.0f, 2.1f, 3.1f), body, trim);
            else
            {
                mb.Box(new Vector3(0, 1.0f, -1.15f), new Vector3(2.0f, 0.2f, 3.1f), Palette.WoodDark);
                for (int side = -1; side <= 1; side += 2) mb.Box(new Vector3(side * 0.95f, 1.35f, -1.15f), new Vector3(0.08f, 0.55f, 3.1f), Palette.Wood);
                for (int i = 0; i < 4; i++) mb.Box(new Vector3(-0.45f + (i % 2) * 0.9f, 1.5f, -0.4f - (i / 2) * 1.1f), new Vector3(0.8f, 0.8f, 0.8f), Palette.Weathered, Palette.WoodPale);
            }
            mb.Matte();
            foreach (float z in new[] { 2.0f, -1.8f })
                for (int side = -1; side <= 1; side += 2)
                    mb.Cylinder(new Vector3(side * 0.9f, 0.5f, z), new Vector3(side * 1.08f, 0.5f, z), 0.5f, 12, Palette.Hex(0x1A1A1A));
            mb.ToObject("Truck", root);
        }
    }
}
