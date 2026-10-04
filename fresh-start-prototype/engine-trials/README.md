# Engine trials: Babylon.js and Godot

Two small slices built beside the three.js prototype, to compare how each engine feels and how the game is built
in it. Both cover the same ground: Nova on the **Helix Foundry** stretch (the approach, the pit, the trench that
bends round the camera, the smelter bend and the helix climb), on the same geometry and the same curved 3D path.
The three.js prototype in `../game` is unchanged.

| | three.js (the prototype) | Babylon.js slice | Godot 4.3 slice |
|---|---|---|---|
| Simulation | the game's own 2D sim (`world.js`, `player.js`…) | **the same modules, unchanged** | Nova's movement rewritten in GDScript on Godot's own 2D physics |
| What's in it | everything | Nova (whole kit), enemies and encounters, breakables, the camera | Nova's movement (run, jumps, wall slide and jump, dash, slide, drop-through, lift pads), a swing that breaks crates, the camera |
| Code for the slice | n/a | `babylon/main.js`, 365 lines (renderer only) | `godot/project/scripts/*.gd`, 849 lines (movement, level build, rig, camera) |
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

## Godot 4.3 (`godot/`)

`godot/project` is a Godot 4.3 project (open it in the editor, or run it with `godot --path godot/project`).
`export-level.mjs` writes the Helix Foundry's boxes, path pieces, lift pads and Nova's numbers from `level.js` and
`config.js` to `project/level.json`, so the slice runs on the same level and the same tuning.

- **How it's built:** collision is Godot's: each level box is a `StaticBody2D` (one-way platforms are a flag on the
  shape), Nova is a `CharacterBody2D` moved with `move_and_slide`, and her swing asks the physics space what is in
  front of her. The movement rules are ported from `player.js` (buffered jumps, coyote time, the jump cut, double
  jump, wall slide and jump, eight-way dash and dash-jump, slide, drop-through). The picture is 3D meshes laid
  along the same curved path, batched per material with `SurfaceTool`.
- **What came for free:** the editor (scenes, inspector, animation, profiler), real physics, input mapping with
  controllers, particles, a procedural sky, and native desktop, console-ready and mobile exports from the same
  project.
- **What it cost:** a rewrite. Everything the prototype does (four characters, enemies, bosses, co-op, bots,
  effects) would be ported to GDScript or C#. The web build is heavy: an 8 MB engine to download and compile before
  anything shows, and only the Compatibility renderer, whose sky lighting
  washed every surface out until the slice switched to a flat ambient light.
- **Feel:** close, but not the same. Godot's collision solver, floor snapping and margins differ from the
  prototype's own, and porting the movement exactly would need side-by-side tuning.

Web export: `godot --headless --path godot/project --export-release Web ../build/index.html` (needs the 4.3
export templates; the single-threaded variant, so no special server headers), then `python3 godot/package-web.py`
to make `dist/godot`. That copy carries the engine (gzipped) and the game pack as base64 inside scripts, like
Emscripten's single-file mode, so it can be published as a page; `?x=600` starts further along the stretch.
`godot --headless --path godot/project -- --autotest` runs a scripted run along the stretch and prints where Nova
gets.

## Reading the comparison

- To keep the game as it is and get a stronger renderer and tools: **Babylon.js**. The game's code stays; only the
  drawing changes.
- To build toward native and console releases with a full editor, and accept a port: **Godot**. Its web build is the
  weakest of the three; it shines as a native engine.
- three.js stays the lightest and keeps everything as it is; what it lacks (inspector, built-in pipeline) is
  already built in the prototype.
