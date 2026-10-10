# Graphics update build

Work branch: `nova-striker-unity-sol-6-1`. Built code commit: `60428826e7d4c8727465f79698d3a6d738132521`. Engine: Unity 6000.3.25f1; URP 17.3.0. [Run 38057267941](https://github.com/moderngentlemen-sudo/nova-striker-claude/actions/runs/38057267941) passes the simulation/asset checks and actual Windows/macOS setup/build/upload steps. Linux remained in setup for over twenty minutes; this documentation checkpoint starts a fresh runner with unchanged game source. The final Linux build and rendered review are pending. Full release acceptance is separate.

## Launch the Windows player

1. [Download **NovaStriker-Windows**](https://github.com/moderngentlemen-sudo/nova-striker-claude/actions/runs/38057267941/artifacts/11671449416) while signed into GitHub.
2. Extract the download. It contains `NovaStriker-Windows.zip`; extract that archive too.
3. Open the extracted folder and double-click **NovaStriker.exe**. Keep `NovaStriker_Data`, `UnityPlayer.dll` and the other supplied files beside it. A Unity editor install is not required.
4. Click or press a key/gamepad button to join. Press **H** (gamepad View) for controls. **Esc/P** (gamepad Menu) opens pause/settings.
5. In **Effects**, cycle Cinematic, Balanced, Classic or Custom. Individual effect choices preserve the current preset values when entering Custom. Low graphics caps decorative particles/lights/distortion/surface marks. Reduced screen effects lowers shake, flashing and distortion.
6. In marked depth-lane stretches, use **B/M** for back/front hops, or click the left stick while holding up/down. Level feature changes apply together at the next zone load or checkpoint reset.

## Builds and review evidence

| Artifact | Link | Check |
|---|---|---|
| Windows | [Player download](https://github.com/moderngentlemen-sudo/nova-striker-claude/actions/runs/38057267941/artifacts/11671449416) | Actual Unity player build; both ZIPs pass integrity checks; required executable/data/assemblies present |
| macOS | [Player download](https://github.com/moderngentlemen-sudo/nova-striker-claude/actions/runs/38057267941/artifacts/11672043883) | Actual Unity player build; native gameplay smoke test remains open |
| Linux | Pending exact-run artifact | Await actual setup/build/capture results |
| Unity captures/logs | Pending exact-run artifact | Await the full 44-still, motion, surface, persistence and resource review |

Artifacts require repository access and expire on **2026-11-09**. Windows archive SHA256: `2009b7136a890922bc8c067e0e60ef713358e165b4d605231af646d35f4edbca`. Its [archive check](checks/windows-6042882-verification.json) confirms valid inner/outer ZIPs and all required player files. All **132 level checks and 21 shield checks** pass; the trace project builds and runtime assets validate. Exact final Linux reports will be recorded alongside these checks.

The capture workflow uses 960×540 OpenGLCore on llvmpipe, a software renderer. Its results establish limited rendered/runtime evidence. They do not establish 1080p/60 fps or a completed native Windows/macOS playthrough.

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
