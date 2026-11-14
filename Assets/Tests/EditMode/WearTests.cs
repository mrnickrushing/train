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
        }

        [Test]
        public void ATripWearsTheEngineOnceWearIsOn()
        {
            _s.State.level = Balance.WearFromLevel;
            Assert.IsTrue(_s.Dispatch(Starter.uid, "pinecrest").Ok);
            Assert.Greater(Starter.wear, 0);
            Assert.Less(Starter.wear, 0.5f, "one short trip is nowhere near worn");
        }

        [Test]
        public void AWornEngineIsSlowerButStillGoes()
        {
            var d = GameDatabase.Destination("pinecrest");
            double fresh = _s.TripSeconds(Starter, d);
            Starter.wear = Balance.WornAt;
            Assert.AreEqual(fresh, _s.TripSeconds(Starter, d), 1e-6, "no penalty up to half worn");
            Starter.wear = 1;
            Assert.AreEqual(fresh * Balance.WornSlow, _s.TripSeconds(Starter, d), 1e-6);
            Assert.IsTrue(_s.Dispatch(Starter.uid, "pinecrest").Ok, "a worn-out engine still leaves");
        }

        [Test]
        public void AWrenchMendsAndIsSpent()
        {
            Starter.wear = 0.8f;
            int had = _s.Wrenches;
            Assert.Greater(had, 0, "a new railroad has wrenches in the toolbox");
            Assert.IsTrue(_s.RepairTrain(Starter.uid).Ok);
            Assert.AreEqual(0, Starter.wear);
            Assert.AreEqual(had - 1, _s.Wrenches);
            Assert.IsFalse(_s.RepairTrain(Starter.uid).Ok, "nothing to mend");
            Starter.wear = 0.8f; _s.State.wrenches = 0;
            Assert.IsFalse(_s.RepairTrain(Starter.uid).Ok, "no wrench");
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
        public void AHigherLevelLastsLonger()
        {
            Assert.AreEqual(1, Balance.Durability(1), 1e-9);
            Assert.Greater(Balance.Durability(5), Balance.Durability(2));
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
