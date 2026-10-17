# Changelog

## 1.10.6

- Fix remote clients seeing the lounge door's E prompt without moving. Native TeleportHelper skips the owning client when a host sends a teleport; send a host-approved travel reply and apply the destination on that owner as well. Fix both entrance and exit; the host path still teleports once.
- Preserve host checks for a living player, the sender connection, door proximity and seated state. Bind approval to the exact request, body and scene; ignore stale, duplicate, cancelled and non-finite travel replies.
- Route lounge replies separately from table replies and show rejected/timed-out travel in local chat instead of an inactive panel. Add per-travel host authorization/client acknowledgement logs. Require matching 1.10.6 versions for this fix; do not add automatic warp or gold.
- Release build, 4,281,807 assertions and native API/HookGen audits pass. Live two-machine co-op door travel remains to be verified in-game. Protocol 200 is unchanged; the JSON travel reply is additive.

## 1.10.5

- Accept the existing host-only modwright_blueportal and modwright_gold shortcuts from local chat as well as console, with or without a leading slash. Reuse one typed execution path, validate amounts/arguments, show local feedback, and leave ordinary chat untouched. No arbitrary chat is forwarded to console and remote chat cannot operate the host.
- Reject an already-ended run with a clear message instead of attempting a Bazaar transition during game over. No automatic warp or test gold is reintroduced.
- Keep the native MoneyText count-up running while casino focus hides its HUD. Use RoR2's own coin cadence, sound and game volume preferences while seated; visible HUDs update normally and no second custom coin event is emitted.
- Release builds, 4,281,781 rule/parser assertions, native member and installed HookGen visibility audits pass. Live console/chat dispatch, native audio timing and co-op verification remain manual. Protocol 200 is unchanged.

## 1.10.4

- Remove Testing.Enabled/StartInBazaar/test-gold configuration, automatic start-of-run Bazaar warps and F8 warp/gold shortcuts. Existing legacy values no longer activate testing behavior.
- Add explicitly invoked host-only native console commands: modwright_blueportal moves the run to Bazaar without gold; modwright_gold <amount> adds 1–1,000,000 gold to the current character without warping.
- Reject commands before a hosted run; do not start a second scene transition or re-warp from Bazaar. Opt the assembly into native searchable command registration.
- Release build, native API/HookGen audits and both console-command metadata registration checks pass. Live game console/transition testing remains manual. Retain protocol 200.

## 1.10.3

- Initialize casino screen buttons at their actual color and animate the image tint directly, avoiding native ColorTint white flashes when controls are rebuilt after clicks/host replies. Reuse this styling for broker and Derby camera buttons; preserve selected broker-item highlighting.
- Scale blackjack/roulette/slot minimum gold to the current Lunar Cache price, respecting the configured minimum floor and maximum cap. Communicate limits on all host replies, including table/race responses. Scale gold-chip presets and −/+ steps; preserve the higher Derby price multiplier.
- Fix Lunar Cache geometry, display, collider and camera to a single ten-reel capacity. Changing 1×/5×/10× only changes centered reel rows inside the screen.
- Show only the numeric amount on blackjack chips, with auto-sizing/no-wrap fit. Apply the same overflow protection to roulette chip amounts.
- Release build, installed-game API visibility audit and 4,281,758 rule/regression assertions pass, including scaled minimum/preset bounds and fixed-screen reel containment. In-game visual flash, camera, chip readability and live host/client checks remain manual.
- Retain protocol 200; matching 1.10.3 versions are recommended for the new UI and economy on every player.

## 1.10.2

- Build Cache rewards from the current run's normal tier drop lists, excluding WorldUnique/IgnoreForDropList/scrap/hidden items and Belmont-only relics. Respect CommandArtifactBlacklist while Command is enabled; prevent Crissaegrim being sold as an unusable generic prize.
- Replace blackjack's absolute Ready wager with incremental Add gold and a clickable own PLACE BET spot. Add chips repeatedly until betting closes, right-click to remove, or Refund all. Send absolute host totals for safe reconciliation and debit/refund only the difference; preserve the original countdown.
- Use the same Gold on table summary and chip denominations for blackjack/roulette. Replace ×10/÷10 with 10/50/100/500 presets plus −/+.
- Add rounded button artwork, clear hover/focus colors, white hover outlines, subtle press feedback, accented primary actions and visible disabled states. Show a white hover outline on betting spots.
- Release build, native API audit and 4,281,710 rule/regression assertions pass, including restricted prizes, repeated chip additions, refunds, budget and maximum-stake checks. Live rendering and host/client interaction checks remain manual.
- Retain protocol 200; matching 1.10.2 versions are recommended for the new interface and reward pool on every player.

## 1.10.1

- Give every purchased Cache case its own clipped horizontal reel: five rows for 5× and ten for 10×. Stagger starts/stops by 0.06 seconds, settle all rows by the existing 5.2-second host dispense time, and keep each prize at its own rarity marker.
- Expand the physical cabinet/display and adjust its fixed camera for the selected batch; retain world-space controls and native/Command pickup drops. Avoid rebuilding identical idle previews on repeated state updates.
- Add persistent Derby Back to bets, Track view, Top view and Exit controls plus readable race status; the physical desk remains reachable during a race.
- Show the actual gold wager on the Derby BET button and explain the separate 4× return including stake and net profit. Show committed ticket gold in the receipt.
- Add blackjack wager amounts next to physical chips and a prominent paid-gold total in betting/playing views, updated after Double/Split.
- Release build, native RoR2 API audit and 4,281,690 automated rule/regression assertions pass. Live camera/control/reel rendering and multiplayer verification remain manual.
- Retain protocol 200. Matching 1.10.1 versions are recommended for the new UI on every player.

## 1.10.0

- Route shared updates by explicit table/slot/race tags. Ignore default nested race objects from unrelated Unity JSON snapshots, preventing Derby stake/bet values alternating between 0 and the real minimum.
- Keep native player input buffers polling while seated/focused, clear gameplay input, and require release after closing. Apply the guard to tables, slots, Cache, Derby and broker so Exit does not immediately reopen the activity.
- Add camera-scoped ambient probes and warm/cool shadow-free directional fill for the off-map lounge; expose Lighting.Brightness (default 1.3).
- Enlarge the Lunar Cache cabinet 1.65× and add 1×/5×/10× batches with scaled total-price verification, independently rolled prizes, an on-cabinet reward grid, native/Command drops, and per-case failed-drop refunds.
- Bundle an original 40-second synthesized lounge jazz loop, preloaded during scene preparation. Fade on lounge entry/exit, honor game music/master volume, pause and focused-audio preferences; expose Audio.Enabled and MusicVolume.
- Release build, native RoR2 API visibility audit and 4,276,754 automated rule/regression assertions pass. Live rendering, interaction, audio and host/client checks remain manual.
- Protocol 200 requires Bazaar Casino 1.10.0 on host and every client.

## 1.9.0

- Load the original Hermit Crab imported model, avatar, material and animator clips directly from native Addressables. Stop cloning unloaded body skin placeholders, deleting dependent CharacterModel components, replacing meshes with procedural crabs, and overriding leg bones manually.
- Replay the original RunForward, BurrowIn, Burrowed, BurrowOut and idle clips through a visual-only PlayableGraph with short blends; retain the host's authoritative running/burrowing timeline and payouts.
- Expand the lounge from 30×24×10 to 60×60×16 and the race from eight to 28 units. Use four full-size racers, broad spectator aisles, a finish gate/scoreboard, and pack-following / V-overhead views.
- Separate the front gaming wing, side-wall slots, entry-wall broker/cache and rear arena. Add warm chandeliers, taller pillars and side seating; preserve the external dark-alcove door position.
- Preload native racing assets during scene preparation and include native models in the early render. Disable race wagering while models are unavailable.
- Release build, installed native asset/clip checks, native API audit, automated rule/layout tests, and full expanded-room collider/map-zone checks pass. In-game rendering and live host/client verification remain manual.
- Protocol 190 requires Bazaar Casino 1.9.0 on host and every client.

## 1.8.0

- Remove wall Blackjack/Roulette labels, rotate both tables lengthwise and move the broker/cache to the entrance wall. Keep walking space around the new central track.
- Eject Cache rewards through native networked pickup droplets after the case reveal, including Artifact of Command conversion. Refund a pending undispensed case on stage exit.
- Add the shared high-stakes Hermit Derby with four native visual Hermit Crabs, running/burrowing pauses, fixed track view and physical betting controls.
- Scale the minimum race stake against Cache/uncommon pricing, open a visible 15-second countdown, allow pre-race wager changes/refunds, and return 4x stake to winning tickets exactly once.
- Replay the host's race timeline for spectators and late clients; retain committed tickets after exiting the view and settle/refund appropriately on stage exit.
- Use network protocol 180; install Bazaar Casino 1.8.0 on host and every client.
- Release build, native API audit and automated rule/layout tests pass. In-game placement, model/animation, Command pickup and host/client verification remain manual.

## 1.7.1

- Render slot wager, wallet and exit controls on the physical cabinet under the reels; remove screen wager/title/status controls.
- Use the native white RoR2 outline on the lever and suppress whole-cabinet highlighting while focused.
- Move Lunar Cache cards, marker, price, odds, wallet and Open/Exit controls onto the physical machine. Its reel can finish after the player exits.
- Consume one casino ESC/cancel frame before the native pause action, independent of component update order.
- Hide local body/child effect renderers while viewing a machine and restore their prior rendering flags on exit.
- Keep existing economy, requests and multiplayer protocol 170.


## 1.7.0

- Start slots by clicking the actual 3D lever; highlight it on hover and remove the separate pull button.
- Release slot input/server locks when the reels settle, while celebration effects continue independently.
- Add the Lunar Cache machine with a case-opening item carousel, normalized 70/27/3 common/uncommon/legendary odds, run-available permanent rewards and authoritative gold/item transactions.
- Mirror the live Bigger Bazaar uncommon gold price, including its tier multiplier and previous/next-stage difficulty setting; use configurable difficulty scaling when unavailable.
- Validate quoted prices, retain/replay the last case receipt and prevent duplicate request rewards.
- Multiplayer protocol 170 requires Bazaar Casino 1.7.0 on every player.


## 1.6.1

- Slot machines use the actual 3D cabinet with transparent screen controls and a fixed camera, without a modal backdrop or duplicate 2D machine.
- Shared world snapshots remain the single source of reel animations/results.
- Local avatar rendering and input resume on exit; casino focus is released immediately and after the closing click.
- Slot overlay is constructed during the existing Bazaar preload, before first interaction.


## 1.6.0

- Broker and slot machine interfaces are dedicated to their respective interactions; remove the private blackjack/roulette tab menu and unrelated wager controls on item selling.
- Broker and world billboards use the gameplay camera rather than a potentially unrelated MainCamera.
- Prepare the lounge on Bazaar scene load/population without waiting for Newt/Seer body positions.
- Cache/preload all 61 images, generate label/canvas geometry early and warm initial rendering offscreen; add startup timing logs.
- Require slot-machine interaction and broker proximity; reject legacy private blackjack/roulette requests. Multiplayer protocol is now 160.
- Preserve the deeper corner entrance, shared tables, fixed/top views and two-hand blackjack split.
- Add source structure, reproducible packaging and GitHub CI. Testing shortcuts are disabled by default in public installations.

## 1.5.2

- Move the entrance deeper into the cave corner and verify return standing clearance.

## 1.5.0

- Direct 3D roulette betting/countdown, fixed/top camera views and integrated blackjack cards with split.
