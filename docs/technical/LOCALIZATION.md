# Languages

The game is shown in **Spanish (Spain)** by default. **English** is the second language. The switch sits in the main-menu masthead («IDIOMA: ES» / "LANGUAGE: EN") and in Settings → Controls («Idioma»). The choice is saved with the other local settings (`AlphaSettings.Language`: 0 Spanish, 1 English).

## How it works

Screens keep composing their text in their source language. Most of it is English; newer faction, pirate and realm text is Spanish.

`UiLocalization` then translates every uGUI `Text` and world `TextMesh` just before the canvases draw, through `Canvas.willRenderCanvases`. So no screen has to know about languages, and a new label only needs a catalog entry.

- It never touches what a player types: an `InputField`'s own text component is skipped, while its placeholder is still translated.
- Switching language takes effect on the next frame. The original text of every label is kept, so it can be translated again.

| Part | Location | Role |
| --- | --- | --- |
| `TextTranslator` | `Assets/Game/Localization/` | The translator. It lives in an assembly with no Unity references and is tested in EditMode. |
| `SpanishCatalog` | same | English → Spanish, grouped by screen. It also fixes Spanish lines that still name English buildings or modes, for example «Hearth» → «Hogar». |
| `EnglishCatalog` | same | Spanish → English for text written in Spanish: realms, faction names, pirate units and newer lines. |
| `UiLocalization` | `Assets/Game/Presentation/` | The display hook and the current language. |

### How the translator reads a text

`TextTranslator` tries each step in turn and stops at the first that applies:

1. The whole text as an exact entry.
2. Each line on its own.
3. The same entry in upper or lower case, when the text is all upper or all lower case. So one entry covers «Settings», «SETTINGS» and «settings».
4. Templates for composed text, such as `Train {u}` → `Entrenar {u}`. The value in each placeholder is translated in turn. Placeholder options:
   - `:int` and `:num` only match numbers.
   - `:lower`, `:upper` and `:cap` adjust case.
   - `:list` translates a comma-separated list.
   - `:raw` keeps names such as usernames untouched.
5. Each part between « · » or « / » separators.

Rules cover shapes a template cannot express, such as cost lists («50 food 20 wood» → «50 comida 20 madera»). Text that matches nothing is shown unchanged.

## Adding or changing text

- **A fixed label** → add the pair to the right group in `SpanishCatalog`. Add it to `EnglishCatalog` if the source is Spanish.
- **Composed text** → add a template that mirrors the composition. Put specific templates before general ones.
- **Proper names** (map names, usernames, the Emberfield brand) → no entry needed.

Building a catalog rejects duplicate sources and templates with unknown placeholders. `LocalizationTests.CatalogsBuildWithoutDuplicatesOrBrokenTemplates` fails on such a mistake. If a catalog still failed while the game was running, translation switches off and the source text is shown; it does not throw every frame.

## Tests

- **EditMode:** `LocalizationTests` covers the translator mechanics, catalog integrity and real composed screens in both directions.
- **PlayMode:** a global `LocalizationTestSetup` turns display translation off for the existing tests, which find controls by their source text. `LocalizationIntegrationTests` turns it on:
  - It checks the match HUD in Spanish and after switching to English.
  - It checks the main menu, Settings, the pause menu and Research.
  - It writes lines that still look English to `TestResults/untranslated-text.txt` for review.

## Limits

- The character gallery («PERSONAJES») draws with IMGUI, so its Spanish labels stay Spanish in English mode.
- Voice commands keep their own language setting, because recognition depends on the Windows speech language.
- Spanish text is longer. Most labels shrink to fit, but new layouts still need a look in both languages.
