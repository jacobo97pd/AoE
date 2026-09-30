# Alpha 0.2 — frontend, tactical HUD and world presentation

Alpha 0.2 gives Emberfield a native product frontend and a revised in-match presentation while retaining the previously implemented offline/online rules. Unity verification, the service suite, the Windows build and packaged functional checks pass. Evidence below identifies the tested executable and separates client gameplay observations from the serialized rendering benchmark.

## Implementation

### Main menu and navigation

Normal Windows launch opens an illustrated Home screen with skirmish setup, faction/mode choices, Multiplayer, Settings and Store. An unfinished local skirmish can open Main menu and resume the same World. Frontend pages and delegated panels keep local simulation paused; server-authoritative online matches continue while their menus are open. Settings-to-guide transitions explicitly return to gameplay instead of reopening the frontend.

Skirmish setup selects Aven Compact/Serevin March and Conquest/Dominion. Learn to play opens the optional practice guide. The Store, named The Quartermaster, is an empty catalog awaiting content. It has no fabricated stock, prices, currency, purchases or payment flow.

### HUD and panel hierarchy

The native HUD uses shared Marcellus/Lato typography, original vector symbols, resource cards, population, selection health, contextual actions and a fog-aware minimap. Research, faction, offline, online and settings panels share surfaces, borders, headings and button states. Existing action identities, scroll behavior and touch interception are retained.

A no-selection tactical summary shows own workers, idle workers, army, busy producers and accepted orders per minute. Performance overlay is a saved opt-in setting, disabled by default.

### Gameplay metrics

AlphaMatchMetrics observes the local client session with bounded windows. Offline income is measured around local World ticks and counts actual delivered stock; purchases, initial resources and cargo still carried by workers are excluded. Online resource rates are explicitly net stock change including spending, because the current snapshot protocol has no cumulative delivery counter.

APM counts confirmed commands once, regardless of selected unit count. Online orders count only after accepted server replies. Worker/army/population/producer counts are local-player observations; private enemy state is not read. FPS and frame p95 use recent client gameplay frame intervals; CPU tick p95 is available only for locally measured World ticks. Menus/results are excluded from gameplay frame samples. No display-present latency or camera-isolated GPU timing is inferred.

These are client-session readings, not a complete match replay or server telemetry. Reconnect starts with the state the client can observe; lost acknowledgements and disconnected intervals are not invented. [ALPHA_METRICS](../technical/ALPHA_METRICS.md) defines exact sampling, rates and limitations.

### World graphics and asset provenance

Original editable geometry now covers **26 faction/archetype combinations**: seven unit types and six building types for each playable faction. Near/far meshes are shared, with additional native resources and public beacons. Both factions have authored units, unique roles, buildings and transports; the earlier provisional Serevin presentation is superseded.

Terrain dressing, faction materials and warm directional/post-processing treatment update the rendered world. Continuous vertex color removes the old visible terrain grid; green grass, ochre paths, a shallow animated creek and stone fords distinguish the landscape. Soft shadows, ACES, restrained bloom/vignette, full render scale and antialiasing complete the lighting treatment. Home framing keeps the starting Hearth above the contextual HUD. Decorative geometry does not alter collision or simulation rules. The earlier art gallery and neutral practice fixtures retain their historical rendering.

The full-screen menu illustration is generated decorative key art, not a claimed gameplay screenshot. Native UI symbols, geometry recipes and retained font licenses are documented in [ALPHA_ASSET_PROVENANCE](../art/ALPHA_ASSET_PROVENANCE.md).

## Verified source behavior

The final current source verification on Unity 6000.3.23f1 passes:

| Check | Result |
| --- | --- |
| EditMode | 423 / 423 |
| PlayMode | 103 / 103 |
| Combined Unity tests | 526 / 526 |
| Service tests, including real authority over HTTP | 16 / 16 |
| Common combat probes | 12 passed |
| Faction probes | 8 passed |
| Natural offline matches | 4 completed and passed |
| Movement matrix | Required gates passed across 30 cases |

Coverage includes the new frontend lifecycle, local state/resource preservation, real session metrics and geometry presentation, alongside retained combat/economy/faction/input/network behavior. The mobile UI-origin regression derives its touch origin from actual rendered UI geometry while preserving the pan/pinch/order-blocking and fresh-contact recovery assertions.

The metrics suite checks actual cargo delivery, paid-command accounting, rolling-window expiry, rejected/opponent order exclusion, duplicate/skipped observations, online net-only/acknowledgement behavior and bounded frame samples. Source checks are not substitute evidence for a built player's visual quality, performance or human usability.

## Packaged verification

### Final Windows package

Unity **6000.3.23f1** produced the Windows development player at `Builds/Windows/Emberfield.exe`, version **0.2.0**, default window **1600×900**. [Build evidence](../testing/evidence/alpha02/build.txt): **zero errors, zero warnings**, **170,386,522 bytes**, application GUID **`0e78fcce58ec4fb489ca9baf9ed74b19`**. No Unity/package/platform module was installed or upgraded. Service/Unity/build stages were executed separately; the final manifest is an inventory, not a claim that one invocation ran every check.

### Native walkthrough and regression matrix

The same final package passed **11 functional checks**. The [packaged matrix](../testing/evidence/alpha02/player-matrix.md) links the individual reports and captures. Both 1280×720 and 1440×1080 frontend walkthroughs dispatch **10 visible controls**, verify local pause, empty Store, real faction/mode setup and scene entry, then develop a settlement through ordinary AI commands. Native resource and selection HUDs, settings and research are captured.

At both resolutions two independent online clients use a fresh real HTTP/SQLite service and C# authority. The runner terminates/relaunches the guest, confirms continued authority ticks and recovered orders, then checks server-owned results/history and each client's **Return to lobby** control. Review found that this control previously reloaded local practice with its lobby closed; setting the existing scene-reopen flag fixes it, and the extended packaged test now verifies an open idle lobby after scene transition.

Seven additional checks cover both faction drills, all shared research, naturally completed Conquest/Dominion, economy and combat. They use accelerated ordinary simulation for functional coverage. The online checks use real-time simulation. Captures render the actual camera and native Unity UI to a screenshot target; they are in-engine evidence, separate from the generated decorative menu illustration.

The hidden-player screenshot driver now renders explicitly when Windows supplies no readable swapchain, waits for new UI geometry before raycasting and restores overlay canvases after capture. Pixel proof samples the whole frame so a dark modal's empty middle row does not create a false failure. Runtime logs are free of detected exceptions/shader errors. [EditMode](../testing/evidence/alpha02/editmode.xml), [PlayMode](../testing/evidence/alpha02/playmode.xml), [services](../testing/evidence/alpha02/server-tests.txt), [combat](../testing/evidence/alpha02/combat.md), [factions](../testing/evidence/alpha02/factions.md), [offline](../testing/evidence/alpha02/offline-matches.json) and [movement](../testing/evidence/alpha02/movement.json) preserve verification evidence.

### Rendered performance

On this workstation (Core i5-10400F, RTX 3060 Ti), the final packaged walkthrough records the latest **300 frames** after eight seconds of ordinary gameplay with the frame cap set to 60:

| Window | Client FPS | Frame p95 | Local World tick p95 |
| --- | ---: | ---: | ---: |
| 1280×720 | 59.92 | 16.67 ms | 1.262 ms |
| 1440×1080 | 59.95 | 16.70 ms | 1.178 ms |

These are client loop intervals in a hidden non-batch player, not display-present latency, GPU timing or mobile measurements. The preceding two minutes of simulation are accelerated setup and are excluded from the latest frame window. [Wide report](../testing/evidence/alpha02/frontend-1280x720.json), [tablet report](../testing/evidence/alpha02/frontend-1440x1080.json).

The separate controlled benchmark uses the shipped Aven/Dominion checkpoint: 6000 setup ticks, 60 warmup ticks and exactly **400 measured ticks at 20 Hz**, with explicit camera/UI rendering and synchronous GPU readback at 1280×720/D3D11. Its driver advances World directly, bypassing the new MatchController metrics hooks. Consequently its cached session-count/rate HUD fields are not valid metric evidence and it does not measure the full new metrics sampling cost. Use the product walkthroughs above for that integration. The retained [probe capture](../testing/evidence/alpha02/render-probe.png) is rendering evidence; the [river battle capture](../testing/evidence/alpha02/river-gameplay.png) comes from the separately instrumented match walkthrough. Benchmark overhead is included, so these are serialized offscreen timings:

| Build/run | Frames | Frame p95 | World tick p95 | View p95 |
| --- | ---: | ---: | ---: | ---: |
| Previous alpha, 1 | 18,986 | 1.898 ms | 0.723 ms | 0.198 ms |
| Previous alpha, 2 | 18,635 | 1.906 ms | 0.738 ms | 0.207 ms |
| Visual alpha, 1 | 9,843 | 2.893 ms | 0.699 ms | 0.339 ms |
| Final alpha, 2 | 9,705 | 2.958 ms | 0.735 ms | 0.381 ms |

The visual upgrade increases serialized frame p95 by approximately **1 ms**. This is an explicit quality/cost tradeoff, not a performance improvement. All four runs keep simulation/wall ratio above 0.9997 and have identical rules, map, checkpoint, sample-start and sample-end hashes. World-tick measurements remain comparable. GPU telemetry is unavailable in every run.

The previous binary has GUID `cdf8183365404135975644fd3c935a58`; the first visual measurement has `91cf20f1c9984e70bcc9568c823d0c60`; the second is the final package above. The first visual run predates only the lobby return correction and Home framing; the benchmark sets its own camera. [Four raw runs and hash comparison](../testing/evidence/alpha02/render-comparison.json) preserve the values and exact identities. These data do not qualify mobile thermals, large art armies or prolonged online load.

## Scope and limits

This pass improves product navigation, observability and presentation; it does not tune faction rules or establish better competitive balance. Human fun, readable decision-making and appropriate match pacing still require human playtesting.

Android platform support and the bundled SDK/NDK/JDK are absent on the audited workstation. No Android build, physical-device touch, thermal/battery or target-mobile performance acceptance is claimed. Dense-choke behavior and cold movement orders remain subject to the historical movement findings until separately remeasured or changed.

Online play remains a runnable local/self-hosted alpha. Public deployment, HTTPS provisioning, password recovery, operational monitoring, sustained load and Internet/mobile-network acceptance remain production gates. Client reconnect works against a live authority; server-process restart does not restore an in-progress World.

Store content is pending. There are no products, transactions, premium currency or item entitlements. Authored geometry covers both playable factions, while a full production asset library, skeletal animation and offline save/load remain future work. Optional local diagnostics remain bounded, explicit-consent device files with no automatic upload.

## Historical evidence

Earlier milestones remain available through the [documentation index](../README.md), including [Phase 9 art slice](PHASE_9_REPORT.md), [Phase 10 optimization](PHASE_10_REPORT.md), [Phase 11 multiplayer](PHASE_11_REPORT.md), [Phase 12 backend](PHASE_12_REPORT.md) and [Phase 13 alpha](PHASE_13_REPORT.md). Their build IDs, smoke counts and timings describe those versions.

During compilation the system drive ran low on space. Generated `Library/PackageCache`, `Builds/AlphaBaseline` and `Builds/Phase10Baseline` were moved intact to their matching paths beneath `D:/EmberfieldWorkingCache/AoE/`, with NTFS junctions preserving their original project paths. Source/assets and package versions were not relocated or changed by this recovery. The successful build/tests use those junctions; D: must remain available. The reference PDF is unchanged (SHA-256 `778295A57495B1F67CC9E42A7F8F01932EF5841F6491FB72E84F55D77BCAEDF7`). Nothing was uploaded, pushed or deployed.
