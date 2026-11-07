using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Ironhorse.Core;
using NUnit.Framework;

namespace Ironhorse.Tests
{
    public class GameCenterContentTests
    {
        static string JsonPath()
        {
            // Unity runs tests from the project root; dotnet test from Tools/CoreTests/bin/...: walk up to find it.
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            for (; dir != null; dir = dir.Parent)
            {
                var p = Path.Combine(dir.FullName, "store", "gamecenter", "gamecenter.json");
                if (File.Exists(p)) return p;
            }
            return null;
        }

        [Test]
        public void Achievements_MatchAppStoreConnectDefinitions()
        {
            var path = JsonPath();
            Assert.NotNull(path, "store/gamecenter/gamecenter.json not found");
            var json = File.ReadAllText(path);
            var ach = Regex.Matches(json, "\"key\":\\s*\"([^\"]+)\",\\s*\"id\":\\s*\"([^\"]+)\",\\s*\"points\":\\s*\\d+,\\s*\"stat\":\\s*\"([^\"]+)\",\\s*\"goal\":\\s*(\\d+)")
                .Cast<Match>().Select(m => m.Groups[1].Value + "|" + m.Groups[2].Value + "|" + m.Groups[3].Value + "|" + m.Groups[4].Value).ToList();
            var code = GameCenterContent.Achievements.Select(a => a.Key + "|" + a.Id + "|" + a.Stat + "|" + a.Goal).ToList();
            CollectionAssert.AreEqual(ach, code);
            var lb = Regex.Matches(json, "\"id\":\\s*\"(ironhorse\\.lb\\.[^\"]+)\",\\s*\"stat\":\\s*\"([^\"]+)\"")
                .Cast<Match>().Select(m => m.Groups[1].Value + "|" + m.Groups[2].Value).ToList();
            CollectionAssert.AreEqual(lb, GameCenterContent.Leaderboards.Select(l => l.Id + "|" + l.Stat).ToList());
        }

        [Test]
        public void EveryStat_IsTracked()
        {
            var s = GameSession.NewGame(0, 1);
            s.stats.dispatches = 3; s.stats.contractsCompleted = 4; s.stats.coinsEarned = 5; s.level = 6;
            s.chaptersClaimed.Add("c1");
            foreach (var a in GameCenterContent.Achievements)
                Assert.AreNotEqual("?", a.Stat);
            Assert.AreEqual(3, GameCenterContent.Value(s, "dispatches"));
            Assert.AreEqual(4, GameCenterContent.Value(s, "contracts"));
            Assert.AreEqual(5, GameCenterContent.Value(s, "coins"));
            Assert.AreEqual(6, GameCenterContent.Value(s, "level"));
            Assert.AreEqual(1, GameCenterContent.Value(s, "chapters"));
            Assert.AreEqual(s.trains.Count, GameCenterContent.Value(s, "locos"));
            var stats = new[] { "dispatches", "contracts", "coins", "level", "locos", "chapters" };
            Assert.IsTrue(GameCenterContent.Achievements.All(a => stats.Contains(a.Stat)));
            s.seasonScore = 7;
            Assert.AreEqual(7, GameCenterContent.Value(s, "season"));
            Assert.IsTrue(GameCenterContent.Leaderboards.All(l => stats.Contains(l.Stat) || l.Stat == "season"));
        }
    }
}
