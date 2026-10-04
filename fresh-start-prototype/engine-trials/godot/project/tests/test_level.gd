# The level data and the path (v12-test.mjs: the path checks and the placements), plus the collision
# helpers against values worked out from level.js.
extends RefCounted

func run(t: TestKit) -> void:
	var L := Tune.L
	var lv := Level.new()

	# The path: continuous through every piece, the old route's frames unchanged, and the new routes apart
	# from it and each other
	var worst := 0.0
	var x := 400.0
	while x < 1180:
		if not (x > 760 and x < 800):
			var a := PathFrame.frame(x)
			var b := PathFrame.frame(x + 0.25)
			var d := U.hypot(a.px - b.px, a.pz - b.pz)
			worst = maxf(worst, maxf(absf(d - 0.25), absf(U.hypot(a.tx, a.tz) - 1)))
		x += 0.25
	var o0 := PathFrame.frame(50)
	var o1 := PathFrame.frame(120)
	var o2 := PathFrame.frame(300)
	var same: bool = o0.px == 50 and o0.nz == 1 and absf(o1.px - 116.7375) < 1e-3 and o2.px == 104 - (300 - L.ARC_END) and o2.nz == -1
	var F := _pts(L.ROUTES[1].sx, L.ROUTES[1].endX)
	var Un := _pts(L.ROUTES[2].sx, L.ROUTES[2].endX)
	var O := _pts(-20, 318)
	var gap := minf(_md(F, Un), minf(_md(F, O), _md(Un, O)))
	t.check(worst < 1e-4 and same and gap > 60, "Path: continuous (error %.7f), the old route unchanged (%s), the three routes at least %.0f m apart" % [worst, str(same), gap])

	# Every enemy spawn (not fliers), power-up and checkpoint rests on a floor
	var bad := []
	for E in L.ENCOUNTERS:
		var waves: Array = E.get("waves", []).duplicate()
		waves.append(E.get("extra", []))
		for w in waves:
			for s in w:
				if s[0] != "drone" and absf(lv.ground_below(s[1], s[2] + 0.05) - s[2]) > 0.05:
					bad.append("%s %s" % [E.id, s[0]])
	for k in L.LEVEL_PICKUPS:
		if absf(lv.ground_below(k[0], k[1] + 0.05) - k[1]) > 0.05:
			bad.append("pickup %s %s" % [k[2], k[0]])
	for c in L.CHECKPOINTS:
		if absf(lv.ground_below(c.x, c.y + 0.05) - c.y) > 0.05:
			bad.append("checkpoint %s" % c.x)
	t.check(bad.is_empty(), "Everything placed on the new routes stands on a floor" + (": " + ", ".join(bad) if bad else ""))

	# Collision helpers (values from level.js)
	var body := { "x": 2.0, "y": 0.3, "w": 0.72, "h": 1.72, "vx": 0.0, "vy": -30.0, "drop_t": 0, "on_ground": false, "hit_wall": 0, "hit_ceil": false, "wall_dir": 0 }
	var B := _Body.new(body)
	lv.move_body(B, 1.0 / 60.0)
	var landed := B.y == 0.0 and B.on_ground
	B.x = 13.0; B.y = 0.0; B.vx = 60.0; B.vy = 0.0
	lv.move_body(B, 1.0 / 60.0)   # runs into nothing (the gap at 14 is open air)
	var free_run := absf(B.x - 14.0) < 1e-9
	B.x = 31.5; B.y = 0.0; B.vx = 60.0
	lv.move_body(B, 1.0 / 60.0)   # into the ledge at 32
	var walled: bool = B.hit_wall == 1 and absf(B.x - (32 - 0.36 - 1e-4)) < 1e-9
	t.check(landed and free_run and walled, "move_body lands on the gym floor, runs free, and stops at the ledge wall (x %.4f)" % B.x)
	var h := lv.ray_cast(40.5, 1.5, 1, 0, 30)
	t.check(h.wall and absf(h.x - 44) < 1e-9 and h.nx == -1 and not lv.segment_blocked(40.5, 0.5, 43, 0.5) and lv.segment_blocked(40, 3, 52, 3),
		"ray_cast hits the tunnel at x %.2f; line of sight under it is clear, through it blocked" % h.x)
	t.check(lv.ground_below(77, 5) == 2.4 and lv.ground_below(77, 2) == 0 and lv.has_headroom(77, 0, 0.72, 1.72) and not lv.has_headroom(45, 0, 0.72, 1.72),
		"ground_below finds the dais (2.4) and the floor (0); headroom under the dais, none in the tunnel")
	t.check(Level.kill_y_at(700) == 14 and Level.kill_y_at(850) == 6 and Level.kill_y_at(50) == -10 and Level.route_at(500).id == "foundry" and Level.zone_at(900).id == "undercity",
		"Fall-out line per stretch (14, 6, -10); routes and zones by x")

func _pts(x0: float, x1: float) -> Array:
	var out := []
	var x := x0
	while x < x1:
		var f := PathFrame.frame(x)
		out.append([f.px, f.pz])
		x += 2
	return out

func _md(A: Array, B: Array) -> float:
	var m := INF
	for a in A:
		for b in B:
			m = minf(m, U.hypot(a[0] - b[0], a[1] - b[1]))
	return m

class _Body:
	var x: float
	var y: float
	var w: float
	var h: float
	var vx: float
	var vy: float
	var drop_t := 0
	var on_ground := false
	var hit_wall := 0
	var hit_ceil := false
	var wall_dir := 0
	func _init(d: Dictionary) -> void:
		x = d.x; y = d.y; w = d.w; h = d.h; vx = d.vx; vy = d.vy
