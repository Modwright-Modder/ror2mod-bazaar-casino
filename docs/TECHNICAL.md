# Bazaar Casino technical notes

## Multiplayer

Install version 1.11.0 on the host and every client. The host owns the decks, turns, wagers, wallet updates, reward selection and race results. Shared snapshots replicate the lounge, seats, cards, chips, wheel and machine displays. Dealer hole cards remain hidden until settlement.

The host validates door proximity and authorizes travel. The owning client then applies the approved native teleport. Door travel approvals are applied on the owning client; approvals are tied to the request, body and scene so stale replies cannot move another character.

Seat snapshots include the exact body ID and a root-coordinate return position. The owning client applies the native seat teleport; host/observer replicas follow the same chair without sending competing teleports. Capsule survivors use capsule clearance; Rigidbody survivors such as Tank use their collider bottom, and both physics position and Transform are held while seated. Leaving restores the captured root position and movement state. Old/stale body IDs and invalid coordinates cannot apply a seat.

Protocol: `203` (1.11.0). Every request includes an explicit All In flag after the table byte, in addition to the existing host-assigned seat anchor fields. Older requests are rejected. The default `Network.MessageId` is `28061`; replies use ID+1 and snapshots ID+2. Every player must use the same value. Split-screen and shared-wallet mods such as ShareSuite have not been validated.

Seat locking: the host captures one immutable root position/yaw, body ID and chair slot at join. Snapshots send that same pose to every peer. Position drift no longer auto-removes a joined player or rejects their wager. Exit, death, disconnect, a changed body or scene cleanup releases the seat. Locks run in fixed update, late update and before camera rendering, including detached model/physics transforms. Additional MonoBehaviour motors implementing the native IPhysMotor interface and non-trigger body colliders are suspended while seated, with original enabled/collision flags restored on release. Foreign native teleports and MapZone respawns are intercepted only for a living seated body. Authorized entry/exit uses the explicit root/rotation teleport overload, avoiding MotorAPI's spawn-height offset on its two-argument overload. The camera override also returns user control as soon as the seat closes.

Flying survivors: 1.10.9 pauses native EntityStateMachine managed updates for living seated bodies, preserving the actual state object without exit/re-entry. This covers HEL-P 2.3.0, whose MainState extends FlyState and uses interact/E for downward flight. RigidbodyDirection is suspended too. Both fixed and render updates enforce the chair pose against the movement motor's actual Rigidbody; kinematic, gravity, constraints and interpolation flags are captured and restored on release. Inputs are cleared while the table interface is waiting for the host as well. Death and scene exit are never held by the state pause. Failed seat setup rolls back movement changes. Local logs include body type, movement components, camera overrides and seat release reasons.

The always-visible “CASINO · E to interact” HUD hint has been removed; native interaction prompts still identify nearby activities.

## Game rules and economy

| Game | Rules |
| --- | --- |
| Blackjack | Up to eight players. Betting lasts 15 seconds; each hand has a 25-second turn. Dealer stands on all 17s. Natural blackjack pays 3:2 profit. One split into matching-rank hands is allowed, with double after split. Split aces receive one card each; split 21 pays a normal win. |
| Roulette | European single-zero wheel. The first bet starts 15 seconds of betting; Spin now starts sooner. Straight numbers return 36x, outside bets 2x, and dozens/columns 3x, including the stake. |
| Slots | Three matching Cherry/Lemon/Bell/Star/Seven/Moon symbols return 3x/5x/8x/12x/25x/60x. A pair returns the wager. Another spin is available when the reels stop, independently of win effects. |
| Hermit Derby | Four equally likely lanes. The first accepted ticket starts a 15-second countdown. Winning tickets return 4x including the stake. The default minimum is the scaled Cache price multiplied by 10, capped by MaximumBet. |

Table bets can be changed or refunded before the betting period ends. Once play begins, committed stakes still settle after leaving a seat. Leaving an active blackjack hand stands it. Wagers use the normal `CharacterMaster` gold wallet.

### Bazaar entry and custom stakes

`Economy.KeepGoldOnBazaarEntry` defaults to true on the host. It finishes only the `ConvertPlayerMoneyToExperience` collector attached to a `SceneExitController` whose resolved destination is exactly `bazaar`, before its first burst. No wallet is copied/restored or credited, so repeated frames cannot duplicate gold or also award that gold as XP. The original exit continues to teleport. Other stage exits, other collectors and Bigger Bazaar's own transfer remain unchanged. A direct stage transition that creates no collector needs no special refund.

Custom stakes are positive whole numbers. Minimum/maximum/affordability checks run on the host, including edited reserved bets. All In requires an exact match against wallet + that player's refundable stake for that same activity, and must be positive and within MaximumBet. A forged flag with a partial amount cannot bypass MinimumBet. Roulette validates the complete aggregate plus every spot, duplicate spot and range; the All In UI replaces the draft with one selected spot. Zero remains a refund operation and cannot be an All In play. All other committed stake/payout rules stay unchanged. Fixed-price Lunar Cache purchases do not use this exception.

## Localization

`language/en.json` is the editable English template; see `language/README.md`. Files are adjacent to the DLL and shipped in the ZIP. `Localization.Language=auto` follows the local RoR2 language. A regional file overlays its base language. JSON parse failures/invalid placeholders fall back to English. Static text, formatted labels, world signs and native interaction prompts are translated at the presentation boundary; network ACK strings stay unchanged so clients may use different languages. Templates support numbered placeholders and nested translated outcomes; regex matching has a time limit and a bounded cache. Native item names still use the game's language catalog.

## Lunar Cache

The default rarity weights are 70/27/3 for common/uncommon/legendary. Missing rarities are excluded and the remaining weights are normalized. Reward pools use the run's ordinary drop lists, excluding equipment, other rarities, scrap, hidden/restricted items and survivor-only relics. Command-blacklisted items are excluded when the artifact is active.

Each case in a 1x/5x/10x batch has its own reel, staggered by 0.06 seconds. The cabinet and camera keep the same size. Rewards appear as normal world pickups, or Command essences when Artifact of Command is active. Closing the view does not cancel a purchase or buy it again. Leaving the stage before a pending batch dispenses refunds it; a failed individual drop refunds that case.

With `MatchBiggerBazaarUncommon` enabled, the cost follows Bigger Bazaar's uncommon gold-shop pricing when available. Otherwise it starts at 50 and uses difficulty scaling. The host checks the quoted price. The live table/slot minimum follows the scaled Cache price within the configured wager limits.

## Settings and testing commands

Config: `BepInEx/config/local.gakon.bazaarcasino.cfg`.

| Section | Settings |
| --- | --- |
| Economy | MinimumBet floor (10), MaximumBet (100000), KeepGoldOnBazaarEntry (true). |
| Selling | Per-tier sale values and difficulty scaling. Only permanent removable items can be sold. |
| Gacha | Enabled, Bigger Bazaar price matching, base price, multiplier and rarity weights. |
| CrabRace | MinimumCachePriceMultiplier (10). |
| Lighting / Audio | Brightness (1.3), music enabled, volume (0.4). Music follows game volume/pause settings. |
| Dealer / Controls | Broker enabled; F6 closes the current interface. E opens activities. |
| Localization | Language (auto). |
| Network | MessageId (28061), identical on all players. |

Developer commands have moved to Quality of RoR2 1.1.0. Casino registers no commands and intercepts no chat. The authorized owner can use `modwright stage bazaar`, wait for it to load, then `modwright gold 5000`. The legacy aliases are available in QoR behind the same owner-account/host check. Ordinary Casino gameplay has no QoR dependency and no automatic test warp or starting gold.

If a door does not move a client, check matching versions and MessageId, then check `BepInEx/LogOutput.log` for host authorization/client acknowledgement or a local chat error. UI and artwork warm on Bazaar load; clients create geometry from host snapshots.

## Build, validation and licenses

Use .NET SDK 9 and PowerShell 7. Build references target RoR2 `1.5.0-r.1` and MMHOOK `2026.10.8`. Commands are in the [README](../README.md); the package script validates metadata, matching versions and the icon.

The rules suite passes 4,582,157 assertions, including shared rounds, bankroll accounting, payouts, race replay, reward filtering and travel reply guards and seated-flight pause/release gates. Native API and HookGen visibility audits pass. Rendering, audio and two-machine co-op still need live verification.

Code and the original synthesized soundtrack use [Apache-2.0](../LICENSE). Playing cards retain [MIT/public-domain notices](../Cards-LICENSE.txt). Native Hermit Crab and item assets load from the installed game; game binaries and decompiled sources are not redistributed. See [credits](../THIRD_PARTY_NOTICES.md).
