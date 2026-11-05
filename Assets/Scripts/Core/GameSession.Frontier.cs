using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironhorse.Core
{
    // The dispatcher's board, engineers, the traveling peddler, Full Steam combos, frontier curios and
    // new-territory charters. Tuning and content live in Frontier.cs.
    public sealed partial class GameSession
    {
        public event Action<EngineerState> EngineerJoined;
        public event Action PeddlerArrived;
        public event Action<CurioDef> CurioFound;

        void RepairFrontier()
        {
            if (State.dailyTasks == null) State.dailyTasks = new List<DailyTask>();
            if (State.dailyChests == null) State.dailyChests = new List<int>();
            if (State.weeklyChests == null) State.weeklyChests = new List<int>();
            if (State.engineers == null) State.engineers = new List<EngineerState>();
            if (State.deals == null) State.deals = new List<DealState>();
            if (State.curios == null) State.curios = new List<string>();
            if (State.shelvesClaimed == null) State.shelvesClaimed = new List<string>();
            if (State.territory < 1) State.territory = 1;
            State.engineers.RemoveAll(e => Frontier.Engineer(e.id) == null);
            foreach (var e in State.engineers) if (e.trainUid != 0 && Train(e.trainUid) == null) e.trainUid = 0;
            foreach (var t in State.trains) if (t.curio == null) t.curio = "";
            foreach (var d in State.deals) if (d.id == null) d.id = "";
        }

        bool TickFrontier()
        {
            bool changed = TickDaily();
            if (TickCrew()) changed = true;
            if (TickPeddler()) changed = true;
            return changed;
        }

        /// <summary>Daily features stay hidden until the tutorial is done (or the player is past it).</summary>
        bool PastTutorial { get { return State.settings.tutorialDone || State.level >= 3; } }

        // ================================================================ Dispatcher's board

        int LocalWeek { get { return (int)Math.Floor((LocalDay + 3) / 7.0); } } // weeks start on Monday

        /// <summary>Seconds until the weekly chests reset (Monday, local midnight).</summary>
        public double WeeklyResetIn { get { return ((Math.Max(LocalWeek, State.weeklyWeek) + 1) * 7 - 3) * 86400.0 - (Now + UtcOffsetSeconds); } }

        bool TickDaily()
        {
            bool changed = false;
            if (LocalWeek > State.weeklyWeek)
            {
                State.weeklyWeek = LocalWeek;
                State.weeklyPoints = 0;
                State.weeklyChests.Clear();
                changed = true;
            }
            if (LocalDay > State.dailyDay)
            {
                NewDailyBoard();
                changed = true;
            }
            return changed;
        }

        void NewDailyBoard()
        {
            State.dailyDay = LocalDay;
            State.dailyPoints = 0;
            State.dailyChests.Clear();
            State.dailyTasks.Clear();
            var rng = new Random(unchecked(State.rngSeed * 31 + LocalDay * 7919));
            var pool = new List<DailyKind> { DailyKind.Collect, DailyKind.Contracts, DailyKind.Rent, DailyKind.EarnCoins };
            if (State.buildings.Any(b => GameDatabase.Building(b.defId).Recipe != null)) pool.Add(DailyKind.Produce);
            if (GameDatabase.Destinations.Any(d => d.IsTown && IsDestinationUnlocked(d))) pool.Add(DailyKind.Passengers);
            if (State.stats.dispatches > Hooks.CrateMinDispatches) pool.Add(DailyKind.Crates);
            if (State.level >= Hooks.EventLevel) pool.Add(DailyKind.Events);
            if (DepthContent.Projects.Any(p => IsProjectUnlocked(p) && !Progress(p.Id).done)) pool.Add(DailyKind.ProjectDeliveries);
            if (HasGarage && State.trucks.Count > 0) pool.Add(DailyKind.TruckDeliveries);
            var kinds = new List<DailyKind> { DailyKind.Dispatch }; // every day starts with sending trains out
            while (kinds.Count < Frontier.DailyTaskCount && pool.Count > 0)
            {
                int i = rng.Next(pool.Count);
                kinds.Add(pool[i]);
                pool.RemoveAt(i);
            }
            foreach (var k in kinds)
                State.dailyTasks.Add(new DailyTask { kind = k, count = Frontier.DailyCount(k, State.level), baseline = DailyMetric(k) });
        }

        int DailyMetric(DailyKind k)
        {
            var st = State.stats;
            switch (k)
            {
                case DailyKind.Dispatch: return st.dispatches;
                case DailyKind.Contracts: return st.contractsCompleted;
                case DailyKind.Produce: return st.produced.Total();
                case DailyKind.Collect: return st.collected.Total();
                case DailyKind.Passengers: return st.passengers;
                case DailyKind.Crates: return State.cratesOpened;
                case DailyKind.Events: return st.eventsDone;
                case DailyKind.Rent: return st.rentCollections;
                case DailyKind.EarnCoins: return st.coinsEarned;
                case DailyKind.TruckDeliveries: return st.truckDeliveries;
                default: return st.projectDeliveries;
            }
        }

        public int DailyProgress(DailyTask t) { return Math.Max(0, Math.Min(t.count, DailyMetric(t.kind) - t.baseline)); }
        public bool IsDailyDone(DailyTask t) { return DailyProgress(t) >= t.count; }

        public bool CanClaimDailyChest(int at) { return State.dailyPoints >= at && !State.dailyChests.Contains(at); }
        public bool CanClaimWeeklyChest(int at) { return State.weeklyPoints >= at && !State.weeklyChests.Contains(at); }

        /// <summary>Finished tasks and full chests waiting to be claimed (for the HUD badge).</summary>
        public int DailyClaimable
        {
            get
            {
                int n = State.dailyTasks.Count(t => !t.claimed && IsDailyDone(t));
                n += Frontier.DailyChestAt.Count(CanClaimDailyChest) + Frontier.WeeklyChestAt.Count(CanClaimWeeklyChest);
                return n;
            }
        }

        public ActionResult ClaimDailyTask(int index)
        {
            if (index < 0 || index >= State.dailyTasks.Count) return ActionResult.Fail("Unknown task");
            var t = State.dailyTasks[index];
            if (t.claimed) return ActionResult.Fail("Already claimed");
            if (!IsDailyDone(t)) return ActionResult.Fail("Not finished yet");
            t.claimed = true;
            State.dailyPoints += Frontier.DailyTaskPoints;
            State.weeklyPoints += Frontier.DailyTaskPoints;
            AddXp(10 + 2 * State.level);
            RaiseChanged();
            return ActionResult.Success("+" + Frontier.DailyTaskPoints + " points");
        }

        public Grant DailyChestGrant(int at) { return Frontier.DailyChest(at, State.level); }
        public Grant WeeklyChestGrant(int at) { return Frontier.WeeklyChest(at, Hooks.PaydayBlueprintLoco(State.weeklyWeek + 1)); }

        public ActionResult ClaimDailyChest(int at)
        {
            if (!CanClaimDailyChest(at)) return ActionResult.Fail("Not yet");
            State.dailyChests.Add(at);
            string text = Apply(DailyChestGrant(at));
            string shards = GiveShards(Frontier.ChestShards(at, false));
            RaiseChanged();
            return ActionResult.Success("Daily chest: " + text + shards);
        }

        public ActionResult ClaimWeeklyChest(int at)
        {
            if (!CanClaimWeeklyChest(at)) return ActionResult.Fail("Not yet");
            State.weeklyChests.Add(at);
            string text = Apply(WeeklyChestGrant(at));
            string shards = GiveShards(Frontier.ChestShards(at, true));
            RaiseChanged();
            return ActionResult.Success("Weekly chest: " + text + shards);
        }

        // ================================================================ Engineers

        public bool CrewUnlocked { get { return State.level >= Frontier.CrewLevel; } }

        public EngineerState Crew(string id) { return State.engineers.FirstOrDefault(e => e.id == id); }

        EngineerState CrewOrNew(string id)
        {
            var e = Crew(id);
            if (e == null) { e = new EngineerState { id = id }; State.engineers.Add(e); }
            return e;
        }

        /// <summary>The recruited engineer riding this locomotive, if any.</summary>
        public EngineerState EngineerOn(TrainState t) { return State.engineers.FirstOrDefault(e => e.trainUid == t.uid && e.level > 0); }

        float EngineerValue(TrainState t, EngineerPerk perk, CargoClass cargo = CargoClass.Flat)
        {
            var e = EngineerOn(t);
            if (e == null) return 0;
            var d = Frontier.Engineer(e.id);
            if (d.Perk != perk) return 0;
            if (perk == EngineerPerk.Cargo && !d.Cargo.Contains(cargo)) return 0;
            return d.Value(e.level);
        }

        /// <summary>Shards needed to recruit (level 0) or train up an engineer; 0 at max level.</summary>
        public int TrainingShards(EngineerState e)
        {
            if (e == null || e.level <= 0) return Frontier.RecruitShards;
            return e.level >= Frontier.MaxEngineerLevel ? 0 : Frontier.LevelUpShards(e.level);
        }

        /// <summary>Recruits an engineer, or trains a recruited one to the next level, with their shards.</summary>
        public ActionResult TrainEngineer(string id)
        {
            var d = Frontier.Engineer(id);
            if (d == null) return ActionResult.Fail("Unknown engineer");
            var e = CrewOrNew(id);
            if (e.level >= Frontier.MaxEngineerLevel) return ActionResult.Fail("Already at top level");
            int need = TrainingShards(e);
            if (e.shards < need) return ActionResult.Fail("Need " + (need - e.shards) + " more " + d.Name.Split(' ')[0] + " shards");
            e.shards -= need;
            e.level++;
            AddXp(40 * e.level);
            RaiseChanged();
            return ActionResult.Success(e.level == 1 ? d.Name + " signs on!" : d.Name + " is now level " + e.level + ": " + d.PerkText(e.level));
        }

        /// <summary>Puts an engineer on a locomotive (0 sends them to the bunkhouse); one engineer per locomotive.</summary>
        public ActionResult AssignEngineer(string id, int trainUid)
        {
            var e = Crew(id);
            if (e == null || e.level <= 0) return ActionResult.Fail("Recruit them first");
            if (trainUid != 0)
            {
                var t = Train(trainUid);
                if (t == null) return ActionResult.Fail("Unknown train");
                foreach (var other in State.engineers) if (other.trainUid == trainUid) other.trainUid = 0;
            }
            e.trainUid = trainUid;
            RaiseChanged();
            var d = Frontier.Engineer(id);
            return ActionResult.Success(trainUid == 0 ? d.Name + " is resting in the bunkhouse" : d.Name + " now rides " + Name(Train(trainUid)));
        }

        /// <summary>The engineer who got the most recent shards (for reveals).</summary>
        public string LastShardsEngineer { get; private set; }

        /// <summary>Gives shards to a random engineer who can still use them; returns ", +N Name shards" (or "").</summary>
        string GiveShards(int n)
        {
            if (n <= 0 || !CrewUnlocked) return "";
            var rng = NextRng();
            var open = Frontier.Engineers.Where(d => { var e = Crew(d.Id); return e == null || e.level < Frontier.MaxEngineerLevel; }).ToList();
            if (open.Count == 0) { EarnCoins(n * 100); return ", +" + n * 100 + " coins"; }
            var pick = open[rng.Next(open.Count)];
            CrewOrNew(pick.Id).shards += n;
            LastShardsEngineer = pick.Id;
            return ", +" + n + " " + pick.Name + " shards";
        }

        bool TickCrew()
        {
            if (!CrewUnlocked || State.engineers.Count > 0) return false;
            // The first engineer signs on as soon as crews unlock.
            var first = new EngineerState { id = "bigjim", level = 1 };
            var train = State.trains.OrderBy(t => t.platform).FirstOrDefault();
            if (train != null) first.trainUid = train.uid;
            State.engineers.Add(first);
            Notify?.Invoke("Big Jim Calloway signed on as your first engineer! More join from chests and crates.");
            EngineerJoined?.Invoke(first);
            return true;
        }

        // ================================================================ Traveling peddler

        public bool PeddlerHere { get { return Now < State.peddlerUntil && State.deals.Count > 0; } }
        public double PeddlerLeft { get { return Math.Max(0, State.peddlerUntil - Now); } }

        bool TickPeddler()
        {
            bool changed = false;
            if (State.peddlerUntil > 0 && Now >= State.peddlerUntil)
            {
                State.peddlerUntil = 0;
                State.deals.Clear();
                changed = true;
            }
            if (PeddlerHere || State.level < Frontier.PeddlerLevel || !PastTutorial) return changed;
            if (State.nextPeddlerAt <= 0) { State.nextPeddlerAt = Now + Frontier.PeddlerFirstDelay; return changed; }
            if (Now < State.nextPeddlerAt) return changed;
            var rng = NextRng();
            State.deals = RollDeals(rng);
            State.peddlerUntil = Now + Frontier.PeddlerStay;
            State.nextPeddlerAt = State.peddlerUntil + Frontier.PeddlerGapMin + rng.NextDouble() * (Frontier.PeddlerGapMax - Frontier.PeddlerGapMin);
            Notify?.Invoke("A traveling peddler pulled into the station with three deals. He leaves in an hour!");
            PeddlerArrived?.Invoke();
            return true;
        }

        List<DealState> RollDeals(Random rng)
        {
            int lvl = State.level;
            var deals = new List<DealState>();
            var pool = ObtainableResources().Where(id => !GameDatabase.Resource(id).IsPassenger).ToList();
            var made = pool.Where(id => GameDatabase.Resource(id).Manufactured).ToList();
            var src = made.Count > 0 && rng.NextDouble() < 0.7 ? made : pool;
            if (src.Count > 0)
            {
                string gid = src[rng.Next(src.Count)];
                var gr = GameDatabase.Resource(gid);
                int amt = Math.Min(gr.Manufactured ? 10 + lvl * 2 : 30 + lvl * 8, Math.Max(5, WarehouseCapacity / 3));
                deals.Add(new DealState { kind = DealKind.Goods, id = gid, amount = amt, coins = (int)Math.Round(gr.Value * amt * 0.8) });
            }

            string[] parts = lvl < 4 ? new[] { "rivets" } : lvl < 8 ? new[] { "rivets", "bearings" } : new[] { "bearings", "rods", "gauges" };
            int pa = 6 + lvl / 2;
            deals.Add(new DealState { kind = DealKind.Parts, id = parts[rng.Next(parts.Length)], amount = pa, coins = pa * 90 });

            double r = rng.NextDouble();
            if (CrewUnlocked && r < 0.45)
            {
                var open = Frontier.Engineers.Where(d => { var e = Crew(d.Id); return e == null || e.level < Frontier.MaxEngineerLevel; }).ToList();
                if (open.Count > 0)
                {
                    deals.Add(new DealState { kind = DealKind.Shards, id = open[rng.Next(open.Count)].Id, amount = 5, coins = 1200 + 150 * lvl });
                    return deals;
                }
            }
            if (r < 0.55) deals.Add(new DealState { kind = DealKind.Blueprint, id = CrateBlueprintLoco(), amount = 1, coins = 5000 + 800 * lvl }); // rare
            else deals.Add(new DealState { kind = DealKind.Fuel, amount = 80, nuggets = 8 });
            return deals;
        }

        public static string DealText(DealState d)
        {
            switch (d.kind)
            {
                case DealKind.Goods:
                case DealKind.Parts: return d.amount + " " + GameDatabase.Resource(d.id).Name;
                case DealKind.Blueprint: return d.amount + " " + GameDatabase.Locomotive(d.id).Name + " blueprint";
                case DealKind.Shards: return d.amount + " " + Frontier.Engineer(d.id).Name + " shards";
                default: return d.amount + " fuel";
            }
        }

        public ActionResult BuyDeal(int index)
        {
            if (!PeddlerHere) return ActionResult.Fail("The peddler has moved on");
            if (index < 0 || index >= State.deals.Count) return ActionResult.Fail("Unknown deal");
            var d = State.deals[index];
            if (d.sold) return ActionResult.Fail("Already sold");
            if (d.kind == DealKind.Goods && WarehouseFree < d.amount) return ActionResult.Fail("Make room in the warehouse first");
            if (!CanAfford(d.coins, null, d.nuggets)) return ActionResult.Fail(MissingText(d.coins, null, d.nuggets));
            Pay(d.coins, null, d.nuggets);
            switch (d.kind)
            {
                case DealKind.Goods:
                case DealKind.Parts: State.inventory.Change(d.id, d.amount); break;
                case DealKind.Blueprint: State.blueprints.Change(d.id, d.amount); break;
                case DealKind.Shards: CrewOrNew(d.id).shards += d.amount; break;
                default: AddFuel(d.amount); break;
            }
            d.sold = true;
            RaiseChanged();
            return ActionResult.Success("Bought " + DealText(d));
        }

        // ================================================================ Full Steam

        /// <summary>Cargo multiplier the next dispatch would get (1 when no combo is running).</summary>
        public float NextCombo { get { return State.level < Frontier.ComboLevel ? 1 : Frontier.ComboMultiplier(Now < State.comboUntil ? State.comboCount + 1 : 1); } }

        public float ComboNow { get { return Now < State.comboUntil ? Frontier.ComboMultiplier(State.comboCount) : 1; } }
        public double ComboLeft { get { return Math.Max(0, State.comboUntil - Now); } }

        void ChainCombo(TrainState t)
        {
            if (State.level < Frontier.ComboLevel) { t.combo = 0; return; }
            State.comboCount = Now < State.comboUntil ? State.comboCount + 1 : 1;
            State.comboUntil = Now + Frontier.ComboWindow;
            t.combo = (int)Math.Round((Frontier.ComboMultiplier(State.comboCount) - 1) * 100);
        }

        // ================================================================ Curios

        public bool HasCurio(string id) { return State.curios.Contains(id); }
        public int ShelfOwned(ShelfDef s) { return s.Curios.Count(c => HasCurio(c.Id)); }
        public bool IsShelfComplete(ShelfDef s) { return ShelfOwned(s) >= s.Curios.Count(); }
        public bool IsShelfClaimed(ShelfDef s) { return State.shelvesClaimed.Contains(s.Id); }
        public int ShelvesClaimable { get { return Frontier.Shelves.Count(s => IsShelfComplete(s) && !IsShelfClaimed(s)); } }

        /// <summary>Called as a train departs: one in ten trips finds the outpost's curio if it's still missing.</summary>
        void RollCurio(TrainState t, Random rng)
        {
            t.curio = "";
            if (State.level < 2) return;
            var c = Frontier.Curios.FirstOrDefault(x => x.DestinationId == t.destinationId);
            if (c == null || HasCurio(c.Id) || State.trains.Any(o => o != t && o.curio == c.Id)) return;
            if (rng.NextDouble() < Frontier.CurioChance) t.curio = c.Id;
        }

        /// <summary>Moves a train's find into the museum; returns it (null if there was none).</summary>
        CurioDef CollectCurio(TrainState t, bool announce)
        {
            if (string.IsNullOrEmpty(t.curio)) return null;
            var c = Frontier.Curio(t.curio);
            t.curio = "";
            if (c == null || HasCurio(c.Id)) return null;
            State.curios.Add(c.Id);
            if (announce)
            {
                Notify?.Invoke(Name(t) + " found a curio: " + c.Name + "! It's on display in the museum.");
                CurioFound?.Invoke(c);
            }
            return c;
        }

        public ActionResult ClaimShelf(string id)
        {
            var s = Frontier.Shelf(id);
            if (s == null) return ActionResult.Fail("Unknown shelf");
            if (IsShelfClaimed(s)) return ActionResult.Fail("Already claimed");
            if (!IsShelfComplete(s)) return ActionResult.Fail((s.Curios.Count() - ShelfOwned(s)) + " curios still missing");
            State.shelvesClaimed.Add(s.Id);
            State.nuggets += s.Nuggets;
            AddXp(150);
            RaiseChanged();
            return ActionResult.Success(s.Name + " complete: " + s.PerkText + " forever, +" + s.Nuggets + " nuggets");
        }

        float ShelfPerk(CivicEffect fx)
        {
            if (State.shelvesClaimed == null) return 0;
            float total = 0;
            foreach (var id in State.shelvesClaimed)
            {
                var s = Frontier.Shelf(id);
                if (s != null && s.Perk == fx) total += s.PerkValue;
            }
            return total;
        }

        // ================================================================ New territory

        /// <summary>A new territory can be chartered once the Golden Spike is driven.</summary>
        public bool CanCharter { get { var p = State.projects.FirstOrDefault(x => x.id == "goldenspike"); return p != null && p.done; } }

        public string NextTownName { get { return Frontier.TownName(State.territory + 1); } }

        float CharterPerk(CivicEffect fx)
        {
            if (fx != CivicEffect.Income && fx != CivicEffect.Yield && fx != CivicEffect.Xp) return 0;
            return Frontier.CharterBonus * (State.territory - 1);
        }

        /// <summary>
        /// Starts over in a new town with a permanent bonus. The town, level, coins, goods, orders and projects
        /// reset; locomotives, wagons, trucks, purchases, nuggets, blueprints, engineers, the album and curios stay.
        /// </summary>
        public ActionResult CharterNewTerritory()
        {
            if (!CanCharter) return ActionResult.Fail("Drive the Golden Spike first");
            double now = Now;
            State.territory++;
            State.townName = Frontier.TownName(State.territory);
            State.coins = Balance.StartCoins;
            State.nuggets += Frontier.CharterNuggets;
            State.xp = 0;
            State.level = 1;
            State.warehouseLevel = 1;
            State.unlockedPlots = Balance.StartPlots;
            State.inventory.Clear();
            State.inventory.Change("timber", 10);
            State.inventory.Change("rivets", 12);
            State.buildings.Clear();
            State.buildings.Add(new BuildingState { uid = NewUid(), defId = "cabin", plot = 0, builtAt = now, readyAt = now, lastCollectAt = now });
            State.buildings.Add(new BuildingState { uid = NewUid(), defId = "cabin", plot = 1, builtAt = now, readyAt = now, lastCollectAt = now });
            State.contracts.Clear();
            State.nextContractAt = 0;
            State.nextExpressAt = 0;
            State.events.Clear();
            State.nextEventAt = 0;
            State.deals.Clear();
            State.peddlerUntil = 0;
            State.nextPeddlerAt = 0;
            State.comboCount = 0;
            State.comboUntil = 0;
            State.hiredCrew = 0;
            State.projects.Clear();
            foreach (var p in DepthContent.Projects) State.projects.Add(new ProjectProgress { id = p.Id });
            State.chaptersClaimed.Clear();
            foreach (var t in State.trains)
            {
                t.destinationId = "";
                t.cargoId = "";
                t.cargoAmount = 0;
                t.crate = false;
                t.robbed = 0;
                t.boom = false;
                t.combo = 0;
                t.curio = "";
            }
            // Trucks stay with the company and wait for the new town's garage; their loads and the board are gone.
            foreach (var t in State.trucks) { t.job = new DeliveryState(); t.departedAt = t.returnsAt = 0; }
            State.deliveries.Clear();
            State.nextDeliveryAt = 0;
            State.fuel = MaxFuel;
            State.fuelAt = now;
            _arrivalSeen.Clear();
            _truckSeen.Clear();
            _constructionSeen.Clear();
            _productionSeen.Clear();
            foreach (var b in State.buildings) _constructionSeen.Add(b.uid);
            ResetTaskBaselines();
            FillContracts(force: true);
            RaiseChanged();
            return ActionResult.Success("Welcome to " + State.townName + "! Every town here earns " + (int)Math.Round(Frontier.CharterBonus * 100 * (State.territory - 1)) + "% more.");
        }
    }
}
