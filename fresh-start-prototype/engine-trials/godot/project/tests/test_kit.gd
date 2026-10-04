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

# Drives one player of a world: run(opts, n) steps n ticks with that input (opts as Cmd.make), and the
# world's events are gathered into `log`
class Driver:
	var w
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
