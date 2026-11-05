using System.Collections.Generic;
using Ironhorse.Core;
using UnityEngine;

namespace Ironhorse.Game
{
    /// <summary>
    /// Fixed geography of the home valley. The station sits along the X axis with platforms south of
    /// the town grid; the main line runs east-west through tunnels in two great mesas.
    /// Trains always arrive from the west tunnel and depart into the east tunnel, so they never reverse.
    /// </summary>
    public static class WorldLayout
    {
        public const float PlatformSpacing = 5.5f;
        public const float FirstPlatformZ = -30f;
        public const float PlatformHalfLength = 46f;
        public const float ParkX = 40f;
        public const float TunnelX = 196f;
        public const float PathEndX = 270f;
        public const float RailTop = 0.42f;

        public static float MainZ { get { return FirstPlatformZ - PlatformSpacing * (Balance.Platforms - 1) * 0.5f; } }
        public static float PlatformZ(int i) { return FirstPlatformZ - PlatformSpacing * i; }

        public static readonly Vector3 LoopCenter = new Vector3(0, 0, 150);
        public const float LoopRx = 150f, LoopRz = 46f;

        /// <summary>Eagle Lake: the big mountain lake north of the valley the river flows out of (the map's north region).</summary>
        public static readonly Vector3 NorthLakeCenter = new Vector3(-20, 0, 588);
        public const float NorthLakeRx = 140f, NorthLakeRz = 82f;
        /// <summary>Eagle Point: the rocky island in Eagle Lake (the lighthouse stands on it).</summary>
        public static readonly Vector3 IslandCenter = new Vector3(-62, 0, 604);
        public const float IslandRadius = 18f;

        /// <summary>Distance from Eagle Lake's centre in shore units (1 = on the shore line, before the shore noise).</summary>
        public static float NorthLakeDist(float x, float z)
        {
            float dx = (x - NorthLakeCenter.x) / NorthLakeRx, dz = (z - NorthLakeCenter.z) / NorthLakeRz;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        public static readonly Vector3 LakeCenter = new Vector3(-104, 0, 22);
        public const float LakeRadius = 22f;
        public const float WaterLevel = -1.1f;

        static TrackPath[] _platformPaths;
        static TrackPath _loop, _river;

        /// <summary>Full west-tunnel → platform i → east-tunnel path.</summary>
        public static TrackPath PlatformPath(int i)
        {
            if (_platformPaths == null) _platformPaths = new TrackPath[Balance.Platforms];
            if (_platformPaths[i] != null) return _platformPaths[i];
            float z = PlatformZ(i), m = MainZ, y = RailTop;
            var p = new TrackPath();
            p.Add(new Vector3(-PathEndX, y, m))
             .Line(new Vector3(-120, y, m))
             .Bezier(new Vector3(-88, y, m), new Vector3(-84, y, z), new Vector3(-PlatformHalfLength - 6, y, z))
             .Line(new Vector3(PlatformHalfLength + 6, y, z))
             .Bezier(new Vector3(84, y, z), new Vector3(88, y, m), new Vector3(120, y, m))
             .Line(new Vector3(PathEndX, y, m));
            _platformPaths[i] = p.Build();
            return _platformPaths[i];
        }

        public static float ParkDistance(int platform) { return PlatformPath(platform).Project(new Vector3(ParkX, RailTop, PlatformZ(platform))); }

        public static TrackPath MainLineWest
        {
            get { return new TrackPath().Add(new Vector3(-PathEndX, RailTop, MainZ)).Line(new Vector3(-120, RailTop, MainZ)).Build(); }
        }

        public static TrackPath MainLineEast
        {
            get { return new TrackPath().Add(new Vector3(120, RailTop, MainZ)).Line(new Vector3(PathEndX, RailTop, MainZ)).Build(); }
        }

        public static TrackPath ScenicLoop
        {
            get { return _loop ?? (_loop = new TrackPath().Ellipse(LoopCenter + Vector3.up * RailTop, LoopRx, LoopRz, 220).Build()); }
        }

        public static TrackPath River
        {
            get
            {
                if (_river != null) return _river;
                _river = new TrackPath();
                // Out of Eagle Lake's south shore, down the valley through the mountains, into the valley and Silver Lake.
                _river.Add(new Vector3(-46, 0, 516))
                      .Bezier(new Vector3(-52, 0, 482), new Vector3(-66, 0, 452), new Vector3(-70, 0, 420))
                      .Bezier(new Vector3(-80, 0, 360), new Vector3(-110, 0, 330), new Vector3(-92, 0, 260))
                      .Bezier(new Vector3(-75, 0, 200), new Vector3(-70, 0, 170), new Vector3(-96, 0, 120))
                      .Bezier(new Vector3(-118, 0, 80), new Vector3(-96, 0, 60), new Vector3(LakeCenter.x, 0, LakeCenter.z + 6));
                return _river.Build();
            }
        }

        // ---------------------------------------------------------------- Town plots

        public const int PlotCols = 6, PlotRows = 6;
        public const float PlotSize = 12f, PlotPitchX = 16f, PlotPitchZ = 16f;
        public const float PlotOriginX = -40f, PlotOriginZ = -2f;

        static List<Vector3> _plots;

        /// <summary>Plot centers ordered from Main Street outward, so early plots are near the station.</summary>
        public static List<Vector3> Plots
        {
            get
            {
                if (_plots != null) return _plots;
                var all = new List<Vector3>();
                for (int r = 0; r < PlotRows; r++)
                    for (int c = 0; c < PlotCols; c++)
                        all.Add(new Vector3(PlotOriginX + c * PlotPitchX, 0, PlotOriginZ + r * PlotPitchZ));
                all.Sort((a, b) =>
                {
                    float ka = a.z * 1.4f + Mathf.Abs(a.x), kb = b.z * 1.4f + Mathf.Abs(b.x);
                    return ka.CompareTo(kb);
                });
                _plots = all;
                return _plots;
            }
        }

        public static Rect TownRect { get { return new Rect(-60, -72, 120, 165); } }

        /// <summary>Freight yard with sidings and gantry cranes, east of town.</summary>
        public static Rect YardRect { get { return new Rect(58, -24, 120, 42); } }
        public static float SidingZ(int i) { return -18f + 5.5f * i; }
        public const int Sidings = 5;

        /// <summary>Roundhouse and turntable, west of town between the lake and the main line.</summary>
        public static readonly Vector3 TurntableCenter = new Vector3(-142, 0, -4);
        public static Rect RoundhouseRect { get { return new Rect(-182, -32, 64, 54); } }
    }
}
