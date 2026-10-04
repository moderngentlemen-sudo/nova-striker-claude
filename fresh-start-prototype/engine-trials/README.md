# Engine trials: Babylon.js and Godot

> **This is the Godot branch.** It carries the Godot port only (`godot/`); the Babylon.js slice lives on
> `claude/engine-babylon`, and the three.js game with both slices on `claude/wizardly-wozniak-r0q6no`. The comparison
> below covers all three.

Two builds beside the three.js prototype, to compare how each engine feels and how the game is built in it. The
Babylon.js slice covers Nova on the **Helix Foundry** stretch; the Godot build began as the same slice and is now a
port of the game (milestone 1 below). The three.js prototype in `../game` is unchanged.

| | three.js (the prototype) | Babylon.js slice | Godot 4.3 port |
|---|---|---|---|
| Simulation | the game's own 2D sim (`world.js`, `player.js`…) | **the same modules, unchanged** | the same sim ported to GDScript, matching the prototype tick for tick (golden traces) |
| What's in it | everything | Nova (whole kit), enemies and encounters, breakables, the camera | milestone 1: all three routes, Nova's whole Marksman kit, every enemy, both bosses, HUD, pause menu, controller support |
| Code | n/a | `babylon/main.js`, 365 lines (renderer only) | `godot/project`: sim 5,200 lines, views and UI 2,750, tests 2,300 |
| Engine download (compressed) | three.js about 0.26 MB | Babylon.js about 1.7 MB | the Godot engine wasm about 8 MB, plus 0.3 MB of loader |
| Rendering on the web | WebGL2, our own post chain | WebGL2 (WebGPU available), built-in pipeline | WebGL2 "Compatibility" renderer only on the web |

## Babylon.js (`babylon/`)

`index.html` loads Babylon.js from jsDelivr; `main.js` imports the prototype's simulation directly from
`../../game/js` and draws it. Keys: A/D move, Space jump, Shift dash, J melee, K fire, L parry, mouse aim, and
<kbd>`</kbd> opens Babylon's inspector (a live scene tree and property editor).

- **What came for free:** a full post-processing pipeline in one object (bloom, ACES tone mapping, FXAA, vignette:
  `DefaultRenderingPipeline`), a glow layer for every emissive part, filtered shadows, a particle system for sparks
  and debris, PBR materials with a reflection probe of our own sky, and mesh merging (one draw per material).
- **What it cost:** about 1.7 MB more to download than three.js, and a renderer to write. The simulation, input,
  level and rules came across without a line changed, so a switch to Babylon is a renderer rewrite (`render.js`,
  `fx.js`, the rigs, animation and effect modules, about 6,000 lines in three.js today), not a game rewrite.
- **Feel:** identical to the prototype, because it *is* the prototype's simulation.

Build a single-file copy (as published): from `engine-trials`,
`npx esbuild babylon/main.js --bundle --format=esm --outfile=/tmp/bab.js`, then put that script inline in place
of `<script type="module" src="./main.js">` in `babylon/index.html` (written to `dist/babylon-trial.html`).

## Godot 4.3 (`godot/`): the port

`godot/project` is no longer a slice: it is a port of the prototype to Godot 4.3, aimed at native desktop (and
later console) builds, with the web as a bonus. **Milestone 1** is in: all three routes (Skyport with the Storm
Spire and Skyline Relay, the Helix Foundry, the Undercity Descent) and the Movement Gym, playable with Nova's full
Marksman kit, every enemy type, both bosses (the Lockwarden and the Stormcaller), encounters, gates, breakables,
lift pads, pickups and Nova's ultimate. Open the project in the Godot 4.3 editor, or run it with
`godot --path godot/project`.

### How it is built

- **The simulation is ported, not re-imagined.** `project/sim/` is the prototype's `world.js`, `player.js`,
  `combat.js`, `enemies.js`, `bosses.js` and `level.js` ported function by function to plain GDScript classes
  (`World`, `PlayerSim`, `EnemySim`, `Combat`, `Bosses`, `Level`), stepped at the same fixed 60 Hz, in the same order,
  with the prototype's own box collision (not Godot's physics) and the same random draws (a seeded mulberry32 where
  the prototype calls `Math.random`). It does not know about nodes or rendering, so it runs headless in tests.
- **Same numbers.** `tools/export-data.mjs` writes `config.js` and the level, enemy and boss tables to
  `project/data/tuning.json` and `level.json`; `Tune.C` and `Tune.L` read them. Change a number in the prototype,
  re-run the exporter, and both games agree.
- **The views read the World.** `project/view/` draws it: `LevelView` (every route's boxes and set pieces, batched
  per material and per 48 m chunk of the path), `Actors` (Nova's and every enemy's rig, shots in one MultiMesh,
  beams, the Aegis, wells, pickups, boss lasers), `Fx` (particle bursts and flashes from the world's events) and
  `CameraRig` (`render.js` `updateCamera`: the interpolated follow, the lead, the ultimate's push-in, shake, and
  each route's fog, sky and sun). `project/ui/` has the HUD and the pause menu. `project/game/game.gd` steps the
  World in `_physics_process` (the physics tick rate is 60, so Godot's fixed step is the sim's) and draws every
  frame in between with the interpolation fraction.
- **Input** is Godot's Input Map (in `project.godot`, written by `tools/define_input.gd`): keyboard and mouse as in
  the prototype, and a standard controller (left stick, right-stick aim with an 18-tick grace, A jump, B dash,
  X melee, Y Aegis, RB attachment, LB secondary, LT dodge, RT fire, R3 lock-on, both triggers for the ultimate, Start
  to pause). The pause menu is navigable with a controller through Godot's focus.
- **Characters** are the prototype's procedural rigs rebuilt from primitives (`view/rig.gd`), posed by a port of
  `anim.js` (rest, combat and air poses, the attack key poses, the aiming layer, squash and stretch, the turn).
  `AnimationTree` comes in with skinned art: on primitive rigs, a code pose blender is the faithful port, and the
  state machine it would drive already lives in the sim.
- **Look.** The lighting is matched to the prototype's frames: three.js's lights are physically scaled (intensity
  over pi), its ACES curve differs from Godot's (exposure 0.8 and a far white point here), the hemisphere light is
  the sky's radiance pass (the sky shader gives sky colour above, ground colour below), and the bloom only takes
  emissive light over 1.5. The web build's Compatibility renderer gets a flat ambient instead.

### Tests

`tools/run-tests.sh` (from `godot/`) runs 136 checks headless: the prototype's checks in `tests/*.mjs` that cover
Nova and the shared game, mirrored in `project/tests/` (movement, combat, enemies, Nova's kit, versions 7 to 9, all
three routes run end to end by a scripted Nova, both bosses, a soak), plus **golden traces**:
`tools/trace-js.mjs` runs the prototype's own simulation on three scripted scenarios (movement, wall and ledge, a
Foundry fight) and records every tick; `test_traces.gd` replays the inputs in Godot and requires every position,
velocity and state to match to 1e-6. Checks that need Echo use Nova's Sentinel kit, and co-op checks use two Novas.

```
cd godot && tools/run-tests.sh                   # all suites (GODOT=/path/to/godot picks the binary)
tools/run-tests.sh test_nova                     # suites whose file name contains "test_nova"
# from fresh-start-prototype, after changing the prototype:
node engine-trials/godot/tools/export-data.mjs   # re-export the tuning and level data
node engine-trials/godot/tools/trace-js.mjs      # re-record the golden traces
```

### Running and building

- `godot --path godot/project`: the game. After `--`: `--zone=gym|arena|tower|skyline|foundry|undercity`,
  `--boss=warden|stormcaller`; on the web the same as a query string (`?zone=foundry`).
- Desktop: the project has Linux and Windows presets (`godot --headless --path godot/project --export-release
  Linux ../build/linux/nova-striker.x86_64`); they need the 4.3 desktop export templates.
- Web: `godot --headless --path godot/project --export-release Web ../build/web/index.html` (the single-threaded
  template, so no special server headers), then `python3 godot/package-web.py` (from `engine-trials`) makes
  `dist/godot`, which carries the engine (gzipped) and the game pack as base64 inside scripts so it can be published
  as a page. The engine is 35 MB (about 10 MB gzipped) before anything shows; the pack is 0.36 MB.
- Builds land in `godot/build/` and `dist/`, both ignored by git.

### Not in milestone 1

Echo, RAM and Fix (the sim marks their branches M2); Nova's Sentinel kit is in the sim but not yet presented;
co-op on separate controllers and the AI teammates; the prototype's impact frames, trails, charge and Aegis effects,
props along the routes (planters, banners, lamps, the arch), audio, music and rumble.

## Reading the comparison

- To keep the game as it is and get a stronger renderer and tools: **Babylon.js**. The game's code stays; only the
  drawing changes.
- To build toward native and console releases with a full editor, and accept a port: **Godot**. Its web build is the
  weakest of the three; it shines as a native engine.
- three.js stays the lightest and keeps everything as it is; what it lacks (inspector, built-in pipeline) is
  already built in the prototype.
