using System.Linq;
using Ironhorse.Core;
using NUnit.Framework;

namespace Ironhorse.Tests
{
    public class DepthTests
    {
        ManualClock _clock;
        GameSession _s;

        [SetUp]
        public void SetUp()
        {
            _clock = new ManualClock(Balance.SeasonEpoch + 86400);
            _s = new GameSession(GameSession.NewGame(_clock.Now, 3), _clock);
        }

        BuildingState BuildSawmill()
        {
            _s.State.inventory.Change("timber", 200);
            int plot = _s.FreePlots()[0];
            Assert.IsTrue(_s.Construct("sawmill", plot).Ok);
            _clock.Advance(1000);
            return _s.BuildingOnPlot(plot);
        }

        // ---------------------------------------------------------------- conveyor & crew

        [Test]
        public void Conveyor_RunsJobsBackToBack()
        {
            var b = BuildSawmill();
            Assert.IsTrue(_s.Enqueue(b.uid).Ok);
            Assert.IsTrue(_s.Enqueue(b.uid).Ok);
            Assert.AreEqual(b.queue[0].endsAt, b.queue[1].startsAt, 1e-6);
            _clock.Advance(_s.ProductionSeconds(b, 1) + 1);
            Assert.AreEqual(1, _s.FinishedJobs(b));
            Assert.IsTrue(_s.IsProducing(b));
            Assert.IsTrue(_s.CollectProduction(b.uid).Ok);
            Assert.AreEqual(3, _s.Amount("planks"));
            Assert.AreEqual(1, b.queue.Count);
        }

        [Test]
        public void Conveyor_LimitedBySlots_AndExtraSlotCostsNuggets()
        {
            var b = BuildSawmill();
            int slots = _s.QueueSlots(b);
            for (int i = 0; i < slots; i++) Assert.IsTrue(_s.Enqueue(b.uid).Ok);
            Assert.IsFalse(_s.Enqueue(b.uid).Ok);
            _s.State.nuggets = 1000;
            Assert.IsTrue(_s.BuySlot(b.uid).Ok);
            Assert.AreEqual(slots + 1, _s.QueueSlots(b));
        }

        [Test]
        public void Crew_LimitsJobsAcrossFactories()
        {
            var b = BuildSawmill();
            b.extraSlots = 3;
            int crew = _s.CrewTotal;
            for (int i = 0; i < crew; i++) Assert.IsTrue(_s.Enqueue(b.uid).Ok, "job " + i);
            var r = _s.Enqueue(b.uid);
            Assert.IsFalse(r.Ok);
            StringAssert.Contains("crew", r.Message);
            _s.State.nuggets = 1000;
            Assert.IsTrue(_s.HireCrew().Ok);
            Assert.IsTrue(_s.Enqueue(b.uid).Ok);
        }

        [Test]
        public void Crew_ReturnsAfterCollect()
        {
            var b = BuildSawmill();
            _s.Enqueue(b.uid);
            int free = _s.CrewFree;
            _clock.Advance(1000);
            _s.CollectProduction(b.uid);
            Assert.AreEqual(free + 1, _s.CrewFree);
        }

        [Test]
        public void LegacyProduction_MigratesIntoQueue()
        {
            var b = BuildSawmill();
            b.productionBatches = 2;
            b.productionEndsAt = _clock.Now + 100;
            var reloaded = new GameSession(_s.State, _clock);
            var rb = reloaded.Building(b.uid);
            Assert.AreEqual(2, rb.queue.Count);
            Assert.AreEqual(0, rb.productionEndsAt);
            _clock.Advance(200);
            Assert.IsTrue(reloaded.CollectProduction(b.uid).Ok);
        }

        [Test]
        public void RushProduction_FinishesWholeQueue()
        {
            var b = BuildSawmill();
            _s.StartProduction(b.uid, 2);
            Assert.IsTrue(_s.RushProduction(b.uid).Ok);
            Assert.AreEqual(2, _s.FinishedJobs(b));
        }

        // ---------------------------------------------------------------- passengers

        TrainState CoachTrain()
        {
            _s.State.level = 6;
            _s.State.coins = 100000;
            var t = _s.State.trains[0];
            foreach (var w in _s.WagonsOf(t)) _s.DetachWagon(w.uid);
            Assert.IsTrue(_s.BuyWagon("mailcoach", t.uid).Ok);
            return t;
        }

        [Test]
        public void Passengers_PayFares_NotStored()
        {
            var t = CoachTrain();
            Assert.IsTrue(_s.Dispatch(t.uid, "silversprings").Ok);
            Assert.Greater(t.cargoAmount, 0);
            _clock.Advance(10000);
            int coins = _s.Coins, used = _s.WarehouseUsed;
            int pax = t.cargoAmount;
            Assert.IsTrue(_s.Unload(t.uid).Ok);
            Assert.Greater(_s.Coins, coins);
            Assert.AreEqual(used, _s.WarehouseUsed);
            Assert.AreEqual(pax, _s.State.stats.passengers);
            Assert.IsTrue(t.IsIdle);
        }

        [Test]
        public void Passengers_CappedByTownPopulation()
        {
            var t = CoachTrain();
            var p = _s.PreviewDispatch(t, GameDatabase.Destination("silversprings"));
            Assert.LessOrEqual(p.Amount, Balance.PassengerDemand(_s.Population));
        }

        [Test]
        public void Contracts_NeverAskForPassengersOrParts()
        {
            _s.State.level = 20;
            foreach (var id in _s.ObtainableResources())
            {
                var r = GameDatabase.Resource(id);
                Assert.IsFalse(r.IsPassenger || r.IsPart, id);
            }
        }

        // ---------------------------------------------------------------- parts & blueprints

        [Test]
        public void Parts_DoNotUseWarehouseSpace()
        {
            int used = _s.WarehouseUsed;
            _s.State.inventory.Change("bearings", 500);
            Assert.AreEqual(used, _s.WarehouseUsed);
        }

        [Test]
        public void LocoUpgrade_UsesParts()
        {
            var t = _s.State.trains[0];
            _s.State.coins = 100000;
            _s.State.inventory.Change("rivets", -_s.Amount("rivets"));
            Assert.IsFalse(_s.UpgradeLocomotive(t.uid).Ok);
            _s.State.inventory.Change("rivets", 50);
            Assert.IsTrue(_s.UpgradeLocomotive(t.uid).Ok);
            Assert.AreEqual(2, t.level);
        }

        [Test]
        public void Blueprints_AssembleLocomotive()
        {
            var d = GameDatabase.Locomotive("thunderhead");
            Assert.IsFalse(_s.AssembleLocomotive(d.Id).Ok);
            _s.Apply(new Grant { BlueprintLoco = d.Id, Blueprints = d.Blueprints });
            Assert.IsTrue(_s.CanAssemble(d));
            Assert.IsTrue(_s.AssembleLocomotive(d.Id).Ok);
            Assert.AreEqual("thunderhead", _s.State.trains.Last().defId);
            Assert.AreEqual(0, _s.BlueprintsOf(d.Id));
        }

        // ---------------------------------------------------------------- projects

        [Test]
        public void Project_PartialDeliveries_AdvanceStages_AndGrantPerk()
        {
            var def = DepthContent.Project("telegraph");
            Assert.IsFalse(_s.DeliverToProject(def.Id).Ok, "locked");
            _s.State.level = def.UnlockLevel;
            float xpBonus = _s.CivicBonus(CivicEffect.Xp);
            var first = def.Stages[0];
            _s.State.inventory.Change(first.ResourceId, -_s.Amount(first.ResourceId));
            _s.State.inventory.Change(first.ResourceId, first.Amount / 2);
            Assert.IsTrue(_s.DeliverToProject(def.Id).Ok);
            Assert.AreEqual(0, _s.Progress(def.Id).stage);
            Assert.AreEqual(first.Amount / 2, _s.Progress(def.Id).delivered);
            foreach (var st in def.Stages) _s.State.inventory.Change(st.ResourceId, st.Amount);
            for (int i = 0; i < def.Stages.Length + 1 && !_s.Progress(def.Id).done; i++) _s.DeliverToProject(def.Id);
            Assert.IsTrue(_s.Progress(def.Id).done);
            Assert.AreEqual(xpBonus + def.PerkValue, _s.CivicBonus(CivicEffect.Xp), 1e-5);
            Assert.IsFalse(_s.DeliverToProject(def.Id).Ok);
        }

        [Test]
        public void Projects_UseOnlyRealResources()
        {
            foreach (var p in DepthContent.Projects)
            {
                foreach (var st in p.Stages) Assert.IsNotNull(GameDatabase.Resource(st.ResourceId), p.Id);
                if (p.Reward.BlueprintLoco != null) Assert.Greater(GameDatabase.Locomotive(p.Reward.BlueprintLoco).Blueprints, 0, p.Id);
            }
        }

        // ---------------------------------------------------------------- foreman

        [Test]
        public void TaskSet_CountsProgressFromWhenItStarted()
        {
            _s.State.stats.dispatches = 50; // history before the set must not count
            var reloaded = new GameSession(_s.State, _clock);
            reloaded.State.taskBaselines.Clear();
            reloaded = new GameSession(reloaded.State, _clock);
            Assert.AreEqual(0, reloaded.TaskProgress(0));
        }

        [Test]
        public void TaskSet_ClaimAdvances()
        {
            Assert.IsFalse(_s.ClaimTaskSet().Ok);
            var set = _s.CurrentTaskSet;
            _s.State.stats.dispatches += 100;
            _s.State.stats.collected.Change("timber", 1000);
            _s.State.stats.produced.Change("planks", 1000);
            Assert.IsTrue(_s.CanClaimTaskSet);
            Assert.IsTrue(_s.ClaimTaskSet().Ok);
            Assert.AreEqual(set.Index + 1, _s.CurrentTaskSet.Index);
            Assert.AreEqual(0, _s.TasksDone, "new set starts fresh");
        }

        [Test]
        public void TaskSets_AreEndless()
        {
            for (int i = 0; i < 40; i++)
            {
                var set = DepthContent.TaskSet(i);
                Assert.AreEqual(3, set.Tasks.Length);
                Assert.IsNotNull(set.Reward);
            }
        }
    }
}
