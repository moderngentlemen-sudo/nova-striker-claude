# enemy-test.mjs: the Drone, Mortar and Charger in the real simulation, and the kill line. The two parry checks
# use Nova's Pass 1 Sentinel kit, which parries (the prototype uses Echo; his kit comes with milestone 2).
extends RefCounted

func run(t: TestKit) -> void:
	# Drone: climbs above and beside the player, keeps station, and fires parryable shots
	var d := TestKit.setup(100)
	var dr := d.spawn("drone", 106, 2)
	d.run({}, 120)
	var above := dr.y - d.p.y
	var side := absf(dr.x - d.p.x)
	d.until({}, 160, func(): d.p.mercy = 60; d.p.hp = d.p.max_hp; return false)
	var shots := d.count("enemyShot", func(s): return s.e == dr) + d.count("telegraph", func(s): return s.e == dr and s.cat == "standard")
	t.check(above > 2.4 and above < 4.6 and side > 3.5 and side < 7.5 and shots >= 1,
		"Drone hovers %s m up and %s m to the side, and attacks (%d telegraphs/shots)" % [TestKit.f1(above), TestKit.f1(side), shots])

	# Drone hit: it flinches in the air instead of dropping, then keeps flying
	d = TestKit.setup(100)
	dr = d.spawn("drone", 104, 3.2, { "cd": 9999 })
	d.run({}, 30)
	var y0 := dr.y
	d.w.spawn_hitbox({ "owner": d.p, "team": "p", "x0": dr.x - 1, "x1": dr.x + 1, "y0": dr.y - 1, "y1": dr.y + 1, "dmg": 1.0, "poise": 5.0, "kb": [4.0, 12.0], "launcher": true, "instance": d.w.new_instance() })
	d.run({}, 1)
	var st := dr.state
	d.run({}, 60)
	t.check(st == "hitstun" and dr.y > 1.5 and dr.state != "launched" and not dr.dead, "Hit drone flinches (%s) and stays airborne (y %s -> %s)" % [st, TestKit.f1(y0), TestKit.f1(dr.y)])

	# Mortar: telegraphs, lobs a shell at where the player stands, and the burst hits a player who stays put
	d = TestKit.setup(100)
	var m := d.spawn("mortar", 110, 0)
	var shot := d.until({}, 200, func(): return d.count("mortarShot") > 0)
	var ev = d.first("mortarShot")
	var blast := d.until({}, 120, func(): d.p.mercy = 0; return d.count("enemyBlast") > 0)
	var b = d.first("enemyBlast")
	t.check(shot and ev != null and absf(ev.x - 100) < 0.6 and d.count("telegraph", func(q): return q.e == m and q.cat == "unblockable") == 1 and blast
		and absf(b.x - 100) < 1.2 and d.count("playerHit") >= 1,
		"Mortar marks x=%s, bursts at x=%s and hits the player who stood still" % [TestKit.f1(ev.x) if ev else "?", TestKit.f1(b.x) if b else "?"])

	# Mortar: a player who walks out of the marker is not hit; a parry does not help against the burst
	d = TestKit.setup(100)
	d.spawn("mortar", 110, 0)
	d.until({}, 200, func(): return d.count("mortarShot") > 0)
	d.run({ "mx": 1 }, 30); d.run({}, 70)
	var dodged := d.count("enemyBlast") == 1 and d.count("playerHit") == 0
	Tune.settings.novaKit = "sentinel"
	var e2 := TestKit.setup(100)
	e2.spawn("mortar", 110, 0)
	e2.until({}, 200, func(): return e2.count("mortarShot") > 0)
	var fly: int = e2.first("mortarShot").ticks
	e2.run({}, fly - 10); e2.run({ "held": { "parry": true } }, 1); e2.run({}, 20)
	Tune.settings.novaKit = "marksman"
	t.check(dodged and e2.count("parryFail") >= 1 and e2.count("playerHit") >= 1, "Walking out avoids the burst; parrying it fails")

	# Mortar shells burst on walls too (cover works)
	d = TestKit.setup(112.2)
	d.spawn("mortar", 104, 0)
	d.w.projectiles.clear()
	d.w.spawn_projectile({ "team": "e", "owner": null, "x": 111.0, "y": 1.2, "vx": 12.0, "vy": 0.0, "gravity": 0.0, "r": 0.3, "dmg": 0.0, "heavy": true, "kind": "mortar", "ttl": 60,
		"blast": Tune.C.MORTAR.blast.duplicate() })
	d.run({}, 20)
	t.check(d.count("enemyBlast") == 1, "A shell that hits a wall bursts there")

	# Charger: heavy telegraph, then a charge that hits a player standing in its path
	d = TestKit.setup(100)
	var c := d.spawn("charger", 108, 0, { "cd": 0 })
	d.until({}, 150, func(): return d.count("playerHit") > 0)
	t.check(d.count("telegraph", func(q): return q.e == c and q.cat == "heavy") >= 1 and d.count("chargeStart") >= 1 and d.count("playerHit", func(q): return q.heavy) == 1,
		"Charger telegraphs a heavy attack, charges, and hits the player (%d hit)" % d.count("playerHit"))

	# A perfect parry stops the charge and leaves the Charger dazed
	Tune.settings.novaKit = "sentinel"
	d = TestKit.setup(100)
	c = d.spawn("charger", 108, 0, { "cd": 0 })
	d.until({}, 150, func(): return c.state == "charge" and c.x - d.p.x < 1.76 + 0.45)
	d.run({ "held": { "parry": true } }, 1); d.run({}, 12)
	Tune.settings.novaKit = "marksman"
	var par = d.first("parry")
	t.check(par != null and par.perfect and c.state in ["dazed", "stagger"] and d.count("playerHit") == 0, "Perfect parry negates the charge; Charger is %s" % c.state)

	# Crashing into a wall dazes it; its armor plate needs an armor-breaking hit
	d = TestKit.setup(111.3)
	c = d.spawn("charger", 104, 0, { "cd": 0 })
	d.until({}, 150, func(): return c.state == "charge")
	d.until({}, 60, func(): d.p.y = 6; d.p.vy = 0; d.p.x = 111.3; return c.state == "dazed")
	var crash := d.count("chargeCrash")
	var dazed := c.state
	d.p.y = 0
	d.w.spawn_hitbox({ "owner": d.p, "team": "p", "x0": c.x - 1, "x1": c.x + 1, "y0": 0.0, "y1": 2.0, "dmg": 2.0, "poise": 5.0, "kb": [0.0, 0.0], "instance": d.w.new_instance() })
	d.run({}, 1)
	var hp1 := c.hp
	var armor1 := c.armor
	d.w.spawn_hitbox({ "owner": d.p, "team": "p", "x0": c.x - 1, "x1": c.x + 1, "y0": 0.0, "y1": 2.0, "dmg": 2.0, "poise": 5.0, "kb": [0.0, 0.0], "armorBreak": true, "instance": d.w.new_instance() })
	d.run({}, 1)
	t.check(crash == 1 and dazed == "dazed" and armor1 == 1 and absf(hp1 - (12 - 2 * 0.3)) < 1e-9 and c.armor == 0,
		"Charger crashes into the wall and is dazed; plain hits do 30%% (hp %s), an armor-break hit strips the plate" % TestKit.f1(hp1))

	# Enemies that fall out of the level die (so an encounter can still finish)
	d = TestKit.setup(100)
	var sw := d.spawn("swarmer", 100, -12)
	d.run({}, 2)
	t.check(sw.dead and d.count("kill", func(q): return q.e == sw) == 1, "A Swarmer below the kill line dies")
