# Graphics update build

Work branch: `nova-striker-unity-sol-6-1`. Built code commit: `007975eb3488221c6b7f5ab800510a4a46da6757`. Engine: Unity 6000.3.25f1; URP 17.3.0. [Run 38035359477](https://github.com/moderngentlemen-sudo/nova-striker-claude/actions/runs/38035359477) passed the simulation/asset checks and actual setup/build/upload steps for Windows, macOS and Linux. Its Linux graphical review passed with zero shader failures or runtime exceptions. Full release acceptance is separate. The documentation-only follow-up does not change this built code.

## Launch the Windows player

1. [Download **NovaStriker-Windows**](https://github.com/moderngentlemen-sudo/nova-striker-claude/actions/runs/38035359477/artifacts/11663661922) while signed into GitHub.
2. Extract the download. It contains `NovaStriker-Windows.zip`; extract that archive too.
3. Open the extracted folder and double-click **NovaStriker.exe**. Keep `NovaStriker_Data`, `UnityPlayer.dll` and the other supplied files beside it. A Unity editor install is not required.
4. Click or press a key/gamepad button to join. Press **H** (gamepad View) for controls. **Esc/P** (gamepad Menu) opens pause/settings.
5. In **Effects**, cycle Cinematic, Balanced, Classic or Custom. Individual effect choices preserve the current preset values when entering Custom. Low graphics caps decorative particles/lights/distortion/surface marks. Reduced screen effects lowers shake, flashing and distortion.
6. In marked depth-lane stretches, use **B/M** for back/front hops, or click the left stick while holding up/down. Level feature changes apply together at the next zone load or checkpoint reset.

## Builds and review evidence

| Artifact | Link | Check |
|---|---|---|
| Windows | [Player download](https://github.com/moderngentlemen-sudo/nova-striker-claude/actions/runs/38035359477/artifacts/11663661922) | Actual Unity player build; both ZIPs pass integrity checks; required executable/data/assemblies present |
| macOS | [Player download](https://github.com/moderngentlemen-sudo/nova-striker-claude/actions/runs/38035359477/artifacts/11664165907) | Actual Unity player build; native gameplay smoke test remains open |
| Linux | [Player download](https://github.com/moderngentlemen-sudo/nova-striker-claude/actions/runs/38035359477/artifacts/11663781121) | Actual Unity player build; automated graphical smoke suite passes |
| Unity captures/logs | [Review download](https://github.com/moderngentlemen-sudo/nova-striker-claude/actions/runs/38035359477/artifacts/11663731391) | 24 stills, 40 motion frames and skyline clip; stage/enemy/preset stills inspected |

Artifacts require repository access and expire on **2026-11-09**. Windows archive SHA256: `a783c26c2f13a1f05bc1139015a5b014539b3e4c7e6197071d2f1ad38d844455`. [Exact job/artifact records](checks/ci-007975e.json), [runtime report](checks/review-007975e.json) and [archive check](checks/windows-007975e-verification.json) are tracked with this delivery. All 21 shield checks and 105 level checks pass. After warmup, reset resource counts stabilize and three character cycles repeat exactly.

The captures ran at 960×540 using OpenGLCore on llvmpipe, a software renderer. They verify visible fire/smoke/debris, stage/enemy rendering and shader/runtime behavior. They do not establish 1080p/60 fps or a completed native Windows/macOS playthrough.

## Open the source in Unity

1. In GitHub Desktop, fetch/pull and switch to `nova-striker-unity-sol-6-1`.
2. Open `fresh-start-prototype/unity` with Unity **6000.3.25f1**.
3. Run **Nova Striker › Set Up Project** once, then open `Scenes/NovaStriker.unity` and press Play.

The owner’s 6000.6.2f1 editor remains a distinct compatibility check. Keep engine/package upgrades outside this update.

## Included changes

- Five reproducible Blender environment kits for arena, tower, skyline, foundry and undercity, placed from simulation layout data.
- Eleven validated enemy/boss exports and cheaper LODs, plus a refined white 1.3 m gull.
- Layered combat VFX, independent Custom controls, reduced screen effects and aggregate native/legacy/weather budgets.
- Wider supporting decks, collision-aware three-lane hops and immutable projectile lanes; tier/hazard timing, reset and companion waypoint fixes.
- Spatial geometry/kit rendering, reduced reflection cost and backdrop exclusion; bounded background cloud clusters.
- Pickup material caching and explicit graph/character cleanup, checked across repeated resets and character swaps.
- World-space particle layers keep simulating when their shared origin leaves the camera; the review checks modern blast counts and camera bounds.
- The renderer copies opaque depth before transparency so soft particles can draw; old reflection targets are destroyed when replaced.

## Remaining acceptance gates

[Graphics update status](GRAPHICS_UPDATE_STATUS.md) records exact authored, compiled, headless-tested and Unity-reviewed states. Native Windows/macOS playthroughs, controller/local multiplayer, all effect triggers and settings persistence, full human+three-bot traversal and reference GPU 1080p performance remain open.

Clouds use lit impostors; the High/Ultra ray-marched cloud renderer is incomplete. Surface marks use pooled quads; projected wall decals, landmark LODs and full area-effect/depth/normal coverage remain open. Software OpenGL captures establish limited rendered evidence and shader/runtime results, not a 60 fps target-machine result.
