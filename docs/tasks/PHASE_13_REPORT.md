# Phase 13 — alpha productization

Status: **local alpha engineering complete and verified on Windows**. This phase was authorized together with Phases 11–12 by the request to continue all remaining phases. Physical mobile, WAN, human gameplay and production service qualification remain acceptance work; they are not implied by the local results below.

## Settings and onboarding

The Settings & Guide panel exposes persistent sound on/off, sound volume, camera pan speed, zoom speed, drag tolerance and high contrast text. Preferences use a versioned, bounded JSON file in the application's local data directory (`Alpha/settings.json`). Missing or invalid files fall back to defaults; numeric values are clamped. A failed write is reported in the panel instead of claiming the preference was saved. Restore defaults also clears tutorial progress and disables local reports.

The interactive guide observes nine real activities: camera movement, owned-unit selection, an accepted move followed by physical movement, worker delivery, completed construction, completed unit production, a validated attack order, completed Era advancement, and a local victory. It never grants resources, creates units, changes costs, issues orders or produces a synthetic victory. The attack milestone confirms a validated attack order; it does not claim the attacker dealt damage. A delivery requires carried cargo to clear and the corresponding player resource stock to rise.

Completed steps persist across scene reloads and matches. Practice teaches the economy and controls; the victory step explicitly directs the player into an actual AI match. Start again clears guide progress; Pause hides the guide while preserving completed steps; Resume restores it. The hint is nonmodal and does not intercept world input. This is a playable onboarding foundation, with human comprehension and touch comfort still requiring validation.

## Local reports and error capture

Reports are **off by default** and require explicit opt-in. The panel explains what is collected before enabling it. Storage is bounded to the latest 128 typed records: event category, UTC timestamp, match tick and an integer count. Unity error, exception and assertion callbacks are reduced to categories and rate limited; raw messages and stack traces are discarded. No account name, password, token, device identifier, endpoint, chat or arbitrary text enters the record schema. Nothing is uploaded.

One local reporting session survives scene reloads. A marker records that a session began; normal application exit clears it. A remaining marker becomes `PreviousSessionInterrupted` on the next opted-in launch. This detects an unclean shutdown and **does not prove a native crash**. Native crash dumps and a hosted crash service are not supplied by this local foundation.

Delete reports removes stored records and the previous export. Disable reports stops collection and removes these files and the marker. Export writes a fixed `diagnostics-export.json` snapshot; on Windows, Open report folder selects that file for the user. Exporting does not transmit the file. Android sharing integration and physical-device verification are not claimed.

## Integration and validation

Source: `AlphaSettings.cs`, `AlphaDiagnostics.cs`, `AlphaTutorial.cs`, `AlphaControls.cs`, `AlphaPanel.cs`, `AlphaTextContrast.cs` in `Assets/Game/Presentation`. Tests: `Assets/Tests/PlayMode/AlphaProductTests.cs`.

The new tests exercise preference persistence and corrupt/schema-mismatched files, numeric bounds, explicit diagnostic consent, bounded retention, revocation/export deletion, clean versus interrupted sessions, rejection of invalid record fields, actual camera/selection/movement, gathering delivery, paid construction and production, paid Era research, validated attack, and winner-only tutorial completion when surrender ends a match without advancing its tick. They use the shipped rules for economy milestones, without grants.

The guide is disabled during online matches; completed steps stay saved for the next Practice or offline match. Opening Settings during an online match does not pause the authority. The standard audio menu uses the same persistent preference as Settings. Camera pan and zoom speeds are applied through presentation properties; pinch keeps its ground anchor.

Final Unity verification passed **511/511 tests**, with zero failures or skips: [423 EditMode tests](../testing/evidence/phase13-editmode.xml) and [88 PlayMode tests](../testing/evidence/phase13-playmode.xml). The PlayMode total includes 12 Alpha product cases and 10 online UI/settings cases. The latter cover the connection/address boundary and actual settings/modal integration alongside the product tests described above.

The complete verification also passed 12 common combat cases, 8 faction counter cases, 4 natural offline matches and all required gates of the 30-case movement matrix. The movement result retains the previously documented dense-choke throughput limits; it does not claim that every congested army arrives within the observation window. The Node service/authority suite passed **16/16**: 13 controlled service cases using real HTTP/SQLite/crypto with a test authority, plus 3 cases using the compiled real simulation worker. Its scope and behavior are documented in the [Phase 12 report](PHASE_12_REPORT.md).

The [final Windows build](../testing/evidence/phase13-build.txt) succeeded with **0 errors, 0 warnings**, 164,137,326 bytes and a duration of 14.086756 seconds. Its build GUID is `cdf8183365404135975644fd3c935a58`; the packaged checks below used this same build. Seven additional packaged regressions passed: two offline matches, both faction drills, technology, combat and economy. The [player verification matrix](../testing/evidence/phase13-player-matrix.md) records the combined distribution checks.

## Reconnection, inactivity and match statistics

Online clients recover their active account seat and accepted-command cursor through sign-in and the current server snapshot. They remain read-only simulation replicas: opening menus or losing a client process cannot pause the authority. The packaged test below verifies a real guest-process termination and fresh-process sign-in, with the same match and a subsequent accepted move.

The service marks a peer disconnected after 15 seconds without a heartbeat and allows a further 60-second reconnect grace. A sole player exceeding that combined absence loses by disconnect; both absent players cause an aborted match. Three minutes without an accepted command triggers AFK handling; a sole inactive player forfeits, while both inactive players abort the match. The client shows a warning during the last minute. These timeout/outcome branches are verified by controlled service-clock tests, separate from the packaged process-reconnect check. Service/authority restart aborts active Worlds and retains completed history; it does not reconstruct an in-progress authoritative World.

Surrender uses a two-step client confirmation and a server-owned result. Post-match history includes each player's own final Era, workers, army, buildings, resource stock and completed-technology count. The service persists the result and statistics with the participant history. These are final-state statistics, not a claim of a complete resource-production or combat-event timeline.

## Local verification and build pipeline

`tools/Verify-Alpha.ps1 -Stage All` serially compiles the standalone authority, runs the Node HTTP/SQLite tests, runs the complete existing Unity verification suite, builds the Windows player, and runs the two-client online smoke described below. It uses installed tools only; it does not install dependencies, publish a build, configure hosting, or upload data. Unity's existing project-lock and bounded process handling remain in effect.

`-Stage Services`, `Unity`, or `Build` executes only that portion and marks its manifest as a partial run. `-Stage Manifest` only inventories files and explicitly reports `InventoryOnly`; it does not present existing artifacts as newly verified. The output is `TestResults/alpha-release-manifest.json`, with the current revision/dirty state, an allowlisted source fingerprint, per-step outcome and SHA256 hashes for player/authority/test artifacts. Server databases, environment files, accounts, local diagnostics, machine identifiers and credentials are excluded. Existing artifacts carry file timestamps so a partial run cannot silently imply they were rebuilt.

The wrapper's PowerShell syntax was checked, and its Services and Build stages executed successfully. All constituent verification/build/smoke checks were run, but the composed `-Stage All` command was **not run as a single invocation**. The final manifest is explicitly `InventoryOnly`: it inventories the final files and their hashes, while the linked test/build/player reports supply the verification evidence. This distinction prevents the manifest from claiming a fresh combined pipeline execution.

`tools/Smoke-Online.ps1` starts a fresh local HTTP service/SQLite database and two real Windows player processes. The development-only `OnlinePlayerSmoke` driver uses native sign-in, room, ready and surrender UI plus the shipped online controls/service. It requests movement, gathering and training and waits for authoritative snapshots to prove the resulting movement, delivered stock and new worker. The runner then kills the guest process, waits until the host sees the missing connection, and starts a fresh guest process. The guest signs in again, recovers the same match/command sequence and issues another ordinary move. The host then surrenders; both clients verify their local winner perspective and the persisted server history/statistics. This deliberately tests a surrender result rather than claiming a naturally played human victory.

The final two-client runs both passed:

| Resolution | Wall time | Guest tick before termination → restored snapshot | Final authority tick | Evidence |
|---|---:|---:|---:|---|
| 1280 × 720 | 30.04 s | 151 → 520 | 534 | [Native two-client report](../testing/evidence/phase13-online-1280.json) |
| 1440 × 1080 | 30.30 s | 150 → 518 | 533 | [Native two-client report](../testing/evidence/phase13-online-1440.json) |

Each run produced **19 PNG captures and 9 native button dispatches**. Both clients showed the real Settings panel and verified that it paused local Practice before sign-in. Their ordinary move, paid Tender training and delivered Wood appeared only through server snapshots. The host observed the missing guest and required 20 further authority ticks while that process was still stopped before the runner could relaunch it. The restored guest recovered its prior trained worker and command cursor and issued the next accepted move. Host surrender then produced reciprocal loss/win views and persisted history with each player's authoritative final statistics.

Screenshots show the actual UI and observed replica at Settings/login/lobby/gameplay/disconnect/reconnect/result/history. Config files contain only disposable test credentials under ignored `TestResults` and were deleted after each run. Reports contain no passwords or session tokens. The runner also verified that the processes it launched stopped. Checkpoint files use atomic replacement with unique temporaries and bounded retries for transient Windows sharing violations; a failed checkpoint records a sanitized operation/HResult/method context rather than hiding the failed evidence write. Default smoke timeout is 120 seconds. The driver never advances server ticks, creates an alternate simulation, grants resources or submits fabricated results.

## Remaining qualification

Android build/toolchain setup and physical-device testing remain outstanding, including touch comfort, battery/temperature, memory and GPU performance. The two-client evidence uses this Windows computer and loopback networking; remote-device WAN behavior and two-human match usability remain unvalidated. Public hosting, TLS/host provisioning, operational backups, abuse handling and scale/load qualification remain operator work. Native crash capture, human onboarding comprehension, gameplay pacing and faction balance are not claimed as validated by these engineering checks.
