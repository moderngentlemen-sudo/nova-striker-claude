# Working on the Unity build of Nova Striker

Notes for whoever continues this work.

## Branches

- **Work only on `nova-striker-unity-claude`.**
- **Leave alone** `nova-striker-unity-astra` (the owner's separate copy) and `claude/wizardly-wozniak-r0q6no`.
- **Pull requests:** don't open one unless asked.

## How the owner works

- **Editor:** they run the project in **Unity 6000.6.2f1** on their own computer, with GitHub Desktop.
  - They pull your pushes, switch to Unity and press Play.
  - Unity upgraded the project locally from 6.3 to 6.6. They should **not** commit the changes this makes to
    `ProjectSettings/ProjectVersion.txt` and `Packages/manifest.json`.
  - When a change needs *Nova Striker › Set Up Project* run again, tell them. That is any change to
    `Editor/NovaSetup.cs`, a new shader, or a new template material.
- **GitHub build:** the workflow `.github/workflows/unity-build.yml` (GameCI) still builds with **6000.3.25f1**,
  the pinned version. It runs on every push under `fresh-start-prototype/unity/**`.
  - Code must compile on **both 6.3 and 6.6**. Example: `LayoutGroup.SetLayoutInputForAxis` broke on 6.6, so
    `FlowLayout` overrides the layout properties instead.
  - The build fails if any error is logged, including shader errors.
- **Tone:** the owner is not a programmer. Give plain, numbered steps for anything they do in Unity or GitHub
  Desktop. Ask them for Console errors as text and for screenshots.

## Checks to run before pushing

All run without Unity, from `SimTests/`:

- **Unity is the primary platform now** (the owner decided; Unreal Engine may follow). Gameplay no longer has to
  match the browser prototype, and new options may default on.
- `bash parity.sh`: the C# simulation against the JavaScript prototype, tick by tick. Informational only now: it
  will differ once Unity's gameplay moves on. Use it to spot unintended simulation changes during graphics work.
- `cd ShieldTests && dotnet run`: headless tests of the Unity-only options. These cover Nova's shield (blocks,
  energy, overfill to 150%, firing behind it, perfect blocks) and the parry stun. It must print `ALL PASS`.
  Extend it when you change those options.
- `RigPreview/`: `sh preview.sh nova|echo|ram out.png` renders a character's rig (faces, not helmets) from six angles without Unity. It converts the C# to three.js mechanically, so it shows the same numbers. Use it after changing his
  rig, and send the picture to the owner. Set `CHROMIUM=/opt/pw-browsers/chromium` in the cloud container.
- `Art/Blender/` (outside `SimTests/`): scripts that model the characters in Blender 4.2. In the cloud
  container, Blender can be downloaded from download.blender.org into `/opt/blender`.
  - **Review renders:** `BLENDER=<path> sh render.sh build_nova.py <out-dir> <name> [samples] [helmet]` builds
    Nova and renders a review sheet (front, three-quarter, side, back, face).
  - **Gym kit:** `build_gym_kit.py` models it and `gym_layout.py` places it (from the level's boxes).
    `render_gym.py` renders before/after review shots. `export_kit.py` writes
    `Resources/NovaStriker/Env/gym_kit.json`. `make_textures.py` writes the paint, worn (RAM), rubber, tread and shadow
    textures (`.bytes`) beside it.
  - **Export to the game:** `blender -b <out-dir>/nova.blend -P rig_export_nova.py --
    ../../Assets/NovaStriker/Resources/NovaStriker/Models/nova_model.fbx` rigs and exports it. Commit the FBX.
    Its bones must keep the rig's joint names, which `Models.cs` drives (`MAP`).
  - **RAM:** `build_ram.py` (set `NS_TEX=<folder of make_textures.py's PNGs>` for the battle-worn review
    renders, and leave it unset for the export), then `rig_export_ram.py` writes `ram_model.fbx` the same way.
    His rigid pieces follow bones by name prefix (`RULES`); keep a new piece's name matching one. His face and
    helmet go in the `Ram_FaceAndHair` and `Ram_Helmet` collections (`render.sh ... helmet` renders the helmet).
    `build_ram_shield.py` and `build_ram_cannon.py` model the Rampart and the Breach Cannon, and
    `export_ram_gear.py` writes each as `Models/ram_shield.json` or `ram_cannon.json`.
  - **Exports land in `Resources`:** write only the `.fbx` or `.json` there. `rig_export_*.py` also saves a
    `*_rigged.blend` beside its output, so export to a scratch folder and copy the file, or delete the `.blend`.
  - **Style and budgets:** the owner asked for fewer triangles and the look of an animated game cinematic: bold,
    simple shapes, crisp single chamfers, light wear. Nova is heavier-set than his first model (`nova_bulk`,
    which his skeleton also goes through). Budgets: Nova about 26k triangles, RAM about 25k, the Rampart 4k, the
    cannon 3k. Keep new pieces within them.
  - **`nova_lib.cyl` along the x axis:** until a fix to `frame`, such cylinders came out empty. RAM's joint
    discs and the gym's rails and edge pipes were missing until they were re-exported.
- `typecheck/`: type-checks the C# against the Unity 6.3 libraries. It needs a one-time download of the Unity
  Linux editor and packages; see `typecheck/README.md`. Shaders can't be compiled here: the GitHub build is
  the shader check.

## What the Unity build adds

The README's *Unity-only additions* has the full description.

- **Nova's absorbing shield.** Setting `novaDefense = "shield"`, Marksman kit; it replaces the LT dodge.
  - **Look:** a personal energy shield: a curved honeycomb shell of hex tiles in front of him, over exactly the arc
    the block covers (`World.GuardFaces`: everything ahead of a line 0.25 m behind his chest, so it wraps a
    little past straight up and down). Blue (`#5fd4ff`) when empty, tiles turn gold (`CHARS["nova"].energy`)
    as `p.absorb` rises (from hit points and outward from the front; ~86% of tiles at 100%, all at 150%, which
    also glows hard). Hits ripple it, worn stability cracks edge tiles off, a break shatters it; it assembles
    from the edges when raised and scatters when lowered. Only a future ultimate (Eclipse Armor) would cover
    him completely. `NovaShieldShell` (no Unity types) holds the tiles, `NovaShieldFX` draws them as one mesh
    with vertex colours and the procedural tile texture (no new shader).
  - **Firing:** he can fire from behind it. Melee, a dash or a Level 4 beam lowers it.
  - **Charge:** blocked hits build a charge that overfills to 150%. Damage bonus is +100% at 100% and +150%
    (2.5×) at 150%.
  - **Glow:** Nova glows brighter as the charge grows.
  - **Code:** tuning is `NOVA_SHIELD` in `Sim/Config.cs`; the simulation is `Sim/PlayerSim.Nova.cs`; the block
    is in `Sim/Combat.cs`; the visuals are `Game/View/NovaShieldFx.cs`, `NovaShieldShell.cs` and
    `Anim.AbsorbGlow`; the HUD is `Game/UI/HudChips.cs`.
- **Nova's design.** His rig follows the concept art the owner shared in chat:
  - **Head:** his face with swept-back dark hair, or the white helmet with the gold visor (`novaHead` setting).
  - **Suit:** pearl-white armour over navy, gold seams, the gold star on his chest, a high collar and a round
    gold buckle.
  - **Bracer:** the long white Sentinel Bracer on his right forearm.

  Gold is his colour (`CHARS["nova"].energy`). Ignore the main repository's blue "Strike Suit" Blender brief: it
  is a different direction. Check rig changes with `RigPreview`.
- **Parry stun.** Setting `novaParryStun`; the code is `World.ParryStun`.
- **Quit game.** On the start card and in the pause menu (`UiMenus.cs`).
- **HDR output.** Setting `hdr`; the code is `View.UpdateHdr` and `HdrOut` in `Shaders/Grade.shader`. It has
  not yet been tested on an HDR display.
- **The graphics and level update** (`docs/NEXT_UPDATE_PLAN.md`). None of it has been seen in Unity yet.
  - **Effects:** `Game/View/Vfx/`. `Vfx` owns the particle layers (`FxPool`), effect lights, quad decals and the
    colliders particles bounce off (layer 9). `Fx.Cine.cs` reacts to events; `CineEvent` returning true replaces
    the classic reaction. Effects read `FxCfg`, never the raw settings. Textures are PNG data in
    `Resources/NovaStriker/Fx/*.png.bytes` (`Art/Blender/make_fx_textures.py`).
  - **New shaders:** `Distort`, `Cloud`, `Bird`, plus the particle templates. Each needs a `TEMPLATES` row in
    `NovaSetup` and the owner running *Set Up Project*.
  - **Clouds and gulls:** `CloudLayer.cs` and `Birds.cs`, drawn with `Graphics.RenderMeshInstanced`. Keep
    everything in the backdrop at least 70 m (clouds) or 60 m (birds) behind the play plane.
  - **Weather and the world reacting:** `Weather.cs`, per zone, from the camera's place. `View.lamps` and
    `View.neon` are the materials it flickers.
  - **Level features:** `Sim/LevelFeatures.cs` (tiers, hazards, depth lanes), applied in
    `Level.RestoreBoxes`. Extra boxes have ids of 10000 and up; `Level.Version` changes when boxes do. The view
    side is `LevelFx.cs`. Lane keys are B and M, or L3 with the stick up or down. `ShieldTests` turns the
    features off; `LevelTests` covers them. With them off, `parity.sh` still matches.
  - **Enemy models:** `EnemyModels.Apply` swaps each rig node's meshes for the model's parts (named
    `<node>__<what>` in Blender) and keeps the rig's materials, so flashes, armour and tube scaling still work.
    Budgets: small enemies (swarmer, sniper, post, turret, drone, shield, charger) about 8k triangles, heavy (brute,
    mortar) 14k, bosses 30k; the models use 84–95% of them. `build_enemy_lib.py` sets the finish (round parts get
    sides by size, `seg`; two-segment bevels on big edges, which `export_enemy.py` keeps) and has a detail kit
    (`armour` plates over gunmetal undercuts, `joint` hinge drums, `piston` actuators with chrome rods, `cables`,
    louvred `grille`s, `sensor`/`lenses` clusters, `panel`s, thrusters, bolt rings, hull patches). The rig mirrors
    enemies in x, so +z always faces the camera: put detail there.
    - **Materials:** seven, each a Blender material, a name in `EnemyModels.Mat` and a per-rig `TMat` in
      `EnemyRigs.Mats`: `Enemy_Plate` (pearl, clear coat), `Enemy_Joint` (graphite), `Enemy_Metal` (gunmetal),
      `Enemy_Chrome`, `Enemy_Rubber`, `Enemy_Lens` (tinted glass that glows with the energy) and `Enemy_Energy`.
      `EnemyRigs.Lit` flashes all the solid ones together (hit flash, a dying boss's flicker); a new material must
      join it. `EnemyModels.Surfaces` gives modelled rigs surface detail (`Look.Detail`:
      paint on the plates, worn metal, rubber).
    - **Review renders:** `render_enemy.py` renders a built `enemy_<type>.blend` with the game's look (surface
      sets from `make_textures.py`'s previews in `NS_TEX`, rim, outlines), or beside Nova with `NOVA=<nova.blend>`.
      Keep its material values in step with `EnemyRigs.Mats`.
  - **Stage dressing:** `ZoneDressing.Build(view, zone)` loads `Env/<zone>_kit.json`. `SimTests/LevelDump`
    writes `Art/Blender/level_boxes.json`, which `zone_layout.py` places from (rerun it after changing the
    level). Build, export and render one stage with `build_zone_kit.py <zone> <out>`, `export_kit.py -- <json>
    <zone>` and `render_zone.py`. Placed triangles: about 54k (Concourse) to 320k (Foundry, Undercity) per
    stage, over the plan's 80k, but the 48 m blocks mean only a slice is drawn at a time.
  - **Controls:** `Game/Input/Bindings.cs` holds every rebindable action (`DEFS`) and the saved changes
    (`Settings.keyBindings`, only what differs from the defaults). `Controls` reads it; the editor is the
    *Change controls* tab of the help card (`UiMenus.cs`, `UpdateControls` captures the input). Guide text uses
    tokens (`{k:id}`, `{k1:id}`, `{p:id}`) that `Bindings.Expand` fills in. Esc, Enter, ` and the pad's
    Start/View/D-pad are reserved.
  - **Beams:** `Vfx/CineBeam.cs` draws the Level 4 beams (`BeamFx`), the Supernova (`UltFx`) and boss lasers
    (`FxEvents.SyncBossLasers`) unless Projectiles is Classic. `View.Flash` (a `ColorAdjustments` exposure
    spike, off with `fxFlash`) and `View.ShakeAtLeast` are its screen hooks.
  - **Enemy break-up:** `Vfx/EnemyBreakup.cs` (pieces on layer 10, colliding only with layer 9 and each other);
    View calls it for dead enemies and on `armorBreak`.
  - **Cinematic moments:** `Vfx/CineCam.cs` (focus, zoom, letterbox, time scale); `GameMain` scales the sim clock
    by `view.cine.TimeScale`; the bars are in `Ui.Letterbox`.
  - **Time of day:** `Vfx/TimeOfDay.cs` moves the atmosphere targets in `View.UpdateCamera`.
  - **Bots and hazards:** `Bots.HazardNear` (used in the movement step and as a final guard in `Out()`);
    `LevelTests` checks the slag, a crusher and a shock panel (each test fails without the avoidance).
  - **Performance:** `Vfx/RenderScale.cs` (pipeline asset render scale and upscaler), `Vfx.WarmUp`,
    `Vfx/LoadTimer.cs` (stage timings in the Console and the readout), IL2CPP in `NovaBuild` when the editor
    has it for the target (`NOVA_BACKEND` overrides).
  - **Limits:** decals are quads; landmarks have no LOD yet; the Windows and macOS GitHub builds are Mono.

## Open threads and ideas

- **The next update is planned in [`docs/NEXT_UPDATE_PLAN.md`](docs/NEXT_UPDATE_PLAN.md):** graphics, effects
  and particles with pause-menu options, clouds and birds, geometry optimisation, multi-tier levels, hazards,
  depth lanes, dressing for the remaining stages, and enemy models. Follow it phase by phase.

- **RAM's concept art** (the owner shared it in chat) shows a gunmetal mech-like tank with ram's horns and a
  T visor, and the hex-panel Rampart with a ram's-head emblem. The owner asked for futuristic horns with a use:
  they are now armoured sensor fins, with a sensor pod at each tip and an actuator disc at the temple. His charge shows the shield as a blue holographic
  ram's head: an idea for Siege Breaker's look. `Rigs.BuildRamRig` follows the art, and `RigPreview` draws him
  (`sh preview.sh ram`). His battle-worn 3D model (`ram_model.fbx`), modelled Rampart (`ram_shield.json`) and
  Breach Cannon (`ram_cannon.json`) are in the game, driven by the rig like Nova's. His helmet
  is knocked off at critical health (`ramHead` setting). His face is our design (the concept shows none): a grey
  buzz cut and beard, a scar, a blue cybernetic right eye; the owner may want changes. None of this has been seen
  in Unity yet.
- **Effects still to add** (from the owner's list; weather, floodlights, the lens flare, heat shimmer, scuffs,
  flickering deck lights, whipping flags, panel chips and the sign's ticker are done):
  - a shifting time of day or a sunset zone;
  - railings that rattle, crash mats that dent, and a dust or droplet overlay in mist and smoke;
  - distant trainees on far decks, spinning vent fans and maintenance drones.
- **Reflections, sparks, birds and motes in Unity.** They have not yet been seen in Unity.
  - **Shader:** `Reflect.shader` is new, so the owner must run *Set Up Project* once. The GitHub build is its
    only compile check.
  - **If the reflection is upside down or offset:** on some graphics APIs the texture comes out flipped. Look at
    the uv in `Reflect.shader`, and the mirror matrix and oblique clip plane in `PlanarReflection.cs`.

- **The stages' dressing in Unity.** It has not yet been seen in Unity. It only dresses the zones (the boxes are
  unchanged).

- **Nova's 3D model in Unity.** It has not yet been seen in Unity. If it stands wrong (facing, scale, limbs),
  the code to adjust is `BuildDriven` in `Models.cs`. Settings › *Character models* › *Built-in rigs* is the
  fallback.
- **Echo's look.** His rig was restyled from the same concept art: cream-white armour, bronze-gold trim, a
  charcoal undersuit, and his bare face with spiky blond hair as the default. The concept also shows his flowing
  cream scarf (drawn by the effects code) and his orange blades.
- **Nova's shield colour.** The owner chose a blue honeycomb that turns gold as it charges (references: Mega Man
  11's hex-tile shield, anime energy flares). `Anim.AbsorbGlow` still tints his armour rim blue (`ABSORB_RIM`);
  it could follow the shell from blue to gold.

- **Balance:** 2.5× damage at a full charge may be too strong. Offer to slow the charge or drain it sooner.
- **Beam and shield:** the owner confirmed that a Level 4 beam keeps lowering the shield.
- **Agreed direction for the Unity move.** The owner was told about these options:
  1. Try a real character model and animations for Nova, through the existing CharacterModel hook
     (Settings › Character models).
  2. Redo the effects with VFX Graph.
  3. Cinemachine camera and more post-processing.
  4. Decided: Unity is the main version (and Unreal Engine may follow), so gameplay changes no longer need to
     match the browser prototype.
- **Browser builds:** a web build would be silent until the audio is reworked. The game synthesizes sound in
  `OnAudioFilterRead`, which Unity's web build doesn't support.

## Conventions

- Match the surrounding code's style: dense, with brief comments that explain why.
- Never put a model name or identifier in commits, code or docs.
