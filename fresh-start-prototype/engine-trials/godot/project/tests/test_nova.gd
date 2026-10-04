# nova-test.mjs: Nova's Marksman kit. Bracer attachments, three charge levels, Perfect Release, Focus, the
# Scatter, splash, rocket jumps, light boosters, full-level range and skate glide. Enemies are frozen in place
# (huge hitstop) unless a check needs them to move.
extends RefCounted

var M: Dictionary
var C: Array
var W: float
var BC: Array

# Hold fire for n ticks (the first tick is the press, which also fires a basic round), then let go
func charge(d: TestKit.Driver, n: int, aim := [1.0, 0.0]) -> void:
	d.run({ "aim": aim, "held": { "fire": true } }, n); d.run({ "aim": aim }, 1)

func select(d: TestKit.Driver, name: String) -> void:
	for i in 4:
		if d.p.attachment == name:
			break
		d.tap("mode"); d.run({}, int(M.switchCd))

func aim_at(p: PlayerSim, e: EnemySim) -> Array:
	var dx := e.x - p.x
	var dy := e.y + e.h / 2 - (p.y + p.h * 0.62)
	var m := U.hypot(dx, dy)
	return [dx / m, dy / m]

func hits_on(d: TestKit.Driver, e) -> int:
	return d.count("hit", func(h): return h.e == e)

func projs(d: TestKit.Driver, kind: String) -> Array:
	return d.w.projectiles.filter(func(pr): return pr.kind == kind)

func run(t: TestKit) -> void:
	M = Tune.C.MARKSMAN; C = M.charge; W = M.perfectWindow; BC = M.burst.charge

	# The mode button cycles the bracer attachments
	var d := TestKit.setup(99)
	var seen := [d.p.attachment]
	for i in 4:
		d.tap("mode"); d.run({}, int(M.switchCd)); seen.append(d.p.attachment)
	t.check(" > ".join(seen) == "lance > volley > arc > prism > lance" and d.count("attach") == 4, "Mode button cycles %s" % " > ".join(seen))

	# Charge stages, including the Perfect Release window
	d = TestKit.setup(99)
	var stages := []
	for n in [1, C[0] - 1, C[1] - C[0], C[2] - C[1], W]:
		d.run({ "aim": [1, 0], "held": { "fire": true } }, int(n)); stages.append(d.p.charge_stage())
	d.run({ "aim": [1, 0] }, 1)
	var st: String = d.p.charge_stage()
	stages.append(st if st != "" else "idle")
	t.check(" > ".join(stages) == "charging > L1 > L2 > perfect > L3 > idle", "Three charge levels: %s" % " > ".join(stages))

	# Tap fires a basic round; a partial charge fires a Lance that pierces two Swarmers
	d = TestKit.setup(99)
	var a := d.dummy("swarmer", 103)
	var b := d.dummy("swarmer", 105)
	charge(d, int(C[0]) + 2, aim_at(d.p, a))
	var lance := projs(d, "lance")
	d.run({}, 40)
	t.check(d.count("shot", func(s): return s.level == 0) == 1 and lance.size() == 1 and lance[0].pierce and a.dead and b.dead,
		"Basic round on press, then a level 1 Lance pierces both Swarmers (dead: %s, %s)" % [a.dead, b.dead])

	# Letting go inside the window after level 3 is a Perfect Release; letting go late is a normal level 3 shot
	var t1 := TestKit.setup(99); charge(t1, int(C[2]) + 2)
	var perf := projs(t1, "rail")
	var t2 := TestKit.setup(99); charge(t2, int(C[2] + W) + 3)
	var late := projs(t2, "rail")
	var t3 := TestKit.setup(99); charge(t3, int(C[1]) + 2)
	var two := projs(t3, "lance")
	t.check(t1.count("perfectRelease") == 1 and perf.size() == 1 and perf[0].perfect and absf(perf[0].dmg - M.lance[3].dmg * M.perfectMult) < 1e-9
		and t2.count("perfectRelease") == 0 and late.size() == 1 and not late[0].perfect and absf(late[0].dmg - M.lance[3].dmg) < 1e-9
		and two.size() == 1 and absf(two[0].dmg - M.lance[2].dmg) < 1e-9,
		"Level 2 Lance deals %s; Perfect Release inside %d ticks of level 3 deals %s; a late release deals %s" % [str(two[0].dmg) if two else "?", int(W), str(perf[0].dmg) if perf else "?", str(late[0].dmg) if late else "?"])

	# A level 2 Lance breaks a layer of Brute armor; a Perfect one breaks a Shieldbearer's guard head-on
	d = TestKit.setup(99)
	var br := d.dummy("brute", 104)
	charge(d, int(C[1]) + 2, aim_at(d.p, br)); d.run({}, 20)
	var s1 := TestKit.setup(99)
	var sh := s1.dummy("shield", 104); sh.shield_dir = -1
	charge(s1, int(C[0]) + 2, aim_at(s1.p, sh)); s1.run({}, 20)
	var blocked_l1 := s1.count("blocked")
	var s3 := TestKit.setup(99)
	var sh3 := s3.dummy("shield", 104); sh3.shield_dir = -1
	charge(s3, int(C[2]) + 2, aim_at(s3.p, sh3)); s3.run({}, 20)
	t.check(br.armor == 2 and blocked_l1 >= 1 and s3.count("guardBreak") == 1,
		"Level 2 Lance strips Brute armor (%d left); a level 1 Lance is blocked by a shield (%d); a Perfect one breaks the guard" % [br.armor, blocked_l1])

	# Volley: five darts spread over two enemies at different heights, and both are hit
	d = TestKit.setup(99); select(d, "volley")
	a = d.dummy("swarmer", 105); b = d.dummy("sniper", 106, 4)
	charge(d, int(C[1]) + 2)
	var darts := projs(d, "dart")
	d.run({}, 60)
	t.check(darts.size() == M.volley.darts[2] and hits_on(d, a) >= 1 and hits_on(d, b) >= 1,
		"Level 2 Volley fires %d darts that home onto both targets (hits %d + %d)" % [darts.size(), hits_on(d, a), hits_on(d, b)])

	# Dart counts: level 1 fires 3, level 3 fires 7, a Perfect Release fires 9
	var dart_count := func(n: int) -> int:
		var q := TestKit.setup(99); select(q, "volley"); charge(q, n)
		return projs(q, "dart").size()
	var d1: int = dart_count.call(int(C[0]) + 1)
	var d3: int = dart_count.call(int(C[2] + W) + 2)
	var dp: int = dart_count.call(int(C[2]) + 1)
	t.check(d1 == 3 and d3 == 7 and dp == 9, "Volley darts: level 1 %d, level 3 %d, Perfect %d" % [d1, d3, dp])

	# Arc: the shell arcs, bursts on the Shieldbearer (not blocked, even from the front) and spares an enemy
	# outside the radius
	d = TestKit.setup(99); select(d, "arc")
	sh = d.dummy("shield", 109.2); sh.shield_dir = -1
	var far := d.dummy("swarmer", 112.3)
	d.run({ "aim": [1, 0], "held": { "fire": true } }, int(C[0]) + 2)
	d.run({ "aim": [1, 0] }, 1)
	var shells := projs(d, "shell")
	var shell = shells[0] if shells else null
	var y0: float = shell.y if shell else 0.0
	var top := y0
	for i in 90:
		if d.count("blast"):
			break
		d.run({}, 1)
		if shell and not shell.dead:
			top = maxf(top, shell.y)
	var blast = d.first("blast")
	var blast_hits := d.count("hit", func(h): return h.e == sh and h.source == "blast")
	t.check(shell != null and top > y0 + 1 and blast != null and blast_hits == 1 and hits_on(d, far) == 0,
		"Arc shell climbs %s m, bursts at x=%s (r %s) and hits the shield head-on (%d); the far Swarmer is untouched" % [TestKit.f2(top - y0), TestKit.f1(blast.x) if blast else "?", str(blast.r) if blast else "?", blast_hits])

	# Prism: the round splits on the first enemy and a shard hits the one behind it
	d = TestKit.setup(99); select(d, "prism")
	a = d.dummy("swarmer", 103); b = d.dummy("sniper", 106)
	charge(d, int(C[1]) + 2, aim_at(d.p, a)); d.run({}, 40)
	var split = d.first("split")
	t.check(split != null and split.n == 3 and hits_on(d, a) >= 1 and hits_on(d, b) >= 1,
		"Prism splits into %s shards on impact and a shard hits the Sniper behind (hits %d)" % [str(split.n) if split else "?", hits_on(d, b)])

	# Prism into a wall: it splits off the surface and the shards ricochet
	d = TestKit.setup(105); select(d, "prism")
	charge(d, int(C[0]) + 2); d.run({}, 60)
	t.check(d.count("split") == 1 and d.count("ricochet") >= 1, "Prism splits off the wall and shards ricochet (%d bounces)" % d.count("ricochet"))

	# Scatter: pellets hit and push an enemy just outside melee reach, Nova stays put (no recoil), and a
	# cooldown applies (closer in, the melee button is his bracer combo instead)
	d = TestKit.setup(99)
	var e := d.dummy("swarmer", 101.6)
	var aim := aim_at(d.p, e)
	d.run({ "aim": aim }, 2)
	d.tap("melee", { "aim": aim })
	var vx_after := d.p.vx
	d.run({ "aim": aim }, 6)
	var hits := hits_on(d, e)
	var push := e.vx
	d.tap("melee", { "aim": aim })
	var quick := d.count("burst")
	d.run({ "aim": aim }, int(M.burst.cd) - 6); d.tap("melee", { "aim": aim })
	t.check(d.count("burst") == 2 and quick == 1 and hits >= 3 and push > 5 and absf(vx_after) < 0.5 and d.p.move_id == null,
		"Scatter lands %d pellets, pushes the enemy (vx %s), no recoil on Nova (vx %s); a second press inside the cooldown does nothing" % [hits, TestKit.f1(push), TestKit.f1(vx_after)])

	# Secondary charge levels: pressing fires a quick burst; holding passes levels 1-3; letting go right after
	# level 3 is a Perfect level 3 burst with a muzzle blast, and its centre pellet breaks armor
	d = TestKit.setup(99)
	br = d.dummy("brute", 102.2)
	aim = aim_at(d.p, br)
	stages = []
	d.run({ "aim": aim, "held": { "melee": true } }, int(BC[0])); stages.append(d.p.burst_stage())
	d.run({ "aim": aim, "held": { "melee": true } }, int(BC[1] - BC[0])); stages.append(d.p.burst_stage())
	d.run({ "aim": aim, "held": { "melee": true } }, int(BC[2] - BC[1]) + 2); stages.append(d.p.burst_stage())
	var before := d.w.projectiles.duplicate()
	d.run({ "aim": aim }, 1)
	var bursts := d.log.filter(func(q): return q.type == "burst")
	var fresh := d.w.projectiles.filter(func(pr): return pr.kind == "pellet" and not before.has(pr)).size()
	d.run({ "aim": aim }, 10)
	t.check(" > ".join(stages) == "L1 > L2 > perfect" and d.count("burstLevel") == 3 and bursts.size() == 2 and bursts[0].level == 0 and bursts[1].level == 3 and bursts[1].perfect
		and fresh == M.burst[3].pellets and d.count("blast") == 1 and br.armor <= 2,
		"Burst stages %s; release fires a Perfect level 3 burst (%d pellets + blast); Brute armor %d" % [" > ".join(stages), fresh, br.armor])

	# A level 1 secondary burst fires 7 pellets
	d = TestKit.setup(99)
	d.run({ "aim": [1, 0], "held": { "melee": true } }, int(BC[0]) + 2)
	before = d.w.projectiles.duplicate(); d.run({ "aim": [1, 0] }, 1)
	fresh = d.w.projectiles.filter(func(pr): return pr.kind == "pellet" and not before.has(pr)).size()
	bursts = d.log.filter(func(q): return q.type == "burst")
	t.check(fresh == M.burst[1].pellets and bursts[bursts.size() - 1].level == 1, "Level 1 secondary burst: %d pellets" % fresh)

	# Pellets fly on but fall off with distance
	var hit_dmg := func(x: float) -> float:
		var q := TestKit.setup(99)
		var post := q.dummy("post", x)
		q.tap("melee", { "aim": aim_at(q.p, post) }); q.run({}, 50)
		var h = q.first("hit", func(z): return z.e == post)
		return h.dmg if h else 0.0
	var near_d: float = hit_dmg.call(100.8)
	var far_d: float = hit_dmg.call(109)
	t.check(near_d > 0 and far_d > 0 and far_d < near_d * 0.6, "Pellet damage %s up close, %s at 10 m" % [TestKit.f2(near_d), TestKit.f2(far_d)])

	# No recoil in the air either: a charged Scatter aimed diagonally down neither lifts nor pushes him
	d = TestKit.setup(99)
	var aim_dn := [0.55, -0.83]
	d.p.y = 14; d.p.vy = 0; d.p.on_ground = false; d.run({}, 4)
	d.run({ "aim": aim_dn, "held": { "melee": true } }, int(BC[2]) + 2)
	var vy0 := d.p.vy
	var vx0 := d.p.vx
	d.run({ "aim": aim_dn }, 1)
	t.check(d.p.vy <= vy0 and absf(d.p.vx - vx0) < 0.5, "Charged Scatter aimed down in the air: no lift (vy %s -> %s) and no push (vx %s -> %s)" % [TestKit.f1(vy0), TestKit.f1(d.p.vy), TestKit.f1(vx0), TestKit.f1(d.p.vx)])

	# Skates: higher top speed, a longer glide than the Sentinel kit, and no backpedal slowdown while aiming behind
	var glide := func(kit: String) -> Dictionary:
		Tune.settings.novaKit = kit
		var q := TestKit.setup(98)
		q.run({ "mx": 1 }, 40)
		var topv := q.p.vx
		var x0 := q.p.x
		q.run({}, 60)
		var dist := q.p.x - x0
		var q2 := TestKit.setup(98); q2.run({ "mx": 1, "aim": [-1, 0], "held": { "fire": true } }, 50)
		return { "top": topv, "dist": dist, "back": q2.p.vx }
	var gm: Dictionary = glide.call("marksman")
	var gs: Dictionary = glide.call("sentinel")
	Tune.settings.novaKit = "marksman"
	t.check(absf(gm.top - M.skate.top) < 0.05 and gm.dist > gs.dist * 3 and gm.back > 7.5 and gs.back < 6.5,
		"Top speed %s vs %s; glide %s m vs %s m; moving while aiming behind %s vs %s" % [TestKit.f2(gm.top), TestKit.f2(gs.top), TestKit.f2(gm.dist), TestKit.f2(gs.dist), TestKit.f1(gm.back), TestKit.f1(gs.back)])

	# Reversing at speed carves to a quick stop
	d = TestKit.setup(98)
	d.run({ "mx": 1 }, 40)
	var n := 0
	while d.p.vx > 0 and n < 30:
		d.run({ "mx": -1 }, 1); n += 1
	t.check(d.count("carve") >= 1 and n <= 7, "Carve event and a stop from %s m/s in %d ticks" % [str(M.skate.top), n])

	# Focus: charged hits build it (2 for a Perfect Release), it boosts damage, and taking a hit clears it
	d = TestKit.setup(99)
	var post := d.dummy("post", 104)
	aim = aim_at(d.p, post)
	charge(d, int(C[0]) + 2, aim); d.run({}, 20)
	var f1 := d.p.focus
	charge(d, int(C[2]) + 2, aim); d.run({}, 20)
	var f2 := d.p.focus
	charge(d, int(C[0]) + 2, aim)
	lance = projs(d, "lance")
	var expected: float = M.lance[1].dmg * (1 + M.focus.dmgPer * floor(d.p.focus))
	d.run({}, 20)
	d.w.spawn_hitbox({ "owner": post, "team": "e", "x0": d.p.x - 0.8, "x1": d.p.x + 0.8, "y0": d.p.y, "y1": d.p.y + 1.8, "dmg": 5.0, "kb": [0.0, 0.0], "instance": d.w.new_instance(), "cat": "standard" })
	d.run({}, 1)
	t.check(floor(f1) == 1 and floor(f2) == 3 and lance.size() == 1 and absf(lance[0].dmg - expected) < 1e-9 and d.p.focus == 0 and d.count("focusLost") == 1,
		"Focus %s after a hit, %s after a Perfect Release; Lance damage %s; cleared when hit" % [TestKit.f2(f1), TestKit.f2(f2), TestKit.f2(lance[0].dmg) if lance else "?"])

	# Focus drains when Nova stops landing shots
	d = TestKit.setup(99); d.p.focus = 3; d.p.focus_t = 5
	d.run({}, 5 + int(M.focus.decayStep) + 1)
	t.check(d.p.focus == 1, "Focus drains one level after a quiet stretch, then one more every %d ticks (now %s)" % [int(M.focus.decayStep), str(d.p.focus)])

	# The Sentinel kit is untouched: mode does nothing, melee starts the jab chain, a full charge fires the rail
	Tune.settings.novaKit = "sentinel"
	d = TestKit.setup(99)
	d.tap("mode"); d.tap("melee")
	var jab = d.p.move_id
	d.run({}, 40)
	charge(d, int(Tune.C.NOVA.charge2) + 1)
	var rail := projs(d, "rail")
	Tune.settings.novaKit = "marksman"
	t.check(d.count("attach") == 0 and jab == "nova_jab1" and rail.size() == 1 and not rail[0].get("perfect") and d.count("burst") == 0,
		"Sentinel kit: no attachment switch, melee starts %s, full charge fires the rail" % str(jab))

	# Splash: a basic round that hits one Swarmer also hurts the one right behind it
	d = TestKit.setup(99)
	a = d.dummy("swarmer", 104); b = d.dummy("swarmer", 104.8)
	d.tap("fire", { "aim": aim_at(d.p, a) }); d.run({}, 30)
	var splash_b := d.count("hit", func(h): return h.e == b and h.source == "blast")
	t.check(hits_on(d, a) >= 1 and splash_b == 1 and d.count("splash") >= 1, "Direct hit on the first Swarmer, splash on the second (%d)" % splash_b)

	# Splash on terrain: a Lance fired into the floor beside a Swarmer still hurts it
	d = TestKit.setup(99)
	e = d.dummy("swarmer", 102.4)
	var ddx := 101.9 - d.p.x
	var ddy := 0 - (d.p.y + d.p.h * 0.62)
	var mm := U.hypot(ddx, ddy)
	charge(d, int(C[0]) + 2, [ddx / mm, ddy / mm]); d.run({}, 20)
	t.check(d.count("hit", func(h): return h.e == e and h.source == "blast") >= 1, "Lance into the floor splashes the Swarmer beside the impact")

	# Range: a basic round flies past the old 20 m limit until it hits the ledge wall 31 m away
	d = TestKit.setup(0)
	d.tap("fire", { "aim": [1, 0] }); d.run({}, 90)
	var wall = d.first("projWall", func(q): return q.pr.kind == "shot")
	t.check(wall != null and wall.x > 31, "Round travelled to x=%s (the ledge wall), well past 20 m" % (TestKit.f1(wall.x) if wall else "?"))

	# Rocket jump: a level 3 shot at his feet launches Nova well above a normal jump; basic rounds do not
	var height := func(nt: int) -> Dictionary:
		var q := TestKit.setup(99)
		var qy0 := q.p.y
		var qtop := qy0
		q.run({ "aim": [0, -1], "held": { "fire": true } }, nt); q.run({ "aim": [0, -1] }, 1)
		for i in 80:
			q.run({}, 1); qtop = maxf(qtop, q.p.y)
		return { "h": qtop - qy0, "rj": q.count("rocketJump") }
	var l3: Dictionary = height.call(int(C[2] + W) + 2)
	var l1: Dictionary = height.call(int(C[0]) + 2)
	var pf: Dictionary = height.call(int(C[2]) + 2)
	d = TestKit.setup(99); d.tap("fire", { "aim": [0, -1] }); d.run({}, 20)
	t.check(l3.rj == 1 and l3.h > 3.2 and pf.h > l3.h and l1.h > 0.8 and l1.h < l3.h and d.count("rocketJump") == 0,
		"Rocket jump heights: level 1 %s m, level 3 %s m, Perfect %s m (a normal jump is 3.2 m); basic rounds don't launch" % [TestKit.f2(l1.h), TestKit.f2(l3.h), TestKit.f2(pf.h)])

	# A second rocket jump in the same airtime is weaker
	d = TestKit.setup(99)
	d.p.y = 6; d.p.on_ground = false; d.p.vy = 0
	var spec := { "r": 1.9, "rocket": 21 }
	d.w.rocket_push(d.p, d.p.x, d.p.y - 0.2, spec, null, false); d.p.vy = 0
	d.w.rocket_push(d.p, d.p.x, d.p.y - 0.2, spec, null, false)
	var ks := d.w.events.filter(func(q): return q.type == "rocketJump").map(func(q): return q.k)
	t.check(ks.size() == 2 and absf(ks[1] / ks[0] - M.rocket.air[1]) < 1e-9, "Second air rocket jump strength x%s" % (TestKit.f2(ks[1] / ks[0]) if ks.size() == 2 else "?"))

	# Light boosters: after the double jump, press and hold jump to hover; fuel runs out, then refills on landing
	d = TestKit.setup(99)
	d.run({ "held": { "jump": true } }, 12); d.run({}, 1)
	d.run({ "held": { "jump": true } }, 1); d.run({}, 1)
	while d.p.vy > 1:
		d.run({}, 1)
	var by0 := d.p.y
	d.run({ "held": { "jump": true } }, 30)
	var hover := d.p.y - by0
	var fuel_mid := d.p.fuel
	var on := d.count("thrustOn")
	d.run({ "held": { "jump": true } }, 60)
	var empty := d.p.fuel
	for i in 120:
		if d.p.on_ground:
			break
		d.run({}, 1)
	d.run({}, 30)
	t.check(on == 1 and hover > 0.5 and fuel_mid < M.boost.fuel and empty == 0 and d.count("thrustOff") >= 1 and d.p.fuel == M.boost.fuel,
		"Boosters lift Nova %s m in half a second after the apex, run dry, and refill on landing" % TestKit.f2(hover))

	# Holding jump through an ordinary jump never fires the boosters; letting go cuts them
	d = TestKit.setup(99)
	for i in 80:
		if i > 5 and d.p.on_ground:
			break
		d.run({ "held": { "jump": true } }, 1)
	var plain := d.count("thrustOn")
	d.run({}, 5); d.run({ "held": { "jump": true } }, 12); d.run({}, 1); d.run({ "held": { "jump": true } }, 1); d.run({}, 1)
	d.run({ "held": { "jump": true } }, 5); d.run({}, 2)
	t.check(plain == 0 and d.count("thrustOn") == 1 and d.count("thrustOff") == 1 and not d.p.thrusting,
		"A held ordinary jump never boosts; a press after the double jump does, and letting go stops it")
