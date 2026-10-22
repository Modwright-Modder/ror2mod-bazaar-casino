# Bazaar Casino

Spend your run's gold in a casino inside the Bazaar Between Time. Play blackjack and roulette with friends, pull slot machines, open item cases, or bet on racing Hermit Crabs.

![Blackjack with readable dealer and player cards, wager totals and Hit, Stand, Double and Split controls](https://raw.githubusercontent.com/Modwright-Modder/ror2mod-bazaar-casino/main/docs/screenshots/blackjack.png)

## Get started

1. Install with Gale, r2modman or Thunderstore Mod Manager. It needs BepInExPack and HookGenPatcher. **The host and every player must use Bazaar Casino 1.11.1.**
2. Enter the Bazaar through a blue portal. Find the glowing Casino Lounge door in the dark alcove beside the two stage-selection orbs. Press **E** to enter.
3. Press **E** at a table, machine or the Lunar Broker. Use the controls there to play or sell items. **Esc / F6** leaves the current activity; the lounge's exit door returns you to the Bazaar.

Everything uses ordinary run gold. You keep it when entering the Bazaar, even without Bigger Bazaar. You can sell permanent items to the Lunar Broker for more gold.

Choose **Custom** to type a wager. In blackjack, Apply sets your total wager; in roulette it sets the chip placed on your next click. Slots and Derby use the amount on the next spin/bet. **All In** uses your wallet plus your own refundable bet. It can go below the minimum, but cannot exceed the maximum. Roulette All In replaces your bets with one spot on your next click. Lunar Cache keeps its fixed purchase prices.

## Games and rewards

| Activity | How it works |
| --- | --- |
| Blackjack | Add gold chips, then use Hit, Stand, Double or Split. Cards appear on the table and on screen. Up to eight players share the dealer. |
| Roulette | Click the table to place a chip; right-click to remove one. The first bet starts a 5-second countdown. Everyone shares the wheel result. |
| Slots | Set a wager on the cabinet and click its lever. A pair returns your stake; three matching symbols pay more. |
| Lunar Cache | Open 1, 5 or 10 cases together. Default odds are 70% common, 27% uncommon and 3% legendary. Rewards drop onto the floor. |
| Hermit Derby | Pick one of four crabs and place a bet. They run, burrow and sometimes fall behind. A winning ticket returns four times your stake. |
| Lunar Broker | Select a permanent item and confirm the sale. Temporary/rental items cannot be sold. |

Displayed returns include your original stake. You can refund table bets before play starts; committed bets still settle if you leave the view.

![Casino Lounge with blackjack, roulette, slot machines and the Hermit Derby arena](https://raw.githubusercontent.com/Modwright-Modder/ror2mod-bazaar-casino/main/docs/screenshots/casino-lounge.png)

## Compatibility and settings

Bigger Bazaar is optional. Lunar Cache prices follow its uncommon-item price when available; otherwise they scale from a base cost of 50 gold. Minimum wagers also scale with difficulty. Artifact of Command turns Cache rewards into Command essences of the rolled tier.

While seated, your position stays locked to the chair. Exit restores movement.

The host checks wagers, gold, rewards and door travel. Keep the same mod version and network MessageId on every player. Split-screen and shared-wallet mods have not been validated.

Settings: `BepInEx/config/local.gakon.bazaarcasino.cfg`. Adjust wager limits, item sale values, Cache odds/prices, Derby stakes, lighting and music there.

## Translations

Casino text follows your game language. To translate it, copy `language/en.json` beside the DLL to a language code such as `language/es.json`, then edit the `text` values. Keep the keys and `{0}` placeholders. Missing entries use English. You can also choose a file through `Localization.Language` in the config. See `language/README.md` included with the mod for translation instructions.

## For developers

Build with .NET 9 and PowerShell 7:

```powershell
dotnet build src/BazaarCasino/BazaarCasino.csproj -c Release
dotnet run --project tests/BazaarCasino.Tests -c Release
pwsh ./scripts/package.ps1
```

The ZIP goes in `dist/`. See [technical notes](https://github.com/Modwright-Modder/ror2mod-bazaar-casino/blob/main/docs/TECHNICAL.md) for game rules and testing notes, [publishing](https://github.com/Modwright-Modder/ror2mod-bazaar-casino/blob/main/PUBLISHING.md), and the [changelog](https://github.com/Modwright-Modder/ror2mod-bazaar-casino/blob/main/CHANGELOG.md).

Code and the original soundtrack use Apache-2.0. Card artwork has separate MIT/public-domain notices. See [credits and licenses](https://github.com/Modwright-Modder/ror2mod-bazaar-casino/blob/main/THIRD_PARTY_NOTICES.md).

## Update history

### 1.11.1

- Shorten blackjack, roulette and Hermit Derby betting to 5 seconds, with a 1-second result pause before reopening bets.
- Keep blackjack turn timers and native wheel/race animation lengths; payouts remain immediate and exactly once.

### 1.11.0

- Keep run gold when entering the Bazaar, even without Bigger Bazaar.
- Enter custom wagers for blackjack, roulette, slots and Derby. All In may go below the minimum; the maximum still applies.
- Add editable language JSON files for local UI translations, with English fallback.
- Start a short README update history; future releases are added above older entries.

### 1.10.10

- Developer commands moved to Quality of RoR2; normal Casino play does not require QoR.
