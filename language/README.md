# Translate Bazaar Casino

The game loads UTF-8 JSON files from **language/** beside **BazaarCasino.dll**. The ZIP installs this folder at `BepInEx/plugins/BazaarCasino/language/` (your mod manager may add a team/package folder above it).

1. Copy `en.json` to a RoR2 language code, for example `es.json`, `pt-br.json` or `zh-cn.json`. Names are lowercase.
2. Translate only each **text** value. Keep the **key** unchanged. You may delete entries you have not translated; English is the fallback.
3. Preserve `{0}`, `{1}` etc. They hold numbers, player/item names or nested text. They can be reordered. Preserve supported TMP tags such as `<color=#E8BC54>` and `</color>`; `\n` is a line break. JSON requires double quotes and no trailing commas.
4. Start the game. `Localization.Language = auto` follows the game language. Set it to your file name without `.json` to force a language locally. Restart after editing files. A regional file overlays its base, e.g. `pt-br.json` over `pt.json`.

Example `es.json`:

```json
{
  "language": "es",
  "entries": [
    { "key": "Custom amount", "text": "Importe personalizado" },
    { "key": "Gold: {0}", "text": "Oro: {0}" },
    { "key": "Stand up / Exit", "text": "Levantarse / Salir" }
  ]
}
```

This is a template system: `Gold: {0}` matches the displayed wallet without changing its number formatting. Translate full keys rather than sentence fragments. Native/modded item names still use RoR2's own language system. The English template includes screen controls, dynamic labels, interaction prompts and gameplay errors. It is not a translation of internal logs or BepInEx settings.

Translations are local; the host and friends can use different UI languages. Protocol messages remain in their original form. Files with invalid JSON are logged and ignored. Entries with missing/extra placeholders are skipped; unknown strings fall back to English. Limit: 1 MB and 4096 entries per file. English is the only supplied full language pack; the Spanish example above is illustrative.

Keep a backup of your translation outside the mod directory when updating. Existing community language files are not deleted by our local installer. Mod managers may replace a package directory; distribute a separate translation package or restore your backup in that case.
