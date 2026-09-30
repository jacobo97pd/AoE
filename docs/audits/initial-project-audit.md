# Initial project audit

Date: 2026-09-09. Recorded before creating or modifying project code.

## Workspace and repository

- Workspace: `C:\Users\jacob\Documents\AoE`, Windows, PowerShell.
- Entire initial workspace inspected recursively: only `docs/references/Age_of_Empires_IV_Estudio_Integral.pdf` (2,542,682 bytes).
- Reference SHA-256: `778295A57495B1F67CC9E42A7F8F01932EF5841F6491FB72E84F55D77BCAEDF7`. Preserve unchanged.
- No Unity project: no Assets, Packages, ProjectSettings, scenes, tests, build configuration or existing code.
- No Git repository or .gitignore. Git 2.47.0.windows.2 is available. Initialize locally; no remote push.
- No AGENTS.md found in the workspace or ancestor directories.

## Environment

- Installed editor: `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe`.
- Actual executable product version: **6000.6.0f1_f7f8ed4d1e24**. Keep this version.
- Installed project templates: URP blank 17.2.1 and 2D cross-platform 7.0.0.
- URP template manifest supplies URP 17.6.0, Input System 1.19.0, Test Framework 1.8.0, uGUI 2.6.0. Use relevant versions, omit unrelated template dependencies.
- Built-in package directory includes URP, Test Framework, uGUI and standard engine modules. No project package lock exists yet; actual resolution is a subsequent verification step.
- Only WebGLSupport is present in PlaybackEngines. Android/iOS editor platform modules are absent; SDK presence alone cannot enable Unity Android builds.
- External Android SDK exists at `%LOCALAPPDATA%\Android\Sdk` with platforms, build-tools, platform-tools, NDK and command-line tools. Compatibility with this editor is not established.
- PATH Java: Eclipse Adoptium JDK 21.0.7.6; separate Oracle JDK 23 and 24 directories present.
- .NET host/runtime 8.0.29; no standalone .NET SDK detected. Unity includes its own compilation toolchain.
- Python 3.13 available. No pypdf, PyMuPDF or pdfplumber initially; a small isolated PDF parser may be used to read the supplied reference.
- Blender not found on PATH or in its conventional Program Files location. Optional; primitives suffice.

## Risks and blockers

- Unity batch licensing, import, compile and test execution still need to be proven. Do not claim a successful build before running one.
- Android build support is missing. Prepare build code and report module requirements; do not install large modules automatically.
- Windows cannot perform a complete signed iOS/Xcode build. Prepare compatible settings only.
- No device profiling or human touch usability evidence yet. FPS and population numbers remain targets.
- No networking SDK, credentials, paid service or asset library is required for this phase.

## Implementation plan and assumptions

1. Read the supplied PDF as systems analysis; create original design documentation and concise future-system plans.
2. Create a lean Unity project in this workspace with stable metadata and a correct .gitignore.
3. Build Phase 1: plain C# simulation assembly, fixed ticks, stable entity IDs, validated command boundary, configurable definitions, one greybox map, touch/mouse camera, selection and movement.
4. Use primitive GameObjects only for presentation. Use explicit composition, with no dependency from simulation to UnityEngine.
5. Compile, run EditMode and PlayMode integration tests, and make a local development build where supported.
6. Continue to Phase 2 economy only if Phase 1 architecture and build are healthy. Keep phase evidence and current status in docs/tasks/CURRENT_PHASE.md.

Working scope is the requested initial phased foundation, not all thirteen product phases. Phase checkpoints are reports, not approval gates under the supplied instructions. No paid services, Unity reinstall, large optional tools, final art, multiplayer implementation or reference content reproduction.

## Follow-up environment evidence

These additional read-only checks refine the initial conventional-path detection above. They do not change the selected editor or establish a successful project build.

- A second editor exists at `D:\Unity\Editors\6000.3.23f1\Editor\Unity.exe`, product version `6000.3.23f1_09d2ecc7fb28`. Its PlaybackEngines directory contains Windows standalone support. Keep the selected 6000.6.0f1 editor; do not downgrade or copy modules between versions.
- Portable Blender **4.5.13 LTS** is available at `D:\CodexTooling\blender-portable\blender-4.5.13-windows-x64\blender.exe`. Its `--version` output reports build date 2026-08-25 and hash `daeeeca98fb0`. It is absent from PATH but usable by absolute path; no installation needed.
- The selected editor includes a .NET SDK at `Editor\Data\DotNetSdk\dotnet.exe`: SDK **8.0.318**, MSBuild **17.10.46**, runtime **8.0.21**. The lack of a standalone system SDK does not prevent Unity compilation.
- The external Android SDK is `C:\Users\jacob\AppData\Local\Android\Sdk`: platform APIs **34, 35, 36**, build-tools **35.0.0 and 36.0.0**, platform-tools **37.0.0**, command-line tools **22.0**, CMake **3.22.1**, NDK **r28c / 28.2.13676358**.
- Unity 6000.6.0f1 module metadata specifies OpenJDK **17.0.18+8** and NDK **r27c** for its Android support. These differ from the external JDK/NDK. The missing Unity Android module is the first build blocker; an arbitrary external toolchain override is not a verified replacement.
- PATH JDK resolves to `C:\Program Files\Eclipse Adoptium\jdk-21.0.7.6-hotspot\bin\java.exe`. Registry evidence additionally lists Oracle JDK **23.0.1** and **24.0.1**.
- Local package evidence: Test Framework **1.8.0** depends on NUnit **2.1.0**; URP **17.6.0** and uGUI **2.6.0** are embedded. The editor also bundles `com.unity.inputsystem-1.20.0.tgz`; the template's **1.19.0** declaration remains a separate version observation. The committed project manifest and resolved package lock will determine the actual project version.
- `%LOCALAPPDATA%\Unity\licenses\UnityEntitlementLicense.xml` exists. The licensing client log reports a successful refresh and parse at **2026-09-09 13:22:08 UTC**. This is encouraging license evidence, not proof of successful editor import or build. No license credentials or license contents are copied into the repository.

## Verified installation blocker and existing-editor fallback

The first actual Unity 6000.6.0f1 batch import failed with **exit code 1**. The retained ignored log is `TestResults/phase1-import.log`; it reports a missing local Package Manager server executable and failure to load the ASTC compressor. This supersedes the initial assumption that the newest installation was usable.

Read-only file checks establish:

| Relative path within Editor/Data | 6000.6.0f1 on C: | 6000.3.23f1 on D: |
| --- | --- | --- |
| `Resources/PackageManager/Server/UnityPackageManager.exe` | Missing | Present, 95,112,112 bytes |
| `Tools/astcenc-avx2.dll` | Missing | Present, 334,256 bytes |
| `PlaybackEngines/windowsstandalonesupport` | Missing | Present, including Windows x64 Mono players |

The 6000.3 Windows x64 development Mono `UnityPlayer.dll` is present at 84,343,728 bytes. File presence alone does not prove a build; subsequent phase reports must record actual editor/test/build results.

Decision: use the **already installed 6000.3.23f1_09d2ecc7fb28** editor under the user's explicit genuine-blocker exception. Do not reinstall, repair, copy editor binaries between versions or install modules. No existing playable project was downgraded: the new bootstrap had failed before initial import. Preserve the 6000.6 bootstrap settings in ignored `.tools-cache/bootstrap-6000.6/`, replace project render settings from the 6000.3 editor's own template, and pin compatible local packages.

Working package targets are URP **17.3.0**, Test Framework **1.6.0** (NUnit **2.0.3**), uGUI **2.0.0**, and Input System **1.20.0**. The Input System archive declares minimum Unity **6000.0**, and is bundled in both installations. The 6000.3 URP template archive is `com.unity.template.3d-cross-platform-17.0.14.tgz`; its older manifest versions are separate from the editor's newer embedded package versions. Use its compatible serialized bootstrap settings with the selected embedded package versions, then verify actual resolution.

Android/iOS platform support remains absent in the fallback editor. Windows Mono development builds are the available local player verification path. The batch scripts now default to the 6000.3 editor and accept explicit overrides.
