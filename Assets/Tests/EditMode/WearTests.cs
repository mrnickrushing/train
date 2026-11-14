using Ironhorse.Core;
using NUnit.Framework;

namespace Ironhorse.Tests
{
    public class WearTests
    {
        ManualClock _clock;
        GameSession _s;

        [SetUp]
        public void SetUp()
        {
            _clock = new ManualClock(Balance.SeasonEpoch + 3 * 86400);
            _s = new GameSession(GameSession.NewGame(_clock.Now, 7), _clock);
        }

        TrainState Starter { get { return _s.State.trains[0]; } }

        [Test]
        public void NoWearInTheFirstLevels()
        {
            Assert.IsFalse(_s.WearOn);
            Assert.IsTrue(_s.Dispatch(Starter.uid, "pinecrest").Ok);
            Assert.AreEqual(0, Starter.wear);
            Starter.wear = 1;
            Assert.AreEqual(GameSession.TripsWhenFresh(Starter.level), _s.TripsLeft(Starter), "wear means nothing before it begins");
        }

        [Test]
        public void SixTripsThenWornOut()
        {
            _s.State.level = Balance.WearFromLevel;
            Assert.AreEqual(6, GameSession.TripsWhenFresh(1));
            var d = GameDatabase.Destination("pinecrest");
            for (int i = 0; i < 6; i++)
            {
                Assert.AreEqual(6 - i, _s.TripsLeft(Starter));
                _s.State.fuel = _s.MaxFuel;
                Assert.IsTrue(_s.Dispatch(Starter.uid, "pinecrest").Ok, "trip " + (i + 1));
                Starter.arrivesAt = _clock.Now;             // home at once: no time for it to mend
                Assert.IsTrue(_s.Unload(Starter.uid).Ok);
            }
            Assert.AreEqual(0, _s.TripsLeft(Starter));
            _s.State.fuel = _s.MaxFuel;
            var p = _s.PreviewDispatch(Starter, d);
            Assert.IsFalse(p.Possible);
            StringAssert.Contains("worn out", p.Reason);
            Assert.IsFalse(_s.Dispatch(Starter.uid, "pinecrest").Ok);
        }

        [Test]
        public void AWornOutEngineGoesAgainAfterAWrenchOrARest()
        {
            _s.State.level = Balance.WearFromLevel;
            Starter.wear = 1; Starter.wearAt = _clock.Now;
            Assert.AreEqual(0, _s.TripsLeft(Starter));
            Assert.AreEqual(Balance.WearMendSeconds / 6, _s.RestNeeded(_s.WearOf(Starter), Starter.level), 1, "one trip's worth of rest");
            _clock.Advance(Balance.WearMendSeconds / 6 + 5);
            Assert.AreEqual(1, _s.TripsLeft(Starter), "rested enough for one trip");
            Assert.IsTrue(_s.Dispatch(Starter.uid, "pinecrest").Ok);

            Starter.wear = 1; Starter.wearAt = _clock.Now;
            int had = _s.Wrenches;
            Assert.IsTrue(_s.RepairTrain(Starter.uid).Ok);
            Assert.AreEqual(6, _s.TripsLeft(Starter));
            Assert.AreEqual(had - 1, _s.Wrenches);
            Assert.IsFalse(_s.RepairTrain(Starter.uid).Ok, "nothing to mend");
            Starter.wear = 0.8f; _s.State.wrenches = 0;
            Assert.IsFalse(_s.RepairTrain(Starter.uid).Ok, "no wrench");
        }

        [Test]
        public void TimeMendsItAllInTwelveHours()
        {
            _s.State.level = Balance.WearFromLevel;
            Starter.wear = 1; Starter.wearAt = _clock.Now;
            _clock.Advance(Balance.WearMendSeconds / 2);
            Assert.AreEqual(0.5f, _s.WearOf(Starter), 0.01f);
            _clock.Advance(Balance.WearMendSeconds);
            Assert.AreEqual(0, _s.WearOf(Starter), 1e-6, "never below fresh");
            Assert.AreEqual(12 * 3600, Balance.WearMendSeconds);
        }

        [Test]
        public void HaulersWearAndAreMendedToo()
        {
            var h = new HaulerState { uid = 9001, defId = Haulage.Haulers[0].Id, wear = 0.9f };
            _s.State.haulers.Add(h);
            Assert.IsTrue(_s.RepairHauler(h.uid).Ok);
            Assert.AreEqual(0, h.wear);
            Assert.IsFalse(_s.RepairHauler(h.uid).Ok);
        }

        [Test]
        public void AHigherLevelMakesMoreTrips()
        {
            Assert.AreEqual(7, GameSession.TripsWhenFresh(2));
            Assert.Greater(GameSession.TripsWhenFresh(5), GameSession.TripsWhenFresh(2));
        }

        [Test]
        public void WrenchesFromAnAdvertNuggetsAndLevels()
        {
            _s.State.wrenches = 0;
            Assert.IsTrue(_s.RewardAd(AdReward.Wrench).Ok);
            Assert.AreEqual(1, _s.Wrenches);
            _s.State.nuggets = Balance.WrenchNuggets;
            Assert.IsTrue(_s.BuyWrench().Ok);
            Assert.AreEqual(2, _s.Wrenches);
            Assert.AreEqual(0, _s.State.nuggets);
            Assert.IsFalse(_s.BuyWrench().Ok);
            _s.AddXp(Balance.XpForNextLevel(_s.State.level));
            Assert.AreEqual(2 + Balance.LevelUpWrenches, _s.Wrenches);
            for (int i = 0; i < 10; i++) _s.RewardAd(AdReward.Wrench);
            Assert.AreEqual(0, _s.AdsLeft(AdReward.Wrench), "a daily limit");
        }
    }
}
