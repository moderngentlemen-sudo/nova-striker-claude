# One player's input for one simulation tick (input.js's command frame): the stick, the aim, and every
# button held, pressed this tick or released this tick. Built by InputRouter from Godot's input actions, by
# the bots, or by tests.
class_name Cmd
extends RefCounted

const BTNS := ["jump", "dash", "melee", "fire", "parry", "sig", "mode", "lock", "sub", "ult"]

var mx := 0.0
var my := 0.0
var aim_free := false
var ax := 0.0
var ay := 0.0
var held := {}
var pressed := {}
var released := {}

func _init() -> void:
	for b in BTNS:
		held[b] = false; pressed[b] = false; released[b] = false

static var _empty: Cmd = null

# A command with nothing held (a player with no input this tick)
static func empty() -> Cmd:
	if _empty == null:
		_empty = Cmd.new()
	return _empty

# A command from what is held now and what was held last tick (the edges come from the difference).
# opts: { mx, my, aim: [x, y], held: { button: true } }
static func make(prev_held: Dictionary, opts := {}) -> Cmd:
	var c := Cmd.new()
	c.mx = opts.get("mx", 0.0)
	c.my = opts.get("my", 0.0)
	if opts.has("aim"):
		c.aim_free = true; c.ax = opts.aim[0]; c.ay = opts.aim[1]
	var h: Dictionary = opts.get("held", {})
	for b in BTNS:
		c.held[b] = bool(h.get(b, false))
		c.pressed[b] = c.held[b] and not prev_held.get(b, false)
		c.released[b] = not c.held[b] and prev_held.get(b, false)
	return c
