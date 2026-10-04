# v8-test.mjs, Nova's half: the slower charge and Level 4 beam, the hard-light Aegis and Overcharge, the
# close-range bracer combo and the ground pound. (Echo's sniper rifle, deflect and moveset come with his kit;
# the Aegis ally check uses a second Nova where the prototype has Echo.)
extends RefCounted

func setup(x := 99.0, y := 0.0) -> TestKit.Driver:
	return TestKit.setup(x, "nova", true, y)

func last(d: TestKit.Driver, type: String):
	for i in range(d.log.size() - 1, -1, -1):
		if d.log[i].type == type:
			return d.log[i]
	return null

func dmg_on(log: Array, e) -> float:
	var s := 0.0
	for h in log:
		if h.type == "hit" and h.e == e:
			s += h.dmg
	return s

func shoot(w: World, from, x: float, y: float, vx: float, vy: float, o := {}) -> void:
	var pr := { "team": "e", "owner": from, "x": x, "y": y, "vx": vx, "vy": vy, "ttl": 120, "r": 0.2, "dmg": 10.0, "kind": "std" }
	pr.merge(o, true)
	w.spawn_projectile(pr)

func run(t: TestKit) -> void:
	Tune.settings.lockOn = true; Tune.settings.dashCharge = true
	var M: Dictionary = Tune.C.MARKSMAN
	var C: Array = M.charge
	var L4: float = M.beam.at
	var A: Dictionary = Tune.C.AEGIS

	# ---- The Level 4 beam
	# Slower charge: levels at the new marks, then Level 4
	var d := setup(99)
	var stages := []
	for n in [C[0] - 2, C[1] - C[0], C[2] - C[1], 4, L4 - C[2] - 2]:
		d.run({ "aim": [1, 0], "held": { "fire": true } }, int(n)); stages.append(d.p.charge_stage())
	var lv := "".join(d.log.filter(func(e): return e.type == "chargeLevel").map(func(e): return str(e.level)))
	t.check(" > ".join(stages) == "charging > L1 > L2 > perfect > L4" and lv == "1234", "Charge: %s (levels at %d, %d, %d and %d ticks)" % [" > ".join(stages), C[0], C[1], C[2], L4])

	# Level 4 release: a sustained beam that pulses through every enemy in line, stops at walls, and ends
	d = setup(0)
	var a := d.dummy("brute", 6)
	var b := d.dummy("shield", 10)
	var hid := d.dummy("swarmer", 35)   # behind the ledge wall at x = 32
	a.hp = 1e9; b.hp = 1e9
	d.run({ "aim": [1, 0], "held": { "fire": true } }, int(L4) + 2); d.run({ "aim": [1, 0] }, 1)
	var began := d.p.state == "beam" and d.count("beamStart") == 1
	d.w.spawn_projectile({ "team": "e", "owner": b, "x": 14.0, "y": 1.1, "vx": -12.0, "vy": 0.0, "ttl": 60, "r": 0.2, "dmg": 10.0, "kind": "std" })
	d.run({ "aim": [1, 0] }, int(M.beam.ticks) + 4)
	var pulses := int(floor(M.beam.ticks / M.beam.pulse))
	var ha := d.count("hit", func(h): return h.e == a)
	t.check(began and ha >= pulses - 1 and d.count("hit", func(h): return h.e == b) >= pulses - 1 and d.count("hit", func(h): return h.e == hid) == 0
		and d.count("erase") >= 1 and d.count("beamEnd") == 1 and d.p.state == "normal" and d.p.hp == d.p.max_hp,
		"Beam: %d pulses on each enemy in line (%s damage), none through the wall, enemy shot erased, ends after %d ticks" % [ha, TestKit.f1(dmg_on(d.log, a)), int(M.beam.ticks)])

	# The beam follows the aim at a limited turn rate; a dash cuts it short; a hit ends it
	d = setup(0)
	d.run({ "aim": [1, 0], "held": { "fire": true } }, int(L4) + 2); d.run({ "aim": [1, 0] }, 1)
	d.run({ "aim": [0, 1] }, 10)
	var ang := atan2(d.p.beam.dy, d.p.beam.dx)
	d.run({ "aim": [0, 1], "mx": 1, "held": { "dash": true } }, 1)
	var cut: bool = d.p.state == "dash" and d.p.beam == null and last(d, "beamEnd").why == "cancel"
	var h2 := setup(0)
	h2.run({ "aim": [1, 0], "held": { "fire": true } }, int(L4) + 2); h2.run({ "aim": [1, 0] }, 3)
	h2.w.spawn_hitbox({ "owner": null, "team": "e", "x0": h2.p.x - 1, "x1": h2.p.x + 1, "y0": 0.0, "y1": 2.0, "dmg": 8.0, "kb": [3.0, 2.0], "instance": h2.w.new_instance() })
	h2.run({}, 1)
	t.check(absf(ang - 10 * M.beam.turn) < 0.02 and cut and h2.p.beam == null and last(h2, "beamEnd").why == "hit",
		"Beam turns %s rad in 10 ticks toward a straight-up aim; a dash cancels it; a hit ends it" % TestKit.f2(ang))

	# Attachment flavours: Prism bounces off the first wall, Arc bursts where it lands, Volley sheds darts
	var beam_with := func(attach: String) -> Dictionary:
		var q := setup(26)
		q.p.attachment = attach
		var darts := {}
		q.run({ "aim": [1, 0], "held": { "fire": true } }, int(L4) + 2)
		for i in 30:
			q.run({ "aim": [1, 0] })
			for pr in q.w.projectiles:
				if pr.kind == "dart":
					darts[pr] = true
		return { "d": q, "darts": darts.size() }
	var pr_: Dictionary = beam_with.call("prism")
	var ar: Dictionary = beam_with.call("arc")
	var vo: Dictionary = beam_with.call("volley")
	t.check(pr_.d.p.beam.segs.size() == 2 and ar.d.count("blast") >= 2 and vo.darts >= 2,
		"Prism beam bounces (%d segments), Arc beam bursts at the wall (%d), Volley beam sheds darts" % [pr_.d.p.beam.segs.size(), ar.d.count("blast")])

	# ---- The Aegis and Overcharge
	# Raise: an enemy shot stops at the hard light; Nova takes nothing; the damage becomes Overcharge
	d = setup(99)
	var dr := d.dummy("drone", 106, 3)
	d.run({ "held": { "sig": true } }, 1); d.run({}, 2)
	shoot(d.w, dr, 104, 1.2, -14, 0, { "dmg": 12.0 })
	d.run({}, 30)
	var hit = last(d, "aegisHit")
	t.check(d.count("aegisOn") == 1 and hit != null and hit.dmg == 12 and d.p.hp == d.p.max_hp and d.p.state != "hitstun" and absf(d.p.overcharge - 12 * A.over.perDmg) < 1e-9 and hit.x > d.p.x + 1,
		"Aegis stops a shot at its surface (x %s m out), Nova unhurt, Overcharge %s" % [TestKit.f2(hit.x - d.p.x) if hit else "?", TestKit.f1(d.p.overcharge)])

	# Blocks everything for allies inside too, including an unblockable blast and a Charger's charge (which rebounds)
	var w := World.new()
	w.enemies = []
	var n := w.add_player("a", "nova")
	var q := w.add_player("b", "nova")
	n.x = 99; q.x = 99.8; n.y = 0; q.y = 0
	var prev := [{}, {}]
	var log := []
	var run2 := func(o0: Dictionary, k: int) -> void:
		for i in k:
			var c0 := Cmd.make(prev[0], o0)
			var c1 := Cmd.make(prev[1], {})
			prev = [c0.held.duplicate(), c1.held.duplicate()]
			w.step({ 0: c0, 1: c1 })
			log.append_array(w.events); w.events.clear()
	run2.call({}, 5); n.mercy = 0; q.mercy = 0
	run2.call({ "held": { "sig": true } }, 1); run2.call({}, 1)
	w.explode({ "owner": null, "team": "e", "x": 99.4, "y": 0.2, "spec": { "r": 2.0, "dmg": 14.0 } })
	var c := EnemySim.create("charger", 102.5, 0, { "cd": 0 }, w)
	w.enemies.append(c)
	run2.call({}, 150)
	var blasts := log.filter(func(e): return e.type == "aegisHit" and e.dmg == 14).size()
	t.check(blasts == 1 and n.hp == n.max_hp and q.hp == q.max_hp and log.filter(func(e): return e.type == "playerHit").is_empty()
		and (c.state == "dazed" or log.any(func(e): return e.type == "chargeCrash")),
		"An unblockable blast on both players counts once (%d); the Charger rebounds off the hard light; nobody inside is hurt" % blasts)

	# Broken by damage it shatters: a burst that hits enemies around him, then a cooldown; pressed again it detonates
	d = setup(99)
	var s := d.dummy("swarmer", 101.4)
	dr = d.dummy("drone", 106, 3)
	d.run({ "held": { "sig": true } }, 1); d.run({}, 1)
	for i in 6:
		shoot(d.w, dr, 104, 1.2, -14, 0, { "dmg": 14.0 }); d.run({}, 10)
	var broke := d.count("aegisOff", func(e): return e.why == "break") == 1 and d.p.aegis == null and d.p.aegis_cd > 0
	var burst := d.count("hit", func(h): return h.e == s and h.source == "blast") == 1
	d.run({ "held": { "sig": true } }, 1); d.run({}, 1)
	var cooling := d.p.aegis == null
	var d2 := setup(99)
	var s2 := d2.dummy("swarmer", 101.2)
	d2.run({ "held": { "sig": true } }, 1); d2.run({}, 20); d2.run({ "held": { "sig": true } }, 1); d2.run({}, 2)
	t.check(broke and burst and cooling and d2.count("aegisOff", func(e): return e.why == "detonate") == 1 and d2.count("hit", func(h): return h.e == s2) == 1,
		"Aegis breaks after %d hits and shatters into a burst that hits the enemy beside him; cooldown holds; a second press detonates it" % d.count("aegisHit"))

	# Overcharge: charges faster, and a charged release hits harder and spends it
	d = setup(99)
	var e := d.dummy("brute", 104)
	e.hp = 1e9; e.armor = 0
	d.run({ "aim": [1, 0], "held": { "fire": true } }, int(C[1]) + 1); d.run({ "aim": [1, 0] }, 20)
	var top := func(l: Array) -> float:
		var m := -INF
		for hh in l:
			if hh.type == "hit" and hh.e == e:
				m = maxf(m, hh.dmg)
		return m
	var plain: float = top.call(d.log)
	d.p.overcharge = 100; d.p.over_t = 1000000000; d.p.focus = 0; d.log.clear()
	var t1 := 0
	for i in 200:
		d.run({ "aim": [1, 0], "held": { "fire": true } })
		if d.p.charge_t >= C[1]:
			t1 = i + 1
			break
	d.run({ "aim": [1, 0] }, 20)
	var over: float = top.call(d.log)
	t.check(t1 < C[1] * 0.7 and absf(over / plain - A.over.dmg) < 0.05 and d.p.overcharge == 100 - A.over.cost,
		"Overcharge: level 2 in %d ticks instead of %d; the release hits %sx and spends %d" % [t1, C[1], TestKit.f2(over / plain), A.over.cost])

	# ---- Close range
	# Near an enemy, melee is the bracer combo: backhand, elbow, blast punch; further off it is the Recoil Burst
	d = setup(99)
	var post := d.dummy("post", 100.3)
	var moves := []
	for i in 60:
		d.run({ "held": { "melee": i % 7 == 0 } }, 1)
		if d.p.move_id != null and not moves.has(d.p.move_id):
			moves.append(d.p.move_id)
	var far := setup(99)
	far.dummy("swarmer", 103); far.run({ "held": { "melee": true } }, 1); far.run({}, 2)
	t.check(",".join(moves) == "nova_k1,nova_k2,nova_k3" and d.count("blast") == 1 and d.count("burst") == 0 and far.count("burst") == 1 and far.p.move_id == null,
		"Close: %s with a blast punch (%s damage); at 4 m the same button fires a Recoil Burst" % [" > ".join(moves), TestKit.f1(dmg_on(d.log, post))])

	# ---- The ground pound
	# A quick one scatters the enemies around the landing; held, he hangs in the air while it charges through
	# three levels, and the full one is wider and breaks armor
	var P: Dictionary = Tune.C.POUND
	d = setup(100, 4); d.p.on_ground = false; d.p.vy = 0
	var l := d.dummy("swarmer", 98.3)
	var r := d.dummy("swarmer", 101.8)
	l.hp = 1e9; r.hp = 1e9
	d.run({ "my": -1, "held": { "melee": true } }, 1); d.run({}, 40)
	var land = last(d, "poundLand")
	var quick: bool = d.count("poundStart") == 1 and land != null and land.level == 0 and l.state == "launched" and r.state == "launched" and l.vx < -5 and r.vx > 5 and d.count("burst") == 0
	var bd := setup(100, 7); bd.p.on_ground = false; bd.p.vy = 0
	var brute := bd.dummy("brute", 102.6)
	var farw := bd.dummy("swarmer", 96.4)
	brute.hp = 1e9; farw.hp = 1e9
	var y0 := bd.p.y
	bd.run({ "my": -1, "held": { "melee": true } }, int(P.charge[2]) + 4)
	var hang := y0 - bd.p.y
	var levels := "".join(bd.log.filter(func(q2): return q2.type == "poundLevel").map(func(q2): return str(q2.level)))
	bd.run({ "my": -1 }, 40)
	var big = last(bd, "poundLand")
	t.check(quick and hang < 2.5 and levels == "123" and big != null and big.level == 3 and big.r > P.land[0].r + 1 and brute.armor < 3 and bd.count("hit", func(hh): return hh.e == farw) == 1,
		"Nova's pound: the quick one launches both swarmers outward; held %d ticks he sinks only %s m through levels %s; the full one reaches %s m and breaks armor" % [int(P.charge[2]) + 4, TestKit.f2(hang), levels, TestKit.f1(big.r) if big else "?"])

	# Holding down to fast-fall and then pressing the secondary is a pound, not a Velocity Break; a diagonal aim
	# in the air is still Nova's Recoil Burst
	d = setup(100, 14); d.p.on_ground = false; d.p.vy = 0
	d.run({ "my": -1 }, 24)
	var ff := d.p.fast_fall and d.p.vy < -18
	d.run({ "my": -1, "held": { "melee": true } }, 1)
	var st := d.p.state
	var u := setup(100, 14); u.p.on_ground = false; u.p.vy = 0; u.run({}, 4)
	u.run({ "aim": [0.55, -0.83], "held": { "melee": true } }, 1)
	t.check(ff and st == "pound" and u.p.state != "pound" and u.count("burst") == 1, "Fast-falling + secondary starts the pound (%s); a diagonal-down secondary is still the Recoil Burst" % st)
