# The curved gameplay path: maps the simulation's x (distance along the path) to a place in the 3D world.
# The simulation never knows: it stays 2D. A port of pathFrame in level.js.
#
# The Skyport route runs straight along +x, bends 180 degrees round the Storm Spire (an arc of radius ARC_R
# from ARC_START), then runs back. The Version 12 routes (x >= PATH2_X0) are each a chain of pieces somewhere
# else in the world: straight lines and arcs (s = +1 wrapping round a centre away from the camera, s = -1
# bending toward it).
#
# frame(x) returns { px, pz: position on the ground plane, tx, tz: tangent, nx, nz: normal toward the camera },
# in double precision like the prototype (Vector3 is single precision); point() and yaw() are for drawing.
class_name PathFrame
extends RefCounted

static func _on(g: Dictionary, u: float) -> Dictionary:
	if g.kind == "line":
		var tx := cos(float(g.phi))
		var tz := -sin(float(g.phi))
		return { "px": g.P.x + tx * u, "pz": g.P.z + tz * u, "tx": tx, "tz": tz, "nx": -tz, "nz": tx }
	var phi: float = g.phi + g.s * u / g.r
	var nx := sin(phi)
	var nz := cos(phi)
	return { "px": g.C.x + g.s * nx * g.r, "pz": g.C.z + g.s * nz * g.r, "tx": cos(phi), "tz": -sin(phi), "nx": nx, "nz": nz }

static func frame(x: float) -> Dictionary:
	Tune.ensure()
	var L := Tune.L
	if x >= L.PATH2_X0:
		var segs: Array = L.SEGS_OF[Level.route_at(x).id]
		var g: Dictionary = segs[segs.size() - 1]
		for q in segs:
			if x < q.x1:
				g = q
				break
		if x < segs[0].x0:
			g = segs[0]
		return _on(g, x - float(g.x0))   # (past either end it runs straight on along the end piece)
	var a0: float = L.ARC_START
	var r: float = L.ARC_R
	if x <= a0:
		return { "px": x, "pz": 0.0, "tx": 1.0, "tz": 0.0, "nx": 0.0, "nz": 1.0 }
	if x < L.ARC_END:
		var th := (x - a0) / r
		var s := sin(th)
		var c := cos(th)
		return { "px": a0 + r * s, "pz": -r + r * c, "tx": c, "tz": -s, "nx": s, "nz": c }
	var d: float = x - L.ARC_END
	return { "px": a0 - d, "pz": -2 * r, "tx": -1.0, "tz": 0.0, "nx": 0.0, "nz": -1.0 }

# A point in the world: x along the path, y up, `depth` toward the camera
static func point(x: float, y: float, depth := 0.0) -> Vector3:
	var f := frame(x)
	return Vector3(f.px + f.nx * depth, y, f.pz + f.nz * depth)

# Heading of the path at x, as a rotation about y (local +x runs along the path)
static func yaw(x: float) -> float:
	var f := frame(x)
	return atan2(-f.tz, f.tx)

# Does the stretch x0..x1 curve (geometry there is drawn in short pieces that follow the bend)?
static func curved_span(x0: float, x1: float) -> bool:
	var L := Tune.L
	if x1 > L.ARC_START and x0 < L.ARC_END and x0 < L.PATH2_X0:
		return true
	for id in L.SEGS_OF:
		for g in L.SEGS_OF[id]:
			if g.kind == "arc" and x1 > g.x0 and x0 < g.x1:
				return true
	return false
