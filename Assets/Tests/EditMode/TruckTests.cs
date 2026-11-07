using System.Linq;
using Ironhorse.Core;
using NUnit.Framework;

namespace Ironhorse.Tests
{
    /// <summary>The Motor Age: a garage, its trucks and the local delivery board.</summary>
    public class TruckTests
    {
        ManualClock _clock;
        GameSession _s;

        [SetUp]
        public void SetUp()
        {
            _clock = new ManualClock();
            _s = new GameSession(GameSession.NewGame(_clock.Now, 7), _clock);
            _s.State.settings.tutorialDone = true;
            _s.State.level = Motor.UnlockLevel;
            _s.State.coins = 100000;
            _s.State.nuggets = 1000;
            _s.State.warehouseLevel = 10;
            // A town that makes planks, flour and tools.
            foreach (var id in new[] { "sawmill", "gristmill", "toolworks" })
                _s.State.buildings.Add(new BuildingState { uid = _s.State.nextUid++, defId = id, plot = 20 + _s.State.buildings.Count, readyAt = _clock.Now });
            foreach (var id in new[] { "planks", "flour", "tools", "steel" }) _s.State.inventory.Change(id, 200);
        }

        BuildingState BuildGarage()
        {
            var g = new BuildingState { uid = _s.State.nextUid++, defId = Motor.GarageId, plot = 30, readyAt = _clock.Now };
            _s.State.buildings.Add(g);
            _s.Tick();
            return g;
        }

        TruckState Runabout { get { return _s.State.trucks[0]; } }

        [Test]
        public void Garage_UnlocksAtLevel12()
        {
            _s.State.level = Motor.UnlockLevel - 1;
            Assert.IsFalse(_s.CanConstruct(Motor.GarageId).Ok);
            _s.State.level = Motor.UnlockLevel;
            Assert.IsTrue(_s.CanConstruct(Motor.GarageId).Ok, _s.CanConstruct(Motor.GarageId).Message);
        }

        [Test]
        public void NoGarage_NoTrucksAndNoBoard()
        {
            _s.Tick();
            Assert.IsEmpty(_s.State.trucks);
            Assert.IsEmpty(_s.State.deliveries);
            Assert.IsFalse(_s.BuyTruck("stakebed").Ok);
        }

        [Test]
        public void FinishedGarage_GivesTheRunabout_AndFillsTheBoard_Once()
        {
            var g = new BuildingState { uid = _s.State.nextUid++, defId = Motor.GarageId, plot = 30, readyAt = _clock.Now + 100 };
            _s.State.buildings.Add(g);
            _s.Tick();
            Assert.IsEmpty(_s.State.trucks, "nothing until the garage is built");
            _clock.Advance(101);
            _s.Tick();
            Assert.AreEqual(1, _s.State.trucks.Count);
            Assert.AreEqual(Motor.StarterTruck, Runabout.defId);
            Assert.AreEqual(Motor.DeliverySlots, _s.State.deliveries.Count);
            // Selling the truck later doesn't bring a second free one.
            _s.State.trucks.Clear();
            _s.Tick();
            Assert.IsEmpty(_s.State.trucks);
        }

        [Test]
        public void Orders_AskOnlyForGoodsTheTownMakes_AndFitTheTrucks()
        {
            BuildGarage();
            var made = _s.ObtainableResources();
            foreach (var d in _s.State.deliveries)
            {
                CollectionAssert.Contains(made, d.resourceId);
                Assert.LessOrEqual(d.amount, _s.BiggestTruckLoad);
                Assert.GreaterOrEqual(d.amount, 2);
                Assert.Greater(d.rewardCoins, 0);
                Assert.Greater(d.rewardXp, 0);
                Assert.IsTrue(Motor.Customer(d.customerId).Wants.Contains(d.resourceId), d.customerId + " doesn't want " + d.resourceId);
            }
            Assert.AreEqual(_s.State.deliveries.Count, _s.State.deliveries.Select(d => d.customerId).Distinct().Count(), "different customers on a fresh board");
        }

        [Test]
        public void Delivery_TakesGoods_DrivesAndPaysOnReturn()
        {
            BuildGarage();
            var job = _s.State.deliveries[0];
            int goods = _s.Amount(job.resourceId), coins = _s.Coins;
            var r = _s.SendTruck(Runabout.uid, job.uid);
            Assert.IsTrue(r.Ok, r.Message);
            Assert.AreEqual(goods - job.amount, _s.Amount(job.resourceId), "goods leave the warehouse at once");
            Assert.IsFalse(_s.State.deliveries.Contains(job));
            Assert.IsTrue(_s.TruckOut(Runabout));
            Assert.IsFalse(_s.CollectTruck(Runabout.uid).Ok, "no pay on the road");

            double trip = Runabout.returnsAt - _clock.Now;
            Assert.AreEqual(Motor.Customer(job.customerId).Seconds, trip, 0.01, "the Runabout drives at reference speed");
            _clock.Advance(trip);
            bool back = false;
            _s.TruckReturned += t => back = true;
            _s.Tick();
            Assert.IsTrue(back);
            Assert.IsTrue(_s.TruckBack(Runabout));

            int stat = _s.State.stats.truckDeliveries;
            Assert.IsTrue(_s.CollectTruck(Runabout.uid).Ok);
            Assert.GreaterOrEqual(_s.Coins, coins + job.rewardCoins);
            Assert.AreEqual(stat + 1, _s.State.stats.truckDeliveries);
            Assert.IsTrue(_s.TruckIdle(Runabout));
            Assert.IsFalse(_s.CollectTruck(Runabout.uid).Ok, "paid once");
        }

        [Test]
        public void TooBigForTheTruck_OrShortOnGoods_CantBeSent()
        {
            BuildGarage();
            var job = _s.State.deliveries[0];
            job.amount = _s.TruckLoad(Runabout) + 1;
            StringAssert.StartsWith("Carries", _s.SendTruck(Runabout.uid, job.uid).Message);
            job.amount = 3;
            _s.State.inventory.Change(job.resourceId, -_s.Amount(job.resourceId));
            StringAssert.StartsWith("Need", _s.SendTruck(Runabout.uid, job.uid).Message);
        }

        [Test]
        public void Board_RefillsAfterASend_AndSkipWaitsTheRefreshTime()
        {
            BuildGarage();
            Assert.IsTrue(_s.SendTruck(Runabout.uid, _s.State.deliveries[0].uid).Ok);
            Assert.AreEqual(Motor.DeliverySlots - 1, _s.State.deliveries.Count);
            _clock.Advance(16);
            _s.Tick();
            Assert.AreEqual(Motor.DeliverySlots, _s.State.deliveries.Count);

            Assert.IsTrue(_s.SkipDelivery(_s.State.deliveries[0].uid).Ok);
            _clock.Advance(Motor.DeliveryRefreshSeconds - 5);
            _s.Tick();
            Assert.AreEqual(Motor.DeliverySlots - 1, _s.State.deliveries.Count);
            _clock.Advance(10);
            _s.Tick();
            Assert.AreEqual(Motor.DeliverySlots, _s.State.deliveries.Count);
        }

        [Test]
        public void Bays_LimitTrucks_AndGrowWithTheGarage()
        {
            var g = BuildGarage();
            Assert.AreEqual(2, _s.GarageBays);
            _s.State.level = 20;
            Assert.IsTrue(_s.BuyTruck("stakebed").Ok);
            Assert.IsFalse(_s.BuyTruck("bulldog").Ok, "two bays at level 1");
            Assert.IsTrue(_s.UpgradeBuilding(g.uid).Ok);
            Assert.AreEqual(3, _s.GarageBays);
            Assert.IsTrue(_s.BuyTruck("bulldog").Ok);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, _s.State.trucks.Select(t => t.bay));
        }

        [Test]
        public void TrucksUnlockByLevel_AndTheVanCostsNuggets()
        {
            var g = BuildGarage();
            _s.State.level = 12;
            StringAssert.Contains("level 13", _s.BuyTruck("stakebed").Message);
            _s.State.level = 16;
            int nuggets = _s.Nuggets, coins = _s.Coins;
            Assert.IsTrue(_s.BuyTruck("cometvan").Ok);
            Assert.AreEqual(nuggets - Motor.Truck("cometvan").PriceNuggets, _s.Nuggets);
            Assert.AreEqual(coins, _s.Coins);
        }

        [Test]
        public void Upgrade_CarriesMoreAndDrivesFaster()
        {
            BuildGarage();
            var c = Motor.Customer("generalstore");
            int load = _s.TruckLoad(Runabout);
            double trip = _s.TruckTripSeconds(Runabout, c);
            Assert.IsTrue(_s.UpgradeTruck(Runabout.uid).Ok);
            Assert.Greater(_s.TruckLoad(Runabout), load);
            Assert.Less(_s.TruckTripSeconds(Runabout, c), trip);
            Runabout.level = Motor.MaxTruckLevel;
            Assert.IsFalse(_s.UpgradeTruck(Runabout.uid).Ok);
        }

        [Test]
        public void Rush_BringsTheTruckBack()
        {
            BuildGarage();
            Assert.IsTrue(_s.SendTruck(Runabout.uid, _s.State.deliveries[0].uid).Ok);
            int cost = _s.TruckRushCost(Runabout), nuggets = _s.Nuggets;
            Assert.Greater(cost, 0);
            Assert.IsTrue(_s.RushTruck(Runabout.uid).Ok);
            Assert.AreEqual(nuggets - cost, _s.Nuggets);
            Assert.IsTrue(_s.TruckBack(Runabout));
            Assert.IsTrue(_s.CollectTruck(Runabout.uid).Ok);
        }

        [Test]
        public void TripsFinishWhileTheAppIsClosed()
        {
            BuildGarage();
            Assert.IsTrue(_s.SendTruck(Runabout.uid, _s.State.deliveries[0].uid).Ok);
            _clock.Advance(3600);
            var s = new GameSession(_s.State, _clock); // the next launch, an hour later
            var t = s.State.trucks[0];
            Assert.IsTrue(s.TruckBack(t));
            Assert.IsTrue(s.CollectTruck(t.uid).Ok);
            Assert.IsTrue(s.TruckIdle(t));
        }

        [Test]
        public void DamagedSave_IsRepaired()
        {
            BuildGarage();
            var st = _s.State;
            st.trucks.Add(null);
            st.trucks.Add(new TruckState { uid = 9001, defId = "removed_truck" });
            st.trucks.Add(new TruckState { uid = 9002, defId = "stakebed", bay = Runabout.bay, level = 99, job = null });
            st.trucks.Add(new TruckState { uid = 9003, defId = "bulldog", bay = 5, job = new DeliveryState { uid = 9004, customerId = "gone", resourceId = "planks", amount = 4 }, returnsAt = _clock.Now + 50 });
            st.deliveries.Add(new DeliveryState { uid = 9005, customerId = "nobody", resourceId = "planks", amount = 3 });
            st.deliveries.Add(null);
            st.nextUid = 5;
            var s = new GameSession(st, _clock);
            Assert.AreEqual(3, s.State.trucks.Count);
            Assert.AreEqual(s.State.trucks.Count, s.State.trucks.Select(t => t.bay).Distinct().Count(), "one truck per bay");
            Assert.AreEqual(Motor.MaxTruckLevel, s.Truck(9002).level);
            Assert.IsTrue(s.TruckIdle(s.Truck(9002)));
            Assert.IsTrue(s.TruckIdle(s.Truck(9003)), "a trip to an unknown customer comes home empty");
            Assert.IsTrue(s.State.deliveries.All(d => d != null && Motor.Customer(d.customerId) != null));
            Assert.Greater(s.State.nextUid, 9003, "new uids never collide with a truck's");
        }

        [Test]
        public void Charter_KeepsTrucks_ClearsLoadsAndBoard()
        {
            BuildGarage();
            Assert.IsTrue(_s.SendTruck(Runabout.uid, _s.State.deliveries[0].uid).Ok);
            _s.State.projects.First(p => p.id == "goldenspike").done = true;
            Assert.IsTrue(_s.CharterNewTerritory().Ok);
            Assert.AreEqual(1, _s.State.trucks.Count);
            Assert.IsTrue(_s.TruckIdle(Runabout));
            Assert.IsEmpty(_s.State.deliveries);
            Assert.IsFalse(_s.HasGarage);
            _s.Tick();
            Assert.IsEmpty(_s.State.deliveries, "no board until the new town builds a garage");
        }

        [Test]
        public void NextStep_CollectsTrucks_ThenSendsThem()
        {
            BuildGarage();
            // Trains and other chores out of the way.
            Assert.IsTrue(_s.Dispatch(_s.State.trains[0].uid, "pinecrest").Ok);
            _s.State.paydayLastDay = int.MaxValue / 2;
            _s.State.dailyTasks.Clear();
            _s.State.contracts.Clear();
            _s.State.platforms = Balance.Platforms;
            foreach (var b in _s.State.buildings) b.lastCollectAt = _clock.Now;

            var a = _s.NextAction();
            Assert.AreEqual(AdviceKind.SendTruck, a.Kind);
            Assert.IsTrue(_s.State.deliveries.Any(d => d.uid == a.Uid));
            Assert.AreEqual(_s.State.deliveries.Max(d => d.rewardCoins), _s.State.deliveries.First(d => d.uid == a.Uid).rewardCoins, "best-paying order first");
            Assert.IsTrue(_s.SendTruck(Runabout.uid, a.Uid).Ok);
            Assert.IsTrue(_s.RushTruck(Runabout.uid).Ok);
            a = _s.NextAction();
            Assert.AreEqual(AdviceKind.CollectTruck, a.Kind);
            Assert.AreEqual(Runabout.uid, a.Uid);
        }

        [Test]
        public void DailyBoard_CanAskForTruckDeliveries()
        {
            BuildGarage();
            var task = new DailyTask { kind = DailyKind.TruckDeliveries, count = 2, baseline = _s.State.stats.truckDeliveries };
            _s.State.dailyTasks.Clear();
            _s.State.dailyTasks.Add(task);
            Assert.AreEqual("Make 2 truck deliveries", Frontier.DailyText(task));
            for (int i = 0; i < 2; i++)
            {
                Assert.IsTrue(_s.SendTruck(Runabout.uid, _s.State.deliveries[0].uid).Ok);
                Assert.IsTrue(_s.RushTruck(Runabout.uid).Ok);
                Assert.IsTrue(_s.CollectTruck(Runabout.uid).Ok);
            }
            Assert.IsTrue(_s.IsDailyDone(task));
        }
    }
}
