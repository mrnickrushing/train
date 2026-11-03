using System.Collections.Generic;
using Ironhorse.Core;
using UnityEngine;
using UnityEngine.SocialPlatforms;

namespace Ironhorse.Game
{
    /// <summary>
    /// Game Center sign-in, achievements and leaderboards (definitions: Core/GameCenterContent). Active on
    /// iOS devices only; everywhere else every call is a no-op.
    /// </summary>
    public static class GameCenter
    {
        const string ReportedKey = "gc.reported"; // achievements already accepted by Game Center
        static readonly HashSet<string> _reported = new HashSet<string>();
        static readonly Dictionary<string, long> _lastScore = new Dictionary<string, long>();
        static bool _started;

        public static bool Supported { get { return Application.platform == RuntimePlatform.IPhonePlayer; } }
        public static bool SignedIn { get { return Supported && Social.localUser.authenticated; } }

        /// <summary>Signs the player in (iOS shows its own banner or sheet), then catches up on progress.</summary>
        public static void Start()
        {
            if (_started || !Supported) return;
            _started = true;
            foreach (var id in PlayerPrefs.GetString(ReportedKey, "").Split(',')) if (id.Length > 0) _reported.Add(id);
            Social.localUser.Authenticate(ok =>
            {
                if (!ok) { Debug.Log("[GameCenter] not signed in"); return; }
                UnityEngine.SocialPlatforms.GameCenter.GameCenterPlatform.ShowDefaultAchievementCompletionBanner(true);
                Sync();
            });
        }

        /// <summary>Reports newly earned achievements and improved leaderboard scores.</summary>
        public static void Sync()
        {
            if (!SignedIn || G.Session == null) return;
            var s = G.Session.State;
            foreach (var a in GameCenterContent.Achievements)
            {
                if (_reported.Contains(a.Id) || GameCenterContent.Value(s, a.Stat) < a.Goal) continue;
                var id = a.Id;
                _reported.Add(id); // optimistic; undone below if Game Center refuses
                Social.ReportProgress(id, 100.0, ok =>
                {
                    if (ok) PlayerPrefs.SetString(ReportedKey, string.Join(",", _reported));
                    else _reported.Remove(id);
                });
            }
            foreach (var l in GameCenterContent.Leaderboards)
            {
                long v = GameCenterContent.Value(s, l.Stat);
                long last;
                if (v <= 0 || (_lastScore.TryGetValue(l.Id, out last) && last >= v)) continue;
                _lastScore[l.Id] = v;
                Social.ReportScore(v, l.Id, ok => { if (!ok) _lastScore.Remove(l.Id); });
            }
        }

        public static void ShowAchievements() { if (SignedIn) Social.ShowAchievementsUI(); else Start(); }
        public static void ShowLeaderboards() { if (SignedIn) Social.ShowLeaderboardUI(); else Start(); }
    }
}
