# Ironhorse Frontier, Act II: The Coast

Act II carries the railroad out of the Frontier era. Motor trucks reach the home valley first,
then the Golden Spike opens a coastal charter with a harbour, steamships and overseas trade.
After that, competition arrives: Blackwell's syndicate as an AI rival first, then weekly
leagues against real players.

It ships as **updates to Ironhorse Frontier**, not as a new app. Players keep their saves,
purchases and reviews, and every system Act II needs already exists: chapters, territory
projects, the charter restart, the album, daily hooks and the store.

## Three kinds of transport, three timescales

Each kind of transport has its own job, so nothing feels like a reskinned train.

| | Job | Trip | Player rhythm |
|---|---|---|---|
| **Trains** (1.0) | Bulk raw goods from outposts | 1 min to 1 h | The economy's engine |
| **Trucks** (1.1) | Small loads of town-made goods to local customers | 30 s to 6 min | Something to do while trains are out |
| **Ships** (1.3) | Overseas trade from the harbour | 2 to 8 h | Exotic goods and big payouts, checked once or twice a day |

Trucks are the active layer and a sink for factory goods. Ships are the long check-in that
brings players back tomorrow. Trains stay the middle.

## Release plan

| Update | Content | Needs a server |
|---|---|---|
| **1.1 Motor Age** | Motor Garage, 5 trucks, the local delivery board, truck upgrades, truck daily task, trucks in the album | No |
| **1.2 Blackwell's Challenge** | AI rival who bids against you for orders; weekly freight races against him | No |
| **1.3 The Coast** | Coastal charter after the Golden Spike: harbour town, steamships, overseas ports, new locomotive tier, Chapters 8 to 14 | No |
| **1.4 Leagues** | Weekly leagues on Game Center recurring leaderboards, ranked by freight delivered, tier rewards | No (Game Center) |
| **Later** | Rail companies (guilds) with shared goals | Yes |

Each update is batched and sent to TestFlight only when the user asks.

## 1.1 Motor Age: trucks

**Unlock.** Level 12 (Chapter 6, *Silver Lake*: the dam lights the valley and the first motor
trucks arrive). The **Motor Garage** is built from the Build menu like any building (one per
town). When it finishes, the first truck, the Dart Runabout, comes free, the same way Dusty
starts the railroad.

**Garage bays.** A level 1 garage holds 2 trucks; each garage level adds a bay (6 at level 5).
Bays are the truck version of platforms.

**The delivery board.** Customers around the valley post small orders for goods: the general
store wants flour, the lumber yard wants planks, the miners' camp wants tools. The board holds
4 orders; a new one arrives every 90 seconds until it's full, and an unwanted one can be skipped.
Each order has a customer, one good, an amount, a drive time and a reward.

- Only goods the player can make or haul are asked for (like town orders).
- Amounts fit the largest truck owned, so every order can be sent by some truck.
- Pay is about 20% better per unit than a town order, plus a little for distance, because a
  truck is tied up for the trip. Some orders add workshop parts.

**A trip.** Pick an order, pick a truck with room for it: the goods leave the warehouse, the
truck drives out of town, and when it's back a coin bubble over the garage pays the reward
(tap it, or the next-step button). Trips can be rushed with nuggets. Trucks burn no coal and
water: they are the free, active layer. (Fuel for trucks is a later tuning lever if trucks
turn out too strong.)

**Trucks** (no cargo classes in 1.1: any truck carries any good).

| Truck | Load | Speed | Level | Price |
|---|---|---|---|---|
| Dart Runabout | 6 | 40 | 12 | free with the garage, then 3,000 |
| Teamster Stake Bed | 10 | 44 | 13 | 6,500 |
| Bulldog Two-Ton | 16 | 40 | 15 | 12,000 |
| Highway Hauler | 22 | 46 | 18 | 24,000 |
| Silver Comet Van | 14 | 64 | 16 | 240 nuggets |

**Upgrades.** Trucks go to level 5: +15% load and +5% speed a level, paid in coins and tools
(copper wire from level 3).

**Customers** (drive time at speed 40).

| Customer | Wants | Drive | Level |
|---|---|---|---|
| Main Street General Store | flour, provisions, tools | 60 s | 12 |
| Halvorsen Lumber Yard | planks, steel | 90 s | 12 |
| Miss Adelaide's Hotel | provisions, flour | 75 s | 12 |
| Willow Flats Co-op | tools, planks, flour | 150 s | 12 |
| Rattlesnake Ranch | provisions, tools, planks | 180 s | 13 |
| Black Hollow miners' camp | tools, provisions, steel | 240 s | 14 |
| Silver Lake dam crews | steel, copper wire, tools | 300 s | 15 |
| Valley Power Company | copper wire, fuel | 360 s | 16 |

**Hooks it feeds.** The next-step button (a truck back with pay, an idle truck and an order it
can take), a "Make truck deliveries" daily task, the Roundhouse album (trucks count as stock).

**Art.** The garage and trucks start as procedural models built in code (free). AI models
through `Tools/art/ai_assets.py` replace them once the costs below are approved.

## 1.2 Blackwell's Challenge

- Blackwell's syndicate posts a rival bid on some town orders: fill it before his timer runs out
  and you get a bonus, miss it and he takes the order.
- A weekly **freight race**: you and Blackwell each have a goods-delivered score for the week;
  his grows on a schedule tuned to your level. Beat him for a chest; lose and he gloats.
- No server needed. It's the first taste of competition and tests whether players want more.

## 1.3 The Coast

- After the Golden Spike, the charter offers a choice: a new frontier territory (as today) or
  **the Coast**. The Coast restarts the town as a harbour city; stock, collections and
  charter bonuses carry over like a normal charter.
- **Harbour**: berths (like platforms), steamships (like locomotives, with holds instead of
  wagons) and overseas ports on a world map (tea, spices, cotton, rubber, machinery).
- New buildings: cannery, shipyard, cotton mill, crane warehouse. New locomotive tier.
- Chapters 8 to 14 with new territory projects: lighthouse, breakwater, drawbridge, dry dock,
  coast railway, ocean liner pier, world's fair.
- New tech: ship routes on a world map, a harbour scene, ships rocking at berth.
  The water shader already exists.

## 1.4 Leagues

- Game Center **recurring leaderboards** (one-week runs). Players are grouped automatically and
  ranked by freight delivered that week; tier rewards are paid in the game from the player's
  final rank. No server of our own, so the privacy labels barely change.
- Rail companies (guilds) come later and need a backend.

## Art budget

The paid pipeline is GPT Image concept, then Tripo 3D, both through Higgsfield. Every item is
priced with `higgsfield generate cost` and approved before any spend.

| Update | Models | Other |
|---|---|---|
| 1.1 | 5 trucks, Motor Garage | 1 cinematic still (first truck arrives) |
| 1.2 | Blackwell's office (optional) | Blackwell portrait |
| 1.3 | ~6 ships, ~10 harbour and industrial buildings, 2 locomotives | Coast key art, chapter scenes, postcards |

## Risks

- Act II proper starts after level 20, so most players will meet trucks and Blackwell first.
  That's why they ship first.
- Trucks must not make town orders pointless: they pay more per unit but cost time and a
  truck, and their orders are small. The balance simulator (`Tools/BalanceSim`) checks the
  economy after each change.
