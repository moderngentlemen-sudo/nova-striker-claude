# Golden traces (tools/trace-js.mjs): the prototype's own simulation was run on scripted input and every tick's
# state saved. Here the port plays the same input and must match tick by tick: the player's position, speed,
# state, facing, height, health and charge, every enemy (id, type, position, health, state), the number of
# projectiles, and the events. A mismatch reports the first tick where the runs part.
extends RefCounted

const TOL := 1e-6

func run(t: TestKit) -> void:
	var dir := "res://tests/traces/"
	var names := []
	for f in DirAccess.get_files_at(dir):
		if f.ends_with(".json"):
			names.append(f)
	names.sort()
	for f in names:
		var data = JSON.parse_string(FileAccess.get_file_as_string(dir + f))
		var res := replay(data)
		t.check(res.ok, "Trace %s: %d ticks match the prototype%s" % [data.name, data.ticks.size(), "" if res.ok else " (first difference at tick %d: %s)" % [res.tick, res.why]])

func replay(data: Dictionary) -> Dictionary:
	Tune.reset_settings()
	for k in data.settings:
		Tune.settings[k] = data.settings[k]
	EnemySim.next_id = int(data.idStart)
	var w := World.new(int(data.seed))
	if data.zone != null:
		w.teleport(data.zone)
	if data.clear:
		w.enemies = []
	var p := w.add_player("test", data.char)
	if data.x != null:
		p.x = data.x; p.y = data.y; p.prev_x = p.x; p.prev_y = p.y; p.last_safe_x = p.x; p.last_safe_y = p.y
	p.mercy = 0
	var prev := {}
	var i := 0
	for step in data.steps:
		var opts: Dictionary = step[1]
		for k in int(step[0]):
			var c := Cmd.make(prev, opts)
			prev = c.held.duplicate()
			w.step({ p.slot: c })
			var want: Dictionary = data.ticks[i]
			var why := _compare(w, p, want)
			w.events.clear()
			if why != "":
				return { "ok": false, "tick": i + 1, "why": why }
			i += 1
	return { "ok": true }

func _compare(w: World, p: PlayerSim, want: Dictionary) -> String:
	var got := [p.x, p.y, p.vx, p.vy, p.state, p.facing, p.h, p.hp, p.charge_t, p.burst_t]
	var names := ["x", "y", "vx", "vy", "state", "facing", "h", "hp", "chargeT", "burstT"]
	for j in got.size():
		if not _same(got[j], want.p[j]):
			return "player %s %s, prototype %s" % [names[j], str(got[j]), str(want.p[j])]
	if w.enemies.size() != want.e.size():
		return "%d enemies, prototype %d" % [w.enemies.size(), want.e.size()]
	for j in w.enemies.size():
		var e: EnemySim = w.enemies[j]
		var ge := [e.id, e.type, e.x, e.y, e.hp, e.state]
		for k in ge.size():
			if not _same(ge[k], want.e[j][k]):
				return "enemy %d (%s) field %d: %s, prototype %s" % [e.id, e.type, k, str(ge[k]), str(want.e[j][k])]
	if w.projectiles.size() != int(want.n):
		return "%d projectiles, prototype %d" % [w.projectiles.size(), want.n]
	var ev: Array = w.events.map(func(e): return e.type)
	if ev != want.ev:
		return "events %s, prototype %s" % [str(ev), str(want.ev)]
	return ""

static func _same(a, b) -> bool:
	if b == null:
		return a == null or (typeof(a) == TYPE_FLOAT and is_inf(a))
	if typeof(b) == TYPE_STRING:
		return str(a) == b
	return absf(float(a) - float(b)) <= TOL * maxf(1.0, absf(float(b)))
