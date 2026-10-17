# Publishing

Repository: https://github.com/Modwright-Modder/ror2mod-bazaar-casino

## Local verification

1. Install .NET SDK 9 and PowerShell 7.
2. Run `pwsh ./scripts/package.ps1`. It builds the DLL, runs the rules tests, validates metadata/icon/version and creates a root-metadata Thunderstore ZIP in `dist`.
3. Import that ZIP into a test mod-manager profile. With identical 1.10.6 builds on a host and remote client, verify E enters and exits the lounge for both, remote movement is replicated, leaving while seated is rejected, and late/duplicate replies do not move a respawned player or another scene. Check host authorization and client acknowledgement logs; then confirm normal runs have no automatic warp/gold/F8 shortcut, test explicit host-only modwright_blueportal and modwright_gold 5000 separately in both console and chat (bare and / forms), malformed amounts/extra arguments, client rejection, ended-run feedback and ordinary chat passthrough; check native gold sounds during seat/cabinet focus and no backlog on exit, and check broker/slot focus, casino button hover/press feedback without a white flash, fixed Cache geometry/camera at 1×/5×/10×, number-only auto-fitting chip labels, stage-scaled host/client minimum wagers and gold presets, chip denominations without multiply/divide controls, repeated blackjack chip additions/right-click refunds, normal versus survivor-only Cache items, Exit/ESC without reopening or an extra click, lounge brightness/music and game volume/pause settings, readable labels, startup logs, 1×/5×/10× independent Cache reels, cabinet/camera fit, prices and native/Command drops, clear paid-stake versus potential-return labels, stable Derby stakes/countdown and visible Back to bets/Exit controls in all cameras, shared race replay/payouts, real Hermit Crab mesh/walk/dig animation, trackside/overhead views, doorway travel, table exit, and host/client play with identical versions. Release CI and automated rules do not replace in-game verification.

## GitHub

Review local changes, commit, and push to the existing origin. The build workflow runs for push, pull request and manual dispatch. Download the ZIP from its `thunderstore-package` artifact. It does not automatically publish a GitHub release or upload to Thunderstore.

When ready, create a release/tag matching the manifest version, for example `v1.10.6`, and attach `BazaarCasino-1.10.6.zip`. No credentials or tokens need to be committed.

## Thunderstore

Sign in to Thunderstore, select/create your own publishing team, choose Risk of Rain 2, and upload the generated ZIP. The team name is selected on Thunderstore; the repository owner does not automatically determine it. Review metadata and README preview before submitting.

The [official package requirements](https://wiki.thunderstore.io/mods/creating-a-package) require `manifest.json`, `README.md` and a 256×256 `icon.png` at the ZIP root. The mod DLL is under `plugins/BazaarCasino/`; code/art license notices are included. Do not ZIP an extra outer directory or upload the source archive as the mod package.

For a new version, update the BepInPlugin version and `thunderstore/manifest.json` together, update README/changelog, then rebuild. The script rejects mismatched versions. Network protocol changes also require matching host/client versions.

Publishing itself remains a manual action. This prepared repository and its artifacts have not been pushed/uploaded by the preparation task.
