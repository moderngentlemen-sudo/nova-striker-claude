# nova-soak.mjs: four Marksman Novas fight on random inputs for 8000 ticks in each zone (the prototype runs two
# Novas and two Echos in two zones; the port also soaks the Helix Foundry and Undercity Descent). Checks for
# NaN positions and projectile, well or enemy build-up, and that no ultimate gets stuck. (A script error would
# show in the output: tools/run-tests.sh fails the run on any.)
extends RefCounted

const BT := ["jump", "dash", "melee", "fire", "parry", "sig", "mode", "lock", "sub", "ult"]
var seed := 7

func rnd() -> float:
	seed = (seed * 1103515245 + 12345) % 2147483648
	return seed / 2147483648.0

func soak(zone: String) -> Dictionary:
	var w := World.new()
	w.teleport(zone)
	var ps := []
	for i in 4:
		ps.append(w.add_player("t%d" % i, "nova"))
	var prev := [{}, {}, {}, {}]
	var plans := [{ "t": 0, "o": {} }, { "t": 0, "o": {} }, { "t": 0, "o": {} }, { "t": 0, "o": {} }]
	var res := { "nan": 0, "proj": 0, "enemies": 0, "wells": 0, "kills": 0, "shots": 0, "stuck": false }
	for tick in 8000:
		var cmds := {}
		for i in 4:
			var pl: Dictionary = plans[i]
			pl.t -= 1
			if pl.t <= 0:
				pl.t = 5 + int(floor(rnd() * 40))
				var held := {}
				for b in BT:
					var pr := 0.5 if b == "fire" else (0.08 if b == "mode" or b == "sub" else (0.06 if b == "lock" else (0.03 if b == "ult" else (0.3 if b == "jump" else 0.15))))
					held[b] = rnd() < pr
				var a := rnd() * TAU
				var mx := (1.0 if rnd() < 0.65 else -1.0) if rnd() < 0.7 else 0.0
				var my := -1.0 if rnd() < 0.15 else (1.0 if rnd() < 0.1 else 0.0)
				pl.o = { "mx": mx, "my": my, "aim": [cos(a), sin(a) * 0.8], "held": held }
			var c := Cmd.make(prev[i], pl.o)
			prev[i] = c.held.duplicate(); cmds[ps[i].slot] = c
		w.step(cmds)
		for e in w.events:
			if e.type == "kill": res.kills += 1
			if e.type == "shot": res.shots += 1
		w.events.clear()
		res.proj = maxi(res.proj, w.projectiles.size()); res.enemies = maxi(res.enemies, w.enemies.size()); res.wells = maxi(res.wells, w.wells.size())
		if tick % 900 == 450:
			for p in ps:
				p.ult = 100.0   # ultimates now and then
		for q in w.wells:
			if not is_finite(q.x + q.y): res.nan += 1
		for pr in w.projectiles:
			if not is_finite(pr.x + pr.y): res.nan += 1
		for e in w.enemies:
			if not is_finite(e.x + e.y): res.nan += 1
		for p in ps:
			if not is_finite(p.x + p.y + p.vx + p.vy): res.nan += 1
		if w.arena.state == "cleared":
			w.reset_arena()
		# keep the soak inside a zone: if the team clears everything there, start its encounters again
		var route: String = Level.route_at(Level.zone_at(ps[0].x).spawn.x).id
		if w.encounters.all(func(S): return S.def.route != route or S.state == "cleared"):
			for S in w.encounters:
				if S.def.route == route:
					S.state = "idle"
	res.stuck = w.ult_cast != null and w.ult_cast.t > 400
	return res

func run(t: TestKit) -> void:
	Tune.settings.lockMode = "auto"
	var ok := true
	var parts := []
	for zone in ["arena", "skyline", "foundry", "undercity"]:
		var r := soak(zone)
		ok = ok and r.nan == 0 and r.proj < 250 and r.enemies < 40 and r.wells <= 4 and not r.stuck
		parts.append("%s %d shots, %d kills, at most %d projectiles" % [zone, r.shots, r.kills, r.proj])
	t.check(ok, "soak: no NaN, projectile and enemy counts bounded, no stuck ultimate (%s)" % "; ".join(parts))
