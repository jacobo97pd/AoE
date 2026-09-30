# Alpha gameplay metrics

`AlphaMatchMetrics` observes the local player's current client session. It does not modify simulation rules, record opponents' private information, change network messages/content versions, persist a competitive result, or upload telemetry. Changing World instance starts a new session; restarting/reconnecting the client therefore does not claim complete match history.

## Integration contract

The MatchController owns `Metrics = new AlphaMatchMetrics(World, LocalPlayer)` after creating its World. For local simulation it calls `Metrics.BeginTick(World)` immediately before `World.Tick()`, then `Metrics.ObserveTick(World, LastTickMilliseconds)` after the tick. Commands/AI/stress setup occur before BeginTick. For an online replica it calls `ObserveTick(World)` after applying a server observation; duplicate tick observations do nothing.

All accepted local user commands pass through `SubmitPlayerCommand`, which calls `ObserveCommand(command, result)`. This includes movement, gathering/returning, construction/training, research, faction actions and surrender. Opponent commands, denied commands and provisional online queue results are excluded. `OnlineControls` calls `ObserveAcceptedOnlineOrder()` once when an actual server reply confirms acceptance. A lost reply remains uncounted because the client cannot prove that acknowledgement was received; reconnect does not invent orders for the disconnected interval.

`ObserveFrame(Time.unscaledDeltaTime, gameplayActive)` records client gameplay frame intervals. The caller excludes menus/inactive views. Render stalls within an included gameplay interval remain in the distribution. HUD formatting should use its existing reduced refresh frequency rather than allocate strings every rendered frame.

## Meaning of the numbers

| Metric | Definition and scope |
| --- | --- |
| Workers / idle workers | Own surviving worker count. Idle means no worker task, no movement order and no attack target. Cargo retained by a stopped worker does not make it productive. |
| Army | Own non-worker, non-packed units with positive attack damage, plus siege equipment such as unarmed ladders and towers. Creatures count as army; transport carts are excluded. |
| Population | Own used, reserved and capacity values from the simulation/filtered snapshot. |
| Busy producers / producers | Own operational buildings capable of training or research. Busy means an active production queue or research. Unfinished/relocating buildings are excluded. |
| Production utilization | Current busy/operational-producer ratio, from 0 to 1; this is an instantaneous reading, not a historical time-weighted percentage. |
| Accepted orders / APM | One confirmed command counts as one order regardless of selected unit count. APM uses the latest 60 seconds of observed match ticks; local paused time does not advance it. During the first minute the denominator is elapsed observed match time, with a one-second floor. |
| Offline income/min | Real stock credited during local World.Tick, measured after outside-tick purchases and before/after the tick. Current EconomySystem.ReturnArrived is the sole in-tick stock credit. Gathering into cargo does not count until delivery. Purchases and initial stock do not count as income. |
| Delivered(resource) | Cumulative credited local-session resource amount. Read only with HasExactIncome; skipped local tick sampling disables the exact-income claim. |
| Online net/min | Signed own stock change between snapshots, including purchases. It can be negative. The protocol contains no cumulative delivery counter, so gross online income is explicitly unavailable rather than inferred from a stock increase. |
| FPS / frame p95 | FPS is 1000 divided by mean frame interval, from the latest 300 included client gameplay frames. Frame p95 uses nearest-rank p95 of those intervals. These are client presentation timings and do not isolate GPU work. |
| CPU tick p95 | Nearest-rank p95 of the latest 200 supplied local World.Tick durations. AI, rendering and server CPU are excluded. Online replicas report this unavailable. |

Rates use a fixed ring of 1200 simulation-tick buckets (60 seconds at 20 Hz). APM and resource rate storage is bounded; session totals use 64-bit integers. Frame/tick windows and percentile scratch arrays are allocated once. Unit/building scans use index loops and inspect ownership before reading relevant state. Quantiles are recalculated lazily when their underlying window changes. The UI must label online resource rates as **Net/min**, local exact rates as **Income/min**, and unavailable exact metrics accordingly.

Seven focused tests cover local-only counts/initial stock, paid-command accounting/rejections, actual cargo delivery and rate expiry, duplicate/missing observation handling, frame sample windows/stalls, online acknowledgement/net-only accounting, and a new-world session reset. They live in `Assets/Tests/PlayMode/AlphaMatchMetricsTests.cs`; root owns their Unity execution as part of the frozen-source verification run.

The Alpha 0.3 expansion walkthrough accelerates ordinary AI development and therefore does not report display FPS. The separate offline render probe now calls BeginTick/ObserveTick around every directly advanced World tick, keeping its captured HUD current. Its World.Tick stopwatch ends before ObserveTick, so CPU tick measurements retain their documented scope. Full-frame measurements include the observer work. Comparisons with older builds must account for this corrected workload and the changed simulation content; the Alpha 0.3 measurement is an absolute local fixture result, not an isolated rendering-speed comparison.

## Efficient performance and balance evaluation

Preserve the previous packaged build and its GUID as the baseline before rebuilding the presentation. Simulation data and protocol must remain unchanged for a meaningful comparison. The existing `tools/Profile-OfflinePlayer.ps1` exercises the shipped Aven/Dominion mid-match fixture with verified tick window, content hashes, command trace and a rendered capture. Run the same 1280×720 case once on baseline and once on the current build first; this resolves whether the new HUD/metrics introduces a material presentation cost without repeating the complete scenario matrix. If a regression or noisy result appears, repeat both builds under the same idle-machine conditions before broadening to 1440-pixel layouts or large armies.

That harness serializes offscreen rendering and is suitable for paired frame/tick/view-cost comparisons. Its timings do not establish ordinary displayed FPS. The new gameplay frame metric supplies an in-session observation, while the existing 50–500-mover CPU/movement harness and `tools/Compare-OfflinePerformance.ps1` remain the controlled tools for simulation performance and narrow-choke concerns. Keep GPU timing marked unavailable when unsupported; no desktop measurement establishes target-mobile performance.

The retained `OfflineRenderProbe` driver advances World directly and bypasses the new MatchController metrics hooks. Its session-count/rate HUD fields remain cached and are not metric evidence; its measured overhead excludes active metrics sampling. `ProductShellSmoke` and ordinary matches use `AdvanceSimulationTick`/the controller update, so their observed gameplay window includes metrics integration. The Alpha 0.2 report records both protocols explicitly.

For gameplay balance, reuse the existing natural offline AI harness with both faction seat assignments and both modes; record winner, duration, accepted orders and actual session delivery readings separately. The earlier four Aven wins and shorter-than-target Conquest matches remain a small scripted signal. Presentation/metrics changes cannot establish a balance improvement. A useful human pass records mode/faction, match duration, long idle-worker periods, producer use and resource float, then asks whether resource spending, selection and objective progress were understandable. Tune rules only in a separate versioned change with matching client/server content; no balance rule changes belong to this instrumentation work.
