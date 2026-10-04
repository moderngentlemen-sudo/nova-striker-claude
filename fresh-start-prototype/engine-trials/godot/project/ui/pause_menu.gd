# The pause menu (ui.js buildPause and buildHelp): resume, jump to any zone or boss, the settings the port
# uses so far, and the controls for Nova. Godot's focus system gives controller navigation: the D-pad or left
# stick moves between controls, left and right change a list or slider, A selects and B (or Start) resumes.
# Settings are remembered between runs in user://settings.cfg.
class_name PauseMenu
extends CanvasLayer

signal resume
signal zone(id: String)
signal boss(id: String)

const SAVE := "user://settings.cfg"
const ZONES := [["gym", "Movement Gym"], ["arena", "Concourse Lock"], ["tower", "Storm Spire Climb"], ["skyline", "Skyline Relay"],
	["foundry", "Helix Foundry"], ["undercity", "Undercity Descent"]]
const BOSSES := [["warden", "Boss: Lockwarden"], ["stormcaller", "Boss: Stormcaller"]]
const DEFS := [
	{ "key": "difficulty", "label": "Difficulty", "opts": [["easy", "Easy"], ["normal", "Normal"], ["hard", "Hard"]] },
	{ "key": "aimAssist", "label": "Aim assist", "bool": true },
	{ "key": "lockOn", "label": "Lock-on", "bool": true },
	{ "key": "lockMode", "label": "Lock-on mode", "opts": [["auto", "Automatic"], ["manual", "On press"]] },
	{ "key": "dashCharge", "label": "Charged dash", "bool": true },
	{ "key": "shake", "label": "Screen shake", "bool": true },
	{ "key": "fov", "label": "Field of view", "range": [26, 50, 1] },
	{ "key": "p1Aim", "label": "Keyboard aim", "opts": [["mouse", "Mouse"], ["stick", "Movement keys (8-way)"]] },
]
const CONTROLS := """[b]Nova · Marksman kit[/b]   (controller · keyboard and mouse)
Move, crouch: left stick · A/D, S          Jump, double jump, wall jump: A · Space
Light boosters: hold A after the double jump · hold Space          Dash (8-way), slide (down + dash): B · Shift
Charged dash: hold B standing still, aim, let go · hold Shift
Fire, and hold to charge the attachment (let go on the flash after level 3 for a Perfect Release; hold to level 4 for the beam): RT · left click or K
Rocket jump: aim at your feet, charge, let go          Aim: right stick · mouse
Melee (close) or the secondary weapon (tap or charge): X · right click or J          Ground pound: down + X in the air
Rising attack, the Solar Uppercut: up + X · W + melee          Dodge (perfect dodge slows enemies): LT · Q or L
Aegis (press again to detonate): Y · E or middle click          Switch attachment: RB · R          Switch secondary: LB · T
Lock-on (tap switches, hold lets go): R3 · F          Ultimate, Supernova, when the bar is full: LT + RT · V
Pause: Start · Esc or P"""

var root: Control
var first: Control
var built := false

func _ready() -> void:
	layer = 10
	process_mode = Node.PROCESS_MODE_ALWAYS
	visible = false

static func load_settings() -> void:
	var cf := ConfigFile.new()
	if cf.load(SAVE) != OK:
		return
	for d in DEFS:
		if cf.has_section_key("settings", d.key):
			Tune.settings[d.key] = cf.get_value("settings", d.key)

static func save_settings() -> void:
	var cf := ConfigFile.new()
	for d in DEFS:
		cf.set_value("settings", d.key, Tune.settings.get(d.key))
	cf.save(SAVE)

func open() -> void:
	if not built:
		_build()
	visible = true
	first.grab_focus()

func close() -> void:
	visible = false

func _unhandled_input(ev: InputEvent) -> void:
	if not visible:
		return
	if ev.is_action_pressed("ui_cancel") or ev.is_action_pressed("pause"):
		get_viewport().set_input_as_handled()
		resume.emit()

func _build() -> void:
	built = true
	var dim := ColorRect.new()
	dim.color = Color(0.03, 0.05, 0.1, 0.72)
	dim.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(dim)
	var scroll := ScrollContainer.new()
	scroll.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	scroll.offset_left = 60; scroll.offset_right = -60; scroll.offset_top = 40; scroll.offset_bottom = -40
	scroll.follow_focus = true
	add_child(scroll)
	var col := VBoxContainer.new()
	col.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	col.add_theme_constant_override("separation", 12)
	scroll.add_child(col)
	root = col
	col.add_child(_text("PAUSED", 14, "ffd889"))
	col.add_child(_text("Nova Striker · Godot build", 30, "ffffff"))
	var row := HFlowContainer.new()
	row.add_theme_constant_override("h_separation", 8); row.add_theme_constant_override("v_separation", 8)
	col.add_child(row)
	first = _button(row, "Resume", func(): resume.emit())
	for z in ZONES:
		_button(row, z[1], func(): zone.emit(z[0]))
	for b in BOSSES:
		_button(row, b[1], func(): boss.emit(b[0]))
	var grid := GridContainer.new()
	grid.columns = 4
	grid.add_theme_constant_override("h_separation", 16); grid.add_theme_constant_override("v_separation", 8)
	col.add_child(grid)
	for d in DEFS:
		grid.add_child(_text(d.label, 16, "dfe8f2"))
		grid.add_child(_control(d))
	var ctl := RichTextLabel.new()
	ctl.bbcode_enabled = true; ctl.fit_content = true; ctl.scroll_active = false
	ctl.add_theme_font_size_override("normal_font_size", 15)
	ctl.add_theme_font_size_override("bold_font_size", 17)
	ctl.text = CONTROLS
	col.add_child(ctl)
	col.add_child(_text("Controller: D-pad or left stick to move · left/right changes a list or slider · A selects · B or Start resumes", 13, "9aa6b5"))

func _text(t: String, size: int, color: String) -> Label:
	var l := Label.new()
	l.text = t
	l.add_theme_font_size_override("font_size", size)
	l.add_theme_color_override("font_color", Color(color))
	return l

func _button(parent: Control, label: String, fn: Callable) -> Button:
	var b := Button.new()
	b.text = label
	b.custom_minimum_size = Vector2(0, 40)
	b.add_theme_font_size_override("font_size", 17)
	b.pressed.connect(fn)
	parent.add_child(b)
	return b

func _control(d: Dictionary) -> Control:
	var key: String = d.key
	if d.get("bool"):
		var c := CheckButton.new()
		c.button_pressed = bool(Tune.settings.get(key, false))
		c.toggled.connect(func(on): Tune.settings[key] = on; save_settings())
		return c
	if d.has("range"):
		var box := HBoxContainer.new()
		var s := HSlider.new()
		s.min_value = d.range[0]; s.max_value = d.range[1]; s.step = d.range[2]
		s.value = float(Tune.settings.get(key, d.range[0]))
		s.custom_minimum_size = Vector2(180, 0)
		s.focus_mode = Control.FOCUS_ALL
		var out := _text(str(int(s.value)), 15, "ffd889")
		s.value_changed.connect(func(v): Tune.settings[key] = v; out.text = str(int(v)); save_settings())
		box.add_child(s); box.add_child(out)
		return box
	var o := OptionButton.new()
	for i in d.opts.size():
		o.add_item(d.opts[i][1], i)
		if d.opts[i][0] == Tune.settings.get(key):
			o.select(i)
	o.item_selected.connect(func(i): Tune.settings[key] = d.opts[i][0]; save_settings())
	return o
