# Phase 6 — factions

**Status: complete.** Aven Compact and Serevin March are playable in a mirrored faction drill. Each has a passive bonus, a distinct mechanic, a unique unit and technology, contextual controls and original placeholder silhouettes. Verification passed **290 EditMode tests, 32 PlayMode tests, 20 counter probes, the required gates of the 30-case movement matrix, a Windows build and nine final-build player checks**. Phase 7 has not started; a complete match against strategic AI remains future work.

## Baseline and environment

Work continues from Phase 5 commit `5eeb572`. Unity **6000.3.23f1** at `D:\Unity\Editors\6000.3.23f1\Editor\Unity.exe`, URP 17.3.0, Input System 1.20.0, Test Framework 1.6.0 and uGUI 2.0.0 remain unchanged. No editor, package or platform module was installed. The [initial audit](../audits/initial-project-audit.md) records the unusable alternative editor installation.

Checks ran on Windows 11 (10.0.26200), Intel i5-10400F 2.90 GHz (6 cores / 12 threads), 16,281 MB RAM and NVIDIA GeForce RTX 3060 Ti reporting 8,024 MB graphics memory. Android/iOS modules are absent; no APK, signed iOS build or physical mobile session was produced.

The supplied reference PDF remains unchanged: SHA256 `778295A57495B1F67CC9E42A7F8F01932EF5841F6491FB72E84F55D77BCAEDF7`. Its [design lessons](../design/REFERENCE_DESIGN_LESSONS.md) inform economic commitments, mobility and counterplay. Names, mechanics as implemented, map layout and primitive visuals are original prototype content.

## Implemented behavior

Choose **Factions** in the bottom HUD, then **Play Aven Compact** or **Play Serevin March**. This starts a fresh 64×56-metre drill with 180-degree mirrored positions and resource nodes. Each side has four Tenders, a Hearth, Muster Hall and Storeyard, **120 Food / 80 Wood / 0 Metal / 40 Stone**, and population **4 used / 6 capacity**. The chosen faction belongs to the local blue player; the opposing red player receives the other faction. Choice and restart preserve the intended assignment. The opponent has local combat responses, without strategic AI.

The original four unit definitions, nine shared research items, Era chain, starting stocks and common costs/stats are preserved. Existing maps remain neutral. The shared Muster Hall additionally trains its owner's unique unit; Storeyards support Aven charters. Foreign unique purchases are hidden in the UI and rejected atomically by simulation. Neutral fixtures retain their original roster and nine-item research tree.

| Choice | Aven Compact | Serevin March |
| --- | --- | --- |
| Identity | Infrastructure and local economic commitments | Mobility and exposed frontier logistics |
| Passive | Tenders carry +2 resources: base 10 becomes 12 | Cavalry speed +5% |
| Mechanic | Pay 40 Wood / 20 Metal for a Storeyard charter; 5-second setup/change, 6-metre radius | Build a Supply Outpost for 80 Wood / 20 Stone; pack into a vulnerable cart, move and redeploy |
| Decisions | Logistics adds +4 local carry; Muster adds +20% local training work. Switching suspends the old benefit | Packing and deployment each take 5 seconds. Drop-off stops during relocation; all damage persists |
| Unique unit | Threadkeeper: 60 Food / 40 Wood, 8-second training, 1 population, 70 health; unarmed support | Ashrunner: 80 Food / 20 Wood, 8-second training, 1 population, 95 health; light cavalry |
| Unique action | Deploy a stationary 4-metre charter relay for 15 seconds, then 20-second cooldown | Reposition for 3 seconds with no attacking, then 15-second cooldown |
| Kingdom Archive research | Reciprocal Stores: 120 Food / 100 Wood, 30 seconds; charter radius and relay tether increase from 6 to 8 metres | Prepared Encampments: 100 Food / 100 Wood, 30 seconds; future packing/deployment each shorten from 5 to 3 seconds |
| Visual language | Terraced roofs, square tabs, charter markers and a framed support signal | Wedge roofs, rails/runners and narrow pennants |

Exact unit/building stats and costs are maintained in the [faction bible](../design/FACTION_BIBLE.md), [unit roster](../design/UNIT_ROSTER.md), [building roster](../design/BUILDING_ROSTER.md) and [technology tree](../design/TECHNOLOGY_TREE.md), backed by shipped JSON.

### Aven commitments and counterplay

Charter influence uses centre-to-centre distance. Overlapping Storeyards and relays never stack; a stable source order resolves overlaps. Logistics capacity reaches 16 for a supported Tender. Leaving support preserves any cargo already carried, including loads above the new capacity; delivery remains physical and sources finite. Muster uses 1,200 training work per tick against the base 1,000, carrying fractional work forward. This is 20% more work, approximately 16.7% less training time under continuous support; it does not accelerate research or construction.

A Threadkeeper must deploy beside an owned, completed, chartered Storeyard. It cannot move or attack while relaying. It extends that site's current charter, creates no new drop-off and cannot form relay chains. Switching the linked charter suspends influence during the change; losing the Storeyard ends the relay and starts its full cooldown. Reciprocal Stores extends the yard's coverage and relay tether, while the relay's own radius stays at four metres. Raiding the stationary support or its linked infrastructure removes the benefit.

### Serevin mobility and counterplay

Ashrunners retain the shared cavalry/light tags and remain vulnerable to prepared spears. Their base speed is 5,000 mm/s: the passive gives 5,250, while Reposition gives 7,750 through additive passive/burst modifiers. Activating cancels attacks and prevents both acquisition and explicit attack commands during the burst. Existing movement routes continue; Stop is valid, and expiry does not silently restore an attack order. Groups consisting of eligible Ashrunners can activate together. The common Strider remains available: the earlier replacement proposal is deferred in favour of a unique alternative.

The Supply Outpost takes eight seconds of construction work, occupies 2×2 cells and has 450 health / 1 armor. It provides drop-off, with no housing or production. Packing requires a completed idle site and immediately disables utility. Its footprint remains blocked during preparation. Conversion preserves the entity ID and current/max health, clears the footprint and produces a population-free cart: 2,200 mm/s, zero armor, no weapon, no worker abilities and no direct training purchase. Losing the cart is a real death; transforming it is not.

Move the cart into position, then choose **Deploy outpost**, preview a legal adjacent footprint and confirm. Deployment requires a stationary cart, takes time at its current location and revalidates the footprint at completion. It does not reserve the destination in advance. An obstructed completion waits at zero until clearance; **Stop** cancels that deployment so the cart can move again. Successful conversion retains health and identity. Previously accepted preparation durations remain fixed if research completes mid-action. There is no extra relocation resource fee; the paid asset, downtime and exposure are the commitment.

## Architecture and rule decisions

The Unity-free simulation owns faction assignment, legality, payment, effective stats, action timers and transformations. `FactionCatalog` validates exactly the authored faction kinds, references, unique gates, per-faction reachability, supported values/overflow and compatible transport definitions. Player faction IDs are copied from map assignments at world creation. Missing assignments retain neutral legacy behaviour; authored unique spawns require the matching faction.

`SetCharterCommand`, `DeployThreadkeeperCommand`, `PackOutpostCommand`, `DeployOutpostCommand` and `RepositionCommand` pass through the existing authoritative command boundary. Read-only validation supplies UI feedback; rejected commands leave resources, orders and active state unchanged. Shared definitions are never mutated to grant player bonuses.

Tick order is **Refresh bonuses → Movement → Refresh bonuses → Economy → Construction → Combat → Faction actions (including bonus refresh) → Production → Research → Refresh bonuses → TickIndex increment**. Combat resolves before a would-be faction completion. Position changes affect local economy/production on the same tick; newly completed research refreshes effective values before the next tick. Charter training uses integer work and remainder. Relocation state stores the accepted total duration separately from remaining ticks.

Presentation owns chooser/modal state, contextual actions, deployment preview and feedback. The existing world view detects a changed definition under the same entity ID and replaces the model without losing selection. Selected influence rings, action bars, cooldown labels and current effective troop/cargo values expose the rules. Modal panels intercept world input. Dead transports clear their deployment preview. Explicit chooser selection takes precedence over the development command-line initial faction. See [simulation architecture](../technical/SIMULATION_ARCHITECTURE.md) and [technical design](../technical/TECHNICAL_DESIGN_DOCUMENT.md).

## Tests and results

`tools/Verify-Unity.ps1 -Stage All` completed compilation, both Unity test suites, both counter suites and movement verification. All 243 prior tests were retained. The final research text layout and automated relocation approach adjustments were followed by another successful **32-test PlayMode run**, the final build and all nine player checks; simulation source did not change after the full verification.

| Check | Result | Evidence |
| --- | --- | --- |
| EditMode | **290/290 passed**, 73 added; zero failed/skipped | [NUnit XML](../testing/evidence/phase6-editmode.xml) |
| PlayMode | **32/32 passed**, 6 added; zero failed/skipped | [Latest NUnit XML](../testing/evidence/phase6-playmode.xml) |
| Common combat roles | **12/12 passed** using shipped JSON | [Counter report](../testing/evidence/phase6-combat-balance.md) |
| Faction counters | **8/8 passed**, including mirrored and numbers-advantage cases | [Faction counter report](../testing/evidence/phase6-faction-counters.md) |
| Movement regression | **30 cases; required gates passed**; known dense narrow-choke failures retained | [Readable report](../testing/evidence/phase6-movement.md), [raw JSON](../testing/evidence/phase6-movement.json) |

New rule tests cover assignment and invalid data; ownership and unique purchase gates; atomic costs/rejections; exact charter, relay and cooldown timing; local influence, non-stacking and preserved cargo; training work; research effects; Reposition restrictions; same-tick death; packing/deployment legality; health/identity/population preservation; footprint changes, obstruction/cancellation and accepted-duration stability. New PlayMode checks cover shipped-map choice, faction-aware actions/research, modal input, relay/relocation integration, grouped Reposition and dead-transport preview cleanup.

Counter probes use explicit attacks at seven metres, no research/charters/Reposition, and a 90-second deadline including outstanding projectiles. A Reedguard defeats one Ashrunner in **5.40 seconds with 64 health**, while two Ashrunners overturn that result. An Ashrunner defeats one Stringwarden in **6.95 seconds with 25 health**, while two Stringwardens overturn it. Both ownership directions give the same results. Ashrunner costs 100 nominal resources against 80 for a common role, so these demonstrate counter behaviour and force concentration, **not equal-cost or full-match competitive balance**.

Review corrected grouped action availability, stale deployment previews, duration bars during research and chooser precedence. The research description received more vertical room after captures exposed clipping. An integration expectation was corrected to account for the absent Archive before checking Era requirements separately. A failed automated second relocation was corrected to move the newly packed cart into a legal adjacent position; the deployment rule was preserved. Final captures and reruns cover these changes.

## Build and packaged-player checks

The final Windows development build **succeeded with zero errors and zero warnings**, size **163,206,372 bytes**, duration **13.7569182 seconds**, GUID `3a4d191876ae44fba8e0e681bfbdeaa0`. Launch `Builds/Windows/Emberfield.exe` and choose **Factions**. Build binaries remain ignored by Git. [Build summary](../testing/evidence/phase6-build.txt).

| Final-build scenario | Resolutions and evidence | Verified outcome |
| --- | --- | --- |
| Aven | [1280×720](../testing/evidence/phase6-aven-1280x720.txt), [1440×1080](../testing/evidence/phase6-aven-1440x1080.txt) | Physical gathering, paid Logistics/Muster switching, Threadkeeper relay and Reciprocal Stores; tick 4,035, population 6 + 0 / 6 |
| Serevin | [1280×720](../testing/evidence/phase6-serevin-1280x720.txt), [1440×1080](../testing/evidence/phase6-serevin-1440x1080.txt) | Ashrunner burst, paid Outpost construction, two relocations preserving 439/450 health, Prepared Encampments; tick 3,844, population 5 + 0 / 6 |
| Technology | [1280×720](../testing/evidence/phase6-technology-1280x720.txt), [1440×1080](../testing/evidence/phase6-technology-1440x1080.txt) | All nine shared research purchases, Empire, existing/new troops upgraded, enemy unchanged; tick 16,338 |
| Combat | [1280×720](../testing/evidence/phase6-combat-1280x720.txt), [1440×1080](../testing/evidence/phase6-combat-1440x1080.txt) | All three common roles attacked, damage/projectiles observed, three deaths and dead-view cleanup; tick 123 |
| Economy | [1440×1080](../testing/evidence/phase6-economy-1440x1080.txt) | Gathered, constructed, trained and fought; tick 1,312 |

All four faction runs verify equal unmodified starting budgets, physically gathered funds and atomic foreign-unique rejection. Each dispatches two actual chooser buttons, switching to the other faction and back, proving that explicit choice overrides the initial CLI setting. Aven verifies nine visible button dispatches and Serevin twelve, including the two chooser dispatches in each run. Button centres must pass EventSystem raycasts before synthetic click handlers run. These are desktop interaction checks, not physical touch or manual scrolling tests.

The faction runner accelerates 100 simulation ticks per yielded frame while waiting: Aven covers 4,035 ticks with 44 yielded frames; Serevin covers 3,844 ticks with 43. These correspond to 201.75 and 192.2 simulated seconds of scripted actions, without establishing human match pacing or ordinary rendering performance. Screenshot FPS labels are not performance evidence. All final player logs were checked for errors.

Inspected evidence includes the [phone chooser](../testing/evidence/phase6-faction-chooser-1280x720.png), [tablet relay](../testing/evidence/phase6-aven-relay-1440x1080.png), [phone charter](../testing/evidence/phase6-aven-charter-1280x720.png), [phone Reposition](../testing/evidence/phase6-serevin-reposition-1280x720.png), [tablet cart](../testing/evidence/phase6-serevin-transport-1440x1080.png), [damaged redeployed Outpost](../testing/evidence/phase6-serevin-deployed-1280x720.png), [Aven research](../testing/evidence/phase6-aven-research-1440x1080.png) and [Serevin research](../testing/evidence/phase6-serevin-research-1280x720.png). Both aspect ratios and existing economy/combat/technology captures are retained in the evidence directory.

## Performance observations and known limits

The unarmed movement fixtures cover 50/100/200/300/500 units and a 120-second simulated deadline. All OpenField, WideCorridor, CrossingGroups and DynamicObstacle groups completed; all 30 cases reported zero observed overlaps and zero static/map violations, and unreachable orders rejected atomically. **NarrowChoke at 200/300/500 still reaches only 31/52/37 arrivals by the deadline.** Circulating units may avoid the immobility counter without reaching their destination. No navigation optimisation or dense-choke fix is claimed.

For this rerun, OpenField active-tick p95 was **0.060 ms at 100 movers**, arriving by 27.45 simulated seconds; **0.374 ms at 500 movers**, arriving by 28.85 seconds. The 500-unit initial command took **30.623 ms**, retaining a cold-order hitch. These are editor CPU figures from neutral movement workloads, without active faction influence or relocation. Rules JSON SHA256 is `6F100B25035E54E4144761F51885F52E45051EBD6562EF6024AFC37601CB63C2`; simulation plus diagnostics source SHA256 is `CEB28A978BF89C869B884BF60C3B2427CDFF91FEA78D752A993166965B66460A`.

The allocation capability probe failed, so allocation measurements remain **unavailable (-1)**. No zero-allocation claim is made. Phase 4's serialized offscreen URP/Canvas plus synchronous GPU-readback measurements remain historical. Phase 6 adds no rendered FPS, active-faction load or target-device performance claim. See [movement methodology](../testing/MOVEMENT_STRESS.md) and [performance budget](../technical/PERFORMANCE_BUDGET.md).

Faction influence currently scans sources against units repeatedly within the tick. Profile representative active charter/relay populations, relocation, research completion, UI refresh and allocations before introducing indexes or wider architectural changes. Existing debt includes an overall route-work budget, dense-choke cooperation, presentation scalability and device CPU/GPU/memory measurements.

This remains a greybox drill. Fog, scouting, strategic AI, match victory/surrender/results, save/load and online services are absent. Research has no voluntary cancellation/refund. Physical touch ergonomics, complete-match pacing, faction win rates and fun require later validation. Unique units supplement the common counter roster; wider faction rosters and final art remain deferred.

## Files created and modified

Paths are relative to the repository. All new Unity scripts and map data include `.meta` files.

| Area | Created | Modified |
| --- | --- | --- |
| Simulation, `Assets/Game/Simulation/` | `FactionDefinitions.cs`, `FactionCatalog.cs`, `FactionSystem.cs` | `Definitions.cs`, `PlayerState.cs`, `EntityStates.cs`, `World.cs`, `Commands.cs`, `MovementSystem.cs`, `EconomySystem.cs`, `InteractionSearch.cs`, `ConstructionSystem.cs`, `ProductionSystem.cs`, `CombatSystem.cs`, `ResearchSystem.cs`, `TechnologyDefinitions.cs` |
| Presentation, `Assets/Game/Presentation/` | `FactionControls.cs`, `FactionPanel.cs`, `FactionHud.cs`, `FactionWorldView.cs`, `FactionPlayerSmoke.cs` | `DefinitionLoader.cs`, `MatchController.cs`, `MatchHud.cs`, `WorldView.cs`, `EconomyControls.cs`, `ResearchControls.cs`, `ResearchPanel.cs`, `RtsInput.cs`, `PlayerSmoke.cs` |
| Authored data | `Assets/Game/Resources/Maps/faction_proving_ground.json` | `Assets/Game/Resources/Definitions/greybox.json` |
| Verification | `Assets/Tests/EditMode/FactionTests.cs`, `FactionTestWorldFactory.cs`; `Assets/Tests/PlayMode/FactionIntegrationTests.cs`; `Assets/Game/Editor/FactionVerification.cs` | `Assets/Game/Editor/CombatVerification.cs` (fixture wording), `tools/Verify-Unity.ps1`, `tools/Smoke-Player.ps1` |
| Documentation | This report and `docs/testing/evidence/phase6-*` | Root/index READMEs, current phase; faction/unit/building/technology design docs; simulation/technical/pathfinding/performance docs; test strategy and movement methodology |

No scenes, package manifests, editor settings or historical phase reports were changed. No paid services were activated and nothing was pushed remotely.

## Next recommended phase

**Phase 7 — full offline match:** fog and scouting, an AI that obeys the same resource and command rules, Conquest/Dominion victory, surrender and result/restart flow. Complete a 1v1 against AI, then measure and tune toward the proposed 12–20-minute match target and Human Checkpoint 3's fun assessment. Phase 7 has not started.
