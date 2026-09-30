# Phase 7 — full offline match

**Status: implemented; final Unity verification, Windows build and all packaged-player checks passed.** Amber Crossing adds a complete offline 1v1 with faction choice, finite resources, fog, strategic AI, Conquest/Dominion objectives, pause, surrender and results. Final combined verification passed **347 EditMode + 66 PlayMode tests (413 total)**, including the 11 focused AI cases, and all four natural AI matches passed. Initial Unity matches exposed short pacing and ineffective Dominion play; the final natural-match results below verify the revised policy while retaining the pacing and balance limitations.

## Baseline and environment

Work continues from Phase 6 commit `549fed4`. Unity **6000.3.23f1**, URP 17.3.0, Input System 1.20.0, Test Framework 1.6.0 and uGUI 2.0.0 remain in use. No editor, package or platform module was installed for this phase. The [initial audit](../audits/initial-project-audit.md) records the editor fallback and absent mobile modules.

Verification uses the same Windows desktop with Intel i5-10400F, approximately 16 GB RAM and NVIDIA GeForce RTX 3060 Ti. No physical mobile performance, touch session or signed mobile build is claimed. The previous [Phase 6 report](PHASE_6_REPORT.md) remains the historical checkpoint for the faction drills and earlier evidence.

## Implemented behavior

The offline chooser starts a fresh **96×72-metre Amber Crossing** map as Aven Compact or Serevin March, with the other faction controlled by the AI. Both sides begin with **four Tenders, one Hearth, 120 Food / 80 Wood / 0 Metal / 40 Stone**, and population **4 / 6**. There are no prebuilt military or research facilities. Starting positions, terrain and finite resource deposits are mirrored, with further deposits toward the crossing. The existing unit balance and global starting budget are unchanged.

Conquest requires destroying every opposing Hearth, including unfinished replacement foundations, while preserving at least one of yours. Dominion provides three public beacons: military presence captures an uncontested zone over **15 seconds**, and a continuous majority of **two of three for eight minutes** wins. Both armies in a zone contest it; an enemy's uncontested recapture also interrupts the previous owner's hold. Empty zones retain their owner, but empty or contested zones reset unfinished capture progress. Conquest remains a valid backup victory in Dominion gameplay. Simultaneous elimination produces an explicit draw; the rules also handle simultaneous hold completion under configurable control thresholds.

Unexplored terrain is concealed; previously explored terrain remains known. Owned units and buildings are always observable. Opposing units, buildings and resource nodes require current visibility. The authoritative command boundary rejects attacks and gathering against unseen targets and placement outside current vision. Presentation hides unseen enemy/resource models and projectiles, and rejects selection or contextual orders through the fog. Public beacon positions and clocks remain visible by design. Under fog, defeat feedback is limited to owned losses: a previous enemy view position cannot prove that a death between presentation updates was visible. This deliberately omits enemy defeat cues rather than revealing hidden deaths.

The offline panel supports faction/mode choice, pause/resume, confirmed surrender and result/restart flow. World commands and simulation ticks stop after a result. Focus loss and application-pause callbacks pause an active match and cancel pending input. Existing neutral drills opt out through `OfflineMatchDefinition.Enabled = false`; the new match explicitly opts in. This flag also avoids Unity's JSON deserializer materializing an omitted configuration object into an accidentally active match.

## Honest AI and architecture

`OfflineAi` uses the normal `World.Submit` boundary. It pays the same costs, physically gathers from finite nodes, constructs prerequisites, reserves population, trains units and completes research. Its controller runs before `World.Tick`, thinks at most once every **10 ticks**, and submits at most **four commands per think**. It stops at the authoritative match result. No resource grants, altered unit stats, forced deaths, hidden production inspection or scripted winner are used.

`OfflineAiObservation` is the only opponent/resource observation layer. It checks visibility before reading live enemy position or stats. Hidden records retain their last observed location and information; a scout must return before a remembered resource can receive another gather order. A last-seen enemy location is forgotten when it is visible and empty, without following the enemy's hidden live state. The AI may inspect its own economy, authored definitions, public terrain geometry and public objective state.

The economy develops toward twelve workers, adds housing and military/research facilities, expands drop-off access and allocates workers according to resource needs. Planned research reserves its cost and producer so continuous recruitment does not consume every incoming food delivery. Army composition responds to currently observed tags and the ordinary counter definitions. Low-health troops can retreat through normal movement; eligible Ashrunners may use their paid unit's existing Reposition action.

Conquest separates scouting and local defence from a coordinated capital assault. The current commitment requires 26 ready troops, ten workers, Empire and eight completed technologies; it is a development condition, with no elapsed-time wait. Dominion assigns defenders and its main force to public beacons, fights nearby threats and returns to the objective when an enemy leaves the area. It does not abandon a beacon to chase a distant visible Hearth.

The simulation remains Unity-free. Tick order is **Vision refresh → faction bonus refresh → Movement → Vision/bonus refresh → Economy → Construction → Vision refresh → Combat → Faction actions → Production → Research → bonus/Vision refresh → TickIndex increment → Match objectives/result**. Fog also refreshes at relevant command and combat boundaries. AI is called before this tick by presentation or the headless harness. Match and fog are opt-in, preserving legacy fixtures.

## Natural-match diagnostics and tuning

The repeatable editor entry is `Emberfield.Editor.OfflineMatchVerification.Run`. It loads the shipped rules and map, runs AI against AI in both ownership assignments for each victory mode, alternates which AI submits first every ten ticks, and writes `TestResults/offline-matches.json` and `.md`. The default deadline is **1,800 simulated seconds per case**. A timeout fails the check; it does not manufacture a draw. The Dominion verification specifically requires a beacon-hold result, while gameplay retains its Conquest backup. The packaged flow driver submits the local AI first, then the rival; its outcomes are reported separately because this order can change command interactions and match duration.

Reports record rules/map SHA256, faction assignment, result, ticks, deaths, captured-objective changes, progression, accepted/rejected commands, spending, deposits and peak continuous majority hold. Deposits are final stock plus recorded purchases minus starting stock, excluding cargo still in transit. A final-state fingerprint identifies that run; it is not a cross-platform determinism guarantee. Ticks are accelerated and runner wall time is not frame-rate or human playtime evidence.

The [initial Unity outcomes](../testing/evidence/phase7-offline-before-tuning.md) are retained as tuning evidence. All four matches ended through Conquest, with **zero beacon ownership changes**. The old priority chased visible enemies before managing objectives, and persistent military purchases starved advancement while workers accumulated less-needed resources. These completed games did not establish acceptable Dominion behaviour or the proposed match pacing.

| Mode / Player 1 faction | Initial Unity outcome | Final Unity result | Interpretation |
| --- | --- | --- | --- |
| Conquest / Aven | Conquest, 289.20 s | Conquest, **419.80 s**, Player 1 | Development and coordinated assault extend the match to about 7 minutes |
| Conquest / Serevin | Conquest, 237.85 s | Conquest, **609.20 s**, Player 2 | About 10.2 minutes; the proposed 12–20-minute target remains unmet |
| Dominion / Aven | Conquest, 232.30 s | Dominion, **954.05 s**, Player 1 | Three ownership changes and a complete 9,600-tick majority hold |
| Dominion / Serevin | Conquest, 423.60 s | Dominion, **1,127.00 s**, Player 2 | Three ownership changes and a complete 9,600-tick majority hold |

Bundled Mono diagnostics using the actual JSON first reproduced the initial Unity outcomes, then tested the revised policy. The final four-case Unity run reproduced all four revised times and outcomes, including actual Dominion victories. Rules SHA256 is `6F100B25035E54E4144761F51885F52E45051EBD6562EF6024AFC37601CB63C2`; map SHA256 is `3EF7E44A71AE3C76C74C3DC04BD0BA7E8EFE43AA0BDC500AD2450487D493D072`. The tracked [natural-match report](../testing/evidence/phase7-offline-matches.md) and [complete JSON](../testing/evidence/phase7-offline-matches.json) retain the observed outcomes, progression and fingerprints.

Both revised ownership assignments favour Aven in these deterministic fixtures. This is a visible balance concern, not a faction win-rate estimate. The sample has one map and one policy per side, without varied openings or human opponents. The 12–20-minute target is aspirational; no artificial timer, global Hearth buff or forced victory was added to meet it. Human Checkpoint 3's assessment of fun, clarity and pacing remains pending.

## Tests and verification status

| Check | Current evidence | Status |
| --- | --- | --- |
| Focused AI rules | **11/11 passed in final Unity EditMode**, also checked in direct Mono | Pass; included in the EditMode total |
| Final Unity EditMode / PlayMode | **347/347 + 66/66 = 413 passed**; [EditMode XML](../testing/evidence/phase10-editmode.xml), [PlayMode XML](../testing/evidence/phase10-playmode.xml) | Pass on frozen final source |
| Common/faction counter probes | [12 common](../testing/evidence/phase10-combat-balance.md) + [8 faction](../testing/evidence/phase10-faction-counters.md) | Pass; controlled counters, not full-match balance |
| Movement regression matrix | [30 cases](../testing/evidence/phase10-movement.md) | Required gates passed; dense-choke limits retained |
| Natural AI matches | [4/4 final Unity cases passed](../testing/evidence/phase7-offline-matches.md), matching the revised diagnostics | Verified natural Conquest and Dominion |
| Final Windows build | [Build summary](../testing/evidence/phase10-build.txt); **163,954,032 bytes**, **13.820391 s**, zero errors/warnings | Pass; GUID `d1b7b1cbc28740e994945476895e1295` |
| Final packaged offline flow | Eight offline cases in the [19/19 standalone matrix](../testing/evidence/phase10-player-matrix.md) | Pass on the final executable |

Focused AI tests cover opt-in/player requirements, bounded order cadence, exact spending without grants, physical harvesting before purchases, hidden-information independence, last-seen memory, owned actor scope, visible command targets, finished-match freeze, research progress, reserved research funds and beacon priority over a visible opposing Hearth. Match/fog tests and PlayMode integration are maintained independently so AI tests do not substitute for authoritative rule or UI checks.

The first PlayMode run exposed old maps activating offline rules because an omitted JSON object was materialized with defaults. The explicit `Enabled` guard and full-match opt-in correct that compatibility issue, and the final suite includes its regression. Further presentation regressions cover hidden Outpost relocation, completion while hidden, first observation of depleted resources and a real projectile killing an enemy after it leaves sight between view updates. The last case verifies that stale visual state emits no hidden defeat cue. The five Phase 10 fog oracle cases compare every cell against an independent source-union predicate, including source creation, death and same-ID packing/redeployment. The final 66-case PlayMode pass also includes two current-selection army role tests; no assertions were weakened.

## Build, performance and known limits

The final Windows build has GUID **`d1b7b1cbc28740e994945476895e1295`**, size **163,954,032 bytes**, build duration **13.820391 seconds** and **zero errors/warnings**. The [final functional matrix](../testing/evidence/phase10-player-matrix.md) passed **19/19 checks**: eight offline, four faction, two technology, two combat, one economy and two art-gallery cases. Evidence checks verified fresh timestamps, emitted build GUIDs, delivered assembly hashes, captures and clean runtime/shader logs. Combat/economy reports lack an emitted GUID; fresh timestamps and the unchanged delivered assemblies identify those three cases.

Each offline case used unmodified starting resources and ordinary commands, checked hidden initial enemy state, exercised Menu/Resume, reached a natural result, verified finished-state freeze and restarted to a fresh world. Both viewport runs agree:

| Local faction / mode | Winner / simulated seconds | 1280×720 | 1440×1080 |
| --- | --- | --- | --- |
| Aven / Conquest | Player 1 / **419.80 s** | [Report](../testing/evidence/phase7-offline-aven-Conquest-1280x720.txt) | [Report](../testing/evidence/phase7-offline-aven-Conquest-1440x1080.txt) |
| Serevin / Conquest | Player 2 / **609.20 s** | [Report](../testing/evidence/phase7-offline-serevin-Conquest-1280x720.txt) | [Report](../testing/evidence/phase7-offline-serevin-Conquest-1440x1080.txt) |
| Aven / Dominion | Player 1 / **959.90 s** | [Report](../testing/evidence/phase7-offline-aven-Dominion-1280x720.txt) | [Report](../testing/evidence/phase7-offline-aven-Dominion-1440x1080.txt) |
| Serevin / Dominion | Player 2 / **1,066.65 s** | [Report](../testing/evidence/phase7-offline-serevin-Dominion-1280x720.txt) | [Report](../testing/evidence/phase7-offline-serevin-Dominion-1440x1080.txt) |

These local-first packaged runs are separate from the alternating-order headless results above. They reinforce the observed Aven advantage in this limited policy/map sample, without establishing a faction win rate. The scenario records two visible Menu/Resume button dispatches before the separate Play again action; local play is driven by AI and ticks are accelerated.

Final capture inspection confirmed that fog and public beacon labels render before the opaque UI, including the result panel. The final suite verifies the hidden-defeat feedback boundary and current-selection role filters. [Phase 8's capture table](PHASE_8_REPORT.md#packaged-captures) records both viewport sizes. Screenshots, scripted button dispatches and accelerated smoke durations are not physical mouse/touch play, rendered-performance or ordinary match-duration evidence.

Phase 4's offscreen rendering measurements and Phase 6's neutral movement CPU results remain historical. Representative offline CPU measurements and the final packaged rendering comparison are documented in the [Phase 10 report](PHASE_10_REPORT.md), with separate measurement scopes and capability checks. They do not establish physical-mobile performance or ordinary display latency. Dense NarrowChoke congestion at 200/300/500 movers remains known debt; the final movement rerun retains **31/52/37 arrivals** at its deadline and does not claim those cases pass full arrival.

The AI is a bounded heuristic opponent, without difficulty tiers, learned strategy or competitive balance validation. Four deterministic AI-versus-AI runs establish legal progression and natural outcomes, not human fun or a statistically meaningful faction comparison. The bounded Aven art slice is documented separately in [Phase 9](PHASE_9_REPORT.md); it is not a complete final-art library. Save/load, online play and physical-device ergonomics remain outside the completed evidence.

## Files created and modified

| Area | Main changes |
| --- | --- |
| Simulation | New offline match definitions/state, fog system, `OfflineAi` and `OfflineAiObservation`; opt-in activation and authoritative fog/result command guards |
| Map | `Assets/Game/Resources/Maps/amber_crossing.json` |
| Presentation | `OfflineControls`, `OfflinePanel`, `OfflineHud`, `FogView`, `OfflinePlayerSmoke`; controller, input, selection, view and HUD integration |
| Verification | `OfflineMatchTests`/factory, `OfflineAiTests`/factory, `OfflineMatchIntegrationTests`, `FogPresentationRegressionTests`, `OfflineMatchVerification`; later `FogOracleTests` |
| Documentation | This report and linked frozen-suite/natural-match evidence |

Unity metadata accompanies imported assets. Build output and scratch Mono diagnostics remain ignored. No remote service, paid package, online backend or deployment is required for the offline match.

## Remaining checkpoint

Final Unity suites, the corrected Windows build, all 19 standalone checks and capture inspection are complete. The explicit pacing and faction-policy limitations remain. Human Checkpoint 3 still requires a real match assessment before claiming the game is fun; automated natural outcomes cannot supply that judgement. Physical-device testing remains outstanding as described in [Phase 8](PHASE_8_REPORT.md).
