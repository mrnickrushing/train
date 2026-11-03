using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironhorse.Core
{
    // Daily hooks: payday streak, the away summary, mystery crates, express telegrams, map events
    // and the Roundhouse album. Tuning and content live in Hooks.cs.
    public sealed partial class GameSession
    {
        public event Action<ContractState> TelegramArrived;
        public event Action<MapEventState> MapEventStarted;
        public event Action<AlbumSetDef> AlbumSetCompleted;

        /// <summary>The player's time zone offset, so the payday day turns over at local midnight.</summary>
        public double UtcOffsetSeconds { get; set; }

        void RepairHooks()
        {
            if (State.events == null) State.events = new List<MapEventState>();
            if (State.album == null) State.album = new List<string>();
            if (State.albumClaimed == null) State.albumClaimed = new List<string>();
            foreach (var e in State.events) if (e.destinationId == null) e.destinationId = "";
            State.events.RemoveAll(e => (e.kind == MapEventKind.GoldStrike || e.kind == MapEventKind.Bandits) && GameDatabase.Destination(e.destinationId) == null);
            RecordAlbum(false);
        }

        bool TickHooks()
        {
            bool changed = TickExpress();
            if (TickEvents()) changed = true;
            if (RecordAlbum(true)) changed = true;
            return changed;
        }

        // ================================================================ Payday

        int LocalDay { get { return (int)Math.Floor((Now + UtcOffsetSeconds) / 86400.0); } }

        /// <summary>Seconds until the next payday (local midnight).</summary>
        public double NextPaydayIn { get { return (LocalDay + 1) * 86400.0 - (Now + UtcOffsetSeconds); } }

        public bool CanClaimPayday { get { return State.paydayLastDay != LocalDay; } }

        bool StreakAlive { get { return State.paydayLastDay >= LocalDay - 1 && State.paydayLastDay >= 0; } }

        /// <summary>Consecutive days claimed, counting today once claimed (0 when the streak was broken).</summary>
        public int PaydayStreak { get { return StreakAlive ? State.paydayStreak : 0; } }

        /// <summary>True when the player had a streak going and missed a day.</summary>
        public bool PaydayStreakLost { get { return State.paydayStreak > 1 && !StreakAlive; } }

        /// <summary>The calendar day (1..7) today's payday gives, or gave if already claimed.</summary>
        public int PaydayDay
        {
            get
            {
                if (State.paydayLastDay == LocalDay) return (State.paydayStreak - 1) % Hooks.PaydayDays + 1;
                return PaydayStreak % Hooks.PaydayDays + 1;
            }
        }

        /// <summary>The reward shown on calendar day 1..7 of the current week.</summary>
        public Grant PaydayGrant(int day)
        {
            int weeks = State.paydayWeeks;
            if (!CanClaimPayday && PaydayDay == Hooks.PaydayDays) weeks--; // today's day-7 was just counted
            return Hooks.PaydayReward(day, State.level, weeks);
        }

        public ActionResult ClaimPayday()
        {
            if (!CanClaimPayday) return ActionResult.Fail("Payday comes again tomorrow");
            int day = PaydayDay;
            var grant = Hooks.PaydayReward(day, State.level, State.paydayWeeks);
            State.paydayStreak = PaydayStreak + 1;
            State.paydayLastDay = LocalDay;
            if (day == Hooks.PaydayDays) State.paydayWeeks++;
            string text = Apply(grant);
            RaiseChanged();
            return ActionResult.Success("Payday, day " + day + ": " + text);
        }

        // ================================================================ While you were away

        /// <summary>What piled up while the app was closed; null when there is nothing worth a summary.</summary>
        public AwayReport Away { get; private set; }

        void BuildAwayReport()
        {
            double away = Now - State.lastSavedAt;
            if (State.lastSavedAt <= 0 || away < Hooks.AwayMinSeconds) return;
            var r = new AwayReport { Seconds = away };
            foreach (var t in State.trains)
            {
                if (!HasArrived(t)) continue;
                r.Trains++;
                if (t.crate) r.Crates++;
                r.Robbed += t.robbed;
                var res = GameDatabase.Resource(t.cargoId);
                if (res == null || t.cargoAmount <= 0) continue;
                if (res.IsPassenger) r.Fares += FareFor(t);
                else r.Cargo.Change(t.cargoId, t.cargoAmount);
            }
            foreach (var b in State.buildings)
            {
                if (!IsReady(b)) continue;
                var d = GameDatabase.Building(b.defId);
                if (d.Category == BuildingCategory.Residential) r.Rent += PendingRent(b);
                int jobs = FinishedJobs(b);
                if (jobs > 0 && d.Recipe != null) r.Goods.Change(d.Recipe.Output.id, d.Recipe.Output.amount * jobs);
            }
            if (r.HasAnything) Away = r;
        }

        public void DismissAway() { Away = null; }

        /// <summary>Unloads every arrived train, collects all rent and all finished goods.</summary>
        public ActionResult CollectAll()
        {
            Away = null;
            int coins = State.coins, trains = 0, goods = 0, left = 0;
            foreach (var t in State.trains.ToList())
            {
                if (!HasArrived(t)) continue;
                var r = Unload(t.uid);
                if (r) trains++;
                if (!t.IsIdle) left++;
            }
            CollectAllRent();
            foreach (var b in State.buildings.ToList())
            {
                if (!IsProductionDone(b)) continue;
                int before = State.stats.produced.Total();
                if (CollectProduction(b.uid)) goods += State.stats.produced.Total() - before;
                else left++;
            }
            var parts = new List<string>();
            if (trains > 0) parts.Add(trains + " train" + (trains > 1 ? "s" : "") + " unloaded");
            if (State.coins > coins) parts.Add((State.coins - coins).ToString("#,0") + " coins");
            if (goods > 0) parts.Add(goods + " goods");
            RaiseChanged();
            if (parts.Count == 0) return left > 0 ? ActionResult.Fail("Warehouse is full!") : ActionResult.Fail("Nothing to collect");
            return ActionResult.Success("Collected: " + string.Join(", ", parts) + (left > 0 ? " (warehouse full, some left waiting)" : ""));
        }

        // ================================================================ Mystery crates

        /// <summary>What the last opened crate held, for the reveal.</summary>
        public Grant LastCrate { get; private set; }

        /// <summary>Called as a train departs: bandits may rob it and a crate may ride home with it.</summary>
        void RollTrip(TrainState t, DispatchPreview p)
        {
            t.robbed = 0;
            t.boom = p.Boom;
            var rng = NextRng();
            if (p.Bandits && rng.NextDouble() < Hooks.BanditChance)
            {
                int lost = Math.Max(1, (int)Math.Round(t.cargoAmount * Hooks.BanditLoss));
                t.cargoAmount -= lost;
                t.robbed = lost;
            }
            if (State.stats.dispatches <= Hooks.CrateMinDispatches) return;
            State.crateDry++;
            if (State.crateDry >= Hooks.CrateDrySpell || rng.NextDouble() < Hooks.CrateChance)
            {
                t.crate = true;
                State.crateDry = 0;
            }
        }

        public ActionResult OpenCrate(int trainUid)
        {
            var t = Train(trainUid);
            if (t == null || !t.crate) return ActionResult.Fail("No crate on that train");
            if (IsEnRoute(t)) return ActionResult.Fail("Still on the line");
            string msg = OpenCrateOn(t);
            RaiseChanged();
            return ActionResult.Success(msg);
        }

        string OpenCrateOn(TrainState t)
        {
            t.crate = false;
            State.cratesOpened++;
            LastCrate = RollCrate();
            return "Mystery crate: " + Apply(LastCrate);
        }

        Grant RollCrate()
        {
            var rng = NextRng();
            double r = rng.NextDouble();
            int lvl = State.level;
            if (r < 0.45)
            {
                string[] parts = lvl < 4 ? new[] { "rivets" } : lvl < 8 ? new[] { "rivets", "bearings" } : new[] { "bearings", "rods", "gauges" };
                return new Grant { Resources = new[] { new ResourceAmount(parts[rng.Next(parts.Length)], 3 + rng.Next(3) + lvl / 3) } };
            }
            if (r < 0.70) return new Grant { Nuggets = 2 + rng.Next(5) };
            if (r < 0.88) return new Grant { BlueprintLoco = CrateBlueprintLoco(), Blueprints = 1 };
            return new Grant { Coins = (500 + 200 * lvl) * (2 + rng.Next(2)) }; // the rare jackpot
        }

        /// <summary>The first assemblable locomotive the player has never owned (Sundown once they own them all).</summary>
        string CrateBlueprintLoco()
        {
            foreach (var d in GameDatabase.Locomotives)
                if (d.Blueprints > 0 && !State.album.Contains(d.Id)) return d.Id;
            return "sundown";
        }

        // ================================================================ Express telegrams

        /// <summary>The active express telegram, if any (it also sits in the contracts list).</summary>
        public ContractState Telegram { get { return State.contracts.FirstOrDefault(c => c.express); } }

        public double TelegramLeft(ContractState c) { return Math.Max(0, c.expiresAt - Now); }

        bool TickExpress()
        {
            bool changed = false;
            var tg = Telegram;
            if (tg != null && Now >= tg.expiresAt)
            {
                State.contracts.Remove(tg);
                State.nextExpressAt = Now + Hooks.ExpressGap;
                Notify?.Invoke("The telegram from " + tg.client + " expired.");
                tg = null;
                changed = true;
            }
            if (tg != null || State.level < Hooks.ExpressLevel) return changed;
            if (State.nextExpressAt <= 0) { State.nextExpressAt = Now + Hooks.ExpressFirstDelay; return changed; }
            if (Now < State.nextExpressAt) return changed;
            var c = GenerateTelegram();
            if (c == null) { State.nextExpressAt = Now + Hooks.ExpressGap; return changed; }
            State.contracts.Add(c);
            State.nextExpressAt = c.expiresAt + Hooks.ExpressGap;
            TelegramArrived?.Invoke(c);
            return true;
        }

        public static string TelegramText(ContractState c)
        {
            var r = c.requirements[0];
            return c.client + " needs " + r.amount + " " + GameDatabase.Resource(r.id).Name + " in " + (int)Math.Round(Hooks.ExpressSeconds / 3600) + " hours: triple pay!";
        }

        ContractState GenerateTelegram()
        {
            var rng = NextRng();
            var pool = ObtainableResources().Where(id => !GameDatabase.Resource(id).IsPassenger).ToList();
            var towns = GameDatabase.Destinations.Where(IsDestinationUnlocked).Select(d => d.Name).ToList();
            if (pool.Count == 0 || towns.Count == 0) return null;
            string id = pool[rng.Next(pool.Count)];
            var res = GameDatabase.Resource(id);
            double scale = res.Manufactured ? 3 + State.level * 1.2 : 12 + State.level * 4;
            int amt = Math.Max(3, (int)Math.Round(scale * (0.9 + rng.NextDouble() * 0.3)));
            if (amt >= 10) amt = (amt + 2) / 5 * 5; // round telegram numbers
            amt = Math.Max(3, Math.Min(amt, WarehouseCapacity / 2));
            int value = res.Value * amt;
            var c = new ContractState
            {
                uid = NewUid(),
                client = towns[rng.Next(towns.Count)],
                express = true,
                expiresAt = Now + Hooks.ExpressSeconds,
                rewardCoins = (int)Math.Round((value * 1.8 + 40) * Hooks.ExpressPayMultiplier),
                rewardXp = Math.Max(10, (int)Math.Round(value * 0.9)),
                rewardNuggets = 3 + rng.Next(4),
            };
            c.requirements.Add(new ResourceAmount(id, amt));
            return c;
        }

        // ================================================================ Map events

        public MapEventState MapEvent(int uid) { return State.events.FirstOrDefault(e => e.uid == uid); }

        public double EventLeft(MapEventState e) { return Math.Max(0, e.expiresAt - Now); }

        bool ActiveAt(MapEventKind k, string destId)
        {
            foreach (var e in State.events) if (e.kind == k && e.destinationId == destId && Now < e.expiresAt) return true;
            return false;
        }

        /// <summary>A gold strike at this outpost doubles every load hauled from it.</summary>
        public bool BoomAt(string destId) { return ActiveAt(MapEventKind.GoldStrike, destId); }

        /// <summary>Bandits on this line may rob trains that run it.</summary>
        public bool BanditsOn(string destId) { return ActiveAt(MapEventKind.Bandits, destId); }

        bool TickEvents()
        {
            bool changed = State.events.RemoveAll(e => Now >= e.expiresAt) > 0;
            // Nothing pops up during Hank's first-launch tutorial.
            if (State.level < Hooks.EventLevel || (!State.settings.tutorialDone && State.level < 3)) return changed;
            if (State.nextEventAt <= 0) { State.nextEventAt = Now + Hooks.EventFirstDelay; return changed; }
            if (Now < State.nextEventAt) return changed;
            var rng = NextRng();
            State.nextEventAt = Now + Hooks.EventGapMin + rng.NextDouble() * (Hooks.EventGapMax - Hooks.EventGapMin);
            if (State.events.Count >= Hooks.MaxEvents) return changed;
            var ev = RollEvent(rng);
            if (ev == null) return changed;
            State.events.Add(ev);
            MapEventStarted?.Invoke(ev);
            return true;
        }

        /// <summary>Unlocked outposts no other event is using.</summary>
        List<DestinationDef> FreeOutposts()
        {
            return GameDatabase.Destinations.Where(d => !d.IsTown && IsDestinationUnlocked(d) && State.events.All(e => e.destinationId != d.Id)).ToList();
        }

        MapEventState RollEvent(Random rng)
        {
            var weights = new[] { (MapEventKind.Tumbleweed, 35), (MapEventKind.Traveler, 25), (MapEventKind.GoldStrike, 20), (MapEventKind.Bandits, 20) };
            var free = FreeOutposts();
            var options = weights.Where(w => State.level >= Hooks.EventLevelFor(w.Item1) && State.events.All(e => e.kind != w.Item1)
                && (free.Count > 0 || (w.Item1 != MapEventKind.GoldStrike && w.Item1 != MapEventKind.Bandits))).ToList();
            if (options.Count == 0) return null;
            int roll = rng.Next(options.Sum(o => o.Item2));
            var kind = options[0].Item1;
            foreach (var o in options) { if (roll < o.Item2) { kind = o.Item1; break; } roll -= o.Item2; }
            var ev = new MapEventState { uid = NewUid(), kind = kind, spot = rng.Next(1000), startsAt = Now, expiresAt = Now + Hooks.EventSeconds(kind) };
            if (kind == MapEventKind.GoldStrike || kind == MapEventKind.Bandits) ev.destinationId = free[rng.Next(free.Count)].Id;
            return ev;
        }

        public static string EventText(MapEventState e)
        {
            var d = GameDatabase.Destination(e.destinationId);
            switch (e.kind)
            {
                case MapEventKind.Traveler: return "A traveler missed the stage and needs a ride into town.";
                case MapEventKind.Tumbleweed: return "Tumbleweeds are blowing through the yard. Some have coins caught in them!";
                case MapEventKind.GoldStrike: return "Rumor of a strike at " + d.Name + ": every load hauled from there is doubled.";
                default: return "Bandits are watching the " + d.Name + " line. Trains that run it may lose cargo.";
            }
        }

        public int EscortCost { get { return Hooks.EscortCoins(State.level); } }

        /// <summary>Picks up a traveler or a tumbleweed.</summary>
        public ActionResult ClaimEvent(int uid)
        {
            var e = MapEvent(uid);
            if (e == null || Now >= e.expiresAt) return ActionResult.Fail("It's gone");
            string msg;
            switch (e.kind)
            {
                case MapEventKind.Traveler:
                    int fare = Hooks.TravelerCoins(State.level);
                    EarnCoins(fare);
                    AddXp(Hooks.TravelerXp(State.level));
                    State.stats.passengers++;
                    msg = "The traveler pays " + fare + " coins for the ride. Much obliged!";
                    break;
                case MapEventKind.Tumbleweed:
                    var rng = NextRng();
                    int coins = Hooks.TumbleweedCoins(State.level);
                    EarnCoins(coins);
                    int nuggets = rng.NextDouble() < 0.15 ? 1 + rng.Next(2) : 0;
                    State.nuggets += nuggets;
                    msg = "+" + coins + " coins" + (nuggets > 0 ? " and " + nuggets + " nugget" + (nuggets > 1 ? "s" : "") : "") + " shaken out of the tumbleweed";
                    break;
                case MapEventKind.GoldStrike:
                    return ActionResult.Fail("Send a train to " + GameDatabase.Destination(e.destinationId).Name + " for double cargo");
                default:
                    return ActionResult.Fail("Pay for an escort to run them off");
            }
            State.events.Remove(e);
            RaiseChanged();
            return ActionResult.Success(msg);
        }

        /// <summary>Hires an escort to clear the bandits off a line.</summary>
        public ActionResult PayEscort(int uid)
        {
            var e = MapEvent(uid);
            if (e == null || e.kind != MapEventKind.Bandits || Now >= e.expiresAt) return ActionResult.Fail("The bandits are gone");
            int cost = EscortCost;
            if (!CanAfford(cost, null)) return ActionResult.Fail(MissingText(cost, null));
            Pay(cost, null);
            AddXp(Hooks.EscortXp(State.level));
            State.events.Remove(e);
            RaiseChanged();
            return ActionResult.Success("The marshal's posse ran the bandits off the " + GameDatabase.Destination(e.destinationId).Name + " line");
        }

        // ================================================================ Roundhouse album

        public bool InAlbum(string defId) { return State.album.Contains(defId); }
        public int AlbumOwned(AlbumSetDef s) { return s.Members.Count(InAlbum); }
        public bool IsAlbumComplete(AlbumSetDef s) { return AlbumOwned(s) >= s.Count; }
        public bool IsAlbumClaimed(AlbumSetDef s) { return State.albumClaimed.Contains(s.Id); }
        public int AlbumClaimable { get { return Hooks.AlbumSets.Count(s => IsAlbumComplete(s) && !IsAlbumClaimed(s)); } }

        /// <summary>Adds any engine or car the player owns to the album; returns true when something new was added.</summary>
        bool RecordAlbum(bool announce)
        {
            bool added = false;
            foreach (var t in State.trains) if (!State.album.Contains(t.defId)) { Collect(t.defId, announce); added = true; }
            foreach (var w in State.wagons) if (!State.album.Contains(w.defId)) { Collect(w.defId, announce); added = true; }
            return added;
        }

        void Collect(string defId, bool announce)
        {
            State.album.Add(defId);
            if (!announce) return;
            foreach (var s in Hooks.AlbumSets)
                if (s.Members.Contains(defId) && IsAlbumComplete(s) && !IsAlbumClaimed(s))
                {
                    Notify?.Invoke("Album set complete: " + s.Name + "! Claim it in the Roundhouse.");
                    AlbumSetCompleted?.Invoke(s);
                }
        }

        public ActionResult ClaimAlbumSet(string id)
        {
            var s = Hooks.AlbumSet(id);
            if (s == null) return ActionResult.Fail("Unknown set");
            if (IsAlbumClaimed(s)) return ActionResult.Fail("Already claimed");
            if (!IsAlbumComplete(s)) return ActionResult.Fail((s.Count - AlbumOwned(s)) + " more to collect");
            State.albumClaimed.Add(s.Id);
            State.nuggets += s.Nuggets;
            AddXp(100 + 20 * s.Count);
            RaiseChanged();
            return ActionResult.Success(s.Name + " complete: " + s.PerkText + " forever, +" + s.Nuggets + " nuggets");
        }

        float AlbumPerk(CivicEffect fx)
        {
            if (State.albumClaimed == null) return 0;
            float total = 0;
            foreach (var id in State.albumClaimed)
            {
                var s = Hooks.AlbumSet(id);
                if (s != null && s.Perk == fx) total += s.PerkValue;
            }
            return total;
        }
    }
}
