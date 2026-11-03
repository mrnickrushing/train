using System.Linq;
using Ironhorse.Core;
using NUnit.Framework;

namespace Ironhorse.Tests
{
    public class HooksTests
    {
        ManualClock _clock;
        GameSession _s;

        [SetUp]
        public void SetUp()
        {
            // Start at a local midnight so day arithmetic is easy to read.
            _clock = new ManualClock(System.Math.Ceiling((Balance.SeasonEpoch + 86400) / 86400.0) * 86400);
            _s = new GameSession(GameSession.NewGame(_clock.Now, 3), _clock);
        }

        void Day(int days = 1) { _clock.Advance(86400 * days); _s.Tick(); }

        void SetLevel(int level)
        {
            _s.State.level = level;
            _s.State.xp = 0;
        }

        // ---------------------------------------------------------------- payday

        [Test]
        public void Payday_OncePerDay_AndStreakRunsThroughTheWeek()
        {
            Assert.IsTrue(_s.CanClaimPayday);
            Assert.AreEqual(1, _s.PaydayDay);
            int coins = _s.Coins;
            Assert.IsTrue(_s.ClaimPayday().Ok);
            Assert.Greater(_s.Coins, coins);
            Assert.IsFalse(_s.CanClaimPayday);
            Assert.IsFalse(_s.ClaimPayday().Ok);
            Assert.AreEqual(1, _s.PaydayStreak);
            for (int day = 2; day <= 6; day++)
            {
                Day();
                Assert.AreEqual(day, _s.PaydayDay);
                Assert.IsTrue(_s.ClaimPayday().Ok);
            }
            Day();
            Assert.AreEqual(7, _s.PaydayDay);
            string loco = _s.PaydayGrant(7).BlueprintLoco;
            Assert.IsNotNull(loco);
            int before = _s.BlueprintsOf(loco);
            Assert.IsTrue(_s.ClaimPayday().Ok);
            Assert.AreEqual(before + Hooks.PaydayBlueprints, _s.BlueprintsOf(loco));
            Assert.AreEqual(loco, _s.PaydayGrant(7).BlueprintLoco, "today's claimed day 7 still shows what it gave");
            Day();
            Assert.AreEqual(1, _s.PaydayDay, "a new week starts after day 7");
            Assert.AreEqual(7, _s.PaydayStreak);
            Assert.AreNotEqual(loco, _s.PaydayGrant(7).BlueprintLoco, "next week's blueprint is a different engine");
        }

        [Test]
        public void Payday_MissingADayResetsTheStreak()
        {
            Assert.IsTrue(_s.ClaimPayday().Ok);
            Day();
            Assert.IsTrue(_s.ClaimPayday().Ok);
            Assert.AreEqual(2, _s.PaydayDay);
            Day(2);
            Assert.IsTrue(_s.PaydayStreakLost);
            Assert.AreEqual(0, _s.PaydayStreak);
            Assert.AreEqual(1, _s.PaydayDay);
            Assert.IsTrue(_s.ClaimPayday().Ok);
            Assert.AreEqual(1, _s.PaydayStreak);
        }

        [Test]
        public void Payday_TurnsOverAtLocalMidnight()
        {
            _s.UtcOffsetSeconds = -5 * 3600; // 7pm local the day before
            Assert.IsTrue(_s.ClaimPayday().Ok);
            _clock.Advance(4 * 3600); // 11pm local: same day
            Assert.IsFalse(_s.CanClaimPayday);
            _clock.Advance(2 * 3600); // 1am local
            Assert.IsTrue(_s.CanClaimPayday);
            Assert.AreEqual(2, _s.PaydayDay);
        }

        // ---------------------------------------------------------------- away summary

        [Test]
        public void Away_SummarisesAndCollectsEverything()
        {
            var t = _s.State.trains[0];
            Assert.IsTrue(_s.Dispatch(t.uid, "pinecrest").Ok);
            _s.State.lastSavedAt = _clock.Now;
            _clock.Advance(3 * 3600);
            var loaded = new GameSession(_s.State, _clock);
            var away = loaded.Away;
            Assert.IsNotNull(away);
            Assert.AreEqual(1, away.Trains);
            Assert.Greater(away.Cargo.Get("timber"), 0);
            Assert.Greater(away.Rent, 0);
            int timber = loaded.Amount("timber"), coins = loaded.Coins;
            Assert.IsTrue(loaded.CollectAll().Ok);
            Assert.IsNull(loaded.Away);
            Assert.AreEqual(timber + away.Cargo.Get("timber"), loaded.Amount("timber"));
            Assert.GreaterOrEqual(loaded.Coins, coins + away.Rent);
            Assert.IsTrue(t.IsIdle);
        }

        [Test]
        public void Away_NothingForAShortBreak()
        {
            _s.State.lastSavedAt = _clock.Now;
            _clock.Advance(60);
            Assert.IsNull(new GameSession(_s.State, _clock).Away);
        }

        // ---------------------------------------------------------------- crates

        [Test]
        public void Crates_ArriveWithinADrySpell_AndOpenForAReward()
        {
            var t = _s.State.trains[0];
            int crates = 0;
            for (int i = 0; i < Hooks.CrateMinDispatches + Hooks.CrateDrySpell; i++)
            {
                _s.State.fuel = 999;
                Assert.IsTrue(_s.Dispatch(t.uid, "pinecrest").Ok);
                if (i < Hooks.CrateMinDispatches) Assert.IsFalse(t.crate, "no crates during the tutorial trips");
                bool crate = t.crate;
                if (crate) { crates++; Assert.IsFalse(_s.OpenCrate(t.uid).Ok, "can't open it mid-trip"); }
                _clock.Advance(3600);
                _s.State.warehouseLevel = 10;
                Assert.IsTrue(_s.Unload(t.uid).Ok);
                if (crate)
                {
                    Assert.IsTrue(t.crate, "the crate waits on the platform after unloading");
                    var r = _s.OpenCrate(t.uid);
                    Assert.IsTrue(r.Ok, r.Message);
                    Assert.IsNotNull(_s.LastCrate);
                    Assert.IsFalse(t.crate);
                }
            }
            Assert.GreaterOrEqual(crates, 1);
            Assert.AreEqual(crates, _s.State.cratesOpened);
        }

        [Test]
        public void Crates_UnopenedCrateIsOpenedOnTheNextDispatch()
        {
            var t = _s.State.trains[0];
            t.crate = true;
            int opened = _s.State.cratesOpened;
            Assert.IsTrue(_s.Dispatch(t.uid, "pinecrest").Ok);
            Assert.AreEqual(opened + 1, _s.State.cratesOpened);
        }

        // ---------------------------------------------------------------- express telegrams

        ContractState WaitForTelegram()
        {
            SetLevel(Hooks.ExpressLevel);
            _s.Tick();
            _clock.Advance(Hooks.ExpressFirstDelay + 1);
            _s.Tick();
            return _s.Telegram;
        }

        [Test]
        public void Telegram_ArrivesPaysTripleAndDoesNotTakeASlot()
        {
            int regular = _s.RegularContracts;
            ContractState arrived = null;
            _s.TelegramArrived += c => arrived = c;
            var tg = WaitForTelegram();
            Assert.IsNotNull(tg);
            Assert.AreSame(tg, arrived);
            Assert.IsTrue(tg.express);
            Assert.AreEqual(regular, _s.RegularContracts);
            Assert.AreEqual(Hooks.ExpressSeconds, _s.TelegramLeft(tg), 1);
            StringAssert.Contains("triple pay", GameSession.TelegramText(tg));
            var req = tg.requirements[0];
            _s.State.warehouseLevel = 10;
            _s.State.inventory.Change(req.id, req.amount);
            int coins = _s.Coins;
            Assert.IsTrue(_s.DeliverContract(tg.uid).Ok);
            Assert.AreEqual(coins + tg.rewardCoins, _s.Coins);
            Assert.Greater(tg.rewardCoins, GameDatabase.Resource(req.id).Value * req.amount * 4);
            Assert.IsNull(_s.Telegram);
            _clock.Advance(Hooks.ExpressGap - 60);
            _s.Tick();
            Assert.IsNull(_s.Telegram, "the next one waits for the gap");
            _clock.Advance(120);
            _s.Tick();
            Assert.IsNotNull(_s.Telegram);
        }

        [Test]
        public void Telegram_ExpiresAfterTwoHours()
        {
            var tg = WaitForTelegram();
            _clock.Advance(Hooks.ExpressSeconds + 1);
            _s.Tick();
            Assert.IsFalse(_s.State.contracts.Contains(tg));
        }

        [Test]
        public void Telegram_NotBeforeLevel3()
        {
            _s.Tick();
            _clock.Advance(Hooks.ExpressFirstDelay * 10);
            _s.Tick();
            Assert.IsNull(_s.Telegram);
        }

        // ---------------------------------------------------------------- map events

        MapEventState Spawn(MapEventKind kind, string dest = "")
        {
            var e = new MapEventState { uid = _s.State.nextUid++, kind = kind, destinationId = dest, startsAt = _clock.Now, expiresAt = _clock.Now + Hooks.EventSeconds(kind) };
            _s.State.events.Add(e);
            return e;
        }

        [Test]
        public void Events_SpawnOverTimeAndExpire()
        {
            SetLevel(8);
            int seen = 0;
            _s.MapEventStarted += e => seen++;
            for (int i = 0; i < 40; i++) { _clock.Advance(Hooks.EventGapMax); _s.Tick(); }
            Assert.Greater(seen, 5);
            Assert.LessOrEqual(_s.State.events.Count, Hooks.MaxEvents);
            Assert.IsTrue(_s.State.events.All(e => _clock.Now < e.expiresAt));
            foreach (var e in _s.State.events.Where(e => e.kind == MapEventKind.GoldStrike || e.kind == MapEventKind.Bandits))
                Assert.IsNotNull(GameDatabase.Destination(e.destinationId));
        }

        [Test]
        public void Events_NoneBeforeLevel2()
        {
            for (int i = 0; i < 10; i++) { _clock.Advance(Hooks.EventGapMax); _s.Tick(); }
            Assert.AreEqual(0, _s.State.events.Count);
        }

        [Test]
        public void Events_TravelerAndTumbleweedPayOut()
        {
            var a = Spawn(MapEventKind.Traveler);
            var b = Spawn(MapEventKind.Tumbleweed);
            int coins = _s.Coins;
            Assert.IsTrue(_s.ClaimEvent(a.uid).Ok);
            Assert.AreEqual(coins + Hooks.TravelerCoins(_s.Level), _s.Coins);
            Assert.IsTrue(_s.ClaimEvent(b.uid).Ok);
            Assert.AreEqual(0, _s.State.events.Count);
            Assert.IsFalse(_s.ClaimEvent(a.uid).Ok);
        }

        [Test]
        public void Events_GoldStrikeDoublesTheLoad()
        {
            var t = _s.State.trains[0];
            var d = GameDatabase.Destination("pinecrest");
            int normal = _s.PreviewDispatch(t, d).Amount;
            Spawn(MapEventKind.GoldStrike, "pinecrest");
            var p = _s.PreviewDispatch(t, d);
            Assert.IsTrue(p.Boom);
            Assert.AreEqual(normal * 2, p.Amount);
            Assert.IsTrue(_s.Dispatch(t.uid, "pinecrest").Ok);
            Assert.AreEqual(normal * 2, t.cargoAmount);
            Assert.IsTrue(t.boom);
        }

        [Test]
        public void Events_BanditsRobSomeTrains_UnlessEscorted()
        {
            SetLevel(10); // no level-up coins muddying the sums
            var t = _s.State.trains[0];
            var e = Spawn(MapEventKind.Bandits, "pinecrest");
            int robbed = 0;
            for (int i = 0; i < 12; i++)
            {
                _s.State.fuel = 999;
                Assert.IsTrue(_s.PreviewDispatch(t, GameDatabase.Destination("pinecrest")).Bandits);
                Assert.IsTrue(_s.Dispatch(t.uid, "pinecrest").Ok);
                if (t.robbed > 0) robbed++;
                _clock.Advance(1000);
                _s.State.warehouseLevel = 10;
                Assert.IsTrue(_s.Unload(t.uid).Ok);
                e.expiresAt = _clock.Now + 3600;
            }
            Assert.That(robbed, Is.InRange(1, 11));
            _s.State.coins = 0;
            Assert.IsFalse(_s.PayEscort(e.uid).Ok);
            _s.State.coins = _s.EscortCost;
            Assert.IsTrue(_s.PayEscort(e.uid).Ok);
            Assert.AreEqual(0, _s.Coins);
            Assert.IsFalse(_s.BanditsOn("pinecrest"));
        }

        // ---------------------------------------------------------------- album

        [Test]
        public void Album_RecordsOwnedStock_AndSetsGivePerks()
        {
            SetLevel(10);
            Assert.IsTrue(_s.InAlbum("dusty"));
            Assert.IsTrue(_s.InAlbum("flatcar"));
            var set = Hooks.AlbumSet("frontier");
            Assert.IsFalse(_s.ClaimAlbumSet("frontier").Ok);
            foreach (var id in set.Locos) _s.State.trains.Add(new TrainState { uid = _s.State.nextUid++, defId = id });
            AlbumSetDef done = null;
            _s.AlbumSetCompleted += a => done = a;
            _s.Tick();
            Assert.AreSame(set, done);
            Assert.AreEqual(1, _s.AlbumClaimable);
            float speed = _s.CivicBonus(CivicEffect.TrainSpeed);
            int nuggets = _s.Nuggets;
            Assert.IsTrue(_s.ClaimAlbumSet("frontier").Ok);
            Assert.AreEqual(speed + set.PerkValue, _s.CivicBonus(CivicEffect.TrainSpeed), 1e-5);
            Assert.AreEqual(nuggets + set.Nuggets, _s.Nuggets);
            Assert.IsFalse(_s.ClaimAlbumSet("frontier").Ok);
            // Selling or scrapping later never removes an album entry.
            _s.State.trains.RemoveAll(t => t.defId == "ironbison");
            _s.Tick();
            Assert.IsTrue(_s.InAlbum("ironbison"));
        }

        [Test]
        public void Album_SetsOnlyNameRealStock()
        {
            foreach (var s in Hooks.AlbumSets)
            {
                foreach (var id in s.Locos) Assert.IsNotNull(GameDatabase.Locomotive(id), id);
                foreach (var id in s.Wagons) Assert.IsNotNull(GameDatabase.Wagon(id), id);
            }
        }
    }
}
