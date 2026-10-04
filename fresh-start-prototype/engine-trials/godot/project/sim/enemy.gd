# Enemy archetypes and behaviour. A port of enemies.js. Every attack announces its category through the
# world's telegraph events: 'standard' (parryable), 'heavy' (perfect parry to fully negate), 'unblockable'.
# The bosses' behaviours are in Bosses.
class_name EnemySim
extends RefCounted

static var next_id := 1

var kind := "enemy"
var id := 0
var type := ""
var x := 0.0
var y := 0.0
var vx := 0.0
var vy := 0.0
var w := 1.0
var h := 1.0
var prev_x := 0.0
var prev_y := 0.0
var facing := -1
var shield_dir := -1
var on_ground := false
var hit_wall := 0
var hit_ceil := false
var wall_dir := 0
var drop_t := 0
var hp := 1.0
var max_hp := 1.0
var poise := 0.0
var poise_max := 1.0
var armor := 0
var armor_max := 0
var state := "idle"
var st := 0
var atk = null
var token = null
var target = null
var cd := 0
var hitstop := 0
var flash := 0
var dead := false
var death_t := 0
var tagged := 0
var stun := 0
var slam_cd := 120
var cycle := 0
var aim_x := 0.0
var aim_y := 0.0
var label := ""
var light := false
var flier := false
var boss := false
var home_x := 0.0
var home_y := 0.0
var zone := ""
var enc := ""
var add := false
var taunt_t := 0
var taunter = null
var shock_t := 0
var well_t := 0
var slow_t := 0
var dizzy := false
# Bosses
var invuln := 0
var stagger_cd := 0
var parried := 0
var phase := 1
var landed := false
var last_atk = null
var laser_high := false
var crash_for := 0
# Held by Echo's lash or RAM's charge (M2)
var catcher = null
var catch_side := 0
var plow_by = null

# extra: the prototype's option names (cd, zone, enc, facing, add, slamCd, hitstop ...)
# world: draws the starting cooldown from world.rng, as the prototype always draws one (even when extra sets it)
static func create(type_: String, x_: float, y_: float, extra := {}, world = null) -> EnemySim:
	var T: Dictionary = Tune.C.ENEMY_TYPES[type_]
	var e := EnemySim.new()
	e.id = next_id; next_id += 1
	e.type = type_; e.x = x_; e.y = y_; e.prev_x = x_; e.prev_y = y_
	e.w = T.w; e.h = T.h; e.hp = T.hp; e.max_hp = T.hp; e.poise_max = T.poise
	e.armor = int(T.get("armor", 0)); e.armor_max = e.armor
	e.cd = 30 + (world.rng.below(40) if world != null else 0)
	e.light = T.light; e.flier = bool(T.get("flier", false)); e.boss = bool(T.get("boss", false))
	e.home_x = x_; e.home_y = y_
	for k in extra:
		e.set_opt(k, extra[k])
	return e

const OPT := { "cd": "cd", "zone": "zone", "enc": "enc", "facing": "facing", "add": "add", "slamCd": "slam_cd", "hitstop": "hitstop",
	"boss": "boss", "phase": "phase", "invuln": "invuln", "staggerCd": "stagger_cd", "parried": "parried", "homeX": "home_x", "homeY": "home_y",
	"shieldDir": "shield_dir", "hp": "hp" }

func set_opt(k: String, v) -> void:
	set(OPT.get(k, k), v)

# Enemies cannot see a player hidden by Veil (Echo, M2)
static func can_target(p) -> bool:
	return p.state != "downed" and p.state != "dead" and not p.veiled

func nearest_player(world, max_d := 40.0):
	if taunt_t > 0 and taunter != null and can_target(taunter):
		return taunter
	var best = null
	var bd := max_d
	for p in world.players:
		if not can_target(p):
			continue
		var d := U.hypot(p.x - x, (p.y + 0.9) - (y + h / 2))
		if d < bd:
			bd = d; best = p
	# (a flaring Echo draws enemies in range: M2)
	return best

func set_state(s: String) -> void:
	state = s; st = 0

func release_token(world) -> void:
	if token != null:
		world.director.release(self)

func physics(world) -> void:
	var T: Dictionary = Tune.C.ENEMY_TYPES[type]
	if T.get("flier"):
		vx *= 0.93; vy *= 0.93; world.level.move_body(self, Tune.DT)   # drones drift to a stop
		return
	if T.get("noGravity"):
		return
	vy -= Tune.C.GRAVITY * Tune.DT
	if vy < -Tune.C.MAX_FALL:
		vy = -Tune.C.MAX_FALL
	world.level.move_body(self, Tune.DT)

func update(world) -> void:
	prev_x = x; prev_y = y
	if flash > 0: flash -= 1
	if drop_t > 0: drop_t -= 1   # lets grappled enemies fall through one-way platforms
	if tagged > 0: tagged -= 1
	if taunt_t > 0: taunt_t -= 1
	if dead:
		death_t += 1
		if boss and not on_ground:   # a downed gunship falls onto the pad
			vy = maxf(vy - Tune.C.GRAVITY * Tune.DT, -14); vx *= 0.95; world.level.move_body(self, Tune.DT)
		return
	if y < Level.kill_y_at(x):   # fell out of the level (a charge off a ledge, a knockback over the edge)
		dead = true; death_t = 0; hp = 0; world.director.release(self)
		world.emit("kill", { "x": x, "y": y, "e": self, "owner": null })
		return
	if shock_t > 0: shock_t -= 1
	if well_t > 0: well_t -= 1
	# Slowed by Nova's perfect dodge: it only acts every other tick
	if slow_t > 0:
		slow_t -= 1
		if slow_t % 2:
			return
	if hitstop > 0:
		hitstop -= 1
		return
	st += 1
	if cd > 0: cd -= 1
	if slam_cd > 0: slam_cd -= 1

	# Shared interrupt states
	if state == "stagger" or state == "hitstun":
		if on_ground:
			vx *= 0.85
		physics(world)
		if st >= stun:
			set_state("idle"); dizzy = false
		return
	dizzy = false
	if state == "launched":
		physics(world)
		if (on_ground or flier) and st > 6:
			stun = 20; set_state("hitstun")
		return
	# (Caught by Echo's lash, snared, plowed by RAM: M2)
	if state == "snared":
		vx = 0; physics(world)
		if st >= stun:
			set_state("idle")
		return
	match type:
		"swarmer": swarmer(world)
		"shield": shield(world)
		"sniper": sniper(world)
		"brute": brute(world)
		"post": post(world)
		"drone": drone(world)
		"mortar": mortar(world)
		"charger": charger(world)
		"turret": turret(world)
		"warden": Bosses.warden(self, world)
		"stormcaller": Bosses.stormcaller(self, world)

# ---- Behaviours ------------------------------------------------------------------------

func walk_toward(tgt, speed: float, stop_dist: float) -> void:
	var dx: float = tgt.x - x
	facing = 1 if dx >= 0 else -1
	if absf(dx) > stop_dist:
		vx = facing * speed
	else:
		vx *= 0.7

func _rand_cd(world, base: int, spread: int) -> int:
	return base + world.rng.below(spread)

func swarmer(world) -> void:
	var p = nearest_player(world, 18)
	target = p
	if state == "idle" or state == "approach":
		if p == null:
			vx *= 0.8; physics(world)
			return
		walk_toward(p, Tune.C.ENEMY_TYPES.swarmer.speed, 1.7)
		var close: bool = absf(p.x - x) < 2.1 and absf(p.y - y) < 1.4
		if close and cd == 0 and world.director.request(self, "melee"):
			set_state("windup"); vx = 0; world.telegraph(self, "standard", 20)
		else:
			state = "approach"
	elif state == "windup":
		vx *= 0.7
		if p != null:
			facing = 1 if p.x >= x else -1
		if st >= 20:
			set_state("attack"); atk = { "inst": world.new_instance() }
	elif state == "attack":
		vx = facing * 10
		var x0 := x if facing > 0 else x - 1.1
		world.spawn_hitbox({ "owner": self, "team": "e", "x0": x0, "x1": x0 + 1.1, "y0": y, "y1": y + 0.9, "dmg": 8.0, "kb": [facing * 5.0, 3.0], "instance": atk.inst, "cat": "standard" })
		if st >= 10:
			set_state("recover")
	elif state == "recover":
		vx *= 0.8
		if st >= 26:
			release_token(world); cd = _rand_cd(world, 40, 40); set_state("idle")
	physics(world)

func shield(world) -> void:
	var p = nearest_player(world, 22)
	target = p
	# The shield turns slowly, so flanking and attacking from above work
	if p != null and st % 36 == 0 and state != "attack":
		shield_dir = 1 if p.x >= x else -1
	if state == "idle" or state == "approach":
		if p == null:
			vx *= 0.8; physics(world)
			return
		var dx: float = p.x - x
		facing = shield_dir
		if absf(dx) > 2.2 and U.sgn(dx) == shield_dir:
			vx = shield_dir * Tune.C.ENEMY_TYPES.shield.speed
		else:
			vx *= 0.7
		var close: bool = absf(dx) < 2.5 and absf(p.y - y) < 1.6 and U.sgn(dx) == shield_dir
		if close and cd == 0 and world.director.request(self, "melee"):
			set_state("windup"); world.telegraph(self, "standard", 22)
	elif state == "windup":
		vx *= 0.6
		if st >= 22:
			set_state("attack"); atk = { "inst": world.new_instance() }
	elif state == "attack":
		vx = shield_dir * 8
		var x0 := x if shield_dir > 0 else x - 1.3
		world.spawn_hitbox({ "owner": self, "team": "e", "x0": x0, "x1": x0 + 1.3, "y0": y + 0.2, "y1": y + 1.8, "dmg": 12.0, "kb": [shield_dir * 7.0, 3.0], "instance": atk.inst, "cat": "standard" })
		if st >= 8:
			set_state("recover")
	elif state == "recover":
		vx *= 0.75
		if st >= 34:
			release_token(world); cd = _rand_cd(world, 60, 40); set_state("idle")
	physics(world)

func sniper(world) -> void:
	var p = nearest_player(world, 24)
	if state == "idle":
		target = p
		if p != null and cd == 0:
			if not world.level.segment_blocked(x, y + 1.4, p.x, p.y + 1) and world.director.request(self, "ranged"):
				set_state("aim"); world.telegraph(self, "heavy", 64)
	elif state == "aim":
		var t = target
		if t == null or not can_target(t):
			release_token(world); target = null; set_state("idle"); physics(world)
			return
		aim_x = t.x; aim_y = t.y + 1.0
		facing = 1 if t.x >= x else -1
		if st >= 48:
			set_state("lock"); world.emit("lock", { "e": self })
	elif state == "lock":
		if st >= 16:
			var sx := x + facing * 0.5
			var sy := y + 1.4
			var dx := aim_x - sx
			var dy := aim_y - sy
			var d := U.hypot(dx, dy)
			if d == 0: d = 1
			world.spawn_projectile({ "team": "e", "owner": self, "x": sx, "y": sy, "vx": dx / d * 24, "vy": dy / d * 24, "r": 0.28, "dmg": 18.0, "heavy": true, "kind": "heavy", "ttl": 90 })
			world.emit("enemyShot", { "e": self, "heavy": true })
			set_state("recover")
	elif state == "recover":
		if st >= 30:
			release_token(world); cd = 140; set_state("idle")
	physics(world)

func brute(world) -> void:
	var p = nearest_player(world, 30)
	target = p
	if state == "idle" or state == "approach":
		if p == null:
			vx *= 0.8; physics(world)
			return
		var dist: float = absf(p.x - x)
		walk_toward(p, Tune.C.ENEMY_TYPES.brute.speed, 2.0)
		if cd == 0:
			if dist > 3.2 and dist < 8.5 and slam_cd == 0 and world.director.request(self, "melee"):
				set_state("slamWindup"); vx = 0; world.telegraph(self, "unblockable", 44)
			elif dist < 2.8 and absf(p.y - y) < 2 and world.director.request(self, "melee"):
				set_state("windup"); vx = 0; world.telegraph(self, "heavy", 26)
	elif state == "windup":
		vx *= 0.6
		if st >= 26:
			set_state("attack"); atk = { "inst": world.new_instance() }
	elif state == "attack":
		var x0 := x + 0.2 if facing > 0 else x - 2.8
		world.spawn_hitbox({ "owner": self, "team": "e", "x0": x0, "x1": x0 + 2.6, "y0": y + 0.3, "y1": y + 2.2, "dmg": 20.0, "kb": [facing * 10.0, 5.0], "heavy": true, "instance": atk.inst, "cat": "heavy" })
		if st >= 6:
			set_state("recover")
	elif state == "slamWindup":
		if st >= 44:
			world.spawn_shockwave(self, 1, 22); world.spawn_shockwave(self, -1, 22)
			world.emit("slam", { "e": self }); set_state("slamRecover")
	elif state == "recover" or state == "slamRecover":
		vx *= 0.8
		var len := 38 if state == "recover" else 50
		if st >= len:
			release_token(world)
			if state == "slamRecover":
				slam_cd = 300
			cd = _rand_cd(world, 30, 30); set_state("idle")
	physics(world)

func post(world) -> void:
	var p = nearest_player(world, 3.6)
	var pattern := ["standard", "heavy", "unblockable"]
	if state == "idle":
		label = ""
		if p != null and cd == 0:
			facing = 1 if p.x >= x else -1
			var cat: String = pattern[cycle % 3]
			cycle += 1
			var wind := 24 if cat == "standard" else (30 if cat == "heavy" else 44)
			atk = { "cat": cat, "wind": wind, "inst": world.new_instance() }
			label = "Standard: parry it" if cat == "standard" else ("Heavy: perfect-parry it" if cat == "heavy" else "Unblockable: jump it")
			set_state("windup"); world.telegraph(self, cat, wind)
	elif state == "windup":
		if st >= atk.wind:
			if atk.cat == "unblockable":
				world.spawn_shockwave(self, 1, 20, 0.6); world.spawn_shockwave(self, -1, 20, 0.6); world.emit("slam", { "e": self })
			set_state("attack")
	elif state == "attack":
		if atk.cat != "unblockable":
			var x0 := x if facing > 0 else x - 2.0
			world.spawn_hitbox({ "owner": self, "team": "e", "x0": x0, "x1": x0 + 2.0, "y0": y + 0.4, "y1": y + 1.9, "dmg": 16.0 if atk.cat == "heavy" else 8.0,
				"kb": [facing * 6.0, 3.0], "heavy": atk.cat == "heavy", "instance": atk.inst, "cat": atk.cat })
		if st >= 5:
			set_state("recover")
	elif state == "recover":
		if st >= 48:
			cd = 20; set_state("idle")
	physics(world)

# Drone: hovers above and to one side of its target, bobbing, and fires parryable shots down at it
func drone(world) -> void:
	var p = nearest_player(world, 26)
	target = p
	var sp: float = Tune.C.ENEMY_TYPES.drone.speed
	var bob := sin((world.tick + id * 37) * 0.05) * 0.5
	var tx := home_x
	var ty := home_y + bob
	if p != null:
		var side := 1 if x >= p.x else -1
		tx = p.x + side * 5.5; ty = p.y + 3.4 + bob; facing = 1 if p.x >= x else -1
	if state == "idle":
		vx = U.approach(vx, clampf((tx - x) * 1.6, -sp, sp), 0.35)
		vy = U.approach(vy, clampf((ty - y) * 1.8, -sp, sp), 0.35)
		if p != null and cd == 0 and absf(p.x - x) < 11 and not world.level.segment_blocked(x, y + 0.3, p.x, p.y + 1) and world.director.request(self, "ranged"):
			set_state("windup"); world.telegraph(self, "standard", 26)
	elif state == "windup":
		vx *= 0.85; vy *= 0.85
		if st >= 26:
			if p != null and can_target(p):
				var sx := x + facing * 0.4
				var sy := y + 0.25
				var dx: float = p.x - sx
				var dy: float = p.y + 1 - sy
				var d := U.hypot(dx, dy)
				if d == 0: d = 1
				world.spawn_projectile({ "team": "e", "owner": self, "x": sx, "y": sy, "vx": dx / d * 13, "vy": dy / d * 13, "r": 0.2, "dmg": 6.0, "kind": "std", "ttl": 110 })
				world.emit("enemyShot", { "e": self, "heavy": false })
			set_state("recover")
	elif state == "recover":
		vx *= 0.9; vy *= 0.9
		if st >= 24:
			release_token(world); cd = 70 + (id * 13) % 50; set_state("idle")
	else:
		set_state("idle")
	physics(world)

# Mortar: stays put and lobs a heavy shell that bursts where a marker shows on the ground. The burst can't be
# parried; move out, or shoot the shell down with a charged shot.
func mortar(world) -> void:
	var MO: Dictionary = Tune.C.MORTAR
	var p = nearest_player(world, MO.range)
	if state == "idle":
		target = p
		if p != null and cd == 0 and absf(p.x - x) > MO.minRange and world.director.request(self, "ranged"):
			facing = 1 if p.x >= x else -1
			set_state("windup"); world.telegraph(self, "unblockable", MO.wind)
	elif state == "windup":
		var t = target
		if t != null:
			facing = 1 if t.x >= x else -1
		if st >= MO.wind:
			if t != null and can_target(t):
				# Aim where the target stands now; flight time grows with distance
				var sx := x + facing * 0.5
				var sy := y + 1.35
				var tx: float = t.x
				var ty := maxf(world.level.ground_below(t.x, t.y + 0.3), t.y - 6)
				var T := clampf(0.9 + absf(tx - sx) / 25, 0.9, 1.5)
				var g: float = MO.gravity
				var pvx := (tx - sx) / T
				var pvy := (ty + 0.2 - sy + 0.5 * g * T * T) / T
				world.spawn_projectile({ "team": "e", "owner": self, "x": sx, "y": sy, "vx": pvx, "vy": pvy, "gravity": g, "r": 0.3, "dmg": 0.0, "heavy": true, "kind": "mortar",
					"ttl": 300, "blast": MO.blast.duplicate() })
				world.emit("mortarShot", { "e": self, "x": tx, "y": ty, "r": MO.blast.r, "ticks": U.jround(T * 60) })
			set_state("recover")
	elif state == "recover":
		if st >= 40:
			release_token(world); cd = int(MO.cd); set_state("idle")
	else:
		set_state("idle")
	physics(world)

# Charger: an armored rusher. It paws the ground (heavy telegraph: a perfect parry negates it), then charges
# in a straight line. Hitting a wall, or being perfect-parried, leaves it dazed and open.
func charger(world) -> void:
	var CH: Dictionary = Tune.C.CHARGER
	var p = nearest_player(world, 26)
	target = p
	if state == "idle" or state == "approach":
		if p == null:
			vx *= 0.8; physics(world)
			return
		var dx: float = p.x - x
		var dist := absf(dx)
		var level: bool = absf(p.y - y) < 1.8
		facing = 1 if dx >= 0 else -1
		var sp: float = Tune.C.ENEMY_TYPES.charger.speed
		if dist < CH.minRange + 1:
			vx = -facing * sp   # backs off to get a run-up
		elif dist > CH.maxRange - 2:
			vx = facing * sp
		else:
			vx *= 0.7
		state = "approach"
		if cd == 0 and level and dist >= CH.minRange and dist <= CH.maxRange and world.director.request(self, "melee"):
			set_state("windup"); vx = 0; world.telegraph(self, "heavy", CH.wind)
	elif state == "windup":
		vx *= 0.5
		if p != null:
			facing = 1 if p.x >= x else -1
		if st >= CH.wind:
			set_state("charge"); atk = { "inst": world.new_instance(), "x0": x }; world.emit("chargeStart", { "e": self })
	elif state == "charge":
		vx = facing * CH.speed
		var x0 := x + 0.1 if facing > 0 else x - 1.4
		world.spawn_hitbox({ "owner": self, "team": "e", "x0": x0, "x1": x0 + 1.3, "y0": y + 0.1, "y1": y + 1.4, "dmg": CH.dmg, "heavy": true,
			"kb": [facing * 12.0, 5.0], "instance": atk.inst, "cat": "heavy" })
		if hit_wall != 0:
			set_state("dazed"); vx = -facing * 3; world.emit("chargeCrash", { "e": self })
		elif st >= CH.maxTicks:
			set_state("recover")
	elif state == "dazed":
		vx *= 0.85
		if st >= CH.daze:
			release_token(world); cd = int(CH.cd); set_state("idle")
	elif state == "recover":
		vx *= 0.85
		if st >= 30:
			release_token(world); cd = int(CH.cd); set_state("idle")
	physics(world)

func turret(world) -> void:
	var tgt = null
	var bd := 13.0
	for p in world.players:
		if not can_target(p) or p.x > x + 0.5 or p.x < 44:
			continue
		var d := U.hypot(p.x - x, p.y + 1 - y)
		if d < bd:
			bd = d; tgt = p
	if state == "idle":
		if tgt != null and cd == 0:
			var heavy := cycle % 3 == 2
			cycle += 1
			atk = { "heavy": heavy }; target = tgt
			set_state("windup"); world.telegraph(self, "heavy" if heavy else "standard", 24)
	elif state == "windup":
		if st >= 24:
			var t = target
			if t != null and can_target(t):
				var dx: float = t.x - x
				var dy: float = t.y + 1.0 - y
				var d := U.hypot(dx, dy)
				if d == 0: d = 1
				var sp := 14.0 if atk.heavy else 12.0
				world.spawn_projectile({ "team": "e", "owner": self, "x": x - 0.5, "y": y + 0.4, "vx": dx / d * sp, "vy": dy / d * sp, "r": 0.28 if atk.heavy else 0.2,
					"dmg": 14.0 if atk.heavy else 6.0, "heavy": atk.heavy, "kind": "heavy" if atk.heavy else "std", "ttl": 120 })
				world.emit("enemyShot", { "e": self, "heavy": atk.heavy })
			cd = 70; set_state("idle")
