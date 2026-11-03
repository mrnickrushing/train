using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironhorse.Core
{
    public sealed partial class GameSession
    {
        public event Action<string> PurchaseGranted;

        void RepairMonetization()
        {
            if (State.ownedProducts == null) State.ownedProducts = new List<string>();
            if (State.transactions == null) State.transactions = new List<string>();
            if (State.offerPurchases == null) State.offerPurchases = new List<string>();
            if (State.passClaimedFree == null) State.passClaimedFree = new List<int>();
            if (State.passClaimedPremium == null) State.passClaimedPremium = new List<int>();
            if (State.adCounts == null) State.adCounts = new List<ResourceAmount>();
            if (State.fuel < 0) { State.fuel = MaxFuel; State.fuelAt = Now; }
            RollSeason();
        }

        // ================================================================ Fuel

        public bool HasFuelTank { get { return State.ownedProducts.Contains(Catalog.FuelTank); } }
        public int MaxFuel { get { return Balance.MaxFuel(State.level, HasFuelTank); } }

        /// <summary>Current fuel including regeneration since the last settle (regen stops at max).</summary>
        public int Fuel
        {
            get
            {
                if (State.fuel >= MaxFuel) return State.fuel;
                int regen = (int)Math.Floor(Math.Max(0, Now - State.fuelAt) / Balance.FuelRegenSeconds);
                return Math.Min(MaxFuel, State.fuel + regen);
            }
        }

        /// <summary>Seconds until the next unit of fuel (0 when full).</summary>
        public double NextFuelIn
        {
            get
            {
                if (Fuel >= MaxFuel) return 0;
                double elapsed = Math.Max(0, Now - State.fuelAt) % Balance.FuelRegenSeconds;
                return Balance.FuelRegenSeconds - elapsed;
            }
        }

        public double FullFuelIn { get { int missing = MaxFuel - Fuel; return missing <= 0 ? 0 : NextFuelIn + (missing - 1) * Balance.FuelRegenSeconds; } }

        /// <summary>Folds regeneration into the stored value, keeping the partial tick.</summary>
        void SettleFuel()
        {
            if (State.fuel >= MaxFuel) { State.fuelAt = Now; return; }
            double elapsed = Math.Max(0, Now - State.fuelAt);
            int regen = (int)Math.Floor(elapsed / Balance.FuelRegenSeconds);
            int newFuel = State.fuel + regen;
            if (newFuel >= MaxFuel) { State.fuel = MaxFuel; State.fuelAt = Now; }
            else { State.fuel = newFuel; State.fuelAt += regen * Balance.FuelRegenSeconds; }
        }

        public void AddFuel(int amount)
        {
            SettleFuel();
            State.fuel += amount; // purchases may overfill the tender
            if (State.fuel >= MaxFuel) State.fuelAt = Now;
        }

        bool TrySpendFuel(int amount)
        {
            SettleFuel();
            if (State.fuel < amount) return false;
            bool wasFull = State.fuel >= MaxFuel;
            State.fuel -= amount;
            if (wasFull && State.fuel < MaxFuel) State.fuelAt = Now; // regen clock starts now
            return true;
        }

        public int FuelCost(TrainState t, DestinationDef d)
        {
            int cost = Balance.FuelCost(WagonsOf(t).Count, d);
            float cut = EngineerValue(t, EngineerPerk.Fuel);
            return cut > 0 ? Math.Max(1, (int)Math.Round(cost * (1 - cut))) : cost;
        }

        public int RefillCost { get { return Balance.RefillNuggets(MaxFuel - Fuel); } }

        public ActionResult RefillFuelWithNuggets()
        {
            int missing = MaxFuel - Fuel;
            if (missing <= 0) return ActionResult.Fail("The tender is already full");
            var r = SpendNuggets(Balance.RefillNuggets(missing));
            if (!r) return r;
            AddFuel(missing);
            RaiseChanged();
            return ActionResult.Success("Tender topped up: +" + missing + " fuel");
        }

        // ================================================================ Rail Season Pass

        public int CurrentSeason { get { return (int)Math.Floor((Now - Balance.SeasonEpoch) / Balance.SeasonSeconds); } }
        public double SeasonEndsIn { get { return Balance.SeasonEpoch + (CurrentSeason + 1) * Balance.SeasonSeconds - Now; } }

        void RollSeason()
        {
            int season = CurrentSeason;
            if (State.passSeason == season) return;
            State.passSeason = season;
            State.passXp = 0;
            State.passPremium = false;
            State.passClaimedFree.Clear();
            State.passClaimedPremium.Clear();
        }

        public int PassTier { get { return Math.Min(Balance.PassTiers, State.passXp / Balance.PassXpPerTier); } }
        public float PassTierProgress { get { return PassTier >= Balance.PassTiers ? 1 : (State.passXp % Balance.PassXpPerTier) / (float)Balance.PassXpPerTier; } }

        public void AddPassXp(int amount)
        {
            RollSeason();
            State.passXp = Math.Min(State.passXp + amount, Balance.PassTiers * Balance.PassXpPerTier);
        }

        public bool CanClaimPass(int tier, bool premium)
        {
            if (tier < 1 || tier > PassTier) return false;
            if (premium && !State.passPremium) return false;
            return !(premium ? State.passClaimedPremium : State.passClaimedFree).Contains(tier);
        }

        public int ClaimablePassRewards
        {
            get
            {
                int n = 0;
                for (int t = 1; t <= PassTier; t++) { if (CanClaimPass(t, false)) n++; if (CanClaimPass(t, true)) n++; }
                return n;
            }
        }

        public ActionResult ClaimPass(int tier, bool premium)
        {
            RollSeason();
            if (!CanClaimPass(tier, premium))
                return ActionResult.Fail(premium && !State.passPremium ? "Unlock the Premium Pass to claim this" : "Not available yet");
            var t = Catalog.PassTiers[tier - 1];
            (premium ? State.passClaimedPremium : State.passClaimedFree).Add(tier);
            string text = Apply(premium ? t.Premium : t.Free);
            RaiseChanged();
            return ActionResult.Success("Season reward: " + text);
        }

        // ================================================================ Offers

        public int WeekIndex { get { return (int)Math.Floor((Now - Balance.SeasonEpoch) / Balance.WeekSeconds); } }
        public double WeekEndsIn { get { return Balance.SeasonEpoch + (WeekIndex + 1) * Balance.WeekSeconds - Now; } }

        /// <summary>The weekly featured bundle: rotates every Monday among offers the player has unlocked.</summary>
        public OfferDef WeeklyOffer
        {
            get
            {
                var pool = Catalog.Offers.Where(o => o.Schedule == OfferSchedule.Weekly && State.level >= o.UnlockLevel).ToList();
                if (pool.Count == 0) return null;
                var o2 = pool[((WeekIndex % pool.Count) + pool.Count) % pool.Count];
                return State.offerPurchases.Contains(o2.Id + "@" + WeekIndex) ? null : o2;
            }
        }

        public bool StarterAvailable { get { return !State.ownedProducts.Contains(Catalog.StarterPack); } }

        public List<OfferDef> ActiveOffers()
        {
            var list = new List<OfferDef>();
            if (StarterAvailable) list.Add(Catalog.Offer("starter"));
            var w = WeeklyOffer;
            if (w != null) list.Add(w);
            list.AddRange(Catalog.Offers.Where(o => o.Schedule == OfferSchedule.Always && State.level >= o.UnlockLevel));
            return list;
        }

        /// <summary>Featured offer to pop up on launch at most every 8 hours.</summary>
        public OfferDef OfferToShow()
        {
            if (State.level < 2 || Now - State.lastOfferShownAt < 8 * 3600) return null;
            var o = StarterAvailable ? Catalog.Offer("starter") : WeeklyOffer;
            if (o == null) return null;
            State.lastOfferShownAt = Now;
            return o;
        }

        public ActionResult BuyNuggetOffer(string offerId)
        {
            var o = Catalog.Offer(offerId);
            if (o == null || o.ProductId != null) return ActionResult.Fail("Unknown offer");
            if (State.level < o.UnlockLevel) return ActionResult.Fail("Unlocks at level " + o.UnlockLevel);
            var r = SpendNuggets(o.NuggetPrice);
            if (!r) return r;
            string text = Apply(o.Grant);
            RaiseChanged();
            return ActionResult.Success("Delivered: " + text);
        }

        // ================================================================ Real-money purchases

        public bool Owns(string productId) { return State.ownedProducts.Contains(productId); }

        /// <summary>
        /// Grants a verified store purchase. Idempotent per transaction id, so a purchase replayed by
        /// the store (restore, crash mid-flow) is never granted twice.
        /// </summary>
        public ActionResult GrantPurchase(string productId, string transactionId)
        {
            var p = Catalog.Product(productId);
            if (p == null) return ActionResult.Fail("Unknown product " + productId);
            if (!string.IsNullOrEmpty(transactionId) && State.transactions.Contains(transactionId)) return ActionResult.Fail("Already delivered");
            if (p.Kind == ProductKind.NonConsumable && Owns(p.Id)) return ActionResult.Fail("Already owned");
            if (p.Id == Catalog.SeasonPass && State.passPremium) return ActionResult.Fail("Premium Pass already active this season");

            if (!string.IsNullOrEmpty(transactionId))
            {
                State.transactions.Add(transactionId);
                if (State.transactions.Count > 300) State.transactions.RemoveAt(0);
            }
            if (p.Kind == ProductKind.NonConsumable) State.ownedProducts.Add(p.Id);
            var weekly = Catalog.Offers.FirstOrDefault(o => o.ProductId == p.Id && o.Schedule == OfferSchedule.Weekly);
            if (weekly != null) State.offerPurchases.Add(weekly.Id + "@" + WeekIndex);
            string text = Apply(p.Grant);
            PurchaseGranted?.Invoke(p.Id);
            RaiseChanged();
            return ActionResult.Success("Thank you! " + text);
        }

        /// <summary>Re-grants non-consumables reported by the store's restore flow.</summary>
        public int RestoreNonConsumables(IEnumerable<string> ownedProductIds)
        {
            int n = 0;
            foreach (var id in ownedProductIds)
            {
                var p = Catalog.Product(id);
                if (p == null || p.Kind != ProductKind.NonConsumable || Owns(id)) continue;
                // Fuel Tank restores its permanent bonus; Starter Pack restores ownership so it isn't offered again.
                State.ownedProducts.Add(id);
                n++;
            }
            if (n > 0) RaiseChanged();
            return n;
        }

        // ================================================================ Rewarded ads

        int Today { get { return (int)Math.Floor(Now / 86400.0); } }

        public int AdsLeft(AdReward r)
        {
            if (State.adDay != Today) return Balance.AdDailyLimit(r);
            return Math.Max(0, Balance.AdDailyLimit(r) - State.adCounts.Get(r.ToString()));
        }

        /// <summary>Call after the ad network confirms the user watched a rewarded ad.</summary>
        public ActionResult RewardAd(AdReward r, int trainUid = 0)
        {
            if (State.adDay != Today) { State.adDay = Today; State.adCounts.Clear(); }
            if (AdsLeft(r) <= 0) return ActionResult.Fail("No more of these today. Come back tomorrow!");
            string msg;
            switch (r)
            {
                case AdReward.Fuel:
                    AddFuel(Balance.AdFuel);
                    msg = "+" + Balance.AdFuel + " fuel";
                    break;
                case AdReward.Nuggets:
                    State.nuggets += Balance.AdNuggets;
                    msg = "+" + Balance.AdNuggets + " nuggets";
                    break;
                default:
                    var t = Train(trainUid);
                    if (t == null || !IsEnRoute(t)) return ActionResult.Fail("That train isn't on a trip");
                    double cut = Math.Min(Balance.AdRushSeconds, t.arrivesAt - Now);
                    t.arrivesAt -= cut;
                    t.departedAt -= cut;
                    msg = Name(t) + " arrives " + (int)(cut / 60) + " min sooner";
                    break;
            }
            State.adCounts.Change(r.ToString(), 1);
            RaiseChanged();
            return ActionResult.Success(msg);
        }

        // ================================================================ Grants

        /// <summary>Applies a grant and returns a human-readable summary.</summary>
        public string Apply(Grant g)
        {
            if (g.Coins > 0) EarnCoins(g.Coins);
            State.nuggets += g.Nuggets;
            if (g.Fuel > 0) AddFuel(g.Fuel);
            foreach (var r in g.Resources) State.inventory.Change(r.id, r.amount); // gifts may exceed warehouse space
            if (g.PassPremium) { RollSeason(); State.passPremium = true; }
            if (g.FuelTank && !State.ownedProducts.Contains(Catalog.FuelTank)) State.ownedProducts.Add(Catalog.FuelTank);
            if (g.BlueprintLoco != null && g.Blueprints > 0) State.blueprints.Change(g.BlueprintLoco, g.Blueprints);
            string extra = "";
            if (g.LocomotiveId != null)
            {
                var d = GameDatabase.Locomotive(g.LocomotiveId);
                if (State.trains.Count < FleetCapacity)
                {
                    int platform = Enumerable.Range(0, Balance.Platforms).First(p => State.trains.All(t => t.platform != p));
                    var granted = new TrainState { uid = NewUid(), defId = d.Id, platform = platform };
                    State.trains.Add(granted);
                    RaiseLocomotiveAdded(granted);
                }
                else
                {
                    // Platforms full: pay out the locomotive's value instead of losing it.
                    int value = Math.Max(150, d.PriceNuggets > 0 ? d.PriceNuggets : d.PriceCoins / 100);
                    State.nuggets += value;
                    extra = " (platforms full, received " + value + " nuggets instead of " + d.Name + ")";
                }
            }
            return g + extra;
        }
    }
}
