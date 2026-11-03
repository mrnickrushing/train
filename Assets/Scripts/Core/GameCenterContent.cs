namespace Ironhorse.Core
{
    public sealed class GcAchievement
    {
        public readonly string Key, Id, Stat;
        public readonly long Goal;
        public GcAchievement(string key, string id, string stat, long goal) { Key = key; Id = id; Stat = stat; Goal = goal; }
    }

    public sealed class GcLeaderboard
    {
        public readonly string Id, Stat;
        public GcLeaderboard(string id, string stat) { Id = id; Stat = stat; }
    }

    /// <summary>
    /// Game Center achievements and leaderboards. They are registered in App Store Connect from
    /// store/gamecenter/gamecenter.json; GameCenterContentTests keeps this list in step with that file.
    /// </summary>
    public static class GameCenterContent
    {
        public static readonly GcAchievement[] Achievements =
        {
            new GcAchievement("first_dispatch", "ironhorse.ach.first_dispatch", "dispatches", 1),
            new GcAchievement("dispatch_100", "ironhorse.ach.dispatch_100", "dispatches", 100),
            new GcAchievement("contracts_10", "ironhorse.ach.contracts_10", "contracts", 10),
            new GcAchievement("contracts_100", "ironhorse.ach.contracts_100", "contracts", 100),
            new GcAchievement("level_5", "ironhorse.ach.level_5", "level", 5),
            new GcAchievement("level_10", "ironhorse.ach.level_10", "level", 10),
            new GcAchievement("level_15", "ironhorse.ach.level_15", "level", 15),
            new GcAchievement("locos_4", "ironhorse.ach.locos_4", "locos", 4),
            new GcAchievement("chapter_first", "ironhorse.ach.chapter_first", "chapters", 1),
            new GcAchievement("golden_spike", "ironhorse.ach.golden_spike", "chapters", 7),
            new GcAchievement("coins_100k", "ironhorse.ach.coins_100k", "coins", 100000)
        };

        public static readonly GcLeaderboard[] Leaderboards =
        {
            new GcLeaderboard("ironhorse.lb.coins", "coins"),
            new GcLeaderboard("ironhorse.lb.contracts", "contracts")
        };

        /// <summary>Current value of a tracked stat.</summary>
        public static long Value(GameState s, string stat)
        {
            switch (stat)
            {
                case "dispatches": return s.stats.dispatches;
                case "contracts": return s.stats.contractsCompleted;
                case "coins": return s.stats.coinsEarned;
                case "level": return s.level;
                case "locos": return s.trains.Count;
                case "chapters": return s.chaptersClaimed.Count;
                default: return 0;
            }
        }
    }
}
