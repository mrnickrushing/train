using System.Linq;
using Ironhorse.Core;
using UnityEngine;

namespace Ironhorse.Game
{
    /// <summary>
    /// The Frontier Museum west of Main Street. It appears with the first curio and grows a wing for each
    /// shelf that's half full; a flag goes up when every shelf is complete. Tap it to open the museum board.
    /// </summary>
    public sealed class Museum : MonoBehaviour, ISelectable
    {
        public static readonly Vector3 Site = new Vector3(-64, 0, 14);

        /// <summary>Keeps scenery off the museum's lot.</summary>
        public static bool Blocks(float x, float z) { return Mathf.Abs(x - Site.x) < 15 && Mathf.Abs(z - Site.z) < 14; }

        GameObject _built;
        string _sig;
        bool _dirty = true;

        void Start() { G.Session.Changed += () => _dirty = true; }

        void Update()
        {
            if (!_dirty) return;
            _dirty = false;
            Rebuild();
        }

        public void OnTapped() { AudioManager.Play(Sfx.Click); UIRoot.Instance.OpenMuseum(); }

        void Rebuild()
        {
            var s = G.Session;
            int found = s.State.curios.Count;
            var wings = Frontier.Shelves.Select(sh => s.ShelfOwned(sh) * 2 >= sh.Curios.Count()).ToArray();
            bool flag = Frontier.Shelves.All(s.IsShelfComplete);
            string sig = found == 0 ? "none" : string.Join("", wings.Select(w => w ? "1" : "0")) + (flag ? "F" : "");
            if (sig == _sig) return;
            _sig = sig;
            if (_built != null) Destroy(_built);
            _built = null;
            if (found == 0) return;

            float y = ValleyTerrain.Height(Site.x, Site.z);
            var mb = new MeshBuilder();
            mb.Translate(Site.x, y, Site.z).RotateY(90); // the portico faces Main Street (east)
            Color wall = Palette.Hex(0xD8C29A), trim = Palette.White, stone = Palette.Stone, roof = Palette.RoofGreen;

            // Plinth and steps
            mb.Matte();
            mb.Box(new Vector3(0, 0.3f, 0), new Vector3(15, 1.0f, 12), Palette.StoneDark, stone);
            for (int i = 0; i < 3; i++) mb.Box(new Vector3(0, 0.2f + i * 0.3f, 6.6f - i * 0.45f), new Vector3(7, 0.3f, 1.2f), stone);

            // Main hall with a classical portico
            mb.Box(new Vector3(0, 3.8f, -0.8f), new Vector3(9, 6, 8), wall, wall);
            mb.Box(new Vector3(0, 6.95f, -0.8f), new Vector3(9.6f, 0.5f, 8.6f), trim);
            mb.GableRoof(new Vector3(0, 7.2f, 0.4f), new Vector3(10, 2.2f, 11), roof, trim, 0.4f);
            mb.Shiny();
            for (int i = 0; i < 4; i++) mb.Cylinder(new Vector3(-3 + i * 2, 0.8f, 4.6f), new Vector3(-3 + i * 2, 6.8f, 4.6f), 0.38f, 10, trim);
            mb.Matte();
            mb.Box(new Vector3(0, 6.95f, 4.6f), new Vector3(9.6f, 0.5f, 1.4f), trim);
            mb.Box(new Vector3(0, 2.5f, 3.25f), new Vector3(2.2f, 3.4f, 0.2f), Palette.WoodDark); // doors
            mb.Box(new Vector3(0, 5.6f, 3.3f), new Vector3(4.6f, 0.9f, 0.2f), Palette.PaintBlue); // name board
            mb.Lit();
            mb.Box(new Vector3(-3.2f, 3.6f, 3.27f), new Vector3(1.2f, 1.8f, 0.12f), Palette.Window);
            mb.Box(new Vector3(3.2f, 3.6f, 3.27f), new Vector3(1.2f, 1.8f, 0.12f), Palette.Window);
            mb.Matte();
            // Red banners either side of the doors
            for (int side = -1; side <= 1; side += 2)
                mb.Box(new Vector3(side * 1.9f, 3.6f, 5.1f), new Vector3(0.9f, 2.6f, 0.08f), Palette.PaintRed);
            // Domed cupola: the landmark you spot from across town
            mb.Cylinder(new Vector3(0, 8.6f, -0.8f), new Vector3(0, 10.4f, -0.8f), 1.9f, 16, wall);
            mb.Box(new Vector3(0, 10.5f, -0.8f), new Vector3(4.2f, 0.3f, 4.2f), trim);
            mb.Shiny();
            mb.Sphere(new Vector3(0, 10.6f, -0.8f), new Vector3(1.95f, 1.9f, 1.95f), 16, Palette.Hex(0x5E8C74));
            mb.Cylinder(new Vector3(0, 12.4f, -0.8f), new Vector3(0, 13.4f, -0.8f), 0.08f, 6, Palette.Brass);
            mb.Matte();

            // Wings: north, south, then a rear gallery
            var wingAt = new[] { new Vector3(-7.4f, 0, -0.8f), new Vector3(7.4f, 0, -0.8f), new Vector3(0, 0, -7.6f) };
            var wingSize = new[] { new Vector3(5.6f, 4.4f, 6.6f), new Vector3(5.6f, 4.4f, 6.6f), new Vector3(7.4f, 4.4f, 5.4f) };
            for (int i = 0; i < wings.Length && i < wingAt.Length; i++)
            {
                if (!wings[i]) continue;
                var c = wingAt[i];
                var sz = wingSize[i];
                mb.Box(new Vector3(c.x, 0.3f, c.z), new Vector3(sz.x + 0.8f, 1.0f, sz.z + 0.8f), Palette.StoneDark, stone);
                mb.Box(new Vector3(c.x, 0.8f + sz.y / 2, c.z), sz, wall, wall);
                mb.Box(new Vector3(c.x, 1.05f + sz.y, c.z), new Vector3(sz.x + 0.4f, 0.5f, sz.z + 0.4f), trim);
                mb.HipRoof(new Vector3(c.x, 1.3f + sz.y, c.z), new Vector3(sz.x + 0.6f, 1.6f, sz.z + 0.6f), roof, 0.3f);
                mb.Lit();
                float face = i == 2 ? 0 : (i == 0 ? -1 : 1);
                if (i < 2) for (int w = -1; w <= 1; w += 2) mb.Box(new Vector3(c.x + face * (sz.x / 2 + 0.04f), 3.0f, c.z + w * 1.6f), new Vector3(0.12f, 1.6f, 1.0f), Palette.Window);
                mb.Matte();
            }

            if (flag)
            {
                mb.Shiny();
                mb.Cylinder(new Vector3(0, 12.4f, -0.8f), new Vector3(0, 17f, -0.8f), 0.12f, 8, Palette.Brass);
                mb.Matte();
                mb.Box(new Vector3(1.1f, 16.2f, -0.8f), new Vector3(2.1f, 1.3f, 0.06f), Palette.PaintRed);
            }

            _built = mb.ToObject("Museum", transform);
            var col = _built.AddComponent<BoxCollider>();
            col.center = new Vector3(Site.x, y + 4, Site.z);
            col.size = new Vector3(14, 10, 16);
            _built.AddComponent<MuseumTap>().Owner = this;
        }
    }

    /// <summary>Forwards taps on the built mesh to the museum.</summary>
    public sealed class MuseumTap : MonoBehaviour, ISelectable
    {
        public Museum Owner;
        public void OnTapped() { Owner.OnTapped(); }
    }
}
