# Faction realms review — 2026-09-14

## Backend implementation

New matches use three separate content realms. `Conquest` and `Dominion` remain victory modes within each realm; private rooms, casual queues and ranked queues all enforce the same content restrictions.

| Realm | Active persistent faction IDs | Map availability |
| --- | --- | --- |
| Historical | `aven` (Franceses), `serevin` (Hispanos), `english` (Ingleses) | Amber Crossing, Sapphire Coast, Sunscar Basin |
| Fantasy | `ashen` (Orcos), `drakeforged` (Enanos), `skeld` (Hombres de las montañas), `verdant` (Elfos) | Amber Crossing, Sapphire Coast, Sunscar Basin |
| Naval | `pirates` (Piratas) | Sapphire Coast only |

Naval currently uses the existing land battlefield on the Caribbean coast. Pirate starter workers become `treasure_seeker` after faction assignment and before authoritative World creation. The authoritative faction roster also rejects `tender` recruitment or injected spawns for Pirates, so human and AI recruitment use the pirate worker. Pirate mirrors are valid. No ships or naval movement are added. English navy, Spanish navy and skeleton fleet entries remain planned and are rejected by admission. `miraj` and `solar` remain readable legacy definitions/history, with no new room or queue admission.

Node checks exact string IDs, realm membership and map compatibility before creating or queuing a room. The C# authority independently calls `ContentRealms.IsPlayableFactionInRealm` and `IsMapAllowedInRealm`; the broader legacy-aware `IsFactionInRealm` is not used for authority admission. Omitted map selects Sapphire Coast for Naval and Amber Crossing for the other realms. Realm/map values supplied later with ready messages cannot replace the validated room selection. Matching continues to partition by realm, map, queue type and victory mode, including after widening the MMR window. An unmatched account stays queued; it is never paired across realms to fill an odd population.

`GET /v1/history/naval` joins the existing aggregate, historical and fantasy history routes. Ratings stay independent by realm/queue/mode, with visible rank separate from internal MMR. Command identity, sequence consumption, paid-command request-ID deduplication, filtered observations, server-owned outcomes and resource limits retain their existing rules.

## Persistence and compatibility

SQLite migration 3 fixes the former migration that would prefix a new `naval:` rating with `historical:` on restart. It migrates only the four recognized legacy `casual/ranked:Conquest/Dominion` keys, inside a transaction. Existing historical/fantasy/naval or unknown future keys are preserved. If a legacy key collides with an existing scoped row, both original rows are retained rather than overwriting a rating. Truly old match schemas receive historical/Amber Crossing defaults only for missing columns.

The existing `frostguard_bronze` cosmetic now belongs to fantasy, matching Skeld and Frostguard. Only its catalog realm changed: item ID, appearance, target and price remain unchanged, so existing entitlements and equipped rows need no migration. A regression test retains a purchase and equipment from the previous historical catalog, reopens the same scratch database with the corrected shipped catalog, and verifies continued ownership, re-equipping, rejection for an unentitled account, and appearance inclusion for a Skeld room but exclusion from historical/naval rooms. Cosmetic metadata is outside the competitive hash. The service caches its catalog at construction and freezes each room's equipment on admission; operators must restart the service to load the corrected metadata.

Tests reopen the same scratch database twice and compare complete rating rows, account credentials/ID, hashed session, entitlement/equipment state and historical results. Old historical `skeld`, historical `miraj` and fantasy `solar` records retain their original realm, faction and content version. Their history is not reclassified using the current roster. No production database was read or modified for these tests.

The reviewed Node active roster and map policy match C# `ContentRealms` and the shipped definitions. The worker matrix below exercises all eight active factions in both player slots, all legal map combinations and explicit legacy/planned/cross-realm rejection. Node and Unity use protocol 2 and a coordinated content pin. Changes to rules or pinned data require another coordinated pin/build; old measured runs keep their original identity.

## Backend evidence

The final full suite ran on the rebuilt authority after the pirate worker roster restriction and its constructor-order correction:

- Content: `frontiers-743b9c3322b4b25aa4d54d445b1ef742b12e926046d5812ef0715254a9f327c6`.
- Authority DLL SHA-256: `803354784e2c579c6a31d748e8872cfa6daeb80b35687568f85d65a053feffd4`.
- Full backend test suite: **38 passed, 0 failed/skipped**, **11.1433801 seconds**. This comprises 31 service/persistence cases with a controlled authority, 2 pool cases with controlled bridges, and 5 cases using real C# authority processes. [Final raw test output](../../TestResults/Faction-Realms-Online-20260914/server-tests-roster-recovery.txt).

Earlier evidence remains available under its original build identity:

- `frontiers-0ede09d9b6ce5db1d7a0a9f8481801cd285078ac804e3c458469d9c938339209`, authority `bfdc1ad71c196724ccfef995eab65fb2e33a6f702ed13c2ad1df2497b29501b9`: initial [37/37 suite, 10.0981071 seconds](../../TestResults/Faction-Realms-Online-20260914/server-tests.txt) and [initial 12-match load PASS](../../TestResults/Faction-Realms-Online-20260914/load-12/report.json).
- `frontiers-7f85ee522534d01e499789efba89308aacc5588ad989947aba82c8bb4479c9d9`, authority `34e36ee414fe7a8fb6d4cb66b5390e15bac3f87f6b10bdac828a39c0671d35a2`: [37/37 after display-text corrections](../../TestResults/Faction-Realms-Online-20260914/server-tests-final.txt), then [38/38 after adding the Frostguard catalog regression](../../TestResults/Faction-Realms-Online-20260914/server-tests-cosmetics-final.txt).
- `frontiers-35c27853d528b6640dc56cff3a710e16c99316a47f3a250d4d63756562028314`: **33 passed / 5 failed**, [failed run preserved](../../TestResults/Faction-Realms-Online-20260914/server-tests-roster-final.txt). The new collection roster gate queried `Player(owner)` during World construction before players were created. Every real-authority creation case failed. The corrected gate resolves the faction from the already initialized, copied `factionCatalog.Assignments`; the final suite above passes with that correction. No production restart was performed by this validation task.

The real content matrix creates **22 Worlds**: nine historical faction/map combinations, twelve fantasy combinations and one naval mirror. Each checks both recipients' faction/realm/map/biome observations and submits an accepted worker recruitment order for each seat. Naval starts contain treasure seekers. Both pirate seats first attempt to recruit a Tender: authority rejection must leave resources, reserved population and the production queue unchanged. A subsequent Treasure Seeker recruitment succeeds. Fourteen invalid creation inputs cover mixed factions, legacy and planned factions, unknown content, traversal-like map input and both forbidden naval maps. Other real cases exercise process failure isolation, reconnect, filtered state, authoritative ranked results and paid-command deduplication.

The full suite was run from the project root with:

```powershell
node --experimental-sqlite --test Server/tests/*.test.mjs
```

## Concurrent validation

A fresh loopback service, scratch SQLite database and **two authority workers with eight slots each** hosted **12 simultaneous matches / 24 HTTP clients**: four historical, four fantasy and four naval. This final rerun uses the `frontiers-743b9c...` content and authority identity recorded above, including the pirate worker restriction. The client driver is a separate Node process. It rotates active factions, legal maps, private/casual/ranked flows and both victory modes. Provisioning uses the real authentication implementation before timing; reconnects use HTTP authentication and quotas are unchanged.

Command:

```powershell
node --experimental-sqlite tools/online-load.mjs --matches 12 --seconds 30 --output TestResults/Faction-Realms-Online-20260914/load-12-final
```

Result: **PASS**. [Final raw report](../../TestResults/Faction-Realms-Online-20260914/load-12-final/report.json).

| Measurement | Observed result |
| --- | --- |
| Timed gameplay duration | 30.0176239 seconds |
| Total HTTP requests, including setup/final checks | 4,307 |
| Validated observations | 3,448 |
| Accepted commands | 721 |
| Intentionally rejected gameplay commands | 2: foreign-owned unit and repeated paid request ID |
| Unexpected errors / connection-limit drops | 0 / 0 |
| Observed simulation rate across 12 matches | Mean 20.0101 Hz; minimum 19.9776 Hz |
| Snapshot HTTP latency, including final checks | p95 31.9767 ms; p99 100.9182 ms; maximum 1,192.7112 ms |
| Command HTTP latency | p95 29.2899 ms; p99 61.3565 ms; maximum 72.3436 ms |
| Re-login with same match, slot and consumed cursor | 2 successful |
| Completed participant histories | 24 verified |
| Replacement match after terminal worker release | Passed |
| Node service peak RSS | 181,710,848 bytes; excludes worker/client process memory |
| Peak concurrent HTTP requests | 24 |
| Service event-loop delay | p95 34.111487 ms; maximum 1,067.450367 ms |

The timed workload polls at five observations per second and attempts one movement/economy/production order per second per connected client. Two clients stop communicating for 20 seconds, from seconds 4 to 24 in this shorter run; peers observe the disconnected status before HTTP re-login. Final checks surrender each match, verify recipient histories and start a replacement match after authority slots are released. The driver also rejects an old command sequence and a forged HTTP result submission. All scratch service/worker processes closed after completion; the public alpha and its database were not restarted or modified.

This is a short same-machine HTTP test with initial armies, not an Internet, physical-device, sustained-load, late-game or balance result. Background development work on the same machine was not isolated. The maximum snapshot response exceeded one second during final checks despite the run completing without errors; the raw report keeps both timed-gameplay and full-run distributions. Snapshot HTTP latency is not a measurement of Unity render latency or displayed animation smoothness. Existing 32/64-match measurements, including the previously recorded transient failure, remain in the [earlier load report](online-load-report.md) under their own content hashes; they are not claimed as reruns of this roster.

## Unity and packaged verification

Unity 6000.3.23f1 ran the simulation and presentation checks against the updated roster:

- **37/37 EditMode checks passed**, covering expansion rules and the pirate hero's restrictions, reservation limits and recruitment. [XML](../../TestResults/Faction-Realms-Unity-20260914/editmode-final.xml).
- The combined PlayMode run passed **126/127** cases. The remaining collection-navigation case exposed a missing `CharacterCollection` entry in Editor Build Settings. The scene was registered in both the project and test project; the subsequent navigation run passed **7/7**. [Combined run](../../TestResults/Faction-Realms-Unity-20260914/playmode.xml), [navigation after correction](../../TestResults/Faction-Realms-Unity-20260914/navigation-final.xml).
- Passing presentation checks cover the eight active faction starts, pirate mirror replicas, planned/retired choices, odd-sized selectors, realm/map restrictions, HTTP choice persistence, history retries, imported pirate/model membership and generic human geometry without dwarven equipment.

The first EditMode compilation found a malformed test fixture introduced during the faction migration; its stray definition in the player-assignment array was removed before the passing run. Failure logs remain in `D:/CodexTooling/faction-realms`.

The local alpha startup also exposed a Windows process-inspection race: `Start-Process` briefly returned no executable path. `Get-OnlineIdentity` now retries for up to two seconds while preserving the original PID and start timestamp, and fails if identity cannot be established. The ownership checks used by stop/restart remain strict. `tools/Test-OnlineIdentity.ps1` passed four checks, including missing-path retry and PID reuse rejection. No unrelated processes were terminated. The persistent database was backed up under `D:/EmberfieldOnline/backups/pre-faction-realms-20260914` before applying the realm migration.

The Windows player completed with zero build errors or warnings in 82.667 seconds (reported output size 819,968,315 bytes). Build GUID: `23bb857c791a4d0395f7a5a91dab4fd7`. The final isolated-source selector guard passed **1/1**: [XML](../../TestResults/Faction-Realms-Unity-20260914/selector-isolated.xml). No FPS or finished national-art claim follows from these functional checks.


## Fuentes de esta compilación

El 14 de septiembre de 2026 se preparó una copia de fuentes en `D:/EmberfieldWorkingCache/AoE/FactionGroupsSourceFreeze` para compilar la separación de facciones mientras otro trabajo modificaba la traducción de la interfaz en el directorio compartido. El manifiesto se creó a las `2026-09-13T22:48:35.420019+00:00` (00:48 del 14 de septiembre en Madrid).

El [manifiesto publicado](../../Artifacts/ArtReview/faction-groups/source-freeze-manifest.json) contiene **510 archivos y 2.999.117 bytes**, con ruta, tamaño y SHA-256 de cada fuente original y de su copia. No contiene el código fuente completo, contraseñas, credenciales ni datos de sesiones. Su publicación conserva exactamente el manifiesto de la copia utilizada para compilar.

Se copiaron Simulation, Networking, Presentation, Diagnostics, Editor, Voice y Tests. La copia conserva la separación de facciones, los piratas, las restricciones de trabajadores, los cambios online, los controles por voz y sus pruebas. Mantiene también la corrección del identificador de escena. Los archivos del proyecto compartido no se revirtieron.

Se excluyeron `Assets/Game/Localization` y los dos archivos `UiLocalization.cs`/`.meta`, porque los catálogos de traducción todavía no estaban disponibles al tomar la copia. Dentro de ella se adaptaron únicamente:

- `AlphaControls.cs` y `AlphaPanel.cs`: se retiraron la conexión y el botón del cambio de idioma incompleto; se conservaron controles y ajustes de voz.
- Los archivos de ensamblado de Presentation, Tests/EditMode y Tests/PlayMode: se retiró exclusivamente la referencia a `Emberfield.Localization`, conservando `Emberfield.Voice` y las demás referencias.

`AlphaSettings.Language` y su normalización permanecen para conservar la compatibilidad de los ajustes. El manifiesto informa que no detectó cambios concurrentes durante la captura, que las copias sin adaptación coinciden con sus originales y que no quedaron referencias activas al módulo de traducción excluido.

Esta es una copia ligera de fuentes, no un archivo independiente del proyecto Unity completo. El coordinador añadió posteriormente los enlaces al arte, Library y la configuración necesarios para ejecutar pruebas y compilar. Esos recursos siguen compartidos; el manifiesto identifica las fuentes copiadas, no garantiza por sí solo que todos los recursos externos sean inmutables.

## Presentación revisada

La interfaz de esta copia conserva texto en español e inglés. El cambio global de idioma de la tarea paralela no forma parte de esta compilación. Los nombres nuevos de facciones y los estados de disponibilidad sí están incluidos.

Se inspeccionaron las tres capturas nativas a **1280 × 720**: los títulos y las descripciones de las tarjetas son legibles y no presentan recortes. Otras resoluciones no se han inspeccionado visualmente en esta entrega.

La [página de entrega](../../Artifacts/ArtReview/faction-groups/index.html) incluye las capturas [Históricas](../../Artifacts/ArtReview/faction-groups/screenshots/historical.png), [Fantasía](../../Artifacts/ArtReview/faction-groups/screenshots/fantasy.png) y [Navales](../../Artifacts/ArtReview/faction-groups/screenshots/naval.png). Los resultados funcionales y online se documentan a continuación.


## Windows package and native checks

The [Windows ZIP](../../Artifacts/ArtReview/faction-groups/Emberfield-Windows.zip) contains **181 entries**, including the player executable, UnityPlayer.dll, game data and a short README. Debug symbols and DoNotShip output are excluded. Size: **390,571,886 bytes** (372.48 MiB).

SHA-256: `a8e38e2211ef8462e7f02564aa8e404a7923aafa717279ffa661a7c9e292c8db`.

The packaging script passed CRC verification for every entry and compared every packaged player file with its source SHA-256. It also checked that the source file list, sizes and modification times stayed stable during packaging. [Package verification](../../Artifacts/ArtReview/faction-groups/package-verification.json). Package integrity is separate from gameplay and network validation.

All three native HTTPS checks passed on this exact Windows build and the `frontiers-743b9c...` content pin:

| Realm | Native result | Duration | Evidence |
| --- | --- | --- | --- |
| Historical | PASS, two standalone clients | 40.03 s | [Summary](../../TestResults/Faction-Realms-HTTPS-historical-20260914/summary.json) |
| Fantasy | PASS, two standalone clients | 39.96 s | [Summary](../../TestResults/Faction-Realms-HTTPS-fantasy-20260914/summary.json) |
| Naval | PASS, two standalone clients | 35.20 s | [Summary](../../TestResults/Faction-Realms-HTTPS-naval-20260914/summary.json) |

All three probes used the public HTTPS endpoint, verified the expected realm/map and starter roster, killed and restarted the guest client, and checked match history. The naval case verified both pirate seats started and recruited Treasure Seekers, and that planned factions were locked. The clients ran on the same computer without simulation acceleration. The runner did not inspect the external service binary or its database; `RealHttpAndSqlite: false` describes that inspection limit rather than a claim of independent runtime verification. These are not tests across independent physical networks or production-capacity certification.

The native expansion and selector run also **passed for all eight active factions**, using **76 visible button clicks at 1280 × 720**. Catalog previews preserved the active world. Each case started with its original economy and developed through ordinary AI commands; accelerated simulation in this run is not an FPS measurement. [Native report](../../TestResults/ExpansionProduct-1280x720-20260913-225626/expansion-smoke.json), [copy included in the review](../../Artifacts/ArtReview/faction-groups/frontend-validation.json), [build summary](../../Artifacts/ArtReview/faction-groups/build-summary.txt).

The source snapshot intentionally excludes the translation work that was incomplete when captured. Translation catalogs subsequently appeared in the shared workspace; their later availability does not change the sources already compiled into this ZIP.
