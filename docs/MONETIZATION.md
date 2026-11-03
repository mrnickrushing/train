# Monetization: how Ironhorse Frontier makes money

Free to play, with five revenue streams. All rules live in the tested simulation
(`Assets/Scripts/Core/Monetization.cs`, `GameSession.Monetization.cs`); the Unity side only shows
screens and talks to the store and ad SDKs.

| Stream | How it works | Where to tune it |
|---|---|---|
| **Coal & Water (fuel)** | Every dispatch burns fuel (2 + wagons + outpost tier). It refills 1 per 45 s up to a max of 60 + 4/level, and leveling up refills it. Players top up with nuggets (1 per 8 fuel), a rewarded ad (+25, 5/day) or a nugget supply crate. | `Balance` → Fuel section |
| **Gold nuggets** | Premium currency, five packs from $0.99 to $49.99. Spent on rushes, fuel, supplies and offers. | `Catalog.Products` |
| **Offers** | One-time *Homesteader's Starter Pack* ($2.99), a **weekly featured bundle** rotating every Monday (Iron Bison, Prospector's Haul, Silver Arrow, Thunderhead Twin), the *Big Tender* upgrade ($4.99, +50 max fuel forever), and nugget-priced supply crates. A featured offer pops up at most every 8 hours from level 2. | `Catalog.Offers` |
| **Rail Season Pass** | 28-day seasons, 30 tiers × 500 season XP, earned by dispatching, contracts, production and building. Free and premium tracks; premium costs $7.99 per season. Premium tier 10 gives the Red Mesa and tier 30 the **Sundown Express**. | `Catalog.PassTiers`, `Balance` → Season Pass |
| **Rewarded ads** | Free fuel, 3 free nuggets, or 30 minutes off a trip, each with daily limits. | `Balance.AdDailyLimit` |

Safeguards built in:
- **Idempotent grants:** each store transaction id is granted once, even if the store replays it.
- **Saved right away:** the game saves immediately after any paid grant.
- **Nothing lost:** a locomotive granted while all platforms are full pays out nuggets instead.
- **No free purchases in release:** test purchases and test ads work only in the editor and
  development builds. A release build without the SDKs shows "store not available."

## 1. App Store Connect: create the products

In App Store Connect → your app → **In-App Purchases**, create these (ids must match exactly):

| Product id | Type | Price |
|---|---|---|
| `ironhorse.nuggets_100` | Consumable | $0.99 |
| `ironhorse.nuggets_550` | Consumable | $4.99 |
| `ironhorse.nuggets_1200` | Consumable | $9.99 |
| `ironhorse.nuggets_2600` | Consumable | $19.99 |
| `ironhorse.nuggets_7000` | Consumable | $49.99 |
| `ironhorse.starter_pack` | Non-consumable | $2.99 |
| `ironhorse.fuel_tank` | Non-consumable | $4.99 |
| `ironhorse.season_pass` | Consumable (one per season) | $7.99 |
| `ironhorse.bundle_prospector` | Consumable | $4.99 |
| `ironhorse.bundle_ironbison` | Consumable | $9.99 |
| `ironhorse.bundle_silverarrow` | Consumable | $19.99 |
| `ironhorse.bundle_thunderhead` | Consumable | $29.99 |

Each needs a display name, a description and a review screenshot. The shop screen works for
that screenshot.

## 2. RevenueCat: connect purchases

Done in the project:
- The 12 products above exist in App Store Connect (app 6830036020) with names, descriptions, US
  prices (other territories follow Apple's equalized tiers) and availability everywhere.
- The **RevenueCat Unity SDK 10.0.0** is installed from OpenUPM (`Packages/manifest.json`). It adds
  RevenueCat's iOS library to the exported Xcode project as a Swift package, so Codemagic needs no
  CocoaPods step.
- `Ironhorse.Game.asmdef` defines `IRONHORSE_REVENUECAT` automatically when the package is present,
  and `RevenueCatStore` is compiled and matched to the v10 API (`PurchaseProduct` returns a
  `PurchaseResult`).

Still to do:
1. In RevenueCat, create a project and add an **App Store** app for bundle id
   `com.rushingtech.ironhorsefrontier`. Upload an **In-App Purchase key** (App Store Connect →
   Users and Access → Integrations → In-App Purchase), then import the products.
2. Put the project's Apple public SDK key (`appl_…`) in `RevenueCatStore.ApiKey`
   (`Assets/Scripts/Game/Monetization/StoreService.cs`). Until then the game keeps using the test
   store (purchases only work in the Editor and development builds, never free in release).
3. Sign the **Paid Apps Agreement** (App Store Connect → Business) or purchases fail even in sandbox.
4. Build to TestFlight and buy with a sandbox account.

## 3. Rewarded ads

`Assets/Scripts/Game/Monetization/AdService.cs` defines `IAds`. To go live, add an ad network
(Unity **LevelPlay** or Google **AdMob**), implement `IAds.ShowRewarded` to call back `true` only
when the network reports a completed rewarded view, and return it from `Ads.Current`.
Ad SDKs also require Apple's **App Tracking Transparency** prompt and the matching App Privacy
answers in App Store Connect.

## 4. Apple review checklist

- **Restore Purchases** button: in the shop (Nuggets tab) and in Settings.
- All real-money purchases go through Apple's In-App Purchase (RevenueCat).
- Offer screens list exactly what each bundle contains.
- No randomized paid rewards (no loot boxes), so no odds disclosure is needed. If you ever add
  them, show the odds before purchase.
- Privacy policy URL, plus App Privacy answers for analytics and ads.

## Ideas for later

- **First-purchase double nuggets:** a classic conversion booster.
- **VIP / monthly subscription:** daily nuggets and +1 platform.
- **Event passes and limited-time locomotives:** ties into the gameplay-depth work.
- **Remote config:** change prices and offer rotation without an app update.
