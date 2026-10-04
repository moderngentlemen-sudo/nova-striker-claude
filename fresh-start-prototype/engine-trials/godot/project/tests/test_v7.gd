# v7-test.mjs: charge-scaled rocket jumps, wall play, the charged dash and lock-on. The wall checks the
# prototype runs with Echo (the grip, wall coyote and climbing a single wall) use Nova here: every character
# shares that code. Echo's own checks (swinging from a wall, the staff-rifle) come with his kit.
extends RefCounted

const WALL_X := 28.5   # the floating practice wall in the gym: x 28.5-29.3, y 2.1-8.6

var M: Dictionary
var C: Array

func setup(x := 99.0, char := "nova", y := 0.0) -> TestKit.Driver:
	return TestKit.setup(x, char, true, y)

func last(d: TestKit.Driver, type: String):
	for i in range(d.log.size() - 1, -1, -1):
		if d.log[i].type == type:
			return d.log[i]
	return null

func rocket(n: int, attach := "lance", extra := Callable()) -> Dictionary:
	var d := setup(99)
	d.p.attachment = attach
	var y0 := d.p.y
	var top := y0
	d.run({ "aim": [0, -1], "held": { "fire": true } }, n - 1)
	var preview = d.w.rocket_preview(d.p)
	d.run({ "aim": [0, -1], "held": { "fire": true } }, 1); d.run({ "aim": [0, -1] }, 1)
	var ys := []
	var state := {}
	for i in 120:
		d.run(extra.call(i, d.p, state) if extra.is_valid() else {}, 1)
		ys.append(d.p.y); top = maxf(top, d.p.y)
	return { "h": top - y0, "rj": d.log.filter(func(e): return e.type == "rocketJump"), "preview": preview, "ys": ys, "d": d }

func on_wall(y := 7.0, vy := -12.0) -> TestKit.Driver:
	var d := setup(20)
	var p := d.p
	p.x = WALL_X - p.w / 2 - 0.02; p.y = y; p.prev_x = p.x; p.prev_y = y; p.vy = vy; p.on_ground = false
	return d

func dash_dist(hold: int, o := {}) -> Dictionary:
	var d := setup(0)
	var x0 := d.p.x
	d.run({ "held": { "dash": true } }, hold); d.run(o, 60)
	return { "d": d.p.x - x0, "ev": last(d, "dash"), "dr": d }

func run(t: TestKit) -> void:
	Tune.settings.lockOn = true; Tune.settings.dashCharge = true; Tune.settings.dashIframes = false
	Tune.settings.lockMode = "manual"   # these check the lock-on button itself (test_v9 covers automatic lock-on)
	M = Tune.C.MARKSMAN; C = M.charge
	var R: Dictionary = M.rocket
	var WL: Dictionary = Tune.C.WALL

	# ---- Rocket jump
	# Height climbs steadily with how long the shot was charged
	var ns := []
	var hs := []
	for i in 6:
		ns.append(U.jround(C[0] + 1 + i * (C[2] - C[0] - 6) / 5))
		hs.append(rocket(ns[i]).h)
	var rising := true
	for i in range(1, 6):
		if not hs[i] > hs[i - 1] + 0.3:
			rising = false
	var l3 := rocket(int(C[2] + M.perfectWindow) + 4)
	var perf := rocket(int(C[2]) + 2)
	var d := setup(99); d.run({ "aim": [0, -1], "held": { "fire": true } }, 1); d.run({ "aim": [0, -1] }, 30)
	var parts := []
	for i in 6:
		parts.append("%dt %s m" % [ns[i], TestKit.f1(hs[i])])
	t.check(rising and hs[0] > 3.2 and l3.h > 7.5 and perf.h > 9.5 and d.count("rocketJump") == 0,
		"Rocket height rises with charge time: %s; level 3 %s m, Perfect %s m; a tap does not launch" % [", ".join(parts), TestKit.f2(l3.h), TestKit.f2(perf.h)])

	# Every attachment launches, the Arc highest, and the apex marker predicts the real peak
	var r := {}
	for a in ["lance", "volley", "arc", "prism"]:
		r[a] = rocket(60, a)
	var off := 0.0
	var all_one := true
	for a in r:
		off = maxf(off, absf(r[a].preview.apex - r[a].h))
		all_one = all_one and r[a].rj.size() == 1
	t.check(all_one and r.arc.h > r.lance.h and r.lance.h > r.prism.h and r.prism.h > r.volley.h and off < 0.12,
		"At 60 ticks: Arc %s, Lance %s, Prism %s, Volley %s m; apex marker off by at most %.3f m" % [TestKit.f2(r.arc.h), TestKit.f2(r.lance.h), TestKit.f2(r.prism.h), TestKit.f2(r.volley.h), off])

	# The launch pauses on impact for a few ticks, then leaves; the camera pulls ahead of the climb
	var q := rocket(int(C[2]) + 2, "arc")
	var ev: Dictionary = q.rj[0]
	var frozen := true
	for i in int(R.freeze[2]) - 1:
		frozen = frozen and q.ys[i] == 0
	var rose: bool = q.ys[int(R.freeze[2]) + 3] > 0.5
	d = setup(99); d.p.attachment = "arc"
	d.run({ "aim": [0, -1], "held": { "fire": true } }, int(C[2]) + 1)
	var pv = d.w.rocket_preview(d.p)
	var ftop: float = d.w.cam.y + d.w.cam.halfH
	t.check(ev.power > 0.99 and ev.perfect and frozen and rose and ev.h >= 9.5 and pv != null and ftop > pv.apex,
		"Perfect Arc rocket: power %s, %d-tick impact pause, then a %s m launch; lining it up frames the apex (%s m, frame top %s m)" % [TestKit.f2(ev.power), int(R.freeze[2]), TestKit.f1(ev.h), TestKit.f1(pv.apex) if pv else "?", TestKit.f1(ftop)])

	# A jump pressed during the climb never cuts it short, and the double jump is kept for the apex
	var plain := rocket(int(C[2]) + 12, "lance")
	var early := rocket(int(C[2]) + 12, "lance", func(i, _p, _s): return { "held": { "jump": true } } if i == 6 else {})
	var apex := rocket(int(C[2]) + 12, "lance", func(_i, p, s):
		if p.vy < 1 and p.vy > -1 and not s.get("dj"):
			s.dj = true
			return { "held": { "jump": true } }
		return {})
	t.check(absf(early.h - plain.h) < 0.05 and apex.h > plain.h + 2 and apex.d.count("djump") == 1,
		"Jump mid-climb: %s vs %s m (unchanged); double jump at the apex adds %s m" % [TestKit.f2(plain.h), TestKit.f2(early.h), TestKit.f2(apex.h - plain.h)])

	# Rocket, double jump, boosters and a Scatter hop from the arena's highest perch cannot clear a sealed gate
	d = setup(94)
	d.w.level.gates.L = true; d.w.level.gates.R = true; d.w.arena.state = "wave2"
	var p := d.p
	p.x = 94; p.y = 5.4; p.prev_x = 94; p.prev_y = 5.4; p.attachment = "arc"; d.run({}, 3)
	var top := p.y
	var wall_dirs := 0
	d.run({ "aim": [0, -1], "held": { "fire": true } }, int(C[2]) + 1); d.run({ "aim": [0, -1] }, 1)
	for i in 200:
		d.run({ "mx": 1 }); top = maxf(top, p.y)
		if p.wall_dir:
			wall_dirs += 1
	var u := setup(94)
	u.w.level.gates.L = true; u.w.level.gates.R = true; u.w.arena.state = "wave2"
	var qp := u.p
	qp.x = 94; qp.y = 5.4; qp.prev_x = 94; qp.prev_y = 5.4; qp.attachment = "arc"; u.run({}, 3)
	var top2 := qp.y
	var phase := 0
	u.run({ "aim": [0, -1], "held": { "fire": true } }, int(C[2]) + 1); u.run({ "aim": [0, -1] }, 1)
	for i in 300:
		var o := { "mx": 0.5 }
		if phase == 0 and qp.vy < 1 and qp.rocket_t < 40:
			o = { "held": { "jump": true } }; phase = 1
		elif phase == 1 and qp.vy < 0.5:
			o = { "held": { "jump": true } }; phase = 2
		elif phase == 2:
			o = { "held": { "jump": qp.fuel > 0 } }
			if qp.fuel <= 0:
				phase = 3
		elif phase == 3:
			o = { "aim": [0, -1], "held": { "melee": true } }; phase = 4
		u.run(o, 1); top2 = maxf(top2, qp.y)
	t.check(top < 30 and top2 < 30 and wall_dirs == 0 and qp.x < 96.2,
		"From the perch (5.4 m): rocket %s m, every extra %s m, gate top 30 m; the gate offers no wall slide" % [TestKit.f1(top), TestKit.f1(top2)])

	# ---- Wall play
	# The slide eases in: a fall is braked, it grips, then speeds up to the slide speed; down slides faster
	d = on_wall()
	d.run({ "mx": 1 }, 1)
	var vys := []
	for i in 40:
		d.run({ "mx": 1 }); vys.append(d.p.vy)
	var cw: Dictionary = Tune.C.CHARS.nova.wall
	var grip := int(WL.grip)
	var braked: bool = vys[0] > -13 and vys[0] < -WL.gripSpeed - 1
	var gripped: bool = absf(vys[grip] + WL.gripSpeed) < 0.05
	var full: bool = absf(vys[35] + cw.slide) < 0.05
	var mid: bool = vys[grip + 7] < -WL.gripSpeed - 0.3 and vys[grip + 7] > -cw.slide + 0.3
	var f := on_wall(); f.run({ "mx": 1 }, 30); f.run({ "mx": 1, "my": -1 }, 10)
	t.check(braked and gripped and mid and full and absf(f.p.vy + cw.slide * WL.fast) < 0.05,
		"Wall slide: fall braked (%s), grips at %s, eases to %s m/s; down slides at %s" % [TestKit.f1(vys[0]), TestKit.f2(vys[grip]), TestKit.f2(vys[35]), TestKit.f2(f.p.vy)])

	# Facing away from the wall; a shot with the stick held toward the wall goes out from it
	d = on_wall()
	d.run({ "mx": 1 }, 12)
	var facing := d.p.facing
	var sliding := d.p.wall_sliding
	d.run({ "mx": 1, "held": { "fire": true } }, 1); d.run({ "mx": 1 }, 1)
	var shots := d.w.projectiles.filter(func(pr): return pr.kind == "shot")
	t.check(sliding and facing == -1 and shots.size() > 0 and shots[0].vx < 0 and d.p.wall_sliding,
		"Sliding Nova faces out (facing %d) and his shot flies away from the wall (vx %s)" % [facing, TestKit.f0(shots[0].vx) if shots else "?"])

	# Letting go of the stick keeps the grip for a moment, then lets go
	d = on_wall()
	d.run({ "mx": 1 }, 20)
	var stuck := []
	for i in int(WL.stick) + 4:
		d.run({}); stuck.append(d.p.wall_sliding)
	var held_all := true
	for i in int(WL.stick) - 1:
		held_all = held_all and stuck[i]
	t.check(held_all and not stuck[stuck.size() - 1], "Grip holds %d ticks after letting go of the stick, then releases" % stuck.count(true))

	# Press away, then jump a few ticks later: still a wall jump (a leap)
	d = on_wall()
	d.run({ "mx": 1 }, 20); d.run({ "mx": -1 }, 3); d.run({ "mx": -1, "held": { "jump": true } }, 1)
	var wj = last(d, "walljump")
	t.check(wj != null and not wj.climb and absf(d.p.vx + cw.jumpVx) < 1.2, "Away, then jump 3 ticks later: wall leap (vx %s)" % TestKit.f1(d.p.vx))

	# Wall coyote: a jump just after leaving the wall still wall-jumps; later it is a double jump
	var late := func(k: int) -> Dictionary:
		var q2 := on_wall(); q2.run({ "mx": 1 }, 20)
		q2.p.x -= 0.4; q2.run({}, 1)
		q2.run({}, k); q2.run({ "held": { "jump": true } }, 1)
		return { "wj": q2.count("walljump"), "dj": q2.count("djump") }
	var la: Dictionary = late.call(3)
	var lb: Dictionary = late.call(int(WL.coyote) + 2)
	t.check(la.wj == 1 and la.dj == 0 and lb.wj == 0 and lb.dj == 1, "Jump 4 ticks off the wall: wall jump; %d ticks off: double jump" % (int(WL.coyote) + 3))

	# Climb kick vs leap, and climbing a single wall
	var kick := func(mx: float) -> float:
		var q3 := on_wall(5); q3.run({ "mx": 1 }, 15)
		var x0 := q3.p.x
		q3.run({ "mx": mx, "held": { "jump": true } }, 1); q3.run({ "mx": mx, "held": { "jump": true } }, 10)
		return x0 - q3.p.x
	var climb: float = kick.call(1.0)
	var leap: float = kick.call(-1.0)
	d = on_wall(2.3, -2)
	var cy0 := d.p.y
	var rel := false
	var ctop := cy0
	for i in 150:
		if d.p.wall_dir != 0 and d.p.vy < 8 and not rel:
			d.run({ "mx": 1 }, 1); rel = true
		else:
			d.run({ "mx": 1, "held": { "jump": true } }, 1); rel = false
		ctop = maxf(ctop, d.p.y)
	t.check(climb < 0.8 and leap > 1.6 and ctop > cy0 + 3 and d.count("walljump", func(e): return e.climb) >= 2,
		"Climb kick pushes out %s m, leap %s m; Nova climbs %s m up a single wall with climb kicks" % [TestKit.f2(climb), TestKit.f2(leap), TestKit.f1(ctop - cy0)])

	# Nova's Scatter from the wall fires out, not into it
	d = on_wall(5, -2)
	d.run({ "mx": 1 }, 12); d.run({ "mx": 1, "held": { "melee": true } }, 1); d.run({ "mx": 1 }, 6)
	var pel := d.w.projectiles.filter(func(pr): return pr.kind == "pellet")
	t.check(d.count("burst") == 1 and pel.size() > 0 and pel.all(func(pr): return pr.vx < 0) and d.p.wall_sliding,
		"Recoil Burst from the wall fires out (%d pellets) and he stays on the wall" % pel.size())

	# ---- Charged dash
	var DC: Dictionary = Tune.C.DASH_CHARGE
	var tap := dash_dist(2)
	var d1 := dash_dist(int(DC.charge[0]) + 1)
	var d2 := dash_dist(int(DC.charge[1]) + 1)
	var d3 := dash_dist(int(DC.charge[2]) + 1)
	var levels: int = d3.dr.count("dashLevel")
	var xd := setup(0); xd.run({ "mx": 1, "held": { "dash": true } }, 1)
	t.check(tap.ev.level == 0 and d1.ev.level == 1 and d2.ev.level == 2 and d3.ev.level == 3 and levels == 3
		and d1.d > tap.d + 0.8 and d2.d > d1.d + 0.8 and d3.d > d2.d + 0.8 and xd.p.state == "dash",
		"Dash distance: tap %s m, level 1 %s, level 2 %s, level 3 %s m; a dash with a direction starts on the press" % [TestKit.f1(tap.d), TestKit.f1(d1.d), TestKit.f1(d2.d), TestKit.f1(d3.d)])

	# Level 2 is invulnerable at the start; level 3 strikes through enemies and primes a tier 3 Velocity Break
	d = setup(0); d.run({ "held": { "dash": true } }, int(DC.charge[1]) + 1); d.run({}, 3)
	var inv2 := d.p.iframe
	var o0 := setup(0); o0.run({ "held": { "dash": true } }, 2); o0.run({}, 3)
	var inv0 := o0.p.iframe
	var s := setup(0)
	var e1 := s.dummy("swarmer", 3, 0)
	var e2 := s.dummy("swarmer", 5.5, 0)
	s.run({ "held": { "dash": true } }, int(DC.charge[2]) + 1); s.run({}, 3)
	var tier := s.p.vb_tier()
	s.run({}, 30)
	t.check(inv2 and not inv0 and tier == 3 and s.count("hit", func(h): return h.e == e1) == 1 and s.count("hit", func(h): return h.e == e2) == 1 and s.p.x > 6,
		"Level 2 dash invulnerable (%s), ordinary not (%s); level 3 hits both enemies in its path once each and primes VB %d" % [inv2, inv0, tier])

	# Jump cancels the charge; the stick aims the release; the setting turns it off
	var j := setup(0); j.run({ "held": { "dash": true } }, 20); j.run({ "held": { "dash": true, "jump": true } }, 1)
	var cancelled := j.p.state == "normal" and j.count("jump") == 1 and j.count("dash") == 0
	var a := setup(0); a.run({ "held": { "dash": true } }, 40); a.run({ "mx": -1, "my": 1 }, 1)
	var aimed = last(a, "dash")
	Tune.settings.dashCharge = false
	var offd := setup(0); offd.run({ "held": { "dash": true } }, 1)
	Tune.settings.dashCharge = true
	t.check(cancelled and aimed != null and aimed.dx < 0 and aimed.dy > 0 and offd.p.state == "dash",
		"Jump cancels the charge; release aimed up-left dashes (%s, %s); with the setting off a neutral dash is instant" % [TestKit.f2(aimed.dx) if aimed else "?", TestKit.f2(aimed.dy) if aimed else "?"])

	# ---- Lock-on
	# Press to lock the best target (in front beats behind), aim follows it, tap cycles, hold lets go
	d = setup(100)
	var front := d.dummy("swarmer", 105, 0)
	var back := d.dummy("swarmer", 96.5, 0)
	var high := d.dummy("drone", 104, 4)
	d.p.facing = 1
	d.run({ "held": { "lock": true } }, 1); d.run({}, 1)
	var first = d.p.lock_t
	var dx: float = first.x - d.p.x
	var dy: float = first.y + first.h * 0.55 - (d.p.y + d.p.h * 0.62)
	var m := U.hypot(dx, dy)
	var aimed_ok := (d.p.aim_x * dx + d.p.aim_y * dy) / m > 0.999
	d.run({ "held": { "lock": true } }, 2); d.run({}, 1)
	var second = d.p.lock_t
	d.run({ "held": { "lock": true } }, int(Tune.C.LOCK.hold) + 1); d.run({}, 1)
	t.check((first == front or first == high) and aimed_ok and second != null and second != first and d.p.lock_t == null
		and d.count("lockOn") == 1 and d.count("lockSwitch") == 1 and d.count("lockOff") == 1,
		"Lock picks a target in front (%s), aim follows it, a tap cycles (%s), holding lets go" % [first.type, second.type if second else "?"])

	# The lock jumps to the next target when this one dies, and lets go when none are left
	d = setup(100)
	d.dummy("swarmer", 104, 0); d.dummy("swarmer", 107, 0)
	d.run({ "held": { "lock": true } }, 1); d.run({}, 1)
	var t0 = d.p.lock_t
	t0.hp = 0.01
	d.w.spawn_hitbox({ "owner": d.p, "team": "p", "x0": t0.x - 1, "x1": t0.x + 1, "y0": 0.0, "y1": 2.0, "dmg": 5.0, "poise": 0.0, "kb": [0.0, 0.0], "instance": d.w.new_instance() })
	d.run({}, 12)
	var t1 = d.p.lock_t
	if t1:
		t1.hp = 0.01
		d.w.spawn_hitbox({ "owner": d.p, "team": "p", "x0": t1.x - 1, "x1": t1.x + 1, "y0": 0.0, "y1": 2.0, "dmg": 5.0, "poise": 0.0, "kb": [0.0, 0.0], "instance": d.w.new_instance() })
	d.run({}, 12)
	t.check(t1 != null and t1 != t0 and t1.dead and d.p.lock_t == null and d.count("lockSwitch", func(e): return e.why == "switch") == 1 and d.count("lockOff") == 1,
		"Target down: the lock moves to the next one, and lets go after the last")

	# Locked Volley: every dart seeks the locked target
	d = setup(100)
	d.dummy("swarmer", 104, 0)
	var locked := d.dummy("drone", 109, 3.5)
	d.p.attachment = "volley"
	d.run({ "held": { "lock": true } }, 1); d.run({}, 1)
	if d.p.lock_t != locked:
		d.run({ "held": { "lock": true } }, 1); d.run({}, 1)
	d.run({ "held": { "fire": true } }, int(C[1]) + 1); d.run({}, 1)
	var darts := d.w.projectiles.filter(func(pr): return pr.kind == "dart")
	t.check(d.p.lock_t == locked and darts.size() == M.volley.darts[2] and darts.all(func(pr): return pr.seek.target == locked), "All %d Volley darts seek the locked drone" % darts.size())

	# Melee magnetism: locked, he turns to a close target and steps in (the prototype checks Echo; this is
	# Nova's Pass 1 jab, which uses the same lunge)
	Tune.settings.novaKit = "sentinel"
	var lunge := func(lock: bool) -> Dictionary:
		var q4 := setup(100)
		var e := q4.dummy("swarmer", 102.6, 0)
		q4.p.facing = -1
		if lock:
			q4.run({ "held": { "lock": true } }, 1); q4.run({}, 1)
		q4.run({ "held": { "melee": true } }, 1); q4.run({}, 6)
		return { "facing": q4.p.facing, "hit": q4.count("hit", func(h): return h.e == e) }
	var la2: Dictionary = lunge.call(false)
	var lb2: Dictionary = lunge.call(true)
	Tune.settings.novaKit = "marksman"
	t.check(la2.facing == -1 and la2.hit == 0 and lb2.facing == 1 and lb2.hit == 1, "Without lock he swings the way he faces (%d hit); locked he turns and steps in (%d hit)" % [la2.hit, lb2.hit])

	# Out of range lets go
	d = setup(100)
	var far := d.dummy("swarmer", 110, 0)
	d.run({ "held": { "lock": true } }, 1); d.run({}, 1)
	far.x = 100 + Tune.C.LOCK.keep + 2; d.run({}, 2)
	t.check(d.p.lock_t == null and d.count("lockOff", func(e): return e.why == "range") == 1, "A target beyond %d m releases the lock" % int(Tune.C.LOCK.keep))
