# Working on the Unity build of Nova Striker

Notes for whoever continues this work.

## Branches

- **Work only on `nova-striker-unity-sol-6-1` for this copy.** The owner's branch instruction supersedes the inherited source-branch rule.
- **Active update plan:** [`docs/GRAPHICS_UPDATE_EXECUTION_PLAN.md`](docs/GRAPHICS_UPDATE_EXECUTION_PLAN.md). It audits the copied baseline and replaces the older plan's execution instructions. The core implementation and Blender source review are present. Read GRAPHICS_UPDATE_STATUS.md for exact checks and remaining release gates.
- **Leave the source branch `nova-striker-unity-claude` unchanged.**
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
- `cd LevelTests && dotnet run`: 105 deterministic checks for lanes, collision masks, projectiles, hazards, resets, combinations and tier waypoints. Failures must return nonzero.
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
  - **Look:** blue hard light (`#5fd4ff`, lighter than RAM's blue).
  - **Firing:** he can fire from behind it. Melee, a dash or a Level 4 beam lowers it.
  - **Charge:** blocked hits build a charge that overfills to 150%. Damage bonus is +100% at 100% and +150%
    (2.5×) at 150%.
  - **Glow:** Nova glows brighter as the charge grows.
  - **Code:** tuning is `NOVA_SHIELD` in `Sim/Config.cs`; the simulation is `Sim/PlayerSim.Nova.cs`; the block
    is in `Sim/Combat.cs`; the visuals are `Game/View/NovaShieldFx.cs` and `Anim.AbsorbGlow`; the HUD is
    `Game/UI/HudChips.cs`.
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
- **The graphics and level update** (`docs/NEXT_UPDATE_PLAN.md`). Its source checks and Blender renders are recorded in GRAPHICS_UPDATE_STATUS.md; only actual player evidence can establish Unity review.
  - **Effects:** `Game/View/Vfx/`. `Vfx` owns the particle layers (`FxPool`), effect lights, quad decals and the
    colliders particles bounce off (layer 9). `Fx.Cine.cs` reacts to events; `CineEvent` returning true replaces
    the classic reaction. Effects read `FxCfg`, never the raw settings. Textures are PNG data in
    `Resources/NovaStriker/Fx/*.png.bytes` (`Art/Blender/make_fx_textures.py`).
  - **New shaders:** `Distort`, `Cloud`, `Bird`, plus the particle templates. Each needs a `TEMPLATES` row in
    `NovaSetup` and the owner running *Set Up Project*.
  - **Clouds and gulls:** `CloudLayer.cs` and `Birds.cs`, drawn with `Graphics.RenderMeshInstanced`. Keep
    entire cloud/bird bounds behind the visible curved-route envelope, not just their centres. Clouds are lit impostors; the legacy key is still `volumetric`.
  - **Weather and the world reacting:** `Weather.cs`, per zone, from the camera's place. `View.lamps` and
    `View.neon` are the materials it flickers.
  - **Level features:** `Sim/LevelFeatures.cs` (tiers, hazards, depth lanes), applied in
    `Level.RestoreBoxes`. Extra boxes have ids of 10000 and up; `Level.Version` changes when boxes do. The view
    side is `LevelFx.cs`. Lane keys are B and M, or L3 with the stick up or down. `ShieldTests` turns the
    features off; `LevelTests` covers them. Browser parity is informational; deterministic level tests are the authority for these rules.
  - **Enemy models:** `EnemyModels.Apply` swaps each rig node's meshes for the model's parts (named
    `<node>__<what>` in Blender) and keeps the rig's materials, so flashes, armour and tube scaling still work.
    Budgets: small enemies about 3k triangles, heavy 7k, bosses 18k (all are well under).
  - **Limits:** complete AI traversal is pending despite deterministic tier waypoints and hazard avoidance; decals are quads; true cloud volumes and landmark LODs remain open.

## Open threads and ideas

- **The earlier design is recorded in [`docs/NEXT_UPDATE_PLAN.md`](docs/NEXT_UPDATE_PLAN.md); execute [`docs/GRAPHICS_UPDATE_EXECUTION_PLAN.md`](docs/GRAPHICS_UPDATE_EXECUTION_PLAN.md) on this branch:** graphics, effects
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
- **Effects to add later** (the owner asked to keep these for later):
  - **Atmosphere:** weather per zone (rain or mist on the Storm Spire, steam in the Foundry, drips and haze in the
    Undercity), a shifting time of day or a sunset zone, and more distant traffic (airships and pods with
    blinking lights).
  - **Light:** deck lights that flicker when something heavy lands, sweeping floodlights, a subtle lens flare
    toward the sun, and heat shimmer (furnaces, Nova's beam).
  - **The world reacting:** scuff marks that fade, railings that rattle, crash mats that dent, flags that whip
    when someone dashes past, chips and sparks off deck panels on big hits, and a dust or droplet overlay in mist
    and smoke.
  - **Living environment:** a scrolling stats ticker on the sign, distant trainees on far decks, and spinning
    vent fans, steam puffs and maintenance drones.
- **Reflections, sparks, birds and motes in Unity.** They have not yet been seen in Unity.
  - **Shader:** `Reflect.shader` is new, so the owner must run *Set Up Project* once. The GitHub build is its
    only compile check.
  - **If the reflection is upside down or offset:** on some graphics APIs the texture comes out flipped. Look at
    the uv in `Reflect.shader`, and the mirror matrix and oblique clip plane in `PlanarReflection.cs`.

- **The gym's dressing in Unity.** It has not yet been seen in Unity. It only dresses the zone (the boxes are
  unchanged). The next zones can follow the same pattern: a kit, a layout from their boxes, and an export.

- **Nova's 3D model in Unity.** It has not yet been seen in Unity. If it stands wrong (facing, scale, limbs),
  the code to adjust is `BuildDriven` in `Models.cs`. Settings › *Character models* › *Built-in rigs* is the
  fallback.
- **Echo's look.** His rig was restyled from the same concept art: cream-white armour, bronze-gold trim, a
  charcoal undersuit, and his bare face with spiky blond hair as the default. The concept also shows his flowing
  cream scarf (drawn by the effects code) and his orange blades.
- **Nova's shield colour.** The concept art shows a gold hexagonal (honeycomb) hard-light shield, but the absorbing
  shield is blue, which the owner asked for earlier. Ask before changing it.

- **Balance:** 2.5× damage at a full charge may be too strong. Offer to slow the charge or drain it sooner.
- **Beam and shield:** the Level 4 beam currently lowers the shield. Ask whether the owner would rather it
  stay up.
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
