# Blender source review

Generated with Blender 4.2 (official bpy wheel), Cycles CPU, AgX, fixed lighting. These are source art renders. They do not prove Unity shader/rig/motion correctness.

| Stage | Wide / gameplay / detail sheet |
|---|---|
| Concourse Lock | [arena](arena.webp) |
| Storm Spire | [tower](tower.webp) |
| Skyline Relay | [skyline](skyline.webp) |
| Helix Foundry | [foundry](foundry.webp) |
| Undercity | [undercity](undercity.webp) |

[Enemy role portraits](enemy-lineup.webp), [white gull](bird.webp).

Turnarounds: [swarmer](swarmer.webp), [shield](shield.webp), [sniper](sniper.webp), [brute](brute.webp), [post](post.webp), [turret](turret.webp), [drone](drone.webp), [mortar](mortar.webp), [charger](charger.webp), [Lockwarden](warden.webp), [Stormcaller](stormcaller.webp).

To reproduce from the Unity project directory:

```sh
dotnet run --project SimTests/LayoutExport/LayoutExport.csproj -- Art/Blender/stage_layout.json
blender -b -P Art/Blender/author_update.py -- Art/Review
blender -b -P Art/Blender/review_update.py -- Art/Review
python3 SimTests/validate_assets.py
```

The same scripts can run with `python` and the official `bpy==4.2.0` wheel. Editable .blend and PNG intermediates live in Art/Review and are ignored; every build input is tracked. Kit placements use actual simulation boxes and sampled route transforms. Runtime exports contain only mesh/material/layout data, not absolute authoring paths. Source review found distinct hostile silhouettes and a mostly white gull; animation, placement and all LOD changes still need the in-game evidence recorded in GRAPHICS_UPDATE_STATUS.
