# Nova Striker: Unity port of Version 13

This is a port of the Version 13 browser prototype in `../game` to Unity 6.3 with URP. It keeps the
prototype's scope: an isolated, hypothetical fresh-start track. The characters are procedural placeholder
rigs, and all sound and music is synthesized placeholder audio.

It is the same game: the same four characters and kits, zones, bosses, AI teammates, ultimates, settings and
look. The simulation is a line-by-line port, checked tick by tick against the JavaScript. The presentation is
rebuilt on Unity's renderer, audio and UI.

## Status: read this first

| Part | How far it has been checked |
|---|---|
| Simulation (`Sim/`) | **Verified.** The JavaScript and the C# are run side by side with the same inputs and compared on every tick (see *Parity tests*). All scenarios match. |
| Sound and music engine | **Rendered offline.** Outside Unity it rendered the score at all three intensities, plus a run of sound effects, to WAV. Levels were sensible, with no NaNs, at about 20× real time. |
| Everything else: view, effects, rigs, UI, input, editor setup | **Compiled, not run.** It compiles without errors against the Unity 6.3 engine assemblies, URP 17.3.0, Input System 1.17.0 and uGUI 2.0.0. It has **not** been run in the Unity editor. |

Expect a first pass in the editor to turn up visual problems that compiling cannot catch: a mirrored axis
somewhere, a colour that is too bright, a shader keyword that is missing. The code is laid out so these are
easy to find (see *Where things are*).

## Open it

1. Install **Unity 6000.3.25f1** with the Hub. Another 6000.3 release opens it, but use this exact version to
   avoid upgrade prompts and changed project files. Add
   the `unity/` folder as a project and open it.
2. The packages install from `Packages/manifest.json`: URP 17.3.0, Input System 1.17.0 and uGUI 2.0.0.
3. Run the menu **Nova Striker › Set Up Project**. It makes the assets the code needs:
   - the template materials (`Resources/NovaStriker/*.mat`)
   - the URP pipeline asset with two renderers. Renderer 0 is for High and Low. Renderer 1 is for Ultra and
     adds Screen Space Ambient Occlusion. Both run the Grade pass as a Full Screen Pass feature after
     post-processing.
   - linear colour space and the Input System backend
   - the scene `Scenes/NovaStriker.unity`, with its `GameMain` object

   If Unity asks to restart to switch input backends, say yes.
4. Open `Scenes/NovaStriker.unity` and press Play. Click, press a key, or press a gamepad button to join.

The controls are the prototype's. See `../README.md`, or press H or View in game.

## Testing on your own computer

This is the quickest way to try a change: there's no build to wait for.

1. **Get the code.** In GitHub Desktop, clone the repository and switch to the branch you're testing (for
   example `nova-striker-unity-claude`). Open `fresh-start-prototype/unity` in Unity and follow *Open it*
   above. The first import takes a while; later opens are quick.
2. **Pick up new changes.** Click *Fetch origin*, then *Pull origin*, in GitHub Desktop. Switch back to Unity:
   it recompiles on its own in a few seconds. Press Play.
3. **Run Set Up Project again** when a change touches `Editor/NovaSetup.cs` or adds a shader. It is always safe
   to run again: it rebuilds the generated assets in place.
4. **If something goes wrong,** open *Window › General › Console*. Copy the red errors into the chat: they say
   exactly where the problem is.
5. **Before sharing a version,** make one real build. Use *Nova Striker › Build for This Platform*, or the GitHub
   workflow (see *Building a game you can run*). The workflow builds from a clean copy of the repository, so it
   also catches anything that only works on your computer by accident. Play mode is very close to a built game,
   but speed, fullscreen and some shader details can differ.

**What stays on your computer.** The files *Set Up Project* and Unity make are listed in `.gitignore`, so git
leaves them out:

- the materials, pipeline, scene and settings;
- `ProjectSettings/`;
- every `.meta` file.

Committing from your computer only picks up real changes. Because `.meta` files are ignored, anything you add
to `Resources/NovaStriker/Models` (character models) stays on your computer too. That is fine for testing. To
keep models in the repository, the ignore rules for them need changing first.

## Where things are

```
Assets/NovaStriker/
  Sim/          the simulation in plain C# (no Unity references): world, players, enemies, bosses, bots, level
  Game/
    GameMain.cs fixed 60 Hz step, interpolation, hit-pause, joining, menus, team commands   (main.js)
    Three/      a thin three.js work-alike over GameObjects: TObj/TMesh/Group, geometry, TMat materials,
                a software 2D canvas (Paint) for generated textures, dynamic meshes and strips
    View/       View.cs (camera, lights, IBL, level baking, impact frames: render.js), Landmarks and
                Breakables, Look (route lighting and grade, environment maps, outlines, surfaces), rigs,
                animation, enemy rigs, character models, and every effect module (fx, charge, aegis, beam,
                sub-weapons, ultimates, RAM, Fix)
    Input/      Controls (keyboard, mouse, gamepads: input.js), Haptics (rumble), SettingsStore (PlayerPrefs)
    Audio/      WebAudio.cs (a small Web Audio work-alike), Sound (audio.js), Music (music.js)
    UI/         uGUI interface built in code: HUD, chips, markers, reticles, boss bar, ultimate letterbox,
                start screen, pause menu with every setting, controls screen
  Shaders/      Grade (ACES, route grade, shockwaves, seven impact frame styles), Unlit (sprites and
                particles), Outline, Rim, Sky, Aegis
  Editor/       NovaSetup (the setup menu)
SimTests/       the parity harness (dotnet + node); ShieldTests; typecheck; RigPreview (renders Nova's rig without Unity)
```

### How the port is put together

- **Coordinates.** All game code works in the prototype's right-handed three.js space. `TObj` mirrors
  positions and rotations into Unity's left-handed space by negating z. `S.W(x, y, depth)` places a sim point
  on the curving path, as `toWorld` does in the prototype.
- **Materials.** `TMat` clones a template (URP Lit, Complex Lit for clear coat, or the custom Unlit) and keeps
  three.js's property names. Colours are stored linear, as three.js stores them. Light intensities are
  three.js's divided by π.
- **Post-processing.** URP Bloom runs in a Volume that View creates. URP's tone mapping is off, because the
  Grade pass applies three.js's ACES curve and exposure itself, then the route's grade, the shockwaves, the
  impact frame and the dim during an ultimate.
- **Audio.** Nothing is recorded. `WebAudio.cs` mirrors the Web Audio nodes the prototype uses: oscillators,
  noise, biquads, gains, delay, compressor and parameter automation. It renders in `OnAudioFilterRead` on the
  camera, so `Sound` and `Music` port almost call for call.

## Unity-only additions

These are in the Unity build only, not in the browser prototype. Each one is off by default, so with its
default settings the simulation is still the prototype's (the parity tests run with the defaults).

- **Nova's absorbing shield** (Settings › *Nova's LT move, Marksman kit* › *Absorbing shield*). It replaces
  his dodge on LT (Q or L). While held, a blue hard-light shield stands where he aims and blocks strikes, shots and
  blasts from in front.
  - **Energy.** Every hit it blocks is absorbed as charge, which makes his attacks hit harder. A full 100%
    charge gives +100% damage. It keeps filling past full up to 150%, which gives +150% (2.5× damage); poise
    damage rises too. The HUD shows the charge and the bonus.
  - **Glow.** As the energy builds, his energy lines burn brighter, his armour picks up a warm rim of light
    and a soft aura grows around him. Each block makes this flare, and each quarter of the gauge sends out a
    pulse.
  - **Perfect block.** Raising it within 6 ticks of a hit costs no stability and gives more energy.
  - **Stability.** Each ordinary block wears down its stability, which grows back once the shield is lowered.
    If it breaks, he reels.
  - **Losing energy.** The energy holds for 6 seconds after the last block, then fades. An unguarded hit
    spills half of it.
  - **Firing.** He can fire, tap or charged, from behind it. Melee, a dash or a Level 4 beam lowers it.
  - The tuning is `NOVA_SHIELD` in `Sim/Config.cs`, the sim is `Sim/PlayerSim.Nova.cs` and the look is
    `Game/View/NovaShieldFx.cs`.
- **Perfect parries stun** (Settings › *Nova's perfect parry or shield block stuns the attacker*). A perfect
  parry (Sentinel kit) or a perfect shield block leaves the attacker dizzy:
  - light enemies for 80 ticks;
  - heavy enemies for 45 ticks;
  - bosses only reel.
- **Nova's 3D model** (Settings › *Character models* › *3D models*, the default). The model is
  `Resources/NovaStriker/Models/nova_model.fbx`, made by the scripts in `Art/Blender`.
  - **Animation:** its skeleton copies the built-in rig's pose every frame, so it moves exactly as the rig does,
    with every move and aim angle and no animation clips.
  - **Materials:** they are the rig's own, so the gold seams pulse with his charge.
  - **Fallback:** if the model can't load, the built-in rig is drawn instead.
- **Nova's helmet comes off at critical health** (Settings › *Nova's head* › *Full helmet*, the default).
  - **When:** at 25% health or less, or when he goes down, the helmet is knocked off. It flies up and back,
    tumbles, bounces and fades, and his face shows: a warning to the player.
  - **Coming back:** it returns once he is healed above 40% or revived.
  - **Code:** `Anim` (`HELMET_OFF`, `HELMET_BACK`) and `HelmetFx`.
- **The Movement Gym's dressing.** A kit modelled in Blender (`Art/Blender`) dresses the first zone without
  changing its shapes:
  - **Deck:** hull panels on the deck's faces (lit edge, pipe runs, vents, hatches, ribs, stencils), and deck
    plating with grip strips, floor lights and drains.
  - **Markings:** start line, chevrons and hazard edges.
  - **Panel and tunnel:** grip pads and framing on the wall-jump panel, and a warning band on the slide tunnel.
  - **Back terrace:** training gear with soft contact shadows, a glass railing, floodlights and the
    *MOVEMENT GYM* sign.
  - **Surfaces:** painted metal with light wear, rubber and tread plate.
  - **Code and data:** `GymDressing.cs` places it from `Resources/NovaStriker/Env/gym_kit.json`; the textures
    are the `.bytes` files beside it.
- **Movement in the sky** (the Skyport route; `Ambience.cs`):
  - **Banners and flags:** the terrace banners and three flags on tall poles ripple in the breeze.
  - **Clouds:** far clouds drift with the wind, and low cloud banks roll past below the deck.
  - **Sunlight:** soft cloud shadows sweep across the deck (a scrolling cookie on the sun), and the sun's light
    and glow rise and fall with the cover. Faint sunbeams fade in when it breaks through.
  - **Light motes:** flecks of light drift in the sun.
  - **Birds:** flocks wheel far out over the city and scatter when something big goes off.
  - **Elsewhere:** away from the open sky, it all fades out.
- **Sparks with weight and light** (`Sparks.cs`). Nova's skate blades throw sparks when he slides, skates fast,
  wall-slides, wall-jumps or lands hard. The sparks bounce along the floor two or three times, and each shower
  lights the deck with a flickering warm light.
- **Living displays.** The gym's holo screens and agility rings flicker and shimmer, and its sign breathes
  (`GymDressing.Animate`).
- **Reflections and sheen** (Settings › *Reflections on the deck*; High and Ultra). A second camera mirrors the
  scene in the floor, and thin sheets over the open sky's floors show it, strongest at grazing angles
  (`PlanarReflection.cs`, `Shaders/Reflect.shader`). High renders it at half size and Ultra at full size. The
  deck, hull paint and sky reflections are glossier too.
- **Quit game.** The start screen has a *Quit game* button. The pause menu has one too, which asks for a second
  press.
- **HDR output** (Settings › *HDR output*). On an HDR display with HDR turned on in the system, the game
  switches the display into HDR mode.
  - **How it works.** The Grade pass makes the same picture as in SDR. Highlights above a knee (energy, bloom,
    blasts) then rise toward the display's peak, up to 6× paper white.
  - **Build settings.** `allowHDRDisplaySupport` is on and `useHDRDisplay` is off, so the game starts in SDR
    until the setting is turned on.
  - **Not yet tested on an HDR display.** If highlights look wrong, the code to adjust is `HdrOut` in
    `Shaders/Grade.shader`.

## Differences from the browser prototype

- **Nova's look.** His rig follows his concept art, which the owner shared:
  - **Armour:** pearl-white armour over a navy bodysuit, with glowing gold seams.
  - **Chest and waist:** a high stand-up collar, the gold four-point star on his chest and a round gold belt
    buckle.
  - **Shoulders and legs:** rounded pauldrons, white side panels down the thighs, knee pads, and white greaves and
    boots.
  - **Bracer:** the Sentinel Bracer, a long white shell over his right forearm that reaches past the fist.

  His face shows, with swept-back dark hair. *Settings › Nova's head* switches to the full white helmet with its
  gold visor (a Unity-only setting, presentation only). The rig is `Rigs.BuildNovaRig`. It is still a procedural
  stand-in, until a real model goes in through *Character models*.
- **Echo's look.** His rig follows his concept art:
  - **Armour:** cream-white armour with bronze-gold trim over a dark charcoal undersuit, and orange energy.
  - **Head:** his bare face with spiky blond hair is the default (settings saved earlier switch to it once). The
    helmet and the mask are still in *Settings › Echo's head*.
- **RAM's look.** His rig follows his concept art (`Rigs.BuildRamRig`):
  - **Armour:** battle-worn gunmetal over dark joints, with electric-blue light in the abdomen's bands, the joint
    discs, the boots and the T visor.
  - **Head and build:** a rounded helm whose horns are armoured sensor fins, huge rounded pauldrons and big blocky
    fists. The fins are overlapping gunmetal blades with a blue channel, a sensor pod at each tip (the Breach
    Cannon's targeting array) and an actuator disc at the temple.
  - **Cannon:** the Breach Cannon over his right shoulder, with a blue muzzle ring.
  - **Shield:** the Rampart, a heavy stone-grey frame round a glowing hexagonal hard-light panel with a
    ram's-head emblem.
  - **Wear:** his armour carries the "worn" texture set (scratches, chipped paint and grime, made by
    `Art/Blender/make_textures.py`), on the rig and the model alike (`Look.Wear`).
- **RAM's 3D model** (the same *Character models* setting). The model is
  `Resources/NovaStriker/Models/ram_model.fbx` (`Art/Blender/build_ram.py`, then `rig_export_ram.py`). Like
  Nova's, it copies the rig's pose every frame and wears the rig's materials.
  - **Rampart:** modelled too (`build_ram_shield.py`, exported by `export_ram_shield.py` as `ram_shield.json`).
    It hangs on the rig's shield, so it is posed as before; the rig's glowing hex panel stays inside its frame.
  - **Breach Cannon:** not modelled; the rig's own stays visible over the model.
- **RAM's helmet comes off at critical health** (Settings › *RAM's head* › *Full helmet*, the default), as
  Nova's does (`Anim.Helmet`, `HelmetFx`). Under it is his face, designed for the game (the concept art shows none):
  a weathered veteran with a grey buzz cut and short grey beard, a scar through his left brow, and a cybernetic
  right eye glowing blue.
- **Fonts.** The UI uses Unity's built-in font. To use the prototype's fonts, put `SairaCondensed-Bold`,
  `Barlow-Medium` and `IBMPlexMono-Regular` in `Resources/NovaStriker/Fonts`.
- **Panels and text.** The HUD panels are not skewed. TextMeshPro is not used.
- **Reverb.** The music's reverb is algorithmic (Freeverb style) instead of a convolution with a generated
  impulse.
- **Ambient occlusion.** Ultra uses URP's SSAO, not three.js's GTAO.
- **Character models.** These go through `CharacterModel` assets instead of glTF files. Create one with
  *Create › Nova Striker › Character Model* in `Resources/NovaStriker/Models/` and name it after the
  character (`nova`, `echo`, `ram`, `fix`). Give it a prefab and clips per state, then set Settings ›
  Character models to *3D models*. Without one, the built-in rig is drawn, as in the prototype.
- **Settings storage.** Settings are kept in PlayerPrefs.
- **Gamepads.** Gamepads that Unity's Input System sees as `Gamepad` work, with the standard layout.

## Parity tests

`SimTests/` runs the C# simulation outside Unity. `parity.sh` replays the same scenario in node (the
prototype's own `world.js`) and in .NET, then compares the full state on every tick:

```
cd SimTests
sh parity.sh 1500 "1 2"    # every zone × every character × alone and with three AI teammates
```

It needs .NET 8 and Node 18 or later. It last reported every scenario matching.

## Building a game you can run (zip or executable)

**On GitHub, no Unity install needed.** `.github/workflows/unity-build.yml` (at the repository root) builds
Windows, macOS and Linux versions with [GameCI](https://game.ci) on every push that changes this project, or on
demand (Actions › Unity build › Run workflow). Each finished run lists `NovaStriker-Windows`,
`NovaStriker-macOS` and `NovaStriker-Linux` under *Artifacts*. GitHub wraps each download in its own zip, so
unzip twice. Then:

- **Windows:** run `NovaStriker.exe`.
- **macOS:** open `NovaStriker.app`. It isn't signed, so the first time, right-click it and choose *Open*.
- **Linux:** run `NovaStriker.x86_64`.

It needs your Unity licence once, as three repository secrets (Settings › Secrets and variables › Actions):

| Secret | Value |
|---|---|
| `UNITY_EMAIL` | your Unity account's email |
| `UNITY_PASSWORD` | its password |
| `UNITY_LICENSE` | the whole contents of `Unity_lic.ulf`, which Unity Hub writes once you sign in and activate a licence (the free Personal one is fine). Windows: `C:\ProgramData\Unity\Unity_lic.ulf`; macOS: `/Library/Application Support/Unity/Unity_lic.ulf`; Linux: `~/.local/share/unity3d/Unity/Unity_lic.ulf` |

With a Pro or Plus licence, set `UNITY_SERIAL` instead of `UNITY_LICENSE`. Until the secrets are set, the
workflow skips the build with a warning.

**On your own machine.** Use the menu **Nova Striker › Build for This Platform**. It runs the setup, then builds
to `Builds/<platform>/`. You can also build from a terminal:

```
Unity -batchmode -projectPath fresh-start-prototype/unity -buildTarget StandaloneWindows64 \
      -executeMethod NovaStriker.EditorTools.NovaBuild.Build
```

The project is pinned to Unity 6000.3.25f1, the newest 6.3 release GameCI has build images for. Any 6000.3
editor opens it.

The template materials keep the shaders and their keyword variants in the build. If a material looks wrong only
in a build, a keyword was probably stripped. Add a template with that keyword to `NovaSetup.TEMPLATES`.
