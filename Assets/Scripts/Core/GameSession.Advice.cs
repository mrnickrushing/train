using System.Linq;

namespace Ironhorse.Core
{
    public enum AdviceKind { Unload, WarehouseFull, Deliver, Payday, CollectGoods, DailyTask, LayPlatform, Dispatch, Rent }

    /// <summary>The one thing worth doing right now, for the HUD's next-step button.</summary>
    public sealed class Advice
    {
        public AdviceKind Kind;
        /// <summary>Short verb phrase for the button ("Unload Dusty").</summary>
        public string Text;
        /// <summary>What it pays or why, under the verb ("+40 timber"), may be empty.</summary>
        public string Detail = "";
        /// <summary>Train, building or contract uid the advice is about (0 when none).</summary>
        public int Uid;
        /// <summary>Resource id for the button's icon, or null.</summary>
        public string Icon;
    }

    public sealed partial class GameSession
    {
        /// <summary>
        /// The most useful next step, or null when there's nothing to do but wait. Money waiting to be picked up
        /// comes first (trains home, orders you can fill, payday, finished goods), then what keeps the railroad
        /// moving (a free platform, idle trains), then rent piling up.
        /// </summary>
        public Advice NextAction()
        {
            var home = State.trains.Where(HasArrived).OrderBy(t => t.arrivesAt).FirstOrDefault();
            if (home != null)
            {
                var res = GameDatabase.Resource(home.cargoId);
                bool fits = res == null || res.IsPassenger || home.cargoAmount <= 0 || WarehouseFree > 0;
                if (fits)
                    return new Advice { Kind = AdviceKind.Unload, Uid = home.uid, Icon = res != null ? res.Id : null, Text = "Unload " + ShortName(home),
                                        Detail = res != null && home.cargoAmount > 0 ? "+" + home.cargoAmount + " " + res.Name : "" };
                return new Advice { Kind = AdviceKind.WarehouseFull, Text = "Warehouse full", Detail = ShortName(home) + " can't unload: upgrade or sell goods" };
            }

            var order = State.contracts.Where(CanDeliver).OrderByDescending(c => c.express).ThenByDescending(c => c.rewardCoins).FirstOrDefault();
            if (order != null)
                return new Advice { Kind = AdviceKind.Deliver, Uid = order.uid, Text = "Fill " + order.client + "'s order",
                                    Icon = order.requirements.Count > 0 ? order.requirements[0].id : null, Detail = "+" + order.rewardCoins + " coins" + (order.express ? " · express" : "") };

            if (PastTutorial && CanClaimPayday) return new Advice { Kind = AdviceKind.Payday, Text = "Collect payday", Detail = "Day " + PaydayDay + " of " + Hooks.PaydayDays };

            foreach (var b in State.buildings)
            {
                var d = GameDatabase.Building(b.defId);
                if (d == null || d.Recipe == null) continue;
                int done = FinishedJobs(b);
                if (done == 0) continue;
                int amount = d.Recipe.Output.amount * done;
                if (WarehouseFree < amount) continue;
                var r = GameDatabase.Resource(d.Recipe.Output.id);
                return new Advice { Kind = AdviceKind.CollectGoods, Uid = b.uid, Icon = d.Recipe.Output.id, Text = "Collect at the " + d.Name,
                                    Detail = "+" + amount + " " + (r != null ? r.Name : d.Recipe.Output.id) };
            }

            if (PastTutorial && State.dailyTasks.Count > 0 && DailyClaimable > 0) return new Advice { Kind = AdviceKind.DailyTask, Text = "Claim a daily reward", Detail = "Dispatcher's board" };

            if (CanLayPlatform().Ok)
                return new Advice { Kind = AdviceKind.LayPlatform, Text = "Lay platform " + (PlatformsBuilt + 1), Detail = "Room for one more engine" };

            var idle = State.trains.Where(t => t.IsIdle && WagonsOf(t).Count > 0).OrderBy(t => t.platform).FirstOrDefault();
            if (idle != null) return new Advice { Kind = AdviceKind.Dispatch, Uid = idle.uid, Text = "Send " + ShortName(idle) + " out", Detail = "Idle at platform " + (idle.platform + 1) };

            var full = State.buildings.FirstOrDefault(b => PendingRent(b) > 0 && Now - b.lastCollectAt >= RentStorageSeconds);
            if (full != null)
            {
                int total = State.buildings.Sum(PendingRent);
                return new Advice { Kind = AdviceKind.Rent, Text = "Collect rent", Detail = "+" + total + " coins waiting" };
            }
            return null;
        }
    }
}
