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
- `RigPreview/`: `sh preview.sh out.png` renders Nova's rig (`Rigs.BuildNovaRig`) from five angles without
  Unity. It converts the C# to three.js mechanically, so it shows the same numbers. Use it after changing his
  rig, and send the picture to the owner. Set `CHROMIUM=/opt/pw-browsers/chromium` in the cloud container.
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

- **Echo's look.** The owner's concept art shows Echo too: a bare face with spiky blond hair, cream-white armour
  with gold and orange accents, a flowing cream scarf, and orange blades. Compare the rig against it.
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
