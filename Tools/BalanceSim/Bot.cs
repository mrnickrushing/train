// Balance simulator: a bot plays Ironhorse Frontier's game logic for a month of short daily sessions
// and prints how fast it levels and how many coins, nuggets and blueprints it collects.
//
//   dotnet run --project Tools/BalanceSim/current          # today's logic (HOOKS defined)
//   dotnet run --project Tools/BalanceSim/baseline         # the logic before the daily hooks
//
// The bot plays like an engaged free player: five 12-minute sessions a day, everything collected,
// every train kept busy, every free reward claimed, nuggets never spent.
using System;
using System.Collections.Generic;
using System.Linq;
using Ironhorse.Core;

static class Program
{
    static ManualClock _clock;
    static GameSession S;
    static int _nuggetsSeen, _nuggetsEarned, _blueprintsEarned, _locosBuilt;
    static readonly Dictionary<string, int> _bpSeen = new Dictionary<string, int>();
    static readonly Dictionary<string, int> _sources = new Dictionary<string, int>();
    static int _jobs;
    static readonly Dictionary<string, int> _missed = new Dictionary<string, int>();
#if HOOKS
    static int _lastDay = -1;
    static List<DailyTask> _yesterday;
#endif

    /// <summary>Runs an action and books the nuggets ("n:") and blueprints ("bp:") it gave under a label.</summary>
    static void Src(string label, Action a)
    {
        int n = S.State.nuggets, bp = S.State.blueprints.Total() + S.State.trains.Count * 100;
        long xp = TotalXp(), coins = S.State.stats.coinsEarned;
        a();
        AddBig("c:" + label, S.State.stats.coinsEarned - coins);
        AddBig("xp:" + label, TotalXp() - xp);
        Add("n:" + label, S.State.nuggets - n);
        Add("bp:" + label, S.State.blueprints.Total() + S.State.trains.Count * 100 - bp);
    }

    static long TotalXp()
    {
        long t = S.State.xp;
        for (int l = 1; l < S.State.level; l++) t += Balance.XpForNextLevel(l);
        return t;
    }

    static readonly Dictionary<string, long> _xp = new Dictionary<string, long>();

    static void AddBig(string k, long v)
    {
        if (v <= 0) return;
        long have;
        _xp.TryGetValue(k, out have);
        _xp[k] = have + v;
    }

    static void Add(string k, int v)
    {
        if (v <= 0 || v >= 100) return;
        int have;
        _sources.TryGetValue(k, out have);
        _sources[k] = have + v;
    }

    static void Main(string[] args)
    {
        int days = args.Length > 0 ? int.Parse(args[0]) : 30;
        int seeds = args.Length > 1 ? int.Parse(args[1]) : 3;
        var results = new List<List<string>>();
        for (int seed = 1; seed <= seeds; seed++)
        {
            results.Add(Run(days, seed));
            var stt = S.State.stats;
            Console.WriteLine("seed " + seed + " totals: dispatches " + stt.dispatches + ", contracts " + stt.contractsCompleted + ", passengers " + stt.passengers + ", produced " + stt.produced.Total() + ", collected " + stt.collected.Total()
#if HOOKS
                + ", crates " + S.State.cratesOpened + ", events " + stt.eventsDone + ", daily jobs claimed " + _jobs + ", missed " + string.Join("/", _missed.Select(k => k.Key + " " + k.Value))
#endif
            );
            long all = _xp.Where(k => k.Key.StartsWith("xp:")).Sum(k => k.Value);
            long allc = _xp.Where(k => k.Key.StartsWith("c:")).Sum(k => k.Value);
            Console.WriteLine("seed " + seed + " coins: " + string.Join(", ", _xp.Where(k => k.Key.StartsWith("c:")).OrderByDescending(k => k.Value).Take(10).Select(k => k.Key.Substring(2) + " " + (100 * k.Value / Math.Max(1, allc)) + "%")));
            Console.WriteLine("seed " + seed + " xp: " + string.Join(", ", _xp.Where(k => k.Key.StartsWith("xp:")).OrderByDescending(k => k.Value).Take(10).Select(k => k.Key.Substring(3) + " " + (100 * k.Value / Math.Max(1, all)) + "%")));
            Console.WriteLine("seed " + seed + ": " + string.Join(", ", _sources.OrderByDescending(k => k.Value).Select(k => k.Key + " " + k.Value)));
        }
        Console.WriteLine("day | level | coins earned | nuggets earned | blueprints | locos | pop");
        for (int i = 0; i < results[0].Count; i++) Console.WriteLine(string.Join("   ||   ", results.Select(r => r[i])));
    }

    static List<string> Run(int days, int seed)
    {
        // Start on a Monday morning, local time = UTC.
        _clock = new ManualClock(1767571200 + 14 * 86400 + 8 * 3600);
        var state = GameSession.NewGame(_clock.Now, seed);
        state.settings.tutorialDone = true;
        S = new GameSession(state, _clock);
        _nuggetsSeen = state.nuggets; _nuggetsEarned = 0; _blueprintsEarned = 0; _locosBuilt = 0;
        _bpSeen.Clear();
        _sources.Clear();
        _xp.Clear();
        _jobs = 0;
        _missed.Clear();
#if HOOKS
        _lastDay = -1;
        _yesterday = null;
#endif
        var report = new List<string>();
        double[] sessionHours = { 8, 12.5, 17, 20, 22.5 };
        int[] marks = { 1, 2, 3, 5, 7, 10, 14, 21, 30, 45, 60 };
        for (int day = 0; day < days; day++)
        {
            double dayStart = 1767571200 + (14 + day) * 86400;
            foreach (double h in sessionHours)
            {
                double at = dayStart + h * 3600;
                if (at > _clock.Now) { _clock.Now = at; S.State.lastSavedAt = at; }
                Session(12 * 60);
            }
            if (marks.Contains(day + 1))
                report.Add(string.Format("{0,3} | {1,5} | {2,12:#,0} | {3,14} | {4,10} | {5,5} | {6,4}",
                    day + 1, S.Level, S.State.stats.coinsEarned, _nuggetsEarned, _blueprintsEarned, S.State.trains.Count, S.Population));
        }
        return report;
    }

    static void Session(double seconds)
    {
        double end = _clock.Now + seconds;
#if HOOKS
        Src("payday", () => S.ClaimPayday());
        Src("collect", () => S.CollectAll());
#endif
        while (_clock.Now < end)
        {
            Src("tick", () => S.Tick());
            Step();
            Track();
            _clock.Advance(20);
        }
    }

    static void Track()
    {
        if (S.State.nuggets > _nuggetsSeen) _nuggetsEarned += S.State.nuggets - _nuggetsSeen;
        _nuggetsSeen = S.State.nuggets;
        foreach (var b in S.State.blueprints)
        {
            int seen;
            _bpSeen.TryGetValue(b.id, out seen);
            if (b.amount > seen) _blueprintsEarned += b.amount - seen;
            _bpSeen[b.id] = b.amount;
        }
        foreach (var id in _bpSeen.Keys.ToList()) if (S.BlueprintsOf(id) == 0) _bpSeen[id] = 0;
    }

    /// <summary>Goods the bot holds back for the next platform (like a player saving up for it).</summary>
    static readonly HashSet<string> _save = new HashSet<string>();
    static bool Saved(string id) { return _save.Contains(id); }

    static void Step()
    {
        var st = S.State;
        _save.Clear();
#if PLATFORMS
        if (st.trains.Count >= S.FleetCapacity && !S.StationComplete && !S.LayingPlatform && st.level >= S.NextPlatformLevel && NextLoco() != null)
            foreach (var r in S.NextPlatformResources) _save.Add(r.id);
#endif
        // Trains home: unload (and open crates).
        foreach (var t in st.trains.ToList())
        {
            if (S.HasArrived(t)) Src("unload", () => S.Unload(t.uid));
#if HOOKS
            if (t.crate && !S.IsEnRoute(t)) { Add("crates", 1); Src("crate", () => S.OpenCrate(t.uid)); }
#endif
        }
        foreach (var b in st.buildings.ToList())
        {
            if (S.IsProductionDone(b)) Src("production", () => S.CollectProduction(b.uid));
            if (S.PendingRent(b) > 0 && S.RentFill(b) > 0.3f) Src("rent", () => S.CollectRent(b.uid));
            if (GameDatabase.Building(b.defId).Recipe != null && S.IsReady(b)) while (S.Enqueue(b.uid).Ok) { }
        }
        foreach (var c in st.contracts.ToList()) if (S.CanDeliver(c) && !c.requirements.Any(r => Saved(r.id))) Src(Express(c) ? "telegram" : "contract", () => S.DeliverContract(c.uid));
        // Contracts needing what we can't get for a long time are skipped now and then.
        foreach (var c in st.contracts.ToList())
            if (c.requirements.Any(r => !S.ObtainableResources().Contains(r.id))) S.DiscardContract(c.uid);
        foreach (var p in DepthContent.Projects)
        {
            var stage = S.IsProjectUnlocked(p) ? S.CurrentStage(p) : null;
            if (stage != null && !Saved(stage.ResourceId) && S.Amount(stage.ResourceId) >= Math.Min(20, stage.Amount)) Src("project", () => S.DeliverToProject(p.Id));
        }
        Src("goals", () => { while (S.CurrentGoal != null && S.IsGoalComplete(S.CurrentGoal)) S.ClaimGoal(); });
        if (S.CanClaimTaskSet) Src("foreman", () => S.ClaimTaskSet());
        var ch = S.CurrentChapter;
        if (ch != null && S.IsChapterComplete(ch)) Src("chapter", () => S.ClaimChapter(ch.Id));
        Src("pass", () => { for (int tier = 1; tier <= S.PassTier; tier++) if (S.CanClaimPass(tier, false)) S.ClaimPass(tier, false); });
        foreach (var d in GameDatabase.Locomotives) if (S.CanAssemble(d) && S.AssembleLocomotive(d.Id).Ok) _locosBuilt++;
#if HOOKS
        if (st.dailyDay != _lastDay)
        {
            if (_yesterday != null) foreach (var t in _yesterday.Where(x => !x.claimed)) { int m; _missed.TryGetValue(t.kind.ToString(), out m); _missed[t.kind.ToString()] = m + 1; }
            _lastDay = st.dailyDay;
        }
        for (int i = 0; i < st.dailyTasks.Count; i++) { int k = i; if (!st.dailyTasks[k].claimed && S.IsDailyDone(st.dailyTasks[k])) Src("daily jobs", () => { if (S.ClaimDailyTask(k).Ok) _jobs++; }); }
        _yesterday = st.dailyTasks.Select(x => new DailyTask { kind = x.kind, claimed = x.claimed }).ToList(); // snapshot for the end of the day
        Src("daily chest", () => { foreach (int at in Frontier.DailyChestAt) if (S.CanClaimDailyChest(at)) S.ClaimDailyChest(at); });
        Src("weekly chest", () => { foreach (int at in Frontier.WeeklyChestAt) if (S.CanClaimWeeklyChest(at)) S.ClaimWeeklyChest(at); });
        foreach (var e in st.events.ToList())
        {
            if (e.kind == MapEventKind.Traveler || e.kind == MapEventKind.Tumbleweed) Src("events", () => S.ClaimEvent(e.uid));
            else if (e.kind == MapEventKind.Bandits && st.coins > S.EscortCost * 5) S.PayEscort(e.uid);
        }
        foreach (var a in Hooks.AlbumSets) if (S.IsAlbumComplete(a) && !S.IsAlbumClaimed(a)) Src("album", () => S.ClaimAlbumSet(a.Id));
        foreach (var sh in Frontier.Shelves) if (S.IsShelfComplete(sh) && !S.IsShelfClaimed(sh)) Src("museum", () => S.ClaimShelf(sh.Id));
        foreach (var d in Frontier.Engineers) { var e = S.Crew(d.Id); if (e != null && e.shards >= S.TrainingShards(e) && e.level < Frontier.MaxEngineerLevel) Src("engineers", () => S.TrainEngineer(d.Id)); }
        foreach (var e in st.engineers.Where(x => x.level > 0 && x.trainUid == 0))
        {
            var free = st.trains.FirstOrDefault(t => S.EngineerOn(t) == null);
            if (free != null) S.AssignEngineer(e.id, free.uid);
        }
        if (S.PeddlerHere)
            for (int i = 0; i < st.deals.Count; i++)
                if (!st.deals[i].sold && st.deals[i].nuggets == 0 && st.coins > st.deals[i].coins * 4) { int k = i; Src("peddler", () => S.BuyDeal(k)); }
#endif
        Build();
        Rolling();
        Dispatch();
    }

    static bool Express(ContractState c)
    {
#if HOOKS
        return c.express;
#else
        return false;
#endif
    }

    static void Build()
    {
        var st = S.State;
        if (S.WarehouseUsed > S.WarehouseCapacity * 0.6f) S.UpgradeWarehouse();
        // Like a player, sell surplus when the warehouse is nearly full, keeping what orders and factories need.
        if (S.WarehouseFree < S.WarehouseCapacity * 0.2f)
        {
            var keep = new HashSet<string>(st.contracts.SelectMany(c => c.requirements).Select(r => r.id));
            foreach (var b in st.buildings) { var d = GameDatabase.Building(b.defId); if (d.Recipe != null) foreach (var i in d.Recipe.Inputs) keep.Add(i.id); }
            var surplus = st.inventory.Where(r => !GameDatabase.Resource(r.id).IsPart && !Saved(r.id)).OrderByDescending(r => keep.Contains(r.id) ? r.amount / 3 : r.amount).FirstOrDefault();
            if (surplus.amount > 0) Src("sell", () => S.Sell(surplus.id, Math.Max(1, surplus.amount / 2)));
        }
        if (S.FreePlots().Count == 0 && S.CanBuyPlot && st.level >= S.NextPlotLevel && st.coins > S.NextPlotCost * 2) S.BuyPlot();
        var plots = S.FreePlots();
        if (plots.Count == 0) return;
        // Production first (one of each), then civic, then houses; keep a coin cushion.
        var wish = GameDatabase.Buildings.Where(d => S.IsBuildingUnlocked(d) && d.Category != BuildingCategory.Decoration)
            .OrderBy(d => d.Category == BuildingCategory.Production && S.CountBuilt(d.Id) == 0 ? 0 : d.Category == BuildingCategory.Civic ? 1 : d.Category == BuildingCategory.Residential ? 2 : 3)
            .ThenBy(d => d.CostCoins);
        foreach (var d in wish)
        {
            if (d.Category == BuildingCategory.Production && S.CountBuilt(d.Id) > 0) continue;
            if (st.coins < d.CostCoins * 1.5) continue;
            if (d.Category != BuildingCategory.Production && d.CostResources != null && d.CostResources.Any(r => Saved(r.id))) continue;
            if (S.CanConstruct(d.Id).Ok && S.Construct(d.Id, plots[0]).Ok) return;
        }
    }

    /// <summary>The cheapest coin-priced engine the bot doesn't own yet.</summary>
    static LocomotiveDef NextLoco()
    {
        return GameDatabase.Locomotives.Where(d => S.IsLocoUnlocked(d) && d.PriceNuggets == 0 && d.PriceCoins > 0 && S.State.trains.All(t => t.defId != d.Id))
            .OrderBy(d => d.PriceCoins).FirstOrDefault();
    }

    static void Rolling()
    {
        var st = S.State;
        var loco = NextLoco();
#if PLATFORMS
        // A full station with an engine waiting to be bought: lay the next platform.
        if (loco != null && st.trains.Count >= S.FleetCapacity && S.CanLayPlatform().Ok && st.coins > S.NextPlatformCoins * 1.5) S.LayPlatform();
#endif
        if (loco != null && st.trains.Count < S.FleetCapacity && st.coins > loco.PriceCoins * 1.6) S.BuyLocomotive(loco.Id);
        var classes = GameDatabase.Destinations.Where(d => S.IsDestinationUnlocked(d)).Select(d => GameDatabase.Resource(d.ResourceId).Cargo).Distinct().ToList();
        foreach (var t in st.trains.Where(x => x.IsIdle && !S.HasArrived(x)))
        {
            int power = GameDatabase.Locomotive(t.defId).Power;
            if (S.WagonsOf(t).Count >= power) continue;
            // Each train specialises in one cargo class, spread across the fleet.
            var cls = classes[st.trains.IndexOf(t) % classes.Count];
            var w = GameDatabase.Wagons.Where(x => x.Cargo == cls && S.IsWagonUnlocked(x)).OrderByDescending(x => x.Capacity).FirstOrDefault();
            if (w != null && st.coins > w.PriceCoins * 3) S.BuyWagon(w.Id, t.uid);
        }
    }

    static void Dispatch()
    {
        var st = S.State;
        var need = new HashSet<string>(st.contracts.SelectMany(c => c.requirements).Where(r => S.Amount(r.id) < r.amount).Select(r => r.id));
        foreach (var t in st.trains.Where(x => x.IsIdle && !S.HasArrived(x)).ToList())
        {
            DestinationDef best = null;
            double bestScore = 0;
            foreach (var d in GameDatabase.Destinations)
            {
                if (!S.IsDestinationUnlocked(d)) continue;
                var p = S.PreviewDispatch(t, d);
                if (!p.Possible || p.Amount <= 0) continue;
                var res = GameDatabase.Resource(d.ResourceId);
                double value = d.IsTown ? p.Amount * Balance.FarePerPassenger : p.Amount * res.Value * (need.Contains(d.ResourceId) ? 3 : 1);
                double score = value / Math.Max(60, p.Seconds);
                if (score > bestScore) { bestScore = score; best = d; }
            }
            if (best != null) Src("dispatch", () => S.Dispatch(t.uid, best.Id));
        }
    }
}
