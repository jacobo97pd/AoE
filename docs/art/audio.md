# Audio: the CC0 clip library

Staged 2026-09-27, wired into the game 2026-09-28. Before this pass, everything Emberfield played was
synthesised at runtime (`SoundForge.cs`, `FrontierSounds.cs`, `FrontierAmbience.cs`) — no recordings shipped
at all. This pass adds a small library of licensed recordings under `Assets/Game/Resources/Audio/` and layers
or swaps them into the existing synthesis, cue by cue, rather than replacing it: a cue with no good clip keeps
its synth, so a missing or renamed resource degrades to the synth instead of to silence.

## Licences

Every clip is **CC0 1.0 Universal** (public domain dedication) — 7 packs from Kenney.nl and 2 items from
OpenGameArt.org (Spring Spring's "Birds and Wind" and pmiller's "Wind Chimes"). Nothing CC-BY, NC, ND or of
unclear licence was kept. The full per-pack summary is `Assets/Game/Resources/Audio/LICENSES.md`, and the
authoritative per-file record (source pack, author, licence URL, duration, measured loudness) is
`Assets/Game/Resources/Audio/manifest.json`, 93 rows. Credit is not legally required for CC0 but is generous:
"Sound: Kenney.nl (CC0), Spring Spring and pmiller via OpenGameArt.org (CC0)".

One pack was downloaded and **not** used: Kenney's fighter voiceover pack, whose lines ("reloading", "RPG",
"sniper", "K.O.") read as a modern arcade shooter, not a medieval RTS. Only the plain Voiceover Pack's neutral
words made it in (see Order below), and only three of its four lines.

## Import settings (`Assets/Game/Editor/AudioAssetImport.cs`)

Short one-shots (`Ui/`, `Units/`, `Combat/`, `Economy/`) decompress on load as ADPCM: cheap to decode, small
enough to hold in memory in full on a phone. The two 81 s ambience beds (forest, reused for highland, and the
elven wind bed) and any future music track stream Vorbis instead, at quality 0.7 — decompressing either bed
fully would hold about 57 MB of PCM per clip, which streaming avoids entirely. "Emberfield/Audio/Apply clip
import settings" reapplies this to clips added after the fact. Total curated size: 93 files, 3.53 MB on disk.

## Cue map

### `FeedbackCue` (`SliceFeedback.cs`, played through the existing 6-voice pool)

| Cue | Real clip(s) | Treatment |
|---|---|---|
| `Chop` | `economy_chop_01` | Swapped in for the synth axe-knock. |
| `Mine` | `economy_mine_01..05` | Swapped in, one of 5 picked at random, ±4% pitch. |
| `Complete` | `economy_building_complete_01` | Swapped in for the synth pluck arpeggio. |
| `Impact` | `combat_metal_{light,medium,heavy}_01..03` (9) | Swapped in, one of 9 picked at random, ±4% pitch. |
| `Objective` | `ui_notify_01..03` | Swapped in for the synth bell strikes. |
| `Order` | `unit_ack_{m,f}_{ready,go,hold}` (6, never `objective_achieved`) | **Layered**, not swapped: the synth pluck always plays, and 25% of the time a quiet (`0.11` vs. the cue pool's `0.18`) neutral acknowledgement plays alongside it on its own `AudioSource`, so a rare bark never steals a slot from the pool every other cue depends on. |
| `Gather` | *(kept synthesised)* | The only real clip is a cloth rustle standing in for grain — a weak approximation the source review flagged, not worth layering yet. |
| `Defeat` | *(kept synthesised)* | Already reads well and fires often in a fight; real bell-toll/stab clips are staged (`Combat/Death`, `Music/Defeat`) but not wired. |

The lookup lives in `FrontierClips.cs`, indexed by `(int)FeedbackCue` the same way `SliceFeedback`'s own
`clips[]`/`materials[]` are — no dictionary keyed on an enum, so no per-lookup allocation risk.

### Ambience beds (`FrontierAmbience.Bed`)

| Biome | Real clip | Notes |
|---|---|---|
| `forest` | `Ambience/Forest/ambience_forest_bed` | Replaces the synth wind+bird bed. |
| `highland` | `Ambience/Highland/ambience_highland_bed` | Same source file as forest — the code path is shared today (`FrontierAmbience.Bed`'s `else` branch), so the art matches. |
| `elven` (`MapLands.Elven`) | `Ambience/Elven/ambience_elven_wind_bed` + a periodic `ambience_elven_chimes_accent` one-shot | The accent plays sparsely (every 6-11 s) over the bed, the same spirit as the synth's own `Chimes()`, volume tracking whichever land's bed is currently audible. |
| `desert`, `caribbean` (coast), `MapLands.Volcanic` | *(kept synthesised)* | No CC0 recording was found for any of the three; a follow-up search is listed in the original staging's `CUE_MAP.md`. |

### UI clicks (new: the game had no UI-chrome sound before this pass)

`UiSound.cs` is a small lazily-created pool (4 voices) wired into every button factory across the HUD and its
panels (`AlphaPanel`, `FactionPanel`, `MatchHud`, `OfflinePanel`, `OnlinePanel`): `UiCue.Click`
(`Ui/Click/ui_click_01..06`) for an ordinary button, `UiCue.Confirm` (`Ui/Confirm/ui_confirm_01..04`) for
whichever button each panel already treats as its primary action (the same boolean each panel already computes
for `AlphaTheme.StyleButton`). Both pick a random variant with ±3% pitch. `Ui/Error` and `Ui/Notification` are
staged but not wired to a call site yet (see Gaps).

## Settings and pooling

Nothing new was added to `AlphaSettings`. Every real clip plays through the game's one existing mute switch
(`SliceFeedback.Muted`, kept in sync with `Settings.SoundEnabled` by `AlphaControls`) and the shared
`AudioListener.volume` master level (`Settings.SoundVolume`) — `UiSound` reads neither directly, exactly like
`SliceFeedback`'s own cue voices never have. The ambience beds keep answering `SoundEnabled` only, never
`MusicVolume`, exactly as the synth beds always did; nothing new touches the score or `MusicVolume`.

## Gaps (left synthesised or unwired, deliberately)

- **Ambience**: coast, desert, volcanic beds — no CC0 source found.
- **Music**: no CC0 replacement for any of the four realm scores or a battle track; only sub-1.5 s stabs exist
  for victory/defeat (`Music/Victory`, `Music/Defeat`), and they are not wired to a match-end event.
- **Combat**: arrow release/impact, collapse, boiling oil — no CC0 source found.
- **Economy**: construction hammering is only weakly approximated (`economy_hammer_01,02`) and not wired.
- **Footsteps and unit selection**: clips are staged (`Units/Footsteps`, `Units/Selection`) but not wired. The
  game has no footstep or selection sound today, and firing one per unit per step risks reintroducing
  per-frame work in the large-army animation path (see `PERFORMANCE_BUDGET.md`); selection fires from a single
  choke point (`MatchController.Select`) shared by dozens of scripted scenarios and smoke tests, several of
  which call it before a `SliceFeedback`/camera exists, so wiring it needs more care than this pass had budget
  for.
- **UI error/notification**: clips are staged (`Ui/Error`, `Ui/Notification`) but no single call site cleanly
  distinguishes a rejection from ordinary feedback text (`MatchController.SetFeedback` carries both today).

Provenance, full file-by-file licensing and duration/loudness data: `Assets/Game/Resources/Audio/manifest.json`
and `LICENSES.md`.
