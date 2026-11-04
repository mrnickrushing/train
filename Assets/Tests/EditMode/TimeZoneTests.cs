using System.Linq;
using Ironhorse.Core;
using NUnit.Framework;

namespace Ironhorse.Tests
{
    /// <summary>Daily resets follow local midnight, and the offset moves with daylight saving and travel:
    /// a smaller offset must never hand out the same day twice.</summary>
    public class TimeZoneTests
    {
        const double H = 3600;
        ManualClock _clock;
        GameSession _s;
        double _midnightUtc;

        [SetUp]
        public void SetUp()
        {
            _midnightUtc = System.Math.Ceiling((Balance.SeasonEpoch + 86400) / 86400.0) * 86400 + 10 * 86400;
            _clock = new ManualClock(_midnightUtc);
            _s = new GameSession(GameSession.NewGame(_clock.Now, 3), _clock);
            _s.State.settings.tutorialDone = true;
        }

        /// <summary>Sets the clock to a local time of day (hours after local midnight, may be negative) on the base day.</summary>
        void At(double offsetHours, double localHours)
        {
            _s.UtcOffsetSeconds = offsetHours * H;
            _clock.Now = _midnightUtc + (localHours - offsetHours) * H;
            _s.Tick();
        }

        [Test]
        public void FlyingWest_AfterPayday_DoesNotPayTheSameDayAgain()
        {
            At(+9, 8); // Tokyo, 8 am
            Assert.IsTrue(_s.ClaimPayday().Ok);
            int coins = _s.Coins, nuggets = _s.Nuggets;

            _s.UtcOffsetSeconds = -10 * H; // same instant in Honolulu: the afternoon before
            _s.Tick();
            Assert.IsFalse(_s.CanClaimPayday);
            Assert.IsFalse(_s.ClaimPayday().Ok);
            Assert.AreEqual(coins, _s.Coins);
            Assert.AreEqual(nuggets, _s.Nuggets);
            Assert.Greater(_s.NextPaydayIn, 0);

            // Honolulu's midnight brings the calendar day already claimed in Tokyo: still nothing.
            At(-10, 1);
            Assert.IsFalse(_s.CanClaimPayday);
            // The day after, payday is back and the streak carried on.
            At(-10, 24 + 1);
            Assert.IsTrue(_s.CanClaimPayday);
            Assert.IsTrue(_s.ClaimPayday().Ok);
            Assert.AreEqual(2, _s.PaydayStreak);
        }

        [Test]
        public void ClocksFallBack_JustAfterMidnight_DoNotReopenPayday()
        {
            At(-4, 0.5); // 12:30 am, summer time
            Assert.IsTrue(_s.ClaimPayday().Ok);
            _s.UtcOffsetSeconds = -5 * H; // the clocks go back: it is 11:30 pm yesterday again
            _s.Tick();
            Assert.IsFalse(_s.CanClaimPayday);
            Assert.AreEqual(1, _s.PaydayDay, "today's claimed day is still shown");
            At(-5, 24 + 0.5);
            Assert.IsTrue(_s.CanClaimPayday);
        }

        [Test]
        public void ClocksSpringForward_ReachMidnightEarly_PaydayOpensOnce()
        {
            At(-5, -0.5); // 11:30 pm the evening before
            Assert.IsTrue(_s.ClaimPayday().Ok);
            _s.UtcOffsetSeconds = -4 * H; // an hour skipped: now 12:30 am, a new day
            _s.Tick();
            Assert.IsTrue(_s.CanClaimPayday);
            Assert.IsTrue(_s.ClaimPayday().Ok);
            Assert.IsFalse(_s.ClaimPayday().Ok);
            Assert.AreEqual(2, _s.PaydayStreak);
        }

        [Test]
        public void FlyingWest_KeepsTheDispatchersBoardAndWeeklyProgress()
        {
            At(+9, 8);
            var tasks = _s.State.dailyTasks.Select(t => t.kind + ":" + t.count).ToList();
            Assert.IsNotEmpty(tasks);
            _s.State.dailyPoints = 40;
            _s.State.dailyChests.Add(20);
            _s.State.weeklyPoints = 70;
            int day = _s.State.dailyDay, week = _s.State.weeklyWeek;

            _s.UtcOffsetSeconds = -10 * H;
            _s.Tick();
            Assert.AreEqual(day, _s.State.dailyDay);
            Assert.AreEqual(tasks, _s.State.dailyTasks.Select(t => t.kind + ":" + t.count).ToList(), "no fresh board to re-claim");
            Assert.AreEqual(40, _s.State.dailyPoints);
            CollectionAssert.Contains(_s.State.dailyChests, 20);
            Assert.AreEqual(week, _s.State.weeklyWeek);
            Assert.AreEqual(70, _s.State.weeklyPoints);
            Assert.Greater(_s.WeeklyResetIn, 0);

            At(-10, 24 + 1); // a real new day there
            Assert.Greater(_s.State.dailyDay, day);
            Assert.AreEqual(0, _s.State.dailyPoints);
        }

        [Test]
        public void WeeklyChests_DoNotResetTwiceAcrossMonday()
        {
            // Find the next local Monday midnight at UTC+0 by stepping days until the week number changes.
            At(0, 12);
            int week = _s.State.weeklyWeek;
            double h = 12;
            while (_s.State.weeklyWeek == week) { h += 24; At(0, h); }
            double monday = h - 12; // local midnight that started the new week
            _s.State.weeklyPoints = 50;
            _s.State.weeklyChests.Add(30);

            // Monday 1 am in London; the same instant is Sunday evening in New York.
            At(0, monday + 1);
            int newWeek = _s.State.weeklyWeek;
            _s.UtcOffsetSeconds = -5 * H;
            _s.Tick();
            Assert.AreEqual(newWeek, _s.State.weeklyWeek);
            Assert.AreEqual(50, _s.State.weeklyPoints);
            // New York reaches Monday too: the week already started, nothing resets.
            At(-5, monday + 1);
            Assert.AreEqual(50, _s.State.weeklyPoints);
            CollectionAssert.Contains(_s.State.weeklyChests, 30);
        }

        [Test]
        public void ClockSetForwardThenBack_DoesNotLockPaydayForever()
        {
            var st = _s.State;
            st.paydayLastDay = (int)(_midnightUtc / 86400) + 400; // claimed "a year from now"
            st.dailyDay = st.paydayLastDay;
            st.adDay = st.paydayLastDay;
            var s = new GameSession(st, _clock);
            s.Tick();
            Assert.IsFalse(s.CanClaimPayday, "today counts as claimed");
            Assert.Less(s.NextPaydayIn, 86400 + 1);
            Assert.LessOrEqual(s.State.dailyDay, (int)(_clock.Now / 86400), "a usable daily board");
            Assert.AreEqual(Balance.AdDailyLimit(AdReward.Fuel), s.AdsLeft(AdReward.Fuel));
            _clock.Advance(86400);
            Assert.IsTrue(s.CanClaimPayday);
        }
    }
}
