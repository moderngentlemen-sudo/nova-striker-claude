# Writes the game's input actions into project.godot (the Input Map), so they can also be edited in the
# editor's Project Settings. The bindings follow the prototype (input.js): keyboard and mouse, and a
# standard controller. Re-run after changing a binding here:
#   godot --headless --path project -s "$PWD/tools/define_input.gd"     (from engine-trials/godot)
# Every event is for any device
# (-1): which controller belongs to which player is decided by InputRouter, not the Input Map.
extends SceneTree

const DEADZONE := 0.2

func key(k: Key) -> InputEventKey:
	var e := InputEventKey.new(); e.physical_keycode = k; e.device = -1
	return e

func mouse(b: MouseButton) -> InputEventMouseButton:
	var e := InputEventMouseButton.new(); e.button_index = b; e.device = -1
	return e

func pad(b: JoyButton) -> InputEventJoypadButton:
	var e := InputEventJoypadButton.new(); e.button_index = b; e.device = -1
	return e

func axis(a: JoyAxis, v: float) -> InputEventJoypadMotion:
	var e := InputEventJoypadMotion.new(); e.axis = a; e.axis_value = v; e.device = -1
	return e

func _init() -> void:
	var actions := {
		# Movement: the left stick, WASD or the arrows; the D-pad's up and down also count as the stick (input.js)
		"move_left": [key(KEY_A), key(KEY_LEFT), axis(JOY_AXIS_LEFT_X, -1.0)],
		"move_right": [key(KEY_D), key(KEY_RIGHT), axis(JOY_AXIS_LEFT_X, 1.0)],
		"move_up": [key(KEY_W), key(KEY_UP), axis(JOY_AXIS_LEFT_Y, -1.0), pad(JOY_BUTTON_DPAD_UP)],
		"move_down": [key(KEY_S), key(KEY_DOWN), axis(JOY_AXIS_LEFT_Y, 1.0), pad(JOY_BUTTON_DPAD_DOWN)],
		# Aim: the right stick (the mouse aims on its own)
		"aim_left": [axis(JOY_AXIS_RIGHT_X, -1.0)],
		"aim_right": [axis(JOY_AXIS_RIGHT_X, 1.0)],
		"aim_up": [axis(JOY_AXIS_RIGHT_Y, -1.0)],
		"aim_down": [axis(JOY_AXIS_RIGHT_Y, 1.0)],
		# Buttons
		"jump": [key(KEY_SPACE), pad(JOY_BUTTON_A)],
		"dash": [key(KEY_SHIFT), pad(JOY_BUTTON_B)],
		"melee": [key(KEY_J), mouse(MOUSE_BUTTON_RIGHT), pad(JOY_BUTTON_X)],
		"fire": [key(KEY_K), mouse(MOUSE_BUTTON_LEFT), axis(JOY_AXIS_TRIGGER_RIGHT, 1.0)],
		"parry": [key(KEY_L), key(KEY_Q), axis(JOY_AXIS_TRIGGER_LEFT, 1.0)],
		"sig": [key(KEY_E), key(KEY_I), mouse(MOUSE_BUTTON_MIDDLE), pad(JOY_BUTTON_Y)],
		"mode": [key(KEY_R), key(KEY_U), mouse(MOUSE_BUTTON_XBUTTON1), pad(JOY_BUTTON_RIGHT_SHOULDER)],
		"sub": [key(KEY_T), key(KEY_Y), pad(JOY_BUTTON_LEFT_SHOULDER)],
		"lock": [key(KEY_F), key(KEY_O), mouse(MOUSE_BUTTON_XBUTTON2), pad(JOY_BUTTON_RIGHT_STICK)],
		"ult": [key(KEY_V), key(KEY_N)],
		# Menus
		"pause": [key(KEY_ESCAPE), key(KEY_P), pad(JOY_BUTTON_START)],
		"help": [key(KEY_H), pad(JOY_BUTTON_BACK)],
	}
	for name in actions:
		var dz := 0.35 if name in ["fire", "parry"] else DEADZONE
		ProjectSettings.set_setting("input/" + name, { "deadzone": dz, "events": actions[name] })
	var err := ProjectSettings.save()
	print("input map written (%d actions): %s" % [actions.size(), error_string(err)])
	quit()
