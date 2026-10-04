# Turns Godot's input into one Cmd per tick for the local player (input.js sample): the stick, every button
# held, pressed this tick (taps that start and end between two ticks still count) or released, and the aim:
# the mouse, as a direction from the player's chest on screen, or the right stick, which keeps its last
# direction for 18 ticks after it is let go so a flick does not snap back.
#
# The Input Map (project.godot, written by tools/define_input.gd) listens to every device, so for now the
# keyboard, mouse and any controller all drive player one; splitting devices between players comes with co-op.
class_name InputRouter
extends Node

const AIM_GRACE := 18

var prev_held := {}
var pressed_extra := {}       # presses seen since the last tick
var grace := 0
var last_free := Vector2(1, 0)
var kind := "kbm"             # the last kind of device used: 'kbm' or 'pad' (for aim and on-screen prompts)
var swallow := false
var mouse_moved := false

func _input(ev: InputEvent) -> void:
	if ev is InputEventJoypadButton or (ev is InputEventJoypadMotion and absf(ev.axis_value) > 0.4):
		kind = "pad"
	elif ev is InputEventKey or ev is InputEventMouseButton:
		kind = "kbm"
	elif ev is InputEventMouseMotion:
		mouse_moved = true
		if ev.relative.length() > 2:
			kind = "kbm"
	if ev.is_echo():
		return
	for b in Cmd.BTNS:
		if InputMap.has_action(b) and ev.is_action_pressed(b):
			pressed_extra[b] = true

# After a menu closes, buttons still held from closing it count as already held (no stray jump or dash)
func swallow_all() -> void:
	swallow = true
	pressed_extra.clear()

func sample(p: PlayerSim, cam: Camera3D) -> Cmd:
	var c := Cmd.new()
	var mv := Input.get_vector("move_left", "move_right", "move_down", "move_up")
	c.mx = mv.x; c.my = mv.y
	var held := {}
	for b in Cmd.BTNS:
		held[b] = InputMap.has_action(b) and Input.is_action_pressed(b)
	# Aim
	if kind == "pad":
		var r := Input.get_vector("aim_left", "aim_right", "aim_down", "aim_up", 0.3)
		if r.length() > 0.35:
			c.aim_free = true; last_free = r.normalized(); grace = AIM_GRACE
			c.ax = last_free.x; c.ay = last_free.y
		elif grace > 0:
			grace -= 1
			c.aim_free = true; c.ax = last_free.x; c.ay = last_free.y
	elif Tune.settings.get("p1Aim", "mouse") == "mouse" and cam != null and p != null:
		var v := aim_from_mouse(p, cam)
		if v != Vector2.ZERO:
			c.aim_free = true; c.ax = v.x; c.ay = v.y
	p.device = kind
	if swallow:
		swallow = false
		prev_held = held.duplicate()
		pressed_extra.clear()
	for b in Cmd.BTNS:
		c.held[b] = held[b]
		c.pressed[b] = (held[b] and not prev_held.get(b, false)) or pressed_extra.get(b, false)
		c.released[b] = not held[b] and prev_held.get(b, false)
	prev_held = held
	pressed_extra.clear()
	return c

# The mouse's direction from the player's chest on screen, in sim space (render.js aimFromMouse)
func aim_from_mouse(p: PlayerSim, cam: Camera3D) -> Vector2:
	var vp := cam.get_viewport()
	var m := vp.get_mouse_position()
	var chest := PathFrame.point(p.x, p.y + p.h * 0.62)
	if cam.is_position_behind(chest):
		return Vector2.ZERO
	var s := cam.unproject_position(chest)
	var d := Vector2(m.x - s.x, -(m.y - s.y))
	return Vector2.ZERO if d.length() < 6 else d.normalized()
