# v9-test.mjs, Nova's half: the five secondary weapons without recoil, the dodge, automatic lock-on, the
# Solar Uppercut, the ultimate bar, Supernova, and team ultimates. The two-player checks pair Nova with a
# second Nova where the prototype pairs him with Echo (so the team ultimate is Binary Star, not the Eclipse
# Protocol); Echo's own checks come with his kit.
extends RefCounted

# Several players: run(spec, n) where spec is player 1's input, or { 0: ..., 1: ... } for several
class Multi:
	var w: World
	var ps: Array = []
	var prev: Array = []
	var log: Array = []
	var p: PlayerSim
	func _init(x: float, chars: Array, y := 0.0) -> void:
		w = World.new(); w.enemies = []; w.tower_spawned = true
		for i in chars.size():
			var q := w.add_player("t%d" % i, chars[i])
			q.x = x + i * 1.2; q.y = y; q.prev_x = q.x; q.prev_y = y
			ps.append(q); prev.append({})
		p = ps[0]
		run({}, 10)
		for q in ps:
			q.mercy = 0
	func run(spec = {}, n := 1, each := Callable()) -> bool:
		for i in n:
			var s: Dictionary = spec.call(i) if spec is Callable else spec
			var multi := s.keys().any(func(k): return k is int)
			var cmds := {}
			for j in ps.size():
				var o: Dictionary = (s.get(j, {}) if multi else (s if j == 0 else {}))
				var c := Cmd.make(prev[j], o)
				prev[j] = c.held.duplicate(); cmds[ps[j].slot] = c
			w.step(cmds)
			log.append_array(w.events); w.events.clear()
			if each.is_valid() and each.call(i):
				return true
		return false
	func tap(b: String, o := {}) -> void:
		var h: Dictionary = o.get("held", {}).duplicate()
		h[b] = true
		var o2 := o.duplicate(); o2.held = h
		run(o2, 1); run(o, 1)
	func count(type: String, f := Callable()) -> int:
		return log.filter(func(e): return e.type == type and (not f.is_valid() or f.call(e))).size()
	func enemy(type: String, x: float, y := 0.0, o := {}) -> EnemySim:
		var opts := { "cd": 9999, "slamCd": 9999 }
		opts.merge(o, true)
		var e := EnemySim.create(type, x, y, opts, w)
		w.enemies.append(e)
		return e
	func hits(e) -> Array:
		return log.filter(func(h): return h.type == "hit" and h.e == e)
	func select_sub(kind: String) -> void:
		var n := 0
		while p.sub != kind and n < 6:
			n += 1
			tap("sub"); run({}, int(Tune.C.SUB.switchCd))

static func still(e: EnemySim) -> EnemySim:
	e.hitstop = 1000000000
	return e

func run(t: TestKit) -> void:
	var S: Dictionary = Tune.settings
	S.lockOn = true; S.lockMode = "manual"; S.dashCharge = true; S.dashIframes = false; S.difficulty = "normal"
	var SUB: Dictionary = Tune.C.SUB
	var SUBS: Array = Tune.C.SUBS
	var BC: Array = Tune.C.MARKSMAN.burst.charge
	var DODGE: Dictionary = Tune.C.DODGE
	var ULT: Dictionary = Tune.C.ULT
	var LOCK: Dictionary = Tune.C.LOCK

	# ---- Secondary weapons
	# The sub button cycles the five secondary weapons in order, with a short lockout between switches
	var m := Multi.new(99, ["nova"])
	var seen := [m.p.sub]
	for i in 5:
		m.tap("sub"); seen.append(m.p.sub); m.run({}, int(SUB.switchCd))
	m.tap("sub")
	var fast := m.p.sub
	m.tap("sub")
	var blocked := m.p.sub == fast
	var want := SUBS.duplicate(); want.append("scatter")
	t.check(" > ".join(seen) == " > ".join(want) and m.count("subSwitch") >= 6 and blocked,
		"Secondary weapons cycle %s; a second press inside %d ticks is ignored" % [" > ".join(seen), SUB.switchCd])

	# No recoil from any secondary weapon, tapped or charged, on the ground or in the air
	var worst := 0.0
	var ok := true
	for kind in SUBS:
		for air in [false, true]:
			var q := Multi.new(99, ["nova"])
			q.select_sub(kind); q.run({}, 30)
			if air:
				q.p.y = 12; q.p.prev_y = 12; q.p.vy = 0; q.p.on_ground = false; q.run({}, 2)
			var aim := [0.55, -0.83] if air else [-1, 0]
			q.run({ "aim": aim, "held": { "melee": true } }, int(BC[1]) + 2)
			var vx0 := q.p.vx
			var vy0 := q.p.vy
			q.run({ "aim": aim }, 1)
			var dvx := absf(q.p.vx - vx0)
			worst = maxf(worst, dvx)
			ok = ok and dvx < 0.3 and q.p.vy - vy0 <= 0.01
	t.check(ok, "No recoil from any secondary weapon, tapped or charged, on the ground or in the air (largest push %s m/s)" % TestKit.f2(worst))

	# Grenade: a tap throws on release; it bounces, comes to rest, and bursts on its fuse, hurting what is close
	m = Multi.new(98, ["nova"])
	m.select_sub("grenade"); m.run({}, 30)
	m.run({ "aim": [1, -0.5], "held": { "melee": true } }, 1)
	var early := m.count("grenadeThrow")
	m.run({ "aim": [1, -0.5] }, 1)
	var g = m.w.projectiles.filter(func(q): return q.kind == "grenade")[0]
	var st := { "rest": -1, "s": null, "t": 0 }
	m.run({}, int(SUB.grenade.fuse[0]) + 4, func(i):
		st.t = i
		if g.get("rest") and st.rest < 0:
			st.rest = i; st.s = still(m.enemy("swarmer", g.x + 1.3)); st.s.hp = 99
		return m.count("frag") > 0)
	t.check(early == 0 and m.count("grenadeThrow") == 1 and m.count("bounce") >= 1 and st.rest > 0 and m.count("frag") == 1 and st.t >= SUB.grenade.fuse[0] - 3
		and st.s != null and m.hits(st.s).size() == 1 and absf(m.p.vx) < 0.3,
		"Grenade: thrown on release, %d bounces, at rest after %d ticks, bursts on the fuse (%d ticks) and hits the Swarmer beside it" % [m.count("bounce"), st.rest, st.t])

	# It bursts early on an enemy it meets; level 3 scatters bomblets that go off too
	m = Multi.new(98, ["nova"])
	m.select_sub("grenade"); m.run({}, 30)
	var b := still(m.enemy("brute", 101.5)); b.hp = 999
	m.tap("melee", { "aim": [1, 0] })
	var at := { "v": -1 }
	m.run({}, 30, func(i):
		if m.count("frag") and at.v < 0:
			at.v = i
		return false)
	var early_burst: bool = at.v >= 0 and at.v < SUB.grenade.fuse[0] - 20
	var m2 := Multi.new(98, ["nova"])
	m2.select_sub("grenade"); m2.run({}, 30)
	m2.run({ "aim": [1, 0.4], "held": { "melee": true } }, int(BC[2]) + 12); m2.run({ "aim": [1, 0.4] }, 1)
	m2.run({}, 140)
	var frags := m2.count("frag")
	t.check(early_burst and m2.count("cluster") == 1 and frags == 1 + SUB.grenade.bomblets.n,
		"Grenade bursts on contact (%d ticks); a level 3 grenade scatters %d bomblets (%d bursts in all)" % [at.v, SUB.grenade.bomblets.n, frags])

	# Grenades land on one-way platforms instead of falling through
	m = Multi.new(70, ["nova"])
	var plat: Level.Box = null
	for bx in m.w.level.boxes:
		if bx.type == "o" and bx.tag == "dais":
			plat = bx
			break
	var gr := m.w.spawn_projectile({ "team": "p", "owner": m.p, "x": (plat.x0 + plat.x1) / 2, "y": plat.y1 + 2, "vx": 0.0, "vy": -4.0, "gravity": 30.0, "bouncy": 0.5, "r": 0.2, "ttl": 200,
		"dmg": 0.0, "kind": "grenade", "intercept": false, "blast": { "r": 1.0, "dmg": 0.0, "poise": 0.0 } })
	for i in 90:
		m.w.step({})
	t.check(gr.get("rest", false) and absf(gr.y - (plat.y1 + gr.r)) < 0.25, "A grenade comes to rest on a one-way platform (y %s, top %s)" % [TestKit.f2(gr.y), str(plat.y1)])

	# Chain: instant lightning to the nearest enemy in front, jumping on to the next nearest; it arcs round a
	# shield from the front, stuns light enemies, and a tap reaches SUB.chain.jumps[0] enemies
	m = Multi.new(98, ["nova"])
	m.select_sub("chain"); m.run({}, 30)
	var ca := m.enemy("shield", 102, 0, { "facing": -1, "shieldDir": -1 })
	var cb := m.enemy("swarmer", 105)
	var cc := m.enemy("swarmer", 108.5)
	var cd := m.enemy("swarmer", 112)
	for e in [ca, cb, cc, cd]:
		e.hp = 99
	m.tap("melee", { "aim": [1, 0] })
	var ev = m.log.filter(func(q): return q.type == "chain")
	ev = ev[0] if ev else null
	var struck := [ca, cb, cc, cd].filter(func(e): return m.hits(e).size() > 0).size()
	t.check(ev != null and ev.n == SUB.chain.jumps[0] and struck == SUB.chain.jumps[0] and m.count("blocked") == 0 and cb.state == "hitstun" and cb.stun >= SUB.chain.stun[0] and absf(m.p.vx) < 0.3,
		"Chain: a tap arcs through %d enemies (%d points), through the shield's guard, and stuns (%s %d ticks)" % [struck, ev.pts.size() if ev else 0, cb.state, cb.stun])

	# The chain needs a clear line: an enemy behind a wall is not reached; with nothing in reach it fizzles
	m = Multi.new(24, ["nova"])
	m.select_sub("chain"); m.run({}, 30)
	var hid := still(m.enemy("swarmer", 34.5))
	m.tap("melee", { "aim": [1, 0] })
	ev = m.log.filter(func(q): return q.type == "chain")
	ev = ev[0] if ev else null
	t.check(ev != null and ev.n == 0 and m.hits(hid).is_empty() and ev.pts[ev.pts.size() - 1].get("fizzle", false),
		"Chain does not pass through walls; with no target it earths itself (%d points)" % (ev.pts.size() if ev else 0))

	# Disc: out and back, cutting an enemy once each way, then caught; one at a time; a press calls it back
	m = Multi.new(98, ["nova"])
	m.select_sub("disc"); m.run({}, 30)
	var de := still(m.enemy("brute", 102)); de.hp = 999; de.armor = 0
	m.tap("melee", { "aim": [1, 0] })
	m.run({}, 90, func(_i): return m.count("discCatch") > 0)
	var nh := m.hits(de).size()
	var caught := m.count("discCatch") == 1
	m2 = Multi.new(98, ["nova"])
	m2.select_sub("disc"); m2.run({}, 30)
	m2.tap("melee", { "aim": [1, 0] }); m2.run({}, 8)
	var out := m2.w.projectiles.filter(func(q): return q.kind == "disc").size()
	m2.tap("melee", { "aim": [1, 0] })
	var backs := m2.w.projectiles.filter(func(q): return q.kind == "disc")
	var back_phase: String = backs[0].disc.phase if backs else ""
	m2.run({}, 40)
	t.check(nh == 2 and caught and out == 1 and back_phase == "back" and m2.count("discRecall") == 1 and m2.count("discCatch") == 1
		and m2.w.projectiles.filter(func(q): return q.kind == "disc").is_empty(),
		"Disc cuts the Brute on the way out and back (%d hits) and is caught; pressing again while it is out calls it back" % nh)

	# A level 2 disc hovers at the far end and keeps cutting; it slices an enemy shot out of the air
	m = Multi.new(98, ["nova"])
	m.select_sub("disc"); m.run({}, 30)
	var D: Dictionary = SUB.disc
	var far: float = 98.6 + D.speed[2] * D.out[2] / 60 * 0.75
	var fe := still(m.enemy("brute", far)); fe.hp = 999; fe.armor = 0
	m.run({ "aim": [1, 0], "held": { "melee": true } }, int(BC[1]) + 2); m.run({ "aim": [1, 0] }, 1)
	m.w.spawn_projectile({ "team": "e", "owner": fe, "x": 101.0, "y": 1.1, "vx": -10.0, "vy": 0.0, "ttl": 60, "r": 0.2, "dmg": 10.0, "kind": "std" })
	m.run({}, 150, func(_i): return m.count("discCatch") > 0)
	t.check(m.hits(fe).size() >= 5 and m.count("intercept") >= 1 and m.count("discCatch") == 1 and m.p.hp == m.p.max_hp,
		"Level 2 disc hovers and cuts %d times, intercepts a shot, and comes back" % m.hits(fe).size())

	# Gravity Well: the orb opens on an enemy, pulls light enemies in and holds them, drags a heavy one a
	# little, swallows enemy shots, then collapses in a blast
	m = Multi.new(98, ["nova"])
	m.select_sub("well"); m.run({}, 30)
	var s1 := m.enemy("swarmer", 102.5, 0, { "cd": 9999 })
	var s2 := m.enemy("swarmer", 104.4, 0, { "cd": 9999 })
	var br := m.enemy("brute", 105.2, 0, { "cd": 9999 })
	for e in [s1, s2, br]:
		e.hp = 999; e.target = null
	var bx0 := br.x
	m.tap("melee", { "aim": [1, 0] })
	m.run({}, 40, func(_i): return m.count("wellOpen") > 0)
	var wl: Dictionary = m.w.wells[0]
	m.w.spawn_projectile({ "team": "e", "owner": br, "x": wl.x + 2.5, "y": wl.y, "vx": -8.0, "vy": 0.0, "ttl": 60, "r": 0.2, "dmg": 10.0, "kind": "std" })
	m.run({}, 30)
	var held := [s1, s2].all(func(e): return U.hypot(e.x - wl.x, e.y + e.h / 2 - wl.y) < 1.0 and e.state == "launched")
	var dragged := absf(br.x - bx0)
	m.run({}, int(SUB.well.life[0]))
	t.check(m.count("wellOpen") == 1 and held and dragged > 0.2 and dragged < 2.5 and m.count("erase") >= 1 and m.count("wellCollapse") == 1 and m.w.wells.is_empty()
		and m.hits(s1).size() >= 3 and m.p.hp == m.p.max_hp,
		"Gravity Well opens, holds both Swarmers at its centre, drags the Brute %s m, swallows a shot, and collapses" % TestKit.f2(dragged))

	# A press while the orb flies opens the well there; another collapses it early; a boss is not pulled
	m = Multi.new(98, ["nova"])
	m.select_sub("well"); m.run({}, 30)
	m.tap("melee", { "aim": [1, 0] }); m.run({}, 6)
	m.tap("melee", { "aim": [1, 0] })
	var opened: bool = m.count("wellOpen") == 1 and m.w.wells.size() > 0 and m.w.wells[0].x < 98.6 + SUB.well.speed * 10 / 60.0
	m.run({}, 20); m.tap("melee", { "aim": [1, 0] }); m.run({}, 2)
	var collapsed := m.count("wellCollapse") == 1
	m2 = Multi.new(98, ["nova"])
	m2.select_sub("well"); m2.run({}, 30)
	var B := still(m2.enemy("warden", 103.5, 0, { "invuln": 0 })); B.state = "idle"; B.hp = 999
	m2.tap("melee", { "aim": [1, 0] }); m2.run({}, 60)
	t.check(opened and collapsed and B.state == "idle" and B.well_t == 0 and m2.hits(B).size() >= 1, "A press opens the well mid-flight, another collapses it early; a boss is hurt but not pulled")

	# ---- Dodge
	# A backstep with the stick centred, a hop the way the stick points otherwise; no parry
	m = Multi.new(100, ["nova"])
	m.p.facing = 1
	var x0 := m.p.x
	m.tap("parry"); m.run({}, int(DODGE.ticks))
	var back := m.p.x - x0
	m2 = Multi.new(100, ["nova"])
	m2.p.facing = 1; m2.run({ "mx": 1 }, 1)
	var x1 := m2.p.x
	m2.run({ "mx": 1, "held": { "parry": true } }, 1); m2.run({ "mx": 1 }, int(DODGE.ticks) - 1)
	var fwd := m2.p.x - x1
	t.check(m.count("dodge") == 1 and m.count("parryStart") == 0 and back < -1.4 and fwd > 1.8, "Dodge: %s m backstep, %s m forward hop (no parry)" % [TestKit.f2(back), TestKit.f2(fwd)])

	# A shot through him in the untouchable ticks misses (and flies on); a second dodge waits for the cooldown
	m = Multi.new(100, ["nova"])
	m.p.facing = 1
	var sn := still(m.enemy("sniper", 110))
	m.tap("parry"); m.run({}, 8)
	var pr := m.w.spawn_projectile({ "team": "e", "owner": sn, "x": m.p.x + 0.6, "y": m.p.y + 1, "vx": -30.0, "vy": 0.0, "ttl": 30, "r": 0.2, "dmg": 10.0, "kind": "std" })
	m.run({}, 2)
	var through: bool = m.p.hp == m.p.max_hp and not pr.dead
	m.tap("parry")
	var second := m.count("dodge")
	m.run({}, int(DODGE.cd)); m.tap("parry")
	t.check(through and second == 1 and m.count("dodge") == 2, "A shot passes through the dodge (hp %s); the next dodge waits %d ticks" % [str(m.p.hp), DODGE.cd])

	# One dodge per airtime; Sentinel Nova still parries
	m = Multi.new(100, ["nova"])
	m.p.y = 10; m.p.prev_y = 10; m.p.on_ground = false; m.run({}, 1)
	m.tap("parry"); m.run({}, int(DODGE.cd) + 2); m.tap("parry")
	var air := m.count("dodge")
	S.novaKit = "sentinel"
	var sm := Multi.new(100, ["nova"])
	sm.tap("parry")
	S.novaKit = "marksman"
	t.check(air == 1 and sm.count("parryStart") == 1, "One air dodge per airtime; Sentinel Nova still parries")

	# Perfect dodge: an attack arriving in the opening ticks slows the enemies close by (and their shots),
	# and gives Overcharge and ultimate charge. A slowed Swarmer covers about half the ground.
	var walked := func(slow: bool) -> Dictionary:
		var q := Multi.new(100, ["nova"])
		q.p.facing = -1
		var sw := q.enemy("swarmer", 104, 0, { "cd": 9999 })
		sw.target = q.p
		var sn2 := still(q.enemy("sniper", 90))
		if slow:
			q.w.spawn_projectile({ "team": "e", "owner": sn2, "x": q.p.x - 1.2, "y": q.p.y + 1, "vx": 30.0, "vy": 0.0, "ttl": 30, "r": 0.2, "dmg": 10.0, "kind": "std" })
			q.tap("parry")
		q.run({}, 4)
		var sx0 := sw.x
		q.run({}, 40)
		return { "d": absf(sw.x - sx0), "q": q, "sw": sw }
	var wn: Dictionary = walked.call(false)
	var ws: Dictionary = walked.call(true)
	var sp: PlayerSim = ws.q.p
	t.check(ws.q.count("perfectDodge") == 1 and ws.sw.slow_t > 0 and sp.overcharge >= DODGE.over - 1 and sp.ult >= ULT.gain.perfect and sp.hp == sp.max_hp and ws.d < wn.d * 0.7,
		"Perfect dodge: nearby Swarmer slowed (walks %s m vs %s m), Overcharge %s, ultimate +%s" % [TestKit.f2(ws.d), TestKit.f2(wn.d), TestKit.f0(sp.overcharge), TestKit.f0(sp.ult)])

	# He keeps charging his shot through a dodge
	m = Multi.new(100, ["nova"])
	m.run({ "aim": [1, 0], "held": { "fire": true } }, 20)
	var c0 := m.p.charge_t
	m.run({ "aim": [1, 0], "held": { "fire": true, "parry": true } }, 1); m.run({ "aim": [1, 0], "held": { "fire": true } }, 10)
	t.check(m.p.charge_t >= c0 + 10 and m.p.state != "ult", "Charging carries on through a dodge (%s -> %s ticks)" % [str(c0), str(m.p.charge_t)])

	# ---- Automatic lock-on
	# The nearest enemy in sight is locked with no press; R3 switches; holding R3 lets go and pauses it until
	# the next press
	S.lockMode = "auto"
	m = Multi.new(100, ["nova"])
	var la := still(m.enemy("swarmer", 103))
	var lb := still(m.enemy("swarmer", 107))
	m.run({}, 2)
	var first = m.p.lock_t
	m.tap("lock")
	var second_t = m.p.lock_t
	m.run({ "held": { "lock": true } }, int(LOCK.hold) + 2); m.run({}, 1)
	var off = m.p.lock_t
	m.run({}, 30)
	var stay_off: bool = m.p.lock_t == null
	m.tap("lock")
	var back_t = m.p.lock_t
	var farm := Multi.new(100, ["nova"])
	still(farm.enemy("swarmer", 100 + LOCK.auto + 3)); farm.run({}, 5)
	S.lockMode = "manual"
	var man := Multi.new(100, ["nova"])
	still(man.enemy("swarmer", 103)); man.run({}, 5)
	t.check(first == la and second_t == lb and off == null and stay_off and back_t == la and farm.p.lock_t == null and man.p.lock_t == null and m.count("lockOn", func(e): return e.why == "auto") == 1,
		"Automatic lock-on takes the nearest enemy, R3 switches to the next, holding R3 lets go until the next press; none past range, none in manual mode")

	# Automatic lock-on leaves free aim alone and, without it, aims at the target
	S.lockMode = "auto"
	m = Multi.new(100, ["nova"])
	still(m.enemy("swarmer", 104, 0))
	m.run({ "aim": [0, 1] }, 3)
	var up: bool = m.p.aim_y > 0.99 and m.p.lock_t != null
	m.run({}, 3)
	var toward := m.p.aim_x > 0.9
	S.lockMode = "manual"
	t.check(up and toward, "Automatic lock-on: free aim still aims where it points; without it, shots go to the target")

	# ---- Rising attacks
	# Nova's Solar Uppercut (up + melee): he rises, hits three times on the way up and a flare bursts off the fist
	m = Multi.new(100, ["nova"])
	m.p.facing = 1
	var ue := m.enemy("brute", 100.9, 0, { "cd": 9999 }); ue.hp = 999; ue.armor = 0; ue.hitstop = 0
	var uy0 := m.p.y
	var top := { "v": uy0 }
	m.run({ "my": 1, "held": { "melee": true } }, 1)
	m.run({ "my": 1 }, 40, func(_i):
		top.v = maxf(top.v, m.p.y)
		return false)
	var un := m.hits(ue).size()
	t.check(m.log.any(func(q): return q.type == "swing" and q.id == "nova_rise") and top.v - uy0 > 2.4 and un >= 3 and m.count("riseBlast") == 1,
		"Solar Uppercut: rises %s m, %d hits, a flare at the top" % [TestKit.f2(top.v - uy0), un])

	# Once per airtime in the air; the Sentinel kit has its rising attack too
	m = Multi.new(100, ["nova"])
	m.p.y = 8; m.p.prev_y = 8; m.p.on_ground = false; m.run({}, 1)
	m.run({ "my": 1, "held": { "melee": true } }, 1); m.run({ "my": 1 }, 34); m.run({ "my": 1, "held": { "melee": true } }, 1); m.run({ "my": 1 }, 2)
	var airs := m.log.filter(func(q): return q.type == "swing" and q.id == "nova_rise").size()
	S.novaKit = "sentinel"
	var sr := Multi.new(100, ["nova"])
	sr.run({ "my": 1, "held": { "melee": true } }, 1)
	S.novaKit = "marksman"
	t.check(airs == 1 and sr.p.move_id == "nova_rise" and Tune.C.MOVES.nova_rise.has("rise"), "Rising attack once per airtime; Sentinel Nova (%s) has it too" % str(sr.p.move_id))

	# ---- Ultimates
	# The bar fills from damage dealt and taken, kills and perfect moves, and says when it is full
	m = Multi.new(100, ["nova"])
	var ke := still(m.enemy("swarmer", 101.4)); ke.hp = 3
	m.tap("melee", { "aim": [1, 0] }); m.run({}, 30)
	var dealt := m.p.ult
	var f := Multi.new(100, ["nova"])
	f.w.spawn_projectile({ "team": "e", "owner": null, "x": f.p.x + 1.5, "y": f.p.y + 1, "vx": -20.0, "vy": 0.0, "ttl": 20, "r": 0.2, "dmg": 10.0, "kind": "std" }); f.run({}, 6)
	var taken := f.p.ult
	m.p.ult = ULT.max - 0.5
	still(m.enemy("post", 101.2)); m.run({}, 20); m.tap("melee", { "aim": [1, 0] }); m.run({}, 30)
	t.check(dealt > 0 and m.count("kill") == 1 and absf(taken - 10 * ULT.gain.taken) < 0.01 and m.count("ultReady") == 1 and m.p.ult == ULT.max,
		"Ultimate bar: +%s from a kill with the Scatter, +%s from a 10-damage hit; \"ready\" at %d" % [TestKit.f1(dealt), TestKit.f1(taken), ULT.max])

	# Both triggers together with a full bar starts it; not with a partial bar (the dodge and shot happen);
	# holding fire to charge and then pulling LT is a dodge, not the ultimate
	var part := Multi.new(100, ["nova"])
	part.p.ult = 60; part.run({ "aim": [1, 0], "held": { "parry": true, "fire": true } }, 1); part.run({}, 2)
	var late := Multi.new(100, ["nova"])
	late.p.ult = ULT.max; late.run({ "aim": [1, 0], "held": { "fire": true } }, 30); late.run({ "aim": [1, 0], "held": { "fire": true, "parry": true } }, 1)
	m = Multi.new(100, ["nova"])
	m.p.ult = ULT.max
	m.run({ "aim": [1, 0], "held": { "parry": true } }, 1); m.run({ "aim": [1, 0], "held": { "parry": true, "fire": true } }, 1)
	t.check(part.w.ult_cast == null and part.count("dodge") == 1 and late.p.state == "dodge" and late.w.ult_cast == null and m.count("ultCast") == 1 and m.p.state == "ult" and m.p.ult == 0,
		"LT + RT within a few ticks with a full bar calls the ultimate; a partial bar or a late second trigger does not")

	# The call freezes the world; the ultimate itself freezes enemies and their shots but not players
	m = Multi.new(100, ["nova", "nova"])
	var fz := m.enemy("swarmer", 106, 0, { "cd": 9999 }); fz.hp = 999
	var fpr := m.w.spawn_projectile({ "team": "e", "owner": fz, "x": 110.0, "y": 1.0, "vx": -10.0, "vy": 0.0, "ttl": 200, "r": 0.2, "dmg": 10.0, "kind": "std" })
	m.run({}, 2); m.p.ult = ULT.max
	m.run({ 0: { "held": { "ult": true } } }, 1)
	var sx := fz.x
	var px: float = fpr.x
	var ex: float = m.ps[1].x
	m.run({ 1: { "mx": 1 } }, int(ULT.cast) - 4)
	var cast_frozen: bool = fz.x == sx and fpr.x == px and m.ps[1].x == ex and m.w.ult_cast.phase == "cast"
	m.run({ 1: { "mx": 1 } }, 10)
	var run_frozen: bool = m.w.ult_cast != null and m.w.ult_cast.phase == "run" and fz.x == sx and fpr.x == px and m.ps[1].x > ex + 0.3
	t.check(cast_frozen and run_frozen, "The call freezes everyone; the ultimate freezes enemies and their shots while the other players move")

	# Supernova: he rises, the beam runs through walls and pulses through everything in line, then the nova
	m = Multi.new(22, ["nova"])
	m.p.ult = ULT.max
	var na := still(m.enemy("brute", 27))
	var nb := still(m.enemy("brute", 35.5))   # inside the ledge, past its wall at x = 32
	na.hp = 9999; nb.hp = 9999
	var ny0 := m.p.y
	m.run({ "aim": [1, 0], "held": { "ult": true } }, 1); m.run({ "aim": [1, 0] }, int(ULT.cast) + 30)
	var rose := m.p.y - ny0
	m.run({ "aim": [1, 0] }, int(ULT.nova.end))
	var pulses := int(floor(ULT.nova.beam / ULT.nova.pulse))
	var hna := m.hits(na).size()
	var hnb := m.hits(nb).size()
	t.check(rose > 1.2 and hna >= pulses and hnb >= pulses - 1 and m.count("ultNova") == 1 and m.count("ultEnd") == 1 and m.p.state == "normal" and m.p.mercy > 0 and m.w.ult_cast == null
		and na.armor == 0 and m.p.ult == 0,
		"Supernova: rises %s m, %d and %d hits (one past a wall), armor stripped, the nova, then back to normal" % [TestKit.f2(rose), hna, hnb])

	# Team ultimate: a teammate with a full bar joins during the call; both run at the team power, then the
	# team finisher hits every enemy on screen. Nova + Nova is Binary Star.
	m = Multi.new(100, ["nova", "nova"])
	var te := still(m.enemy("brute", 108)); te.hp = 9999
	m.p.ult = ULT.max; m.ps[1].ult = ULT.max
	m.run({ 0: { "aim": [1, 0], "held": { "ult": true } } }, 1)
	m.run({ 0: { "aim": [1, 0] } }, 10)
	m.run({ 0: { "aim": [1, 0] }, 1: { "held": { "parry": true } } }, 1); m.run({ 0: { "aim": [1, 0] }, 1: { "held": { "parry": true, "fire": true } } }, 1)
	var joined: bool = m.count("ultJoin") == 1 and m.ps[1].state == "ult"
	m.run({ 0: { "aim": [1, 0] } }, int(ULT.cast + ULT.join + ULT.nova.end) + 80, func(_i): return m.w.ult_cast == null)
	var tfs := m.log.filter(func(q): return q.type == "teamFinisher")
	var rus := m.log.filter(func(q): return q.type == "ultRun")
	t.check(joined and rus.size() and rus[0].team and rus[0].name == "Binary Star" and tfs.size() and tfs[0].members.size() == 2
		and m.hits(te).any(func(h): return h.dmg >= ULT.team.dmg * 2 * 0.99) and m.w.ult_cast == null and m.ps.all(func(q): return q.state == "normal"),
		"Team ultimate: %s, joined in the call, finisher on every enemy on screen" % (rus[0].name if rus else "?"))

	# A teammate without a full bar can't join; a new ultimate can't start while one plays out; bosses take less
	m = Multi.new(100, ["nova", "nova"])
	var wb := m.enemy("warden", 110, 0, { "invuln": 0 })
	wb.state = "idle"; wb.cd = 9999; wb.hitstop = 1000000000; wb.hp = 9999; wb.armor = 0
	m.p.ult = ULT.max; m.ps[1].ult = 50
	m.run({ 0: { "aim": [1, 0], "held": { "ult": true } } }, 1); m.run({ 0: { "aim": [1, 0] }, 1: { "held": { "ult": true } } }, 2)
	var no_join := m.count("ultJoin") == 0
	m.run({ 0: { "aim": [1, 0] } }, int(ULT.cast) + 20); m.ps[1].ult = ULT.max; m.run({ 0: { "aim": [1, 0] }, 1: { "held": { "ult": true } } }, 1)
	var no_second := m.count("ultCast") == 1
	m.run({ 0: { "aim": [1, 0] } }, 200)
	var per := m.hits(wb).filter(func(h): return h.source == "blast")
	t.check(no_join and no_second and per.size() > 0 and absf(per[0].dmg - ULT.nova.dmg * ULT.boss) < 0.01,
		"No joining without a full bar; no second ultimate mid-way; a boss takes %d%% (%s a pulse)" % [int(ULT.boss * 100), TestKit.f2(per[0].dmg) if per else "?"])
