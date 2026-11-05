using System.Collections.Generic;
using UnityEngine;

namespace Ironhorse.Game
{
    /// <summary>
    /// Storyline landmarks raised from a generic building site: a levelled lot, a foundation, timber scaffolding and
    /// walls that climb stage by stage, a crane and stacks of materials, then the finished building's art once the
    /// last order is in (Models/Buildings/{id}_done).
    /// </summary>
    public sealed partial class ProjectSites
    {
        /// <summary>Landmark lots: centre, footprint half-size and the way the front faces (degrees).</summary>
        public static readonly Dictionary<string, (Vector3 at, float half, float yaw)> Landmarks = new Dictionary<string, (Vector3, float, float)>
        {
            { "winery", (new Vector3(96, 0, 424), 11f, 180f) },
            { "capitol", (new Vector3(8, 0, 384), 13f, 180f) },
            { "opera", (new Vector3(266, 0, 170), 10f, 210f) },
            { "mission", (new Vector3(-160, 0, -248), 10f, 20f) },
        };

        static bool LandmarkBlocks(float x, float z)
        {
            foreach (var kv in Landmarks)
            {
                var c = kv.Value.at; float h = kv.Value.half + 8;
                if (Mathf.Abs(x - c.x) < h && Mathf.Abs(z - c.z) < h) return true;
            }
            return false;
        }

        Site Landmark(string id)
        {
            var spec = Landmarks[id];
            var c = spec.at;
            float floor = GY(c.x, c.z) + 0.3f;
            c.y = floor;
            float half = spec.half;
            var rot = Quaternion.Euler(0, spec.yaw, 0);
            var s = new Site { Id = id, Focus = c + Vector3.up * 5, Distance = 75 };
            System.Func<float, float, float, Vector3> P = (x, y, z) => c + rot * new Vector3(x, y, z);

            s.Pieces.Add(new Piece(c, mb => { mb.Matte(); mb.Surf = SurfaceKind.Stone; mb.Push().Translate(c).Rotate(rot); mb.Box(new Vector3(0, -0.4f, 0), new Vector3(half * 2, 0.8f, half * 2), Palette.StoneDark, Palette.Stone); mb.Pop(); mb.Surf = SurfaceKind.Auto; }));
            const int floors = 4;
            for (int f = 0; f < floors; f++)
            {
                float y0 = f * 3.2f;
                int k = f;
                // Walls climb inside scaffolding poles.
                s.Pieces.Add(new Piece(P(0, y0, 0), mb =>
                {
                    mb.Matte();
                    mb.Push().Translate(c).Rotate(rot);
                    float w = half * 2 - 3 - k * 0.6f;
                    mb.Box(new Vector3(0, y0 + 1.6f, 0), new Vector3(w, 3.2f, w * 0.8f), k % 2 == 0 ? Palette.Stone : MeshBuilder.Darken(Palette.Stone, 0.92f));
                    mb.Pop();
                }));
                s.Pieces.Add(new Piece(P(0, y0, 0), mb =>
                {
                    mb.Matte();
                    mb.Push().Translate(c).Rotate(rot);
                    float w = half * 2 - 1;
                    for (float x = -w / 2; x <= w / 2 + 0.01f; x += w / 4)
                        foreach (float z in new[] { -w * 0.42f, w * 0.42f })
                            mb.Cylinder(new Vector3(x, y0, z), new Vector3(x, y0 + 3.4f, z), 0.1f, 4, Palette.WoodPale, false);
                    mb.Box(new Vector3(0, y0 + 3.2f, -w * 0.42f), new Vector3(w, 0.12f, 0.8f), Palette.WoodPale);
                    mb.Box(new Vector3(0, y0 + 3.2f, w * 0.42f), new Vector3(w, 0.12f, 0.8f), Palette.WoodPale);
                    mb.Pop();
                }));
            }
            // A tall timber crane beside the works.
            s.Pieces.Add(new Piece(P(half + 3, 0, 0), mb =>
            {
                mb.Matte();
                mb.Push().Translate(c).Rotate(rot);
                mb.Box(new Vector3(half + 3, 8, 0), new Vector3(0.8f, 16, 0.8f), Palette.WoodDark);
                mb.Box(new Vector3(half - 2, 15.6f, 0), new Vector3(12, 0.5f, 0.5f), Palette.WoodDark);
                mb.Cylinder(new Vector3(half - 7, 15.4f, 0), new Vector3(half - 7, 9, 0), 0.03f, 4, Palette.Iron, false);
                mb.Box(new Vector3(half - 7, 8.6f, 0), new Vector3(1.2f, 0.8f, 1.2f), Palette.Weathered);
                mb.Pop();
            }));
            foreach (var o in new[] { new Vector2(-half - 5, -4), new Vector2(-half - 5, 5), new Vector2(4, half + 5) })
            {
                var p = At(c.x + o.x, c.z + o.y);
                s.Pieces.Add(new Piece(p, mb => Crates(mb, p)));
            }
            s.DoneModel = id + "_done";
            s.DoneAt = new Vector3(c.x, floor - 0.3f, c.z);
            s.DoneYaw = spec.yaw;
            s.Stakes = mb => { foreach (var o in new[] { new Vector2(-half, -half), new Vector2(half, -half), new Vector2(-half, half), new Vector2(half, half) }) Stake(mb, At(c.x + o.x, c.z + o.y)); };
            s.Camp = mb => Camp(mb, new Vector2(c.x - half - 12, c.z + half + 6), CampKind.Stone);
            return s;
        }
    }
}
