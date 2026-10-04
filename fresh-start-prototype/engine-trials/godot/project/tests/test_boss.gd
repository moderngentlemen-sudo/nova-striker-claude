# boss-test.mjs: the Lockwarden (the Concourse Lock's last wave) and the Stormcaller (the relay beacon). The
# perfect-parried hammer uses Nova's Sentinel kit, which parries (the prototype uses Echo); Echo's deflect of
# the Stormcaller's volley comes with his kit. The three-player health check uses three Novas.
extends RefCounted

class Fight:
	var w: World
	var ps: Array = []
	var p: PlayerSim
	var e: EnemySim
	var prev := {}
	var log: Array = []
	var crouch := false
	func _init(boss: String, chars := ["nova"]) -> void:
		w = World.new(); w.enemies = []
		for i in chars.size():
			var q := w.add_player("p%d" % i, chars[i])
			ps.append(q); prev[q.slot] = {}
		p = ps[0]
		w.boss_rush(boss); run({}, 3)
		for q in w.enemies:
			if q.boss:
				e = q
		for q in ps:
			q.mercy = 0
	func run(o = {}, n := 1, each := Callable()) -> bool:
		for i in n:
			var cmds := {}
			for q in ps:
				var want: Dictionary = o.call(i, q) if o is Callable else (o if q.slot == 0 else {})
				var c := Cmd.make(prev[q.slot], want)
				prev[q.slot] = c.held.duplicate(); cmds[q.slot] = c
			w.step(cmds)
			log.append_array(w.events); w.events.clear()
			if each.is_valid() and each.call(i):
				return true
		return false
	func count(type: String, f := Callable()) -> int:
		return log.filter(func(q): return q.type == type and (not f.is_valid() or f.call(q))).size()
	func hit_boss(o := {}) -> void:
		var hb := { "owner": p, "team": "p", "x0": e.x - 2, "x1": e.x + 2, "y0": e.y, "y1": e.y + e.h, "dmg": 5.0, "poise": 0.0, "kb": [0.0, 0.0], "instance": w.new_instance() }
		hb.merge(o, true)
		w.spawn_hitbox(hb)
	# Let the boss finish its arrival and stand still, idle, at x facing `facing`
	func settle(x: float, facing := -1, y = null) -> void:
		run({}, 1)
		for i in 400:
			if e.state != "intro":
				break
			run({}, 1)
		e.x = x; e.prev_x = x; e.vx = 0; e.vy = 0; e.facing = facing; e.cd = 9999; e.state = "idle"; e.st = 0; e.atk = null
		if y == null and e.type == "warden":
			y = 0.0
		if y != null:
			e.y = y; e.prev_y = y

static func put(q: PlayerSim, x: float, y: float) -> void:
	q.x = x; q.y = y; q.prev_x = x; q.prev_y = y; q.vx = 0; q.vy = 0; q.mercy = 0; q.state = "normal"; q.st = 0

func run(t: TestKit) -> void:
	Tune.settings.difficulty = "normal"; Tune.settings.dashIframes = true
	var BW: Dictionary = Tune.C.BOSS.warden
	var BS: Dictionary = Tune.C.BOSS.stormcaller

	# ---- The Lockwarden
	# It drops into the lock: hits are shrugged off while it arrives, the landing throws shockwaves, then it fights
	var f := Fight.new("warden")
	var sealed: bool = f.w.level.gates.L and f.w.level.gates.R and f.e != null and f.e.type == "warden" and f.e.state == "intro"
	var hp0 := f.e.hp
	f.hit_boss({ "dmg": 30.0 }); f.run({}, 1)
	var shrug := f.e.hp == hp0 and f.count("blocked", func(b): return b.e == f.e) == 1
	f.run({}, 200)
	t.check(sealed and shrug and f.count("bossIntro") == 1 and f.count("bossSlam", func(b): return b.get("big")) >= 1 and f.e.state != "intro",
		"Lockwarden: the gates seal, it drops in (hits during the arrival are blocked), lands with a slam and starts fighting (%s)" % f.e.state)

	# Health scales with the team
	var one := Fight.new("warden")
	var three := Fight.new("warden", ["nova", "nova", "nova"])
	t.check(one.e.max_hp == BW.hp and three.e.max_hp == U.jround(BW.hp * 2.2), "Lockwarden health: %d solo, %d for three players" % [one.e.max_hp, three.e.max_hp])

	# Each attack announces its category
	f = Fight.new("warden"); f.settle(82)
	put(f.p, 79, 0)
	var cats := {}
	for k in ["sweep", "hammer", "stomp", "missiles", "charge"]:
		f.w.events.clear(); f.e.state = "idle"; f.e.atk = null; Bosses.force_attack(f.w, f.e, k)
		var tg := f.w.events.filter(func(q): return q.type == "telegraph")
		cats[k] = tg[0].cat if tg else null
	t.check(cats.sweep == "standard" and cats.hammer == "heavy" and cats.stomp == "unblockable" and cats.missiles == "standard" and cats.charge == "heavy",
		"Lockwarden telegraphs: %s" % ", ".join(cats.keys().map(func(k): return "%s %s" % [k, cats[k]])))

	# The sweep hits a player in reach; a perfect parry of the hammer leaves it dazed
	var a := Fight.new("warden"); a.settle(82); put(a.p, 79.6, 0); a.p.facing = 1
	Bosses.force_attack(a.w, a.e, "sweep"); a.run({}, int(BW.sweep.wind) + 6)
	var hit := a.count("playerHit", func(h): return h.p == a.p) == 1
	Tune.settings.novaKit = "sentinel"
	var b := Fight.new("warden"); b.settle(82); put(b.p, 79.6, 0); b.p.facing = 1
	Bosses.force_attack(b.w, b.e, "hammer"); b.run({}, int(BW.hammer.wind) - 1)
	b.run({ "held": { "parry": true } }, 4); b.run({}, 14)
	Tune.settings.novaKit = "marksman"
	var parried := b.count("parry", func(q): return q.perfect) == 1 and b.e.state == "dazed" and b.count("bossDazed") == 1 and b.p.hp == b.p.max_hp
	t.check(hit and parried, "Sweep lands on a player in reach; a perfect-parried hammer leaves the Lockwarden dazed (%s)" % b.e.state)

	# Charging into a wall dazes it
	f = Fight.new("warden"); f.settle(76, -1); put(f.p, 71, 0)
	Bosses.force_attack(f.w, f.e, "charge"); f.run({ "held": { "jump": true } }, 1); f.run({}, int(BW.charge.wind) + 40)
	t.check(f.count("chargeCrash", func(q): return q.e == f.e) == 1 and f.e.state == "dazed", "Lockwarden charge crashes into the column and is dazed (%s)" % f.e.state)

	# Armor: a hit that can't break it does a third of the damage; armor-breaking hits strip the plates one by one
	f = Fight.new("warden"); f.settle(82)
	var h0 := f.e.hp
	f.hit_boss({ "dmg": 10.0 }); f.run({}, 1)
	var soft := h0 - f.e.hp
	var h1 := f.e.hp
	f.hit_boss({ "dmg": 10.0, "armorBreak": true }); f.run({}, 1)
	var hard := h1 - f.e.hp
	t.check(absf(soft - 3) < 0.01 and absf(hard - 6) < 0.01 and f.e.armor == 3, "Armor: %s damage without a break, %s with one (plates %d/4)" % [TestKit.f1(soft), TestKit.f1(hard), f.e.armor])

	# It is never knocked back, and a combo never freezes it for long
	f = Fight.new("warden"); f.settle(82); f.e.armor = 0; put(f.p, 80, 0)
	f.hit_boss({ "dmg": 2.0, "kb": [20.0, 8.0] }); f.run({}, 1)
	t.check(absf(f.e.vx) < 0.01 and f.e.hitstop <= 2, "No knockback (vx %s), hit-stop %d" % [TestKit.f2(f.e.vx), f.e.hitstop])

	# Enough poise damage staggers it; for a while afterwards it cannot be staggered again
	f = Fight.new("warden"); f.settle(82); f.e.armor = 0
	f.hit_boss({ "dmg": 1.0, "poise": 500.0 }); f.run({}, 1)
	var first := f.e.state == "stagger"
	f.run({}, 160); f.hit_boss({ "dmg": 1.0, "poise": 500.0 }); f.run({}, 1)
	t.check(first and f.e.state != "stagger" and f.count("stagger", func(q): return q.e == f.e) == 1, "Stagger once (then immune for %d ticks)" % BW.staggerCd)

	# Phase two at half health: it roars (hits shrugged off), re-arms two plates and gains the laser
	f = Fight.new("warden"); f.settle(86, -1); f.e.armor = 0
	f.e.hp = f.e.max_hp / 2 + 1; f.hit_boss({ "dmg": 4.0, "armorBreak": true }); f.run({}, 6)
	var roared: bool = f.e.phase == 2 and f.e.state == "roar" and f.e.armor == BW.rearm and f.count("bossPhase") == 1
	var hh := f.e.hp
	f.hit_boss({ "dmg": 10.0, "armorBreak": true }); f.run({}, 1)
	t.check(roared and f.e.hp == hh, "Phase two: roar (hit blocked), %d plates back" % f.e.armor)

	# The laser: the low one catches a player standing in its path but not one up on the dais; the high one
	# misses a crouching player
	var laser := func(high: bool, place: Callable) -> int:
		var g := Fight.new("warden"); g.settle(88, -1); g.e.phase = 2; g.e.laser_high = not high
		place.call(g)
		Bosses.force_attack(g.w, g.e, "laser")
		g.run({ "my": -1 } if g.crouch else {}, int(BW.laser.wind + BW.laser.ticks))
		return g.count("playerHit", func(q): return q.p == g.p)
	var stand: int = laser.call(false, func(g): put(g.p, 80, 0))
	var dais: int = laser.call(false, func(g): put(g.p, 80, 2.4); g.p.on_ground = true)
	var crouch: int = laser.call(true, func(g): put(g.p, 80, 0); g.crouch = true)
	var stand_high: int = laser.call(true, func(g): put(g.p, 80, 0))
	t.check(stand == 1 and dais == 0 and crouch == 0 and stand_high == 1, "Laser: low hits standing (%d), misses on the dais (%d); high misses crouching (%d), hits standing (%d)" % [stand, dais, crouch, stand_high])

	# A wipe in the boss fight comes back to the boss: walking in starts it again, without the waves
	f = Fight.new("warden"); f.run({}, 20)
	f.w.reset_to_checkpoint(); f.run({}, 2)
	var ready: bool = f.w.arena.state == "bossReady" and not f.w.enemies.any(func(q): return q.zone == "arena") and not f.w.level.gates.L
	put(f.p, 66, 0); f.run({}, 3)
	t.check(ready and f.w.arena.state == "boss" and f.w.enemies.any(func(q): return q.type == "warden" and not q.dead) and f.w.level.gates.L,
		"After a wipe the arena waits at the boss (%s); walking back in brings the Lockwarden straight back" % ready)

	# ---- The Stormcaller
	# It arrives when the team steps onto the beacon pad, and the gate seals behind them
	f = Fight.new("stormcaller")
	var S: Dictionary = f.w.encounters.filter(func(q): return q.def.id == "beacon")[0]
	t.check(S.state == "active" and f.e != null and f.e.type == "stormcaller" and f.w.level.gates.R2 and f.count("banner", func(q): return q.text == "Stormcaller") == 1,
		"Stormcaller: stepping onto the pad seals the gate and brings it in (%s)" % S.state)

	# The dive crashes it onto the pad (a punish window); diving into Nova's Aegis crashes it for longer
	a = Fight.new("stormcaller"); a.settle(308, -1, 25.2); put(a.p, 303, 18.6)
	Bosses.force_attack(a.w, a.e, "dive"); a.run({ "mx": 1 }, int(BS.dive.wind) - 2); a.run({ "held": { "dash": true }, "mx": 1 }, 2); a.run({}, 50)
	var crash := a.log.filter(func(q): return q.type == "bossCrash")
	b = Fight.new("stormcaller"); b.settle(307.5, -1, 25.2); put(b.p, 307, 18.6)
	Bosses.force_attack(b.w, b.e, "dive"); b.run({ "held": { "sig": true } }, 1); b.run({}, 60)
	var crash2 := b.log.filter(func(q): return q.type == "bossCrash")
	t.check(crash.size() and not crash[0].parried and crash2.size() and crash2[0].parried and b.e.crash_for == BS.dive.parried and b.count("aegisHit") >= 1,
		"Dive: crashes onto the pad (%s); into the Aegis it crashes for %d ticks" % ["parried" if crash and crash[0].parried else "missed", b.e.crash_for])

	# The sweep laser runs across the pad at ankle height: a player up on a perch is clear of it
	var sweep := func(place: Callable) -> Dictionary:
		var g := Fight.new("stormcaller"); g.settle(306, -1, 25.2)
		place.call(g)
		Bosses.force_attack(g.w, g.e, "sweep")
		g.run({}, int(90 + BS.sweep.wind + BS.sweep.ticks))
		return { "hits": g.count("playerHit", func(q): return q.p == g.p), "fired": g.count("bossLaser") }
	var ground: Dictionary = sweep.call(func(g): put(g.p, 307, 18.6))
	var perch: Dictionary = sweep.call(func(g): put(g.p, 310.5, 22.2); g.p.on_ground = true)
	t.check(ground.fired == 1 and ground.hits == 1 and perch.hits == 0, "Sweep: hits a player on the pad (%d), not one on a perch (%d)" % [ground.hits, perch.hits])

	# Phase two calls in two drones; when it falls they go with it, the gate opens, and the route completes
	f = Fight.new("stormcaller", ["nova", "nova"]); f.settle(308, -1, 25.2)
	put(f.ps[0], 305, 18.6); put(f.ps[1], 306, 18.6)
	f.e.hp = f.e.max_hp / 2 + 1; f.hit_boss({ "dmg": 4.0 }); f.run({}, 40)
	var drones := f.w.enemies.filter(func(q): return q.enc == "beacon" and q.add and not q.dead).size()
	f.e.invuln = 0; f.e.armor = 0; f.e.hp = 1; f.hit_boss({ "dmg": 9.0 }); f.run({}, 3)
	S = f.w.encounters.filter(func(q): return q.def.id == "beacon")[0]
	var after := f.w.enemies.filter(func(q): return q.enc == "beacon" and not q.dead).size()
	f.run({}, 160)
	t.check(drones == BS.drones and after == 0 and S.state == "cleared" and not f.w.level.gates.R2 and f.count("bossDown") == 1
		and f.count("banner", func(q): return q.text == "Beacon secured") == 1 and f.count("banner", func(q): return q.text == "Route complete") == 1,
		"Phase two brings %d drones; defeat takes them down, opens the gate, secures the beacon, then the route completes" % drones)
