# Release documentation

For every new mod version, keep short, user-facing update notes in both README.md and thunderstore/README.md. Prepend the new version above older entries under Update history at the bottom of the README; never replace or remove previous release notes. Use scripts/add-release-notes.ps1 with one to six short bullets from docs/releases/<version>.md, then keep detailed implementation history in CHANGELOG.md. The package script checks the newest README version against the manifest.

Keep language/en.json in sync with new visible Casino text. Never translate protocol payloads or internal identifiers on the server; language choice belongs to each client. Preserve community translation files when installing local builds.
