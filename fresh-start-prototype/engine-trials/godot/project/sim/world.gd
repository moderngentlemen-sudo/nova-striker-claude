# World: owns every entity, runs the fixed-tick simulation (60 Hz), and collects events for the views, audio
# and UI. Nothing in here touches nodes or rendering. A port of world.js; step() runs in the same order as the
# prototype's (players, enemies, shockwaves, projectiles, wells, pickups, lifts, hitboxes, ultimates,
# barriers, revives, camera, encounters), which the tests depend on.
#
# Events are Dictionaries { type, ... } with the prototype's names and fields; the Game node re-emits them as
# a signal each tick.
class_name World
extends RefCounted

var players: Array = []
var enemies: Array = []
var projectiles: Array = []
var hitboxes: Array = []
var barriers: Array = []
var shockwaves: Array = []
var events: Array = []
var scheduled: Array = []
var wells: Array = []
var gadgets: Array = []
var pickups: Array = []
var ult_cast = null
var hit_sets := {}
var tick := 0
var instance_seq := 1
var checkpoint := 0
var wipe_t := 0
var global_bark_cd := 0
var arena := { "state": "idle" }
var tower_spawned := false
var encounters: Array = []
var route_done := false
var routes_done := {}
var boss_cleared_t := -1000000000
var aspect := 16.0 / 9.0
var cam := { "x": 0.0, "y": 3.0, "dist": 16.0, "halfW": 10.0, "halfH": 5.0 }
var director: Director
var level: Level
var rng: Rng

const BARKS := {
	"nova": {
		"intercept_save": ["Got that one.", "Covered."], "saved_reply": ["Thanks. Eyes up."],
		"perfect": ["Denied.", "Not today."], "revive": ["Up. I have you.", "On your feet."],
		"revived": ["Thanks.", "Noted."], "lash_reply": ["Show-off.", "I had him."],
		"lash_save": ["Over here."], "lock_broken": ["Lock's open. Move."],
		"challenge_reply": ["Don't make a habit of that.", "I see them. Covering you."],
		"perfect_shot": ["Textbook.", "Right on the mark.", "Clean."],
	},
}

func _init(seed_ := 1) -> void:
	Tune.ensure()
	rng = Rng.new(seed_)
	level = Level.new()
	director = Director.new(self)
	for def in Tune.L.ENCOUNTERS:
		encounters.append({ "def": def, "state": "idle", "wave": 0, "boss": null })
	level.restore_boxes(); spawn_level_pickups()
	spawn_gym()

func emit(type: String, data := {}) -> void:
	var ev := data.duplicate()
	ev.type = type
	events.append(ev)

func new_instance() -> int:
	instance_seq += 1
	return instance_seq - 1

func schedule(ticks: int, fn: Callable) -> void:
	scheduled.append({ "t": tick + ticks, "fn": fn })

func active_players() -> Array:
	return players.filter(func(p): return p.state != "dead" and p.state != "downed")

# ---- Spawning helpers used by players, enemies and combat ----
func spawn_hitbox(hb: Dictionary) -> void:
	hitboxes.append(hb)

func spawn_projectile(pr: Dictionary) -> Dictionary:
	var out := { "ttl": 60, "r": 0.15, "dmg": 1.0, "poise": 6.0, "hitSet": {}, "dead": false }
	out.merge(pr, true)
	out.px = out.x; out.py = out.y
	projectiles.append(out)
	return out

func spawn_shockwave(e, dir: int, dmg: float, scale := 1.0) -> void:
	shockwaves.append({ "owner": e, "x": e.x + dir * (e.w / 2), "y": e.y, "dir": dir, "speed": 11.0, "ttl": U.jround(60 * scale), "dmg": dmg, "h": 0.9, "instance": new_instance() })

func telegraph(e, cat: String, ticks: int) -> void:
	emit("telegraph", { "e": e, "cat": cat, "ticks": ticks })

func fire_shot(p: PlayerSim, lvl: int) -> void:
	var N: Dictionary = Tune.C.NOVA
	var M: Dictionary = Tune.C.MARKSMAN
	var cx := p.chest_x()
	var cy := p.chest_y()
	var ax := p.aim_x
	var ay := p.aim_y
	var spec: Dictionary
	if lvl == 0:
		spec = { "speed": N.shotSpeed, "dmg": N.shotDmg, "poise": 6.0, "r": 0.16, "kb": 1.5, "kind": "shot" }
	elif lvl == 1:
		spec = { "speed": N.lance.speed, "dmg": N.lance.dmg, "poise": N.lance.poise, "r": 0.24, "pierce": true, "kb": 5.0, "kind": "lance" }
	else:
		spec = { "speed": N.rail.speed, "dmg": N.rail.dmg, "poise": N.rail.poise, "r": 0.3, "pierce": true, "rail": true, "armorBreak": true, "kb": 9.0, "kind": "rail" }
	spec.dmg *= p.focus_mult()
	var pr := { "team": "p", "owner": p, "x": cx + ax * 0.7, "y": cy + ay * 0.7, "vx": ax * spec.speed, "vy": ay * spec.speed,
		"ttl": U.jround(N.shotRange / spec.speed * 60), "intercept": true, "homing": lvl > 0, "level": lvl }
	# Marksman kit: rounds fly the whole level and splash where they land
	if p.marksman():
		spec.splash = M.round.splash; spec.ttl = M.life
	pr.merge(spec, true)
	spawn_projectile(pr)
	if lvl == 2 and not p.on_ground:
		p.vy = maxf(p.vy, 1.5)   # a mid-air shot briefly holds him up (no push-back)
	elif lvl == 1 and not p.on_ground:
		p.vy = maxf(p.vy, 0.5)
	emit("shot", { "p": p, "level": lvl, "x": cx + ax * 0.7, "y": cy + ay * 0.7 })

# Marksman kit: a charged release fires the loaded attachment. Every projectile from one release shares a
# family, so the shot as a whole earns Focus once and rocket-jumps Nova at most once. The family also carries
# how long the shot was charged (chargeT), which sets the rocket jump height.
func fire_attachment(p: PlayerSim, kind: String, lvl: int, perfect: bool, charge_t := -1.0) -> void:
	var M: Dictionary = Tune.C.MARKSMAN
	if charge_t < 0:
		charge_t = M.charge[lvl - 1]
	var ax := p.aim_x
	var ay := p.aim_y
	var over := p.spend_overcharge()   # Aegis Overcharge: a stronger release
	var x := p.chest_x() + ax * 0.7
	var y := p.chest_y() + ay * 0.7
	var fm := p.focus_mult() * over
	var mult: float = fm * (M.perfectMult if perfect else 1.0)
	var family := { "focused": false, "rocketed": false, "perfect": perfect, "chargeT": charge_t, "attach": kind, "level": lvl }
	var base := { "team": "p", "owner": p, "x": x, "y": y, "level": lvl, "perfect": perfect, "family": family, "intercept": true, "ttl": M.life }
	var splash := func(S: Dictionary) -> Dictionary:
		var o := S.duplicate()
		o.dmg = S.dmg * mult; o.poise = S.poise * mult; o.r = S.r * (1.2 if perfect else 1.0)
		return o
	if kind == "lance":
		var L: Dictionary = M.lance[lvl]
		var pr := base.duplicate()
		pr.merge({ "vx": ax * L.speed, "vy": ay * L.speed, "r": L.r * (1.2 if perfect else 1.0), "dmg": L.dmg * mult, "poise": L.poise * mult, "kb": L.kb, "pierce": true,
			"homing": true, "armorBreak": bool(L.get("armorBreak", false)) or perfect, "rail": bool(L.get("rail", false)) or perfect, "interceptHeavy": true,
			"kind": "rail" if lvl == 3 else "lance", "splash": splash.call(L.splash) }, true)
		spawn_projectile(pr)
		if not p.on_ground:
			p.vy = maxf(p.vy, 1.5 if lvl == 3 else 0.5)
	elif kind == "volley":
		var V: Dictionary = M.volley
		var key = "perfect" if perfect else lvl
		var n: int = int(V.darts[key])
		var fan: float = V.fan[key]
		var a0 := atan2(ay, ax)
		# Darts are spread over the enemies in front: the lower darts take the lower targets. Locked onto a
		# target he chose, every dart goes for it.
		var targets: Array
		if p.lock_t and not p.lock_t.dead and p.lock_chosen():
			targets = [p.lock_t]
		else:
			targets = enemies_in_cone(p.chest_x(), p.chest_y(), ax, ay, V.seekRange, V.seekCone)
		var S: Dictionary = V.splash.duplicate()
		S.dmg = V.splash.dmg * fm; S.poise = V.splash.poise * fm; S.rocket = V.rocket
		for i in n:
			var a := a0 + fan * (float(i) / (n - 1) - 0.5)
			var sp: float = V.speed * (1 + (i % 2) * 0.08)
			var target = targets[mini(targets.size() - 1, int(floor(float(i) * targets.size() / n)))] if targets.size() else null
			var pr := base.duplicate()
			pr.merge({ "vx": cos(a) * sp, "vy": sin(a) * sp, "r": V.r, "dmg": V.dmg * fm, "poise": V.poise * fm, "kb": 2.0, "interceptHeavy": false,
				"kind": "dart", "splash": S, "seek": { "target": target, "delay": V.seekDelay, "until": V.seekFor, "turn": V.turn, "age": 0 } }, true)
			spawn_projectile(pr)
	elif kind == "arc":
		var A: Dictionary = M.arc
		var S: Dictionary = A[lvl]
		var dx := ax
		var dy: float = ay + A.lift
		var m := U.hypot(dx, dy)
		if m == 0: m = 1
		var pr := base.duplicate()
		pr.merge({ "intercept": false, "vx": dx / m * A.speed, "vy": dy / m * A.speed, "gravity": A.gravity, "r": 0.2, "dmg": 0.0, "poise": 0.0, "kind": "shell",
			"blast": { "r": S.r * (A.perfectRadius if perfect else 1.0), "dmg": S.dmg * mult, "poise": S.poise * mult, "armorBreak": bool(S.get("armorBreak", false)) or perfect, "rocket": S.rocket } }, true)
		spawn_projectile(pr)
	else:
		var P: Dictionary = M.prism
		var S: Dictionary = P[lvl]
		var pr := base.duplicate()
		pr.merge({ "vx": ax * P.speed, "vy": ay * P.speed, "r": P.r * (1.2 if perfect else 1.0), "dmg": S.dmg * mult, "poise": S.poise * mult, "kb": 3.0, "homing": true,
			"interceptHeavy": true, "kind": "prism", "splash": splash.call(S.splash),
			"prism": { "shards": S.shards, "bounces": S.bounces + (P.perfectBounces if perfect else 0), "mult": mult } }, true)
		spawn_projectile(pr)
	emit("shot", { "p": p, "level": lvl, "attach": kind, "perfect": perfect, "x": x, "y": y, "ax": ax, "ay": ay, "over": over > 1 })
	if perfect:
		emit("perfectRelease", { "p": p, "x": x, "y": y, "attach": kind }); bark(p, "perfect_shot", 0.25)

# Explosions: splash from Nova's shots, Arc shells, the level 3 burst, and enemy mortar shells. A player blast
# hits every enemy in the radius (over the top of shields) except `skip`, the one a shot already hit
# directly; with `rocket` set it can also launch Nova. An enemy blast hits players and cannot be parried.
# o: { owner, team = 'p', x, y, spec, level = 0, perfect = false, family = null, skip = null, rocket = false, kind = 'splash' }
func explode(o: Dictionary) -> void:
	var owner = o.get("owner")
	var team: String = o.get("team", "p")
	var x: float = o.x
	var y: float = o.y
	var spec: Dictionary = o.spec
	var family = o.get("family")
	var skip = o.get("skip")
	blast_boxes(x, y, spec.r, (spec.dmg if spec.get("dmg") else 2.0) * 1.5, owner)
	if team == "e":
		var id := new_instance()
		for q in players:
			if q.state == "dead" or q.state == "downed" or not _reach(q, x, y, spec.r):
				continue
			Combat.hit_player(self, q, { "owner": owner, "dmg": spec.dmg, "unblockable": true, "cat": "unblockable", "heavy": true,
				"kb": [U.sgn_or(q.x - x, 1) * 7.0, 6.0], "instance": id, "at": { "x": x, "y": y } })
		emit("enemyBlast", { "x": x, "y": y, "r": spec.r })
		return
	for e in enemies:
		if e.dead or e == skip or not _reach(e, x, y, spec.r):
			continue
		var res := Combat.hit_enemy(self, e, { "owner": owner, "dmg": spec.get("dmg", 0.0), "poise": spec.get("poise", 0.0), "armorBreak": bool(spec.get("armorBreak", false)),
			"kb": [U.sgn_or(e.x - x, 1) * 7.0, 5.0], "blast": true }, "blast")
		if family != null and (res == "hit" or res == "kill"):
			Combat.award_focus(self, { "owner": owner, "family": family })
	if o.get("rocket") and spec.get("rocket") and owner != null and owner.kind == "player" and owner.marksman():
		rocket_push(owner, x, y, spec, family, o.get("perfect", false))
	emit(o.get("kind", "splash"), { "p": owner, "x": x, "y": y, "r": spec.r, "level": o.get("level", 0), "perfect": o.get("perfect", false) })

static func _reach(ent, x: float, y: float, r: float) -> bool:
	var nx := maxf(ent.x - ent.w / 2, minf(x, ent.x + ent.w / 2))
	var ny := maxf(ent.y, minf(y, ent.y + ent.h))
	return U.hypot(x - nx, y - ny) <= r

# Height gained by a body launched upward at v with no rise cut, as the fixed-tick integration plays it out
static func apex_gain(v: float) -> float:
	return maxf(0, v * v / (2 * Tune.C.GRAVITY) - v * Tune.DT / 2)

# Rocket jump: a charged shot bursting on terrain close to Nova launches him away from the burst. The launch
# speed is the one that reaches the height the charge earned (rocket_height), weaker for a burst further away
# and for each extra rocket jump in the same airtime. It sets his climb speed rather than adding to it, so the
# height the charge earned is a real ceiling. A short impact pause (freeze) sells the blast before he leaves.
func rocket_push(p: PlayerSim, x: float, y: float, spec: Dictionary, family, perfect: bool) -> void:
	var R: Dictionary = Tune.C.MARKSMAN.rocket
	var G: float = Tune.C.GRAVITY
	if family != null and family.rocketed:
		return
	var reach: float = spec.r + R.reach
	if U.hypot(p.x - x, p.y + p.h * 0.5 - y) > reach or p.state == "downed" or p.state == "dead":
		return
	# A burst anywhere under his boots counts as right under him: the first `slack` m sideways are ignored for
	# both the direction and the strength
	var ox := p.x - x
	var sx: float = U.sgn(ox) * maxf(0, absf(ox) - R.slack)
	var dx := sx
	var dy := p.y + p.h * 0.5 - y
	var d_eff := U.hypot(dx, dy)
	if d_eff < 0.05:
		dx = 0; dy = 1
	else:
		dx /= d_eff; dy /= d_eff
	if family != null:
		family.rocketed = true
	var H := PlayerSim.rocket_height(family.chargeT if family != null else Tune.C.MARKSMAN.charge[2], family.attach if family != null else "arc", perfect)
	var near_f: float = maxf(0, d_eff - R.close) / maxf(0.01, reach - R.close)   # 0 for a burst at his feet
	var air: float = 1.0 if p.on_ground else R.air[mini(p.rockets, R.air.size() - 1)]
	var k: float = sqrt(2 * G * H) * (1 - R.falloff * minf(1, near_f)) * air
	if not p.on_ground:
		p.rockets += 1
	var nvx: float = p.vx + dx * k * R.side
	if absf(nvx) > R.sideMax and absf(nvx) > absf(p.vx):
		nvx = U.sgn(nvx) * maxf(R.sideMax, absf(p.vx))
	p.vx = nvx
	if dy > 0:
		p.vy = maxf(p.vy, dy * k)
	else:
		p.vy += dy * k * 0.5
	if dy > 0.2:
		p.on_ground = false; p.coyote = 0   # launched: no late ground jump to cut the climb
	p.dash_carry = true; p.fast_fall = false   # keeps the launch: no rise cut, momentum carries
	var power := minf(1, k / sqrt(2 * G * R.perfect))
	var hgt := apex_gain(dy * k) if dy > 0 else 0.0
	p.rocket_t = 50; p.rocket_pow = power
	p.hitstop = maxi(p.hitstop, int(R.freeze[0 if power < 0.45 else (1 if power < 0.8 else 2)]))
	emit("rocketJump", { "p": p, "x": x, "y": y, "k": k, "h": hgt, "power": power, "perfect": perfect, "level": family.level if family != null else 3, "dx": dx, "dy": dy })

# What a rocket jump would do right now: while Nova charges with his aim pointing down and a surface close
# enough below, the height his feet would reach if he let go now. Presentation only (the apex marker).
func rocket_preview(p: PlayerSim):
	var M: Dictionary = Tune.C.MARKSMAN
	var R: Dictionary = M.rocket
	if not p.marksman() or p.charge_t < M.charge[0] or p.charge_t >= M.beam.at or p.aim_y > -0.6 or not p.state in ["normal", "slide"]:
		return null
	var gy := level.ground_below(p.x, p.y + 0.1)
	if gy == -INF:
		return null
	var stage := p.charge_stage()
	var perfect := stage == "perfect"
	var lvl := 3 if p.charge_t >= M.charge[2] else (2 if p.charge_t >= M.charge[1] else 1)
	var A := p.attachment
	var r: float
	if A == "lance": r = M.lance[lvl].splash.r * (1.2 if perfect else 1.0)
	elif A == "volley": r = M.volley.splash.r
	elif A == "arc": r = M.arc[lvl].r * (M.arc.perfectRadius if perfect else 1.0)
	else: r = M.prism[lvl].splash.r * (1.2 if perfect else 1.0)
	var d := p.y + p.h * 0.5 - (gy + 0.15)
	var reach: float = r + R.reach
	if d > reach:
		return null
	var H := PlayerSim.rocket_height(p.charge_t, A, perfect)
	var near_f: float = maxf(0, d - R.close) / maxf(0.01, reach - R.close)
	var air: float = 1.0 if p.on_ground else R.air[mini(p.rockets, R.air.size() - 1)]
	var k: float = sqrt(2 * Tune.C.GRAVITY * H) * (1 - R.falloff * minf(1, near_f)) * air
	var up := maxf(p.vy, k)
	return { "x": p.x, "y": p.y, "apex": p.y + apex_gain(up), "level": lvl, "perfect": perfect, "h": H }

# ---- Nova: the Level 4 beam ----
# Every tick: trace the beam to the first wall (a Prism beam bounces once), erase enemy shots it touches, and
# every `pulse` ticks hit every enemy in it. Attachments add their flavour.
func beam_tick(p: PlayerSim) -> void:
	var B := p.beam_spec()
	var b: Dictionary = p.beam
	var cx := p.chest_x()
	var cy := p.chest_y()
	var segs := []
	var sx: float = cx + b.dx * 0.6
	var sy: float = cy + b.dy * 0.6
	var dx: float = b.dx
	var dy: float = b.dy
	var bounces: int = int(B.prism.bounces) if b.attach == "prism" else 0
	for i in bounces + 1:
		var hh := level.ray_cast(sx, sy, dx, dy, B.range)
		segs.append({ "x0": sx, "y0": sy, "x1": hh.x, "y1": hh.y, "wall": hh.wall, "nx": hh.nx, "ny": hh.ny })
		if not hh.wall or i == bounces:
			break
		var dot: float = dx * hh.nx + dy * hh.ny
		dx -= 2 * dot * hh.nx; dy -= 2 * dot * hh.ny
		sx = hh.x + hh.nx * 0.05; sy = hh.y + hh.ny * 0.05
	b.segs = segs; b.pulse += 1
	var last: Dictionary = segs[segs.size() - 1]
	var end_box = level.ray_cast(last.x0, last.y0, dx, dy, B.range).box
	if end_box != null and end_box.type == "d" and b.pulse % int(B.pulse) == 1:
		damage_box(end_box, B.dmg * b.mult * 2, last.x1, last.y1, p)
	for pr in projectiles:
		if pr.team == "e" and not pr.dead and _near_segs(segs, pr.x, pr.y, B.width + pr.r):
			pr.dead = true; emit("erase", { "x": pr.x, "y": pr.y })
	if b.pulse % int(B.pulse) == 1:
		for e in enemies:
			if e.dead:
				continue
			var hb := Combat.hurtbox(e)
			var touched := false
			for g in segs:
				if seg_hits_box(g, hb, B.width):
					touched = true
					break
			if not touched:
				continue
			var lastp = b.armor.get(e.id)
			var ab: bool = lastp == null or b.pulse - lastp >= B.armorEvery
			if ab:
				b.armor[e.id] = b.pulse
			var res := Combat.hit_enemy(self, e, { "owner": p, "dmg": B.dmg * b.mult, "poise": B.poise * b.mult, "kb": [U.sgn(b.dx) * B.get("kb", 3.0), 1.0], "vx": b.dx,
				"armorBreak": ab, "rail": true, "beam": true }, "proj")
			if p.char == "nova" and (res == "hit" or res == "kill"):
				Combat.award_focus(self, { "owner": p, "family": b.family })
	var end: Dictionary = segs[segs.size() - 1]
	if b.attach == "arc" and b.pulse % int(B.arc.every) == 0:
		var spec: Dictionary = B.arc.blast.duplicate()
		spec.dmg = B.arc.blast.dmg * b.mult; spec.poise = B.arc.blast.poise * b.mult; spec.armorBreak = true
		explode({ "owner": p, "x": end.x1, "y": end.y1, "spec": spec, "level": 2, "kind": "blast" })
	if b.attach == "volley" and b.pulse % int(B.volley.every) == 0:
		var V: Dictionary = Tune.C.MARKSMAN.volley
		var a: float = atan2(b.dy, b.dx) + (rng.next() - 0.5) * 0.9
		var sx0: float = cx + b.dx * 0.7
		var sy0: float = cy + b.dy * 0.7
		var target = nearest_enemy_in_cone(sx0, sy0, b.dx, b.dy, V.seekRange, V.seekCone)
		spawn_projectile({ "team": "p", "owner": p, "x": sx0, "y": sy0, "vx": cos(a) * V.speed, "vy": sin(a) * V.speed, "ttl": Tune.C.MARKSMAN.life, "r": V.r,
			"dmg": V.dmg * b.mult, "poise": V.poise * b.mult, "kb": 2.0, "kind": "dart", "level": 4, "family": b.family, "intercept": true, "interceptHeavy": false,
			"splash": V.splash.duplicate(), "seek": { "target": target, "delay": 4, "until": V.seekFor, "turn": V.turn, "age": 0 } })

func _near_segs(segs: Array, x: float, y: float, r: float) -> bool:
	for g in segs:
		if dist_to_seg(x, y, g) < r:
			return true
	return false

func end_beam(p: PlayerSim, why: String) -> void:
	if p.beam == null:
		return
	emit("beamEnd", { "p": p, "why": why })
	p.beam = null

# ---- Nova: the hard-light Aegis ----
func raise_aegis(p: PlayerSim) -> void:
	var A: Dictionary = Tune.C.AEGIS
	p.aegis = { "hp": A.hp, "max": A.hp, "t": A.ticks, "seen": {} }
	emit("aegisOn", { "p": p })

# Ends it: 'break' (damage) shatters it outward, 'detonate' (pressed again) blasts it outward on purpose
func end_aegis(p: PlayerSim, why: String) -> void:
	var S = p.aegis
	if S == null:
		return
	var A: Dictionary = Tune.C.AEGIS
	var cx := p.chest_x()
	var cy := p.chest_y()
	var frac := maxf(0, S.hp / S.max)
	p.aegis = null; p.aegis_cd = A.cd
	if why == "break" or why == "detonate":
		var B: Dictionary = A.shatter if why == "break" else A.detonate
		var k := 0.5 + 0.5 * frac if why == "detonate" else 1.0
		spawn_hitbox({ "owner": p, "team": "p", "x0": cx - B.r, "x1": cx + B.r, "y0": cy - B.r, "y1": cy + B.r, "dmg": B.dmg * k, "poise": B.poise * k,
			"kb": [B.kb, 5.0], "radial": true, "cx": cx, "armorBreak": true, "instance": new_instance(), "aegisBurst": true })
		if why == "detonate":
			p.overcharge = minf(A.over.max, p.overcharge + A.detonate.over * frac); p.over_t = int(A.over.hold)
	emit("aegisOff", { "p": p, "why": why, "x": cx, "y": cy, "frac": frac })

func detonate_aegis(p: PlayerSim) -> void:
	end_aegis(p, "detonate")

# The Nova whose Aegis shelters this player (their own, or one they stand inside), if any
func shield_for(q):
	for n in players:
		if n.aegis == null or n.state == "dead" or n.state == "downed":
			continue
		if n == q:
			return n
		if U.hypot(n.chest_x() - q.chest_x(), n.chest_y() - q.chest_y()) < Tune.C.AEGIS.radius:
			return n
	return null

# The Aegis a point (a shot of radius r) has reached, if any
func aegis_at(x: float, y: float, r: float):
	for n in players:
		if n.aegis == null or n.state == "dead" or n.state == "downed":
			continue
		if U.hypot(x - n.chest_x(), y - n.chest_y()) < Tune.C.AEGIS.radius + r:
			return n
	return null

# The Aegis takes a hit coming from (fx, fy): damage to the hard light becomes Overcharge. `key` makes one
# attack (a hitbox, a blast) count once even when it reaches several players inside.
func absorb_aegis(n: PlayerSim, dmg: float, fx: float, fy: float, key) -> void:
	var S = n.aegis
	if S == null:
		return
	if key != null:
		if S.seen.has(key):
			return
		S.seen[key] = true
	var A: Dictionary = Tune.C.AEGIS
	var dx := fx - n.chest_x()
	var dy := fy - n.chest_y()
	var m := U.hypot(dx, dy)
	if m == 0: m = 1
	dx /= m; dy /= m
	S.hp -= dmg
	n.overcharge = minf(A.over.max, n.overcharge + dmg * A.over.perDmg); n.over_t = int(A.over.hold)
	emit("aegisHit", { "p": n, "x": n.chest_x() + dx * A.radius, "y": n.chest_y() + dy * A.radius, "dx": dx, "dy": dy, "dmg": dmg, "frac": maxf(0, S.hp / S.max) })
	if S.hp <= 0:
		end_aegis(n, "break")

# Prism rounds split into shards that fan out along (dx, dy); shards skip the enemy that split them
func split_prism(pr: Dictionary, x: float, y: float, dx: float, dy: float, skip) -> void:
	var S: Dictionary = Tune.C.MARKSMAN.prism.shard
	var n: int = int(pr.prism.shards)
	var a0 := atan2(dy, dx)
	var splash: Dictionary = S.splash.duplicate()
	splash.dmg = S.splash.dmg * pr.prism.mult; splash.poise = S.splash.poise * pr.prism.mult
	for i in n:
		var a: float = a0 + S.fan * (i - (n - 1) / 2.0)
		var sh := spawn_projectile({ "team": "p", "owner": pr.owner, "x": x, "y": y, "vx": cos(a) * S.speed, "vy": sin(a) * S.speed, "ttl": Tune.C.MARKSMAN.life, "r": S.r,
			"dmg": S.dmg * pr.prism.mult, "poise": S.poise * pr.prism.mult, "kb": 2.0, "level": pr.level, "perfect": pr.perfect, "family": pr.family,
			"intercept": true, "interceptHeavy": false, "bounces": pr.prism.bounces, "kind": "shard", "splash": splash })
		if skip != null:
			sh.hitSet[skip.id] = true
	emit("split", { "p": pr.owner, "x": x, "y": y, "n": n })

# ---- Nova: secondary weapons (SUB) ----
func fire_sub(p: PlayerSim, lvl: int, perfect := false) -> void:
	var k := p.sub
	if k == "grenade": throw_grenade(p, lvl, perfect)
	elif k == "chain": fire_chain(p, lvl, perfect)
	elif k == "disc": throw_disc(p, lvl, perfect)
	elif k == "well": launch_well(p, lvl, perfect)
	else: fire_burst(p, lvl, perfect)
	p.shoot_t = 10

# Scatter: point-blank pellets, level 0 for the quick press or 1-3 when charged; level 3 adds a blast at the
# muzzle. No recoil: Nova stays where he is.
func fire_burst(p: PlayerSim, lvl: int, perfect := false) -> void:
	var M: Dictionary = Tune.C.MARKSMAN
	var B: Dictionary = M.burst
	var S: Dictionary = B[lvl] if lvl else B.tap
	var ax := p.aim_x
	var ay := p.aim_y
	var a0 := atan2(ay, ax)
	var x := p.chest_x() + ax * 0.5
	var y := p.chest_y() + ay * 0.5
	var mult: float = M.perfectMult if perfect else 1.0
	var n: int = int(S.pellets)
	for i in n:
		var a: float = a0 + S.fan * (float(i) / (n - 1) - 0.5)
		spawn_projectile({ "team": "p", "owner": p, "x": x, "y": y, "vx": cos(a) * S.speed, "vy": sin(a) * S.speed, "ttl": M.life, "r": 0.16,
			"dmg": S.dmg * mult, "poise": S.poise * mult, "kb": S.kb, "kbY": 2.0, "intercept": true, "interceptHeavy": false, "kind": "pellet",
			"falloff": { "x": x, "y": y, "d": B.falloff }, "armorBreak": bool(S.get("armorBreak", false)) and i == (n >> 1) })
	if S.has("blast"):
		explode({ "owner": p, "x": x + ax * 0.7, "y": y + ay * 0.7, "level": lvl, "perfect": perfect, "kind": "blast",
			"spec": { "r": S.blast.r * (1.25 if perfect else 1.0), "dmg": S.blast.dmg * mult, "poise": S.blast.poise * mult, "armorBreak": true } })
	p.shoot_t = 10; p.burst_cd = B.cd
	emit("burst", { "p": p, "x": x, "y": y, "ax": ax, "ay": ay, "level": lvl, "charged": lvl > 0, "perfect": perfect })

# Grenade: a bouncing frag on a fuse (Combat bounces it); it bursts early on an enemy, and level 3 scatters
# bomblets when it goes off (cluster_burst)
func throw_grenade(p: PlayerSim, lvl: int, perfect: bool) -> void:
	var G: Dictionary = Tune.C.SUB.grenade
	var ax := p.aim_x
	var ay := p.aim_y
	var dy: float = ay + G.lift
	var m := U.hypot(ax, dy)
	if m == 0: m = 1
	var sp: float = G.speed[lvl]
	var mult: float = Tune.C.MARKSMAN.perfectMult if perfect else 1.0
	var B: Dictionary = G.blast[lvl]
	var x := p.chest_x() + ax * 0.6
	var y := p.chest_y() + ay * 0.6
	spawn_projectile({ "team": "p", "owner": p, "x": x, "y": y, "vx": ax / m * sp, "vy": dy / m * sp, "gravity": G.gravity, "bouncy": G.bounce, "r": G.r, "ttl": G.fuse[lvl],
		"dmg": 0.0, "poise": 0.0, "kind": "grenade", "level": lvl, "perfect": perfect, "intercept": false,
		"blast": { "r": B.r * (1.2 if perfect else 1.0), "dmg": B.dmg * mult, "poise": B.poise * mult, "armorBreak": bool(B.get("armorBreak", false)) or perfect },
		"cluster": G.bomblets if lvl == 3 else null })
	p.burst_cd = G.cd
	emit("grenadeThrow", { "p": p, "x": x, "y": y, "level": lvl, "perfect": perfect })

func cluster_burst(pr: Dictionary, x: float, y: float) -> void:
	var K: Dictionary = pr.cluster
	var n: int = int(K.n)
	for i in n:
		var a := PI / 2 + (float(i) / (n - 1) - 0.5) * 2.2
		spawn_projectile({ "team": "p", "owner": pr.owner, "x": x, "y": y + 0.2, "vx": cos(a) * K.speed, "vy": sin(a) * K.speed + K.lift * 0.3, "gravity": Tune.C.SUB.grenade.gravity,
			"bouncy": 0.35, "r": 0.14, "ttl": K.fuse + i * 3, "dmg": 0.0, "poise": 0.0, "kind": "bomblet", "level": 1, "intercept": false, "blast": K.blast.duplicate() })
	emit("cluster", { "p": pr.owner, "x": x, "y": y, "n": n })

# Chain: instant lightning from the bracer to the nearest enemy in front (the lock-on target first), then
# from each enemy on to the nearest one within `hop` m it has not hit. It needs a clear line each jump, arcs
# round shields, and stuns light enemies (Combat.hit_enemy: hit.stun).
func fire_chain(p: PlayerSim, lvl: int, perfect: bool) -> void:
	var C: Dictionary = Tune.C.SUB.chain
	var ax := p.aim_x
	var ay := p.aim_y
	var mult: float = Tune.C.MARKSMAN.perfectMult if perfect else 1.0
	var x0 := p.chest_x() + ax * 0.6
	var y0 := p.chest_y() + ay * 0.6
	var R: float = C.range[lvl]
	var cosc := cos(C.cone)
	var first = null
	var t = p.lock_t
	if t and not t.dead and U.hypot(t.x - x0, t.y + t.h * 0.55 - y0) <= R and not level.segment_blocked(x0, y0, t.x, t.y + t.h * 0.55):
		first = t
	else:
		var bd := R
		for e in enemies:
			if e.dead:
				continue
			var mx: float = e.x
			var my: float = e.y + e.h * 0.55
			var dx := mx - x0
			var dy := my - y0
			var d := U.hypot(dx, dy)
			if d < bd and d > 1e-3 and (dx * ax + dy * ay) / d > cosc and not level.segment_blocked(x0, y0, mx, my):
				bd = d; first = e
	var pts := [{ "x": x0, "y": y0 }]
	var hit := {}
	var cur = first
	var from_x := x0
	while cur != null and hit.size() < C.jumps[lvl]:
		hit[cur] = true
		var mx: float = cur.x
		var my: float = cur.y + cur.h * 0.55
		pts.append({ "x": mx, "y": my })
		Combat.hit_enemy(self, cur, { "owner": p, "dmg": C.dmg[lvl] * mult, "poise": C.poise[lvl] * mult, "kb": [U.sgn_or(mx - from_x, p.facing) * 2.0, 1.0], "stun": C.stun[lvl], "shock": true,
			"armorBreak": lvl == 3 and hit.size() == 1 }, "blast")
		from_x = mx; cur = null
		var bd: float = C.hop
		for e in enemies:
			if e.dead or hit.has(e):
				continue
			var nx: float = e.x
			var ny: float = e.y + e.h * 0.55
			var d := U.hypot(nx - mx, ny - my)
			if d < bd and not level.segment_blocked(mx, my, nx, ny):
				bd = d; cur = e
	# Nothing in reach: the arc lashes out and earths itself on the nearest surface in front
	if first == null:
		var hh := level.ray_cast(x0, y0, ax, ay, R * 0.7)
		pts.append({ "x": hh.x, "y": hh.y, "fizzle": true })
	p.burst_cd = C.cd
	emit("chain", { "p": p, "pts": pts, "level": lvl, "perfect": perfect, "n": hit.size() })

# Disc: out along the aim, (from level 2) a hover at the far end, then back to him (Combat.steer_disc)
func throw_disc(p: PlayerSim, lvl: int, perfect: bool) -> void:
	var D: Dictionary = Tune.C.SUB.disc
	var ax := p.aim_x
	var ay := p.aim_y
	var sp: float = D.speed[lvl]
	var mult: float = Tune.C.MARKSMAN.perfectMult if perfect else 1.0
	var x := p.chest_x() + ax * 0.6
	var y := p.chest_y() + ay * 0.6
	spawn_projectile({ "team": "p", "owner": p, "x": x, "y": y, "vx": ax * sp, "vy": ay * sp, "ttl": 100000, "r": D.r[lvl] * (1.2 if perfect else 1.0),
		"dmg": D.dmg[lvl] * mult, "poise": D.poise[lvl] * mult, "kb": 3.0, "pierce": true, "intercept": true, "interceptHeavy": lvl >= 2, "kind": "disc", "level": lvl, "perfect": perfect,
		"disc": { "phase": "out", "t": 0, "out": D.out[lvl], "hover": D.hover[lvl] + (20 if perfect else 0), "dx": ax, "dy": ay, "speed": sp } })
	p.burst_cd = D.cd
	emit("discThrow", { "p": p, "x": x, "y": y, "level": lvl, "perfect": perfect })

# Gravity Well: an orb that opens where it stops (update_wells)
func launch_well(p: PlayerSim, lvl: int, perfect: bool) -> void:
	var W: Dictionary = Tune.C.SUB.well
	var x := p.chest_x() + p.aim_x * 0.7
	var y := p.chest_y() + p.aim_y * 0.7
	wells.append({ "owner": p, "x": x, "y": y, "px": x, "py": y, "vx": p.aim_x * W.speed, "vy": p.aim_y * W.speed, "phase": "orb", "t": 0, "level": lvl, "perfect": perfect,
		"r": W.r[lvl] * (1.2 if perfect else 1.0), "life": W.life[lvl] + (30 if perfect else 0), "mult": Tune.C.MARKSMAN.perfectMult if perfect else 1.0,
		"collapse": false, "dead": false })
	p.burst_cd = W.cd
	emit("wellLaunch", { "p": p, "x": x, "y": y, "level": lvl, "perfect": perfect })

# Is a disc or a well of his still out?
func sub_out(p: PlayerSim, kind: String) -> bool:
	if kind == "disc":
		for pr in projectiles:
			if pr.get("owner") == p and pr.kind == "disc" and not pr.dead:
				return true
		return false
	if kind == "well":
		for w in wells:
			if w.owner == p:
				return true
	return false

# Pressed again while it is out: the disc turns for home; the well opens where the orb is, or collapses
func recall_sub(p: PlayerSim, kind: String) -> void:
	if kind == "disc":
		for pr in projectiles:
			if pr.get("owner") == p and pr.kind == "disc" and not pr.dead:
				if pr.disc.phase != "back":
					pr.disc.phase = "back"; pr.disc.t = 0; pr.ghost = true; pr.hitSet.clear(); emit("discRecall", { "p": p, "x": pr.x, "y": pr.y })
				return
	elif kind == "well":
		for w in wells:
			if w.owner == p:
				if w.phase == "orb":
					open_well(w)
				else:
					w.collapse = true
				return

func open_well(w: Dictionary) -> void:
	var W: Dictionary = Tune.C.SUB.well
	w.phase = "open"; w.t = 0
	# A well that opens at floor level lifts a little, so what it catches floats up into it
	var gy := level.ground_below(w.x, w.y + 0.05)
	if gy > -INF and w.y - gy < W.lift and not level.point_in_solid(w.x, gy + W.lift):
		w.y = gy + W.lift
	emit("wellOpen", { "p": w.owner, "x": w.x, "y": w.y, "r": w.r, "level": w.level })

func update_wells(frozen: bool) -> void:
	var W: Dictionary = Tune.C.SUB.well
	var DT := Tune.DT
	for w in wells:
		w.px = w.x; w.py = w.y; w.t += 1
		var gone := not players.has(w.owner)
		if w.phase == "orb":
			var nx: float = w.x + w.vx * DT
			var ny: float = w.y + w.vy * DT
			var open: bool = w.t >= W.travel[w.level] or gone
			if level.point_in_solid(nx, ny):
				open = true
			else:
				w.x = nx; w.y = ny
			if not open:
				for e in enemies:
					if not e.dead and absf(e.x - w.x) < e.w / 2 + 0.35 and w.y > e.y - 0.35 and w.y < e.y + e.h + 0.35:
						open = true
						break
			if open:
				open_well(w)
			continue
		# Open: pull light enemies in and hold them, drag heavy ones, swallow enemy shots, hurt everything
		var L: int = w.level
		var tick_hit: bool = w.t % int(W.tick) == 0
		for e in enemies:
			if e.dead:
				continue
			var cy: float = e.y + e.h * 0.5
			var dx: float = w.x - e.x
			var dy: float = w.y - cy
			var d := U.hypot(dx, dy)
			if d > w.r:
				continue
			if tick_hit:
				Combat.hit_enemy(self, e, { "owner": w.owner, "dmg": W.tickDmg[L] * w.mult, "poise": 4.0, "kb": [0.0, 0.0], "well": true }, "blast")
			if e.dead or e.boss or frozen:
				continue
			if Tune.C.ENEMY_TYPES[e.type].get("stationary"):
				continue
			var ux := dx / d if d > 1e-3 else 0.0
			var uy := dy / d if d > 1e-3 else 0.0
			var sp := minf(W.pull[L], d * 6)
			if e.light and e.armor <= 0:
				if not e.state in ["stagger", "caught", "snared"]:
					if e.state == "windup" or e.state == "aim" or e.state == "lock":
						director.release(e)
					e.state = "launched"; e.st = 1
				e.vx = ux * sp; e.vy = uy * sp + (0.0 if e.flier else Tune.C.GRAVITY * DT); e.well_t = 2
			else:
				# Heavy: dragged along the ground toward it
				var step: float = ux * W.pull[L] * W.heavy * DT
				var nx: float = e.x + step
				if not level.point_in_solid(nx + U.sgn(step) * e.w / 2, e.y + 0.3):
					e.x = nx
				e.well_t = 2
		for pr in projectiles:
			if pr.team != "e" or pr.dead:
				continue
			var dx: float = w.x - pr.x
			var dy: float = w.y - pr.y
			var d := U.hypot(dx, dy)
			if d > w.r:
				continue
			if d < 0.6:
				pr.dead = true; emit("erase", { "x": pr.x, "y": pr.y })
				continue
			var sp := U.hypot(pr.vx, pr.vy)
			pr.vx += dx / d * 40 * DT; pr.vy += dy / d * 40 * DT
			var s2 := U.hypot(pr.vx, pr.vy)
			if s2 == 0: s2 = 1
			pr.vx *= sp / s2; pr.vy *= sp / s2
		if w.t >= w.life or w.collapse or gone:
			var S: Dictionary = W.implode[L]
			explode({ "owner": null if gone else w.owner, "x": w.x, "y": w.y, "level": L + 1, "perfect": w.perfect, "kind": "wellCollapse",
				"spec": { "r": S.r * (1.2 if w.perfect else 1.0), "dmg": S.dmg * w.mult, "poise": S.poise * w.mult, "armorBreak": bool(S.get("armorBreak", false)) } })
			w.dead = true
	wells = wells.filter(func(w): return not w.dead)

# ---- Lock-on ----
# Candidates in range, best first: near, in front, in sight (training targets last)
func lock_candidates(p: PlayerSim) -> Array:
	var cx := p.chest_x()
	var cy := p.chest_y()
	var out := []
	for e in enemies:
		if e.dead:
			continue
		var ty: float = e.y + e.h * 0.55
		var dx: float = e.x - cx
		var d := U.hypot(dx, ty - cy)
		if d > Tune.C.LOCK.range:
			continue
		var behind := dx * p.facing < -0.5
		var blocked := level.segment_blocked(cx, cy, e.x, ty)
		out.append({ "e": e, "score": d + (7 if behind else 0) + (10 if blocked else 0) + (4 if e.type == "post" or e.type == "turret" else 0) })
	out.sort_custom(func(a, b): return a.score < b.score)
	return out.map(func(o): return o.e)

func best_lock_target(p: PlayerSim):
	for e in lock_candidates(p):
		if e != p.lock_t:
			return e
	return null

# Automatic lock-on: the nearest enemy in sight within LOCK.auto
func auto_lock_target(p: PlayerSim):
	var cx := p.chest_x()
	var cy := p.chest_y()
	for e in lock_candidates(p):
		if U.hypot(e.x - cx, e.y + e.h * 0.55 - cy) <= Tune.C.LOCK.auto and not level.segment_blocked(cx, cy, e.x, e.y + e.h * 0.55):
			return e
	return null

func next_lock_target(p: PlayerSim):
	var list := lock_candidates(p)
	if list.is_empty():
		return p.lock_t
	return list[(list.find(p.lock_t) + 1) % list.size()]

func set_lock(p: PlayerSim, e, why: String) -> void:
	var prev = p.lock_t
	p.lock_t = e; p.lock_lost = 0
	p.lock_picked = e != null and (why == "on" or why == "cycle")   # chosen by the player, not by automatic lock-on
	if e != null and e != prev:
		emit("lockSwitch" if prev != null else "lockOn", { "p": p, "e": e, "why": why })
	elif e == null and prev != null:
		emit("lockOff", { "p": p, "why": why })
	elif e == null and why == "on":
		emit("lockNone", { "p": p })

# The target died (the lock moves to the next one), left (removed), or is out of range or sight too long
func validate_lock(p: PlayerSim) -> void:
	var t = p.lock_t
	if t == null:
		return
	if t.dead or not enemies.has(t):
		set_lock(p, best_lock_target(p), "switch")
		return
	var cx := p.chest_x()
	var cy := p.chest_y()
	var ty: float = t.y + t.h * 0.55
	if U.hypot(t.x - cx, ty - cy) > Tune.C.LOCK.keep:
		set_lock(p, null, "range")
		return
	p.lock_lost = p.lock_lost + 1 if level.segment_blocked(cx, cy, t.x, ty) else 0
	if p.lock_lost > Tune.C.LOCK.lost:
		set_lock(p, null, "sight")

func nearest_enemy_in_cone(x: float, y: float, dx: float, dy: float, range_: float, half: float):
	var best = null
	var bd := range_
	var c := cos(half)
	for e in enemies:
		if e.dead or e.type == "post" or e.type == "turret":
			continue
		var ex: float = e.x - x
		var ey: float = e.y + e.h / 2 - y
		var d := U.hypot(ex, ey)
		if d < bd and (ex * dx + ey * dy) / d > c:
			bd = d; best = e
	return best

# Enemies within range and half-angle of a direction, ordered by signed angle from it
func enemies_in_cone(x: float, y: float, dx: float, dy: float, range_: float, half: float) -> Array:
	var c := cos(half)
	var out := []
	for e in enemies:
		if e.dead:
			continue
		var ex: float = e.x - x
		var ey: float = e.y + e.h / 2 - y
		var d := U.hypot(ex, ey)
		if d > range_ or d < 1e-3 or (ex * dx + ey * dy) / d < c:
			continue
		out.append({ "e": e, "a": atan2(dx * ey - dy * ex, dx * ex + dy * ey) })
	out.sort_custom(func(a, b): return a.a < b.a)
	return out.map(func(o): return o.e)

func nearest_enemy_dist(x: float, y: float) -> float:
	var bd := INF
	for e in enemies:
		if not e.dead:
			bd = minf(bd, U.hypot(e.x - x, e.y + e.h / 2 - y))
	return bd

func projectile_target(b: Dictionary, savior):
	var sp := U.hypot(b.vx, b.vy)
	if sp == 0: sp = 1
	for q in players:
		if q == savior or q.state == "dead" or q.state == "downed":
			continue
		var dx: float = q.x - b.x
		var dy: float = q.y + 1 - b.y
		var along: float = (dx * b.vx + dy * b.vy) / sp
		if along > 0 and along < 5 and absf(dx * b.vy - dy * b.vx) / sp < 1.4:
			return q
	return null

# ---- Barks (placeholder lines) ----
func bark(p, key: String, chance := 1.0, force := false) -> void:
	if not Tune.settings.barks or p == null or rng.next() > chance:
		return
	if not force and (p.bark_cd > 0 or global_bark_cd > 0):
		return
	var lines = BARKS.get(p.char, {}).get(key)
	if lines == null:
		return
	p.bark_cd = 300; global_bark_cd = 60
	emit("bark", { "p": p, "text": lines[rng.below(lines.size())] })

# ---- Players ----
func add_player(device: String, char_id: String) -> PlayerSim:
	var used := {}
	for p in players:
		used[p.slot] = true
	var slot := 0
	while used.has(slot):
		slot += 1
	if slot > 3:
		return null
	var act := active_players()
	var anchor = act[0] if act.size() else null
	var cp: Dictionary = Tune.L.CHECKPOINTS[checkpoint]
	var x: float = anchor.x - 0.8 if anchor != null else cp.x + slot * 0.8
	var y: float = anchor.last_safe_y if anchor != null else cp.y
	var p := PlayerSim.create(slot, device, char_id, x, y)
	p.mercy = 120
	players.append(p); players.sort_custom(func(a, b): return a.slot < b.slot)
	emit("join", { "p": p })
	return p

func remove_player(slot: int) -> void:
	players = players.filter(func(p): return p.slot != slot)
	wells = wells.filter(func(w): return players.has(w.owner))
	if ult_cast != null:
		ult_cast.members = ult_cast.members.filter(func(m): return players.has(m))
		if ult_cast.members.is_empty():
			ult_cast = null
	emit("leave", { "slot": slot })

func swap_character(p: PlayerSim, char_id: String) -> void:
	if ult_cast != null or p.state == "ult":
		return   # not in the middle of an ultimate
	if p.thrusting:
		emit("thrustOff", { "p": p })
	if p.aegis != null:
		end_aegis(p, "swap")
	if p.beam != null:
		end_beam(p, "swap")
	p.set_character(char_id)
	emit("swap", { "p": p })

func down_player(p: PlayerSim) -> void:
	if p.lock_t:
		set_lock(p, null, "downed")
	if p.aegis != null:
		end_aegis(p, "down")
	if p.beam != null:
		end_beam(p, "down")
	p.fix_revive = false; p.revive_by = null; p.revive_gain = 0
	p.hp = 0; p.charge_t = 0; p.melee_charged = false; p.dash = null; p.lash = null; p.zip = null; p.rifle_t = 0; p.dash_charge_t = 0
	p.dodge = null; p.pound = null; p.burst_t = 0; p.sub_armed = false
	p.veiled = false; p.ambush_t = 0
	p.state = "downed"; p.st = 0; p.revive = 0; p.auto_revive = 0
	if players.size() == 1:
		p.downed_t = 9999
		if p.second_wind:
			p.second_wind = false; p.auto_revive = 70; emit("downed", { "p": p, "secondWind": true })
		else:
			emit("downed", { "p": p }); start_wipe()
		return
	p.downed_t = 600
	emit("downed", { "p": p })
	if active_players().is_empty():
		start_wipe()

func bleed_out(p: PlayerSim) -> void:
	p.state = "dead"; p.respawn_t = 360
	emit("bleedOut", { "p": p })
	if active_players().is_empty():
		start_wipe()

func revive_player(p: PlayerSim, by, frac: float) -> void:
	p.state = "normal"; p.st = 0; p.hp = U.jround(p.max_hp * frac); p.mercy = 90; p.revive = 0
	p.fix_revive = false; p.revive_by = null; p.revive_gain = 0
	p.h = Tune.C.CHARS[p.char].height; p.crouch = not level.has_headroom(p.x, p.y, p.w, p.h)
	emit("revived", { "p": p, "by": by })
	if by != null:
		bark(by, "revive", 1, true); schedule(40, func(): bark(p, "revived", 1, true))

func start_wipe() -> void:
	if wipe_t <= 0:
		wipe_t = 100; emit("wipe", {})

func reset_to_checkpoint() -> void:
	var cp: Dictionary = Tune.L.CHECKPOINTS[checkpoint]
	for i in players.size():
		var p: PlayerSim = players[i]
		p.x = cp.x + i * 0.8; p.y = cp.y; p.prev_x = p.x; p.prev_y = p.y; p.vx = 0; p.vy = 0
		p.hp = p.max_hp; p.strain = 0; p.state = "normal"; p.st = 0; p.second_wind = true; p.mercy = 60
		p.h = Tune.C.CHARS[p.char].height; p.last_safe_x = p.x; p.last_safe_y = p.y; p.resolve = 0; p.charge_t = 0
		p.veiled = false; p.ambush_t = 0; p.focus = 0
		p.aegis = null; p.aegis_cd = 0; p.overcharge = 0; p.beam = null
		p.dodge = null; p.pound = null; p.burst_t = 0; p.sub_armed = false; p.ult_run = null; p.lock_suspend = false
		p.plate = 0; p.overclock_t = 0; p.tune_t = 0; p.brace_t = 0; p.fix_revive = false; p.revive_gain = 0
	projectiles = []; shockwaves = []; barriers = []; wells = []; ult_cast = null
	gadgets = []; pickups = []
	level.restore_boxes(); spawn_level_pickups()
	if arena.state != "cleared":
		reset_arena()
	if tower_spawned and enemies.any(func(e): return e.zone == "tower" and not e.dead):
		enemies = enemies.filter(func(e): return e.zone != "tower"); tower_spawned = false
	# Encounters that were not finished start over (and their gates open)
	for S in encounters:
		if S.state == "cleared":
			continue
		enemies = enemies.filter(func(e): return e.enc != S.def.id)
		S.state = "idle"; S.wave = 0
		for g in S.def.get("gates", []):
			level.gates[g] = false
	director.reset()
	emit("respawnAll", {})

func teleport(zone_id: String) -> void:
	var z = null
	for q in Tune.L.ZONES:
		if q.id == zone_id:
			z = q
	if z == null:
		return
	checkpoint = 0
	for i in Tune.L.CHECKPOINTS.size():
		var c: Dictionary = Tune.L.CHECKPOINTS[i]
		if c.x == z.spawn.x and c.y == z.spawn.y:
			checkpoint = i
			break
	if zone_id == "arena":
		arena.state = "idle"
	wipe_t = 0
	reset_to_checkpoint()
	emit("banner", { "text": z.name, "sub": "Zone loaded" })

func reset_arena() -> void:
	var boss: bool = arena.state == "boss" or arena.state == "bossReady"
	enemies = enemies.filter(func(e): return e.zone != "arena")
	level.gates.L = false; level.gates.R = false
	arena = { "state": "bossReady" if boss else "idle" }   # a wipe in the boss fight comes back to the boss

# The Concourse Lock's last wave: the Lockwarden drops in
func start_warden() -> void:
	arena.state = "boss"
	Bosses.spawn(self, "warden", 87, 12, { "zone": "arena" })   # drops in beside the dais, not onto it
	emit("banner", { "text": Tune.C.BOSS.warden.name, "sub": Tune.C.BOSS.warden.title })

# Straight to a boss fight (pause menu): the arena's boss, or the beacon's with the relay already won
func boss_rush(id: String) -> void:
	if id == "warden":
		teleport("arena"); arena.state = "bossReady"
		for p in players:
			p.x = 64.5 + p.slot * 0.8; p.prev_x = p.x
		return
	for S in encounters:
		S.state = "idle" if S.def.has("boss") else "cleared"
		for g in S.def.get("gates", []):
			level.gates[g] = false
	enemies = enemies.filter(func(e): return e.zone != "skyline")
	for i in Tune.L.CHECKPOINTS.size():
		if Tune.L.CHECKPOINTS[i].x == 302:
			checkpoint = i
	wipe_t = 0
	reset_to_checkpoint()
	emit("banner", { "text": "Skyline Relay", "sub": "The beacon pad" })

# ---- Nova: the perfect dodge ----
# An attack reached him early in a dodge: time slows for enemies close by (and their shots), and he gains
# Overcharge and ultimate charge
func perfect_dodge(p: PlayerSim) -> void:
	var D: Dictionary = Tune.C.DODGE
	var cx := p.chest_x()
	var cy := p.chest_y()
	p.dodge.perfect = true
	for e in enemies:
		if not e.dead and U.hypot(e.x - cx, e.y + e.h / 2 - cy) < D.slowRange + e.w / 2:
			e.slow_t = int(D.slowTicks) >> 1 if e.boss else int(D.slowTicks)
	for pr in projectiles:
		if pr.team == "e" and not pr.dead and U.hypot(pr.x - cx, pr.y - cy) < D.slowRange:
			pr.slowT = int(D.slowTicks)
	p.overcharge = minf(Tune.C.AEGIS.over.max, p.overcharge + D.over); p.over_t = int(Tune.C.AEGIS.over.hold)
	p.gain_ult(Tune.C.ULT.gain.perfect, self)
	emit("perfectDodge", { "p": p, "x": cx, "y": cy })
	bark(p, "perfect", 0.3)

func heal(q: PlayerSim, amount: float, from = null) -> float:
	if not (amount > 0) or q.state == "dead" or q.state == "downed":
		return 0.0
	var before := q.hp
	q.hp = minf(q.max_hp, q.hp + amount)
	if q.strain:
		q.strain = maxf(0, minf(q.strain, q.max_hp - q.hp))
	var healed := q.hp - before
	if from != null and from != q and healed > 0:
		from.gain_ult(healed * Tune.C.ULT.gain.heal, self)
	return healed

# ---- Power-ups ----
func update_pickups() -> void:
	var P: Dictionary = Tune.C.FIX.power
	var DT := Tune.DT
	for k in pickups:
		k.px = k.x; k.py = k.y; k.t += 1
		if k.target != null and (k.target.state == "dead" or k.target.state == "downed" or not players.has(k.target)):
			k.target = null
		if k.target != null:
			# a pass that speeds up into their hands
			var q = k.target
			var dx: float = q.x - k.x
			var dy: float = q.y + q.h * 0.55 - k.y
			var d := U.hypot(dx, dy)
			if d == 0: d = 1
			var sp := minf(26, P.speed + k.t * 0.5)
			k.vx += (dx / d * sp - k.vx) * 0.25; k.vy += (dy / d * sp - k.vy) * 0.25
			k.x += k.vx * DT; k.y += k.vy * DT
		elif not k.rest:
			k.vy -= P.gravity * DT
			var nx: float = k.x + k.vx * DT
			var ny: float = k.y + k.vy * DT
			if level.point_in_solid(nx, k.y):
				k.vx *= -0.3
			else:
				k.x = nx
			var g := level.ground_below(k.x, k.y + 0.05)
			if k.vy <= 0 and g > -INF and ny - 0.18 <= g:
				k.y = g + 0.18; k.vy = 0.0; k.vx = 0.0; k.rest = true
			else:
				k.y = ny
			if k.y < Level.kill_y_at(k.x):
				k.dead = true
		for q in players:
			if k.dead or q.state == "dead" or q.state == "downed" or (q == k.owner and k.t < P.ownerDelay):
				continue
			if absf(q.x - k.x) < q.w / 2 + P.grab * 0.5 and k.y > q.y - 0.4 and k.y < q.y + q.h + 0.4:
				apply_power(q, k.kind, k.owner); k.dead = true
		if not k.dead:
			k.life -= 1
			if k.life <= 0:
				k.dead = true; emit("powerFade", { "x": k.x, "y": k.y, "kind": k.kind })
	pickups = pickups.filter(func(k): return not k.dead)

func apply_power(q: PlayerSim, kind: String, from) -> void:
	var P: Dictionary = Tune.C.FIX.power
	if kind == "overclock":
		q.overclock_t = maxi(q.overclock_t, int(P.overclock.ticks))
	elif kind == "plating":
		q.add_plate(P.plating.plate)
	elif kind == "ultcell":
		q.gain_ult(Tune.C.POWERUPS.ultcell.ult, self)
	elif kind == "fury":
		q.fury_t = maxi(q.fury_t, int(Tune.C.POWERUPS.fury.ticks))
	else:
		heal(q, P.medkit.heal, from)
	emit("powerUp", { "p": q, "kind": kind, "from": from })

# Lift pads (level.json LIFTS): a player coming down onto one is thrown up to the platform over it
func lift_tick() -> void:
	for L in Tune.L.LIFTS:
		var x: float = L[0]
		var y: float = L[1]
		var top: float = L[2]
		for q in players:
			if q.pad_cd > 0 or not q.state in ["normal", "guard", "patch", "attack"] or q.vy > 0.5:
				continue
			if absf(q.x - x) > 0.8 + q.w / 2 or q.y < y - 0.05 or q.y > y + 0.45:
				continue
			q.vy = sqrt(2 * Tune.C.GRAVITY * (top - y + 1.6)); q.on_ground = false; q.coyote = 0; q.jumps_used = 0; q.air_dashes = 1; q.air_rise = true
			q.fast_fall = false; q.dash_carry = true; q.pad_cd = 30   # (dash_carry: the full rise, as off Fix's spring pad)
			emit("liftBounce", { "p": q, "x": x, "y": y, "top": top })

# Power-ups along the routes: they wait where they are until someone takes them
func spawn_level_pickups() -> void:
	pickups = pickups.filter(func(k): return not k.level)
	for k in Tune.L.LEVEL_PICKUPS:
		add_level_pickup(k[0], k[1] + 0.18, k[2], true)

func add_level_pickup(x: float, y: float, kind: String, rest := false, vy := 0.0) -> Dictionary:
	var k := { "id": new_instance(), "kind": kind, "owner": null, "x": x, "y": y, "px": x, "py": y, "vx": 0.0, "vy": vy, "target": null, "t": 0,
		"life": 1e9 if rest else Tune.C.POWERUPS.dropLife, "rest": rest, "dead": false, "level": true }
	pickups.append(k)
	return k

# ---- Breakable pieces ----
# Damage to a breakable piece: a pillar ignores blows under its `min`; at 0 it breaks (it stops being solid,
# anything on it falls) and drops its power-up, if it holds one
func damage_box(b: Level.Box, dmg: float, x: float, y: float, by = null) -> bool:
	if b == null or b.broken or b.type != "d":
		return false
	var D: Dictionary = Tune.L.DESTRUCT[b.tag]
	if dmg < D.min:
		emit("boxChip", { "b": b, "x": x, "y": y, "hard": true })
		return false
	b.hp -= dmg; b.hit_t = tick
	if b.hp > 0:
		emit("boxChip", { "b": b, "x": x, "y": y })
		return false
	b.broken = true
	emit("boxBreak", { "b": b, "x": (b.x0 + b.x1) / 2, "y": (b.y0 + b.y1) / 2, "by": by })
	if b.loot != null:
		add_level_pickup((b.x0 + b.x1) / 2, (b.y0 + b.y1) / 2, b.loot, false, 5)
	return true

# Every breakable piece touching a box (a strike), once per attack instance
func strike_boxes(hb: Dictionary) -> void:
	var set = hit_sets.get(hb.instance)
	if set == null:
		set = {}
		hit_sets[hb.instance] = set
	for b in level.near(hb.x0, hb.x1):
		if b.type != "d" or b.broken or set.has("b%d" % b.id):
			continue
		if hb.x0 < b.x1 and hb.x1 > b.x0 and hb.y0 < b.y1 and hb.y1 > b.y0:
			set["b%d" % b.id] = true
			var dmg: float = (hb.dmg if hb.get("dmg") else 1.0) * (2.0 if hb.get("heavy") or hb.get("armorBreak") else 1.0) + (6.0 if hb.get("ram") else 0.0)
			damage_box(b, dmg, (b.x0 + b.x1) / 2, minf(b.y1, maxf(b.y0, (hb.y0 + hb.y1) / 2)), hb.get("owner"))

# A blast: every breakable piece within r (more damage the closer)
func blast_boxes(x: float, y: float, r: float, dmg: float, by) -> void:
	for b in level.near(x - r, x + r):
		if b.type != "d" or b.broken:
			continue
		var nx := maxf(b.x0, minf(x, b.x1))
		var ny := maxf(b.y0, minf(y, b.y1))
		var d := U.hypot(x - nx, y - ny)
		if d <= r:
			damage_box(b, dmg * (1.5 - 0.5 * d / maxf(0.1, r)), nx, ny, by)

# An enemy fell (Fix picks up Scrap from it if she is close: M2)
func on_kill(_e, _owner) -> void:
	pass

# ---- Ultimates (ULT) ----
# A full bar and both triggers: the call. The world freezes for ULT.cast ticks while the caster powers up;
# teammates with a full bar can pull both triggers to join (each join keeps the call open ULT.join more).
func start_ult(p: PlayerSim) -> void:
	p.ult = 0; p.chord_p = 99; p.chord_f = 99
	enter_ult(p)
	ult_cast = { "members": [p], "phase": "cast", "t": 0, "len": Tune.C.ULT.cast, "name": Tune.C.ULT[p.char].name, "team": false, "power": 1.0 }
	# Nothing is left mid-motion to smear while everything holds still
	for q in players + enemies:
		q.prev_x = q.x; q.prev_y = q.y
	for pr in projectiles:
		pr.px = pr.x; pr.py = pr.y
	for w in wells:
		w.px = w.x; w.py = w.y
	emit("ultCast", { "p": p, "name": ult_cast.name, "x": p.x, "y": p.y + p.h * 0.6 })

func enter_ult(p: PlayerSim) -> void:
	if p.beam != null:
		end_beam(p, "ult")
	if p.thrusting:
		p.thrusting = false; emit("thrustOff", { "p": p })
	p.state = "ult"; p.st = 0; p.ult_run = null; p.dash = null; p.dodge = null; p.pound = null; p.lash = null; p.zip = null; p.slash = null
	p.charge_t = 0; p.burst_t = 0; p.sub_armed = false; p.rifle_t = 0; p.dash_charge_t = 0; p.melee_charged = false; p.crouch = false; p.hitstop = 0; p.wall_sliding = false

func ult_cast_tick(cmds: Dictionary) -> void:
	var Ux = ult_cast
	Ux.t += 1
	for q in players:
		if Ux.members.has(q) or q.state == "dead" or q.state == "downed":
			continue
		var cmd: Cmd = cmds.get(q.slot, Cmd.empty())
		q.track_chord(cmd)
		if q.ult >= Tune.C.ULT.max and q.chord_ready(cmd):
			q.ult = 0; q.chord_p = 99; q.chord_f = 99; q.prev_x = q.x; q.prev_y = q.y
			enter_ult(q); Ux.members.append(q); Ux.len = maxf(Ux.len, Ux.t + Tune.C.ULT.join)
			emit("ultJoin", { "p": q, "n": Ux.members.size() })
	if Ux.t >= Ux.len:
		run_ult()

func run_ult() -> void:
	var Ux = ult_cast
	Ux.phase = "run"; Ux.t = 0
	Ux.team = Ux.members.size() > 1; Ux.power = Tune.C.ULT.team.power if Ux.team else 1.0
	if Ux.team:
		if Ux.members.size() > 2:
			Ux.name = Tune.C.ULT.teamAll
		else:
			var names: Array = Ux.members.map(func(m): return m.char)
			names.sort()
			Ux.name = Tune.C.ULT.teamNames.get("+".join(names), Tune.C.ULT.teamAll)
	for m in Ux.members:
		begin_ult(m, Ux.power)
	emit("ultRun", { "members": Ux.members.duplicate(), "team": Ux.team, "name": Ux.name })

func begin_ult(p: PlayerSim, power: float) -> void:
	# Supernova: it opens toward the lock-on target if he has one. (Echo's, RAM's and Fix's: M2.)
	var dx := p.aim_x
	var dy := p.aim_y
	if p.lock_t and not p.lock_t.dead:
		var ex: float = p.lock_t.x - p.chest_x()
		var ey: float = p.lock_t.y + p.lock_t.h * 0.55 - p.chest_y()
		var m := U.hypot(ex, ey)
		if m == 0: m = 1
		dx = ex / m; dy = ey / m
	p.ult_run = { "kind": "nova", "t": 0, "power": power, "dx": dx, "dy": dy, "pulse": 0, "segs": null }
	emit("ultBegin", { "p": p, "kind": p.ult_run.kind })

# Ultimate damage: breaks armor, reduced on bosses, and never charges anyone's ultimate
func ult_hit(p: PlayerSim, e, dmg: float, poise: float, kx := 0.0) -> String:
	return Combat.hit_enemy(self, e, { "owner": p, "dmg": dmg * (Tune.C.ULT.boss if e.boss else 1.0), "poise": poise, "kb": [kx, 3.0], "armorBreak": true, "ult": true }, "blast")

# Each member's ultimate, a tick at a time (called from their 'ult' state)
func ult_step(p: PlayerSim, _cmd: Cmd) -> void:
	var R = p.ult_run
	if R == null:
		p.vx = 0; p.vy = -0.5 if p.on_ground else 0.0
		return
	R.t += 1
	ult_nova(p, R)

func ult_nova(p: PlayerSim, R: Dictionary) -> void:
	var N: Dictionary = Tune.C.ULT.nova
	var t: int = R.t
	var cx := p.chest_x()
	var cy := p.chest_y()
	# He rises into a hover while the light gathers, then holds there
	p.vx *= 0.7; p.vy = N.rise * 10 * (1 - t / 12.0) if t <= 12 else 0.0
	if t > N.gather and t <= N.gather + N.beam:
		# The beam turns toward his aim, runs N.range m through everything, and pulses every N.pulse ticks
		var a0 := atan2(R.dy, R.dx)
		var da := U.wrap_angle(atan2(p.aim_y, p.aim_x) - a0)
		var a: float = a0 + clampf(da, -N.turn, N.turn)
		R.dx = cos(a); R.dy = sin(a)
		if absf(R.dx) > 0.2:
			p.facing = U.sgn(R.dx)
		var g := { "x0": cx + R.dx * 0.6, "y0": cy + R.dy * 0.6, "x1": cx + R.dx * N.range, "y1": cy + R.dy * N.range }
		R.segs = [g]
		for pr in projectiles:
			if pr.team == "e" and not pr.dead and dist_to_seg(pr.x, pr.y, g) < N.width + pr.r:
				pr.dead = true; emit("erase", { "x": pr.x, "y": pr.y })
		if (t - int(N.gather)) % int(N.pulse) == 1:
			R.pulse += 1
			for e in enemies:
				if not e.dead and seg_hits_box(g, Combat.hurtbox(e), N.width):
					ult_hit(p, e, N.dmg * R.power, 30, U.sgn(R.dx) * 2)
	else:
		R.segs = null
	if t == N.gather + N.beam + 6:
		# The nova: a burst of light from where he hangs
		var B: Dictionary = N.nova
		var r: float = B.r * (1.15 if R.power > 1 else 1.0)
		for e in enemies:
			if e.dead:
				continue
			var nx := maxf(e.x - e.w / 2, minf(cx, e.x + e.w / 2))
			var ny := maxf(e.y, minf(cy, e.y + e.h))
			if U.hypot(nx - cx, ny - cy) <= r:
				ult_hit(p, e, B.dmg * R.power, B.poise, U.sgn_or(e.x - cx, 1) * 9)
		emit("ultNova", { "p": p, "x": cx, "y": cy, "r": r })
	if t >= N.end:
		finish_ult(p)

func finish_ult(p: PlayerSim) -> void:
	p.ult_run = null; p.state = "normal"; p.st = 0; p.mercy = maxi(p.mercy, int(Tune.C.ULT.mercy)); p.vy = minf(p.vy, 0)
	emit("ultEnd", { "p": p })

# The run (and a team ultimate's finisher) after every member has finished
func ult_tick() -> void:
	var Ux = ult_cast
	Ux.members = Ux.members.filter(func(m): return players.has(m))
	Ux.t += 1
	if Ux.phase == "run":
		if Ux.members.any(func(m): return m.ult_run != null):
			return
		if Ux.team and Ux.members.size():
			Ux.phase = "finish"; Ux.t = 0
		else:
			ult_cast = null
	elif Ux.phase == "finish":
		if Ux.t == 8:
			# The team finisher: every enemy on screen
			var n: int = Ux.members.size()
			var lead: PlayerSim = Ux.members[0]
			for e in enemies:
				if e.dead or absf(e.x - cam.x) > cam.halfW + 2 or absf(e.y + e.h / 2 - cam.y) > cam.halfH + 2:
					continue
				ult_hit(lead, e, Tune.C.ULT.team.dmg * n, 120, U.sgn_or(e.x - cam.x, 1) * 10)
			emit("teamFinisher", { "name": Ux.name, "members": Ux.members.duplicate(), "x": cam.x, "y": cam.y, "chars": Ux.members.map(func(m): return m.char) })
		if Ux.t >= Tune.C.ULT.team.t:
			ult_cast = null

func spawn_gym() -> void:
	enemies.append(EnemySim.create("post", 54.5, 0, { "zone": "gym" }, self))
	enemies.append(EnemySim.create("turret", 58, 3.05, { "zone": "gym", "facing": -1 }, self))

# ---- The tick ----
func step(cmds: Dictionary) -> void:
	tick += 1
	# An ultimate being called: the world holds still, and only teammates joining in are listened to
	if ult_cast != null and ult_cast.phase == "cast":
		ult_cast_tick(cmds)
		return
	if global_bark_cd > 0:
		global_bark_cd -= 1
	var due := scheduled.filter(func(s): return s.t <= tick)
	scheduled = scheduled.filter(func(s): return s.t > tick)
	for s in due:
		s.fn.call()
	# While an ultimate plays out, enemies, their shots and shockwaves stay frozen
	var frozen := ult_cast != null

	for p in players:
		if p.bark_cd > 0:
			p.bark_cd -= 1
		if p.state == "dead":
			tick_dead(p)
			continue
		p.update(cmds.get(p.slot, Cmd.empty()), self)
	for e in enemies:
		if frozen and not e.dead:
			e.prev_x = e.x; e.prev_y = e.y
			if e.flash > 0:
				e.flash -= 1
			continue
		e.update(self)
	if not frozen:
		Combat.update_shockwaves(self)
	Combat.update_projectiles(self, frozen)
	update_wells(frozen)
	update_pickups()
	lift_tick()
	Combat.resolve_hitboxes(self)
	if ult_cast != null:
		ult_tick()
	for b in barriers:
		b.ttl -= 1
	barriers = barriers.filter(func(b): return b.ttl > 0)

	tick_revives()
	update_camera()
	update_encounters()

	enemies = enemies.filter(func(e): return not (e.dead and e.death_t > (84 if e.boss else 45)))   # a boss stays for its explosions
	if tick % 120 == 0:
		var keep := instance_seq - 400
		for k in hit_sets.keys():
			if k < keep:
				hit_sets.erase(k)
	if wipe_t > 0:
		wipe_t -= 1
		if wipe_t == 0:
			reset_to_checkpoint()

func tick_dead(p: PlayerSim) -> void:
	p.prev_x = p.x; p.prev_y = p.y
	if wipe_t > 0:
		return
	p.respawn_t -= 1
	if p.respawn_t <= 0:
		var act := active_players()
		if act.is_empty():
			return
		var ally: PlayerSim = act[0]
		p.x = ally.last_safe_x; p.y = ally.last_safe_y; p.prev_x = p.x; p.prev_y = p.y; p.vx = 0; p.vy = 0
		p.state = "normal"; p.st = 0; p.hp = U.jround(p.max_hp * 0.3); p.mercy = 120; p.h = Tune.C.CHARS[p.char].height
		emit("respawn", { "p": p })

# Reviving: everyone standing beside a downed teammate adds to it (Fix counts more: M2)
func tick_revives() -> void:
	for p in players:
		if p.state != "downed":
			p.revive_gain = 0
			continue
		if p.auto_revive > 0:
			p.auto_revive -= 1
			if p.auto_revive == 0:
				revive_player(p, null, 0.4)
			continue
		var gain: float = p.revive_gain
		var by = p.revive_by
		for q in players:
			if q == p or q.state == "downed" or q.state == "dead" or q.state == "hitstun" or absf(q.x - p.x) >= 1.7 or absf(q.y - p.y) >= 1.6:
				continue
			gain += 1
			if by == null:
				by = q
		p.revive_gain = 0
		if gain > 0:
			p.revive += gain
			if p.revive >= 120:
				revive_player(p, by, 0.4)
		else:
			p.revive = maxf(0, p.revive - 0.5)

func update_camera() -> void:
	var act := players.filter(func(p): return p.state != "dead")
	if act.is_empty():
		return
	var x0 := INF
	var x1 := -INF
	var y0 := INF
	var y1 := -INF
	for p in act:
		x0 = minf(x0, p.x); x1 = maxf(x1, p.x); y0 = minf(y0, p.y); y1 = maxf(y1, p.y + p.h)
		# Lining up a rocket jump, the camera eases back to show how high it will go, and it keeps the peak in
		# view on the way up, so the frame never has to chase the climb
		if p.charge_t > 0:
			var pv = rocket_preview(p)
			if pv != null:
				y1 = maxf(y1, pv.apex + 0.6)
		if p.rocket_t > 0 and p.vy > 0:
			y1 = maxf(y1, p.y + apex_gain(p.vy) * 0.85 + p.h * 0.5); y0 = minf(y0, p.y - 2)
	# A boss in the fight stays in the frame
	var mid := (x0 + x1) / 2
	for e in enemies:
		if not e.boss or e.dead or absf(e.x - mid) > 24:
			continue
		x0 = minf(x0, e.x - e.w / 2); x1 = maxf(x1, e.x + e.w / 2); y0 = minf(y0, e.y); y1 = maxf(y1, minf(e.y + e.h, y0 + 16))
	var tan_h := tan(deg_to_rad(float(Tune.settings.fov)) / 2)
	var need_w := (x1 - x0) + 9
	var need_h := (y1 - y0) + 7
	var dist := maxf(need_h / 2 / tan_h, need_w / 2 / (tan_h * aspect))
	dist = clampf(dist, 13, 32)
	var half_h := dist * tan_h
	var half_w := half_h * aspect
	cam = { "x": (x0 + x1) / 2 + 0.8, "y": (y0 + y1) / 2 + 1.0, "dist": dist, "halfW": half_w, "halfH": half_h }
	# Spread limit and recall (co-op). Being left behind is never a damage penalty.
	var multi := act.size() > 1
	for p in players:
		if p.state == "dead":
			continue
		if p.y < Level.kill_y_at(p.x):
			recall(p, true)
			continue
		if not multi:
			continue
		var right: float = cam.x + half_w - 0.7
		if p.x > right:
			p.x = right
			if p.vx > 0:
				p.vx = 0
		var off: bool = p.x < cam.x - half_w - 0.5 or p.y + p.h < cam.y - half_h - 1
		p.offscreen_t = p.offscreen_t + 1 if off else 0
		if p.offscreen_t > 90 and p.state != "downed":
			recall(p, false)

func recall(p: PlayerSim, pit: bool) -> void:
	var allies := active_players().filter(func(q): return q != p)
	allies.sort_custom(func(m, n): return absf(m.x - p.x) < absf(n.x - p.x))
	var tx: float = allies[0].last_safe_x if allies.size() else p.last_safe_x
	var ty: float = allies[0].last_safe_y if allies.size() else p.last_safe_y
	p.x = tx; p.y = ty; p.prev_x = tx; p.prev_y = ty; p.vx = 0; p.vy = 0; p.offscreen_t = 0
	p.mercy = 90
	# An ultimate under way ends here (left running out of its state it would never finish, and the world
	# would stay frozen); one still being called carries on from the new spot
	if p.state == "ult":
		if p.ult_run != null:
			finish_ult(p)
	elif p.state != "downed":
		p.state = "normal"; p.st = 0
	emit("recall", { "p": p, "pit": pit })
	if pit and p.state != "downed":
		p.hp -= 10
		if p.hp <= 0:
			down_player(p)

func update_encounters() -> void:
	# Checkpoints
	var CPS: Array = Tune.L.CHECKPOINTS
	for i in range(checkpoint + 1, CPS.size()):
		var cp: Dictionary = CPS[i]
		if players.any(func(p): return p.state != "dead" and p.x >= cp.x - 0.5 and p.y >= cp.y - 0.5 and p.on_ground):
			if i == 1 or arena.state == "cleared" or i > 2:
				checkpoint = i; emit("checkpoint", { "i": i })
	# Concourse Lock
	var n := maxi(1, players.size())
	var A := arena
	var at_x: float = Tune.L.ARENA_TRIGGER_X
	if A.state == "idle" and players.any(func(p): return p.state != "dead" and p.x > at_x and p.x < 96):
		level.gates.L = true; level.gates.R = true; A.state = "wave1"
		for p in players:
			if p.x < 63:
				p.x = 64 + p.slot * 0.8; p.y = 0; p.vx = 0; p.vy = 0
		var sp := [EnemySim.create("shield", 88, 0, {}, self), EnemySim.create("shield", 92, 0, {}, self), EnemySim.create("sniper", 94.1, 5.4, {}, self)]
		if n >= 3:
			sp.append(EnemySim.create("shield", 71, 0, {}, self)); sp.append(EnemySim.create("sniper", 64.9, 5.4, { "facing": 1 }, self))
		for e in sp:
			e.zone = "arena"; enemies.append(e)
		emit("banner", { "text": "Concourse Lock", "sub": "Gate sealed. Break the lock." })
		emit("gates", { "closed": true })
	elif A.state == "wave1":
		var alive := enemies.filter(func(e): return e.zone == "arena" and not e.dead).size()
		if alive <= 1:
			A.state = "wave2"
			var count := 3 if n == 1 else (4 if n == 2 else 6)
			for i in count:
				var e := EnemySim.create("swarmer", 66 if i % 2 else 94, 0, {}, self)
				e.zone = "arena"; e.cd = 20 + i * 12; enemies.append(e)
			var b := EnemySim.create("brute", 90, 0, {}, self)
			b.zone = "arena"; enemies.append(b)
			emit("banner", { "text": "Wave 2", "sub": "The Brute holds the lock." })
	elif A.state == "wave2":
		if not enemies.any(func(e): return e.zone == "arena" and not e.dead):
			start_warden()
	elif A.state == "bossReady":
		# After a wipe in the boss fight, walking back in goes straight to the boss
		if players.any(func(p): return p.state != "dead" and p.x > at_x and p.x < 96):
			level.gates.L = true; level.gates.R = true; emit("gates", { "closed": true })
			for p in players:
				if p.x < 63:
					p.x = 64 + p.slot * 0.8; p.y = 0; p.vx = 0; p.vy = 0
			start_warden()
	elif A.state == "boss":
		if not enemies.any(func(e): return e.zone == "arena" and not e.dead):
			A.state = "cleared"; level.gates.L = false; level.gates.R = false
			emit("banner", { "text": "Lockwarden destroyed", "sub": "Gates open. Storm Spire climb ahead." })
			emit("gates", { "closed": false })
			var act := active_players()
			if act.size():
				bark(act[rng.below(act.size())], "lock_broken", 1, true)
	update_skyline(n)
	# Storm Spire climb enemies
	var tt: float = Tune.L.TOWER_TRIGGER_X
	if not tower_spawned and players.any(func(p): return p.x > tt and p.x < 162):
		tower_spawned = true
		for s in [["swarmer", 122, 5.2], ["swarmer", 134, 11.2], ["drone", 129, 12], ["shield", 152, 15.6], ["drone", 147, 19.5], ["sniper", 158, 15.6]]:
			var e := EnemySim.create(s[0], s[1], s[2], {}, self)
			e.zone = "tower"; enemies.append(e)

# The data-driven encounters (level.json ENCOUNTERS): the Skyline Relay and the Version 12 routes
func _here(x0: float, route: String) -> bool:
	return players.any(func(p): return p.state != "dead" and p.state != "downed" and p.x > x0 and Level.route_at(p.x).id == route)

func update_skyline(n: int) -> void:
	# (a trigger counts only for players on that encounter's route: the routes share one long x axis)
	for S in encounters:
		var E: Dictionary = S.def
		if S.state == "idle":
			if not _here(E.trigger, E.route):
				continue
			S.state = "active"; S.wave = 0
			if E.has("boss"):
				S.boss = Bosses.spawn(self, E.boss, E.bossAt[0], E.bossAt[1], { "zone": "skyline", "enc": E.id })
			else:
				spawn_wave(S, n)
			if E.has("gates"):
				for g in E.gates:
					level.gates[g] = true
				# Anyone still outside the gate is brought in, as in the Concourse Lock
				for p in players:
					if p.x < E.inside - 1:
						p.x = E.inside + p.slot * 0.8; p.y = level.ground_below(p.x, 40); p.vx = 0; p.vy = 0; p.prev_x = p.x; p.prev_y = p.y
				emit("gates", { "closed": true })
			emit("banner", { "text": E.banner[0], "sub": E.banner[1] })
		elif S.state == "active":
			if E.has("boss") and S.boss != null and S.boss.dead:
				# The boss is down: its drones go with it
				for e in enemies:
					if e.enc == E.id and not e.dead and e.add:
						e.hp = 0; e.dead = true; e.death_t = 0; emit("kill", { "x": e.x, "y": e.y + e.h / 2, "e": e, "owner": null })
			var alive := enemies.filter(func(e): return e.enc == E.id and not e.dead).size()
			var last: bool = true if E.has("boss") else S.wave >= E.waves.size() - 1
			if not last and alive <= 1:
				S.wave += 1; spawn_wave(S, n)
				var wb = E.get("waveBanners")
				if wb != null and wb[S.wave] != null:
					emit("banner", { "text": wb[S.wave][0], "sub": wb[S.wave][1] })
			elif last and alive == 0:
				S.state = "cleared"
				if E.has("boss"):
					boss_cleared_t = tick
				if E.has("gates"):
					for g in E.gates:
						level.gates[g] = false
					emit("gates", { "closed": false })
				if E.has("cleared"):
					emit("banner", { "text": E.cleared[0], "sub": E.cleared[1] })
					var act := active_players()
					if act.size():
						bark(act[rng.below(act.size())], "lock_broken", 1, true)
	# A route completes once every encounter on it is won and someone reaches its end (a few seconds after a
	# boss falls, so the banners don't collide)
	for R in Tune.L.ROUTES:
		if routes_done.get(R.id) or not _here(R.endX, R.id) or tick - boss_cleared_t <= 150:
			continue
		if not encounters.all(func(S): return S.def.route != R.id or S.state == "cleared"):
			continue
		routes_done[R.id] = true
		if R.id == "skyport":
			route_done = true
		emit("banner", { "text": "Route complete", "sub": "You reached the end of the Skyport route." if R.id == "skyport" else "%s cleared." % R.name })

func spawn_wave(S: Dictionary, n: int) -> void:
	var E: Dictionary = S.def
	var list: Array = E.waves[S.wave].duplicate()
	if S.wave == 0 and n >= 3 and E.has("extra"):
		list.append_array(E.extra)
	for i in list.size():
		var s: Array = list[i]
		enemies.append(EnemySim.create(s[0], s[1], s[2], { "zone": "skyline", "enc": E.id, "cd": 40 + i * 14 }, self))

# Distance from a point to a beam segment, and whether a thick segment touches a box
static func dist_to_seg(x: float, y: float, g: Dictionary) -> float:
	var vx: float = g.x1 - g.x0
	var vy: float = g.y1 - g.y0
	var L := vx * vx + vy * vy
	var t := clampf(((x - g.x0) * vx + (y - g.y0) * vy) / L, 0, 1) if L > 1e-9 else 0.0
	return U.hypot(x - (g.x0 + vx * t), y - (g.y0 + vy * t))

static func seg_hits_box(g: Dictionary, b: Dictionary, w: float) -> bool:
	var vx: float = g.x1 - g.x0
	var vy: float = g.y1 - g.y0
	var L := U.hypot(vx, vy)
	if L < 1e-6:
		return false
	var hh := Level.ray_box_t(g.x0, g.y0, vx / L, vy / L, b.x0 - w, b.y0 - w, b.x1 + w, b.y1 + w)
	return not hh.is_empty() and hh.t <= L

# The attack tokens: how many enemies may commit to a melee or ranged attack at once (more with more players)
class Director:
	var world
	var used := { "melee": {}, "ranged": {} }
	func _init(w) -> void:
		world = w
	func cap(pool: String) -> int:
		var n := maxi(1, world.players.filter(func(p): return p.state != "dead").size())
		var d: int = int(Tune.C.DIFFICULTY.get(Tune.settings.difficulty, Tune.C.DIFFICULTY.normal).tokens)
		return maxi(1, 2 + (n - 1) + d) if pool == "melee" else (2 if n >= 3 else 1)   # (Echo's Flare adds one: M2)
	func request(e, pool: String) -> bool:
		if e.token != null:
			return true
		if used[pool].size() < cap(pool):
			used[pool][e] = true; e.token = pool
			return true
		return false
	func release(e) -> void:
		if e.token != null:
			used[e.token].erase(e); e.token = null
	func reset() -> void:
		used.melee.clear(); used.ranged.clear()
