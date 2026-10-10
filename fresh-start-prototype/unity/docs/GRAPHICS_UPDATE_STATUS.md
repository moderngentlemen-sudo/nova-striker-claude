# Graphics and level update status

Work branch: `nova-striker-unity-sol-6-1`. Implementation baseline: `8895f1e006ba5cc0894c177b1ea200c9cb3641b9`. See the phase commits in this branch for exact changed-file sets. This record distinguishes implemented code, automated checks and observed Unity results.

## Verification states before the update CI run

| Phase | Authored | Compiled / headless tested | Unity played / visually reviewed |
|---|---|---|---|
| 0 — verification | Independent projects, nonzero exits, failure probes, asset audit, build gate repaired | 21 shield + 70 level assertions pass; both injected failure probes fail the child process; trace builds; editor/player assembly checks pass | Baseline licensed setup/build succeeds for all desktop targets; update run pending |
| 1 — lane slice / origins | Collision/query masks, safe hops/exit, snapshot shot lane/depth, lane-aware beam/charge/well/chain and revive | Four characters, blocked/unsupported hops, wall/solid masks, immutable shots, respawns and deterministic quality replay tested | Human traversal, keyboard/gamepad and full sustained-effect depth review pending |
| 2 — geometry / lifecycle | Preserve 48m bake; split long straight boxes; wider visible decks; spatial instanced stage kits; lower reflection resolution/backdrop exclusion; enemy material disposal | Assembly checked; kit bounds and budgets validated | Matched captures/profiling, culling/LOD boundaries and retained-count review pending |
| 3 — effects / controls | Aggregate native/legacy/weather admission; ambient reservation; independent Custom values; Off transitions; reduced screen effects; fixed-rate classic/modern projectile emission; event catalog | Source inventory has 212 literal events; settings parsing and assembly checks pass | Every trigger, persistence, density at 30/60/120 fps and stress budget review pending |
| 4 — backdrop / birds | Full-bound curved-route exclusion, safe instancing, pause drawing, teleport wrapping; anatomically refined 1.3m gull | Valid exports; Blender gull render reviewed; shader/player check pending | Real cloud volume path is not authored; full-FOV, camera and bird motion review pending |
| 5 — five stage kits | Arena/tower/skyline/foundry/undercity kits, simulation layout exporter and instanced runtime loader | Clean reproducible builders; every new zone <=80k placed LOD0 triangles; 15 Blender stage images reviewed | Six zones and both boss spaces need Unity captures; landmark LODs/optional trainees remain open |
| 6 — eleven enemies | Existing designs preserved, model fallback validation, reduced distance/Low exports, existing-enemy toggle | All eleven LOD0/LOD1 exports pass; eleven four-angle turnarounds and role portraits reviewed; mortar reduced to 2806 triangles | Rig nodes, every attack/armour state, LOD transitions and motion require Unity review |
| 7 — hazard / AI integration | Deterministic support waypoints, hazard escape; complete baseline hazard families, solid warnings, impact timing/volume fixes and reset masks | 70 focused assertions include all eight feature combinations, all families, collapse restore, waypoints and empty-air impact exclusion | Full six-zone/boss human+3-bot completion, revive access and multiplayer remain open |
| 8 — delivery | Actual-build licence gate, simulation/asset CI, optional player review harness and evidence upload | Local checks below | Exact update builds/artifacts and player review pending |

## Checks and tools

- .NET SDK 8.0.425: standard `dotnet run --project SimTests/ShieldTests/ShieldTests.csproj` and `dotnet run --project SimTests/LevelTests/LevelTests.csproj` now execute successfully. Initial process-metadata restrictions required the tracked Roslyn fallback, `SimTests/headless.py`; it compiles the same sources. Deliberate failures were injected only in scratch source copies and removed after the probe.
- `dotnet build SimTests/SimTests.csproj`: succeeds. `bash parity.sh`: 0 match / 128 differ with new features enabled; first differences are intentional hazard warnings. This is recorded as informational, not a regression authority or an acceptance pass.
- Pinned Unity 6000.3.25f1 reference assemblies, URP 17.3.0, Input System 1.17.0/uGUI: NovaStriker.Sim, NovaStriker.Game and NovaStriker.Editor report `ok`. Initial package bootstrap was run for separate editor/player outputs. Unresolved optional package GUIDs in the approximate type-check are not project compile errors; CI performs the real import/shader check.
- Blender 4.2 official bpy / Python 3.11: all builders and source renders run. New placed triangles: arena 12340, tower 13696, skyline 30600, foundry 71856, undercity 76216. Existing gym 305940 is unchanged and outside the new-dressing target. All eleven enemy LOD0 budgets and all eleven cheaper LOD1 exports pass `validate_assets.py`.
- Local evidence: [checks](checks/), [Blender source sheets](art-review/), [effect origins](EFFECT_ORIGINS.md), [event inventory](VFX_COVERAGE.md).
- Baseline CI: [run 38023635837](https://github.com/moderngentlemen-sudo/nova-striker-claude/actions/runs/38023635837), commit `8895f1e`. All three real setup/build/upload jobs succeeded. Those baseline artifacts are not this update.

## Explicit remaining release gates

The full plan is not yet accepted. No local interactive Unity editor, owner's 6000.6.2f1 editor or reference desktop GPU is available. Licensed CI is available and must build this update. Its Linux player capture uses software OpenGL; captures and errors can establish rendering/runtime evidence but cannot establish target 60 fps performance or native Windows/macOS gameplay.

The safe production cloud backend is lit impostors. A true bounded ray-marched URP cloud feature remains unimplemented because it needs a real editor/Render Graph integration and GPU cost/artifact review. The menu names the actual backend. Decals remain pooled surface quads, with no projected wall decal claim. Some older aura decorative emissions still use render-frame probability; hazard emission is now fixed at 60 steps/second; some all-lanes deployable/snare/shockwave geometry still uses middle-depth fallback. Landmark LODs, full impact-normal/material projection, all effect trigger/persistence checks and full bot traversal remain open.

The optional player argument `--nova-review <absolute-output-folder> --nova-commit <sha>` captures six zones, two boss spaces, four presets, a pause image, eleven enemies, skyline motion and ten reset resource-count samples. It reports runtime errors and hardware/API/counters in runtime.json. It is an automated smoke/capture suite, not a completed playthrough. Run under a graphical display without Unity's `-batchmode` (end-of-frame capture needs rendered frames).

Next task: publish the verified phase commits only to the work branch, inspect the exact update's setup/build/artifact steps and player review. Fix every build/runtime blocker, compare the captures, then replace the pending CI rows with observed evidence and record remaining manual acceptance gaps.

## First update CI import and correction

[Run 38027361022](https://github.com/moderngentlemen-sudo/nova-striker-claude/actions/runs/38027361022), implementation commit `859de7a4981e98099299a83fd7a135292c45aeb8`: real simulation/asset job passed. Windows import failed with CS0103 in the optional review helper: ScreenCapture belongs to a module absent from this project’s manifest. The helper now reads the rendered framebuffer after end of frame using existing Core/ImageConversion modules. This is evidence of why the approximate assembly check cannot replace a clean Unity import. Subsequent build is required.

The correction also freezes shader/decorative hazard emission cadence, includes 150 density-scaled ambient motes in the particle ledger, suppresses residual screen waves/punch/bloom kicks in reduced-screen mode, and provides a white procedural bird fallback for missing/unsupported model rendering.
