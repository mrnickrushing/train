using System.Linq;
using Ironhorse.Core;
using NUnit.Framework;

namespace Ironhorse.Tests
{
    public class GameSessionTests
    {
        ManualClock _clock;
        GameSession _s;

        [SetUp]
        public void SetUp()
        {
            _clock = new ManualClock(1_000_000);
            _s = new GameSession(GameSession.NewGame(_clock.Now, 42), _clock);
        }

        TrainState Starter { get { return _s.State.trains[0]; } }

        [Test]
        public void NewGame_HasStarterTrainWagonsAndContracts()
        {
            Assert.AreEqual(1, _s.State.trains.Count);
            Assert.AreEqual(2, _s.WagonsOf(Starter).Count);
            Assert.AreEqual(Balance.BaseContractSlots, _s.State.contracts.Count);
            Assert.AreEqual(12, _s.Population);
        }

        [Test]
        public void Dispatch_ThenUnload_AddsCargoAndXp()
        {
            Assert.IsTrue(_s.Dispatch(Starter.uid, "pinecrest").Ok);
            Assert.IsTrue(_s.IsEnRoute(Starter));
            Assert.IsFalse(_s.Unload(Starter.uid).Ok, "can't unload mid-trip");

            int expected = Starter.cargoAmount;
            Assert.Greater(expected, 0);
            _clock.Advance(Starter.arrivesAt - _clock.Now + 1);
            int before = _s.Amount("timber");
            Assert.IsTrue(_s.Unload(Starter.uid).Ok);
            Assert.AreEqual(before + expected, _s.Amount("timber"));
            Assert.IsTrue(Starter.IsIdle);
            Assert.Greater(_s.State.xp, 0);
        }

        [Test]
        public void Dispatch_FailsWithoutMatchingWagon()
        {
            var r = _s.Dispatch(Starter.uid, "willowflats"); // grain needs a boxcar; also locked at L1
            Assert.IsFalse(r.Ok);
            _s.State.level = 2;
            r = _s.Dispatch(Starter.uid, "willowflats");
            Assert.IsFalse(r.Ok);
            StringAssert.Contains("boxcar", r.Message);
        }

        [Test]
        public void FasterLocomotive_HasShorterTrip()
        {
            var d = GameDatabase.Destination("pinecrest");
            double slow = _s.TripSeconds(Starter, d);
            Starter.level = 5;
            Assert.Less(_s.TripSeconds(Starter, d), slow);
        }

        [Test]
        public void Unload_PartialWhenWarehouseFull()
        {
            _s.State.inventory.Change("coal", _s.WarehouseFree - 5);
            _s.Dispatch(Starter.uid, "blackhollow");
            int cargo = Starter.cargoAmount;
            _clock.Advance(10_000);
            Assert.IsTrue(_s.Unload(Starter.uid).Ok);
            Assert.AreEqual(cargo - 5, Starter.cargoAmount);
            Assert.IsFalse(Starter.IsIdle);
            Assert.IsFalse(_s.Unload(Starter.uid).Ok);
        }

        [Test]
        public void Rush_CostsNuggetsAndCompletesTrip()
        {
            _s.Dispatch(Starter.uid, "pinecrest");
            int nuggets = _s.Nuggets;
            int cost = _s.TrainRushCost(Starter);
            Assert.IsTrue(_s.RushTrain(Starter.uid).Ok);
            Assert.AreEqual(nuggets - cost, _s.Nuggets);
            Assert.IsTrue(_s.HasArrived(Starter));
        }

        [Test]
        public void ArrivalEvent_FiresOnceOnTick()
        {
            int arrivals = 0;
            _s.TrainArrived += t => arrivals++;
            _s.Dispatch(Starter.uid, "pinecrest");
            _s.Tick();
            Assert.AreEqual(0, arrivals);
            _clock.Advance(10_000);
            _s.Tick();
            _s.Tick();
            Assert.AreEqual(1, arrivals);
        }

        [Test]
        public void BuyLocomotive_RespectsLevelAndCoins()
        {
            Assert.IsFalse(_s.BuyLocomotive("lark").Ok, "locked at level 1");
            _s.State.level = 2;
            _s.State.coins = 100;
            Assert.IsFalse(_s.BuyLocomotive("lark").Ok, "too poor");
            _s.State.coins = 5000;
            Assert.IsTrue(_s.BuyLocomotive("lark").Ok);
            Assert.AreEqual(2, _s.State.trains.Count);
            Assert.AreNotEqual(_s.State.trains[0].platform, _s.State.trains[1].platform);
        }

        [Test]
        public void AttachWagon_RespectsLocomotivePower()
        {
            _s.State.coins = 10_000;
            Assert.IsTrue(_s.BuyWagon("flatcar").Ok);
            var w = _s.DepotWagons().Single();
            var r = _s.AttachWagon(w.uid, Starter.uid);
            Assert.IsFalse(r.Ok, "Dusty can only pull 2");
            var existing = _s.WagonsOf(Starter)[0];
            Assert.IsTrue(_s.DetachWagon(existing.uid).Ok);
            Assert.IsTrue(_s.AttachWagon(w.uid, Starter.uid).Ok);
        }

        [Test]
        public void Construct_PaysCost_AndCompletesAfterTimer()
        {
            _s.State.inventory.Change("timber", 50);
            int coins = _s.Coins;
            int plot = _s.FreePlots()[0];
            Assert.IsTrue(_s.Construct("sawmill", plot).Ok);
            var b = _s.BuildingOnPlot(plot);
            Assert.Less(_s.Coins, coins);
            Assert.IsFalse(_s.IsReady(b));
            Assert.IsFalse(_s.StartProduction(b.uid, 1).Ok);
            _clock.Advance(1000);
            Assert.IsTrue(_s.IsReady(b));
        }

        [Test]
        public void Production_ConsumesInputsAndYieldsOutput()
        {
            _s.State.inventory.Change("timber", 100);
            int plot = _s.FreePlots()[0];
            _s.Construct("sawmill", plot);
            var b = _s.BuildingOnPlot(plot);
            _clock.Advance(1000);
            int timber = _s.Amount("timber");
            Assert.IsTrue(_s.StartProduction(b.uid, 2).Ok);
            Assert.AreEqual(timber - 12, _s.Amount("timber"));
            Assert.IsFalse(_s.CollectProduction(b.uid).Ok);
            _clock.Advance(1000);
            Assert.IsTrue(_s.CollectProduction(b.uid).Ok);
            Assert.AreEqual(6, _s.Amount("planks"));
            Assert.AreEqual(6, _s.State.stats.produced.Get("planks"));
        }

        [Test]
        public void Rent_AccumulatesAndCapsAtStorage()
        {
            var cabin = _s.State.buildings.First(b => b.defId == "cabin");
            Assert.AreEqual(0, _s.PendingRent(cabin));
            _clock.Advance(600);
            int tenMin = _s.PendingRent(cabin);
            Assert.Greater(tenMin, 0);
            _clock.Advance(100_000);
            int capped = _s.PendingRent(cabin);
            _clock.Advance(100_000);
            Assert.AreEqual(capped, _s.PendingRent(cabin));
            int coins = _s.Coins;
            Assert.IsTrue(_s.CollectRent(cabin.uid).Ok);
            Assert.AreEqual(coins + capped, _s.Coins);
            Assert.AreEqual(0, _s.PendingRent(cabin));
        }

        [Test]
        public void Saloon_BoostsIncome()
        {
            var cabin = _s.State.buildings.First(b => b.defId == "cabin");
            double before = _s.RentPerMinute(cabin);
            _s.State.level = 3;
            _s.State.coins = 10_000;
            _s.State.inventory.Change("planks", 50);
            Assert.IsTrue(_s.Construct("saloon", _s.FreePlots()[0]).Ok);
            _clock.Advance(10_000);
            Assert.AreEqual(before * 1.10, _s.RentPerMinute(cabin), 1e-6);
        }

        [Test]
        public void LevelUp_GrantsRewardsAndFiresEvent()
        {
            int levels = 0;
            _s.LeveledUp += l => levels++;
            int nuggets = _s.Nuggets;
            _s.AddXp(Balance.XpForNextLevel(1) + Balance.XpForNextLevel(2));
            Assert.AreEqual(3, _s.Level);
            Assert.AreEqual(2, levels);
            Assert.Greater(_s.Nuggets, nuggets);
        }

        [Test]
        public void Contract_DeliverPaysReward_AndRefills()
        {
            var c = _s.State.contracts[0];
            foreach (var r in c.requirements) _s.State.inventory.Change(r.id, r.amount);
            int coins = _s.Coins;
            Assert.IsTrue(_s.DeliverContract(c.uid).Ok);
            Assert.AreEqual(coins + c.rewardCoins, _s.Coins);
            Assert.AreEqual(Balance.BaseContractSlots - 1, _s.State.contracts.Count);
            _clock.Advance(15);
            _s.Tick();
            Assert.AreEqual(Balance.BaseContractSlots, _s.State.contracts.Count);
        }

        [Test]
        public void Contracts_OnlyAskForObtainableResources()
        {
            var obtainable = _s.ObtainableResources();
            foreach (var c in _s.State.contracts)
                foreach (var r in c.requirements)
                    CollectionAssert.Contains(obtainable, r.id);
        }

        [Test]
        public void Contracts_AreDeterministicForSeed()
        {
            var other = new GameSession(GameSession.NewGame(_clock.Now, 42), _clock);
            Assert.AreEqual(_s.State.contracts.Select(c => c.client + c.rewardCoins), other.State.contracts.Select(c => c.client + c.rewardCoins));
        }

        [Test]
        public void Discard_WaitsForCooldown()
        {
            int n = _s.State.contracts.Count;
            _s.DiscardContract(_s.State.contracts[0].uid);
            _s.Tick();
            Assert.AreEqual(n - 1, _s.State.contracts.Count);
            _clock.Advance(Balance.ContractRefreshSeconds + 1);
            _s.Tick();
            Assert.AreEqual(n, _s.State.contracts.Count);
        }

        [Test]
        public void Goals_ProgressAndClaim()
        {
            var g = _s.CurrentGoal;
            Assert.AreEqual("g1", g.Id);
            Assert.IsFalse(_s.ClaimGoal().Ok);
            _s.Dispatch(Starter.uid, "pinecrest");
            Assert.IsTrue(_s.ClaimGoal().Ok);
            Assert.AreEqual("g2", _s.CurrentGoal.Id);
        }

        [Test]
        public void Warehouse_UpgradeIncreasesCapacity()
        {
            int cap = _s.WarehouseCapacity;
            _s.State.coins = 10_000;
            _s.State.inventory.Change("timber", 100);
            Assert.IsTrue(_s.UpgradeWarehouse().Ok);
            Assert.Greater(_s.WarehouseCapacity, cap);
        }

        [Test]
        public void BuyPlot_IncreasesLand()
        {
            _s.State.coins = 100_000;
            int plots = _s.State.unlockedPlots;
            Assert.IsTrue(_s.BuyPlot().Ok);
            Assert.AreEqual(plots + 1, _s.State.unlockedPlots);
        }

        [Test]
        public void OfflineProgress_TrainsArriveWhileAway()
        {
            _s.Dispatch(Starter.uid, "pinecrest");
            var saved = _s.State;
            _clock.Advance(3600);
            var reloaded = new GameSession(saved, _clock);
            Assert.IsTrue(reloaded.HasArrived(reloaded.State.trains[0]));
            Assert.IsTrue(reloaded.Unload(reloaded.State.trains[0].uid).Ok);
        }

        [Test]
        public void Database_IsConsistent()
        {
            foreach (var d in GameDatabase.Destinations) Assert.IsNotNull(GameDatabase.Resource(d.ResourceId), d.Id);
            foreach (var b in GameDatabase.Buildings)
            {
                foreach (var r in b.CostResources) Assert.IsNotNull(GameDatabase.Resource(r.id), b.Id);
                if (b.Recipe != null)
                {
                    Assert.IsNotNull(GameDatabase.Resource(b.Recipe.Output.id), b.Id);
                    foreach (var r in b.Recipe.Inputs) Assert.IsNotNull(GameDatabase.Resource(r.id), b.Id);
                }
            }
            foreach (var g in GameDatabase.Goals)
            {
                if (g.Type == GoalType.ConstructBuilding) Assert.IsNotNull(GameDatabase.Building(g.Target), g.Id);
                if (g.Type == GoalType.CollectResource || g.Type == GoalType.ProduceResource) Assert.IsNotNull(GameDatabase.Resource(g.Target), g.Id);
            }
            // every cargo class used by a destination has a wagon that carries it
            foreach (var d in GameDatabase.Destinations)
            {
                var cls = GameDatabase.Resource(d.ResourceId).Cargo;
                Assert.IsTrue(GameDatabase.Wagons.Any(w => w.Cargo == cls && w.UnlockLevel <= d.UnlockLevel), d.Id + " has no wagon available at its unlock level");
            }
        }

        [Test]
        public void Simulation_CanReachLevel5ByPlaying()
        {
            // A crude bot plays the game; guards against economy dead-ends.
            for (int step = 0; step < 4000 && _s.Level < 5; step++)
            {
                _clock.Advance(30);
                _s.Tick();
                foreach (var t in _s.State.trains)
                {
                    if (_s.HasArrived(t)) _s.Unload(t.uid);
                    if (t.IsIdle)
                    {
                        var dest = GameDatabase.Destinations.Where(d => _s.PreviewDispatch(t, d).Possible)
                            .OrderBy(d => _s.Amount(d.ResourceId)).FirstOrDefault();
                        if (dest != null) _s.Dispatch(t.uid, dest.Id);
                    }
                }
                foreach (var c in _s.State.contracts.ToList()) if (_s.CanDeliver(c)) _s.DeliverContract(c.uid);
                _s.CollectAllRent();
                foreach (var b in _s.State.buildings.ToList())
                {
                    if (_s.IsProductionDone(b)) _s.CollectProduction(b.uid);
                    int n = _s.AffordableBatches(b);
                    if (n > 0 && b.queue.Count == 0) _s.StartProduction(b.uid, n);
                }
                if (_s.CountBuilt("sawmill") == 0 && _s.CanConstruct("sawmill").Ok) _s.Construct("sawmill", _s.FreePlots()[0]);
                if (_s.CanConstruct("cabin").Ok && _s.Coins > 1500) _s.Construct("cabin", _s.FreePlots()[0]);
                if (_s.Level >= 2 && _s.State.trains.Count < 2) _s.BuyLocomotive("lark");
                foreach (var t in _s.State.trains.Where(t => t.IsIdle))
                    if (_s.WagonsOf(t).Count < GameDatabase.Locomotive(t.defId).Power && _s.Coins > 600)
                        _s.BuyWagon(_s.WagonsOf(t).Count % 2 == 0 ? "flatcar" : "hopper", t.uid);
                if (_s.CurrentGoal != null && _s.IsGoalComplete(_s.CurrentGoal)) _s.ClaimGoal();
            }
            Assert.GreaterOrEqual(_s.Level, 5, "bot got stuck at level " + _s.Level);
        }
    
        [Test]
        public void TutorialOrder_PostedOnlyWhenNothingCanBeDelivered()
        {
            var s = new GameSession(GameSession.NewGame(1_000_000, 9), new ManualClock());
            s.State.contracts.Clear();
            s.State.inventory.Clear();
            Assert.IsFalse(s.PostTutorialOrder(), "nothing on hand to ask for");
            s.State.inventory.Change("planks", 6);
            Assert.IsTrue(s.PostTutorialOrder());
            var c = s.State.contracts[0];
            Assert.IsTrue(s.CanDeliver(c));
            Assert.IsFalse(s.PostTutorialOrder(), "one deliverable order is enough");
            Assert.IsTrue(s.DeliverContract(c.uid).Ok);
        }
    }

}
