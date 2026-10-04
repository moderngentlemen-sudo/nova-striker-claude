# sim-test.mjs: Nova's jump height, dash distance, double jump and Velocity Break; an arena run and a ranged
# run without errors. (The prototype runs the arena with Echo; the port has Nova until Echo's kit lands.)
extends RefCounted

func run(t: TestKit) -> void:
	var w := World.new()
	var p := w.add_player("test", "nova")
	var d := TestKit.Driver.new(w)
	p.x = 2; p.y = 0
	d.run({}, 20)
	var max_y := 0.0
	d.run({ "held": { "jump": true } })
	for i in 60:
		d.run({ "held": { "jump": true } }); max_y = maxf(max_y, p.y)
	t.check(absf(max_y - 3.2) < 0.25, "nova jump height %s m (target 3.2)" % TestKit.f2(max_y))
	d.run({}, 60)
	var x0 := p.x
	d.run({ "held": { "dash": true }, "mx": 1 }); d.run({}, 30)
	var dash_dist := p.x - x0
	t.check(dash_dist > 3.5 and dash_dist < 7.5, "nova ground dash travel %s m" % TestKit.f2(dash_dist))
	d.run({}, 30); p.x = 5; d.run({}, 5)
	var top := 0.0
	d.run({ "held": { "jump": true } }); d.run({ "held": { "jump": true } }, 20); d.run({}, 2); d.run({ "held": { "jump": true } })
	for i in 60:
		d.run({ "held": { "jump": true } }); top = maxf(top, p.y)
	t.check(top > 5.0, "nova jump + double jump reaches %s m (perches at 5.4)" % TestKit.f2(top))
	d.run({}, 60); p.x = 2; d.run({}, 5)
	d.run({ "held": { "dash": true }, "mx": 1 }); d.run({ "mx": 1 }, 3); d.run({ "held": { "melee": true } })
	t.check(p.state == "vb", "nova melee during dash starts Velocity Break (state=%s, tier=%s)" % [p.state, str(p.vb_info.tier if p.vb_info else null)])
	d.run({}, 3)
	t.check(absf(p.vx) < 0.5, "nova Velocity Break hard stop (vx=%s)" % TestKit.f2(p.vx))

	# Arena: walk in, gates close, fight with scripted mashing
	var aw := World.new()
	var ap := aw.add_player("test", "nova")
	var ad := TestKit.Driver.new(aw)
	aw.teleport("arena"); ad.run({}, 10)
	var spawned := false
	var kills := 0
	var hits := 0
	for tk in 60 * 90:
		var e = null
		for q in aw.enemies:
			if q.zone == "arena" and not q.dead:
				e = q
				break
		var o := { "mx": 1.0 }
		if e != null:
			var dx: float = e.x - ap.x
			o.mx = float(U.sgn(dx)) if absf(dx) > 1.4 else 0.0
			o.held = { "melee": tk % 8 < 2, "parry": e.state == "windup" and e.st > 10 and tk % 3 == 0, "fire": tk % 30 < 20 }
			if e.y > ap.y + 2 and tk % 40 < 2:
				o.held.jump = true
		ad.run(o)
		for ev in ad.log:
			if ev.type == "kill": kills += 1
			if ev.type == "hit": hits += 1
		ad.log.clear()
		if aw.arena.state != "idle":
			spawned = true
		if ap.state == "downed" or ap.state == "dead":
			ap.hp = ap.max_hp; ap.state = "normal"
	t.check(spawned, "arena encounter triggered (state=%s; hits=%d kills=%d)" % [aw.arena.state, hits, kills])

	# Nova ranged: shots intercept turret projectiles in the gym; charge levels
	var rw := World.new()
	var rp := rw.add_player("test", "nova")
	var rd := TestKit.Driver.new(rw)
	rp.x = 24; rp.y = 0
	var intercepts := 0
	var rails := 0
	for tk in 60 * 20:
		var fire := tk % 10 < 1
		rd.run({ "aim": [1, 0.1], "held": { "fire": (tk % 140 < 125) if tk > 600 else fire } })
		for ev in rd.log:
			if ev.type == "intercept": intercepts += 1
			if ev.type == "shot" and ev.level == (3 if ev.has("attach") else 2): rails += 1
		rd.log.clear()
		if rp.state == "downed":
			rp.hp = rp.max_hp; rp.state = "normal"
	t.check(rails > 0, "fully charged shot (a level 3 attachment) fires after holding fire (%d, intercepts %d)" % [rails, intercepts])
