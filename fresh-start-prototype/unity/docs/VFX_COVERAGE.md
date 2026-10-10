# VFX coverage and review ledger

Inventory of literal `World.Emit` calls at the implementation commit. Presence of a handler is source coverage, not visual approval. Every row still needs its actual trigger reviewed in Unity. Dynamic event expressions and sustained render owners are listed separately below. New unknown presentation event names warn once instead of disappearing silently.

Depth: event reactions use the immutable `Ev.depth` snapshot; raw native particle/light APIs convert exactly once. Projectile cores and trails retain their emission lane. Area attacks still reach all lanes; their full visible coverage requires play review.

Policy: CineEvent's replacement cases suppress classic reactions. Persistent charge release, craters, finisher timing and owner-driven meshes retain the classic side effects. Hit decoration and projectile trails now select one backend. A wider additive-effect review remains pending. Cleanup: native decoration has bounded lifetimes and aggregate admission; sustained meshes/strips are owned by their actor/projectile and removed or orphaned on reset. See EFFECT_ORIGINS.md.

| Required family | Production owners | Review scenario | Unity trigger review |
|---|---|---|---|
| Nova standard/charged attachments, perfect release and beam | ChargeFx, BeamFx, FxSync, Fx.Cine | Fire each attachment at 0–4 charge, perfect release; maintain/release beam in each lane | Partial: outer-lane production charge/release, Classic and pause captured at `65198de`; other attachments/triggers pending |
| Scatter, grenade/cluster, chain, disc and gravity well | SubFx, FxSync, Fx.Cine | Bounce/detonate cluster, chain multiple targets, recall disc, open/collapse well while hopping | Partial: production chain/body arc/spark/pause captures at `65198de`; other triggers pending |
| Echo rifle, ranged attacks, snares, blades and glaive | FxEvents, ChargeFx, SubFx, scarf sync | Rifle focus/critical, plant snare, returning glaive, dash/lash in each lane | Pending |
| RAM cannon, Rampart, slams and energy release | RamFx, Fx.Cine, FxSync | Charged cannon, guard/break/store/release, quake and rush | Partial: charged-beam release/Classic/pause captured at `65198de`; other triggers pending |
| Fix weapons, deployables and rockets | FxEvents, FxSync, ChargeFx | Rivet/hot rivet, rocket jump, sentry rocket, burst/deploy/destroy equipment | Pending |
| All nine enemies and both bosses | EnemyRigs, FxEvents, FxSync, Fx.Cine | Muzzle, projectile, death, armour/tube break; every boss phase and sustained laser | Pending |
| Ultimate and team attacks | UltFx, Fx.Cine | Cast/join/run/cut/finisher/end, four participants in different lanes | Pending |
| Impacts, deflections, breakables | FxEvents, Breakables, Fx.Cine | Floor/wall/metal/glass, perfect deflect, armour plate, destroyed pillar | Pending |
| Hazards, weather and world reaction | LevelFx, Weather, Breakables | Every hazard idle/warn/impact/recovery, collapse; zone reset; particles Off | Pending |

Seven production settings checks cover preset-to-Custom snapshots, independent Classic/energy selection, Low caps and reduced screen effects. They run in LevelTests with the deterministic gameplay checks.

Historical blast review: player commit `007975e`, [run 38035359477](https://github.com/moderngentlemen-sudo/nova-striker-claude/actions/runs/38035359477), has inspected Cinematic/Balanced/Classic/Custom blast demonstrations, stage weather stills and a paused frame. Modern fire/smoke/debris are visible after opaque-depth ordering was fixed; peak decorative particles/effect lights/weather lights were 1682/4/3, within their High caps. These demonstrations call the visual helper directly and do not approve the live simulation event rows below. Every inventory trigger, full depth/normal alignment, persistence and matched attack-density review remains pending. See [runtime evidence](checks/review-007975e.json).

Settings: Cinematic/Balanced/Classic/Custom, independent explosion/projectile style, smoke/debris/lights/distortion/decals/screen, clouds/birds/weather/models. Custom snapshots effective preset values before editing. Reduced screen effects removes shake/impact/flash/distortion decoration. Warning machinery stays visible. Particle caps aggregate native and legacy systems including weather: 6000/3000/1200; weather/smoke yield capacity first. RAM pane shards and breakable mesh chunks participate in admission and immediate debris-Off cleanup. Critical feedback has separate limits/telemetry (82 general sprites/rings; 80 charge glints). Persistent projectile meshes, ribbons, hazard geometry, clouds/birds and UI are separate geometry, not particles; they remain in draw/triangle telemetry. Ambient weather lights are separate from the bounded transient impact-light pool (maximum three active lights: floodlights in the Concourse or one lightning light in the Tower, reported separately); GPU profiling remains pending.

Both modern and classic projectile emission advance at 60 steps/second with an eight-step catch-up limit. Weather is time-scaled. Hazard emission also advances at 60 steps/second; legacy sustained aura/deployable effects now share the bounded 60-step clock. The production clock passes equivalent ten-second 30/60/120 fps tests; complete attack-density comparison remains open.

## Literal event inventory

Each row has event snapshot depth unless it is purely UI/state. `State sync/UI/audio` means no independent burst is required: the effect follows simulation state, or the HUD/audio owns the event. Trigger the event through its listed simulation source. Capture and sign off each row before full release acceptance.

| Event | Simulation source | Handler / state policy | Lifetime | Unity review |
|---|---|---|---|---|
| `absorbSpill` | PlayerSim.Nova.cs | FxEvents.cs, Haptics.cs, NovaShieldFx.cs, Sound.cs | Owner or bounded reaction | Pending |
| `aegisHit` | World.cs | AegisFx.cs, FxEvents.cs, Haptics.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `aegisOff` | World.cs | AegisFx.cs, FxEvents.cs, Haptics.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `aegisOn` | World.cs | AegisFx.cs, FxEvents.cs, Haptics.cs, Sound.cs | Owner or bounded reaction | Pending |
| `ambush` | Combat.cs | FxEvents.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `amplify` | Combat.cs | FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `armorBreak` | Combat.cs | Fx.Cine.cs, FxEvents.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `armorHit` | Combat.cs | FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `attach` | PlayerSim.Kits.cs | FxEvents.cs, Sound.cs, Widgets.cs | Owner or bounded reaction | Pending |
| `banner` | World.Step.cs | Sound.cs, Ui.cs | Owner or bounded reaction | Pending |
| `bark` | World.cs | Sound.cs, Ui.cs | Owner or bounded reaction | Pending |
| `barrierBlock` | Combat.cs | FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `beamEnd` | World.cs | BeamFx.cs, FxEvents.cs, Haptics.cs, Sound.cs | Owner or bounded reaction | Pending |
| `beamStart` | PlayerSim.Kits.cs | BeamFx.cs, FxEvents.cs, Haptics.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `bleedOut` | World.Step.cs | State sync/UI/audio (audit trigger) | Owner or bounded reaction | Pending |
| `blocked` | Combat.cs | Fx.Cine.cs, FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `boost` | World.Step.cs | FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `bossCall` | Bosses.cs | FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `bossCrash` | Bosses.cs | FxEvents.cs, Haptics.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `bossDazed` | Bosses.cs | FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `bossDive` | Bosses.cs | FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `bossDown` | Combat.cs | Fx.Cine.cs, FxEvents.cs, Haptics.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `bossIntro` | Bosses.cs | Haptics.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `bossLaser` | Bosses.cs | FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `bossMissiles` | Bosses.cs | FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `bossPhase` | Bosses.cs | FxEvents.cs, Haptics.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `bossSlam` | Bosses.cs | Fx.Cine.cs, FxEvents.cs, Haptics.cs, Sound.cs, View.cs, Weather.cs | Owner or bounded reaction | Pending |
| `bounce` | Combat.cs | FxEvents.cs, Sound.cs, SubFx.cs | Owner or bounded reaction | Pending |
| `boxBreak` | World.Step.cs | Fx.Cine.cs, Landmarks.cs, View.cs, Weather.cs | Owner or bounded reaction | Pending |
| `boxChip` | World.Step.cs | Landmarks.cs, View.cs | Owner or bounded reaction | Pending |
| `bulwark` | World.cs | FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `burst` | World.cs | FxEvents.cs, Haptics.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `carve` | PlayerSim.cs | FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `chain` | World.cs | FxEvents.cs, Haptics.cs, Sound.cs, SubFx.cs, View.cs | Owner or bounded reaction | Pending |
| `challenge` | World.cs | FxEvents.cs, Haptics.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `chargeCrash` | Bosses.cs, Combat.cs, Enemies.cs | Fx.Cine.cs, FxEvents.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `chargeStart` | Bosses.cs, Enemies.cs | FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `checkpoint` | World.Step.cs | Sound.cs, Ui.cs | Owner or bounded reaction | Pending |
| `cluster` | World.cs | Fx.Cine.cs, FxEvents.cs, Sound.cs, SubFx.cs, View.cs | Owner or bounded reaction | Pending |
| `crescent` | PlayerSim.cs | FxEvents.cs, Haptics.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `crit` | World.cs | FxEvents.cs, Haptics.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `dash` | PlayerSim.cs | FxEvents.cs, Haptics.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `dashChargeEnd` | PlayerSim.cs | State sync/UI/audio (audit trigger) | Owner or bounded reaction | Pending |
| `dashChargeStart` | PlayerSim.cs | FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `dashSlash` | PlayerSim.cs | FxEvents.cs, Haptics.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `deflect` | World.cs | Fx.Cine.cs, FxEvents.cs, Haptics.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `discCatch` | Combat.cs | FxEvents.cs, Haptics.cs, Sound.cs, SubFx.cs | Owner or bounded reaction | Pending |
| `discFade` | Combat.cs | FxEvents.cs, SubFx.cs | Owner or bounded reaction | Pending |
| `discRecall` | World.cs | FxEvents.cs, Sound.cs, SubFx.cs | Owner or bounded reaction | Pending |
| `discThrow` | World.cs | FxEvents.cs, Haptics.cs, Sound.cs, SubFx.cs | Owner or bounded reaction | Pending |
| `dive` | PlayerSim.cs | State sync/UI/audio (audit trigger) | Owner or bounded reaction | Pending |
| `djump` | PlayerSim.cs | FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `dodge` | PlayerSim.Kits.cs | FxEvents.cs, Haptics.cs, Sound.cs, SubFx.cs | Owner or bounded reaction | Pending |
| `downed` | World.Step.cs | FxEvents.cs, Haptics.cs, Sound.cs, Ui.cs | Owner or bounded reaction | Pending |
| `enemyBlast` | Combat.cs, World.Step.cs, World.cs | Fx.Cine.cs, FxEvents.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `enemyShot` | Bosses.cs, Enemies.cs | Fx.Cine.cs, FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `erase` | World.Step.cs, World.Ult.cs, World.cs | FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `focusLost` | PlayerSim.Kits.cs | FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `focusUp` | PlayerSim.Kits.cs | FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `fortify` | World.Ult.cs | FxEvents.cs, RamFx.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `gadgetDeploy` | World.Step.cs | FixFx.cs, FxEvents.cs, Haptics.cs, Sound.cs | Owner or bounded reaction | Pending |
| `gadgetEnd` | World.Step.cs | FixFx.cs, FxEvents.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `gadgetHit` | World.Step.cs | FixFx.cs, FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `gadgetLand` | World.Step.cs | FixFx.cs, FxEvents.cs | Owner or bounded reaction | Pending |
| `gadgetSelect` | PlayerSim.Kits.cs | FixFx.cs, FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `gadgetUp` | World.Step.cs | FixFx.cs, FxEvents.cs, Haptics.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `gadgetWrench` | World.Step.cs | FixFx.cs, FxEvents.cs, Haptics.cs, Sound.cs | Owner or bounded reaction | Pending |
| `gates` | World.Step.cs | Sound.cs | Owner or bounded reaction | Pending |
| `grenadeThrow` | World.cs | FxEvents.cs, Sound.cs, SubFx.cs | Owner or bounded reaction | Pending |
| `guardBlock` | World.Step.cs | FxEvents.cs, Haptics.cs, RamFx.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `guardBreak` | Combat.cs | Fx.Cine.cs, FxEvents.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `guardOff` | PlayerSim.Kits.cs | Sound.cs | Owner or bounded reaction | Pending |
| `guardOn` | PlayerSim.Kits.cs | FxEvents.cs, Haptics.cs, RamFx.cs, Sound.cs | Owner or bounded reaction | Pending |
| `hazardHit` | LevelFeatures.cs | LevelFx.cs | Owner or bounded reaction | Pending |
| `hazardOff` | LevelFeatures.cs | State sync/UI/audio (audit trigger) | Owner or bounded reaction | Pending |
| `hazardOn` | LevelFeatures.cs | LevelFx.cs | Owner or bounded reaction | Pending |
| `hazardWarn` | LevelFeatures.cs | State sync/UI/audio (audit trigger) | Owner or bounded reaction | Pending |
| `hit` | Combat.cs | Fx.Cine.cs, FxEvents.cs, Haptics.cs, Sound.cs | Owner or bounded reaction | Pending |
| `impact` | Combat.cs | View.cs | Owner or bounded reaction | Pending |
| `intercept` | Combat.cs | FxEvents.cs, Haptics.cs, Sound.cs | Owner or bounded reaction | Pending |
| `interceptFail` | Combat.cs | FxEvents.cs | Owner or bounded reaction | Pending |
| `join` | World.Step.cs | FxEvents.cs, Sound.cs, Ui.cs | Owner or bounded reaction | Pending |
| `jump` | PlayerSim.Kits.cs, PlayerSim.cs | FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `kill` | Combat.cs, Enemies.cs, World.Step.cs | Fx.Cine.cs, FxEvents.cs, Haptics.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `kineticRelease` | World.Step.cs | Fx.Cine.cs, FxEvents.cs, Haptics.cs, RamFx.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `land` | PlayerSim.cs | Fx.Cine.cs, FxEvents.cs, Haptics.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `laneBlocked` | LevelFeatures.cs | LevelFx.cs, View.cs | Owner or bounded reaction | Pending |
| `laneHop` | LevelFeatures.cs | LevelFx.cs, View.cs | Owner or bounded reaction | Pending |
| `lash` | PlayerSim.cs | Sound.cs | Owner or bounded reaction | Pending |
| `lashAlly` | World.cs | State sync/UI/audio (audit trigger) | Owner or bounded reaction | Pending |
| `lashPull` | World.cs | FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `lashZip` | World.cs | FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `leap` | World.Step.cs | FxEvents.cs, Haptics.cs, RamFx.cs, Sound.cs | Owner or bounded reaction | Pending |
| `leapLand` | World.Step.cs | FxEvents.cs, Haptics.cs, RamFx.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `leash` | World.cs | FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `leashEnd` | World.cs | Sound.cs | Owner or bounded reaction | Pending |
| `leave` | World.Step.cs | Ui.cs | Owner or bounded reaction | Pending |
| `liftBounce` | World.Step.cs | Landmarks.cs, View.cs | Owner or bounded reaction | Pending |
| `link` | World.Step.cs | FxEvents.cs, Haptics.cs, RamFx.cs, Sound.cs | Owner or bounded reaction | Pending |
| `linkEnd` | World.Step.cs | FxEvents.cs, RamFx.cs, Sound.cs | Owner or bounded reaction | Pending |
| `linkHit` | World.Step.cs | FxEvents.cs, Haptics.cs, RamFx.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `linkNone` | World.Step.cs | FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `lock` | Enemies.cs | FxEvents.cs, Sound.cs, Widgets.cs | Owner or bounded reaction | Pending |
| `lockNone` | World.cs | Sound.cs | Owner or bounded reaction | Pending |
| `lockOff` | World.cs | Haptics.cs, Sound.cs | Owner or bounded reaction | Pending |
| `lostTrack` | Enemies.cs, World.cs | FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `meleeCharged` | PlayerSim.cs | Sound.cs | Owner or bounded reaction | Pending |
| `mortarShot` | Bosses.cs, Enemies.cs | FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `noScrap` | World.Step.cs | FixFx.cs, FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `notReady` | PlayerSim.Kits.cs | FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `nshieldBlock` | PlayerSim.Nova.cs | FxEvents.cs, Haptics.cs, NovaShieldFx.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `nshieldBreak` | PlayerSim.Nova.cs | FxEvents.cs, Haptics.cs, NovaShieldFx.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `nshieldOff` | PlayerSim.Nova.cs | Sound.cs | Owner or bounded reaction | Pending |
| `nshieldOn` | PlayerSim.Nova.cs | FxEvents.cs, Haptics.cs, NovaShieldFx.cs, Sound.cs | Owner or bounded reaction | Pending |
| `nshieldReady` | PlayerSim.Nova.cs | FxEvents.cs, NovaShieldFx.cs, Sound.cs | Owner or bounded reaction | Pending |
| `overhaulDone` | World.Ult.cs | FixFx.cs, FxEvents.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `overhaulPulse` | World.Ult.cs | FixFx.cs, FxEvents.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `padBounce` | World.Step.cs | FixFx.cs, FxEvents.cs, Haptics.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `padPlace` | World.Step.cs | FixFx.cs, FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `parry` | Combat.cs | FxEvents.cs, Haptics.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `parryFail` | Combat.cs | FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `parryStart` | PlayerSim.cs | State sync/UI/audio (audit trigger) | Owner or bounded reaction | Pending |
| `parryStun` | World.cs | FxEvents.cs, Haptics.cs, NovaShieldFx.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `patchOff` | PlayerSim.Kits.cs | Sound.cs | Owner or bounded reaction | Pending |
| `patchOn` | PlayerSim.Kits.cs | FixFx.cs, FxEvents.cs, Haptics.cs, Sound.cs | Owner or bounded reaction | Pending |
| `patchTarget` | World.Step.cs | FixFx.cs, FxEvents.cs | Owner or bounded reaction | Pending |
| `perfectDodge` | World.Step.cs | FxEvents.cs, Haptics.cs, Sound.cs, SubFx.cs, View.cs | Owner or bounded reaction | Pending |
| `perfectGuard` | World.Step.cs | FxEvents.cs, Haptics.cs, RamFx.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `perfectRelease` | World.cs | FxEvents.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `plateHit` | Combat.cs | Fx.Cine.cs, FxEvents.cs, Haptics.cs | Owner or bounded reaction | Pending |
| `playerHit` | Combat.cs | Fx.Cine.cs, FxEvents.cs, Haptics.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `plowCatch` | World.Step.cs, World.Ult.cs | FxEvents.cs, Haptics.cs, RamFx.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `podCall` | World.Ult.cs | FixFx.cs, FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `podLand` | World.Ult.cs | FixFx.cs, FxEvents.cs, Haptics.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `pogo` | PlayerSim.cs | FxEvents.cs, Haptics.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `poundDrop` | PlayerSim.cs | FxEvents.cs, Haptics.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `poundLand` | PlayerSim.cs | Fx.Cine.cs, FxEvents.cs, Haptics.cs, Sound.cs, View.cs, Weather.cs | Owner or bounded reaction | Pending |
| `poundLevel` | PlayerSim.cs | FxEvents.cs, Haptics.cs, Sound.cs | Owner or bounded reaction | Pending |
| `poundStart` | PlayerSim.cs | FxEvents.cs, Haptics.cs, Sound.cs | Owner or bounded reaction | Pending |
| `powerFade` | World.Step.cs | FixFx.cs, FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `powerSelect` | PlayerSim.Kits.cs | FixFx.cs, FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `powerToss` | World.Step.cs | FixFx.cs, FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `powerUp` | World.Step.cs | FixFx.cs, FxEvents.cs, Haptics.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `projWall` | Combat.cs | Fx.Cine.cs | Owner or bounded reaction | Pending |
| `provoke` | World.Step.cs | FxEvents.cs, Haptics.cs, RamFx.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `pursuit` | World.Step.cs | State sync/UI/audio (audit trigger) | Owner or bounded reaction | Pending |
| `quake` | World.Step.cs | Fx.Cine.cs, FxEvents.cs, Haptics.cs, RamFx.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `ramBonk` | World.Step.cs | FxEvents.cs, Haptics.cs, RamFx.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `ramSlam` | World.Ult.cs | Fx.Cine.cs, FxEvents.cs, Haptics.cs, RamFx.cs, Sound.cs, View.cs, Weather.cs | Owner or bounded reaction | Pending |
| `ramSplat` | World.Step.cs | FxEvents.cs, Haptics.cs, RamFx.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `rampartBreak` | World.Step.cs | Fx.Cine.cs, FxEvents.cs, Haptics.cs, RamFx.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `rampartReady` | PlayerSim.Kits.cs | FxEvents.cs, RamFx.cs, Sound.cs | Owner or bounded reaction | Pending |
| `recall` | World.Step.cs | FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `repairPulse` | World.Step.cs | FixFx.cs, FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `respawn` | World.Step.cs | FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `respawnAll` | World.Step.cs | State sync/UI/audio (audit trigger) | Owner or bounded reaction | Pending |
| `revived` | World.Step.cs | FxEvents.cs, Haptics.cs, Sound.cs | Owner or bounded reaction | Pending |
| `ricochet` | Combat.cs | Fx.Cine.cs, FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `rifleFocus` | PlayerSim.Kits.cs | FxEvents.cs, Haptics.cs, Sound.cs | Owner or bounded reaction | Pending |
| `rifleLower` | PlayerSim.Kits.cs | Sound.cs | Owner or bounded reaction | Pending |
| `rifleRaise` | PlayerSim.Kits.cs | FxEvents.cs, Haptics.cs, Sound.cs | Owner or bounded reaction | Pending |
| `rivetStick` | Combat.cs | FixFx.cs, FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `rocketJump` | World.cs | Fx.Cine.cs, FxEvents.cs, Haptics.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `rush` | PlayerSim.Kits.cs | FxEvents.cs, Haptics.cs, RamFx.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `rushEnd` | World.Step.cs | State sync/UI/audio (audit trigger) | Owner or bounded reaction | Pending |
| `scarfMode` | PlayerSim.Kits.cs | FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `scrap` | World.Step.cs | FixFx.cs, FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `sentryRocket` | World.Step.cs | FixFx.cs, Fx.Cine.cs, FxEvents.cs, FxSync.cs, Sound.cs | Owner or bounded reaction | Pending |
| `sentryShot` | World.Step.cs | FixFx.cs, FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `shot` | World.Step.cs, World.cs | Fx.Cine.cs, FxEvents.cs, Haptics.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `slam` | Enemies.cs | FxEvents.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `slide` | PlayerSim.cs | Fx.Cine.cs, Sound.cs | Owner or bounded reaction | Pending |
| `snarePlant` | World.cs | FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `snareThrow` | World.cs | FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `snareTrigger` | World.cs | FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `snared` | World.cs | FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `snipe` | World.cs | FxEvents.cs, Haptics.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `sparkRing` | World.Step.cs | FixFx.cs, FxEvents.cs, Haptics.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `spinStun` | World.cs | FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `split` | World.cs | FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `stagger` | Combat.cs, World.Step.cs | FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `subSwitch` | PlayerSim.Kits.cs | FxEvents.cs, Haptics.cs, Sound.cs, SubFx.cs | Owner or bounded reaction | Pending |
| `swap` | World.Step.cs | GameMain.cs, Ui.cs, UiMenus.cs | Owner or bounded reaction | Pending |
| `swing` | PlayerSim.cs | FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `tag` | Combat.cs, World.cs | FxEvents.cs | Owner or bounded reaction | Pending |
| `taunted` | World.Step.cs, World.cs | FxEvents.cs | Owner or bounded reaction | Pending |
| `teamFinisher` | World.Ult.cs | Fx.Cine.cs, FxEvents.cs, Haptics.cs, Sound.cs, UltFx.cs, View.cs | Owner or bounded reaction | Pending |
| `telegraph` | World.cs | FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `thrustOff` | PlayerSim.cs, World.Step.cs, World.Ult.cs | Sound.cs | Owner or bounded reaction | Pending |
| `tracer` | World.cs | Fx.Cine.cs, FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `ultBegin` | World.Ult.cs | FxEvents.cs, Sound.cs, UltFx.cs | Owner or bounded reaction | Pending |
| `ultCast` | World.Ult.cs | FxEvents.cs, Haptics.cs, Sound.cs, UltFx.cs, View.cs | Owner or bounded reaction | Pending |
| `ultCut` | World.Ult.cs | FxEvents.cs, Haptics.cs, Sound.cs, UltFx.cs, View.cs | Owner or bounded reaction | Pending |
| `ultEnd` | World.Ult.cs | FxEvents.cs, Sound.cs, UltFx.cs | Owner or bounded reaction | Pending |
| `ultFinisher` | World.Ult.cs | Fx.Cine.cs, FxEvents.cs, Haptics.cs, Sound.cs, UltFx.cs, View.cs | Owner or bounded reaction | Pending |
| `ultJoin` | World.Ult.cs | FxEvents.cs, Haptics.cs, Sound.cs, UltFx.cs, View.cs | Owner or bounded reaction | Pending |
| `ultNova` | World.Ult.cs | FxEvents.cs, Haptics.cs, Sound.cs, UltFx.cs, View.cs | Owner or bounded reaction | Pending |
| `ultReady` | PlayerSim.Kits.cs | FxEvents.cs, Haptics.cs, Sound.cs, Ui.cs | Owner or bounded reaction | Pending |
| `ultRun` | World.Ult.cs | FxEvents.cs, Sound.cs, UltFx.cs | Owner or bounded reaction | Pending |
| `vanish` | PlayerSim.cs | FxEvents.cs, Haptics.cs, Sound.cs | Owner or bounded reaction | Pending |
| `vbStart` | PlayerSim.cs | FxEvents.cs, Haptics.cs, Sound.cs | Owner or bounded reaction | Pending |
| `veilBreak` | PlayerSim.Kits.cs | FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `veilOn` | PlayerSim.Kits.cs | FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |
| `wallDown` | World.Step.cs | FxEvents.cs, RamFx.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `wallHit` | World.Step.cs | FxEvents.cs, RamFx.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `wallSlide` | PlayerSim.cs | FxEvents.cs, Haptics.cs, Sound.cs | Owner or bounded reaction | Pending |
| `wallUp` | World.Step.cs | FxEvents.cs, Haptics.cs, RamFx.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `walljump` | PlayerSim.cs | FxEvents.cs, Haptics.cs, Sound.cs, View.cs | Owner or bounded reaction | Pending |
| `wellLaunch` | World.cs | FxEvents.cs, Sound.cs, SubFx.cs | Owner or bounded reaction | Pending |
| `wellOpen` | World.cs | FxEvents.cs, Haptics.cs, Sound.cs, SubFx.cs, View.cs | Owner or bounded reaction | Pending |
| `wipe` | World.Step.cs | Sound.cs, Ui.cs | Owner or bounded reaction | Pending |
| `yank` | World.cs | FxEvents.cs, Sound.cs | Owner or bounded reaction | Pending |

## Sustained owners and projectile cores

BeamFx: beams/charging following actor depth. ChargeFx: previews, releases and classic ribbons. FxSync: projectile templates, sniper lasers, shockwaves, barriers, snares and scarf. SubFx: event chain snapshots, fixed-lane wells, enemy auras and skate sparks. RamFx: shield/stored energy. UltFx: timed participant effects. NovaShieldFx: absorbing shield. Particle/strip resets, model toggles, teleport and paused drawing must be reviewed in the player. Deployable/snare/well origins now retain their frozen lane; area fields and shockwave cores communicate their retained all-lanes policy. The headless suite checks shot/device lifetime and cross-lane queries; sustained motion in every lane remains a player review gate.

Unknown projectile kinds use the existing standard mesh and energy family; add an explicit inventory row before accepting a new family. Physical families are selected by SOLID/EXPLOSIVE/HEAVY sets in Fx.Cine, not by making every impact a fireball. Floor/wall marks now have native URP projection with normal-oriented quad fallback; bounded cloud ray marching and 3D beam/lightning paths have actual captures at `65198de`; later cleanup/origin changes need the next exact-commit review. Every unreviewed inventory row remains pending.

## Projectile families, contact policy and lifetime

| Physical family | Kinds / owner | Origin and reaction | Cleanup / scenario |
|---|---|---|---|
| Ballistic / hot metal | slug, rivet, hotRivet, pellet, shell | Immutable shot lane; stretched streak, hot-rivet smoke; outward surface sparks/chips/pale abrasion from actual wall contact | Aggregate admitted lifetimes; flight/death removes core and emission clock. Wall/floor/ceiling contact tested; rendered projectile contact is the current 44th capture gate |
| Explosive | grenade, bomblet, mortar, missile, sentryRocket | Immutable lane; moving fire/smoke exhaust; actual-radius blast envelope, smoke/debris/pressure ring. Mortar landing warning remains solid feedback | Existing fuse/bounce/cluster rules; no decoration determines damage. Trigger all explosive sizes/fuses in each lane; full live review pending |
| Heavy energy | lance, rail, breach, heavy, reflected | Round core plus bright native motes, bounded light/distortion, normal-aware terrain contact | Existing projectile/deflection owners and global caps; low/off keeps core. Full trigger review pending |
| Standard energy / role projectiles | std, bolt, tracer, dart, shard, sentryBolt, wave | Team/weapon colour, immutable lane, quality-selected trail; ray-contact material reaction | Same owner/clock cleanup; ordinary and deflected/enemy variants require live review |
| Returning / planted | disc, snare | Disc keeps shot lane; snare stores landing lane and has a solid all-lanes area boundary | Return/catch/end or trap expiry/reset; lane lifetime headless checked, full flight/plant review pending |
| Sustained 3D energy | Nova/RAM beam and charge, chain/lightning bodies | Actor-resolved beam depth; event-snapshot chain endpoints; eight-sided tubes/filaments, mesh light particles, electrical body sparks and surface lights | Separate 66-tube feedback cap plus aggregate admitted particles/lights; pause draws without advancing; release/end/death removes owners. Actual controlled production release/chain captures exist |

Exact kinds include literal projectile initializers and dynamic attachment/deflection kinds. Unsupported/new kinds use the explicit standard core/family fallback and must be added to this ledger before approval. Surface contact emits one modern reaction instead of both the legacy and modern wall reactions. The final capture must prove a production contact mark in addition to the direct projector prototype. Every remaining pending row above still needs its live trigger, without treating this family mapping as visual approval.
