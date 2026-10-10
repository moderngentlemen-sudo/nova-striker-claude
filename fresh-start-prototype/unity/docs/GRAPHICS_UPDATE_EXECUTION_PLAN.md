# Nova Striker Unity — graphics and level update execution plan

Prepared 2026-10-10 UTC. Repository: `moderngentlemen-sudo/nova-striker-claude`.
Work branch: `nova-striker-unity-sol-6-1`.
Audited baseline: `480d6c2488f453cf64e6507a49288dbaf23306be`.

## 1. Assignment and precedence

Complete and validate the existing Unity graphics and level-feature implementation. Deliver richer, physically convincing combat effects; efficient world geometry; Blender-built enemies and environment kits; realistic white birds; background-only clouds with convincing volume; detailed stages; configurable effects; and playable depth lanes, vertical tiers and hazards.

This document is the active execution plan on the work branch. `NEXT_UPDATE_PLAN.md` is an earlier design reference, not an accurate remaining-work checklist. Follow current code where it differs from that reference, except where this plan explicitly calls for a change. The owner's current branch instruction overrides inherited instructions to work on the source branch. Unity is the primary platform; future Unreal development is a separate project. Browser gameplay parity is no longer an acceptance requirement.

This commit is planning only. The phases below describe the subsequent implementation. No instruction can guarantee error-free work: small changes, reproducible checks and actual Unity review are required before declaring completion.

### Execution contract

1. Fetch the remote and verify the work branch before editing. Preserve any intervening work. Never force-push, reset another person's work, or modify the sibling branches. Do not open a PR unless requested.
2. Read this file, `CLAUDE.md`, `README.md`, the relevant implementation files, and any newly added `AGENTS.md` before each affected phase.
3. Preserve the existing game, characters, controls, encounters and asset hooks. Extend the current code rather than introducing a second effects or level system alongside it.
4. Keep the premium animated-cinematic art direction: readable silhouettes, deliberate materials and physically convincing motion, light, smoke and debris. Realistic effects do not require photorealistic characters or indiscriminate screen clutter.
5. Keep simulation deterministic and independent of rendering quality. Graphics settings must not change damage, hitboxes, movement or hazard timing. Gameplay features may intentionally diverge from the browser.
6. Work phase by phase. Finish the narrow acceptance checks, record evidence, and make a descriptive commit before the next phase. A blocked Unity check may permit independent authoring to continue, but never counts as a pass.
7. Do not upgrade the engine or switch render pipelines as part of this update. Keep `6000.3.25f1` and URP `17.3.0` as the clean-build baseline. The inherited notes report the owner's editor as `6000.6.2f1`; verify compatibility there separately rather than claiming it from a 6.3 compile.
8. Keep gameplay scope to lanes, tiers, hazards and the AI/camera/combat fixes necessary for those systems. Preserve character designs, shield colours and unrelated combat tuning.
9. Runtime assets must be reproducible from tracked inputs. No dependency on a developer's untracked Unity scene, local model, generated material or absolute filesystem path.

## 2. Baseline: what exists and what remains uncertain

Paths below are relative to `fresh-start-prototype/unity/` unless specified otherwise.

| Area | Source evidence at the audited commit | Required next work |
|---|---|---|
| Render pipeline | URP 17.3; setup in `Assets/NovaStriker/Editor/NovaSetup.cs` | Verify a clean setup, shader compilation, actual renderer settings and target builds |
| Geometry | `View.cs` merges meshes into 48 m world-XZ cells and uploads baked meshes without a CPU copy | Measure culling, oversize bounds, memory, LOD and rebuild ownership; do not reimplement chunking as if absent |
| Effects | `Game/View/Vfx/`, `Fx.Cine.cs`, PNG-backed `.bytes` textures, preset UI | Audit every attack family, lane position, duplicate reaction, settings transition and resource lifetime |
| Budgets | `FxCfg.MaxParticles` advertises 6000/3000/1200; `Vfx.BuildLayers` independently assigns layer capacities | Implement an aggregate budget; include weather and legacy emitters in measurements |
| Lane placement | Player/projectile visuals have depth offsets; many `Fx.Cine.cs` impact/blast helpers use fixed depth | Carry event depth through every affected effect and attachment |
| Clouds | `CloudLayer.cs` renders lit billboard clusters, with centers behind the local play plane | Verify entire cloud bounds, curved routes, camera changes, transparency and batching; these are impostors, not ray-marched volumes |
| Birds | Blender bird model, shader deformation and flock motion are present | Verify size, anatomy, orientation, lighting, pauses and camera-relative motion |
| Enemy models | Eleven `enemy_*.json` exports and associated Blender builders exist | Validate rig-node mappings and render/animate each enemy; file presence is not visual approval |
| Environment kits | `Env/gym_kit.json` and gym authoring/export scripts exist | Build kits for the other five zones and dress both boss encounter spaces |
| Level features | `Sim/LevelFeatures.cs`, combat lane filters, extra platforms and `LevelFx.cs` exist | Finish depth-aware geometry, collision semantics, AI navigation, telegraphs and reset behaviour |
| Tests | `LevelTests/Program.cs` exists but its `.csproj` is absent; `.gitignore` ignores it; parent project excludes ShieldTests but not LevelTests | Repair test discovery before trusting the test suite |
| Test results | Shield and level test programs print failures but have void Main methods without failure exit codes | Make failure return a nonzero process status |
| Build automation | Root `.github/workflows/unity-build.yml` targets Windows/macOS/Linux; missing licence secrets can skip actual builds | Require a real build step and downloadable artifact, not merely a green workflow |
| Runtime verification | Inherited docs explicitly say presentation has not been run in Unity | Establish a fresh baseline; no runtime validation was performed while preparing this plan |

Other inherited-plan corrections: textures are PNG data in `<name>.png.bytes`, not a custom NSFX header; the type-check bootstrap needs an initial package compilation before `--only`; its player output directory must be a real separate argument, not the typo `out--player`. Check the current helper's CLI before using it.

## 3. Rendering architecture and feature choices

Use Unity's capabilities where they improve the result and survive profiling. Keep URP for this update. Use lit materials, normal/roughness detail, Forward+ where supported, bounded effect lights, soft particles, mesh particles, trails, bloom, SSAO, spatial culling, instancing, LOD and appropriate reflections. Avoid multiple tone-mapping passes: `Grade.shader` already applies the game's grading and tone response.

The baseline particle system remains the production backend. The older plan's blanket exclusion of VFX Graph/Shader Graph should not be mistaken for a Unity limitation. An optional VFX Graph evaluation is allowed only after the core update works: use a version compatible with the pinned editor, create/import graphs in a real editor, preserve GUIDs, and prove a visible/performance advantage with a fallback. Do not hand-invent serialized graph files. This evaluation must not block the requested effects.

Cloud delivery has two quality paths: improved lit impostors for Balanced/Low, and a bounded custom volumetric-cloud renderer for High/Ultra if it passes the renderer compatibility and GPU budget checks. Native HDRP volumetric clouds cannot simply be enabled in the current URP project. Implement the custom volume using a tested URP renderer feature and a seeded 3D density texture; sample opaque depth, bound ray steps and render only the backdrop volume. Begin at half resolution and composite before foreground transparency. Verify the pinned URP Render Graph integration in package source before coding. If this path is blocked, keep the improved volume-like impostors and explicitly mark true volume rendering incomplete, with the blocker documented.

For projected scorch/scuff marks, prototype one URP DecalProjector using a verified decal material and renderer feature. Keep pooled surface-aligned quads as the safe fallback where projected decals are unavailable. The label in the menu must describe the actual backend.

Reference documentation, checked during planning (recheck against installed packages during implementation):

- [Unity 6.3 render-pipeline feature comparison](https://docs.unity3d.com/6000.3/Documentation/Manual/render-pipelines-feature-comparison.html).
- [Unity 6.3 Graphics.RenderMeshInstanced](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Graphics.RenderMeshInstanced.html): per-call capacity depends on instance data; the usual two-matrix path permits 511. Use conservative batches of at most 500 initially and test on target APIs. Do not use `assumeuniformscaling` for nonuniformly scaled cloud puffs.
- [HDRP 17.3 volumetric clouds](https://docs.unity.cn/Packages/com.unity.render-pipelines.high-definition%4017.3/manual/create-realistic-clouds-volumetric-clouds.html): reference for the distinction between a real volume and the current impostors, not an instruction to migrate pipelines.

## 4. Shared contracts to establish before art expansion

### Coordinates and effect ownership

Simulation uses route x and height y, plus lane state. `Level.Frame(x)` defines the curved route. `S.W(x,y,depth)` returns the game's right-handed scene coordinates. `TObj` converts these to Unity coordinates; raw Unity APIs need one conversion with `Th.P`. Existing `FxLayer.Emit` already converts both position and velocity: never convert twice.

Create one documented effect-origin contract carrying route position, resolved depth, direction, impact normal, surface type and owner/team where available. Use event snapshots when an actor/projectile can disappear before rendering. Snapshot shot lane at emission; a shooter changing lanes must not drag an existing projectile into another lane. Specify local/world velocity and rotation conversions. Cover negative-scale transforms, route curves and one known test marker in each coordinate system.

Do not introduce UnityEngine types into `Sim`. A new primitive event field for depth is permitted when presentation cannot safely recover it. Such changes must preserve simulation timing and avoid drawing random numbers solely for presentation.

### Quality and readability

Keep the current settings keys wherever possible. The desired starting budgets are design targets, not measured performance claims:

| Limit | Cinematic/High | Balanced | Low graphics |
|---|---:|---:|---:|
| Live decorative particles, aggregate | 6000 | 3000 | 1200 |
| Transient effect lights | 8 | 4 | 0 |
| Active decals | 32 | 16 | 0 |
| Small enemy LOD0 triangles | 3000 | 3000 | use cheaper LOD |
| Heavy enemy LOD0 triangles | 7000 | 7000 | use cheaper LOD |
| Boss LOD0 triangles | 18000 | 18000 | use cheaper LOD |
| New environment dressing per zone at LOD0 | 80000 | same source assets | reduced visible detail |

Measure all decorative emitters, including legacy sparks, weather, trails and mesh debris. Reserve capacity for player feedback; shed ambient weather/smoke before critical hit cues. Hazard warning shapes and projectile cores remain visible regardless of decorative budgets. Any intentionally separate category must appear in telemetry, with its own cap.

Target a stable 60 fps at 1080p on the actual recorded test machine. Record GPU, CPU, RAM, API, resolution, preset and build commit before claiming a performance result. Use CPU/GPU frame times, p95 frame time, visible triangles, draw calls, SetPass calls, allocations, particle counts and memory; FPS alone is insufficient. Budgets may be tuned with measured evidence.

## 5. Ordered implementation phases

### Phase 0 — restore trustworthy verification

**Files:** `.gitignore`, `SimTests/SimTests.csproj`, both test programs, proposed `SimTests/LevelTests/LevelTests.csproj`, build workflow only if necessary.

1. Reconfirm the baseline SHA and record available tools: .NET 8, Node, Python, Blender and licensed Unity. Read the type-check helper's setup instructions. Do not assume tools or credentials exist.
2. Add the missing LevelTests project using the ShieldTests project as a pattern. Allowlist it in the Unity `.gitignore`. Exclude `LevelTests/**` from the parent trace project to avoid multiple entry points; also exclude future independent test projects.
3. Make both test suites exit nonzero on failed assertions. Make shell wrappers propagate compiler/runtime failures and retain logs. Prove an intentional temporary failing assertion fails the command, then remove it.
4. Compile editor and player assemblies. On a fresh type-check output folder, compile required packages before filtering to project assemblies. Inspect the NovaStriker assembly results, not just the tool exit code.
5. Run an actual Unity setup/build when available. Inspect steps and artifacts when CI skips for missing licences. Do not request secret values in chat.
6. Create `docs/GRAPHICS_UPDATE_STATUS.md` containing phase, commit, changed files, checks, evidence locations, blockers and exact next task. Keep 'authored', 'compiled', 'headless-tested', 'Unity-played' and 'visually-reviewed' as separate states.

**Acceptance:** test projects are independently runnable; failures fail the process; baseline failures are reproduced and recorded. No graphics feature is marked complete from a C# type-check alone.

### Phase 1 — a representative playable slice and depth contract

**Files:** `Sim/LevelFeatures.cs`, `Types.cs`, `Combat.cs`, `PlayerSim*`, `Enemies.cs`, `Bots.cs`, `World*`, `Game/View/Space.cs`, `View.cs`, `FxSync.cs`, `Vfx/LevelFx.cs`, `Input/Controls.cs` as needed.

1. Use the Movement Gym's existing lane stretch and one upper platform as the reference slice. Demonstrate player movement, one enemy, one projectile, one explosion, one hazard and a lane transition before scaling the change across stages.
2. Retain three discrete playable lanes: back/middle/front. This is 3D lane gameplay along a side-view route, not unrestricted 3D movement. Retain the current 1.4 m spacing and ten-tick hop initially; revise only when collision/readability testing justifies it.
3. Give platforms, blockers and hazards explicit lane masks where needed, defaulting legacy geometry to all lanes. Apply masks consistently to grounding, swept collision, one-way platforms, wall moves, shots, targeting, revive/pickups and damage queries. Audit every query site before introducing lane-specific decks.
4. Reject a lane hop with no safe destination support or one obstructed by solid geometry. Define airborne transitions, downed states, corner cases at lane-strip ends and checkpoint respawns. Validate the automatic return to middle lane rather than letting it cross a wall.
5. Use the lane-depth event contract for muzzle flashes, hits, shields, trails, beams, boss attacks, pickups and decals. Test a shot fired just before, during and after a hop. Area attacks must have an explicit reach policy; preserve the current all-lanes policy initially, with matching visible coverage.
6. Make deck widths and collision agree. Outer lanes must lie on visible supporting geometry with safe margins. Mark lane entries/exits and communicate a blocked hop. Camera framing must retain all human players and upper/lower tiers without exposing backdrop content as play space.
7. Add headless tests for obstructed transitions, shot lane lifetime, same-lane vs area damage, exits, respawns and setting-off behaviour. Test controls on keyboard and gamepad.

**Acceptance:** complete the reference slice with Nova, Echo, RAM and Fix; effects line up with the hit location on all lanes; no invisible floor, wall bypass or wrong-lane hit. Automated tests cover the relevant rules before stage expansion.

### Phase 2 — geometry, lighting and lifecycle foundation

**Files:** `View.cs`, `Landmarks.cs`, `GymDressing.cs`, `Look.cs`, `PlanarReflection.cs`, `Three/Geo.cs`, `Three/TMat.cs`, `Editor/NovaSetup.cs`.

1. Preserve the existing 48 m spatial bake. Profile its actual bounds and culling. Split long meshes crossing multiple cells where profitable; do not replace working world-space cells solely to match the older route-cell proposal.
2. Exclude moving, destructible, collapsing and stateful geometry from permanent static batches. Give each object exactly one visual owner; inspect whether extra tier/collapse boxes are drawn by both BuildLevel and LevelFx. Rebuild relevant geometry/colliders when feature settings or level versions change.
3. Instance genuinely identical repeated meshes/materials; use spatial batches and safe instance counts. Add LODs to landmarks and environment kits, with appropriate shadow and small-prop culling. Do not batch all distant landmarks into one enormous bound.
4. Cache reusable meshes/materials. Document who destroys each runtime allocation. Avoid allocations in steady-state effects and flock updates; account for exceptions with profiler evidence. Verify ten resets/zone transitions do not accumulate meshes, materials, lights or orphaned Vfx roots.
5. Validate lighting under the actual pipeline: sun, ambient/reflection lighting, contact shading, metallic/rough surfaces, emissive accents and bloom. Add probes or baked lighting only through a reproducible generation/bake path; runtime-generated geometry must not depend on missing baked assets.
6. Tune reflections by quality and camera visibility; avoid reflecting the whole expensive backdrop at full resolution by default. Compare anti-aliasing choices using the supported pinned pipeline settings; inspect particles and animated birds for ghosting.

**Acceptance:** matched camera captures and profiler measurements before/after; readable silhouettes; no missing geometry at cell/LOD boundaries; stable resource counts after resets. Report actual improvement, including any GPU cost of extra detail.

### Phase 3 — comprehensive combat VFX and pause-menu controls

**Files:** `Vfx/*`, `FxEvents.cs`, `FxSync.cs`, `BeamFx.cs`, `ChargeFx.cs`, `SubFx.cs`, `RamFx.cs`, `UltFx.cs`, `Fx.cs`, `Config.cs`, `UI/UiMenus.cs`, `Input/Controls.cs` settings persistence, shaders and templates.

1. Build an effect inventory from all emitted event names, projectile kinds and sustained-effect owners. Record handler, visual family, lane source, classic/additive/replacement policy, cleanup and test scenario in `docs/VFX_COVERAGE.md`. Unknown events may not silently disappear.
2. Cover at minimum: Nova's normal/charged attachments, perfect release, sustained beam, scatter, grenade/cluster, chain lightning, returning disc and gravity well; Echo's ranged attacks, rifle, snares, blades and glaive; RAM's cannon, shield, slams and stored-energy release; Fix's weapons, deployables and rockets; every enemy/boss projectile, explosive, death and armour break; ultimate/team attacks; impacts, deflections and breakables.
3. Make effects distinct by physical cause: explosive fire/smoke/debris, plasma/energy rings, electric arcs, ballistic tracers/ricochets, hard-light shards, gravity distortion and material-specific destruction. Do not turn every hit into a fireball. Preserve team/weapon colours and damage telegraphs.
4. Use a layered blast envelope: immediate light/flash, outward hot core, pressure ring, directional debris/sparks, rising smoke and fading surface mark. Scale timing and size with the actual attack. Orient impacts with their normal, and use appropriate floor/wall materials.
5. Implement global particle admission/budgeting and rate-over-time emitters. Avoid emission tied directly to render-frame count: equivalent ten-second scenarios at 30/60/120 fps should produce comparable density. Prevent duplicate hits when a new effect and a legacy handler both react.
6. Resolve effects independently in Custom mode: choosing Classic explosions must not globally disable an independently selected energy projectile style. Audit `FxCfg.Classic` and the early return in `CineEvent` specifically. When a feature is Off, verify its emitters actually stop; critical telegraphs remain.
7. Keep presets Cinematic/Balanced/Classic/Custom. Support explosion style, projectile style, trail length, particle density, smoke, debris, lights, distortion, decals and screen effects. Expose clouds, birds, weather and enemy models in readable sections. Use clear labels for effective quality caps, not controls that appear broken.
8. Add a reduced-screen-effects option covering shake, flashing and distortion without removing gameplay warnings. Persist valid settings, migrate missing/invalid values, and keep keyboard/gamepad navigation usable. Changing an individual effect can switch to Custom while preserving current effective values.
9. Apply visual settings immediately and safely, including existing projectiles and enemies. If an option truly requires a zone reload, say so visibly and queue it consistently. Pause must freeze the intended effects without making instanced birds/clouds vanish; drawing and time advancement must be separable.

**Acceptance:** every inventory row has a reviewed trigger; side-by-side preset captures; no stale lights/trails after death, pause, restart or option changes. Performance telemetry stays within the aggregate budget under a four-character combat stress scene. All settings survive restart.

### Phase 4 — background clouds and white gulls

**Files:** `Vfx/CloudLayer.cs`, `Vfx/Birds.cs`, `Ambience.cs`, `Shaders/Cloud.shader`, `Bird.shader`, Blender bird scripts; optional new backdrop-volume renderer.

1. Improve cloud silhouettes with layered density, varied scale, sun-facing highlights, shaded interiors and gradual atmospheric falloff. Use deterministic atlas variants. Fix obvious repeating billboards, sorting seams and harsh intersections before adding more particles.
2. Enforce backdrop-only placement using full rendered bounds, not just cloud centers. Keep all cloud geometry at least the intended 70 m behind the playable envelope, including outer lanes, upper tiers, curved-route bends and maximum camera offsets. Re-evaluate after camera mode/FOV changes and teleports. Use a dedicated backdrop mask/composite if route-space separation alone cannot guarantee foreground occlusion.
3. Preserve correct opaque depth. Clouds may not cover player silhouettes, platforms, reticles or hazard telegraphs. Clamp/reseed after large teleports rather than relying on one wrap subtraction. Test both Classic and the improved cloud modes.
4. Implement the High/Ultra volume path described in section 3 only with a real shader/runtime validation loop. Measure ray-step cost and temporal artifacts. Keep weather mist/smoke separate from background clouds and independently controllable.
5. Refine the Blender gull: mostly white body/head, pale grey wing shading, restrained dark tips, recognizable beak/tail and anatomical wing shape. Verify actual exported dimensions before applying runtime scale; aim around the prior 1.3 m wingspan rather than compensating for distance with giant birds.
6. Animate wing roots and tips with coherent flaps, long glides and smooth banking. Derive heading from flock motion rather than camera relocation; initialize previous positions and reset them on teleport. Keep individual phase variation and loose separation. White plumage must remain readable under daylight without blowing out.
7. Keep birds behind the playable envelope, including deformed wing bounds. Expand mesh bounds for shader animation. Provide a missing-resource/unsupported-instancing fallback. Produce a Blender turnaround and a Unity motion clip.

**Acceptance:** a full traversal, reverse camera movement, curved-route turns, all FOVs, pause, boss framing and checkpoint teleports show no cloud in the play area; birds stay visible while paused and resume smoothly. Record captures in every open-sky zone and a populated stress case.

### Phase 5 — Blender environment kits and stage detail

**Files:** `Art/Blender/build_gym_kit.py`, `gym_layout.py`, `export_kit.py`, `render_gym.py` as patterns; proposed shared stage kit/layout/export modules; `Resources/NovaStriker/Env/`; proposed `Game/View/StageDressing.cs`; `Landmarks.cs`, `Vfx/Weather.cs`, `LevelFx.cs`.

1. Establish a shared kit schema and material vocabulary. Extend the proven gym pipeline. Add one narrow, read-only simulation layout exporter if needed, capturing real boxes, route transforms, lane masks and tier/hazard locations. Do not manually reconstruct approximate stage coordinates in Blender.
2. Author the five remaining kits below, one at a time. Export only runtime geometry/material data to Resources. Store source scripts and required source assets in the repository; preserve editable Blender sources or reproducible builders. Use Git LFS only after changing CI's current `lfs: false` checkout and verifying downloads.
3. Separate decoration from gameplay collision and moving hazard machinery. Place lanes and tier structures from the same layout data as simulation. Preserve jump arcs, aiming lines, checkpoint space and boss-camera visibility.
4. Build distant landmarks with LODs: reactor core, furnace dome, cooling tower, transit train and city blocks. Add surface detail through normals/roughness/decals where extra geometry does not improve the silhouette.
5. Produce a wide stage render, a gameplay-angle render and a close detail render for each new kit; then obtain matching Unity captures. Blender renders are art review, not proof of in-game rendering.

| Zone / ID | Required structural and surface detail | Atmosphere and movement | Gameplay integration |
|---|---|---|---|
| Movement Gym / `gym` | Preserve existing panels, grip strips, rails and markings; resolve clipping/LOD | Sign ticker, distant trainees or maintenance drones where practical, responsive flags | Safe introduction to tiers/lanes and a clearly telegraphed vent |
| Concourse Lock / `arena` | Glass balustrades, holo columns, gate hardware, lane markings, mezzanine supports | Sweeping floodlights, restrained holo dust, transit activity | Shock plates and laser emitters; a readable Lockwarden arena |
| Storm Spire Climb / `tower` | Wet grating, ledge brackets, cable trays, lightning rods, storm shutters | Rain/splashes, mist, anemometers and controlled lightning | Vertical routes with safe landings and visible strike plates |
| Skyline Relay / `skyline` | Relay dishes, antenna arrays, gantries, bridge supports, windsocks | Background cloud banks, distant airships/pods with lights, wind | Collapsing sections, wind and lasers; clear Stormcaller arena sightlines |
| Helix Foundry / `foundry` | Furnaces/vents, crucibles, pipes/valves, crane rails/hooks, catwalks, striped machinery | Steam, embers, local heat shimmer, moving fans and machinery | Crushers, slag and vents; readable multi-tier helix routes |
| Undercity Descent / `undercity` | Fire escapes, balconies, neon, wet paving, crates, cables, transit and cooling infrastructure | Drips, low haze, restrained neon flicker and distant movement | Shock grates, debris clusters and meaningful upper/lower routes |

The six rows are the actual baseline zones. `warden` and `beacon` are encounter/test destinations, not two additional zone kits. Do not substitute old world-codex regions for current level IDs.

Carry forward the earlier detail requests represented in the inherited plan: impact-reactive deck lights, dash-reactive flags, scuffs, material chips/sparks, the gym ticker, distant traffic and per-zone weather. Add railing motion, mat compression or distant trainees only where they fit the kit and measured budget; record those as optional dressing rather than silently claiming them done.

**Acceptance:** all five new kits load from a clean checkout; six zones and both boss spaces have reviewed Unity captures; decoration does not create an invisible blocker or obscure a required route. Weather is localized, quality-scaled and removed correctly on transition.

### Phase 6 — enemy and boss asset completion

**Files:** all `Art/Blender/build_enemy_*.py`, `build_enemy_lib.py`, `export_enemy.py`, `render_turnaround.py`, `Game/View/EnemyModels.cs`, `EnemyRigs.cs`, exported model JSON.

1. Audit every required type: `swarmer`, `shield`, `sniper`, `brute`, `post`, `turret`, `drone`, `mortar`, `charger`, `warden`, `stormcaller`. Improve existing models rather than regenerating blindly.
2. Validate finite vertices/normals, valid triangle indices, UV lengths, nonzero bounds, material slots, triangle budgets and node names. Preserve the `<node>__<part>` contract and model each piece in the rig node's local coordinates.
3. Preserve armour plates, tubes, horns, shields, gun pivots and rotating parts that animate or change visibility. Test damage/flash materials, armour breaking, muzzle locations, aim limits, death and every boss attack. Mesh replacement must happen before outlines are generated.
4. Maintain a coherent hostile ceramic/graphite/magenta design language with distinct role silhouettes. Add LODs for repeated small/heavy enemies. Keep hit volumes consistent with readable bodies.
5. Render all eleven turnarounds at consistent lighting/scale, plus a lineup. Review in-game stills and motion for each; a polished Blender still does not establish that the rig animates correctly.
6. Verify the model toggle changes existing as well as newly spawned enemies, and that missing/corrupt exports fall back without repeated log spam or invisible enemies.

**Acceptance:** eleven valid exported models and eleven reviewed turnarounds; all in-game animation/state checks pass; no material leak on respawn or model toggling.

### Phase 7 — finish tiers, hazards and AI across all stages

1. Expand the Phase 1 lane contract to every marked stretch. Make upper/lower routes useful alternate combat/traversal choices, with safe returns, rather than disconnected decorative shelves.
2. Build reachability from actual movement capabilities and collision data. Support AI teammates reaching the human's tier/lane, navigating safe transitions, avoiding imminent hazards and reviving without repeatedly entering danger. Avoid teleport-based fixes except the game's explicit recovery system.
3. Cover vent, shock, laser, lightning, crusher, slag, wind, debris and collapse hazards. Document idle/warning/active/recovery states, damage cadence, lane coverage and reset rules. Persistent hazards need continuous readable boundaries. No dependence on decorative particles for warnings.
4. Match visible machinery to the damaging volume. In particular, inspect crusher damage timing versus its descending head and debris damage timing versus impact. Handle invulnerability, knockback, air states, multiple occupants and enemy interaction explicitly.
5. Keep collapsing platforms and particle colliders synchronized: no ghost support, invisible collider or permanent baked duplicate after collapse. Reset their state correctly on checkpoint, restart and disabling the feature.
6. Treat level toggles as pending until the documented zone-reload boundary. Rebuild simulation, render geometry and collision together. Test all eight combinations of tiers/hazards/lanes.
7. Add focused tests for every hazard kind, collision masks, damage windows/cooldowns, reset, collapse restoration, AI safe routes, revive access and deterministic replay. Do not overfit tests to one hard-coded showcase position.

**Acceptance:** complete every zone and both bosses with one human plus three bots, and test available local multiplayer configurations. No progression softlock, unreachable mandatory enemy, repeated bot hazard deaths or disagreement between warning and damage.

### Phase 8 — integration, evidence and delivery

1. Run the regression matrix below. Fix blockers and repeat only affected checks. Keep the checklist honest about missing hardware/editor access.
2. Update `README.md`, `CLAUDE.md`, `GRAPHICS_UPDATE_STATUS.md` and VFX coverage to describe actual behaviour and current limitations. Remove stale claims that all models are placeholders, browser parity is mandatory, or all visuals remain untested after they have been reviewed.
3. Commit and push only to the work branch. Inspect the workflow associated with that exact commit, not an unrelated latest run. Require actual setup/build steps and artifacts. Compile all enabled desktop targets; playtest Windows first and record platform smoke-test gaps.
4. Deliver a Windows artifact link if produced, setup requirements, plain numbered launch steps, captures and a concise change list. Keep source/render evidence associated with its commit. Do not claim the build is ready to play when CI merely skipped.

## 6. Verification matrix and release gate

After Phase 0 repairs, use these explicit commands from the Unity project directory; substitute the discovered .NET executable if it is not on PATH:

```sh
dotnet run --project SimTests/ShieldTests/ShieldTests.csproj
dotnet run --project SimTests/LevelTests/LevelTests.csproj
dotnet build SimTests/SimTests.csproj
```

Run `bash parity.sh` from `SimTests/` as an informational legacy comparison. It may expose accidental changes, but Unity-specific intentional differences are acceptable. A deterministic Unity test suite is the authority for new gameplay. Perform editor/player type-checks using the current `SimTests/typecheck/README.md`, followed by actual Unity import, shader compile and player build.

| Test family | Required coverage | Evidence |
|---|---|---|
| Clean import/build | Pinned editor, generated templates/pipeline/scene, all enabled desktop targets | Exact commit, build logs and artifacts; skipped is not passed |
| Compatibility | Owner's reported 6.6 editor plus pinned 6.3 build | Distinct results; no inferred compatibility claim |
| Combat/effects | Every VFX inventory row, every character kit, enemy/boss type, all lanes | Trigger checklist and representative clips |
| Presets | Cinematic/Balanced/Classic/Custom; every individual toggle; Low caps | Captures, persistence checks and measured counts |
| Depth/tiers | Curves, lane entry/exit, blocked hops, jumps, dashes, wall moves, shots, revive | Headless tests and playable traversal |
| Hazards | Every kind, every state, each lane mask, resets and all feature combinations | Deterministic tests and warning/damage comparison |
| Art | All eleven enemies; five new stage kits; existing gym; bird | Blender sheets plus Unity stills/motion |
| Backdrop | Cloud bounds, camera extremes, routes, pause, teleports, boss cameras | Debug-bound capture plus normal rendered clip |
| Lifetime | Ten consecutive restarts/zone changes; repeated settings cycles | Stable retained counts/memory after cleanup |
| Performance | Fixed scenarios, warmup, 60-second samples, baseline vs update | Hardware/API/resolution and CPU/GPU/p95 metrics |

The update is complete only when required art is present, all requested gameplay is traversable, effect controls function, shaders/builds pass, and Unity captures substantiate the graphics improvements. Record unresolved blockers explicitly; never substitute asset-file existence or a static compile for visual verification.

## 7. Copyable implementation handoff

> Continue Nova Striker Unity in `moderngentlemen-sudo/nova-striker-claude`, branch `nova-striker-unity-sol-6-1`. Read `fresh-start-prototype/unity/docs/GRAPHICS_UPDATE_EXECUTION_PLAN.md` and any status file first, then inspect the current remote. This is a completion and validation pass over existing graphics and level-feature work. Execute Phases 0–8 in order, beginning with the broken level-test project setup and failure exit codes. Preserve completed assets and mechanics; implement the documented effects, geometry, cloud/bird, Blender stage/enemy, lane/tier/hazard and AI improvements. Unity is the primary platform and browser parity is informational. Use the pinned Unity/URP versions, document exact checks and runtime evidence, and push incremental verified commits only to this branch. Continue through all feasible phases without routine approval requests. If a required editor, licence, graphical runtime or dependency is unavailable, record the exact blocker, finish independent work, and distinguish authored/compiled/tested/reviewed status. Do not claim an error-free or visually validated build without the corresponding evidence.
