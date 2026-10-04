# The level's path through 3D space: a chain of straight and curved pieces (level.js SEGS). The movement is
# 2D (x along the path, y up); frame(x) gives the world position on the path, its tangent and the normal
# toward the camera. The same maths as pathFrame in level.js.
extends RefCounted

var segs: Array = []

func _init(pieces: Array) -> void:
	segs = pieces

func _on(g: Dictionary, u: float) -> Array:
	var P: Dictionary = g.P
	if g.kind == "line":
		var tx := cos(float(g.phi))
		var tz := -sin(float(g.phi))
		return [Vector3(P.x + tx * u, 0, P.z + tz * u), Vector3(tx, 0, tz), Vector3(-tz, 0, tx)]
	var phi := float(g.phi) + float(g.s) * u / float(g.r)
	var n := Vector3(sin(phi), 0, cos(phi))
	var C: Dictionary = g.C
	return [Vector3(C.x + g.s * n.x * g.r, 0, C.z + g.s * n.z * g.r), Vector3(cos(phi), 0, -sin(phi)), n]

# [position, tangent, normal]
func frame(x: float) -> Array:
	var g: Dictionary = segs[segs.size() - 1]
	for q in segs:
		if x < q.x1:
			g = q
			break
	if x < segs[0].x0:
		g = segs[0]
	return _on(g, x - float(g.x0))

func point(x: float, y: float, depth: float = 0.0) -> Vector3:
	var f := frame(x)
	return f[0] + f[2] * depth + Vector3(0, y, 0)

# Heading of the path at x, as a rotation about y (local +x runs along the path)
func yaw(x: float) -> float:
	var t: Vector3 = frame(x)[1]
	return atan2(-t.z, t.x)

func curved(x0: float, x1: float) -> bool:
	for g in segs:
		if g.kind == "arc" and x1 > g.x0 and x0 < g.x1:
			return true
	return false
