# The three routes: skyline-test.mjs (the Skyline Relay run, its encounters, the Relay Gate, a wipe), v12-test.mjs
# (route scoping, breakables, power-ups, lift pads) and a run along each whole route. The prototype checks the
# Helix Foundry and Undercity Descent with a bot following a player; until the bots are ported, Nova runs them
# with the same simple runner the Skyline check uses.
extends RefCounted

func killall(w: World, p: PlayerSim, enc: String) -> void:
	for e in w.enemies:
		if e.dead or (enc != "" and e.enc != enc):
			continue
		e.hp = 0.001; e.armor = 0
		w.spawn_hitbox({ "owner": p, "team": "p", "x0": e.x - 1, "x1": e.x + 1, "y0": e.y - 1, "y1": e.y + 3, "dmg": 99.0, "poise": 0.0, "kb": [0.0, 0.0], "armorBreak": true, "instance": w.new_instance() })

func zone_setup(zone: String) -> TestKit.Driver:
	var w := World.new()
	w.enemies = []
	var p := w.add_player("test", "nova")
	w.teleport(zone)
	var d := TestKit.Driver.new(w)
	d.p = p
	d.run({}, 5)
	return d

# A simple runner: hold right; jump at a ledge edge or a wall; double jump near the top of the jump; shoot a
# breakable piece in the way. Encounters
# are cleared first (traversal only), gates left open.
func run_route(zone: String, end_x: float, max_secs := 60) -> Dictionary:
	var d := zone_setup(zone)
	var p := d.p
	var lv := d.w.level
	for S in d.w.encounters:
		S.state = "cleared"
	p.mercy = 1000000000
	var jump_hold := 0
	var doubled := false
	var falls := 0
	var t := 0
	while t < 60 * max_secs and p.x < end_x:
		var wall := lv.point_in_solid(p.x + 0.9, p.y + 0.4) or lv.point_in_solid(p.x + 0.9, p.y + 1.4)
		var gap := p.on_ground and lv.ground_below(p.x + 1.1, p.y + 0.2) < p.y - 0.5
		var o := { "mx": 1.0, "held": {} }
		# A breakable piece in the way (the glass panes are too tall to jump): shoot it
		if lv.breakable_at(p.x + 1.4, p.y + 1.0, 0.6) != null and t % 2 == 0:
			o.aim = [1.0, 0.0]; o.held.fire = true
		if p.on_ground:
			doubled = false
			if (wall or gap) and jump_hold == 0:
				jump_hold = 14
		elif not doubled and jump_hold == 0 and p.vy < 2 and (wall or lv.ground_below(p.x + 1.5, p.y) < p.y - 3):
			doubled = true; jump_hold = 12
		if jump_hold > 0:
			o.held.jump = jump_hold > 1; jump_hold -= 1
		var before := d.count("recall")
		d.run(o, 1)
		if d.count("recall") > before:
			falls += 1
		t += 1
	return { "x": p.x, "falls": falls, "secs": t / 60.0 }

func run(t: TestKit) -> void:
	var L := Tune.L

	# ---- Skyline Relay
	var r := run_route("skyline", L.ROUTE_END_X)
	t.check(r.x >= L.ROUTE_END_X and r.falls == 0, "nova runs the Skyline Relay from the tower top to the beacon in %s s without falling (x %s)" % [TestKit.f1(r.secs), TestKit.f1(r.x)])

	# Drone patrol triggers on the roof
	var d := zone_setup("skyline")
	d.p.x = 190.5; d.p.y = 15.6; d.run({}, 2); d.p.x = 192; d.run({}, 2)
	var S: Dictionary = d.w.encounters.filter(func(s): return s.def.id == "patrol")[0]
	var spawned := d.w.enemies.filter(func(e): return e.enc == "patrol")
	t.check(S.state == "active" and spawned.size() == 4 and spawned.filter(func(e): return e.type == "drone").size() == 2 and d.log.any(func(e): return e.type == "banner" and e.text == "Skyline Relay"),
		"Drone patrol starts at x=%s with %d enemies" % [str(S.def.trigger), spawned.size()])

	# Relay Gate: seals, pulls stragglers in, runs two waves, opens when cleared, then the route completes
	var w := World.new()
	w.enemies = []
	var p := w.add_player("a", "nova")
	var q := w.add_player("b", "nova")
	var log := []
	var run2 := func(n: int) -> void:
		for i in n:
			w.step({ 0: Cmd.make({}), 1: Cmd.make({}) })
			log.append_array(w.events); w.events.clear()
	w.teleport("skyline"); run2.call(3)
	for s in w.encounters:
		if s.def.id != "relay":
			s.state = "cleared"
	p.x = 262; p.y = 18.6; q.x = 250; q.y = 18.6; run2.call(2)
	var R: Dictionary = w.encounters.filter(func(s): return s.def.id == "relay")[0]
	var sealed: bool = w.level.gates.L2 and w.level.gates.R2
	var pulled := q.x > 258.8
	var w1 := w.enemies.filter(func(e): return e.enc == "relay" and not e.dead).size()
	killall(w, p, "relay"); run2.call(3)
	var wave2: bool = R.wave == 1 and w.enemies.any(func(e): return e.enc == "relay" and e.type == "brute" and not e.dead)
	for i in 5:
		killall(w, p, "relay"); run2.call(3)
	var open: bool = not w.level.gates.L2 and not w.level.gates.R2 and R.state == "cleared"
	for pl in [p, q]:
		pl.x = L.ROUTE_END_X + 1; pl.y = 18.6
	run2.call(3)
	var banners := func(text: String) -> int:
		return log.filter(func(e): return e.type == "banner" and e.text == text).size()
	t.check(sealed and pulled and w1 >= 5 and wave2 and open and banners.call("Relay secured") == 1 and banners.call("Route complete") == 1,
		"Relay Gate seals (straggler pulled in), wave 1 (%d) then the Brute wave, opens when cleared, and the route completes" % w1)

	# A wipe mid-encounter resets it: its enemies go, the gates open, and it can start again
	d = zone_setup("skyline")
	for s in d.w.encounters:
		if s.def.id != "relay":
			s.state = "cleared"
	d.p.x = 262; d.p.y = 18.6; d.run({}, 2)
	R = d.w.encounters.filter(func(s): return s.def.id == "relay")[0]
	var was: String = R.state
	d.w.reset_to_checkpoint(); d.run({}, 2)
	t.check(was == "active" and R.state == "idle" and not d.w.level.gates.L2 and not d.w.level.gates.R2 and not d.w.enemies.any(func(e): return e.enc == "relay"),
		"Wipe resets the Relay Gate encounter (%s -> %s), gates open" % [was, R.state])

	# Teleporting to the zone lands on the tower-top bridge
	d = zone_setup("skyline")
	t.check(absf(d.p.x - 166) < 1 and absf(d.p.y - 15.6) < 0.05, "Skyline Relay zone spawn at x=%s, y=%s" % [TestKit.f1(d.p.x), TestKit.f1(d.p.y)])

	# ---- The Version 12 routes, end to end
	for route in L.ROUTES.slice(1):
		var rr := run_route(route.id, route.endX, 90)
		t.check(rr.x >= route.endX and rr.falls == 0, "nova runs the %s to its end (x %s) in %s s without falling" % [route.name, TestKit.f1(rr.x), TestKit.f1(rr.secs)])

	# Encounters start only for players on their own route; each route completes on its own
	w = World.new()
	w.teleport("foundry")
	p = w.add_player("kbm", "nova")
	p.x = 720; p.y = 24.2; p.mercy = 1000000000
	w.step({ p.slot: Cmd.make({}) })
	var skyline := w.encounters.filter(func(s): return s.def.route == "skyport" and s.state != "idle").size()
	var crucible: String = w.encounters.filter(func(s): return s.def.id == "f-crucible")[0].state
	for s in w.encounters:
		if s.def.route == "foundry":
			s.state = "cleared"
	for g in w.level.gates:
		w.level.gates[g] = false
	p.x = 759; w.events.clear(); w.step({ p.slot: Cmd.make({}) })
	var done: bool = w.routes_done.get("foundry", false) and not w.routes_done.get("skyport", false) and not w.route_done
	t.check(skyline == 0 and crucible == "active" and done, "Route scoping: no Skyport encounter starts from the Foundry (%d); the Crucible does (%s); the Foundry completes on its own (%s)" % [skyline, crucible, done])

	# Breakable pieces: a crate breaks under a strike and drops its power-up; glass shatters at a shot; a pillar
	# shrugs off small arms but not a heavy blow; a reset puts them all back. (RAM's charge through a barricade:
	# with his kit.)
	w = World.new()
	w.teleport("foundry")
	p = w.add_player("kbm", "nova")
	p.mercy = 1000000000
	var box := func(x: float, tag: String) -> Level.Box:
		for b in w.level.boxes:
			if b.type == "d" and b.tag == tag and b.x0 == x:
				return b
		return null
	var crate: Level.Box = box.call(412.5, "crate")
	var glass: Level.Box = box.call(482, "glass")
	var pillar: Level.Box = box.call(528, "pillar")
	w.spawn_hitbox({ "owner": p, "team": "p", "x0": 412.0, "x1": 414.0, "y0": 1.0, "y1": 2.0, "dmg": 8.0, "poise": 10.0, "kb": [2.0, 0.0], "instance": w.new_instance(), "heavy": true })
	w.step({ p.slot: Cmd.make({}) })
	var loot := w.pickups.filter(func(k): return k.level and not k.rest and k.kind == "fury")
	w.spawn_projectile({ "team": "p", "owner": p, "x": 479.0, "y": 1.5, "vx": 20.0, "vy": 0.0, "ttl": 60, "r": 0.15, "dmg": 1.5, "kind": "shot" })
	for i in 20:
		w.step({ p.slot: Cmd.make({}) })
	var hp0 := pillar.hp
	w.damage_box(pillar, 2, 528.5, 1, p)
	var shrugged := pillar.hp == hp0
	w.damage_box(pillar, 80, 528.5, 1, p)
	var broke := [crate.broken, glass.broken, pillar.broken]
	w.reset_to_checkpoint()
	var back := [crate, glass, pillar].all(func(b): return not b.broken and b.hp == L.DESTRUCT[b.tag].hp)
	t.check(broke[0] and loot.size() > 0 and broke[1] and shrugged and broke[2] and back,
		"Breakables: the crate breaks and drops Fury (%s), glass shatters at a shot (%s), a pillar ignores 2 damage (%s) and breaks under a heavy blow, and a reset restores them (%s)" % [loot.size() > 0, broke[1], shrugged, back])

	# Power-ups along the routes: they wait where they are; an Ult Cell fills the bar, Fury makes hits do more and expires
	w = World.new()
	w.teleport("foundry")
	p = w.add_player("kbm", "nova")
	p.mercy = 1000000000; p.x = 486; p.y = 4.9; p.prev_x = p.x; p.prev_y = p.y
	var waiting := w.pickups.filter(func(k): return k.level and k.rest).size()
	var prev := {}
	for i in 40:
		var c := Cmd.make(prev, { "mx": 1 })
		prev = c.held
		w.step({ p.slot: c })
	var ult := p.ult
	w.apply_power(p, "fury", null)
	var e := EnemySim.create("brute", p.x + 1.4, p.y, { "cd": 9999 }, w)
	e.hp = 500; e.armor = 0; w.enemies.append(e)
	w.spawn_hitbox({ "owner": p, "team": "p", "x0": p.x, "x1": p.x + 3, "y0": p.y, "y1": p.y + 2, "dmg": 10.0, "poise": 0.0, "kb": [1.0, 0.0], "instance": w.new_instance() })
	w.step({ p.slot: Cmd.make({}) })
	var fury_hit := 500 - e.hp
	for i in int(Tune.C.POWERUPS.fury.ticks) + 5:
		w.step({ p.slot: Cmd.make({}) })
	t.check(waiting == L.LEVEL_PICKUPS.size() and ult >= Tune.C.POWERUPS.ultcell.ult - 1 and absf(fury_hit - 10 * Tune.C.POWERUPS.fury.dmg) < 0.01 and p.fury_t == 0,
		"Power-ups: %d waiting along the routes; an Ult Cell fills %s%%; Fury hits for %s (10 base) and wears off after %s s" % [waiting, TestKit.f0(ult), TestKit.f1(fury_hit), TestKit.f0(Tune.C.POWERUPS.fury.ticks / 60)])

	# Lift pads: coming down onto one on the foundry floor throws you up past the platform over it
	w = World.new()
	w.teleport("foundry")
	p = w.add_player("kbm", "nova")
	p.mercy = 1000000000
	var lift: Array = L.LIFTS[2]
	p.x = lift[0]; p.y = lift[1] + 1; p.prev_x = p.x; p.prev_y = p.y
	var peak := 0.0
	for i in 90:
		w.step({ p.slot: Cmd.make({}) }); peak = maxf(peak, p.y)
	t.check(peak > lift[2], "Lift pad at x %s: thrown to %s m, over the platform at %s m" % [str(lift[0]), TestKit.f1(peak), str(lift[2])])
