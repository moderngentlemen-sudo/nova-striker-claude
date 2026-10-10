# Effect origin and ownership

Simulation events contain only primitives and simulation actors. `World.Emit` snapshots `Ev.depth` in metres toward the camera; projectile events resolve the projectile's immutable emission lane, ordinary actor events resolve the actor's current eased depth, and muzzle events resolve the shot's discrete collision lane. Positionless actor events snapshot route x and feet y. Impact events already supply their hit location. `ExplodeArgs.depth` carries a projectile's origin through detonation; wells keep their emission lane. Presentation never uses the simulation random generator.

`Fx.OnEvent` establishes an origin for the synchronous reaction and restores it in `finally`. `Fx.W(x,y,localDepth)` adds the local artistic offset once. Sprite/legacy particle positions, cinematic particles, lights and floor marks inherit that origin. Persistent beams/charge previews follow their actor; projectiles, chain bolt paths and well meshes keep their stored origin. Refer to VFX_COVERAGE for remaining individual motion checks.

Coordinates:

- Route: x follows `Level.Frame(x)`; y is world up; lane -1/0/+1 is back/middle/front, separated by 1.4m. Direction/normal primitives (`ax/ay`, `dx/dy`, `nx/ny`) are local route axes unless the effect already receives a world-space vector.
- `S.W` produces right-handed three-space. At the straight gym marker (52,2,+1.4), the result is (52,2,+1.4). `S.Dir(x,dx,dy)` resolves the route tangent and vertical velocity; depth velocity uses the frame normal.
- TObj positions and GeoBuilder vertices use three-space. They convert internally. Raw Unity position, light, collider, camera and instancing APIs receive `Th.P` exactly once. The same marker in Unity is (52,2,-1.4).
- FxLayer converts both position and velocity internally. Its caller passes three-space. Never call Th.P before FxLayer.Emit. Rotation conversion is `Th.Quat`; reflected meshes reverse winding in GeoBuilder/export loaders. Nonuniform scales stay explicit; cloud batches do not assert uniform scaling.
- An impact normal, when emitted, selects the contact orientation. Without a normal the current fallback is a radial impact and a floor-only mark. Wall projection remains incomplete; the menu identifies pooled surface quads.

Static batches own baked geometry for the view's lifetime. StageDressing owns shared kit meshes for that same lifetime and submits 48m cells in at most 500 instances. LevelFx owns feature geometry, caches identical boxes, and destroys transient meshes/materials on rebuilding. FxColliders owns its invisible colliders; collapsing boxes disable these when they lose support. Vfx's root is parented to the scene. ParticleBudget accounts for all FxPool native systems, weather systems and legacy particle/smoke/spark pools. Hazard machinery, projectile meshes, strips and UI cues are separate from decorative particles.
