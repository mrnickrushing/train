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
                // A seasonal board starts from zero each season, so remember the best report per season.
                string key = l.Seasonal ? l.Id + "@" + s.passSeason : l.Id;
                if (v <= 0 || (_lastScore.TryGetValue(key, out last) && last >= v)) continue;
                _lastScore[key] = v;
                Social.ReportScore(v, l.Id, ok => { if (!ok) _lastScore.Remove(key); });
            }
        }

        public static void ShowAchievements() { if (SignedIn) Social.ShowAchievementsUI(); else Start(); }
        public static void ShowLeaderboards() { if (SignedIn) Social.ShowLeaderboardUI(); else Start(); }

        /// <summary>One row of a leaderboard: rank, player name, score; Me marks the local player.</summary>
        public struct Row { public int Rank; public string Name; public long Score; public bool Me; }

        /// <summary>The top of the Season Pass leaderboard for this season and the player's own row (null when
        /// Game Center isn't available or the player hasn't scored yet). Calls back on the main thread.</summary>
        public static void LoadSeason(int top, System.Action<List<Row>, Row?> done)
        {
            if (!SignedIn) { done(null, null); return; }
            var board = Social.CreateLeaderboard();
            board.id = GameCenterContent.SeasonBoard;
            board.timeScope = TimeScope.AllTime; // a recurring board only holds the current occurrence
            board.range = new Range(1, top);
            board.LoadScores(ok =>
            {
                if (!ok || board.scores == null) { done(null, null); return; }
                var ids = new List<string>();
                foreach (var sc in board.scores) ids.Add(sc.userID);
                Social.LoadUsers(ids.ToArray(), users =>
                {
                    var names = new Dictionary<string, string>();
                    if (users != null) foreach (var u in users) names[u.id] = u.userName;
                    var rows = new List<Row>();
                    string me = Social.localUser.id;
                    foreach (var sc in board.scores)
                    {
                        string n;
                        rows.Add(new Row { Rank = sc.rank, Name = names.TryGetValue(sc.userID, out n) ? n : "Railroader", Score = sc.value, Me = sc.userID == me });
                    }
                    Row? mine = null;
                    var ls = board.localUserScore;
                    if (ls != null && ls.rank > 0) mine = new Row { Rank = ls.rank, Name = Social.localUser.userName, Score = ls.value, Me = true };
                    done(rows, mine);
                });
            });
        }

        public static void ShowSeasonBoard()
        {
            if (!SignedIn) { Start(); return; }
            UnityEngine.SocialPlatforms.GameCenter.GameCenterPlatform.ShowLeaderboardUI(GameCenterContent.SeasonBoard, TimeScope.AllTime);
        }
    }
}
