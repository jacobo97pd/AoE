# Audio licences — Emberfield CC0 library

Staged 2026-09-27. Every clip in this library is either **CC0 1.0 Universal** (public domain
dedication) or came from a pack whose author dedicates it to CC0. Nothing CC-BY, NC, ND or
unclear-licence was downloaded or used; three OpenGameArt search sessions turned up CC-BY and
GPL-licensed ambience/music that were seen and rejected without downloading (not listed below,
since nothing of theirs was kept).

A machine-readable copy of the same facts, one row per **output** file (93 rows), is in
`manifest.json` in this folder — that is the authoritative per-file record; this document is
the human-readable pack-level summary plus a couple of notes manifest.json doesn't carry.

## Packs used

### Kenney.nl (7 packs, all CC0 1.0 Universal)

All Kenney.nl assets are blanket CC0 — confirmed on every pack page (`creativecommons.org/
publicdomain/zero/1.0/`) and in each pack's own `License.txt`, which was read after
extraction (see `_downloads/kenney/extracted/<pack>/License.txt`). Author: **Kenney Vleugels**
(kenney.nl), except the two voice packs where Kenney is the *packager* and the voice talent is
credited separately below. Accessed 2026-09-27.

| Pack | Author | Source URL | Used for |
|---|---|---|---|
| UI Audio | Kenney Vleugels | https://kenney.nl/assets/ui-audio | UI click |
| Interface Sounds | Kenney Vleugels | https://kenney.nl/assets/interface-sounds | UI click/confirm/error/notification, unit selection |
| Impact Sounds | Kenney Vleugels | https://kenney.nl/assets/impact-sounds | Footsteps, melee/armour hits, mining, building hits, siege ram (approx.) |
| RPG Audio | Kenney Vleugels | https://kenney.nl/assets/rpg-audio | Chopping, cloth rustle (farming approx.), metal click/latch (hammering approx.), creak (ladder) |
| Music Jingles | Kenney Vleugels | https://kenney.nl/assets/music-jingles | Short orchestral "hit" stabs for building/research complete and victory/defeat accents |
| Voiceover Pack | Jeffrey M. Smith (male voice, fiverr.com/jeffreymsmith) / Giselle (female voice, fiverr.com/easymedia), packaged by Kenney Vleugels | https://kenney.nl/assets/voiceover-pack | Optional unit acknowledgement barks (ready/go/hold/objective_achieved) |

**Downloaded but not used**: `voiceover-pack-fighter.zip` (kept in `_downloads/kenney/` for
completeness of the audit; its lines are all modern-military/arcade-fighter callouts —
"reloading", "RPG", "sniper", "K.O."-style rounds — that clash with a medieval RTS's tone, so
none of its 47 clips were carried into the curated library; see `CUE_MAP.md` gaps).

### OpenGameArt.org (2 items, both CC0 1.0 Universal)

License confirmed on each item's own page (the `field-name-field-art-licenses` block showing
the CC0 badge linking to `creativecommons.org/publicdomain/zero/1.0/`) and by the explicit
"Author:" byline field (not a commenter's name). Accessed 2026-09-27.

| Item | Author | Source URL | Used for |
|---|---|---|---|
| Birds and Wind – Ambient, Birds, Wind and Synth | Spring Spring | https://opengameart.org/content/birds-and-wind-ambient-birds-wind-and-synth | Forest/highland ambience bed, base layer for the elven bed |
| Wind Chimes | pmiller | https://opengameart.org/content/wind-chimes | Elven-forest chime accent |

**Downloaded but not used**: `_downloads/opengameart/jungle-chill.ogg` ("I Think I'd Stay –
Jungle Chill" by emmntt, CC0, https://opengameart.org/content/i-think-id-stay-jungle-chill) —
a lo-fi chillhop track. Genuinely CC0 and kept for the record, but stylistically it does not
fit the game's Dorian/Aeolian/Mixolydian original score (see `FrontierAmbience.Score` in the
audit) and was not carried into the curated library or `manifest.json`.

**Search session cut short**: a third round of OpenGameArt searches (ocean/coast waves,
desert wind, volcanic rumble, medieval/tavern music, bow/arrow whoosh) hit the site returning
`502 Bad Gateway` on every request after the two items above were fetched, including on a
retry after a pause. Nothing was downloaded from that round — there is nothing to license or
omit. See `CUE_MAP.md`'s gap list for what a follow-up session should search for once the site
is back.

## What "CC0" means for this project

CC0 1.0 Universal is a public-domain dedication: no attribution is legally required, no
share-alike, usable in a commercial project without royalties. Kenney explicitly says credit
"would be nice but is not mandatory"; this document exists anyway so the game can credit
Kenney, Spring Spring and pmiller in an in-game credits screen if the team chooses to (a
one-line "Sound: Kenney.nl (CC0), Spring Spring and pmiller via OpenGameArt.org (CC0)" would
be generous and accurate, not required).

## Processing note (affects licence-relevant provenance, not licence terms)

Every clip was re-encoded from the original CC0 source to OGG Vorbis after silence-trimming
and loudness-matching (see `CUE_MAP.md` and `INTEGRATION.md` for the exact method). CC0 places
no restriction on derivative works, so this re-encoding does not change anything about the
licence — it is noted here only so the provenance chain (original file → processed output) is
traceable against `manifest.json`.
