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

- `bash parity.sh`: the C# simulation against the JavaScript prototype, tick by tick. It must stay at
  `N match, 0 differ`. Every Unity-only option defaults **off** so this keeps holding; keep it that way for
  new options.
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

## Open threads and ideas

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
  4. Decide whether Unity becomes the main version. Once it does, gameplay changes no longer need to match
     the browser prototype.
- **Browser builds:** a web build would be silent until the audio is reworked. The game synthesizes sound in
  `OnAudioFilterRead`, which Unity's web build doesn't support.

## Conventions

- Match the surrounding code's style: dense, with brief comments that explain why.
- Never put a model name or identifier in commits, code or docs.
