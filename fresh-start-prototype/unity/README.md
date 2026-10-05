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

1. Install **Unity 6.3** (6000.3.x) with the Hub. Add the `unity/` folder as a project and open it. If the Hub
   asks to change the editor version, pick any 6000.3 release.
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
SimTests/       the parity harness (dotnet + node)
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

## Differences from the browser prototype

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

## Building

Use a normal Unity build of `Scenes/NovaStriker.unity`. The template materials keep the shaders and their
keyword variants in the build. If a material looks wrong only in a build, a keyword was probably stripped.
Add a template with that keyword to `NovaSetup.TEMPLATES`.
