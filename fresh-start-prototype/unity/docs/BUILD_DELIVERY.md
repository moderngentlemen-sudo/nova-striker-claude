# Graphics update build

Work branch: `nova-striker-unity-sol-6-1`. Built commit: `4d116a7c5c69cf3e1be461427fa236dcfda46ced`; its game source matches `60428826e7d4c8727465f79698d3a6d738132521` (the intervening commit changes documentation only). Engine: Unity 6000.3.25f1; URP 17.3.0. [Run 38058817385](https://github.com/moderngentlemen-sudo/nova-striker-claude/actions/runs/38058817385) passes the simulation/asset checks, actual Windows/macOS/Linux setup/build/upload steps and the complete Linux graphical review. Full release acceptance is separate. This documentation-only follow-up does not change the built game source.

## Launch the Windows player

1. [Download **NovaStriker-Windows**](https://github.com/moderngentlemen-sudo/nova-striker-claude/actions/runs/38058817385/artifacts/11672701536) while signed into GitHub.
2. Extract the download. It contains `NovaStriker-Windows.zip`; extract that archive too.
3. Open the extracted folder and double-click **NovaStriker.exe**. Keep `NovaStriker_Data`, `UnityPlayer.dll` and the other supplied files beside it. A Unity editor install is not required.
4. Click or press a key/gamepad button to join. Press **H** (gamepad View) for controls. **Esc/P** (gamepad Menu) opens pause/settings.
5. In **Effects**, cycle Cinematic, Balanced, Classic or Custom. Individual effect choices preserve the current preset values when entering Custom. Low graphics caps decorative particles/lights/distortion/surface marks. Reduced screen effects lowers shake, flashing and distortion.
6. In marked depth-lane stretches, use **B/M** for back/front hops, or click the left stick while holding up/down. Level feature changes apply together at the next zone load or checkpoint reset.

## Builds and review evidence

| Artifact | Link | Check |
|---|---|---|
| Windows | [Player download](https://github.com/moderngentlemen-sudo/nova-striker-claude/actions/runs/38058817385/artifacts/11672701536) | Actual Unity player build; both ZIPs pass integrity checks; required executable/data/assemblies present |
| macOS | [Player download](https://github.com/moderngentlemen-sudo/nova-striker-claude/actions/runs/38058817385/artifacts/11672212318) | Actual Unity player build; native gameplay smoke test remains open |
| Linux | [Player download](https://github.com/moderngentlemen-sudo/nova-striker-claude/actions/runs/38058817385/artifacts/11672591769) | Actual Unity player build; automated graphical smoke suite passes |
| Unity captures/logs | [Review download](https://github.com/moderngentlemen-sudo/nova-striker-claude/actions/runs/38058817385/artifacts/11673115595) | 44 named stills, 86 motion frames, four short clips, focused surface probe, resource counts and second-launch settings check |

Artifacts require repository access and expire on **2026-11-09**. Windows archive SHA256: `db571b20cfcde6dcf947e0ab2d31f924e8d11a47c4f00d3373d0e5390b5b9009`. Its [archive check](checks/windows-4d116a7-verification.json) confirms valid inner/outer ZIPs and all required player files. All **132 level checks and 21 shield checks** pass; the trace project builds and runtime assets validate. See [exact job/artifact records](checks/ci-4d116a7.json), [runtime report](checks/review-4d116a7.json), [focused surface report](checks/surface-4d116a7.json), [settings reload](checks/settings-reload-4d116a7.json) and [limited visual inspection](checks/visual-review-4d116a7.json).

The actual logs contain zero shader failures and runtime exceptions. The production projectile mark changes 121 visible pixels in the full suite and 166 in the focused probe, above the unchanged 24-pixel minimum. Full-suite floor/wall prototypes change 673/25 darkened pixels. Three native projectors are active. Valid Custom settings survive a second launch. Inspected latest charge/release/pause/Classic, chain, cloud and contact images provide limited visual evidence; earlier six-zone/enemy inspections are retained in the status history.

After bounded first use, ten reset samples stabilize at 708 materials, 2513 meshes and 18 lights. Three complete character cycles repeat their retained counts exactly. Peak decorative particles/effect lights/weather lights are 1727/8/3, within their High caps. The backdrop volume renders 376 frames at 24 steps; 29 spatial LOD groups are present. These results do not substitute for every live effect trigger or the full four-character combat stress matrix.

The capture machine uses an AMD EPYC 7763 CPU, 15989 MB RAM and llvmpipe LLVM 20.1.2 software GPU, OpenGLCore, 960×540 and High graphics. CPU/GPU timings were unavailable (zero), allocations unavailable (-1); mixed capture p95 was 1174.35 ms, with 832 draw calls, 130 SetPass calls and 1285224 triangles including repeated passes. This is not a fixed 60-second benchmark. No 1080p/60 fps or completed native Windows/macOS playthrough is claimed.

## Open the source in Unity

1. In GitHub Desktop, fetch/pull and switch to `nova-striker-unity-sol-6-1`.
2. Open `fresh-start-prototype/unity` with Unity **6000.3.25f1**.
3. Run **Nova Striker › Set Up Project** once, then open `Scenes/NovaStriker.unity` and press Play.

The owner’s 6000.6.2f1 editor remains a distinct compatibility check. Keep engine/package upgrades outside this update.

## Included changes

- Five reproducible Blender environment kits for arena, tower, skyline, foundry and undercity, placed from simulation layout data.
- Eleven validated enemy/boss exports and cheaper LODs, plus a refined white 1.3 m gull.
- True 3D Nova/RAM beam cores, charge cages and helical filaments, with native light particles gathering during charge and erupting on release. Classic remains independently selectable.
- Branching 3D chain lightning, electrified-body arcs, sparks and pooled ground illumination, with immutable target-depth snapshots.
- Layered combat VFX, independent Custom controls, reduced screen effects, immediate Off cleanup and aggregate native/legacy/weather budgets.
- Wider supporting decks, collision-aware three-lane hops and immutable projectile/device/trap origins; tier/hazard timing, reset and companion waypoint fixes.
- High/Ultra seeded half-resolution URP cloud volume; lit impostors for Balanced/Low and unsupported renderers. Full cloud bounds stay behind the playable envelope.
- Preserved 48 m spatial geometry baking, spatial kit instancing and landmark/city LODs, with reduced reflection cost and explicit runtime ownership.
- Native projected floor/wall marks with normal-aligned quad fallback. Projectile contacts snapshot actual surface normals/materials; ballistic abrasion and energetic scorch remain distinct.
- Pickup material caching and explicit graph/character cleanup, checked across repeated resets and character swaps.
- World-space particle layers keep simulating when their shared origin leaves the camera; the review checks modern blast counts and camera bounds.
- The renderer copies opaque depth before transparency so soft particles can draw; old reflection targets are destroyed when replaced.

## Remaining acceptance gates

[Graphics update status](GRAPHICS_UPDATE_STATUS.md) records exact authored, compiled, headless-tested and Unity-reviewed states. Native Windows/macOS playthroughs, controller/local multiplayer, every effect trigger, full human+three-bot traversal, all route/FOV/teleport backdrop conditions and reference GPU 1080p performance remain open. Owner-editor compatibility is also unverified. Automated valid-Custom persistence and targeted captures are narrower checks, not full acceptance of the execution plan.

High/Ultra use a seeded half-resolution URP cloud volume, with lit impostors on Balanced/Low and unsupported renderers. Surface marks use native projected decals with a quad fallback. Spatial landmark/city LODs and scoped effect/device origins are implemented. Full curved-camera traversal, every material/impact trigger and measured performance remain open. Software OpenGL captures establish limited rendered evidence and shader/runtime results, not a 60 fps target-machine result.
