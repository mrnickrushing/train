using System.Linq;
using Ironhorse.Core;
using NUnit.Framework;

namespace Ironhorse.Tests
{
    public class ChapterTests
    {
        ManualClock _clock;
        GameSession _s;

        [SetUp]
        public void SetUp()
        {
            _clock = new ManualClock(Balance.SeasonEpoch + 86400);
            _s = new GameSession(GameSession.NewGame(_clock.Now, 3), _clock);
        }

        [Test]
        public void Chapters_AreContiguousAndCoverTheProjects()
        {
            var cs = ChapterContent.Chapters;
            Assert.AreEqual(1, cs[0].StartLevel);
            for (int i = 1; i < cs.Count; i++) Assert.AreEqual(cs[i - 1].EndLevel, cs[i].StartLevel, cs[i].Id);
            foreach (var p in DepthContent.Projects)
            {
                var c = cs.Single(x => x.ProjectId == p.Id);
                Assert.AreEqual(c.StartLevel, p.UnlockLevel, "project " + p.Id + " unlocks when its chapter starts");
            }
        }

        [Test]
        public void FirstChapter_CompletesAtItsEndLevel_AndPaysOnce()
        {
            var c = _s.CurrentChapter;
            Assert.AreEqual("railhead", c.Id);
            Assert.IsFalse(_s.ClaimChapter(c.Id).Ok);
            _s.State.level = c.EndLevel;
            int coins = _s.Coins;
            Assert.IsTrue(_s.ClaimChapter(c.Id).Ok);
            Assert.AreEqual(coins + c.Reward.Coins, _s.Coins);
            Assert.IsFalse(_s.ClaimChapter(c.Id).Ok);
            Assert.AreEqual("wires", _s.CurrentChapter.Id);
        }

        [Test]
        public void ProjectChapter_NeedsItsProjectDone()
        {
            _s.State.chaptersClaimed.Add("railhead");
            var c = _s.CurrentChapter;
            _s.State.level = c.EndLevel;
            Assert.IsFalse(_s.IsChapterComplete(c));
            Assert.Less(_s.ChapterProgress(c), 1f);
            _s.State.projects.First(p => p.id == c.ProjectId).done = true;
            Assert.IsTrue(_s.IsChapterComplete(c));
            Assert.AreEqual(1f, _s.ChapterProgress(c), 1e-4);
            Assert.IsTrue(_s.ClaimChapter(c.Id).Ok);
        }

        [Test]
        public void NextUnlocks_AreAboveTheCurrentLevel()
        {
            var next = _s.NextUnlocks();
            Assert.IsNotEmpty(next);
            Assert.IsTrue(next.All(u => u.Level == next[0].Level && u.Level > _s.State.level));
        }
    }
}
