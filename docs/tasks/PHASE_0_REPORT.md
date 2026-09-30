# Phase 0 — discovery and foundation

Status: complete. This report is Human Checkpoint 1; the brief authorizes automatic continuation.

## Files created / modified

Created `docs/audits/initial-project-audit.md`, the design/art/technical/testing foundation listed in `docs/README.md`, `docs/tasks/DEVELOPMENT_PLAN.md`, `CURRENT_PHASE.md`, root README, .gitignore and .gitattributes. Existing reference PDF was read in full (20 pages) and preserved byte-for-byte. No pre-existing source code existed.

## Features and decisions

Working IP: Emberfield. Original map and faction proposals are documented separately from implementation. MVP remains one biome, two factions, four resources and Eras, a small roster, two victory modes and offline 1v1 before networking. Fair competitive resets exclude persistent combat or economic power.

Architecture: plain C# simulation with fixed ticks, stable IDs and commands; explicit Unity composition with primitive visual proxies, URP, touch input and a safe-area HUD. Integer state makes replay testing practical but does not certify future cross-platform lockstep. No full ECS rewrite, paid services, backend SDK, matchmaking or large optional installation.

Read-only audit found Unity 6000.6.0f1 incomplete. Its first real launch exited 1 due to missing Package Manager. The existing 6000.3.23f1 editor is used under the brief's genuine-blocker exception. Native template settings and compatible packages were selected; no editor was installed or repaired.

## Tests and build

No game tests belonged to documentation discovery. The PowerShell build/test wrappers were syntax checked and their XML validation exercised with passing/failing/empty-result fixtures. First 6000.6 launch failed before game compilation; 6000.3 successfully started and resolved packages. Phase 1 reports carry actual game build/test evidence.

## Limitations, performance and debt

No target-device profiling. Android/iOS Unity modules absent; full iOS output additionally needs macOS/Xcode/signing. The reference's current-product claims were not reused as verified facts. Creative names are working names, not a trademark clearance claim. Future-system docs describe design intent; they are not working backend or multiplayer integrations.

Next phase: Phase 1 greybox selection and movement, compile/tests, development build and smoke verification.
