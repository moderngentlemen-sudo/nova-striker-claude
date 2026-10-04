# The heads-up display (ui.js): a panel per player (health with strain and plating, the ultimate bar, and chips
# for Nova's attachment, secondary weapon, charge stages, Focus, booster fuel, Aegis and Overcharge), the boss
# bar, banners and toasts, the ultimate's title card, and the on-screen markers: player tags, lock-on
# reticles, the rocket jump's height readout and the sparring posts' labels. Reads the World; changes nothing.
# (Text sticks to characters the default font has: the web build has no system fonts to fall back on.)
class_name Hud
extends CanvasLayer

const STAGE := { "charging": "charging", "L1": "Level 1", "L2": "Level 2", "perfect": "Release!", "L3": "Level 3", "L4": "Level 4 · Beam" }
const ENEMY_NAMES := { "swarmer": "Swarmer", "shield": "Shieldbearer", "sniper": "Sniper", "brute": "Brute", "post": "Sparring post",
	"turret": "Turret", "drone": "Drone", "mortar": "Mortar", "charger": "Charger" }
const GOLD := "ffd889"
const PERFECT := "fff3c4"

var world: World
var cam: Camera3D
var root: Control
var panels := {}        # slot -> { box, mark, hp, strain, plate, ult, ult_txt, chips, key }
var markers := {}       # slot -> Label
var reticles := {}      # slot -> Label
var apex := {}          # slot -> Label
var post_labels := {}   # EnemySim -> Label
var banner: VBoxContainer
var banner_t := 0.0
var toast: Label
var toast_t := 0.0
var boss_box: VBoxContainer
var boss_name: Label
var boss_fill: ColorRect
var boss_back: ColorRect
var boss_armor: Label
var ult_card: VBoxContainer
var ult_key := ""
var hint: Label

func setup(w: World, camera: Camera3D) -> void:
	world = w; cam = camera
	layer = 5
	root = Control.new()
	root.set_anchors_preset(Control.PRESET_FULL_RECT)
	root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(root)
	# Player panels along the top left (each on its own plate)
	var row := HBoxContainer.new()
	row.name = "Panels"
	row.position = Vector2(16, 16)
	row.add_theme_constant_override("separation", 14)
	row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.add_child(row)
	# The banner (centre top), the toast (under it), the boss bar (top), the ultimate's card (centre)
	banner = VBoxContainer.new()
	banner.set_anchors_and_offsets_preset(Control.PRESET_CENTER_TOP)
	banner.anchor_top = 0.14; banner.anchor_bottom = 0.14
	banner.offset_top = 0; banner.offset_left = -400; banner.offset_right = 400
	banner.alignment = BoxContainer.ALIGNMENT_CENTER
	banner.add_child(_label("", 40, "ffffff", true))
	banner.add_child(_label("", 18, "dfe8f2", true))
	banner.visible = false
	root.add_child(banner)
	toast = _label("", 17, "ffffff", true)
	toast.set_anchors_and_offsets_preset(Control.PRESET_CENTER_BOTTOM)
	toast.offset_top = -48; toast.offset_bottom = -22; toast.offset_left = -400; toast.offset_right = 400
	toast.visible = false
	root.add_child(toast)
	boss_box = VBoxContainer.new()
	boss_box.set_anchors_and_offsets_preset(Control.PRESET_CENTER_TOP)
	boss_box.offset_top = 16; boss_box.offset_left = -235; boss_box.offset_right = 235
	boss_name = _label("", 18, "ffffff", true)
	boss_box.add_child(boss_name)
	boss_back = ColorRect.new(); boss_back.color = Color(0, 0, 0, 0.55); boss_back.custom_minimum_size = Vector2(470, 12)
	boss_fill = ColorRect.new(); boss_fill.color = Color("ff2e7e"); boss_fill.size = Vector2(470, 12)
	boss_back.add_child(boss_fill)
	boss_box.add_child(boss_back)
	boss_armor = _label("", 14, "e6e9f0", true)
	boss_box.add_child(boss_armor)
	boss_box.visible = false
	root.add_child(boss_box)
	ult_card = VBoxContainer.new()
	ult_card.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	ult_card.offset_left = -500; ult_card.offset_right = 500; ult_card.offset_top = -80; ult_card.offset_bottom = 80
	ult_card.add_child(_label("", 16, "dfe8f2", true))
	ult_card.add_child(_label("", 54, PERFECT, true))
	ult_card.add_child(_label("", 16, GOLD, true))
	ult_card.visible = false
	root.add_child(ult_card)
	hint = _label("", 14, "dfe8f2", false)
	hint.set_anchors_and_offsets_preset(Control.PRESET_TOP_RIGHT)
	hint.offset_left = -420; hint.offset_right = -14; hint.offset_top = 10
	hint.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	root.add_child(hint)

func _label(text: String, size: int, color: String, centred: bool) -> Label:
	var l := Label.new()
	l.text = text
	l.add_theme_font_size_override("font_size", size)
	l.add_theme_color_override("font_color", Color(color))
	l.add_theme_color_override("font_outline_color", Color(0.05, 0.07, 0.12, 0.9))
	l.add_theme_constant_override("outline_size", maxi(4, size / 5))
	if centred:
		l.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return l

func _bar(w: float, h: float, back: Color) -> ColorRect:
	var r := ColorRect.new(); r.color = back; r.custom_minimum_size = Vector2(w, h)
	r.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return r

func _fill(parent: Control, color: Color, h: float) -> ColorRect:
	var r := ColorRect.new(); r.color = color; r.size = Vector2(0, h)
	r.mouse_filter = Control.MOUSE_FILTER_IGNORE
	parent.add_child(r)
	return r

# ---- Events ----
func show_banner(text: String, sub := "", t := 2.6) -> void:
	banner.get_child(0).text = text; banner.get_child(1).text = sub
	banner.visible = true; banner_t = t

func show_toast(text: String, t := 2.4) -> void:
	toast.text = text; toast.visible = true; toast_t = t

func on_event(ev: Dictionary) -> void:
	match ev.type:
		"banner": show_banner(ev.text, ev.get("sub", ""))
		"checkpoint": show_toast("Checkpoint reached")
		"join": show_toast("Player %d joined as %s" % [ev.p.slot + 1, Tune.C.CHARS[ev.p.char].name])
		"downed": show_toast("Second Wind: getting back up" if ev.get("secondWind") else "Player %d is down. Stand next to them to revive" % (ev.p.slot + 1))
		"wipe": show_banner("Team down", "Returning to the last checkpoint")
		"ultReady": show_toast("P%d ultimate ready: %s" % [ev.p.slot + 1, "press V" if ev.p.device == "kbm" else "press both triggers"])

# ---- Each frame ----
func update(dt: float) -> void:
	if banner_t > 0:
		banner_t -= dt
		banner.visible = banner_t > 0
	if toast_t > 0:
		toast_t -= dt
		toast.visible = toast_t > 0
	_panels()
	_markers()
	_boss_bar()
	_ult_card()

func _chip(text: String, color := "dfe8f2") -> String:
	return "[color=#%s]%s[/color]   " % [color, text]

func _pips(n: int, total: int) -> String:
	return "%d/%d" % [n, total]

func _secs(t: float) -> String:
	return "%ds" % ceili(t / 60.0)

func _panels() -> void:
	var row: HBoxContainer = root.get_node("Panels")
	var seen := {}
	for p in world.players:
		seen[p.slot] = true
		if not panels.has(p.slot):
			panels[p.slot] = _make_panel(p, row)
		var P: Dictionary = panels[p.slot]
		var W := 300.0
		P.hp.size.x = W * clampf(p.hp / p.max_hp, 0, 1)
		P.strain.size.x = W * clampf((p.hp + p.strain) / p.max_hp, 0, 1)
		P.plate.size.x = W * clampf(p.plate / p.max_hp, 0, 1)
		var U: Dictionary = Tune.C.ULT
		P.ult.size.x = W * clampf(p.ult / U.max, 0, 1)
		var ready: bool = p.ult >= U.max and p.state != "ult"
		P.ult.color = Color(PERFECT) if ready else Color(GOLD)
		P.ult_txt.text = ("Ultimate · " + ("V" if p.device == "kbm" else "LT + RT")) if ready else ""
		var sub := _chips(p)
		if sub != P.key:
			P.key = sub
			P.chips.text = sub
	for slot in panels.keys():
		if not seen.has(slot):
			panels[slot].box.queue_free(); panels.erase(slot)

func _make_panel(p: PlayerSim, row: HBoxContainer) -> Dictionary:
	var col := Color(PlayerView.PLAYER_COLORS[p.slot % 4])
	var card := PanelContainer.new()
	var sb := StyleBoxFlat.new()
	sb.bg_color = Color(0.06, 0.09, 0.16, 0.86)
	sb.border_width_left = 4; sb.border_color = col
	sb.content_margin_left = 14; sb.content_margin_right = 12; sb.content_margin_top = 8; sb.content_margin_bottom = 10
	sb.set_corner_radius_all(4)
	card.add_theme_stylebox_override("panel", sb)
	card.mouse_filter = Control.MOUSE_FILTER_IGNORE
	row.add_child(card)
	var box := VBoxContainer.new()
	box.custom_minimum_size = Vector2(300, 0)
	box.add_theme_constant_override("separation", 3)
	box.mouse_filter = Control.MOUSE_FILTER_IGNORE
	card.add_child(box)
	var top := _label("P%d   %s · %s" % [p.slot + 1, Tune.C.CHARS[p.char].name, "Marksman"], 16, col.to_html(false), false)
	box.add_child(top)
	var bar := _bar(300, 12, Color(0, 0, 0, 0.55))
	box.add_child(bar)
	var strain := _fill(bar, Color("ff8a6a"), 12)
	var hp := _fill(bar, col, 12)
	var plate := _fill(bar, Color(0.85, 0.9, 1.0, 0.7), 4)
	var ub := _bar(300, 6, Color(0, 0, 0, 0.55))
	box.add_child(ub)
	var ult := _fill(ub, Color(GOLD), 6)
	var ult_txt := _label("", 12, PERFECT, false)
	box.add_child(ult_txt)
	var chips := RichTextLabel.new()
	chips.bbcode_enabled = true; chips.fit_content = true; chips.scroll_active = false
	chips.custom_minimum_size = Vector2(300, 0)
	chips.add_theme_font_size_override("normal_font_size", 13)
	chips.add_theme_color_override("font_outline_color", Color(0.05, 0.07, 0.12, 0.9))
	chips.add_theme_constant_override("outline_size", 4)
	chips.mouse_filter = Control.MOUSE_FILTER_IGNORE
	box.add_child(chips)
	return { "box": card, "hp": hp, "strain": strain, "plate": plate, "ult": ult, "ult_txt": ult_txt, "chips": chips, "key": "" }

func _chips(p: PlayerSim) -> String:
	if p.state == "downed":
		return _chip("Second Wind..." if p.auto_revive > 0 else "Down · revive %d%% · %s" % [floori(p.revive / 1.2), _secs(p.downed_t)], "ff8a6a")
	if p.state == "dead":
		return _chip("Respawning in " + _secs(p.respawn_t), "ff8a6a")
	var s := ""
	if p.marksman():
		var A: Dictionary = Tune.C.ATTACH_LOOK[p.attachment]
		var S: Dictionary = Tune.C.SUB_LOOK.get(p.sub, Tune.C.SUB_LOOK.scatter)
		var M: Dictionary = Tune.C.MARKSMAN
		var out := ""
		if (p.sub == "disc" or p.sub == "well") and world.sub_out(p, p.sub):
			out = " · out" if p.sub == "disc" else " · open"
		s += _chip(A.name, A.tint.substr(1)) + _chip(S.name + out, S.tint.substr(1))
		if p.state == "beam" and p.beam != null:
			s += _chip("Beam %.1fs" % (p.beam.t / 60.0), PERFECT)
		var stage := p.charge_stage()
		if STAGE.has(stage):
			s += _chip(STAGE[stage], PERFECT if stage == "perfect" or stage == "L4" else GOLD)
		var bstage := p.burst_stage()
		if STAGE.has(bstage):
			s += _chip(S.name + " " + STAGE[bstage], PERFECT if bstage == "perfect" else GOLD)
		var f := floori(p.focus)
		s += _chip("Focus " + _pips(f, int(M.focus.max)), GOLD if f > 0 else "9aa6b5")
		s += _chip("Fuel %d%%" % roundi(p.fuel / M.boost.fuel * 100), "bfe8ff")
		if p.aegis != null:
			s += _chip("Aegis %d%%" % roundi(maxf(0, p.aegis.hp / p.aegis.max * 100)), PERFECT)
		else:
			s += _chip("Aegis ready" if p.aegis_cd == 0 else "Aegis " + _secs(p.aegis_cd), GOLD if p.aegis_cd == 0 else "9aa6b5")
		if p.overcharge > 0:
			s += _chip("Overcharged %d%%" % roundi(p.overcharge / Tune.C.AEGIS.over.max * 100), "ff9f40")
	else:
		s += _chip("Bulwark ready" if p.bulwark_cd == 0 else "Bulwark " + _secs(p.bulwark_cd), GOLD if p.bulwark_cd == 0 else "9aa6b5")
	if p.vb_tier() > 0:
		s += _chip("VB %d" % p.vb_tier(), "ff7fb2")
	# Common chips: a charging dash, a held ground pound, the lock-on target, boosts
	var C: Array = Tune.C.DASH_CHARGE.charge
	var t: float = p.dash_charge_t if p.state == "dashCharge" else 0.0
	var L := 3 if t >= C[2] else (2 if t >= C[1] else (1 if t >= C[0] else 0))
	if L > 0:
		s += _chip("Dash %d" % L, PERFECT if L == 3 else GOLD)
	if p.state == "pound" and p.pound != null and p.pound.phase == "hold" and p.pound.level > 0:
		s += _chip("Pound %d" % p.pound.level, PERFECT if p.pound.level == 3 else GOLD)
	if p.lock_t != null:
		s += _chip("Lock: " + ENEMY_NAMES.get(p.lock_t.type, "Boss" if p.lock_t.boss else "Target"), "ff7fb2")
	elif p.lock_suspend:
		s += _chip("Lock paused", "9aa6b5")
	if p.plate > 0.5:
		s += _chip("Plating %d" % ceili(p.plate), "e6e9f0")
	if p.overclock_t > 0:
		s += _chip("Overclock " + _secs(p.overclock_t), "7fe3ff")
	if p.fury_t > 0:
		s += _chip("Fury " + _secs(p.fury_t), "ff5a4a")
	return s

func _screen(x: float, y: float) -> Dictionary:
	var at := PathFrame.point(x, y)
	if cam.is_position_behind(at):
		return { "pos": Vector2(-1000, -1000), "vis": false }
	var s := cam.unproject_position(at)
	var r := root.get_viewport_rect().size
	return { "pos": s, "vis": s.x > -40 and s.x < r.x + 40 and s.y > -40 and s.y < r.y + 40 }

func _place(l: Control, s: Vector2, ax := 0.5, ay := 1.0) -> void:
	l.position = s - Vector2(l.size.x * ax, l.size.y * ay)

func _markers() -> void:
	var r := root.get_viewport_rect().size
	var seen := {}
	var on_target := {}
	for p in world.players:
		seen[p.slot] = true
		var col: String = PlayerView.PLAYER_COLORS[p.slot % 4]
		# The player tag over their head (kept on screen)
		if not markers.has(p.slot):
			markers[p.slot] = _label("", 14, col, true); root.add_child(markers[p.slot])
		var m: Label = markers[p.slot]
		m.text = "P%d%s" % [p.slot + 1, " · DOWN" if p.state == "downed" else ""]
		var s: Vector2 = _screen(p.x, p.y + p.h + 0.45).pos
		_place(m, Vector2(clampf(s.x, 16, r.x - 16), clampf(s.y, 16, r.y - 16)))
		m.visible = p.state != "dead"
		# The lock-on reticle on their target; several on one target nest
		if not reticles.has(p.slot):
			reticles[p.slot] = Reticle.new(); reticles[p.slot].color = Color(col); root.add_child(reticles[p.slot])
		var ret: Reticle = reticles[p.slot]
		var tg = p.lock_t if p.state != "dead" and p.state != "downed" else null
		if tg == null:
			ret.visible = false
		else:
			var n: int = on_target.get(tg, 0); on_target[tg] = n + 1
			var sc := _screen(tg.x, tg.y + tg.h * 0.55)
			ret.radius = (46 + minf(90, tg.h * 18) + n * 14) / 2.0
			ret.position = sc.pos
			if ret.target != tg:
				ret.target = tg; ret.pop = 1.0
			ret.visible = sc.vis
		# The rocket jump's height readout beside its apex marker while Nova lines one up
		if not apex.has(p.slot):
			apex[p.slot] = _label("", 15, GOLD, false); root.add_child(apex[p.slot])
		var al: Label = apex[p.slot]
		var pv = world.rocket_preview(p) if p.charge_t > 0 else null
		if pv == null:
			al.visible = false
		else:
			var sa := _screen(pv.x, pv.apex)
			al.text = "Apex %.1f m%s" % [pv.apex - p.y, " · Perfect" if pv.perfect else ""]
			al.add_theme_color_override("font_color", Color(PERFECT if pv.perfect else GOLD))
			_place(al, sa.pos + Vector2(34, 0), 0.0, 0.5)
			al.visible = sa.vis
	for slot in markers.keys():
		if not seen.has(slot):
			for d in [markers, reticles, apex]:
				if d.has(slot):
					d[slot].queue_free(); d.erase(slot)
	# The sparring posts' teaching labels
	var alive := {}
	for e in world.enemies:
		if e.type != "post":
			continue
		alive[e] = true
		if not post_labels.has(e):
			post_labels[e] = _label("", 14, "ffffff", true); root.add_child(post_labels[e])
		var l: Label = post_labels[e]
		l.text = e.label if e.label != "" else "Sparring post: step close"
		var cat: String = e.atk.cat if e.state == "windup" and e.atk != null else ""
		l.add_theme_color_override("font_color", Color("ff5a9a") if cat != "" else Color.WHITE)
		_place(l, _screen(e.x, e.y + e.h + 0.8).pos)
		l.visible = absf(e.x - world.cam.x) <= world.cam.halfW + 1
	for e in post_labels.keys():
		if not alive.has(e):
			post_labels[e].queue_free(); post_labels.erase(e)

func _boss_bar() -> void:
	var e = null
	for q in world.enemies:
		if q.boss and not q.dead and absf(q.x - world.cam.x) < world.cam.halfW + 14:
			e = q; break
	if e == null:
		boss_box.visible = false
		return
	var B: Dictionary = Tune.C.BOSS[e.type]
	boss_box.visible = true
	boss_name.text = "%s  ·  %s" % [B.name, "Phase two" if e.phase == 2 else B.title]
	boss_fill.size.x = 470 * clampf(e.hp / e.max_hp, 0, 1)
	var shielded: bool = e.state == "roar" or e.state == "intro"
	boss_fill.color = Color("9aa6b5") if shielded else (Color("ff5a4a") if e.phase == 2 else Color("ff2e7e"))
	boss_armor.text = ("Armor %d/%d" % [e.armor, e.armor_max]) if e.armor_max else ""

# The ultimate: during the call its name fills the screen; while it plays out it sits small at the top
func _ult_card() -> void:
	var Uc = world.ult_cast
	if Uc == null:
		ult_card.visible = false; ult_key = ""
		return
	var key := "%s|%s|%d" % [Uc.phase, Uc.name, Uc.members.size()]
	if key == ult_key:
		return
	ult_key = key
	ult_card.visible = true
	ult_card.get_child(0).text = " + ".join(Uc.members.map(func(m): return "P%d %s" % [m.slot + 1, Tune.C.CHARS[m.char].name]))
	ult_card.get_child(1).text = Uc.name
	ult_card.get_child(1).add_theme_font_size_override("font_size", 54 if Uc.phase == "cast" else 26)
	if Uc.phase == "cast":
		banner.visible = false; banner_t = 0
		ult_card.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
		ult_card.offset_left = -500; ult_card.offset_right = 500; ult_card.offset_top = -80; ult_card.offset_bottom = 80
	else:
		ult_card.set_anchors_and_offsets_preset(Control.PRESET_CENTER_TOP)
		ult_card.offset_left = -500; ult_card.offset_right = 500; ult_card.offset_top = 60

# A lock-on reticle (ui.js .reticle): four arcs turning round the target, popping in when the target changes
class Reticle extends Control:
	var color := Color.WHITE
	var radius := 30.0
	var target = null
	var pop := 0.0
	var spin := 0.0

	func _init() -> void:
		mouse_filter = Control.MOUSE_FILTER_IGNORE

	func _process(dt: float) -> void:
		spin += dt * 1.6
		pop = maxf(0.0, pop - dt * 5)
		queue_redraw()

	func _draw() -> void:
		var r := radius * (1.0 + 0.35 * pop)
		var outline := Color(0.05, 0.07, 0.12, 0.8)
		for i in 4:
			var a := spin + i * TAU / 4
			draw_arc(Vector2.ZERO, r, a - 0.45, a + 0.45, 12, outline, 6.0, true)
			draw_arc(Vector2.ZERO, r, a - 0.45, a + 0.45, 12, color, 3.0, true)
		draw_circle(Vector2.ZERO, 3.0, color)
