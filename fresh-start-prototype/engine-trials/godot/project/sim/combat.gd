# Combat resolution: melee hitboxes, projectiles, barriers, shockwaves, damage and parries. A port of
# combat.js. Hitboxes, hits and projectiles are Dictionaries with the prototype's keys (dmg, poise, kb,
# armorBreak, ...); optional keys are read with get(), where the prototype relied on `undefined` being falsy.
class_name Combat
extends RefCounted

static func hurtbox(ent) -> Dictionary:
	return { "x0": ent.x - ent.w / 2, "x1": ent.x + ent.w / 2, "y0": ent.y, "y1": ent.y + ent.h }

static func overlap(a: Dictionary, b: Dictionary) -> bool:
	return a.x0 < b.x1 and a.x1 > b.x0 and a.y0 < b.y1 and a.y1 > b.y0

static func overlap_ent(a: Dictionary, ent) -> bool:
	return a.x0 < ent.x + ent.w / 2 and a.x1 > ent.x - ent.w / 2 and a.y0 < ent.y + ent.h and a.y1 > ent.y

static func circle_ent(c: Dictionary, ent) -> bool:
	var x0: float = ent.x - ent.w / 2
	var x1: float = ent.x + ent.w / 2
	var nx := maxf(x0, minf(c.x, x1))
	var ny := maxf(ent.y, minf(c.y, ent.y + ent.h))
	return (c.x - nx) * (c.x - nx) + (c.y - ny) * (c.y - ny) < c.r * c.r

static func crosses_barrier(b: Dictionary, x0: float, y0: float, x1: float, y1: float) -> bool:
	var s0: float = (x0 - b.x) * b.nx + (y0 - b.y) * b.ny
	var s1: float = (x1 - b.x) * b.nx + (y1 - b.y) * b.ny
	if (s0 > 0) == (s1 > 0):
		return false
	var t := s0 / (s0 - s1)
	var cx := x0 + (x1 - x0) * t
	var cy := y0 + (y1 - y0) * t
	return absf((cx - b.x) * -b.ny + (cy - b.y) * b.nx) <= b.half

static func _hit_set(world, inst) -> Dictionary:
	var s = world.hit_sets.get(inst)
	if s == null:
		s = {}
		world.hit_sets[inst] = s
	return s

static func resolve_hitboxes(world) -> void:
	for hb in world.hitboxes:
		world.strike_boxes(hb)   # breakable pieces in reach (either side's strikes)
		var set := _hit_set(world, hb.instance)
		if hb.team == "p":
			for e in world.enemies:
				if e.dead or set.has(e.id) or not overlap_ent(hb, e):
					continue
				set[e.id] = true
				# Radial hits (landing shockwaves, the Aegis bursts, ground pounds) push each enemy away from
				# their centre. Bursts and pound shockwaves count as blasts (a shield cannot stop them); a pound
				# that lands a hit still counts as a connected strike for its recovery.
				var hit: Dictionary = hb
				if hb.get("radial"):
					hit = hb.duplicate()
					hit.kb = [U.sgn_or(e.x - hb.cx, 1) * absf(hb.kb[0]), hb.kb[1]]
				var src := "blast" if hb.get("aegisBurst") or hb.get("scatter") or hb.get("quake") else "melee"
				var res := hit_enemy(world, e, hit, src)
				if hb.get("scatter") and hb.owner and (res == "hit" or res == "kill"):
					hb.owner.hit_confirm = true
			# (Fix's wrench on her gadgets: M2)
		else:
			for p in world.players:
				if p.state == "dead" or p.state == "downed" or set.has("p%d" % p.slot):
					continue
				if not overlap_ent(hb, p):
					continue
				set["p%d" % p.slot] = true
				# (a Bulwark Wall between the striker and the player takes the blow instead: M2)
				hit_player(world, p, hb)
	world.hitboxes.clear()

# ---- Enemies taking hits ---------------------------------------------------------------

static func hit_enemy(world, e, hit: Dictionary, source: String) -> String:
	if e.dead:
		return "none"
	var owner = hit.get("owner")
	var cx: float = e.x
	var cy: float = e.y + e.h * 0.55
	# A boss arriving or roaring into its second phase shrugs everything off
	if e.invuln > 0:
		world.emit("blocked", { "x": cx, "y": cy, "e": e })
		return "blocked"
	# Veil ambush (Echo, M2): the first melee hit after striking from hiding breaks guard and armor
	var is_player: bool = owner != null and owner.kind == "player"
	var ambush: bool = source == "melee" and is_player and owner.ambush_t > 0
	if ambush:
		owner.ambush_t = 0

	# Arc blasts come over the top of the shield, so only direct hits are checked against it (and a shield
	# that is staggered, or stunned by Echo's spin, is down)
	if e.type == "shield" and e.state != "stagger" and not (e.state == "hitstun" and e.dizzy) and source != "blast":
		var from_front: bool
		if source == "proj":
			var hvx: float = hit.get("vx", 0.0)
			from_front = U.sgn(-hvx) == e.shield_dir or absf(hvx) < 1e-3
		else:
			from_front = U.sgn(owner.x - e.x) == e.shield_dir
		var breaks: bool = hit.get("armorBreak", false) or hit.get("bulwark", false) or hit.get("vbTier", 0) >= 2 or hit.get("rail", false) \
			or hit.get("amplified", false) or hit.get("ram", false) or ambush
		if from_front and not breaks:
			e.poise += hit.get("poise", 10) * 0.35 if hit.get("poise", 0) else 10 * 0.35
			world.emit("blocked", { "x": cx + e.shield_dir * 0.5, "y": cy, "e": e })
			if source == "melee" and owner:
				owner.vx = -owner.facing * 3; owner.hitstop = 3
			if e.poise >= e.poise_max:
				stagger(world, e, 90)
			return "blocked"
		if from_front and breaks:
			world.emit("guardBreak", { "x": cx, "y": cy, "e": e }); stagger(world, e, 90)

	var fury: bool = is_player and owner.fury_t > 0
	if fury and hit.get("kb") != null and not hit.get("furied"):
		hit = hit.duplicate(); hit.furied = true; hit.kb = [hit.kb[0] * Tune.C.POWERUPS.fury.kb, hit.kb[1]]
	var dmg: float = hit.get("dmg", 0.0) * (Tune.C.SCARF.ambushDmg if ambush else 1.0) * (Tune.C.POWERUPS.fury.dmg if fury else 1.0)
	var poise: float = hit.get("poise", 0.0)
	var armored: bool = e.armor > 0
	if armored:
		if hit.get("armorBreak") or ambush:
			e.armor -= 1; armored = e.armor > 0; dmg *= 0.6
			world.emit("armorBreak", { "x": cx, "y": cy, "e": e, "left": e.armor, "owner": owner })
		else:
			dmg *= 0.3; poise *= 0.4; world.emit("armorHit", { "x": cx, "y": cy, "e": e })
	if e.tagged > 0:
		poise *= 1.25
	if e.hp != INF:
		e.hp -= dmg
	e.poise += poise
	e.flash = 6
	if is_player:
		if source == "melee":
			owner.hit_confirm = true
		owner.on_dealt_damage(dmg, source == "melee")
		if not hit.get("ult"):
			owner.gain_ult(dmg * Tune.C.ULT.gain.dealt, world)
	var heavy_hit: bool = hit.get("vbTier", 0) >= 2 or hit.get("armorBreak", false) or hit.get("rail", false) or poise >= 40
	if source == "melee":
		var stop: int = 3 + int(hit.vbTier) * 2 if hit.get("vbTier") else (6 if heavy_hit else 3)
		if owner:
			owner.hitstop = maxi(owner.hitstop, stop)
		e.hitstop = stop + 1
	else:
		e.hitstop = maxi(e.hitstop, 2)
	if e.boss:
		e.hitstop = mini(e.hitstop, 2)   # a combo never freezes a boss in place

	world.emit("hit", { "x": cx, "y": cy, "e": e, "owner": owner, "heavy": heavy_hit, "tier": hit.get("vbTier", 0), "source": source, "dmg": dmg })
	if hit.get("vbTier") == 3 or (hit.get("rail") and (e.type == "brute" or e.boss)):
		world.emit("impact", { "x": cx, "y": cy, "big": true })

	if ambush:
		world.emit("ambush", { "x": cx, "y": cy, "e": e, "owner": owner })
	if e.hp <= 0:
		kill(world, e, owner, hit)
		return "kill"
	var T: String = e.type
	var can_move: bool = not T in ["post", "turret", "sniper", "mortar"]
	var kb = hit.get("kb")
	if ambush and T != "post" and T != "turret":
		if e.state != "stagger":
			stagger(world, e, 120 if T == "brute" else 90)
	elif hit.get("scatter") and e.light and can_move and not e.flier and not armored:
		# A ground pound's scatter blast throws light enemies outward in an arc
		if e.state == "windup" or e.state == "aim" or e.state == "lock":
			world.director.release(e)
		e.state = "launched"; e.st = 0; e.vy = kb[1]; e.vx = kb[0]; e.poise = 0
	elif e.poise >= e.poise_max:
		stagger(world, e, 120 if T == "brute" else (90 if T == "shield" else 60))
	elif not armored and e.state != "stagger" and not (e.state == "snared" and not hit.get("launcher")) and T in ["swarmer", "shield", "sniper", "drone"]:
		if e.state == "windup" or e.state == "aim" or e.state == "lock":
			world.director.release(e)
		# Drones fly, so they flinch in place instead of being launched
		if hit.get("launcher") and e.light and can_move and not e.flier:
			e.state = "launched"; e.st = 0; e.vy = kb[1]; e.vx = kb[0] * 0.3; world.director.release(e)
		elif not e.flier and (e.state == "launched" or (not e.on_ground and can_move)):
			e.state = "launched"; e.st = 0; e.vy = maxf(e.vy, 4); e.vx = kb[0] * 0.3 if kb != null else 0.0
		elif e.state != "caught":
			# (an enemy stunned by Echo's spin keeps what is left of that stun through the follow-up hits)
			var left: int = e.stun - e.st if e.state == "hitstun" and e.dizzy else 0
			e.state = "hitstun"; e.st = 0; e.stun = maxi(16 if T == "swarmer" else 12, left)
	# Chain lightning holds a light enemy it stuns for longer; everything it touches crackles for a moment
	if hit.get("shock"):
		e.shock_t = maxi(e.shock_t, int(hit.get("stun", 12)))
		if e.light and not armored and not e.boss and e.state == "hitstun":
			e.stun = maxi(e.stun, int(hit.get("stun", 0)))
	if can_move and not armored and kb != null and not e.boss and not hit.get("well"):
		if e.state != "launched":
			e.vx = kb[0]
			if kb[1] > 0:
				e.vy = maxf(e.vy, kb[1] * 0.6)
	# (RAM's close-range knockback, RAM.knock: M2)
	return "hit"

static func stagger(world, e, ticks: int) -> void:
	if e.boss:
		if e.stagger_cd > 0 or e.invuln > 0:
			return
		ticks = 150; e.stagger_cd = 420; e.atk = null
	world.director.release(e)
	e.state = "stagger"; e.st = 0; e.stun = ticks; e.poise = 0
	world.emit("stagger", { "x": e.x, "y": e.y + e.h * 0.6, "e": e })

static func kill(world, e, owner, hit := {}) -> void:
	e.dead = true; e.death_t = 0; e.hp = 0
	world.director.release(e)
	if owner != null and owner.kind == "player" and not hit.get("ult"):
		owner.gain_ult(Tune.C.ULT.gain.kill, world)
	world.on_kill(e, owner)
	world.emit("kill", { "x": e.x, "y": e.y + e.h / 2, "e": e, "owner": owner })
	if e.boss:
		world.emit("bossDown", { "e": e, "x": e.x, "y": e.y + e.h / 2, "owner": owner })

static func _diff() -> Dictionary:
	return Tune.C.DIFFICULTY.get(Tune.settings.difficulty, Tune.C.DIFFICULTY.normal)

# ---- Players taking hits ---------------------------------------------------------------

static func hit_player(world, p, hit: Dictionary) -> String:
	if p.state == "dead" or p.state == "downed" or p.state == "ult":
		return "ignored"
	# Nova's Aegis blocks every attack (unblockables too) for him and anyone inside it
	var guard = world.shield_for(p)
	var o = hit.get("owner")
	if guard != null:
		var fx: float
		var fy: float
		if hit.get("proj") != null:
			fx = hit.proj.x; fy = hit.proj.y
		elif hit.get("at") != null:
			fx = hit.at.x; fy = hit.at.y
		elif o != null:
			fx = o.x; fy = o.y + o.h * 0.5
		else:
			fx = p.x + p.facing; fy = p.y + 1
		world.absorb_aegis(guard, hit.get("dmg", 0.0) * _diff().dmg, fx, fy, ("i%d" % hit.instance) if hit.has("instance") else null)
		# Heavy melee rebounds off hard light: a charging Charger is dazed as if it hit a wall
		if o != null and o.kind == "enemy" and hit.get("proj") == null and hit.get("heavy"):
			o.poise += 30
			if o.state == "charge":
				o.state = "dazed"; o.st = 0; o.vx = -o.facing * 4; world.emit("chargeCrash", { "e": o })
			if o.boss and o.state == "dive":
				o.parried = 2   # a diving Stormcaller crashes off the hard light
		return "shielded"
	var attacker = o
	var unblockable: bool = hit.get("cat") == "unblockable" or hit.get("unblockable", false)
	var pos := { "x": p.x, "y": p.y + p.h * 0.6 }
	# (RAM's Rampart: M2)

	var win: Dictionary = p.parry_windows()
	if p.state == "parry" and p.parry_result == null and p.parry_t <= win.window:
		if unblockable:
			world.emit("parryFail", pos)
		else:
			var perfect: bool = p.parry_t <= win.perfect
			p.parry_result = "perfect" if perfect else "normal"; p.st = 0
			p.hitstop = 6 if perfect else 4
			if hit.get("heavy") and not perfect:
				p.hp -= hit.dmg * 0.3 * _diff().dmg
				p.vx = -p.facing * 5
				if p.hp <= 0:
					world.down_player(p)
					return "hit"
			if attacker != null and attacker.kind == "enemy" and hit.get("proj") == null:
				if attacker.boss:
					attacker.parried = 2 if perfect else 1   # bosses react on their next tick
				attacker.poise += 60 if perfect else 25
				attacker.hitstop = 8 if perfect else 4
				if attacker.poise >= attacker.poise_max:
					stagger(world, attacker, 120 if attacker.type == "brute" else 80)
				elif perfect and attacker.state == "attack":
					attacker.state = "recover"; attacker.st = 0
				elif perfect and attacker.state == "charge":
					attacker.state = "dazed"; attacker.st = 0; attacker.vx = -attacker.facing * 3   # a parried Charger reels
			if perfect:
				if p.char == "nova":
					p.bulwark_cd = maxf(0, p.bulwark_cd - 120)
					for e in world.enemies:
						if e.dead or e.type in ["post", "turret"]:
							continue
						if absf(e.x - p.x) < 2.4 and absf(e.y - p.y) < 2:
							e.vx = U.sgn(e.x - p.x) * 8
				# (Echo's riposte and Resolve: M2)
			else:
				p.add_resolve(10)
			if perfect:
				p.gain_ult(Tune.C.ULT.gain.perfect, world)
			var ev := pos.duplicate(); ev.p = p; ev.perfect = perfect; ev.heavy = bool(hit.get("heavy", false))
			world.emit("parry", ev)
			return "parried"

	# Nova's dodge: an attack that reaches him in its opening ticks is a perfect dodge
	if p.state == "dodge" and p.dodge != null and not p.dodge.perfect and p.dodge.t <= Tune.C.DODGE.perfect:
		world.perfect_dodge(p)
	if p.mercy > 0 or p.iframe:
		return "ignored"
	var dmg: float = hit.dmg * _diff().dmg
	var armored := false   # (Echo's armored charged swing: M2)
	# (RAM's Provoke brace, mid-charge damage cut and Guardian Link: M2.) Plating soaks what it can.
	if p.plate > 0:
		var a := minf(p.plate, dmg)
		p.plate -= a; dmg -= a
		if a > 0:
			world.emit("plateHit", { "p": p, "x": pos.x, "y": pos.y, "left": p.plate })
	var stalwart := false   # (RAM's Stalwart: M2)
	p.hp -= dmg
	p.gain_ult(dmg * Tune.C.ULT.gain.taken, world)
	p.break_veil(world, "hit")
	if not stalwart:
		p.charge_t = 0
		if p.focus > 0:
			p.lose_focus(world)
		p.rifle_t = 0; p.dash_charge_t = 0; p.burst_t = 0; p.sub_armed = false; p.dodge = null
	p.mercy = int(Tune.C.MERCY_TICKS); p.hitstop = 2 if stalwart else 4
	world.emit("playerHit", { "x": pos.x, "y": pos.y, "p": p, "dmg": dmg, "heavy": bool(hit.get("heavy", false)), "armored": armored or stalwart })
	if p.hp <= 0:
		p.hp = 0
		world.down_player(p)
		return "hit"
	if not armored and not stalwart:
		if p.beam != null:
			world.end_beam(p, "hit")
		p.state = "hitstun"; p.st = 0; p.stun = 24 if hit.get("heavy") or unblockable else 14
		var kb = hit.get("kb")
		p.vx = kb[0] if kb != null else 0.0; p.vy = kb[1] if kb != null else 3.0
		p.dash = null; p.lash = null; p.zip = null; p.melee_charged = false
	return "hit"

# ---- Projectiles, barriers, shockwaves --------------------------------------------------

# Minimum distance between two points moving linearly over the same tick.
static func swept_distance(a: Dictionary, b: Dictionary) -> float:
	var r0x: float = a.px - b.px
	var r0y: float = a.py - b.py
	var dx: float = (a.x - b.x) - r0x
	var dy: float = (a.y - b.y) - r0y
	var dd := dx * dx + dy * dy
	var t := clampf(-(r0x * dx + r0y * dy) / dd, 0, 1) if dd > 1e-9 else 0.0
	return U.hypot(r0x + dx * t, r0y + dy * t)

static func projectile_hits(world, pr: Dictionary) -> void:
	if pr.team == "p":
		for e in world.enemies:
			if e.dead or pr.hitSet.has(e.id) or not circle_ent(pr, e):
				continue
			pr.hitSet[e.id] = true
			# (Echo's snares and Fix's Hot Rivets: M2)
			if pr.get("blast") != null:
				detonate(world, pr, pr.x, pr.y, null, false); pr.dead = true
				return
			var hit: Dictionary = pr.duplicate()
			hit.kb = [U.sgn(pr.vx) * (pr.kb if pr.get("kb") else 2.0), pr.kbY if pr.get("kbY") else 1.0]
			if pr.get("falloff") != null:   # secondary weapon pellets lose damage over distance
				var f := maxf(0.25, 1 - U.hypot(pr.x - pr.falloff.x, pr.y - pr.falloff.y) / pr.falloff.d)
				hit.dmg *= f; hit.poise *= f; hit.kb[0] *= f
			var res := hit_enemy(world, e, hit, "proj")
			if pr.get("disc") != null:   # the disc cuts on through; a shield or a boss's guard turns it for home
				if res == "blocked" and pr.disc.phase == "out":
					disc_turn(pr, "back"); world.emit("ricochet", { "x": pr.x, "y": pr.y, "pr": pr })
				continue
			if res == "hit" or res == "kill":
				award_focus(world, pr)
			if pr.get("tracer") or pr.get("mark"):
				e.tagged = maxi(e.tagged, 600); world.emit("tag", { "x": e.x, "y": e.y + e.h, "e": e })
			if pr.get("splash") != null:
				detonate(world, pr, pr.x, pr.y, e, false)   # splash reaches the enemies around this one
			if pr.get("prism") != null:   # splits on impact; a blocked prism glances back off the shield
				world.split_prism(pr, pr.x, pr.y, -pr.vx if res == "blocked" else pr.vx, pr.vy, e)
				pr.dead = true
				return
			if not pr.get("pierce") or res == "blocked":
				pr.dead = true
				return
			if pr.has("pierceLeft"):
				pr.pierceLeft -= 1
				if pr.pierceLeft < 0:
					pr.dead = true
					return
	else:
		for p in world.players:
			if p.state == "dead" or p.state == "downed":
				continue
			# (Echo's deflect: M2)
			if not circle_ent(pr, p):
				continue
			if pr.get("blast") != null:   # mortar shells burst on contact
				detonate(world, pr, pr.x, pr.y, null, false); pr.dead = true
				return
			var res := hit_player(world, p, { "dmg": pr.dmg, "heavy": pr.get("heavy", false), "cat": "heavy" if pr.get("heavy") else "standard",
				"kb": [U.sgn(pr.vx) * 6.0, 3.0], "owner": pr.get("owner"), "proj": pr })
			if res != "ignored":
				pr.dead = true
				return

# Marksman kit: the first piece of a charged release to land earns Focus for the whole shot (2 on a Perfect
# Release); every basic round that lands earns a little.
static func award_focus(world, pr: Dictionary) -> void:
	var p = pr.get("owner")
	if p == null or p.kind != "player" or p.char != "nova":
		return
	var fam = pr.get("family")
	if fam != null:
		if not fam.focused:
			fam.focused = true; p.gain_focus(2 if fam.perfect else 1, world)
	elif pr.get("kind") == "shot":
		p.gain_focus(Tune.C.MARKSMAN.focus.perRound, world)

# Where a projectile bursts. Arc shells and mortar shells explode; Nova's other shots splash. A burst on
# terrain (on_terrain) can rocket-jump Nova; an Arc shell can wherever it bursts.
static func detonate(world, pr: Dictionary, x: float, y: float, skip, on_terrain: bool) -> void:
	var common := { "owner": pr.get("owner"), "team": pr.team, "x": x, "y": y, "level": pr.get("level", 0), "perfect": bool(pr.get("perfect", false)),
		"family": pr.get("family"), "skip": skip }
	# (RAM's Breach Shot burst and Fix's Hot Rivets: M2)
	if pr.get("blast") != null:
		var o := common.duplicate(); o.spec = pr.blast; o.rocket = true
		o.kind = "frag" if pr.kind == "grenade" or pr.kind == "bomblet" else "blast"
		world.explode(o)
	elif pr.get("splash") != null:
		var o := common.duplicate(); o.spec = pr.splash; o.rocket = on_terrain; o.kind = "splash"
		world.explode(o)
	if pr.get("cluster") != null:
		world.cluster_burst(pr, x, y)   # a level 3 grenade scatters bomblets

# Walls: shards ricochet while they have bounces left, shells burst, other shots splash, prisms split off
# the surface. Returns true when the projectile is gone.
static func hit_wall(world, pr: Dictionary, ox: float, oy: float) -> bool:
	# A breakable piece takes the shot's damage (enemy fire wears cover down too); blasting shells do it in explode
	var bk = null if pr.get("blast") != null else world.level.breakable_at(pr.x, pr.y, 0.05)
	if bk != null:
		world.damage_box(bk, maxf(0.5, pr.get("dmg", 0.0)) * (2.0 if pr.get("heavy") else 1.0), pr.x, pr.y, pr.get("owner"))
	var fx: bool = world.level.point_in_solid(pr.x, oy)
	var fy: bool = world.level.point_in_solid(ox, pr.y)
	var flip_x := fx or not fy
	var flip_y := fy or not fx
	if pr.get("disc") != null:
		# The disc glances off terrain on its way out and turns for home (hovering first if it would)
		pr.x = ox; pr.y = oy
		if pr.disc.phase == "out":
			disc_turn(pr, "hover" if pr.disc.hover > 0 else "back")
		elif pr.disc.phase == "hover":
			pr.vx = 0.0; pr.vy = 0.0
		world.emit("ricochet", { "x": ox, "y": oy, "pr": pr })
		return false
	if pr.get("bouncy"):
		bounce(world, pr, ox, oy, flip_x, flip_y)
		return false
	if pr.get("bounces", 0) > 0:
		pr.x = ox; pr.y = oy
		if flip_x: pr.vx = -pr.vx
		if flip_y: pr.vy = -pr.vy
		pr.bounces -= 1
		world.emit("ricochet", { "x": ox, "y": oy, "pr": pr })
		return false
	detonate(world, pr, ox, oy, null, true)
	if pr.get("prism") != null:
		world.split_prism(pr, ox, oy, -pr.vx if flip_x else pr.vx, -pr.vy if flip_y else pr.vy, null)
	pr.dead = true; world.emit("projWall", { "x": pr.x, "y": pr.y, "pr": pr })
	return true

# End of a projectile's time (enemy shots): shells burst in the air, prisms split forward
static func expire(world, pr: Dictionary) -> void:
	if pr.get("blast") != null:
		detonate(world, pr, pr.x, pr.y, null, false)
	elif pr.get("prism") != null:
		world.split_prism(pr, pr.x, pr.y, pr.vx, pr.vy, null)
	pr.dead = true

# A grenade bounces off terrain, keeping `bouncy` of its speed; on a floor with little speed left it comes to
# rest and rolls to a stop
static func bounce(world, pr: Dictionary, ox: float, oy: float, flip_x: bool, flip_y: bool) -> void:
	pr.x = ox; pr.y = oy
	var sp := U.hypot(pr.vx, pr.vy)
	if flip_x:
		pr.vx = -pr.vx * pr.bouncy
	if flip_y:
		var floor_hit: bool = pr.vy < 0
		pr.vy = -pr.vy * pr.bouncy; pr.vx *= Tune.C.SUB.grenade.roll
		if floor_hit and pr.vy < 2.4:
			pr.vy = 0.0; pr.rest = true
			var g: float = world.level.ground_below(pr.x, oy + 0.05)
			if g > -INF and oy - g < 0.5:
				pr.y = g + pr.r
	if sp > 3:
		world.emit("bounce", { "x": ox, "y": oy, "pr": pr, "sp": sp })

static func update_projectiles(world, frozen := false) -> void:
	var list: Array = world.projectiles
	var lv: Level = world.level
	var DT := Tune.DT
	for pr in list:
		if pr.dead:
			continue
		pr.px = pr.x; pr.py = pr.y
		if frozen and pr.team == "e":
			continue   # an ultimate holds enemy fire in the air
		# (Fix's stuck Hot Rivets: M2)
		# A perfect dodge slows enemy shots close by
		var k := 1.0
		if pr.get("slowT", 0) > 0:
			pr.slowT -= 1; k = 0.5
		if pr.get("homing"):
			steer_to_tagged(world, pr)
		if pr.get("seek") != null:
			steer_dart(world, pr)
		if pr.get("disc") != null:
			steer_disc(world, pr)
			if pr.dead:
				continue
		if pr.get("rest"):
			# A grenade at rest rolls to a stop, and falls again if the floor goes
			pr.vx *= 0.8; pr.vy = 0.0
			if not lv.point_in_solid(pr.x, pr.y - pr.r - 0.08) and lv.one_way_top(pr.x, pr.y, pr.y - pr.r - 0.08) == null:
				pr.rest = false
		elif pr.get("gravity"):
			pr.vy -= pr.gravity * DT * k
		pr.ttl -= 1
		if pr.ttl <= 0:
			expire(world, pr)
			continue
		# Anything that leaves the level is gone (Nova's shots otherwise fly until they hit something)
		if pr.x < lv.level_x0 - 2 or pr.x > lv.level_x1 + 2 or pr.y < Level.kill_y_at(pr.x) - 6 or pr.y > 90:
			pr.dead = true
			continue
		# Sub-step so fast shots cannot skip over thin targets or walls
		var steps := maxi(1, ceili(U.hypot(pr.vx, pr.vy) * DT * k / 0.3))
		var s := 0
		while s < steps and not pr.dead:
			s += 1
			var ox: float = pr.x
			var oy: float = pr.y
			pr.x += pr.vx * DT * k / steps; pr.y += pr.vy * DT * k / steps
			if pr.get("bouncy") and pr.vy < 0:
				var top = lv.one_way_top(pr.x, oy, pr.y)
				if top != null:
					bounce(world, pr, pr.x, top + 0.01, false, true)
					continue
			if not pr.get("ghost") and lv.point_in_solid(pr.x, pr.y):
				if hit_wall(world, pr, ox, oy):
					break
				continue
			if pr.team == "e":
				var guard = world.aegis_at(pr.x, pr.y, pr.r)
				if guard != null:
					# Stopped at the hard light: a shell bursts on it, everything else is absorbed
					world.absorb_aegis(guard, pr.blast.dmg if pr.get("blast") != null else pr.dmg, pr.x, pr.y, null)
					if pr.get("blast") != null:
						world.emit("enemyBlast", { "x": pr.x, "y": pr.y, "r": pr.blast.r * 0.6 })
					pr.dead = true
					break
				# (RAM's Rampart and Fix's gadgets: M2)
			for b in world.barriers:
				if not crosses_barrier(b, ox, oy, pr.x, pr.y):
					continue
				if pr.team == "e":
					pr.dead = true; world.emit("barrierBlock", { "x": pr.x, "y": pr.y, "kind": b.kind })
					break
				if not pr.get("amplified"):
					pr.amplified = true; pr.pierce = true; pr.dmg *= 1.5; pr.poise = (pr.poise if pr.poise else 8.0) * 1.5; pr.r *= 1.3
					if pr.get("blast") != null:
						pr.blast.dmg *= 1.5; pr.blast.poise *= 1.5; pr.blast.r *= 1.2
					world.emit("amplify", { "x": pr.x, "y": pr.y, "pr": pr })
			if not pr.dead:
				projectile_hits(world, pr)
	# Nova's shots intercept hostile projectiles; heavy ones need a charged shot (not darts, shards or pellets)
	for a in list:
		if a.dead or a.team != "p" or not a.get("intercept"):
			continue
		for b in list:
			if b.dead or b.team != "e":
				continue
			if swept_distance(a, b) > a.r + b.r + 0.25:
				continue
			var ih = a.get("interceptHeavy")
			if ih == null:
				ih = bool(a.get("level", 0))
			if b.get("heavy") and not ih:
				a.dead = true; world.emit("interceptFail", { "x": a.x, "y": a.y })
				break
			b.dead = true
			if not a.get("pierce"):
				a.dead = true
			var saved = world.projectile_target(b, a.owner)
			world.emit("intercept", { "x": b.x, "y": b.y, "owner": a.owner, "heavy": b.get("heavy", false), "saved": saved })
			if a.dead:
				break
	world.projectiles = list.filter(func(q): return not q.dead)

# Nova's disc: out along the throw (easing off toward the far end), a hover there from level 2 (cutting again
# every SUB.disc.tick ticks), then home to his chest, faster and faster and through walls. It cuts each enemy
# once per leg. It fades if he is gone.
static func disc_turn(pr: Dictionary, phase: String) -> void:
	pr.disc.phase = phase; pr.disc.t = 0; pr.hitSet.clear()
	if phase == "back":
		pr.ghost = true

static func steer_disc(world, pr: Dictionary) -> void:
	var D: Dictionary = Tune.C.SUB.disc
	var s: Dictionary = pr.disc
	var o = pr.owner
	s.t += 1
	if o == null or not world.players.has(o) or o.state == "dead" or o.state == "downed" or o.char != "nova":
		pr.dead = true; world.emit("discFade", { "x": pr.x, "y": pr.y })
		return
	if s.phase == "out":
		var f: float = 1 - 0.65 * maxf(0, (s.t - s.out * 0.55) / (s.out * 0.45))
		pr.vx = s.dx * s.speed * f; pr.vy = s.dy * s.speed * f
		if s.t >= s.out:
			disc_turn(pr, "hover" if s.hover > 0 else "back")
	elif s.phase == "hover":
		pr.vx *= 0.6; pr.vy *= 0.6
		if s.t % int(D.tick) == 0:
			pr.hitSet.clear()
		if s.t >= s.hover:
			disc_turn(pr, "back")
	else:
		var dx: float = o.chest_x() - pr.x
		var dy: float = o.chest_y() - pr.y
		var d := U.hypot(dx, dy)
		if d == 0:
			d = 1
		var sp := minf(D.back + s.t * 0.6, 44)
		pr.vx = dx / d * sp; pr.vy = dy / d * sp
		if d < 0.8 + sp * Tune.DT:
			pr.dead = true; world.emit("discCatch", { "p": o, "x": o.chest_x(), "y": o.chest_y() })
		elif s.t > D.maxBack:
			pr.dead = true

# Volley darts fly straight for a moment so the fan opens, then turn toward their target. A dart whose
# target is gone picks the nearest enemy ahead it has not hit yet.
static func steer_dart(world, pr: Dictionary) -> void:
	var s: Dictionary = pr.seek
	s.age += 1
	if s.age < s.delay or s.age > s.until:
		return   # after `until` ticks a dart flies straight on
	var t = s.target
	if t == null or t.dead:
		t = null
		var bd := 10.0
		var sp0 := U.hypot(pr.vx, pr.vy)
		if sp0 == 0:
			sp0 = 1
		for e in world.enemies:
			if e.dead or pr.hitSet.has(e.id):
				continue
			var dx: float = e.x - pr.x
			var dy: float = e.y + e.h / 2 - pr.y
			var d := U.hypot(dx, dy)
			if d < bd and (dx * pr.vx + dy * pr.vy) / (d * sp0) > 0:
				bd = d; t = e
		s.target = t
		if t == null:
			return
	var sp := U.hypot(pr.vx, pr.vy)
	var a := atan2(pr.vy, pr.vx)
	var da := U.wrap_angle(atan2(t.y + t.h / 2 - pr.y, t.x - pr.x) - a)
	var na: float = a + clampf(da, -s.turn, s.turn)
	pr.vx = cos(na) * sp; pr.vy = sin(na) * sp

# Homing shots curve toward tagged enemies, and toward the shooter's lock-on target
static func steer_to_tagged(world, pr: Dictionary) -> void:
	var best = null
	var bd := 14.0
	var sp := U.hypot(pr.vx, pr.vy)
	var dx0: float = pr.vx / sp
	var dy0: float = pr.vy / sp
	var lock = pr.owner.lock_t if pr.get("owner") != null else null
	for e in world.enemies:
		if e.dead or (e.tagged <= 0 and e != lock):
			continue
		var dx: float = e.x - pr.x
		var dy: float = e.y + e.h / 2 - pr.y
		var d := U.hypot(dx, dy)
		if d < bd and (dx * dx0 + dy * dy0) / d > 0.5:
			bd = d; best = e
	if best == null:
		return
	var ta := atan2(best.y + best.h / 2 - pr.y, best.x - pr.x)
	var a := atan2(pr.vy, pr.vx)
	var da := U.wrap_angle(ta - a)
	var na := a + clampf(da, -0.06, 0.06)
	pr.vx = cos(na) * sp; pr.vy = sin(na) * sp

static func update_shockwaves(world) -> void:
	var lv: Level = world.level
	for s in world.shockwaves:
		s.x += s.dir * s.speed * Tune.DT; s.ttl -= 1
		if lv.point_in_solid(s.x + s.dir * 0.5, s.y + 0.3):
			var bk = lv.breakable_at(s.x + s.dir * 0.5, s.y + 0.3)
			if bk != null:
				world.damage_box(bk, (s.dmg if s.dmg else 4.0) * 2, s.x + s.dir * 0.5, s.y + 0.4, s.owner)
			s.ttl = 0
		if s.get("team") == "p":
			world.spawn_hitbox({ "owner": s.owner, "team": "p", "x0": s.x - 0.5, "x1": s.x + 0.5, "y0": s.y, "y1": s.y + s.h,
				"dmg": s.dmg, "poise": s.poise, "kb": [s.dir * 6.0, 9.0], "launcher": true, "instance": s.instance, "quake": true })
		else:
			world.spawn_hitbox({ "owner": s.owner, "team": "e", "x0": s.x - 0.45, "x1": s.x + 0.45, "y0": s.y, "y1": s.y + s.h,
				"dmg": s.dmg, "kb": [s.dir * 8.0, 7.0], "unblockable": true, "cat": "unblockable", "instance": s.instance, "ground": true })
	world.shockwaves = world.shockwaves.filter(func(q): return q.ttl > 0)
