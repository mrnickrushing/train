using System.Linq;
using Ironhorse.Core;
using NUnit.Framework;

namespace Ironhorse.Tests
{
    public class FrontierTests
    {
        ManualClock _clock;
        GameSession _s;

        [SetUp]
        public void SetUp()
        {
            // A Monday, local midnight (2026-01-05 is a Monday).
            _clock = new ManualClock(1767571200 + 7 * 86400);
            _s = new GameSession(GameSession.NewGame(_clock.Now, 5), _clock);
            _s.State.settings.tutorialDone = true;
        }

        void SetLevel(int level) { _s.State.level = level; _s.State.xp = 0; }

        TrainState AddTrain(string loco, params string[] wagons)
        {
            var t = new TrainState { uid = _s.State.nextUid++, defId = loco, platform = _s.State.trains.Count };
            _s.State.trains.Add(t);
            for (int i = 0; i < wagons.Length; i++) _s.State.wagons.Add(new WagonState { uid = _s.State.nextUid++, defId = wagons[i], trainUid = t.uid, slot = i });
            return t;
        }

        // ---------------------------------------------------------------- dispatcher's board

        [Test]
        public void Daily_FourTasks_ClaimForPointsAndChests()
        {
            _s.Tick();
            var tasks = _s.State.dailyTasks;
            Assert.AreEqual(Frontier.DailyTaskCount, tasks.Count);
            Assert.AreEqual(DailyKind.Dispatch, tasks[0].kind);
            Assert.AreEqual(tasks.Count, tasks.Select(t => t.kind).Distinct().Count());
            Assert.IsFalse(_s.ClaimDailyTask(0).Ok);
            _s.State.stats.dispatches += tasks[0].count;
            Assert.IsTrue(_s.IsDailyDone(tasks[0]));
            Assert.AreEqual(1, _s.DailyClaimable);
            Assert.IsTrue(_s.ClaimDailyTask(0).Ok);
            Assert.IsFalse(_s.ClaimDailyTask(0).Ok);
            Assert.AreEqual(Frontier.DailyTaskPoints, _s.State.dailyPoints);
            Assert.AreEqual(Frontier.DailyTaskPoints, _s.State.weeklyPoints);
            _s.State.dailyPoints = 50;
            int coins = _s.Coins;
            Assert.IsTrue(_s.ClaimDailyChest(50).Ok);
            Assert.Greater(_s.Coins, coins);
            Assert.IsFalse(_s.ClaimDailyChest(50).Ok);
            Assert.IsFalse(_s.ClaimDailyChest(100).Ok);
        }

        [Test]
        public void Daily_NewBoardEachDay_WeeklyResetsOnMonday()
        {
            _s.Tick();
            _s.State.weeklyPoints = 300;
            int day = _s.State.dailyDay;
            _clock.Advance(86400);
            _s.Tick();
            Assert.AreEqual(day + 1, _s.State.dailyDay);
            Assert.AreEqual(0, _s.State.dailyPoints);
            Assert.AreEqual(300, _s.State.weeklyPoints, "same week");
            Assert.IsTrue(_s.CanClaimWeeklyChest(250));
            Assert.IsTrue(_s.ClaimWeeklyChest(250).Ok);
            _clock.Advance(6 * 86400); // next Monday
            _s.Tick();
            Assert.AreEqual(0, _s.State.weeklyPoints);
            Assert.AreEqual(0, _s.State.weeklyChests.Count);
        }

        // ---------------------------------------------------------------- engineers

        [Test]
        public void Crew_FirstEngineerJoinsAtLevel3_AndTrainsWithShards()
        {
            _s.Tick();
            Assert.AreEqual(0, _s.State.engineers.Count);
            SetLevel(Frontier.CrewLevel);
            _s.Tick();
            var jim = _s.Crew("bigjim");
            Assert.AreEqual(1, jim.level);
            Assert.AreEqual(_s.State.trains[0].uid, jim.trainUid);
            Assert.IsFalse(_s.TrainEngineer("bigjim").Ok);
            jim.shards = Frontier.LevelUpShards(1);
            Assert.IsTrue(_s.TrainEngineer("bigjim").Ok);
            Assert.AreEqual(2, jim.level);
            Assert.AreEqual(0, jim.shards);
            Assert.IsFalse(_s.AssignEngineer("rosa", jim.trainUid).Ok, "not recruited");
        }

        [Test]
        public void Crew_PerksChangeTrips()
        {
            SetLevel(Frontier.CrewLevel);
            _s.Tick();
            var t = _s.State.trains[0];
            var mine = GameDatabase.Destination("blackhollow");
            var jim = _s.Crew("bigjim");
            _s.AssignEngineer("bigjim", 0);
            int plain = _s.PreviewDispatch(t, mine).Amount;
            _s.AssignEngineer("bigjim", t.uid);
            Assert.Greater(_s.PreviewDispatch(t, mine).Amount, plain, "Big Jim hauls more coal");

            _s.State.engineers.Add(new EngineerState { id = "rosa", level = 3 });
            double slow = _s.TripSeconds(t, mine);
            Assert.IsTrue(_s.AssignEngineer("rosa", t.uid).Ok);
            Assert.AreEqual(0, jim.trainUid, "one engineer per locomotive");
            Assert.Less(_s.TripSeconds(t, mine), slow);

            _s.State.engineers.Add(new EngineerState { id = "smitty", level = 5 });
            int fuel = _s.FuelCost(t, mine);
            _s.AssignEngineer("smitty", t.uid);
            Assert.Less(_s.FuelCost(t, mine), fuel);
        }

        // ---------------------------------------------------------------- peddler

        [Test]
        public void Peddler_VisitsWithThreeDeals_AndLeaves()
        {
            SetLevel(Frontier.PeddlerLevel);
            _s.Tick();
            Assert.IsFalse(_s.PeddlerHere);
            _clock.Advance(Frontier.PeddlerFirstDelay + 1);
            _s.Tick();
            Assert.IsTrue(_s.PeddlerHere);
            Assert.AreEqual(3, _s.State.deals.Count);
            var parts = _s.State.deals.FindIndex(d => d.kind == DealKind.Parts);
            var deal = _s.State.deals[parts];
            _s.State.coins = deal.coins;
            int have = _s.Amount(deal.id);
            Assert.IsTrue(_s.BuyDeal(parts).Ok);
            Assert.AreEqual(have + deal.amount, _s.Amount(deal.id));
            Assert.AreEqual(0, _s.Coins);
            Assert.IsFalse(_s.BuyDeal(parts).Ok, "sold out");
            _clock.Advance(Frontier.PeddlerStay + 1);
            _s.Tick();
            Assert.IsFalse(_s.PeddlerHere);
            Assert.IsFalse(_s.BuyDeal(0).Ok);
        }

        // ---------------------------------------------------------------- Full Steam

        [Test]
        public void Combo_TrainsSentInARowHaulMore()
        {
            SetLevel(Frontier.ComboLevel);
            var a = _s.State.trains[0];
            var b = AddTrain("dusty", "flatcar", "hopper");
            var d = GameDatabase.Destination("pinecrest");
            _s.State.fuel = 999;
            int plain = _s.PreviewDispatch(b, d).Amount;
            Assert.IsTrue(_s.Dispatch(a.uid, "pinecrest").Ok);
            Assert.AreEqual(0, a.combo);
            var p = _s.PreviewDispatch(b, d);
            Assert.AreEqual(1.1f, p.Combo, 1e-4);
            Assert.Greater(p.Amount, plain);
            Assert.IsTrue(_s.Dispatch(b.uid, "pinecrest").Ok);
            Assert.AreEqual(10, b.combo);
            _clock.Advance(Frontier.ComboWindow + 1);
            Assert.AreEqual(1f, _s.NextCombo, 1e-4);
        }

        [Test]
        public void Combo_NotBeforeLevel3()
        {
            var a = _s.State.trains[0];
            _s.Dispatch(a.uid, "pinecrest");
            Assert.AreEqual(1f, _s.NextCombo, 1e-4);
        }

        // ---------------------------------------------------------------- curios

        [Test]
        public void Curios_FoundOnTripsAndCollectedOnArrival()
        {
            SetLevel(2);
            var t = _s.State.trains[0];
            bool found = false;
            for (int i = 0; i < 120 && !found; i++)
            {
                _s.State.fuel = 999;
                Assert.IsTrue(_s.Dispatch(t.uid, "pinecrest").Ok);
                bool carrying = t.curio == "curio_eagle";
                _clock.Advance(3600);
                _s.Tick();
                if (carrying) { Assert.IsTrue(_s.HasCurio("curio_eagle")); found = true; }
                _s.State.warehouseLevel = 10;
                _s.Unload(t.uid);
                if (t.crate) _s.OpenCrate(t.uid);
            }
            Assert.IsTrue(found);
            _s.State.fuel = 999;
            _s.Dispatch(t.uid, "pinecrest");
            Assert.AreEqual("", t.curio, "each curio is found once");
        }

        [Test]
        public void Curios_FullShelfGivesAPerk()
        {
            var shelf = Frontier.Shelf("prairie");
            Assert.IsFalse(_s.ClaimShelf("prairie").Ok);
            foreach (var c in shelf.Curios) _s.State.curios.Add(c.Id);
            Assert.AreEqual(1, _s.ShelvesClaimable);
            float income = _s.CivicBonus(CivicEffect.Income);
            Assert.IsTrue(_s.ClaimShelf("prairie").Ok);
            Assert.AreEqual(income + shelf.PerkValue, _s.CivicBonus(CivicEffect.Income), 1e-5);
        }

        [Test]
        public void Curios_FoundWhileAwayShowInTheSummary()
        {
            var t = _s.State.trains[0];
            _s.State.fuel = 999;
            Assert.IsTrue(_s.Dispatch(t.uid, "pinecrest").Ok);
            t.curio = "curio_eagle";
            _s.State.lastSavedAt = _clock.Now;
            _clock.Advance(3 * 3600);
            var loaded = new GameSession(_s.State, _clock);
            Assert.IsTrue(loaded.HasCurio("curio_eagle"));
            Assert.AreEqual("curio_eagle", loaded.Away.Curios.Single().Id);
        }

        [Test]
        public void Curios_EveryDestinationIdIsReal()
        {
            foreach (var c in Frontier.Curios)
            {
                Assert.IsNotNull(GameDatabase.Destination(c.DestinationId), c.Id);
                Assert.IsNotNull(Frontier.Shelf(c.ShelfId), c.Id);
            }
            Assert.AreEqual(Frontier.Curios.Count, Frontier.Curios.Select(c => c.DestinationId).Distinct().Count());
        }

        // ---------------------------------------------------------------- new territory

        [Test]
        public void Charter_NeedsTheGoldenSpike()
        {
            Assert.IsFalse(_s.CanCharter);
            Assert.IsFalse(_s.CharterNewTerritory().Ok);
        }

        [Test]
        public void Charter_ResetsTheTownKeepsTheCollection()
        {
            SetLevel(18);
            _s.State.coins = 99999;
            _s.State.nuggets = 50;
            _s.State.warehouseLevel = 9;
            _s.State.inventory.Change("steel", 200);
            var t = _s.State.trains[0];
            t.level = 6;
            _s.State.blueprints.Change("sundown", 5);
            _s.State.curios.Add("curio_eagle");
            _s.State.engineers.Add(new EngineerState { id = "rosa", level = 2, trainUid = t.uid });
            _s.State.ownedProducts.Add(Catalog.FuelTank);
            foreach (var p in _s.State.projects) { p.done = true; p.stage = 99; }
            _s.State.chaptersClaimed.Add("railhead");
            float income = _s.CivicBonus(CivicEffect.Income);
            string next = _s.NextTownName;

            var r = _s.CharterNewTerritory();
            Assert.IsTrue(r.Ok, r.Message);
            var st = _s.State;
            Assert.AreEqual(2, st.territory);
            Assert.AreEqual(next, st.townName);
            Assert.AreEqual(1, st.level);
            Assert.AreEqual(Balance.StartCoins, st.coins);
            Assert.AreEqual(50 + Frontier.CharterNuggets, st.nuggets);
            Assert.AreEqual(1, st.warehouseLevel);
            Assert.AreEqual(0, _s.Amount("steel"));
            Assert.AreEqual(2, st.buildings.Count);
            Assert.IsTrue(st.projects.All(p => !p.done && p.stage == 0));
            Assert.AreEqual(0, st.chaptersClaimed.Count);
            Assert.Greater(st.contracts.Count, 0);
            // Kept
            Assert.AreEqual(6, t.level);
            Assert.AreEqual(5, _s.BlueprintsOf("sundown"));
            Assert.IsTrue(_s.HasCurio("curio_eagle"));
            Assert.AreEqual(t.uid, _s.Crew("rosa").trainUid);
            Assert.IsTrue(_s.HasFuelTank);
            Assert.IsTrue(_s.InAlbum("dusty"));
            // The charter bonus replaces the projects' income perks.
            Assert.AreEqual(Frontier.CharterBonus, _s.CivicBonus(CivicEffect.Income) - BuildingsIncome(), 1e-5);
            Assert.Less(_s.CivicBonus(CivicEffect.Income), income + Frontier.CharterBonus);
            Assert.IsFalse(_s.CanCharter);
        }

        float BuildingsIncome()
        {
            float total = 0;
            foreach (var b in _s.State.buildings)
            {
                var d = GameDatabase.Building(b.defId);
                if (d.Effect == CivicEffect.Income) total += d.EffectValue * Balance.BuildingLevelMultiplier(b.level);
            }
            return total;
        }
    }
}
