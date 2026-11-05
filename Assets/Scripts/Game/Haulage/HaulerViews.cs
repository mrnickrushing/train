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

            int parked = 0, moored = 0;
            foreach (var h in s.State.haulers)
            {
                var v = _views[h.uid];
                if (h.IsIdle) Park(v, v.Def.OnWater ? moored++ : parked++);
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
            if (v.Def.OnWater)
            {
                // Tied up alongside the pier head, one behind another.
                var dir = (HaulRoutes.Landing - HaulRoutes.PierRoot); dir.y = 0; dir.Normalize();
                var right = Vector3.Cross(Vector3.up, dir);
                pos = HaulRoutes.Landing + right * (5.5f + slot * 9f) - dir * 3f;
                pos.y = WorldLayout.WaterLevel + Mathf.Sin(Time.time * 0.9f + slot) * 0.06f;
                rot = Quaternion.LookRotation(-dir);
            }
            else
            {
                // In a row across the yard, noses toward the gate.
                var c = HaulRoutes.Yard;
                pos = new Vector3(c.x - 6 + (slot % 3) * 5f, 0, c.z - 3.5f + (slot / 3) * 4.5f);
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
            v.Paddle = BuildModel(d, go.transform, v.Team);
            var b = ModelRegistry.RendererBounds(go);
            var col = go.AddComponent<BoxCollider>();
            col.center = go.transform.InverseTransformPoint(b.center);
            col.size = b.size + Vector3.one * 1.5f;
            return v;
        }

        /// <summary>Builds a hauler's model under <paramref name="root"/> (also used for the Depot's card art). Horse
        /// animations go into <paramref name="team"/> when given; returns the paddle wheel of a steamer, if any.</summary>
        public static Transform BuildModel(HaulerDef d, Transform root, List<Animation> team = null)
        {
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

        static void Horse(Transform root, List<Animation> team, Vector3 at, float scale)
        {
            var horse = ModelRegistry.Spawn("Animals", "horse", root);
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
            for (int i = 0; i < 3; i++)
            {
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
