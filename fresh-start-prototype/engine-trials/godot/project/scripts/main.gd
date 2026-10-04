# Engine trial: Godot 4.3. Builds the Helix Foundry stretch from level.json (exported from level.js): Godot
# 2D physics bodies for the collision, and 3D meshes laid along the curved path for the picture. Nova runs
# on the 2D bodies (nova.gd) and is drawn in 3D where the path puts her. The camera follows the way the
# three.js build's does.
extends Node3D

const PathFrame := preload("res://scripts/path.gd")
const NovaBody := preload("res://scripts/nova.gd")
const S := 100.0

var data: Dictionary
var path
var nova: CharacterBody2D
var rig := {}
var camera: Camera3D
var sun: DirectionalLight3D
var hud: Label
var breakables := {}   # StaticBody2D -> { box, mesh, hp }
var cam := { "x": 402.0, "y": 3.0, "dist": 16.0 }
var lead := 0.0
var trauma := 0.0
var phase := 0.0
var yaw := 0.0
var lean := 0.0
var squash := 1.0
var autotest := false
var spark_mat: StandardMaterial3D

func _ready() -> void:
	data = JSON.parse_string(FileAccess.get_file_as_string("res://level.json"))
	path = PathFrame.new(data.segs)
	_input_map()
	_environment()
	_level()
	_landmarks()
	_build_rig()
	nova = NovaBody.new()
	add_child(nova)
	nova.setup(data, Vector2(data.route.spawn[0], data.route.spawn[1]))
	# On the web, ?x=600 starts further along the stretch (the helix is at 586-662)
	if OS.has_feature("web"):
		var q = JavaScriptBridge.eval("new URLSearchParams(location.search).get('x') || ''")
		if q is String and q.is_valid_float():
			nova.place(Vector2(float(q), 30.0))
	nova.event.connect(_on_event)
	camera = Camera3D.new()
	camera.fov = 40
	camera.near = 0.1
	camera.far = 1000
	add_child(camera)
	camera.make_current()
	snap_camera()
	hud = Label.new()
	hud.position = Vector2(12, 10)
	hud.add_theme_color_override("font_color", Color(1, 0.96, 0.9))
	hud.add_theme_color_override("font_shadow_color", Color(0, 0, 0, 0.6))
	var layer := CanvasLayer.new()
	add_child(layer)
	layer.add_child(hud)
	var keys := Label.new()
	keys.text = "A/D move · Space jump · Shift dash · S crouch/slide · J swing (breaks crates)"
	keys.add_theme_color_override("font_color", Color(1, 0.96, 0.9, 0.75))
	keys.anchor_top = 1; keys.anchor_bottom = 1; keys.offset_left = 12; keys.offset_top = -30
	layer.add_child(keys)
	autotest = "--autotest" in OS.get_cmdline_user_args()

# ---- Controls (keyboard and controller), set up in code rather than the project file ----
func _input_map() -> void:
	var bind := func(action: String, keys: Array, buttons: Array, axis: int = -1, axis_dir: float = 0):
		if not InputMap.has_action(action):
			InputMap.add_action(action, 0.3)
		for k in keys:
			var e := InputEventKey.new(); e.physical_keycode = k; InputMap.action_add_event(action, e)
		for b in buttons:
			var e := InputEventJoypadButton.new(); e.button_index = b; InputMap.action_add_event(action, e)
		if axis >= 0:
			var e := InputEventJoypadMotion.new(); e.axis = axis; e.axis_value = axis_dir; InputMap.action_add_event(action, e)
	bind.call("left", [KEY_A, KEY_LEFT], [JOY_BUTTON_DPAD_LEFT], JOY_AXIS_LEFT_X, -1.0)
	bind.call("right", [KEY_D, KEY_RIGHT], [JOY_BUTTON_DPAD_RIGHT], JOY_AXIS_LEFT_X, 1.0)
	bind.call("up", [KEY_W, KEY_UP], [JOY_BUTTON_DPAD_UP], JOY_AXIS_LEFT_Y, -1.0)
	bind.call("down", [KEY_S, KEY_DOWN], [JOY_BUTTON_DPAD_DOWN], JOY_AXIS_LEFT_Y, 1.0)
	bind.call("jump", [KEY_SPACE], [JOY_BUTTON_A])
	bind.call("dash", [KEY_SHIFT], [JOY_BUTTON_RIGHT_SHOULDER, JOY_BUTTON_B])
	bind.call("melee", [KEY_J], [JOY_BUTTON_X])

# ---- Light and air: the foundry's warm haze ----
func _environment() -> void:
	var env := Environment.new()
	var sky_mat := ProceduralSkyMaterial.new()
	sky_mat.sky_top_color = Color("3a4f78")
	sky_mat.sky_horizon_color = Color("d99a6a")
	sky_mat.ground_horizon_color = Color("d99a6a")
	sky_mat.ground_bottom_color = Color("f2c79a")
	sky_mat.sun_angle_max = 20
	var sky := Sky.new(); sky.sky_material = sky_mat
	env.background_mode = Environment.BG_SKY
	env.sky = sky
	# (a flat warm ambient and no sky reflections: the Compatibility renderer used for the web doesn't blur the
	# sky for rough surfaces, so sky lighting washed every surface out)
	env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.ambient_light_color = Color("8a6f5c")
	env.ambient_light_energy = 0.55
	env.reflected_light_source = Environment.REFLECTION_SOURCE_DISABLED
	env.tonemap_mode = Environment.TONE_MAPPER_ACES
	env.tonemap_exposure = 0.9
	env.glow_enabled = true
	env.glow_intensity = 0.35
	env.glow_bloom = 0.0
	env.glow_hdr_threshold = 1.2
	env.fog_enabled = true
	env.fog_light_color = Color("e6c3a0")
	env.fog_density = 0.0045
	env.fog_sky_affect = 0.4
	var we := WorldEnvironment.new(); we.environment = env
	add_child(we)
	sun = DirectionalLight3D.new()
	sun.light_color = Color("ffc690")
	sun.light_energy = 0.95
	sun.shadow_enabled = true
	sun.directional_shadow_max_distance = 60
	sun.rotation = Vector3(deg_to_rad(-52), deg_to_rad(-35), 0)
	add_child(sun)

func _mat(color: String, rough := 0.7, metal := 0.1, emissive := "", energy := 1.0) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = Color(color); m.roughness = rough; m.metallic = metal
	if emissive != "":
		m.emission_enabled = true; m.emission = Color(emissive); m.emission_energy_multiplier = energy
	return m

# ---- The level: a static body per box for the collision, and the meshes batched per material ----
func _level() -> void:
	var M := {
		"ground": _mat("8d7b6e", 0.85), "cap": _mat("c9b6a0", 0.6), "walk": _mat("6d7a88", 0.4, 0.5),
		"trim": _mat("5a3418", 0.6, 0.0, "ff8a2a", 2.0), "dark": _mat("3a3430"), "block": _mat("a08f80"),
	}
	var tools := {}
	for k in M:
		var t := SurfaceTool.new(); t.begin(Mesh.PRIMITIVE_TRIANGLES); tools[k] = t
	for b in data.boxes:
		var body := StaticBody2D.new()
		var cs := CollisionShape2D.new()
		var rect := RectangleShape2D.new()
		rect.size = Vector2((b.x1 - b.x0) * S, (b.y1 - b.y0) * S)
		cs.shape = rect
		cs.position = Vector2((b.x0 + b.x1) / 2 * S, -(b.y0 + b.y1) / 2 * S)
		if b.type == "o":
			cs.one_way_collision = true
			body.set_meta("oneway", true)
		body.add_child(cs)
		add_child(body)
		var depth: float = 2.6 if b.type == "o" else (1.8 if b.tag in ["panel", "column", "pillar"] else 4.4)
		if b.type == "d":   # breakable: its own mesh
			var D: Dictionary = data.destruct[b.tag]
			var mi := _piece_mesh(b.x0, b.x1, b.y0, b.y1, depth * 0.6, _mat(D.color, 0.05 if b.tag == "glass" else 0.8))
			if b.tag == "glass":
				mi.material_override.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
				mi.material_override.albedo_color.a = 0.45
			body.set_meta("breakable", true)
			breakables[body] = { "box": b, "mesh": mi, "hp": float(D.hp), "min": float(D.min) }
			continue
		var mat_key: String = "dark" if b.tag == "bound" else ("walk" if b.type == "o" else ("block" if b.tag == "block" else "ground"))
		var cap_h: float = 0.0 if b.type == "o" else minf(0.3, (b.y1 - b.y0) * 0.3)
		var n := maxi(1, ceili((b.x1 - b.x0) / 1.2)) if path.curved(b.x0, b.x1) else 1
		for i in n:
			var a: float = b.x0 + (b.x1 - b.x0) * i / n
			var c: float = b.x0 + (b.x1 - b.x0) * (i + 1) / n
			_append_box(tools[mat_key], a, c, b.y0, b.y1 - cap_h, depth)
			if cap_h > 0:
				_append_box(tools["cap"], a, c, b.y1 - cap_h, b.y1, depth + 0.1)
			if b.tag != "bound":
				_append_box(tools["trim"], a, c, b.y1 - cap_h - 0.06, b.y1 - cap_h, 0.06, depth / 2 + 0.03)
	for k in tools:
		var mi := MeshInstance3D.new()
		mi.mesh = tools[k].commit()
		mi.material_override = M[k]
		add_child(mi)

func _box_transform(x0: float, x1: float, y0: float, y1: float, z: float) -> Transform3D:
	var xm := (x0 + x1) / 2
	var pos: Vector3 = path.point(xm, (y0 + y1) / 2, z)
	return Transform3D(Basis(Vector3.UP, path.yaw(xm)), pos)

func _append_box(t: SurfaceTool, x0: float, x1: float, y0: float, y1: float, depth: float, z := 0.0) -> void:
	var bm := BoxMesh.new()
	bm.size = Vector3(x1 - x0 + (0.04 if path.curved(x0, x1) else 0.0), y1 - y0, depth)
	# (the box's arrays, transformed into place: works under the headless dummy renderer too, unlike append_from)
	var xf := _box_transform(x0, x1, y0, y1, z)
	var arr := bm.get_mesh_arrays()
	var verts: PackedVector3Array = arr[Mesh.ARRAY_VERTEX]
	var norms: PackedVector3Array = arr[Mesh.ARRAY_NORMAL]
	for i in arr[Mesh.ARRAY_INDEX]:
		t.set_normal(xf.basis * norms[i])
		t.add_vertex(xf * verts[i])

func _piece_mesh(x0: float, x1: float, y0: float, y1: float, depth: float, mat: Material) -> MeshInstance3D:
	var bm := BoxMesh.new()
	bm.size = Vector3(x1 - x0, y1 - y0, depth)
	var mi := MeshInstance3D.new()
	mi.mesh = bm; mi.material_override = mat
	mi.transform = _box_transform(x0, x1, y0, y1, 0)
	add_child(mi)
	return mi

# The reactor core inside the helix, the smelter dome, stacks out in the haze
func _landmarks() -> void:
	var helix: Dictionary
	var smelter: Dictionary
	for g in data.segs:
		if g.kind == "arc" and g.r == 15: helix = g
		if g.kind == "arc" and g.r == 22: smelter = g
	var glow := _mat("5a3418", 0.5, 0.0, "ff8a2a", 2.5)
	var core := MeshInstance3D.new()
	var cyl := CylinderMesh.new(); cyl.height = 46; cyl.top_radius = 4.5; cyl.bottom_radius = 4.5; cyl.radial_segments = 40
	core.mesh = cyl; core.material_override = _mat("2e3542", 0.35, 0.8)
	core.position = Vector3(helix.C.x, 17, helix.C.z)
	add_child(core)
	for i in 6:
		var band := MeshInstance3D.new()
		var tm := TorusMesh.new(); tm.inner_radius = 4.45; tm.outer_radius = 4.85; tm.rings = 48
		band.mesh = tm; band.material_override = glow
		band.position = Vector3(helix.C.x, 2 + i * 7, helix.C.z)
		add_child(band)
	var dome := MeshInstance3D.new()
	var sm := SphereMesh.new(); sm.radius = 11; sm.height = 11; sm.is_hemisphere = true
	dome.mesh = sm; dome.material_override = _mat("6b5a52", 0.5, 0.5)
	dome.position = Vector3(smelter.C.x, -1, smelter.C.z)
	add_child(dome)
	var ring := MeshInstance3D.new()
	var rt := TorusMesh.new(); rt.inner_radius = 10.7; rt.outer_radius = 11.3; rt.rings = 64
	ring.mesh = rt; ring.material_override = glow
	ring.position = Vector3(smelter.C.x, 0.4, smelter.C.z)
	add_child(ring)
	var x0: float = data.route.x0
	var x1: float = data.route.x1
	var placed := 0
	for i in 40:
		if placed >= 9: break
		var f: Array = path.frame(x0 + fmod(i * 37.0, x1 - x0))
		var back := 30.0 + (i % 4) * 16
		var hgt := 36.0 + (i % 5) * 7
		var p: Vector3 = f[0] - f[2] * back
		if not _clear_of_view(p, 6.0): continue
		placed += 1
		var stack := MeshInstance3D.new()
		var sc := CylinderMesh.new(); sc.height = hgt; sc.top_radius = 1.5; sc.bottom_radius = 2.1
		stack.mesh = sc; stack.material_override = _mat("3a3430")
		stack.position = p + Vector3(0, hgt / 2 - 6, 0)
		add_child(stack)

func _clear_of_view(p: Vector3, r: float) -> bool:
	var u: float = data.route.x0
	while u < data.route.x1:
		var f: Array = path.frame(u)
		var a: Vector3 = f[0]
		var b: Vector3 = a + f[2] * 22
		var ab := Vector2(b.x - a.x, b.z - a.z)
		var t := clampf(Vector2(p.x - a.x, p.z - a.z).dot(ab) / ab.length_squared(), 0, 1)
		if Vector2(p.x - a.x - ab.x * t, p.z - a.z - ab.y * t).length() < r:
			return false
		u += 2
	return true

# ---- Nova's rig: primitives on pivots, posed each frame ----
func _build_rig() -> void:
	var suit := _mat("e9eef5", 0.35, 0.2)
	var accent := _mat("5ac8fa", 0.4, 0.0, "5ac8fa", 1.6)
	var dark := _mat("2a3140", 0.6)
	var root := Node3D.new(); add_child(root)
	var body := Node3D.new(); root.add_child(body)
	var hips := Node3D.new(); body.add_child(hips); hips.position.y = 0.86
	var mesh := func(m: Mesh, mat: Material, parent: Node3D, p: Vector3) -> MeshInstance3D:
		var mi := MeshInstance3D.new(); mi.mesh = m; mi.material_override = mat; mi.position = p; parent.add_child(mi); return mi
	var cap := func(height: float, radius: float) -> CapsuleMesh:
		var c := CapsuleMesh.new(); c.height = height; c.radius = radius; return c
	var torso: MeshInstance3D = mesh.call(cap.call(0.72, 0.24), suit, hips, Vector3(0, 0.38, 0))
	var plate := BoxMesh.new(); plate.size = Vector3(0.08, 0.22, 0.36)
	mesh.call(plate, accent, torso, Vector3(0.21, 0.1, 0))
	var hs := SphereMesh.new(); hs.radius = 0.19; hs.height = 0.38
	var head: MeshInstance3D = mesh.call(hs, suit, hips, Vector3(0, 0.92, 0))
	var visor := BoxMesh.new(); visor.size = Vector3(0.12, 0.09, 0.3)
	mesh.call(visor, accent, head, Vector3(0.15, 0.02, 0))
	var limb := func(len: float, r: float, mat: Material, at: Vector3) -> Node3D:
		var pivot := Node3D.new(); pivot.position = at; hips.add_child(pivot)
		mesh.call(cap.call(len, r), mat, pivot, Vector3(0, -len / 2 + r, 0)); return pivot
	rig = {
		"root": root, "body": body, "hips": hips, "head": head,
		"legL": limb.call(0.86, 0.11, dark, Vector3(0, 0.02, 0.12)), "legR": limb.call(0.86, 0.11, dark, Vector3(0, 0.02, -0.12)),
		"armL": limb.call(0.62, 0.085, suit, Vector3(0, 0.66, 0.3)), "armR": limb.call(0.62, 0.085, suit, Vector3(0, 0.66, -0.3)),
	}
	# the scarf: a ribbon of points that trail the neck, rebuilt each frame with ImmediateMesh
	var scarf := MeshInstance3D.new()
	scarf.mesh = ImmediateMesh.new()
	var sm := accent.duplicate(); sm.cull_mode = BaseMaterial3D.CULL_DISABLED
	scarf.material_override = sm
	add_child(scarf)
	rig.scarf = scarf
	rig.pts = []
	for i in 8: rig.pts.append(Vector3.ZERO)
	spark_mat = _mat("ffffff", 1.0, 0.0, "ffd28a", 3.0)
	spark_mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	spark_mat.vertex_color_use_as_albedo = true

func _pose(dt: float, alpha: float) -> void:
	var p: Vector2 = nova.prev_pos.lerp(nova.cur_pos, alpha)
	var root: Node3D = rig.root
	root.position = path.point(p.x, p.y)
	var want: float = path.yaw(p.x) + (PI if nova.facing < 0 else 0.0)
	yaw += wrapf(want - yaw, -PI, PI) * minf(1, dt / 0.08)
	root.rotation.y = yaw
	var speed := absf(nova.vx)
	var run: float = minf(1, speed / nova.C.run) if nova.on_ground else 0.0
	phase += dt * (4 + speed * 1.6) * (1.0 if nova.on_ground else 0.2)
	var sw := sin(phase) * 0.9 * run
	rig.legL.rotation.z = sw; rig.legR.rotation.z = -sw
	rig.armL.rotation.z = -sw * 0.7; rig.armR.rotation.z = sw * 0.7
	if not nova.on_ground:
		var t := clampf(nova.vy / 15, -1, 1)
		rig.legL.rotation.z = 0.5 * t + 0.3; rig.legR.rotation.z = -0.3 - 0.2 * t
		rig.armL.rotation.z = -0.6 - 0.4 * t; rig.armR.rotation.z = -0.6 - 0.4 * t
	if nova.wall_sliding:
		rig.armL.rotation.z = 2.4; rig.armR.rotation.z = 1.9
	if nova.swing_t > 0:
		rig.armL.rotation.z = lerpf(2.6, -1.0, 1.0 - nova.swing_t / 14.0)
	var dashing: bool = nova.state == "dash" or nova.state == "slide"
	lean = lerpf(lean, -0.55 if dashing else -run * 0.18, minf(1, dt * 14))
	rig.hips.rotation.z = lean
	squash = lerpf(squash, 0.62 if nova.h < 1.6 else 1.0, minf(1, dt * 18))
	rig.body.scale = Vector3(1, squash, 1)
	rig.hips.position.y = 0.86 + (absf(cos(phase)) * 0.05 * run if nova.on_ground else 0.0)
	# scarf
	var neck: Vector3 = rig.head.global_position + Vector3(0, -0.22, 0)
	var back := Vector3(-cos(yaw), 0, sin(yaw))
	rig.pts[0] = neck
	for i in range(1, 8):
		var tgt: Vector3 = rig.pts[i - 1] + back * 0.12 + Vector3(0, -0.03 + sin(phase * 0.7 + i) * 0.02, 0)
		rig.pts[i] = rig.pts[i].lerp(tgt, minf(1, dt * 22))
	var im: ImmediateMesh = rig.scarf.mesh
	im.clear_surfaces()
	im.surface_begin(Mesh.PRIMITIVE_TRIANGLE_STRIP)
	for q in rig.pts:
		im.surface_add_vertex(q + Vector3(0, 0.07, 0))
		im.surface_add_vertex(q - Vector3(0, 0.07, 0))
	im.surface_end()
	# breakables shake down as they take damage
	for body in breakables:
		var B: Dictionary = breakables[body]
		var hurt: float = 1.0 - B.hp / float(data.destruct[B.box.tag].hp)
		B.mesh.scale.y = 1.0 - hurt * 0.08

# ---- Camera: frame Nova (world.js updateCamera), lead ahead of her run, ease in (render.js) ----
func _camera(dt: float, alpha: float) -> void:
	var p: Vector2 = nova.prev_pos.lerp(nova.cur_pos, alpha)
	var tan_h := tan(deg_to_rad(camera.fov) / 2)
	var vp := get_viewport().get_visible_rect().size
	var aspect := vp.x / maxf(1.0, vp.y)
	var dist := clampf(maxf((nova.h + 7) / 2 / tan_h, 9.0 / 2 / (tan_h * aspect)), 13, 32)
	var T := { "x": p.x + 0.8, "y": p.y + nova.h / 2 + 1.0, "dist": dist }
	var want := clampf(nova.vx * 0.22, -2.2, 2.2)
	lead += (want - lead) * (1 - exp(-dt * 1.8))
	T.x += lead
	var k := 1 - exp(-dt * 5.5)
	var ky := 1 - exp(-dt * (5.5 + maxf(0, absf(T.y - cam.y) - 1.2) * 5))
	cam.x += (T.x - cam.x) * k; cam.y += (T.y - cam.y) * ky; cam.dist += (T.dist - cam.dist) * k
	trauma = maxf(0, trauma - dt * 1.8)
	var f: Array = path.frame(cam.x)
	var s := trauma * trauma * 0.35
	var look: Vector3 = f[0] + Vector3(0, cam.y, 0)
	camera.position = look + f[2] * cam.dist + Vector3(randf_range(-s, s) / 2, cam.dist * 0.1 + randf_range(-s, s) / 2, 0)
	camera.look_at(look)

func snap_camera() -> void:
	cam.x = nova.cur_pos.x + 0.8; cam.y = nova.cur_pos.y + 1.86; lead = 0

func _process(dt: float) -> void:
	var alpha := Engine.get_physics_interpolation_fraction()
	_pose(dt, alpha)
	_camera(dt, alpha)
	hud.text = "Godot %s · %d fps · %d draws · x %.1f · %s" % [Engine.get_version_info().string, Engine.get_frames_per_second(),
		RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_TOTAL_DRAW_CALLS_IN_FRAME), nova.cur_pos.x, data.route.name]

# ---- Events: dust, sparks, debris (CPUParticles3D bursts) ----
func _on_event(type: String, d: Dictionary) -> void:
	var p: Vector2 = nova.cur_pos
	match type:
		"land":
			if d.vy < -14: _burst(p.x, p.y + 0.05, Color("e6c3a0"), 16, 3.0, 0.2, -4.0)
		"dash":
			_burst(p.x, p.y + 0.9, Color("9fe7ff"), 18, 4.0, 0.12, 0.0)
		"lift":
			_burst(d.x, d.y + 0.1, Color("ffb15a"), 24, 6.0, 0.14, -10.0)
		"strike":
			_strike(d.body, d.dmg)

func _strike(body: StaticBody2D, dmg: float) -> void:
	if not breakables.has(body): return
	var B: Dictionary = breakables[body]
	var b: Dictionary = B.box
	var col := Color(data.destruct[b.tag].color)
	var cx: float = (b.x0 + b.x1) / 2
	var cy: float = (b.y0 + b.y1) / 2
	if dmg < B.min:
		_burst(cx, cy, Color(1, 1, 1), 8, 3.0, 0.08)
		return
	B.hp -= dmg
	if B.hp > 0:
		_burst(cx, cy, col, 10, 4.0, 0.1)
		trauma = minf(1, trauma + 0.08)
		return
	_burst(cx, cy, col, 60, 9.0, 0.22)
	trauma = minf(1, trauma + 0.3)
	B.mesh.queue_free()
	body.queue_free()
	breakables.erase(body)

func _burst(x: float, y: float, color: Color, count: int, power: float, size: float, gravity := -18.0) -> void:
	var ps := CPUParticles3D.new()
	var q := QuadMesh.new(); q.size = Vector2(size, size)
	var m: StandardMaterial3D = spark_mat.duplicate()
	m.billboard_mode = BaseMaterial3D.BILLBOARD_ENABLED
	q.material = m
	ps.mesh = q
	ps.amount = count
	ps.one_shot = true
	ps.explosiveness = 1.0
	ps.lifetime = 0.6
	ps.direction = Vector3(0, 1, 0)
	ps.spread = 80
	ps.initial_velocity_min = power * 0.4
	ps.initial_velocity_max = power
	ps.gravity = Vector3(0, gravity, 0)
	ps.color = color
	ps.scale_amount_min = 0.5
	ps.position = path.point(x, y, 0.4)
	add_child(ps)
	ps.emitting = true
	get_tree().create_timer(1.0).timeout.connect(ps.queue_free)

# ---- Autotest (godot --headless -- --autotest): scripted input along the stretch, printing where Nova gets ----
var t_tick := 0
func _physics_process(_dt: float) -> void:
	if not autotest: return
	t_tick += 1
	var press := func(a: String, on: bool): (Input.action_press(a) if on else Input.action_release(a))
	press.call("right", t_tick > 10)
	# jump every 40 ticks (over the pit, the block, onto the walkways), swing at crates, dash now and then
	press.call("jump", t_tick % 40 < 14 and t_tick > 30)
	press.call("melee", t_tick % 20 == 0)
	press.call("dash", t_tick % 90 == 45)
	if t_tick % 60 == 0:
		print("tick %d x %.2f y %.2f state %s ground %s breakables %d" % [t_tick, nova.cur_pos.x, nova.cur_pos.y, nova.state, nova.on_ground, breakables.size()])
	if t_tick == 1500:
		get_tree().quit()
