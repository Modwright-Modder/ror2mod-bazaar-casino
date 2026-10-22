# Publishing

Repository: https://github.com/Modwright-Modder/ror2mod-bazaar-casino

## Local verification

1. Install .NET SDK 9 and PowerShell 7.
2. Run `pwsh ./scripts/package.ps1`. It builds the DLL, runs the rules tests, validates metadata/icon/version and creates a root-metadata Thunderstore ZIP in `dist`.
3. Verify the same host-assigned chair root pose is received by both machines. Reproduce the HEL-P displacement with MotorAPI installed; the client must remain seated instead of becoming a spectator. Verify normal teleports/OOB behavior returns after Exit, and death/disconnect/scene cleanup restore all flags. Also test a motorless/custom-IPhysMotor survivor and a grounded survivor. On two machines, use HEL-P 2.3.0 as a remote client. Hold/release E while entering both tables, wait through betting, switch camera views, stand up, fly up/down again and repeat. Verify the host sees the same seated pose, no seat timeout, and release logs match explicit exits. Also test a grounded survivor, death and scene exit; inspect captured Rigidbody flag restoration. Check the persistent CASINO HUD hint is absent. Test remote Tank and humanoid survivors sitting, standing and rejoining both tables, without embedding in the floor or falling. Check local-seat logs, root-position restoration, normal movement after exit, death/respawn and rejection of older casino versions. Import that ZIP into a test mod-manager profile. With identical 1.11.0 builds on a host and remote client, verify E enters and exits the lounge for both, remote movement is replicated, leaving while seated is rejected, and late/duplicate replies do not move a respawned player or another scene. Check host authorization and client acknowledgement logs; then confirm normal runs have no automatic warp/gold/F8 shortcut, with Quality of RoR2 1.1.0, test owner-only modwright stage bazaar and modwright gold 5000; verify non-owner/client rejection and ordinary chat passthrough; check native gold sounds during seat/cabinet focus and no backlog on exit, and check broker/slot focus, casino button hover/press feedback without a white flash, fixed Cache geometry/camera at 1×/5×/10×, number-only auto-fitting chip labels, stage-scaled host/client minimum wagers and gold presets, chip denominations without multiply/divide controls, repeated blackjack chip additions/right-click refunds, normal versus survivor-only Cache items, Exit/ESC without reopening or an extra click, lounge brightness/music and game volume/pause settings, readable labels, startup logs, 1×/5×/10× independent Cache reels, cabinet/camera fit, prices and native/Command drops, clear paid-stake versus potential-return labels, stable Derby stakes/countdown and visible Back to bets/Exit controls in all cameras, shared race replay/payouts, real Hermit Crab mesh/walk/dig animation, trackside/overhead views, doorway travel, table exit, and host/client play with identical versions. Release CI and automated rules do not replace in-game verification.

## GitHub

Review local changes, commit, and push to the existing origin. The build workflow runs for push, pull request and manual dispatch. Download the ZIP from its `thunderstore-package` artifact. It does not automatically publish a GitHub release or upload to Thunderstore.

When ready, create a release/tag matching the manifest version, for example `v1.11.0`, and attach `BazaarCasino-1.11.0.zip`. No credentials or tokens need to be committed.

## Thunderstore

Sign in to Thunderstore, select/create your own publishing team, choose Risk of Rain 2, and upload the generated ZIP. The team name is selected on Thunderstore; the repository owner does not automatically determine it. Review metadata and README preview before submitting.

The [official package requirements](https://wiki.thunderstore.io/mods/creating-a-package) require `manifest.json`, `README.md` and a 256×256 `icon.png` at the ZIP root. The mod DLL is under `plugins/BazaarCasino/`; code/art license notices are included. Do not ZIP an extra outer directory or upload the source archive as the mod package.

For a new version, update the BepInPlugin version and `thunderstore/manifest.json` together, update README/changelog, then rebuild. The script rejects mismatched versions. Network protocol changes also require matching host/client versions.

Publishing itself remains a manual action. This prepared repository and its artifacts have not been pushed/uploaded by the preparation task.

## README screenshots

The GitHub README uses local images in `docs/screenshots/`. The Thunderstore README uses absolute URLs to the same files on the repository main branch. Commit and push the screenshots before updating the Thunderstore page so the image URLs can resolve. The ZIP also includes the screenshots and technical notes. Keep both README copies in sync when changing the text.

## Short release history

For each release, write one to six concise bullets in `docs/releases/<version>.md` and run:

```powershell
pwsh ./scripts/add-release-notes.ps1 -Version 1.11.0 -NotesPath ./docs/releases/1.11.0.md
```

Run this once for a new version before packaging. It inserts the new entry at the top of **Update history**, which stays at the bottom of both READMEs. It never replaces previous entries and rejects a duplicate version. Keep detailed technical history in CHANGELOG.md. The package script refuses a README whose newest history version does not match the manifest.

Test 1.11.0 in a profile without Bigger Bazaar: bring gold through a blue portal; check both host and remote player balances. Repeat with Bigger Bazaar enabled to check it does not transfer twice. Verify custom wagers, sub-minimum All In, refunds and maximum rejection at all four gambling activities. Test a translated pack on only the client while the host uses English, including error messages, world signs and custom amount entry. These require live play; automated rule/API checks do not replace them.
