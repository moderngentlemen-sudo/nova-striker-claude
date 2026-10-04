# What every test suite gets: check() prints PASS or FAIL lines like the prototype's node suites (so the two
# outputs can be compared line by line), and a few helpers to drive a World one tick at a time.
class_name TestKit
extends RefCounted

var passed := 0
var failed := 0
var suite := ""

func check(cond: bool, msg: String) -> void:
	if cond:
		passed += 1
		print("PASS " + msg)
	else:
		failed += 1
		print("FAIL " + msg)

static func f2(v: float) -> String:
	return "%.2f" % v

static func f1(v: float) -> String:
	return "%.1f" % v

static func f0(v: float) -> String:
	return "%.0f" % v

# A world with one player at (x, y), the enemies cleared, run for 10 idle ticks with mercy then cleared (the
# prototype suites' setup())
static func setup(x := 100.0, char := "nova", clear := true, y := 0.0) -> Driver:
	var w := World.new()
	if clear:
		w.enemies = []
	var p := w.add_player("test", char)
	p.x = x; p.y = y; p.prev_x = x; p.prev_y = y
	var d := Driver.new(w)
	d.p = p
	d.run({}, 10)
	p.mercy = 0
	return d

# Drives one player of a world: run(opts, n) steps n ticks with that input (opts as Cmd.make), and the
# world's events are gathered into `log`
class Driver:
	var w: World
	var p: PlayerSim
	var slot := 0
	var prev := {}
	var log: Array = []
	func _init(world, s := 0) -> void:
		w = world; slot = s
	func run(opts := {}, n := 1) -> void:
		for i in n:
			var c := Cmd.make(prev, opts)
			prev = c.held.duplicate()
			w.step({ slot: c })
			log.append_array(w.events)
			w.events.clear()
	# Runs up to n ticks, stopping (and returning true) as soon as fn() returns true
	func until(opts: Dictionary, n: int, fn: Callable) -> bool:
		for i in n:
			run(opts, 1)
			if fn.call():
				return true
		return false
	func spawn(type: String, x: float, y: float, o := {}) -> EnemySim:
		var e := EnemySim.create(type, x, y, o, w)
		w.enemies.append(e)
		return e
	# An enemy frozen in place (huge hitstop, no attacks), as nova-test.mjs's enemy()
	func dummy(type: String, x: float, y := 0.0, o := {}) -> EnemySim:
		var opts := { "cd": 9999, "slamCd": 9999 }
		opts.merge(o, true)
		var e := spawn(type, x, y, opts)
		e.hitstop = 1000000000
		return e
	func first(type: String, f := Callable()):
		for e in log:
			if e.type == type and (not f.is_valid() or f.call(e)):
				return e
		return null
	func tap(button: String, extra := {}) -> void:
		var o := extra.duplicate()
		o["held"] = { button: true }
		run(o, 1)
		run(extra, 1)
	func count(type: String, f := Callable()) -> int:
		var n := 0
		for e in log:
			if e.type == type and (not f.is_valid() or f.call(e)):
				n += 1
		return n
