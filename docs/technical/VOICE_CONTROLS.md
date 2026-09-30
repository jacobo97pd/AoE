# Voice controls

Voice is an optional input layer on top of the touch, mouse and HUD controls. It understands Spanish and English. A spoken phrase becomes an intent and runs through the same handlers as the HUD buttons, so the rules validate a spoken order exactly as they validate a tapped one. Nothing in the simulation knows that voice exists.

Voice is **off by default**. The game never opens the microphone until the player turns voice on.

## Using it

Turn voice on in either place:

- **Settings → Voice control** (main menu or in-game Settings).
- The **VOICE** chip above the command bar. The first tap switches to push-to-talk and listens for one command.

| Mode | Behaviour |
| --- | --- |
| Off | The default. The microphone is never opened. |
| Push-to-talk (V) | Hold **V** and speak; listening stops 0.8 s after release. Tapping the chip listens for one command, for up to 6 s. |
| Always listening | Listens whenever the game has focus and the main menu is closed. Tapping the chip shows or hides the help list. |

**Voice language** has three values:

- **Auto** follows the system language: Spanish on a Spanish system, English otherwise.
- **Español**.
- **English**.

The chip shows the current state and the last phrase it heard. A heard phrase is green; one it could not understand is red and labelled "no entendido" / "not understood". Say "ayuda" / "help" to list examples above the chip.

"Here" means the ground under the mouse pointer. The screen centre is used instead when the pointer is over the HUD or there is no mouse.

## Commands

| What | Spanish | English |
| --- | --- | --- |
| Select | «selecciona el ejército», «trabajadores ociosos», «selecciona los lanceros», «selecciona el cuartel» | "select army", "idle workers", "select spearmen", "select the barracks" |
| Train | «entrena dos lanceros», «recluta un jinete» (counts 1–10; the recognizer offers 1–5) | "train two spearmen", "recruit a rider" |
| Build | «construye una casa», then «confirma» or «cancela» | "build a house", then "confirm" or "cancel" |
| Gather | «recolecta madera», «a por comida», «mina piedra», «entrega los recursos» | "gather wood", "collect food", "mine stone", "return cargo" |
| Army | «ataca aquí», «muévete aquí», «alto», «retirada», «cambia la formación» | "attack here", "move here", "stop", "retreat", "change formation" |
| Research | «investiga armas / armaduras / herramientas / tecnología de facción», «avanza de era» | "research weapons / armor / tools / faction technology", "advance age" |
| Camera | «cámara a la base», «cámara al ejército», «gira la cámara», «acércate», «aléjate» | "camera home", "camera to army", "rotate camera", "zoom in", "zoom out" |
| Game | «pausa», «reanuda», «ayuda», «cierra la ayuda» | "pause", "resume", "help", "close help" |

Units and buildings use everyday names in each language, as well as the in-game names. For example, *reedguard* is «lancero» / «piquero» and *spearman*; *muster hall* is «cuartel» and *barracks*.

The vocabulary is built for each match. It only includes what the local faction can train, build and research in the map's realm. A historical match does not understand «entrena un draco».

### How orders are carried out

Each spoken order does what the equivalent HUD action does, with a few voice conveniences:

- **Selection**
  - «selecciona …» picks every owned unit of that kind, not just the visible ones.
  - Saying the same building again steps to the next building of that type.
  - The camera moves only when the chosen building is off screen.
- **Training** queues at every finished producer, shortest queue first. When there is none, the feedback names the missing building, for example "Necesitas un cuartel terminado…".
- **Building**
  - If no worker is selected, the nearest idle worker is chosen, or else the nearest worker.
  - The footprint previews at the pointer.
  - «confirma» keeps a valid spot the player tapped. Otherwise it re-previews at the pointer, then places the building.
- **Gathering** sends the selected workers, or else every idle worker, to the nearest source of that kind that the player can currently see.
- **Attacking**
  - Targets the nearest visible enemy within 6 m of the pointer; units come before buildings.
  - If there is none, the army advances to that point and engages on its own.
  - With no army selected, the whole army is used.
- **Retreat** moves the selection, or the whole army, to the map-centre side of the settlement's seat (the building that trains workers and takes deliveries).
- **Research** starts the first unfinished technology of the family. «avanza de era» starts the technology that leads to the next era.
- **While a menu is open**, only «pausa», «reanuda», «cancela», «ayuda» and «cierra la ayuda» work. Everything else answers "Hay un menú abierto…".
- **While the main menu is open**, voice stops listening.

## Architecture

| Part | Location | Responsibility |
| --- | --- | --- |
| `Emberfield.Voice` | `Assets/Game/Voice/` | An assembly with no Unity references, tested in EditMode. |
| `VoiceText` | Voice | Lower-cases text, strips accents (Unicode FormD) and punctuation, reads number words and digits (1–10), and tries regular plural forms. |
| `VoiceVocabulary` | Voice | Builds the per-match vocabulary: authored Spanish and English nouns (with gender and plural) plus the rules' display names, which are understood when typed but never offered to the recognizer. `Phrases()` lists every phrase for the recognizer together with the intent it stands for. |
| `VoiceCommandParser` | Voice | Turns a phrase into a `VoiceIntent`. It tries fixed commands, then verbs, removing courtesy words and articles and reading counts along the way. |
| `IVoiceRecognizer` | Presentation | The boundary to a recognizer: load a phrase list, start, stop, and receive the recognized text. |
| `WindowsVoiceRecognizer` | Presentation | Wraps `UnityEngine.Windows.Speech.KeywordRecognizer` at medium confidence. Loading only compiles the grammar; `Start` opens the microphone. Errors become a readable `Problem`, not an exception. |
| `ScriptedVoiceRecognizer` | Presentation | Delivers typed phrases as if heard. Like a keyword recognizer, it only accepts phrases from its list. Used by the tests and the smoke. |
| `VoiceControls` | Presentation, `MatchController.Voice` | Handles modes, push-to-talk and focus, queues recognized text, and runs each intent through the HUD handlers. |
| `VoicePanel` | Presentation, `MatchHud.VoicePanel` | The chip and help list, anchored above the command bar on the left, away from the minimap. |
| Settings | `AlphaSettings.VoiceMode`, `AlphaSettings.VoiceLanguage` | Persisted alongside the other local settings. Also exposed in the product shell and in-game Settings. |

Recognized text is queued and handled in `MatchController.Update`, after pointer input and before the simulation step.

## Privacy

- Voice is off until the player enables it.
- The microphone opens only while listening: V held, a chip tap, or always-listening mode.
- Windows' speech system matches the audio on this device against the fixed phrase list.
- The game records no audio and sends nothing.
- The only stored values are the two settings.

## Platform limits

- Needs Windows 10/11 (editor or standalone) with speech recognition available. On other platforms the chip is hidden and the settings are kept.
- The keyword recognizer uses the Windows speech language. Choose the matching voice language, or leave it on Auto: English phrases against a Spanish speech recognizer, or the reverse, recognize poorly.
- Windows privacy settings must allow desktop apps to use the microphone. If they do not, the chip reports the problem instead of failing silently.
- Recognition quality depends on the microphone and room noise. Always-listening mode can react to conversation that happens to match a short command such as «alto» or «para». Push-to-talk avoids this.

## Adding content

Every trainable unit and every building needs a spoken name in both language packs, in `VoiceLanguagePack` in `VoiceVocabulary.cs`. `VoiceControlsIntegrationTests.EveryShippedFactionHasSpokenNamesThatRoundTrip` fails if a shipped id only has its display name. The same test fails if any offered phrase parses to a different command than the one it was generated for.

## Verification

- EditMode: `VoiceCommandParserTests` covers normalization, fixed commands, counts and plurals, buildings, selection, gathering, research, rejection of unknown phrases, faction and realm filtering, and a round trip of every generated phrase.
- PlayMode: `VoiceControlsIntegrationTests` covers:
  - Spanish orders in an offline match: idle workers, gathering wood, training two workers, a missing producer, building, confirming and cancelling a house, and the help panel.
  - Pause and resume gating.
  - Push-to-talk and English.
  - Alias coverage for every shipped faction.
  - Windows compiling both phrase lists, without starting the microphone.
- Results on 2026-09-13:
  - EditMode passed 584/584, including 95 voice cases.
  - The 5 voice PlayMode tests passed. The whole PlayMode suite passed 194/196; the two failures belong to the unfinished reference-character work, not to voice.
  - Development build `9689d92b`: Windows compiled 364 Spanish and 361 English phrases, and all 11 scripted commands ran.
  - After that build, the feedback for «pausa» and «cierra la ayuda» was reworded. The PlayMode tests check the new text.
- Packaged smoke: `tools/Smoke-Voice.ps1`, after `tools/Build-PirateCrew.ps1 -PlayerOnly`. It runs the development player with `-emberfieldVoiceSmoke <folder> -emberfieldOffline Conquest`. It checks that Windows accepts both phrase lists, runs scripted Spanish and English commands, captures the chip, the build preview and the help list, and writes `voice-smoke.json`.
