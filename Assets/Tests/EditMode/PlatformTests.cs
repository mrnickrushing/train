using System.Linq;
using Ironhorse.Core;
using NUnit.Framework;

namespace Ironhorse.Tests
{
    public class PlatformTests
    {
        ManualClock _clock;
        GameSession _s;

        [SetUp]
        public void SetUp()
        {
            _clock = new ManualClock();
            _s = new GameSession(GameSession.NewGame(_clock.Now, 3), _clock);
            _s.State.settings.tutorialDone = true;
        }

        TrainState AddTrain(string loco, params string[] wagons)
        {
            var t = new TrainState { uid = _s.State.nextUid++, defId = loco, platform = _s.State.trains.Count };
            _s.State.trains.Add(t);
            for (int i = 0; i < wagons.Length; i++) _s.State.wagons.Add(new WagonState { uid = _s.State.nextUid++, defId = wagons[i], trainUid = t.uid, slot = i });
            return t;
        }

        void Stock(params ResourceAmount[] goods) { foreach (var g in goods) _s.State.inventory.Change(g.id, g.amount); }

        [Test]
        public void NewGame_StartsWithThreePlatforms()
        {
            Assert.AreEqual(Balance.StartPlatforms, _s.PlatformsBuilt);
            Assert.AreEqual(3, _s.FleetCapacity);
            Assert.IsFalse(_s.StationComplete);
        }

        [Test]
        public void FullStation_RefusesNewLocomotive()
        {
            AddTrain("lark");
            AddTrain("redmesa");
            _s.State.level = 9;
            _s.State.coins = 100000;
            var r = _s.BuyLocomotive("copperqueen");
            Assert.IsFalse(r.Ok);
            StringAssert.Contains("lay a new platform", r.Message);
        }

        [Test]
        public void LayPlatform_NeedsLevelCoinsAndGoods_ThenOpensOnTimer()
        {
            Assert.IsFalse(_s.CanLayPlatform().Ok, "level gate");
            _s.State.level = _s.NextPlatformLevel;
            _s.State.coins = _s.NextPlatformCoins;
            Assert.IsFalse(_s.CanLayPlatform().Ok, "goods are needed too");
            Stock(_s.NextPlatformResources);
            Assert.IsTrue(_s.LayPlatform().Ok);
            Assert.AreEqual(0, _s.Coins);
            Assert.IsTrue(_s.NextPlatformResources.All(g => _s.Amount(g.id) == 0) || _s.LayingPlatform);
            Assert.IsTrue(_s.LayingPlatform);
            Assert.AreEqual(3, _s.FleetCapacity, "not open until the crew finishes");
            Assert.IsFalse(_s.LayPlatform().Ok, "one at a time");

            int opened = -1;
            _s.PlatformOpened += i => opened = i;
            _clock.Advance(Balance.PlatformBuildSeconds(3) / 2);
            _s.Tick();
            Assert.AreEqual(3, _s.FleetCapacity);
            Assert.AreEqual(0.5f, _s.PlatformProgress, 0.01f);
            _clock.Advance(Balance.PlatformBuildSeconds(3));
            _s.Tick();
            Assert.AreEqual(4, _s.FleetCapacity);
            Assert.AreEqual(3, opened);
            Assert.IsFalse(_s.LayingPlatform);
        }

        [Test]
        public void RushPlatform_OpensAtOnceForNuggets()
        {
            _s.State.level = 20;
            _s.State.coins = 1000000;
            Stock(Balance.PlatformResources(3));
            Assert.IsTrue(_s.LayPlatform().Ok);
            int cost = _s.PlatformRushCost;
            Assert.Greater(cost, 0);
            _s.State.nuggets = cost;
            Assert.IsTrue(_s.RushPlatform().Ok);
            Assert.AreEqual(0, _s.Nuggets);
            Assert.AreEqual(4, _s.FleetCapacity);
        }

        [Test]
        public void Station_StopsAtSixPlatforms()
        {
            _s.State.level = 30;
            for (int built = 3; built < Balance.Platforms; built++)
            {
                _s.State.coins = Balance.PlatformCoins(built);
                Stock(Balance.PlatformResources(built));
                Assert.IsTrue(_s.LayPlatform().Ok);
                _clock.Advance(Balance.PlatformBuildSeconds(built) + 1);
                _s.Tick();
            }
            Assert.AreEqual(Balance.Platforms, _s.FleetCapacity);
            Assert.IsTrue(_s.StationComplete);
            Assert.IsFalse(_s.CanLayPlatform().Ok);
        }

        [Test]
        public void OldSave_KeepsAPlatformForEveryEngine()
        {
            var st = GameSession.NewGame(_clock.Now, 4);
            st.platforms = 0; // saves from before platforms were laid one at a time
            for (int i = 1; i < 5; i++) st.trains.Add(new TrainState { uid = 500 + i, defId = "lark", platform = i });
            var s = new GameSession(st, _clock);
            Assert.AreEqual(5, s.FleetCapacity);

            var small = GameSession.NewGame(_clock.Now, 4);
            small.platforms = 0;
            Assert.AreEqual(Balance.StartPlatforms, new GameSession(small, _clock).FleetCapacity);
        }

        [Test]
        public void BoughtLocomotive_WithFullStation_LaysAPlatformForIt()
        {
            AddTrain("lark");
            AddTrain("redmesa");
            var r = _s.GrantPurchase("ironhorse.bundle_thunderhead", "x");
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(4, _s.FleetCapacity);
            Assert.AreEqual(4, _s.State.trains.Count);
            Assert.AreEqual(3, _s.State.trains.Last().platform);
        }

        // ---------------------------------------------------------------- selling engines

        [Test]
        public void SellLocomotive_PaysHalfAndFreesItsPlatform()
        {
            var lark = AddTrain("lark", "boxcar", "flatcar");
            AddTrain("redmesa");
            var e = _s.State.engineers.FirstOrDefault();
            if (e != null) { e.level = 1; e.trainUid = lark.uid; }
            int coins = _s.Coins;
            var r = _s.SellLocomotive(lark.uid);
            Assert.IsTrue(r.Ok, r.Message);
            Assert.AreEqual(coins + GameDatabase.Locomotive("lark").PriceCoins / 2, _s.Coins);
            Assert.IsNull(_s.Train(lark.uid));
            Assert.AreEqual(2, _s.DepotWagons().Count(w => w.defId == "boxcar" || w.defId == "flatcar"));
            if (e != null) Assert.AreEqual(0, e.trainUid);

            _s.State.level = 9;
            _s.State.coins = 100000;
            Assert.IsTrue(_s.BuyLocomotive("copperqueen").Ok);
            Assert.AreEqual(1, _s.State.trains.First(t => t.defId == "copperqueen").platform);
        }

        [Test]
        public void SellLocomotive_RefundsSomeOverhaulsAndValuesNuggetEngines()
        {
            var lark = new TrainState { defId = "lark", level = 3 };
            var d = GameDatabase.Locomotive("lark");
            int expected = d.PriceCoins / 2 + (Balance.LocoUpgradeCoins(d, 1) + Balance.LocoUpgradeCoins(d, 2)) / 4;
            Assert.AreEqual(expected, GameSession.LocoSellPrice(lark));
            Assert.AreEqual(GameDatabase.Locomotive("sundown").PriceNuggets * 100, GameSession.LocoSellPrice(new TrainState { defId = "sundown" }));
            Assert.Greater(GameSession.LocoSellPrice(new TrainState { defId = "dusty" }), 0);
        }

        [Test]
        public void SellLocomotive_RefusesLastEngineAndBusyOnes()
        {
            var dusty = _s.State.trains[0];
            Assert.IsFalse(_s.SellLocomotive(dusty.uid).Ok, "only engine");

            var lark = AddTrain("lark");
            lark.destinationId = "pinecrest";
            lark.arrivesAt = _clock.Now + 100;
            Assert.IsFalse(_s.SellLocomotive(lark.uid).Ok, "out on the line");
            _clock.Advance(200);
            Assert.IsFalse(_s.SellLocomotive(lark.uid).Ok, "arrived, not unloaded");

            var mesa = AddTrain("redmesa");
            mesa.crate = true;
            Assert.IsFalse(_s.SellLocomotive(mesa.uid).Ok, "crate still aboard");
            mesa.crate = false;
            Assert.IsTrue(_s.SellLocomotive(mesa.uid).Ok);
        }
    }
}
