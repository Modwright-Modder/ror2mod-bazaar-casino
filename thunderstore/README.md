# Bazaar Casino

A Casino Lounge in the Bazaar Between Time for Risk of Rain 2. Play shared blackjack and roulette with friends, pull animated slot machines, open weighted item cases, bet on the Hermit Derby, and sell items to the Lunar Broker for gold.

## Play

- Enter the Bazaar through a blue portal. Find the glowing Casino Lounge doorway at the back of the dark alcove beside the Seer area; press **E** to enter.
- **E on a table** assigns a seat. Blackjack and roulette support up to eight players, sharing one dealer or wheel while keeping wagers and payouts separate.
- **Top View / Seat View** switches the fixed table camera. **Stand up / Exit**, **ESC**, or **F6** releases the seat and restores movement/camera.
- **E on a slot machine** opens only that machine's slots. Adjust your wager on the physical panel below the reels, then click the 3D lever to spin. Its white outline appears on hover. **E on the Lunar Broker** opens only item selling. There is no separate private blackjack/roulette menu.

## Games and gold

**Blackjack:** choose a scaled gold-chip denomination (minimum ×1/×2/×5/×10, shown as actual gold amounts, or −/+), then click **Add gold** or your own chip spot on the table. Each chip adds gold to the existing stake; right-click the spot removes one chip, or **Refund all** clears it. Further chips are allowed until the original 15-second betting countdown closes. Gold updates debit/refund only the difference, and the countdown does not restart. Committed gold appears beside each physical chip and as Wager paid in the player view, including the updated total after Double/Split. Cards appear on the table and in a readable screen overlay. Hit, Stand, Double, or Split on your turn. One split into two hands is allowed for matching ranks; double after split is supported. Split aces receive one card each. Dealer stands on all 17s. Natural blackjack pays 3:2; a split 21 pays the ordinary 1:1 win. Each hand has a 25-second timeout.

**Roulette:** select a scaled gold-chip denomination (actual gold amounts or −/+; no multiply/divide buttons) and click the actual 3D betting felt to place a chip. Right-click removes one. The first confirmed chip starts a 15-second countdown; Spin now starts sooner. All players share the same simulated ball and final pocket. Single zero, straight-number, outside, dozen, and column bets are supported. A number pays 36×, outside 2×, and dozen/column 3×, including the original stake.

**Slots:** three machines with a fixed cabinet view, wager/gold/exit controls drawn on the cabinet and picture reels, downward spin animation, lever and win/jackpot effects. Three Cherry/Lemon/Bell/Star/Seven/Moon symbols return 3×/5×/8×/12×/25×/60× the wager. A pair returns the wager. A machine is ready as soon as its reels stop; win effects do not delay another spin.


**Lunar Cache:** press E at the green machine on the door-side wall. The case reel, price, odds, gold and Open/Exit controls are on the cabinet itself. Choose **1×, 5× or 10×** on the enlarged cabinet. The quoted batch price is the current scaled per-case cost multiplied by the count. Each purchased case has its own horizontal reel: 5× shows five stacked rows, 10× shows ten. Rows start and stop 0.06 seconds apart; every prize is revealed by 5.2 seconds, then one normal world pickup is ejected per case. The cabinet, display, collider and camera remain the same size/position for every batch. One or five reels are centered inside the same display that accommodates ten reels. Each winning item remains under its own central rarity marker. Walk over to collect it; with Artifact of Command enabled it becomes a Command essence of the rolled tier. Closing the view does not cancel a purchase, and reopening replays the receipt without buying again. Leaving the stage before a pending batch is dispensed refunds its total price; a failed individual drop refunds only that case. Default odds: 70% common, 27% uncommon, 3% legendary. Unavailable rarities are excluded and remaining chances renormalized. The reward pool uses the run's ordinary common/uncommon/legendary drop lists. Equipment, lunar/void/boss tiers, scrap, hidden items, WorldUnique/IgnoreForDropList items and survivor-only Belmont relics such as Crissaegrim are excluded. Command-blacklisted items are excluded while Artifact of Command is enabled. Ordinary modded droppable items remain eligible. Cost mirrors Bigger Bazaar's uncommon gold-shop settings/scaling when available; otherwise it uses a difficulty-scaled base 50. The host verifies the quoted price.

**Hermit Derby:** press E at the rear arena's betting desk. The four racers use RoR2's original Hermit Crab mesh, avatar, material and RunForward/BurrowIn/Burrowed/BurrowOut animations, loaded directly from the game. The persistent **Back to bets**, **Track view**, **Top view**, and **Exit** buttons remain accessible in every camera. Track view follows the pack; Back to bets returns to the physical desk, including during a race. Press **V** or the desk View control to toggle top view and the betting desk. The countdown/race status remains visible while watching. Select one of four Hermit Crabs, adjust the stake on the physical board and click BET. The first accepted wager starts a visible 15-second betting countdown. Crabs sprint and occasionally bury themselves, pausing before they emerge and resume. The BET button shows the gold committed (for example BET 500 GOLD). A separate line explains the potential return: 2,000 including the 500 stake, profit +1,500. Every player sees the same race; the host decides the finish and pays once. All four lanes have equal odds; a winning ticket returns 4× the stake including the original wager. Minimum stake is the current scaled Cache price ×10 by default, capped by MaximumBet. Replace or cancel/refund before the countdown ends. During a race, ESC/F6 exits the view while your committed ticket remains active. Results stay visible for six seconds, then betting reopens. On stage exit, uncommitted wagers are refunded and an active race settles against its existing run/burrow timeline.

The lounge is now 60 × 60 units with a 16-unit ceiling: five times its previous floor area. The 28-unit, four-lane sand track occupies a dedicated rear arena with a finish gate/scoreboard and viewing aisles. Full-size crabs are approximately 2.4 units wide. Blackjack/roulette occupy the front gaming wing, slots form a side-wall bank, and the broker/cache remain by the entrance wall. Warm chandeliers, camera-scoped ambient lighting and shadow-free fill lights illuminate the lounge without changing the outside Bazaar. Larger pillars and side seating define the casino areas. The external doorway stays at the same dark Bazaar alcove.

**Broker:** select a permanent removable item, then confirm Sell 1. Temporary, rental, hidden, and non-removable items cannot be sold. Sale values follow difficulty by default. Gold uses the ordinary CharacterMaster wallet, including Bigger Bazaar shops.

Casino screen buttons use rounded corners, high-contrast hover/focus colors, white hover outlines, press feedback and visible disabled states. They initialize at their actual tint without a white ColorTint flash after clicks/state updates. Chip labels contain numbers only and automatically shrink to fit.

All displayed returns include the original wager. Before a round starts, canceling or leaving refunds reserved gold. After a round starts, blackjack stands remaining hands and roulette still settles; committed stakes are not refunded.

## Multiplayer and compatibility

Install **Bazaar Casino 1.10.6 on the host and every client**, with BepInExPack and HookGenPatcher. Version 1.10.x uses protocol 200; versions before 1.10.0 are incompatible.

Door entry/exit is approved by the host after checking the player and distance. Version 1.10.6 also sends that approval to the owning client, which applies the native teleport locally; this fixes clients seeing E without moving. Every player must update to 1.10.6 for this travel fix. Expired requests cannot teleport a respawned body or a different scene; door rejection/timeout messages appear locally in chat.

The host owns decks, turns, wagers, gold updates, and settlement. Shared snapshots synchronize the lounge, seats, visible cards, chips, wheel, slot display and results. Dealer hole cards remain hidden until settlement. The lounge does not replace Bigger Bazaar shops or its economy; Bigger Bazaar is optional.

ESC closes the active casino interface before the game processes its pause action. Native input buffers keep polling while seated/focused; a short release guard prevents a held interaction or the Exit click from reopening the activity. The world-machine cursor root contains no screen graphics. Local body render flags are restored when leaving the fixed view.

Standard single-player and networked co-op are supported by the implementation. Live multi-instance co-op, split-screen, and shared-wallet mods such as ShareSuite have not yet been validated.

## Configuration

`BepInEx/config/local.gakon.bazaarcasino.cfg`

- MinimumBet: configured floor 10. The live table/slot minimum follows the current scaled Lunar Cache price (including Bigger Bazaar integration), capped by the configured MaximumBet. The host communicates/enforces the same minimum on clients. Chip presets and −/+ increments follow that minimum. MaximumBet: 100000 initially, or aggregate roulette stake. Split plus doubles can reserve up to 4× the initial blackjack stake.
- Selling: per-tier values and difficulty scaling.
- Gacha: Enabled, MatchBiggerBazaarUncommon, BaseGoldCost, PriceMultiplier, and common/uncommon/legendary weights. Defaults: enabled, green-price mirroring, base 50, multiplier 1, weights 70/27/3.
- Lighting.Brightness: 1.3 by default (0.3–3); lounge ambient and fill lighting.
- Audio.Enabled: true; Audio.MusicVolume: 0.4 (0–1). An original 40-second lounge jazz loop fades in inside the casino and out on exit. It follows the game music/master volume, pause, and focused-audio setting. Native Bazaar music resumes outside.
- CrabRace.MinimumCachePriceMultiplier: 10 by default; controls the high-stakes minimum relative to the current scaled Cache price.
- Dealer.Enabled: indoor item broker.
- Controls.OpenCasino: F6, closes the active interface; use E to open interactables.
- Network.MessageId: 28061; every player must use the same value. Replies use ID+1 and table snapshots ID+2.
- Automatic test warps, test gold and F8 shortcuts are removed, including old Testing.Enabled/StartInBazaar settings. Normal runs always begin normally.

## Manual console and chat commands

Run these from the game's console as the host, after starting a run:

```text
modwright_blueportal
modwright_gold 5000
```

You can also enter `/modwright_blueportal` or `/modwright_gold 5000` into chat (the leading slash is optional). The mod consumes only these two shortcuts and shows local feedback instead of broadcasting them as chat messages. They remain host-only; ordinary chat is unchanged. Commands reject a run that has already ended, so start a new run after game over.

`modwright_blueportal` moves the run directly to the Bazaar, without adding gold. Wait for the Bazaar to load before adding test gold. `modwright_gold <amount>` adds 1–1,000,000 gold to your current character without warping; it never runs automatically. Existing wallet limits still apply. Client-only commands are rejected. These commands affect the current run, not account currency. No cheat toggle is required.

## Scene preparation

Artwork, UI, lounge and tables are prepared on Bazaar scene load/scene population, using fixed native scene anchors instead of waiting for Newt's body. Native Hermit Crab FBX/controller/material are requested asynchronously at scene preparation, and their real mesh/animation setup completes before the warmup render. Shared artwork is decoded once, labels/canvases are generated early, and the lounge/door receive an offscreen first render. Clients warm the host's replicated layout on their initial snapshots. Logs beginning `Bazaar scene preload:` show completion and elapsed milliseconds. This is intended to reduce first-visit stalls; runtime frame-time profiling is still needed.

## Build

Use the .NET 9 SDK and PowerShell 7. The project references public modding NuGet packages; no game DLLs are included in the repository or release.

```powershell
dotnet build src/BazaarCasino/BazaarCasino.csproj -c Release
dotnet run --project tests/BazaarCasino.Tests -c Release
pwsh ./scripts/package.ps1
```

The package is written to `dist/BazaarCasino-1.10.6.zip`. GitHub Actions builds, tests and uploads the ZIP as an artifact. See [PUBLISHING.md](PUBLISHING.md) for publishing steps and [CHANGELOG.md](CHANGELOG.md) for release changes.

## Gold audio

When a fixed casino camera hides the HUD, the native RoR2 money counter continues its own count-up and `Play_UI_coin` sound locally. Gold increases are audible while seated or focused on a cabinet, with the normal game cadence and volume settings. It does not wait until leaving the seat or add a duplicate custom sound. Visible HUDs retain their normal automatic update.

## Validation and license

Release build and a local audit against the installed game's RoR2 API pass. The rules suite covers 4,281,807 assertions, including bounded console/chat shortcut parsing, shared eight-player rounds, split/double accounting, refunds, timeouts, dealer privacy, deterministic roulette replay, exactly-once table payouts, gacha odds/pool filtering, price bounds and viewport-independent case reveal, native crab run/burrow replay, lane fairness, tagged snapshot routing, interaction release guards and safe batch quotes and staggered multi-reel settlement/replay, repeated blackjack chip additions/refunds, budget/limit enforcement and restricted Cache rewards, scaled minimum/preset bounds and fixed-screen reel containment. Static native geometry checks cover the doorway, return standing area, approach route, room volume and MapZone bounds. Rendering, exit behavior, audio playback, startup frame-time improvements and two-instance co-op require live verification.

Code is Apache-2.0; see [LICENSE](LICENSE). Card images have separate MIT/public-domain notices in [Cards-LICENSE.txt](Cards-LICENSE.txt). Other casino artwork is procedural. The included original synthesized soundtrack is also Apache-2.0; it contains no sampled or third-party music. Game assemblies, decompiled game code, collision dumps, user profiles and credentials are not distributed.
