# Level bosses. A port of bosses.js. The Lockwarden holds the Concourse Lock (its last wave); the Stormcaller
# guards the relay beacon at the end of the Skyline Relay. Both have two phases: at half health they roar
# (invulnerable for a moment), re-arm and speed up, and gain a new attack. Each opens real punish windows: a
# Lockwarden that charges into a wall or has its hammer perfect-parried is dazed; a Stormcaller that dives
# into the pad, or is perfect-parried out of the dive, crashes and lies open. They are never knocked back,
# hit-stop on them is kept short, poise decays between hits, and after a stagger they cannot be staggered
# again for a while (Combat).
class_name Bosses
extends RefCounted

static func _pick(e: EnemySim, world, opts: Array) -> String:
	var pool := opts.filter(func(o): return o[0] != e.last_atk)
	var sum := 0.0
	for o in pool:
		sum += o[1]
	var r: float = world.rng.next() * sum
	for o in pool:
		r -= o[1]
		if r <= 0:
			return o[0]
	return pool[0][0]

# Shared: poise decays, stagger immunity, and the half-health phase change (a roar that re-arms it)
static func _tick(e: EnemySim, world, B: Dictionary) -> bool:
	e.poise = 0.0 if e.stagger_cd > 0 else maxf(0, e.poise - B.poiseDecay)
	if e.stagger_cd > 0: e.stagger_cd -= 1
	if e.invuln > 0: e.invuln -= 1
	if e.phase == 1 and e.hp <= e.max_hp / 2 and e.state != "intro":
		e.phase = 2; e.armor = int(B.rearm); e.armor_max = e.armor; e.invuln = 80; e.atk = null
		e.set_state("roar")
		world.emit("bossPhase", { "e": e, "x": e.x, "y": e.y + e.h * 0.6 })
		return true
	return false

# Starts an attack: telegraphs it and remembers what it is
static func _begin(e: EnemySim, world, kind: String, cat: String, wind: int, extra := {}) -> void:
	e.atk = { "kind": kind, "wind": wind, "inst": world.new_instance() }
	e.atk.merge(extra)
	e.last_atk = kind
	e.set_state("windup"); world.telegraph(e, cat, wind)

# Where a laser runs: from the boss along `dir` at height band [y0, y1] above the floor, to the first wall
static func _laser_span(e: EnemySim, world, dir: int, fy: float, band: Array, range_: float) -> Dictionary:
	var y: float = fy + (band[0] + band[1]) / 2
	var x0: float = e.x + dir * (e.w / 2 + 0.1)
	var h: Dictionary = world.level.ray_cast(x0, y, dir, 0, range_)
	return { "x0": x0, "x1": x0 + dir * h.t, "y0": fy + band[0], "y1": fy + band[1], "y": y }

# ---- Lockwarden ----------------------------------------------------------------------------------
static func warden(e: EnemySim, world) -> void:
	var B: Dictionary = Tune.C.BOSS.warden
	var fast := 0.8 if e.phase == 2 else 1.0
	if _tick(e, world, B):
		e.physics(world)
		return
	var p = e.nearest_player(world, 60)
	e.target = p
	var s := e.state
	var A = e.atk
	if s == "intro":
		# Dropped into the lock from above: the landing throws shockwaves both ways
		e.physics(world)
		if e.on_ground and not e.landed:
			e.landed = true; world.spawn_shockwave(e, 1, 12); world.spawn_shockwave(e, -1, 12); world.emit("bossSlam", { "e": e, "x": e.x, "y": e.y, "big": true })
		if e.landed and e.st >= 110:
			e.invuln = 0; e.set_state("idle"); e.cd = 30
		return
	if s == "roar":
		e.vx *= 0.8; e.physics(world)
		if e.st >= 80:
			e.set_state("idle"); e.cd = 16
		return
	if s == "dazed":
		e.vx *= 0.85; e.physics(world)
		if e.st >= B.daze:
			e.set_state("idle"); e.cd = 20
		return
	if s == "idle" or s == "approach":
		if p == null:
			e.vx *= 0.8; e.physics(world)
			return
		var dx: float = p.x - e.x
		var dist := absf(dx)
		e.facing = 1 if dx >= 0 else -1
		if dist > 3.2:
			e.vx = U.approach(e.vx, e.facing * B.walk * (1.3 if e.phase == 2 else 1.0), 0.35)
		else:
			e.vx *= 0.7
		if e.cd == 0 and e.on_ground:
			var opts: Array
			if dist < 4.4:
				opts = [["sweep", 4], ["hammer", 3], ["stomp", 2]]
			elif dist < 11:
				opts = [["charge", 3], ["missiles", 3], ["stomp", 2]]
			else:
				opts = [["missiles", 4], ["charge", 3]]
			if e.phase == 2:
				opts.append(["laser", 3])
			start_warden(e, world, _pick(e, world, opts))
		e.physics(world)
		return
	if A == null:
		e.set_state("idle"); e.physics(world)
		return
	if s == "windup":
		e.vx *= 0.6
		if p != null and A.kind != "laser" and A.kind != "charge":
			e.facing = 1 if p.x >= e.x else -1
		if A.kind == "laser":
			A.span = _laser_span(e, world, e.facing, e.y, B.laser.high if A.high else B.laser.low, B.laser.range)
		if e.st >= A.wind:
			if A.kind == "stomp":
				e.vy = B.stomp.jump; e.vx = clampf((p.x - e.x) * 0.9, -B.stomp.hop, B.stomp.hop) if p != null else 0.0; e.on_ground = false; e.set_state("jump")
			elif A.kind == "missiles":
				_fire_missiles(e, world, B.missiles); e.set_state("recover"); A.rec = B.missiles.rec
			elif A.kind == "charge":
				e.set_state("charge"); world.emit("chargeStart", { "e": e })
			elif A.kind == "laser":
				e.set_state("laser"); world.emit("bossLaser", { "e": e, "ticks": B.laser.ticks, "high": A.high })
			else:
				e.set_state("attack")
		e.physics(world)
		return
	if s == "attack":
		var S: Dictionary = B.hammer if A.kind == "hammer" else B.sweep
		var x0: float = e.x + 0.3 if e.facing > 0 else e.x - 0.3 - S.reach
		var hammer: bool = A.kind == "hammer"
		world.spawn_hitbox({ "owner": e, "team": "e", "x0": x0, "x1": x0 + S.reach, "y0": e.y + (0.0 if hammer else 0.2), "y1": e.y + (3.0 if hammer else 2.6),
			"dmg": S.dmg, "kb": [e.facing * S.kb[0], S.kb[1]], "heavy": hammer, "instance": A.inst, "cat": "heavy" if hammer else "standard" })
		if hammer and e.st == 2:
			world.emit("bossSlam", { "e": e, "x": e.x + e.facing * 2.6, "y": e.y })
		if e.st >= S.active:
			e.set_state("recover"); A.rec = S.rec
		e.vx *= 0.5; e.physics(world)
		return
	if s == "jump":
		e.physics(world)
		if e.on_ground and e.st > 3:
			world.spawn_shockwave(e, 1, B.stomp.dmg, 1.3); world.spawn_shockwave(e, -1, B.stomp.dmg, 1.3)
			world.emit("bossSlam", { "e": e, "x": e.x, "y": e.y, "big": true })
			A.hops -= 1
			if A.hops > 0:
				e.vy = B.stomp.jump; e.vx = clampf((p.x - e.x) * 0.9, -B.stomp.hop, B.stomp.hop) if p != null else 0.0; e.on_ground = false; e.st = 0
			else:
				e.set_state("recover"); A.rec = B.stomp.rec; e.vx = 0
		return
	if s == "charge":
		e.vx = e.facing * B.charge.speed
		var x0 := e.x + 0.4 if e.facing > 0 else e.x - 1.9
		world.spawn_hitbox({ "owner": e, "team": "e", "x0": x0, "x1": x0 + 1.5, "y0": e.y + 0.1, "y1": e.y + 2.8, "dmg": B.charge.dmg, "heavy": true,
			"kb": [e.facing * 13.0, 6.0], "instance": A.inst, "cat": "heavy" })
		e.physics(world)
		if e.hit_wall != 0:
			e.set_state("dazed"); e.vx = -e.facing * 3; world.emit("chargeCrash", { "e": e }); world.emit("bossSlam", { "e": e, "x": e.x + e.facing * 1.1, "y": e.y })
		elif e.st >= B.charge.maxTicks:
			e.set_state("recover"); A.rec = B.charge.rec
		return
	if s == "laser":
		A.span = _laser_span(e, world, e.facing, e.y, B.laser.high if A.high else B.laser.low, B.laser.range)
		var L: Dictionary = A.span
		world.spawn_hitbox({ "owner": e, "team": "e", "x0": minf(L.x0, L.x1), "x1": maxf(L.x0, L.x1), "y0": L.y0, "y1": L.y1, "dmg": B.laser.dmg,
			"kb": [e.facing * 6.0, 5.0], "unblockable": true, "cat": "unblockable", "instance": A.inst })
		e.vx = 0; e.physics(world)
		if e.st >= B.laser.ticks:
			e.set_state("recover"); A.rec = B.laser.rec
		return
	if s == "recover":
		e.vx *= 0.8; e.physics(world)
		# A perfect parry of the hammer (or any heavy blow) leaves it dazed
		if e.parried == 2:
			e.parried = 0; e.set_state("dazed"); world.emit("bossDazed", { "e": e, "x": e.x, "y": e.y + e.h * 0.7 })
			return
		if e.st >= A.get("rec", 30) * fast:
			e.atk = null; e.parried = 0; e.set_state("idle"); e.cd = int(B.cd[e.phase - 1])
		return
	e.set_state("idle"); e.physics(world)

static func start_warden(e: EnemySim, world, k: String) -> void:
	var B: Dictionary = Tune.C.BOSS.warden
	var fast := 0.8 if e.phase == 2 else 1.0
	e.vx = 0
	if k == "sweep": _begin(e, world, k, "standard", U.jround(B.sweep.wind * fast))
	elif k == "hammer": _begin(e, world, k, "heavy", U.jround(B.hammer.wind * fast))
	elif k == "stomp": _begin(e, world, k, "unblockable", U.jround(B.stomp.wind * fast), { "hops": 2 if e.phase == 2 else 1 })
	elif k == "missiles": _begin(e, world, k, "standard", U.jround(B.missiles.wind * fast))
	elif k == "charge": _begin(e, world, k, "heavy", U.jround(B.charge.wind * fast))
	else:
		e.laser_high = not e.laser_high
		_begin(e, world, k, "unblockable", U.jround(B.laser.wind * fast), { "high": e.laser_high })

# Missiles lob up off its back and come down on the players (with some spread): standard shots, so they can
# be parried, deflected, shot down or erased by the beam
static func _fire_missiles(e: EnemySim, world, M: Dictionary) -> void:
	var n: int = int(M.n[e.phase - 1])
	var targets: Array = world.players.filter(func(q): return EnemySim.can_target(q))
	for i in n:
		var t = targets[i % targets.size()] if targets.size() else null
		var sx := e.x - e.facing * 0.5 + (i - (n - 1) / 2.0) * 0.25
		var sy := e.y + e.h + 0.1
		var tx: float = (t.x if t != null else e.x + e.facing * 6) + (i - (n - 1) / 2.0) * 1.3
		var base_y: float = t.y if t != null else e.y
		var ty: float = maxf(world.level.ground_below(tx, base_y + 1), base_y - 6) + 0.6
		var T := clampf(1.0 + absf(tx - sx) / 24 + i * 0.07, 1.0, 1.7)
		var g: float = M.gravity
		world.spawn_projectile({ "team": "e", "owner": e, "x": sx, "y": sy, "vx": (tx - sx) / T, "vy": (ty - sy + 0.5 * g * T * T) / T, "gravity": g, "r": 0.26, "dmg": M.dmg,
			"kind": "missile", "ttl": 240 })
	world.emit("bossMissiles", { "e": e, "n": n })

# ---- Stormcaller ---------------------------------------------------------------------------------
static func _home(e: EnemySim, tx: float, ty: float, k := 1.6, mx := -1.0) -> void:
	if mx < 0:
		mx = Tune.C.BOSS.stormcaller.speed
	e.vx = U.approach(e.vx, clampf((tx - e.x) * k, -mx, mx), 0.45); e.vy = U.approach(e.vy, clampf((ty - e.y) * k, -mx, mx), 0.45)

static func stormcaller(e: EnemySim, world) -> void:
	var B: Dictionary = Tune.C.BOSS.stormcaller
	var fast := 0.8 if e.phase == 2 else 1.0
	var floor_y: float = B.floor
	if _tick(e, world, B):
		e.vx *= 0.9; e.vy *= 0.9; e.physics(world)
		return
	var p = e.nearest_player(world, 60)
	e.target = p
	var s := e.state
	var A = e.atk
	var bob := sin(world.tick * 0.04) * 0.4
	if s == "intro":
		_home(e, e.home_x, floor_y + B.hover, 1.2, 5)
		if e.st >= 110:
			e.invuln = 0; e.set_state("idle"); e.cd = 30
		e.physics(world)
		return
	if s == "roar":
		e.vx *= 0.9; e.vy *= 0.9
		if e.st == 30:
			# Phase two: it calls in drones
			for i in int(B.drones):
				var d := EnemySim.create("drone", e.x + (4 if i else -4), e.y + 1.5, { "zone": e.zone, "enc": e.enc, "cd": 60 + i * 30, "add": true }, world)
				world.enemies.append(d)
			world.emit("bossCall", { "e": e })
		if e.st >= 80:
			e.set_state("idle"); e.cd = 16
		e.physics(world)
		return
	if s == "crashed":
		# Down on the pad: open to everything until it lifts off
		e.vx *= 0.85; e.vy = maxf(e.vy - Tune.C.GRAVITY * Tune.DT, -12)
		e.physics(world)
		if e.st >= e.crash_for:
			e.set_state("rise")
		return
	if s == "rise":
		_home(e, e.x, floor_y + B.hover, 1.4, 6); e.physics(world)
		if e.st >= B.dive.rise:
			e.set_state("idle"); e.cd = 16
		return
	if s == "idle":
		if p == null:
			_home(e, e.home_x, floor_y + B.hover + bob); e.physics(world)
			return
		e.facing = 1 if p.x >= e.x else -1
		# Hover above the pad off to one side of the target
		var side := 1 if e.x >= p.x else -1
		var tx := clampf(p.x + side * 4.5, B.padX[0], B.padX[1])
		_home(e, tx, floor_y + B.hover + bob)
		if e.cd == 0:
			start_storm(e, world, _pick(e, world, [["volley", 4], ["rain", 3], ["sweep", 3], ["dive", 3]]))
		e.physics(world)
		return
	if A == null:
		e.set_state("idle"); e.physics(world)
		return
	if s == "reposition":
		var tx: float = B.padX[A.edge]
		var ty: float = floor_y + B.sweep.hoverY
		_home(e, tx, ty, 2.2, 9)
		e.facing = 1 if A.edge == 0 else -1
		if (absf(e.x - tx) < 0.4 and absf(e.y - ty) < 0.4) or e.st > 90:
			e.set_state("windup"); world.telegraph(e, "unblockable", A.wind)
		e.physics(world)
		return
	if s == "windup":
		if A.kind == "sweep":
			_home(e, B.padX[A.edge], floor_y + B.sweep.hoverY, 2, 4); A.span = _laser_span(e, world, e.facing, floor_y, B.sweep.high if A.high else B.sweep.low, 30)
		else:
			e.vx *= 0.9; e.vy *= 0.9
			if p != null:
				e.facing = 1 if p.x >= e.x else -1
		if A.kind == "dive" and p != null:
			A.tx = p.x; A.ty = p.y + 0.6
		if e.st >= A.wind:
			if A.kind == "volley":
				e.set_state("volley"); A.n = 0
			elif A.kind == "rain":
				_fire_rain(e, world, B.rain); e.set_state("recover"); A.rec = B.rain.rec
			elif A.kind == "sweep":
				e.set_state("laser"); world.emit("bossLaser", { "e": e, "ticks": B.sweep.ticks, "high": A.high })
			else:
				var dx: float = A.tx - e.x
				var dy: float = A.ty - (e.y + e.h / 2)
				var m := U.hypot(dx, dy)
				if m == 0: m = 1
				A.dx = dx / m; A.dy = dy / m; e.set_state("dive"); world.emit("bossDive", { "e": e })
		e.physics(world)
		return
	if s == "volley":
		e.vx *= 0.9; e.vy *= 0.9
		if e.st % int(B.volley.every) == 1 and p != null and EnemySim.can_target(p):
			var sx := e.x + e.facing * 1.2
			var sy := e.y + 0.3
			var a := atan2(p.y + 1 - sy, p.x - sx)
			for d in [-1, 1]:
				var aa: float = a + d * B.volley.spread
				world.spawn_projectile({ "team": "e", "owner": e, "x": sx, "y": sy + d * 0.25, "vx": cos(aa) * B.volley.speed, "vy": sin(aa) * B.volley.speed, "r": 0.2, "dmg": B.volley.dmg, "kind": "std", "ttl": 150 })
			world.emit("enemyShot", { "e": e, "heavy": false })
			A.n += 1
			if A.n >= A.shots:
				e.set_state("recover"); A.rec = B.volley.rec
		e.physics(world)
		return
	if s == "laser":
		_home(e, B.padX[A.edge], floor_y + B.sweep.hoverY, 2, 3)
		A.span = _laser_span(e, world, e.facing, floor_y, B.sweep.high if A.high else B.sweep.low, 30)
		var L: Dictionary = A.span
		world.spawn_hitbox({ "owner": e, "team": "e", "x0": minf(L.x0, L.x1), "x1": maxf(L.x0, L.x1), "y0": L.y0, "y1": L.y1, "dmg": B.sweep.dmg,
			"kb": [e.facing * 6.0, 5.0], "unblockable": true, "cat": "unblockable", "instance": A.inst })
		e.physics(world)
		if e.st >= B.sweep.ticks:
			e.set_state("recover"); A.rec = B.sweep.rec; A.low = true
		return
	if s == "dive":
		e.vx = A.dx * B.dive.speed; e.vy = A.dy * B.dive.speed
		world.spawn_hitbox({ "owner": e, "team": "e", "x0": e.x - e.w / 2 - 0.2, "x1": e.x + e.w / 2 + 0.2, "y0": e.y - 0.2, "y1": e.y + e.h, "dmg": B.dive.dmg, "heavy": true,
			"kb": [U.sgn_or(A.dx, e.facing) * 12.0, 6.0], "instance": A.inst, "cat": "heavy" })
		e.physics(world)
		var hit_floor: bool = e.on_ground or e.hit_wall != 0 or e.y <= floor_y + 0.05   # the pad, a perch or a wall
		if e.parried == 2 or hit_floor or e.st >= B.dive.maxTicks:
			var parried := e.parried == 2
			e.parried = 0
			if parried or hit_floor:
				e.crash_for = int(B.dive.parried if parried else B.dive.crash); e.set_state("crashed"); world.emit("bossCrash", { "e": e, "x": e.x, "y": e.y, "parried": parried })
			else:
				e.set_state("rise")
		return
	if s == "recover":
		if A.get("low"):
			e.vx *= 0.85; e.vy *= 0.85
		else:
			_home(e, e.x, floor_y + B.hover + bob, 1, 3)
		e.physics(world)
		if e.parried == 2:
			e.parried = 0; e.crash_for = int(B.dive.parried); e.set_state("crashed"); world.emit("bossCrash", { "e": e, "x": e.x, "y": e.y, "parried": true })
			return
		if e.st >= A.get("rec", 30) * fast:
			var low: bool = A.get("low", false)
			e.atk = null; e.parried = 0; e.set_state("rise" if low else "idle"); e.cd = int(B.cd[e.phase - 1])
		return
	e.set_state("idle"); e.physics(world)

static func start_storm(e: EnemySim, world, k: String) -> void:
	var B: Dictionary = Tune.C.BOSS.stormcaller
	var fast := 0.8 if e.phase == 2 else 1.0
	if k == "volley": _begin(e, world, k, "standard", U.jround(B.volley.wind * fast), { "shots": B.volley.bursts[e.phase - 1] })
	elif k == "rain": _begin(e, world, k, "unblockable", U.jround(B.rain.wind * fast))
	elif k == "dive": _begin(e, world, k, "heavy", U.jround(B.dive.wind * fast))
	else:
		# The sweep: it drops low at one edge of the pad, then fires a laser across it at ankle height (jump it)
		# or, in phase two, alternately at chest height (crouch or slide under it)
		var edge := 0 if absf(e.x - B.padX[0]) < absf(e.x - B.padX[1]) else 1
		e.laser_high = (not e.laser_high) if e.phase == 2 else false
		e.atk = { "kind": "sweep", "inst": world.new_instance(), "edge": edge, "high": e.laser_high, "wind": U.jround(B.sweep.wind * fast) }
		e.last_atk = "sweep"
		e.set_state("reposition")

# Tests and tools: start a named attack now, the same way the boss would choose it
static func force_attack(world, e: EnemySim, kind: String) -> void:
	e.cd = 0
	if e.type == "warden":
		start_warden(e, world, kind)
	else:
		start_storm(e, world, kind)

# Shells rained across the pad (unblockable bursts, with landing markers): one on each player, the rest spread
static func _fire_rain(e: EnemySim, world, R: Dictionary) -> void:
	var n: int = int(R.n[e.phase - 1])
	var B: Dictionary = Tune.C.BOSS.stormcaller
	var targets: Array = world.players.filter(func(q): return EnemySim.can_target(q))
	for i in n:
		var t = targets[i] if i < targets.size() else null
		var tx: float = t.x if t != null else B.padX[0] + (B.padX[1] - B.padX[0]) * ((i + 0.5) / n) + (world.rng.next() - 0.5)
		var ty := maxf(world.level.ground_below(tx, B.floor + 2), B.floor - 6)
		var sx := e.x + (i - n / 2.0) * 0.3
		var sy := e.y + e.h * 0.2
		var T := 0.95 + i * 0.12
		var g: float = R.gravity
		world.spawn_projectile({ "team": "e", "owner": e, "x": sx, "y": sy, "vx": (tx - sx) / T, "vy": (ty + 0.2 - sy + 0.5 * g * T * T) / T, "gravity": g, "r": 0.3, "dmg": 0.0, "heavy": true,
			"kind": "mortar", "ttl": 300, "blast": R.blast.duplicate() })
		world.emit("mortarShot", { "e": e, "x": tx, "y": ty, "r": R.blast.r, "ticks": U.jround(T * 60) })

# A boss for an encounter: scaled for the number of players, arriving on its intro
static func spawn(world, type: String, x: float, y: float, extra := {}) -> EnemySim:
	var B: Dictionary = Tune.C.BOSS[type]
	var n := maxi(1, world.players.size())
	var opts := extra.duplicate()
	opts.merge({ "boss": true, "phase": 1, "invuln": 999, "staggerCd": 0, "parried": 0, "cd": 60, "facing": -1, "homeX": x, "homeY": y }, true)
	var e := EnemySim.create(type, x, y, opts, world)
	e.hp = U.jround(B.hp * (1 + 0.6 * (n - 1))); e.max_hp = e.hp
	e.state = "intro"; e.st = 0
	world.enemies.append(e)
	world.emit("bossIntro", { "e": e, "name": B.name, "title": B.title })
	return e
