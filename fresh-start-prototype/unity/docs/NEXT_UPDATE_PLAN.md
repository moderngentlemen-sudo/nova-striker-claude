# Nova Striker Unity: the next update (graphics, effects and level features)

A plan to execute phase by phase. It is written for an engineer (or coding agent) working in this repository with
no memory of earlier sessions. Follow it closely and in order. Where a step says "verify", run the check
and read the output before moving on.

The owner asked for:

1. Optimised world geometry.
2. Realistic effects for every blast and projectile.
3. 3D models, made in Blender, for every enemy and boss, and for the stages not yet dressed.
4. Birds that are white and look real.
5. Clouds with volume that stay in the background and never drift into the play area.
6. The detail increases for the other stages discussed earlier (dressing kits like the Movement Gym's, plus
   per-zone atmosphere).
7. A heavy focus on special effects and particles, with pause-menu options to cycle between effect styles.
8. 3D depth lanes, multi-tier levels and level hazards (gameplay).
9. Full use of Unity's graphics capabilities throughout.

---

## Part A. Rules that apply to every phase

### A1. Before you start

1. `git fetch origin nova-striker-unity-claude && git checkout nova-striker-unity-claude && git pull`.
2. Read `fresh-start-prototype/unity/CLAUDE.md` and `fresh-start-prototype/unity/README.md` in full. They
   describe the branch rules, how the owner tests, and the checks. This plan does not repeat them all.
3. Work only on `nova-striker-unity-claude`. Never touch `nova-striker-unity-astra` or
   `claude/wizardly-wozniak-r0q6no`. Do not open pull requests.
4. Never put an AI model name or identifier in code, comments, docs or commit messages.
5. End commit messages with the attribution lines your session asks for.

### A2. The checks (run all of them before every push)

From `fresh-start-prototype/unity/SimTests/`:

| Check | Command | Must print |
|---|---|---|
| Type-check, editor | see `typecheck/README.md`; the exact line is in A3 | `ok` for `NovaStriker.Sim`, `NovaStriker.Game` and `NovaStriker.Editor` |
| Type-check, player | the same with `--player` and `--only NovaStriker.Sim,NovaStriker.Game` | `ok` for both |
| Parity | `bash parity.sh` | `128 match, 0 differ` (or `N match, 0 differ`) |
| Shield tests | `cd ShieldTests && ~/.dotnet/dotnet run` | `ALL PASS` |
| Level feature tests (from Phase 7 on) | `cd LevelTests && ~/.dotnet/dotnet run` | `ALL PASS` |

If the typecheck folders (`typecheck/unity`, `typecheck/pkgs`, `typecheck/out`) are missing in a fresh
container, follow `typecheck/README.md` to set them up first. `dotnet` lives at `~/.dotnet/dotnet`; if it is
missing, install the .NET 8 SDK with Microsoft's `dotnet-install.sh --channel 8.0`.

### A3. The type-check command

```sh
cd fresh-start-prototype/unity/SimTests/typecheck
B=unity/Editor/Data/Resources/PackageManager/BuiltInPackages
ROOTS="pkgs/inputsystem/package pkgs/com.unity.burst/package pkgs/com.unity.mathematics/package pkgs/com.unity.collections/package pkgs/com.unity.searcher/package $B/com.unity.ugui $B/com.unity.render-pipelines.core $B/com.unity.render-pipelines.universal $B/com.unity.render-pipelines.universal-config"
python3 ucompile.py out $ROOTS ../../Assets --only NovaStriker.Sim,NovaStriker.Game,NovaStriker.Editor
python3 ucompile.py out--player $ROOTS ../../Assets --only NovaStriker.Sim,NovaStriker.Game --player
```

The type-check compiles against Unity **6.3** (6000.3.25f1). The owner runs **6.6**. Use only APIs present in
both. If you are unsure an API exists in 6.3, the type-check tells you. Never use an API marked obsolete with an
error.

### A4. Shaders cannot be compiled here

The GitHub build (`.github/workflows/unity-build.yml`) is the only shader compiler you have. It fails on any
shader error. So:

1. Model every new shader on an existing one in `Assets/NovaStriker/Shaders/` (`Reflect.shader` and
   `Unlit.shader` are good templates): URP `HLSLPROGRAM`, `#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"`,
   properties inside `CBUFFER_START(UnityPerMaterial)` so the SRP Batcher accepts it, and
   `#pragma multi_compile_instancing` wherever instancing is used.
2. Keep each shader small and plain. No custom includes, no compute shaders, no geometry shaders, no
   tessellation.
3. Every new shader needs a template material: add a row to `TEMPLATES` in `Editor/NovaSetup.cs` and a matching
   property to `Templates` in `Game/Three/TMat.cs` (follow the `Reflect` row exactly). The owner must then run
   *Nova Striker › Set Up Project* once (say so in your message).
4. After each push that touches a shader, wait for the build and read its result:
   `gh run list --repo moderngentlemen-sudo/nova-striker-claude --branch nova-striker-unity-claude --limit 1`,
   then `gh run view <id> --repo moderngentlemen-sudo/nova-striker-claude --log-failed` if it failed. Fix and
   push again until it is green. A run takes about 8 minutes. Wait with a background
   `until ... ; do sleep 30; done` loop, never a foreground sleep.

### A5. The simulation and parity

1. `Assets/NovaStriker/Sim/` is a tick-for-tick port of the browser prototype. `parity.sh` checks it.
2. Graphics work (Phases 1 to 6, 10 and 11) must **not** change any file in `Sim/` except to add new fields to
   the `Settings` class in `Sim/Config.cs`. The simulation must never read graphics settings.
3. Gameplay work (Phases 7 to 9) does change `Sim/`. Every new gameplay behaviour must sit behind a setting
   whose default is **off**, and with it off the simulation must behave exactly as before: same boxes, same
   events, no extra random numbers drawn. `parity.sh` runs with defaults, so it must stay at `0 differ`.
4. Never call `UnityEngine.Random` or read the clock in `Sim/`. Use tick counts.

### A6. Coordinate spaces (the most common source of bugs)

1. The simulation is 2D: `x` along the route, `y` up. The route curves through 3D; `Level.Frame(x)` gives the
   point (`px`, `pz`), the tangent (`tx`, `tz`) and the normal toward the camera (`nx`, `nz`).
2. `S.W(x, y, depth)` (`Game/View/Space.cs`) turns a sim point into **three.js space** (z toward the camera).
3. The game's scene graph (`TObj`, `TMesh` in `Game/Three/`) is in three.js space and mirrors itself into Unity.
   Anything native to Unity (a `ParticleSystem`, a `Light`, a `DecalProjector`, `Graphics.RenderMeshInstanced`,
   a `BoxCollider`) needs **Unity** space: convert with `Th.P(v)` or `S.ToUnity(v)` (z negated). Rotations too:
   a yaw of `S.YawAt(x)` in three.js space is `-yaw` about Unity's y (check with an existing user such as
   `Sparks.cs` or `PlanarReflection.cs` before guessing).
4. Blender (Z up, the model faces -Y) to three.js: `three = (bx, bz, -by)`. The existing exporters
   (`Art/Blender/export_kit.py`, `export_ram_gear.py`) already do this. Reuse them.

### A7. Performance budgets (design limits, since nothing can be profiled here)

| Item | Cinematic preset | Balanced | Low quality |
|---|---|---|---|
| Live particles, all systems | 6,000 | 3,000 | 1,200 |
| Effect point lights at once | 8 | 4 | 0 |
| Decals at once | 32 | 16 | 0 |
| New dressing per zone (triangles) | 80,000 | same | same |
| Enemy model (triangles) | small 3,000, heavy 7,000, boss 18,000 | same | same |

Pool everything. No `new` of meshes, materials, `GameObject`s or lists inside per-frame updates. Prefer one
`ParticleSystem` per effect layer, fed with `ParticleSystem.Emit(EmitParams, count)` at many positions, over one
system per explosion.

### A8. Unity features to use, and why some are not

Use: the built-in **Particle System** (from code), **TrailRenderer**, URP **Volume** overrides, URP
**Forward+** rendering (no per-object light limit for effect lights), the **camera opaque texture** (for heat
haze and distortion), the URP **Decal** renderer feature, **SRP Lens Flare**, **GPU instancing**
(`Graphics.RenderMeshInstanced`), **light cookies**, soft particles (the depth texture), **LOD** by distance, and
spatially chunked static batching.

Do not use **VFX Graph**: its graphs are editor-authored assets that cannot be written reliably as text, and the
owner cannot author them. Everything here is built from C# so it can be checked and rebuilt. Do not use Shader
Graph for the same reason (except URP's own shipped decal shader, see Phase 2).

### A9. Settings: how they work (read before Phase 1)

1. Fields live in the `Settings` class in `Sim/Config.cs`. A new field's initializer is its default. Saved
   settings load with `JsonUtility.FromJsonOverwrite` (`Game/Input/Haptics.cs`, `SettingsStore`), so a new field
   needs **no** migration. Bump `settingsVersion` only to change an existing field's default for players who
   saved before.
2. The pause menu (`Game/UI/UiMenus.cs`, `BuildPause`) draws every entry of `SETTING_DEFS` in a two-column
   grid: `@bool` gives an On/Off button, `opts` a cycler, `range` a slider. Code reads `SETTINGS.<field>` live,
   so changes apply at once unless a phase says otherwise.

### A10. The owner

The owner is not a programmer. After each phase's push, give plain numbered steps:

1. GitHub Desktop: **Fetch origin**, then **Pull origin**.
2. If you changed `Packages/manifest.json`: before pulling, in GitHub Desktop right-click the changed
   `Packages/manifest.json` (and `ProjectSettings/ProjectVersion.txt`) and choose **Discard changes** (Unity 6.6
   upgrades them again when it opens the project).
3. In Unity: *Nova Striker › Set Up Project* if this phase added a shader, template material or renderer
   feature.
4. Press **Play** and check the listed things. Ask for screenshots and for any red Console errors as text.

### A11. Per-phase routine

1. Re-read the files a phase touches before editing.
2. Make the change in small steps; type-check often.
3. Run all checks (A2). Commit with a clear message. Push. Confirm the GitHub build is green (A4).
4. Update `README.md` (*Unity-only additions*) and `CLAUDE.md` (open threads, budgets) for what changed.
5. Send the owner renders (Blender phases) and steps (A10).

---

## Part B. Graphics and effects

### Phase 1. Effect settings and a sectioned pause menu

**Goal:** every new visual has a setting with a cinematic default and a "classic" fallback, grouped in the pause
menu.

1. In `Sim/Config.cs` `Settings`, add these fields (all presentation only; the simulation never reads them):

   | Field | Default | Options |
   |---|---|---|
   | `fxPreset` | `"cinematic"` | `cinematic`, `balanced`, `classic`, `custom` |
   | `fxExplosions` | `"volumetric"` | `volumetric`, `plasma`, `stylised`, `classic` |
   | `fxProjectiles` | `"energy"` | `energy`, `tracer`, `classic` |
   | `fxTrails` | `"long"` | `long`, `short`, `off` |
   | `fxDensity` | `"high"` | `high`, `medium`, `low` |
   | `fxDebris` | `"physics"` | `physics`, `simple`, `off` |
   | `fxSmoke` | `"rich"` | `rich`, `light`, `off` |
   | `fxLights` | `true` | bool: dynamic light from shots and blasts |
   | `fxDistortion` | `true` | bool: heat haze and shockwave refraction |
   | `fxDecals` | `true` | bool: scorch marks and scuffs |
   | `fxScreen` | `"cinematic"` | `cinematic` (chromatic aberration kicks, lens dirt, grain), `clean` |
   | `weather` | `true` | bool: per-zone weather and atmosphere |
   | `clouds` | `"volumetric"` | `volumetric`, `classic` |
   | `birds` | `"realistic"` | `realistic`, `classic`, `off` |
   | `enemyModels` | `true` | bool: Blender models for enemies where available |
   | `perfOverlay` | `false` | bool: frame time readout |
   | `levelTiers` | `false` | bool, gameplay (Phase 7) |
   | `levelHazards` | `false` | bool, gameplay (Phase 8) |
   | `depthLanes` | `false` | bool, gameplay (Phase 9) |

2. Create `Game/View/Vfx/FxCfg.cs`: a static class that resolves the *effective* value of each fx setting:

   ```csharp
   // With a preset other than Custom, the preset decides; Custom uses each setting. Low graphics quality caps them.
   public static string Explosions => SETTINGS.fxPreset switch {
       "cinematic" => "volumetric", "balanced" => "volumetric", "classic" => "classic", _ => SETTINGS.fxExplosions };
   ```

   Do the same for each fx field. Use this table:

   | Field | cinematic | balanced | classic |
   |---|---|---|---|
   | Explosions | volumetric | volumetric | classic |
   | Projectiles | energy | energy | classic |
   | Trails | long | short | short |
   | Density | high | medium | medium |
   | Debris | physics | simple | off |
   | Smoke | rich | light | light |
   | Lights | on | on | off |
   | Distortion | on | off | off |
   | Decals | on | on | off |
   | Screen | cinematic | clean | clean |

   Then, if `SETTINGS.quality == "low"`: Density at most `low`, Lights off, Distortion off, Decals off, Debris at
   most `simple`. Add `FxCfg.MaxParticles`, `MaxLights` and `MaxDecals` from A7. Every effect reads `FxCfg`,
   never the raw fields.

3. Pause-menu sections: add `public string section;` to `SettingDef` in `UiMenus.cs`. In `BuildPause`, instead
   of one grid, walk `SETTING_DEFS` in order and start a new header plus grid whenever `section` changes (a
   null section continues the previous one). Header: `pause.Para("<b>" + section.ToUpper() + "</b>", 15, Pal.text)`.
   Build each grid exactly as the current one is built (move the cell-building code into a local function and
   call it per def). Keep focus navigation working: `Collect()` already gathers every `Selectable` under
   `pause.content`.
4. Put the existing entries under sections: "Game" (kits, heads, gameplay toggles, difficulty, AI), "Controls",
   "Graphics" (`camera`, `fov`, `quality`, `reflections`, `charModels`, `hdr`), "Audio". Add new sections:
   - "Effects": `fxPreset` first, then the other fx entries, `weather`, `clouds`, `birds`, `enemyModels`,
     `perfOverlay`. Label the individual fx entries "(Custom preset)".
   - "Level features (Unity only)": `levelTiers`, `levelHazards`, `depthLanes`, each labelled "applies when a
     zone loads".
5. Add a perf overlay (`Game/View/Vfx/PerfOverlay.cs`): when `perfOverlay` is on, a small top-right text with
   frame ms (smoothed), FPS, live particle count (sum of the pools' `particleCount`), effect lights in use and
   decals in use. Use the existing UI helpers in `Game/UI/Widgets.cs`.
6. Verify: type-check, parity, ShieldTests. Push. Owner: pause, see the sections, cycle the Effects preset (no
   visual change yet).

### Phase 2. Effects foundation

**Goal:** the shared machinery every later effect uses. No visible change on its own except the setup steps.

1. **Packages:** add `"com.unity.modules.particlesystem": "1.0.0"` to `Packages/manifest.json` (the Particle
   System is an engine module that must be enabled). Physics is already there. Tell the owner about A10 step 2.
2. **Pipeline (`Editor/NovaSetup.cs`):**
   - `MakePipeline`: `asset.supportsCameraOpaqueTexture = true` (needed for distortion), keep the depth texture
     on, and `asset.maxAdditionalLightsCount = 8` (the property name may differ in 6.3; check with the
     type-check and use the serialized property via `Set(so, "m_AdditionalLightsPerObjectLimit", 8)` if the
     setter is missing).
   - `MakeRenderer`: set `data.renderingMode = RenderingMode.ForwardPlus` (Forward+ lifts the per-object light
     limit). Add a `DecalRendererFeature` to both renderers before the Grade feature (`using
     UnityEngine.Rendering.Universal;`). If either API is missing in 6.3, log a **warning** and skip it.
   - Never `Debug.LogError` for optional features: an error fails the GitHub build.
3. **Effect textures, made offline:** write `Art/Blender/make_fx_textures.py` (numpy, like
   `make_textures.py`) that writes `Assets/NovaStriker/Resources/NovaStriker/Fx/<name>.bytes`, each a 12-byte
   header (`b"NSFX"`, `uint16 width`, `uint16 height`, `uint16 channels`, `uint16 frames`, little-endian),
   then raw 8-bit pixels, top row first, and a PNG preview in a folder given on the command line. Textures:
   - `smoke` 8×8 flipbook, 128 px frames (1024²), RGBA: a billowing puff that grows and thins, alpha =
     density, RGB = a baked normal-ish shading (lit from the top-left) so it reads as volume.
   - `fireball` 8×8 flipbook, 128 px frames, RGBA: hot turbulent core turning to dark smoke.
   - `spark` 64², `ember` 64², `glow` 128² (soft radial), `ring` 256² (shock ring), `streak` 64×256 (tracer and
     raindrop).
   - `scorch` 256² RGBA (decal: dark charred centre, ragged edge), `scuff` 256².
   - `lensdirt` 512² RGB (soft smudges and specks, mostly black).
   - `cloudpuff` 4 variants packed 2×2 in 1024², RGBA: alpha = density, RGB = the puff's surface normal (for
     lighting in Phase 5).
   Use a fixed random seed so reruns are identical. Draw new random numbers only after the existing ones, as
   `make_textures.py` does.
4. **`Game/View/Vfx/FxTex.cs`:** `FxTex.Get(name)` loads a `.bytes` file once (`Resources.Load<TextAsset>`),
   checks the header, builds a `Texture2D` (mipmaps on, `wrapMode` Clamp except `streak`), caches it, and returns
   null (with a single warning) if it is missing. Follow `Look.LoadSurface` as the pattern.
5. **Particle templates:** add template rows to `NovaSetup.TEMPLATES` and `Templates` for URP's particle shaders:
   `ParticleAdd` (`Universal Render Pipeline/Particles/Unlit`, additive), `ParticleAlpha` (same, alpha
   blended, soft particles on), `ParticleLit` (`Universal Render Pipeline/Particles/Lit`, alpha blended, soft
   particles on). Configure their keywords in a new branch of `Templates.Configure`, keyed on the shader name
   (`_SURFACE_TYPE_TRANSPARENT`, `_BLENDMODE_ADD` or `_ALPHABLEND_ON`, `_SOFTPARTICLES_ON`,
   `_FLIPBOOKBLENDING_ON`; set the `_Surface`, `_Blend`, `_SoftParticlesNearFadeDistance` and
   `_SoftParticlesFarFadeDistance` floats). Look up the exact keyword names in the URP 17 package source under
   `typecheck/unity/Editor/Data/Resources/PackageManager/BuiltInPackages/com.unity.render-pipelines.universal/Shaders/Particles/`
   before writing them.
6. **`Game/View/Vfx/FxPool.cs`:** a factory for long-lived particle *layers*. `FxPool.Layer(name, material, setup)`
   creates one `GameObject` with a `ParticleSystem` (simulation space World, `playOnAwake` false, looping off,
   `maxParticles` from a share of `FxCfg.MaxParticles`, emission rate 0) and returns a small wrapper with
   `Emit(Vector3 unityPos, Vector3 vel, float size, float life, Color c, float rot = 0)` built on `EmitParams`.
   The `setup` callback configures modules once: size over lifetime, colour over lifetime, texture sheet
   animation (flipbooks), noise, rotation, the renderer (`ParticleSystemRenderer`: billboard, stretched or mesh
   mode, sort mode, soft particles via the material), and collision (World mode, layer mask = the effects
   collision layer from step 8, bounce, lifetime loss) where wanted. Also enable `GPU instancing` on mesh
   particles (`renderer.enableGPUInstancing = true`).
7. **`Game/View/Vfx/FxLights.cs`:** a pool of `MaxLights` (8) point lights. `FxLights.Flash(Vector3 three, Color c,
   float intensity, float range, float life, string owner = null)` takes the oldest free light (or the dimmest),
   fades it out over `life` with a fast flicker, and never exceeds the effective `FxCfg.MaxLights`. `Sparks.cs`
   already has a small light pool: leave it alone.
8. **`Game/View/Vfx/FxColliders.cs`:** for particle and debris collisions, build invisible `BoxCollider`s from
   `Level.BOXES` (skip `'d'` breakables and open gates) on layer 9 (name it in code as `FX_LAYER = 9`; layers work
   by index). Curved boxes: split along x into pieces of at most 2 m, each oriented by `Level.Frame` at its middle
   (use the same splitting as `View.BuildLevel`'s `segs` list; read it first). Depth: the box's visual depth
   (`DepthFor` in `View.BuildLevel`). No `Rigidbody`. Rebuild when breakables break (`Breakables` events) by
   disabling the collider for that box id.
9. **`Game/View/Vfx/FxDecals.cs`:** a pool of `MaxDecals` `DecalProjector`s with a material made from the
   decal template (add a `Decal` template row using shader `Shader Graphs/Decal`, URP's shipped decal shader;
   log a warning and disable decals if `Shader.Find` returns null). `FxDecals.Stamp(simX, simY, normal, size,
   texName, tint, life)` projects onto the floor or wall at that point and fades out over `life` (8 s default)
   by `fadeFactor`.
10. **`Game/View/Vfx/Vfx.cs`:** one owner object, made in `View`'s constructor after `fx`, that holds the layers,
    lights, decals and colliders, and has `Update(dt)` called from `View.Update` next to `sparks.Update(dt)`.
    Every effect module gets it through `view.vfx`.
11. Verify: type-check, parity, ShieldTests. Push and check the GitHub build is green (it compiles the new
    template setup). Owner: discard the manifest change before pulling, Set Up Project, Play (nothing should look
    different yet).

### Phase 3. Blasts and projectiles

**Goal:** every projectile and every blast gets a cinematic version; "classic" keeps today's code path untouched.

1. **Inventory first.** Build the definitive list of projectile kinds and blast events from the code, not from
   this plan:
   - projectile kinds: `grep -rn "kind ==\|kind = \"" Assets/NovaStriker/Sim Assets/NovaStriker/Game/View/FxSync.cs`
     and the `TrailColor` switch in `FxSync.cs`;
   - blast and impact events: the `case "..."` handlers in `Game/View/FxEvents.cs`, `RamFx.cs`, `SubFx.cs`,
     `ChargeFx.cs`, `UltFx.cs`, `FixFx.cs`, `BeamFx.cs`.
   Write the list as a table at the top of a new file `Game/View/Vfx/FxCatalog.cs` (a comment), with each entry's
   family from step 2 and the handler that triggers it.
2. **Families and recipes** (all in `Game/View/Vfx/ProjectileLooks.cs` and `Explosions.cs`). Colours come from
   the existing code (`TrailColor`, `CHARS[...]`, `HOSTILE` for enemies), so ownership stays readable.

   | Family | Kinds (typical, confirm in step 1) | Energy look |
   |---|---|---|
   | A. Energy bolts | `shot`, `bolt`, `tracer`, `rifle`, `markShot`, `sentryBolt`, enemy shots | HDR core (stretched along velocity, 3–4× the current size in length), soft glow sprite, short `TrailRenderer`, a tiny light only for the owner's charged shots |
   | B. Charged and heavy | `lance`, `rail`, `breach`, `heavy`, charged shots | larger core, a spiral of motes (particle layer, noise), long trail, heat distortion sprite (if Distortion), a light |
   | C. Physical rounds | `slug`, `rivet`, `hotRivet`, `pellet`, `shell`, `dart` | metallic tracer streak (stretched billboard), sparks on impact (`Sparks.Emit` for floor hits), `hotRivet` glows and smokes |
   | D. Explosives | `grenade`, `bomblet`, `mortar`, `missile`, `sentryRocket` | keep the body mesh; add a flame jet (additive particles) and a lit smoke trail (`ParticleLit` with the `smoke` flipbook); detonation = a Volumetric blast |
   | E. Special | `disc`, `wave`, `prism`, `shard`, snares | keep the shape; add glow, trail and a themed impact |

   **Muzzle flashes** on firing (the `shot`, `enemyShot`, `sentryShot`, `snipe`, `mortarShot` events): a 2-frame
   star sprite plus a 0.06 s light. **Impacts** on hits and walls (`hit`, `crit`, `projWall`, `deflect`,
   `ricochet`): sparks, a small flash, a scuff decal on walls (if Decals).

   **Volumetric blast** (`Explosions.Blast(x, y, radius, colour, power)`), in this order:
   1. Flash: a 0.05 s HDR glow sprite (radius × 2.5) and `FxLights.Flash` (range radius × 4, life 0.25 s).
   2. Fireball: 6–14 `fireball`-flipbook particles (`ParticleLit`, emissive tint), outward 2–6 m/s, life 0.5–0.8 s.
   3. Smoke: 8–20 `smoke`-flipbook particles, slow rise, life 2–4 s, drag, lit by the sun (rich) or 4–8 (light).
   4. Embers and sparks: stretched particles with gravity and world collision (the colliders from Phase 2).
   5. Shock ring: a ground ring (`ring` texture) expanding to radius × 2 in 0.3 s; if Distortion, a refraction
      ring (Phase 4's `Distort.shader`).
   6. Debris (if Debris): physics = mesh particles (3–4 chunky shapes made in code with `Geo`) with world
      collision, bounce 0.3, rotation; simple = the existing `Fx` debris.
   7. Decal: a `scorch` stamp under it (if Decals), radius × 1.2, 10 s.
   8. Camera: the existing bloom kick and shake stay; add a chromatic aberration pulse (Phase 4).
   9. Birds startle (the existing `ambience.Startle`).

   **Plasma** blast: an inward implosion of motes (0.12 s), then a bright energy sphere that pops into rings and
   motes in the owner's colour, no smoke, short light. **Stylised** blast: the same timing with hard-edged toon
   puffs (alpha-cutoff smoke, two-tone colour over lifetime) for a crisp, animated-cinematic look. **Classic**:
   call the existing code exactly as now.
3. **Hook-up rule.** In each handler from step 1, wrap the existing effect code:
   `if (FxCfg.Explosions == "classic") { /* existing code, unchanged */ } else Explosions.Blast(...)`. Projectile
   visuals: in `FxSync.cs`, where a projectile mesh is created or updated, add the new look as extra children
   when `FxCfg.Projectiles != "classic"` and keep the existing mesh hidden in that case. Remove the extras when
   the projectile ends (find where `FxSync` disposes projectile meshes).
4. **Enemy deaths** (`kill` and the enemy death handler): the construct cracks; ceramic plate shards (mesh
   particles, plate colour) burst out, the magenta core flares and collapses with a plasma pop, then a small smoke
   puff. **Breakables** (`boxBreak`): material-specific debris (crate splinters, glass shards that glint, concrete
   chunks with dust).
5. **Big moments:** `bossSlam`, `poundLand`, `quake`, `kineticRelease`, `rampartBreak` and `ultCast` get the
   largest Volumetric blast plus a dust wave along the floor (`Fx.Dust` both ways, scaled).
6. Verify: type-check, parity, ShieldTests. Push; GitHub build green. Owner: play a zone with each character;
   cycle Effects preset between Cinematic, Balanced and Classic in the pause menu; send screenshots of blasts.

### Phase 4. Post-processing and screen effects

1. **`Game/View/Vfx/PostFx.cs`**, made by `View` and given the existing `volume.sharedProfile`:
   - add `ChromaticAberration` (base 0, pulses to 0.35 on big blasts, decays in 0.25 s), `LensDistortion` (a
     brief -0.15 pulse on the biggest blasts only), `FilmGrain` (intensity 0.12, `Thin1`) and a bloom lens dirt
     (`bloom.dirtTexture = FxTex.Get("lensdirt")`, `dirtIntensity` 1.5). All only when `FxCfg.Screen ==
     "cinematic"`; set them to zero otherwise. Do **not** add tone mapping or vignette (the Grade pass does those).
   - `PostFx.Kick(strength)`, called by `Explosions.Blast` and the big-moment handlers.
2. **Sun lens flare (SRP Lens Flare):** add a `LensFlareComponentSRP` to the sun light with a `LensFlareDataSRP`
   built in code (a glow, a soft ring and three small ghosts, using `FxTex` textures). Intensity follows the
   cloud cover (`Ambience`'s private `cover` field: expose it as a public read-only property) and is 0 off the open-sky Skyport route. Verify the class names compile in 6.3;
   if not, draw the flare as camera-facing sprites placed along the sun-to-screen-centre line.
3. **`Shaders/Distort.shader`** (new; Phase 2's opaque texture): a transparent unlit shader that samples
   `_CameraOpaqueTexture` (`#include ".../DeclareOpaqueTexture.hlsl"`) at the screen uv offset by a normal-ish
   distortion read from a texture's red/green channels times `_Strength` and the vertex alpha. Used for blast
   shock rings, the heat haze over Foundry furnaces (Phase 10), Nova's beam and charged shots. Add its
   template row (A4.3).
4. Verify and push; GitHub build green (new shader). Owner: Set Up Project, then check blasts and the sun.

### Phase 5. Clouds and birds

**Goal:** clouds with volume, placed so they can never enter the play area; birds that are white and look
real.

**Why clouds enter the play area today:** `View.BuildBackdrop` places 44 cloud sprites on a ring of radius
170–330 m around (60, z -40); about half of that ring has positive z, which is **in front of** the play area and
between it and the camera. `Ambience` also drifts every cloud along +x and wraps it at x 480 → -340, through
everything.

1. **`Shaders/Cloud.shader`** (new): a lit cloud impostor. Inputs: the `cloudpuff` atlas (alpha = density, RGB =
   normal), per-instance colour and the variant in vertex colour or uv2. Lighting: `saturate(dot(n, sunDir))`
   lifted by a forward-scatter term (bright edges when the sun is behind), a second density tap offset toward the
   sun for self-shadow, an ambient sky tint from the top and bottom sky colours (`View.skyTop`/`skyBot`, passed
   as globals), and distance fog. Depth fade (soft particles) so puffs never cut hard into geometry. Template row
   (A4.3).
2. **`Game/View/Vfx/CloudLayer.cs`** replaces the sprite clouds when `clouds == "volumetric"`:
   - A cloud is a cluster of 6–14 puffs (instanced quads, `Graphics.RenderMeshInstanced` with the Cloud
     material, one draw for all clouds). Build 40 far clouds and 16 low banks.
   - **Placement rule, enforced every frame:** each cloud has a backdrop coordinate (`u` along the route from
     the camera's x, `d` = distance behind the play plane, `h` = height). Its world position is
     `S.W(camX + u, h, -d)`, so it follows the route's curve and always lies on the far side of the play plane.
     Far clouds: `d` 180–420 m, `h` -10 to 60 m. Low banks: `d` 70–200 m, `h` -40 to -22 m (below the deck).
     Clamp `d >= 70` for every cloud in every frame. Drift = `u += wind × dt`, wrapping `u` within ±320 m of the
     camera, so clouds slide past behind the action and never cross it.
   - Each puff faces the camera, and its size, variant and colour are fixed when it is made.
   - Hide the layer off the Skyport route the same way `Ambience` fades its sky (`sky` weight), and keep the
     cloud-shadow cookie as it is.
   - When `clouds == "classic"`, keep today's sprites but apply the same `d >= 70` clamp to them in
     `Ambience.Update` (fixes the bug in classic mode too).
3. **Birds** (`birds == "realistic"`):
   - Model a gull in Blender (`Art/Blender/build_bird.py`): white body and head, pale grey upper wings with
     darker grey tips, a small yellow beak, about 200 triangles. Wings as separate strips from the shoulder so
     they can flap. Export with a tiny exporter (copy `export_ram_gear.py`) to
     `Resources/NovaStriker/Models/bird.json`, adding a per-vertex flap weight in uv2.x (0 at the body, 1 at the
     wingtip) and a side sign in uv2.y (-1 left wing, +1 right wing, 0 body).
   - **`Shaders/Bird.shader`** (new, instanced): in the vertex shader, rotate wing vertices about the body's long
     axis by `flapAngle × uv2.x × uv2.y`, where `flapAngle = amp × sin(time × freq + phase)` comes per instance
     (an instanced property, `UNITY_DEFINE_INSTANCED_PROP`). Simple lit fragment: sun diffuse, sky ambient, a
     little rim. Template row (A4.3).
   - **`Game/View/Vfx/Birds.cs`:** replaces `Ambience`'s silhouette flocks when realistic. Three flocks of 7–11
     birds. Each bird glides most of the time, then flaps in bursts (amplitude ramps up and down), banks into its
     turns (roll proportional to turn rate), follows its flock loosely (cohesion and separation), and scatters
     when `Startle` is called (keep `Ambience.Startle` as the entry point and forward to `Birds`). Realistic size
     (about 1.3 m wingspan) but placed 60–200 m behind the play plane (`d >= 60`, the same backdrop rule as
     clouds), so they read as distant gulls. One `RenderMeshInstanced` call.
   - `birds == "classic"` keeps the current flocks (also clamp them to `d >= 60`); `off` hides them.
4. Verify and push; GitHub build green. Owner: Set Up Project; play the Movement Gym and Skyline Relay; check
   that clouds never pass in front of the action and that birds are white gulls that glide and flap. Send the
   Blender bird render.

### Phase 6. World geometry optimisation

1. **Spatial chunks:** `View.FlushBaked` merges parts per material in batches of 512 in *list* order, so one
   merged mesh can span the whole level and is never culled. Change it to group parts by material, shadow flag
   **and** a 48 m cell of route x (compute each part's cell from its transform's position projected onto the
   route: store the sim x in `BakeSet.parts` alongside each `CombineInstance` by giving `Bake` an optional
   `double simX` parameter and passing it from every caller; callers that place by `S.W(x, ...)` know their x).
   Then merge per cell. Keep `isStatic = true`.
2. **Shadows:** chunks whose cell is more than 70 m from the play path's centre in depth, and decorative
   backdrop meshes, do not cast shadows (`cast = false`). Keep receivers.
3. **Mesh data:** after merging and computing UVs and tangents, call `merged.Optimize()` and
   `merged.UploadMeshData(true)` (frees the CPU copy). Check first that nothing reads a baked mesh's vertices
   later (`grep -rn "\.vertices" Game/View`); if something does, skip `UploadMeshData` for that set.
4. **Instancing:** repeated landmark pieces in `Landmarks.cs` (windows, pipes, lamps) built as many separate
   meshes become `Graphics.RenderMeshInstanced` batches when they are identical meshes with the same material.
   Enable `enableInstancing` on the Lit, Surf and Coat templates in `NovaSetup.MakeTemplates`.
5. **LOD:** distant landmark structures (spires, towers, the reactor core) get a low-detail version (fewer `Geo`
   segments) swapped by camera distance (> 150 m) in `View.Update`, or a `LODGroup` with two renderers.
6. **Culling distances:** put small dressing props on layer 10 and set `camera.layerCullDistances[10] = 160`.
7. **Pipeline:** shadow distance 70 m and 2 cascades are fine; set the cascade split to 0.25 and soft shadows
   medium. Confirm the SRP Batcher is on (`GraphicsSettings.useScriptableRenderPipelineBatching = true` in
   setup).
8. Verify (parity untouched: no Sim change) and push. Owner: perf overlay on, compare frame times before and after
   in the Movement Gym and Undercity; send the numbers.

---

## Part C. Level features (gameplay)

All three features change the simulation, sit behind settings that default **off**, and apply when a zone loads
(the pause menu's zone buttons, a respawn at a checkpoint, or a new game). With all three off the game must be
exactly as now (parity).

### Phase 7. Multi-tier levels

**Goal:** zones gain upper catwalks and lower service levels, so fights and traversal use several heights.

1. Read `Sim/Level.cs` in full: the `LevelBox` types (`'s'` solid, `'o'` one-way, `'g'` gate, `'d'` breakable),
   `BuildRaw`, the static constructor, and every function that loops over `BOXES`. Note that the collision code
   loops over `BOXES` directly (no index), so appended boxes take part automatically.
2. Add `Level.EXTRA` (a list of raw rows tagged by zone id and feature: `"tiers"` now, `"hazards"` and `"lanes"`
   later) and `Level.ApplyFeatures(bool tiers, bool hazards, bool lanes)`, which removes every box whose id is
   ≥ 10000 and appends the rows for the enabled features with ids from 10000 up. With all off, `BOXES` must be
   identical to before (same objects, same order). Call it from wherever a zone loads or the world resets (find
   where `Level.RestoreBoxes()` is called: it runs on a checkpoint reset and a zone load), passing `SETTINGS.levelTiers`, etc.
3. **Reach limits:** jump `jumpV` 17.7 with gravity 49 gives about 3.2 m; a double jump adds about 2.4 m (5.5 m
   in all). Space tiers 3.0–4.4 m apart, catwalk gaps 3–6 m, and keep at least one route through each zone with
   no tier needed. Use `'o'` (one-way) for catwalks so players can jump up through them and drop down (down + jump
   already drops through one-way platforms; confirm in `PlayerSim.cs`).
4. **Per zone** (route x ranges in `Level.ZONES`). Write rows only inside each zone's x range and never within 6 m
   of a checkpoint (`Level.CHECKPOINTS`) or a zone's spawn:
   - Movement Gym (x -10 to 60): one catwalk tier over the start area and a high route over the wall-jump panel.
   - Concourse Lock (60 to 97): mezzanine ledges on both sides of the arena floor, 4 m up.
   - Storm Spire Climb (97 to 162): alternating side ledges up the climb (a vertical zone already; add
     shortcuts).
   - Skyline Relay (162 to 318): a lower maintenance gantry under the main deck between relay towers, and upper
     antenna platforms.
   - Helix Foundry (398 to 764): overhead crane catwalks over the furnace floor and a lower slag channel walkway.
   - Undercity Descent (798 to 1181): stacked balconies and fire escapes along the descent.
5. **Enemies on tiers:** find the spawn tables (`SpawnDef` uses in `Sim/Enemies.cs` and `World.cs`). Add new
   spawn rows only when `SETTINGS.levelTiers` (gated the same way), placing snipers, turrets and drones on upper
   tiers.
6. **AI teammates** (`Sim/Bots.cs`): make sure they can follow a player onto a tier (they reuse the player's
   jump logic; check that the target-height logic considers one-way platforms). If they cannot, they stay on the
   ground route; that is acceptable.
7. **Tests:** create `SimTests/LevelTests/` (copy `ShieldTests/ShieldTests.csproj` and its `Program.cs` structure).
   Tests: with all features off, `Level.BOXES` count and the first and last box are unchanged; with tiers on, the
   extra rows exist, lie inside their zone, are at least 6 m from checkpoints, and a scripted player can land on
   a catwalk (step the world with inputs as ShieldTests does).
8. View: `View.BuildLevel` builds geometry from `Level.BOXES` once at start. Rebuild the level geometry for boxes
   with id ≥ 10000 when features change: build those into their own group (not baked into the shared chunks), and
   destroy and rebuild that group whenever `Level.ApplyFeatures` runs (expose a version counter on `Level` that the
   view watches). The FX colliders (Phase 2) watch the same counter.
9. Verify: all checks including LevelTests; parity still 0 differ. Push. Owner: turn on *Level features ›
   Multi-tier levels*, reload a zone from the pause menu, play.

### Phase 8. Level hazards

**Goal:** timed, readable hazards that hurt players and enemies alike (enemies can be lured into them).

1. New file `Sim/Hazards.cs`: `public sealed class Hazard { int id; string kind, zone; double x0, x1, y0, y1;
   int lanes = 7; int period, warn, on, phase; double dmg, kbx, kby; int state, t; }` and a static list filled by
   `Level.ApplyFeatures` from `EXTRA` rows tagged `"hazards"`. Everything is tick-based and deterministic: no
   random numbers (patterns come from `period`, `phase` and the tick).
2. Step them in `World.Step` (one call, only when the list is non-empty). States: idle → warn (`warn` ticks) →
   on (`on` ticks) → idle. Emit `hazardWarn`, `hazardOn`, `hazardOff` and `hazardHit` events with the hazard's id,
   x and y (use the existing `world.Emit(name, new Ev { ... })` pattern).
3. Damage: find the existing functions that damage players and enemies (`grep -n "void Damage\|void Hurt\|Hit("
   Sim/*.cs`) and reuse them, so invulnerability, i-frames, downed players, hit-stun and kill credit keep working.
   A hazard hits each entity at most once per `on` window (keep a small per-hazard hit set).
4. **Kinds** (each with sensible defaults; scale damage by difficulty if a multiplier exists):

   | Kind | Behaviour | Zones |
   |---|---|---|
   | `vent` | steam column: on = an upward launch (kby 22) and light damage; doubles as a launch pad | Gym (training pads), Foundry |
   | `shock` | electrified floor panel: on = damage every 20 ticks to grounded entities on it | Concourse Lock, Undercity |
   | `crusher` | piston: warn = shadow, on = a 6-tick slam (heavy damage, knockdown), then it rises | Foundry |
   | `laser` | a beam across a height band that toggles; jump or slide through | Concourse Lock, Skyline |
   | `wind` | gust zone: on = push along x (vx ±6 per second) | Storm Spire, Skyline |
   | `lightning` | strike at one of 3 marked spots in turn (from the tick), big damage | Storm Spire |
   | `slag` | molten pool: always on, damage while standing in it | Foundry |
   | `collapse` | a one-way platform that crumbles 48 ticks after being stood on and returns after 360 ticks | Skyline, Undercity |
   | `debris` | falling chunks from a marked ceiling spot | Undercity |

   `collapse` needs collision support: give `LevelBox` a `collapsed` int (ticks left) and make every `Level`
   collision helper skip a box while `collapsed > 0`, exactly as they skip broken breakables.
5. Placement rules: never within 6 m of a checkpoint or spawn, never in the middle of a boss arena, and every
   hazard must be avoidable by jumping, sliding or waiting.
6. **AI teammates:** treat hazards in warn or on like pits: do not stop inside them and jump over a `shock` or
   `slag` span (find the pit-avoidance logic in `Bots.cs` and add hazard spans to it).
7. **View** (`Game/View/Vfx/HazardFx.cs`): telegraphs in warning amber (`#ffb02e`): warn = pulsing floor stripes
   and a rising hum glow; on = the effect (steam column with the `smoke` flipbook, arcs of electricity, the
   crusher slam with dust and a blast, a sweeping laser with heat distortion, wind streaks, a lightning bolt with
   a full-screen flash and FxLights, glowing slag with embers, crumbling debris, falling chunks). Models for the
   hazard machinery come in Phase 10.
8. **Tests** (LevelTests): with hazards off, no `hazard*` events are emitted; with hazards on, a `vent` cycles
   through its states on the expected ticks, a player standing in a `shock` panel takes damage only while it is
   on, and a `collapse` platform drops a standing player after 48 ticks and returns.
9. Verify (parity 0 differ) and push. Owner: turn on *Level hazards*, reload zones, play.

### Phase 9. Depth lanes

**Goal:** in marked stretches, the play area widens into three depth lanes (back, middle, front). Players and
enemies hop between them; attacks only reach their own lane unless they are area attacks.

1. **Model:** lane index `-1` (back), `0` (middle), `+1` (front). `LANE_W = 1.4` m between lanes (the deck is
   4.4 m deep; Phase 10 widens the deck to 7.2 m in lane stretches and then `LANE_W` may become 2.2; keep it one
   constant in `Sim/Config.cs`). Outside a lane stretch everyone is in lane 0. Lane stretches are rows in
   `Level.EXTRA` tagged `"lanes"`: `(x0, x1)` per zone, two or three per zone, each 25–60 m long.
2. **State:** add to the player and enemy classes (find them in `Sim/Types.cs`): `int lane; int laneFrom; int
   laneT;` (laneT counts down a 10-tick hop; the logical lane switches at the hop's midpoint). Only touched when
   `SETTINGS.depthLanes`.
3. **Input:** add a `lane` intent to the sim's input (find the input struct the sim reads; add a field
   defaulting to 0 so recorded parity inputs are unaffected): keyboard `Z` = toward the back, `X` = toward the
   camera (both free; confirm in `Game/Input/Controls.cs` `KEYMAP`); gamepad: click the left stick (L3) while
   holding the stick up (back) or down (front). Confirm L3 is unused first; if it is used, choose a free
   combination and document it in the Controls help (`HELP_ROWS` in `UiMenus.cs`).
4. **Rules:**
   - A hop needs a lane stretch and a free destination (no solid box with that lane in its mask overlapping).
     Hopping is allowed on the ground and in the air.
   - Leaving a lane stretch from lane ±1 starts an automatic hop to lane 0.
   - `LevelBox` gains `int lanes = 7` (bit 1 back, 2 middle, 4 front). All existing boxes keep 7. Lane rows may
     add cover walls and lane-only platforms with other masks. Every collision helper in `Level.cs` gains a
     `lane` parameter (default 0) and skips boxes whose mask lacks that lane's bit. With the feature off, every
     caller passes 0 and every box has mask 7, so behaviour is unchanged.
   - Hits: add `Lanes.Same(a, b)` (true when the feature is off). Melee, projectiles, beams and lasers need the
     same lane. Area attacks (blasts, shockwaves, slams, ultimates, Kinetic Release, hazards with mask 7) hit all
     lanes. Projectiles copy their shooter's lane when fired.
   - Find every overlap test between two entities or between an entity and a box: `grep -n "Overlap\|Hits\|InRange\|Math.Abs(.*\.x - .*\.x)" Sim/*.cs`
     and the loops over `world.enemies`, `world.players` and projectiles. Add the lane check at each. List every
     changed site in your commit message.
   - Enemies: spawn rows may give a lane; the AI targets the nearest player, preferring its own lane, and hops
     toward the target's lane after 72 ticks apart (only inside a stretch). Bosses stay in lane 0 and their big
     attacks are area attacks.
   - AI teammates hop to follow the player they are tracking.
5. **View:** each entity's visual depth eases to `lane × LANE_W` (pass it as the `depth` argument wherever its
   rig is placed with `S.W(x, y)`; in `View.Update` for players and enemies, and in `FxSync` for projectiles).
   The camera pulls back slightly and centres the lanes while any player is in a stretch. Lane stretches show
   lane guide lines on the floor (Phase 10 dresses them). Shadows and the contact rings follow the depth.
6. **Tests** (LevelTests): with lanes off, everyone's lane stays 0 and no hop starts; with lanes on, Z and X hops
   take 10 ticks and change the lane at tick 5, a projectile from lane -1 passes an enemy in lane 0 without
   hitting it, a blast hits enemies in all three lanes, and leaving a stretch returns a player to lane 0.
7. Verify (parity 0 differ) and push. Owner: turn on *Depth lanes*, reload a zone, try Z and X (or L3 with up or
   down) in a lane stretch.

---

## Part D. Stages, enemies and wrap-up

### Phase 10. Stage dressing for the remaining zones, weather and the world reacting

**Goal:** dress Concourse Lock, Storm Spire Climb, Skyline Relay, Helix Foundry and Undercity Descent the way
the Movement Gym is dressed, including the new tiers, hazards and lane stretches, with per-zone weather.

1. **Level data for Blender:** make `SimTests/LevelDump/` (a console project like ShieldTests) that writes
   `Art/Blender/level_boxes.json`: every `Level.BOXES` entry (id, x0, x1, y0, y1, type, tag, lanes) plus the
   rows of `Level.EXTRA` (with their feature tag) and `Level.ZONES`, `Level.CHECKPOINTS` and the hazards. Layout
   scripts read this file instead of hand-copied numbers (the gym's `GROUND` list in `gym_layout.py` was copied
   by hand; leave the gym as it is).
2. **Generalise the pipeline:**
   - `Art/Blender/build_zone_kit.py <zone>` builds that zone's kit pieces in collections named `Kit_<Zone><Piece>`.
   - `Art/Blender/zone_layout.py` holds `placements(zone)`, computed from `level_boxes.json`.
   - `export_kit.py` gains a zone argument and writes `Resources/NovaStriker/Env/<zone>_kit.json` (the default
     stays `gym_kit.json`).
   - `Game/View/GymDressing.cs` becomes `ZoneDressing.cs` with `Build(view, zoneId)` called for every zone that
     has a kit file, and per-zone material names (`Kit_*` stay; add each zone's new names to `MatFor`).
   - Follow the gym's style: bold simple shapes, crisp single chamfers, light wear, texture sets from
     `make_textures.py`. Budget 80k triangles per zone. Render before and after shots with `render_gym.py`
     (generalise it the same way) and send them to the owner.
3. **Pieces per zone** (the theme follows `Landmarks.cs` and the zone names):
   - **Concourse Lock:** a transit hall: glass balustrades, holo advertising columns, lock gates with warning
     lights, floor lane markings, mezzanine supports for the tiers, shock-panel floor plates, laser emitters.
   - **Storm Spire Climb:** a weather station tower: wet steel grating, lightning rods, cable trays, anemometers,
     storm shutters, ledge brackets for the tiers, lightning strike plates.
   - **Skyline Relay:** relay towers and dishes, antenna arrays, maintenance gantries, wind socks, crumbling
     panel sections for `collapse`, laser fence posts.
   - **Helix Foundry:** furnace housings with glowing vents, crucibles, pipework and valves, crane rails and hooks,
     crusher pistons, slag channels, catwalk railings, warning stripes.
   - **Undercity Descent:** fire escapes and balconies, neon signs, wet pavement, dumpsters and crates, hanging
     cables, shock floor grates, ceiling debris clusters.
   - **Lane stretches** (all zones): widen the deck to 7.2 m deep with lane guide lines, low cover walls and
     lane-side props.
   - **Landmarks:** Blender models replace the procedural reactor core, furnace dome, cooling tower, transit
     train and city blocks in `Landmarks.cs` (export as kit pieces; instance the repeated ones).
4. **Weather and atmosphere** (`Game/View/Vfx/Weather.cs`, when `weather` is on; camera-local particle volumes
   so the cost is constant):
   - Storm Spire: rain (stretched `streak` particles, slanted by wind, splashes on collision), lightning flashes
     (FxLights plus a sky flash), dark rolling clouds, mist.
   - Skyline Relay: wind streaks, drifting high clouds (Phase 5 layer), distant airships and pods with
     blinking lights.
   - Helix Foundry: steam puffs from vents, embers rising from furnaces, heat haze (Distort.shader) over them.
   - Undercity: drips from ledges, low haze, flickering neon (emissive pulse), a damp sheen (raise floor
     smoothness slightly).
   - Concourse Lock: sweeping floodlights (spot lights with cookies) and floating holo dust.
   - Movement Gym: keep today's ambience.
5. **The world reacting** (from the owner's earlier list; only what works with baked geometry):
   - deck lights flicker briefly when something heavy lands (`bossSlam`, `poundLand`, `ramSlam`): pulse the lamp
     materials' emission;
   - scuff decals that fade where players slide or land hard;
   - chips and sparks off deck panels on big hits;
   - flags whip when someone dashes past (`Ambience.Banner` cloths: add a gust when a player passes within 3 m);
   - a scrolling ticker on the Movement Gym sign (an animated texture offset on the sign's text material).
6. Verify (parity untouched; no Sim change here except the dump tool, which only reads) and push. Owner: walk
   each zone; send screenshots.

### Phase 11. Enemy and boss models

**Goal:** a Blender model for each enemy (`swarmer`, `shield`, `sniper`, `brute`, `post`, `turret`, `drone`,
`mortar`, `charger`) and boss (`warden`, the Lockwarden; `stormcaller`, the Stormcaller), driven by the existing
rigs so every animation, armour readout and hit flash keeps working.

1. **How the rigs work:** `Game/View/EnemyRigs.cs` builds each type from groups (`TObj`) stored in
   `EnemyParts` (`torso`, `head`, `gun`, `armN`, `armF`, `rotor`, `legsW` hips and knees, `hull`, `pod`,
   `hammer`, `blade`, `cannon` and so on) and meshes added to them; `EnemyRigs.Animate` (and `AnimateWarden`,
   `AnimateStorm`) moves those groups. Some meshes show state and must keep working: `P.plates` (armour: hidden
   one by one as armour breaks), `P.horns`, `P.tubes` (scaled), `P.shield` (the warden's shield bubble).
2. **Approach: rigid parts per node.** For each type, `Art/Blender/build_enemy_<type>.py` models a part for
   each rig node, in that node's **local** space (read the `Add(parent, geo, mat, x, y, z)` calls under each
   group: the part replaces those meshes and occupies the same envelope; convert positions with `P(x, y, z) =
   Vector((x, -z, y))` as in `build_ram_shield.py`). Node names: `body` for meshes added directly to the rig's
   body, otherwise the `EnemyParts` field name (`torso`, `gun`, `legsW[0].hip`, `rotors[2]`, ...). State meshes:
   model each as its own part named after the list entry (`plates[0]`, `horns[1]`, `tubes[3]`), centred on that
   mesh's own origin and orientation.
3. **Export:** `Art/Blender/export_enemy.py` (copy `export_ram_gear.py`) writes
   `Resources/NovaStriker/Models/enemy_<type>.json` = `{ "parts": [ { "node", "mat", "p", "n", "uv", "i" } ] }`,
   with crisp single chamfers and box UVs, within the budgets (A7). Materials: `Enemy_Plate`, `Enemy_Joint`,
   `Enemy_Energy` (and `Enemy_Glass` if needed).
4. **Design language:** pale ceramic plating, graphite joints, hostile magenta energy (as now), in the
   bold animated-cinematic style of the player models: readable silhouettes, chunky plates, glowing cores and
   eyes. Bosses: the Lockwarden as a heavy gatekeeper with a hammer and a projected shield; the Stormcaller as a
   floating storm engine with rotors, a pod of missile tubes and arcing coils. Render each with
   `render_turnaround.py` and send the sheets.
5. **Game (`Game/View/EnemyModels.cs`):** `EnemyModels.Apply(EnemyRig R)`, called in `View.cs` right after
   `EnemyRigs.Build(e.type)` and **before** `Look.AddOutlines(R.root, ...)` (so outlines wrap the new meshes),
   when `SETTINGS.enemyModels` and the JSON exists:
   - Resolve each part's node with a switch from the name to the `EnemyParts` field (and list index).
   - Map materials: `Enemy_Plate` → `R.mats.plate`, `Enemy_Joint` → `R.mats.joint`, `Enemy_Energy` →
     `R.mats.energy`, so hit flashes and armour glows keep working.
   - For ordinary nodes: hide the rig's meshes that belong to that node (its direct `TMesh` children and those
     in unnamed subgroups; not those under another named node, and never `P.shield`), then add the model's
     meshes as children of the node.
   - For state meshes: replace the mesh's geometry (`TMesh.geometry = modelMesh`); its visibility and scale
     logic keeps working.
   - Build meshes with `GeoBuilder` exactly as `ModelSkin.BuildGear` in `Models.cs` does (it already mirrors three.js to Unity).
6. Verify (no Sim change) and push. Owner: play the Concourse Lock and both boss fights; check each enemy's
   animation, armour breaking and hit flashes; send screenshots.

### Phase 12. Wrap-up

1. Update `README.md` (*Unity-only additions*: the effects, settings, clouds, birds, optimisation, level
   features, stage kits, enemy models) and `CLAUDE.md` (open threads, budgets, what has not been seen in Unity).
2. Run every check. Push. Wait for the GitHub build to be green.
3. Give the owner the Windows build: the build workflow uploads `NovaStriker-Windows` as an artifact. Find its
   id with `gh api repos/moderngentlemen-sudo/nova-striker-claude/actions/runs/<run id>/artifacts` and give the
   link `https://github.com/moderngentlemen-sudo/nova-striker-claude/actions/runs/<run id>/artifacts/<artifact id>`
   with steps: download (signed in to GitHub), unzip twice, keep the folder together, run `NovaStriker.exe`, and
   choose *More info › Run anyway* if Windows warns.

---

## Appendix 1. Troubleshooting

- **The type-check fails on a Unity API:** it does not exist in 6.3, or its namespace differs. Search the URP
  package source under `typecheck/unity/.../BuiltInPackages/com.unity.render-pipelines.universal/` for the
  right name, or use the serialized-property route (`NovaSetup.Set`).
- **The GitHub build fails on a shader:** read the log (A4). Typical causes: a property used outside
  `CBUFFER_START(UnityPerMaterial)`, a missing include, a type mismatch (half versus float), a missing
  `#pragma vertex` or `fragment` line.
- **Parity shows differences after a gameplay phase:** a feature runs while its setting is off, a random number
  is drawn, a box is added with defaults, or iteration order changed. Diff `Level.BOXES` with all features off
  against the previous commit (LevelTests has this test).
- **Something appears in the wrong place:** a three.js and Unity space mix-up (A6). Native Unity objects need
  `Th.P`.
- **Blender:** download 4.2 LTS into `/opt/blender` if missing
  (`https://download.blender.org/release/Blender4.2/blender-4.2.9-linux-x64.tar.xz`). Export to a scratch folder
  and copy only the `.json` or `.fbx` into `Resources` (exporters may write a `.blend` beside their output). A
  `cyl()` along x used to come out empty; `nova_lib.frame` is fixed, so do not reintroduce the old frame code.

## Appendix 2. When to stop and ask the owner

- Before making any level feature (tiers, hazards, lanes) on by default: that ends parity with the browser
  prototype, which is the owner's decision (CLAUDE.md, *Agreed direction*, item 4).
- Before changing Nova's shield colour, character designs, or any gameplay tuning not listed here.
- If a phase cannot meet its budget or a Unity feature is unavailable in 6.3: explain the trade-off and propose an
  alternative.
