using Ironhorse.Core;
using UnityEngine;

namespace Ironhorse.Game
{
    /// <summary>
    /// The station's platform tracks, laid one at a time: every platform the player has built gets its track
    /// (the second island platform and its canopy come with platform 4), the one under construction shows the
    /// rails reaching east as the crew works, and the next one waits behind survey stakes and a pile of ties.
    /// Tapping the work site opens the trains screen, where the next platform is laid.
    /// </summary>
    public sealed class StationPlatforms : MonoBehaviour, ISelectable
    {
        public static StationPlatforms Instance { get; private set; }

        /// <summary>Platform 4 (index 3) shares the second island with platform 5.</summary>
        public static float Island2 { get { return (WorldLayout.PlatformZ(3) + WorldLayout.PlatformZ(4)) / 2; } }

        readonly GameObject[] _tracks = new GameObject[Balance.Platforms];
        GameObject _island2, _work;
        string _sig;
        float _next;

        void Awake() { Instance = this; }

        void Start()
        {
            G.Session.PlatformOpened += OnOpened;
            Refresh();
        }

        void OnDestroy() { if (G.Session != null) G.Session.PlatformOpened -= OnOpened; }

        void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.5f;
            Refresh();
        }

        public void OnTapped() { AudioManager.Play(Sfx.Click); UIRoot.Instance.OpenFleet(); }

        void OnOpened(int index)
        {
            Refresh();
            var at = new Vector3(0, 0, WorldLayout.PlatformZ(index));
            CameraRig.Instance.FlyTo(at, 80);
            Fx.Burst(at + Vector3.up * 3, 60);
        }

        static void StationSpan(TrackPath path, out float a, out float b)
        {
            a = path.Project(new Vector3(-121, 0, WorldLayout.MainZ));
            b = path.Project(new Vector3(121, 0, WorldLayout.MainZ));
        }

        void Refresh()
        {
            var s = G.Session;
            int built = s.PlatformsBuilt;
            int step = s.LayingPlatform ? Mathf.FloorToInt(s.PlatformProgress * 24) : -1;
            string sig = built + ":" + step + ":" + s.StationComplete;
            if (sig == _sig) return;
            _sig = sig;

            for (int i = 0; i < Balance.Platforms; i++)
            {
                if (i < built && _tracks[i] == null)
                {
                    var mb = new MeshBuilder();
                    var path = WorldLayout.PlatformPath(i);
                    StationSpan(path, out float a, out float b);
                    TrackBuilder.Build(mb, path, a, b);
                    _tracks[i] = mb.ToObject("Platform " + (i + 1), transform);
                }
                else if (i >= built && _tracks[i] != null) { Destroy(_tracks[i]); _tracks[i] = null; }
            }
            bool island = built >= 4;
            if (island && _island2 == null) _island2 = BuildIsland2();
            else if (!island && _island2 != null) { Destroy(_island2); _island2 = null; }

            if (_work != null) Destroy(_work);
            _work = null;
            if (s.StationComplete) return;
            _work = BuildWorkSite(built, s.LayingPlatform ? s.PlatformProgress : -1);
        }

        GameObject BuildIsland2()
        {
            var mb = new MeshBuilder();
            float half = WorldLayout.PlatformHalfLength, z = Island2;
            TrackBuilder.Platform(mb, z, 2.4f, half - 4);
            Station.Canopy(mb, z, half - 10);
            for (float x = -40; x <= 40; x += 16) Station.Lantern(mb, new Vector3(x + 8, 3.9f, z));
            var go = mb.ToObject("Island 2", transform);
            Station.Walkable(go);
            return go;
        }

        /// <summary>The next platform's site: survey stakes before work starts; partial track, ties and rails while
        /// the crew lays it (<paramref name="progress"/> 0..1, or -1 when not started).</summary>
        GameObject BuildWorkSite(int index, float progress)
        {
            var mb = new MeshBuilder();
            float z = WorldLayout.PlatformZ(index), half = WorldLayout.PlatformHalfLength;
            var path = WorldLayout.PlatformPath(index);
            StationSpan(path, out float a, out float b);
            float front = -half - 6;
            if (progress >= 0)
            {
                float end = a + (b - a) * Mathf.Max(0.02f, progress);
                TrackBuilder.Build(mb, path, a, end);
                front = path.PositionAt(end).x;
            }
            mb.Matte();
            // Survey stakes with orange flags along the rest of the line.
            for (float x = -half; x <= half; x += 9)
            {
                if (x < front + 4) continue;
                mb.Box(new Vector3(x, 0.55f, z + 1.6f), new Vector3(0.12f, 1.1f, 0.12f), Palette.WoodPale);
                mb.Box(new Vector3(x + 0.22f, 1.0f, z + 1.6f), new Vector3(0.4f, 0.26f, 0.03f), Palette.Hex(0xE8742A));
            }
            // Ties stacked crosswise and a bundle of rails at the work front.
            float px = Mathf.Clamp(front + 5, -half, half);
            for (int layer = 0; layer < 4; layer++)
                for (int k = 0; k < 5; k++)
                {
                    float off = (k - 2) * 0.34f;
                    var c = layer % 2 == 0 ? new Vector3(px + off, 0.14f + layer * 0.24f, z) : new Vector3(px, 0.14f + layer * 0.24f, z + off);
                    var size = layer % 2 == 0 ? new Vector3(0.26f, 0.22f, 2.6f) : new Vector3(2.6f, 0.22f, 0.26f);
                    mb.Box(c, size, k % 2 == 0 ? Palette.WoodDark : Palette.Wood);
                }
            mb.Mat(0.5f, 0.5f);
            for (int r = 0; r < 6; r++)
                mb.Box(new Vector3(px + 5, 0.1f + (r / 3) * 0.16f, z - 0.5f + (r % 3) * 0.32f), new Vector3(9, 0.14f, 0.12f), Palette.Steel);
            mb.Matte();
            // A handcar on the finished rails behind the front while the crew works.
            if (progress >= 0 && front > -half + 4)
            {
                float hx = front - 3.5f, y = WorldLayout.RailTop;
                mb.Box(new Vector3(hx, y + 0.55f, z), new Vector3(2.2f, 0.18f, 1.6f), Palette.WoodDark);
                mb.Box(new Vector3(hx, y + 1.15f, z), new Vector3(0.14f, 1.0f, 0.14f), Palette.Iron);
                mb.Box(new Vector3(hx, y + 1.65f, z), new Vector3(1.8f, 0.1f, 0.1f), Palette.Iron);
                for (int w = -1; w <= 1; w += 2)
                    for (int side = -1; side <= 1; side += 2)
                        mb.Cylinder(new Vector3(hx + w * 0.7f, y + 0.3f, z + side * 0.72f), new Vector3(hx + w * 0.7f, y + 0.3f, z + side * 0.82f), 0.3f, 10, Palette.Iron);
            }
            var go = mb.ToObject("Platform Site", transform);
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 1.2f, z);
            col.size = new Vector3(half * 2 + 8, 2.4f, 4.6f);
            return go;
        }
    }
}
