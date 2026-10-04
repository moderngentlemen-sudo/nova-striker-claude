# The level's picture: every route's boxes as 3D geometry laid along the curved path, the gates, the breakable
# pieces, the lift pads, and each route's set pieces (the Storm Spire and Skyport's spires, the Helix Foundry's
# reactor core and furnace dome, the Undercity's cooling tower, towers and transit line). A port of render.js
# buildLevel and landmarks.js. Presentation only: it reads the World and never changes it.
#
# Static geometry is merged per material into one mesh per 48 m chunk of the path, so the renderer culls what
# is off screen and draws the rest in a few calls.
class_name LevelView
extends Node3D

const CHUNK := 48.0

var world: World
var mats := {}
var gates: Array = []          # [{ node, tag }]
var breakables := {}           # Level.Box -> { node, home, shake, mats }
var _batches := {}             # "mat|chunk" -> SurfaceTool
var _seed := 1234

func build(w: World) -> void:
	world = w
	_materials()
	_level()
	_breakables()
	_landmarks()
	_skyport()
	_commit()

# The prototype's dressing randomness (landmarks.js rnd), so the set pieces fall where they do there
func rnd() -> float:
	_seed = (_seed * 16807) % 2147483647
	return _seed / 2147483647.0

static func mat(color: String, rough := 0.7, metal := 0.0, emissive := "", energy := 1.0) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = Color(color); m.roughness = rough; m.metallic = metal
	if emissive != "":
		m.emission_enabled = true; m.emission = Color(emissive); m.emission_energy_multiplier = energy
	return m

func _materials() -> void:
	mats = {
		# render.js buildLevel
		"cap": mat("d9dfe7", 0.82), "body": mat("5f7897", 0.72), "dark": mat("46596f", 0.7),
		"trim": mat("7fe3ff", 0.5, 0.0, "4fd6ff", 2.0),
		# landmarks.js
		"steel": mat("6b6f7a", 0.55, 0.4), "rust": mat("8a5a3c", 0.8, 0.2), "core": mat("d8dde4", 0.35, 0.3),
		"molten": mat("ff8a2a", 0.5, 0.0, "ff6a10", 2.4), "hot": mat("ffd28a", 0.5, 0.0, "ffa040", 2.0),
		"concrete": mat("8d8fa3", 0.9), "windows": _windows(), "neon": mat("ff7ad9", 0.5, 0.0, "ff4fc8", 2.2),
		"cyan": mat("7fe3ff", 0.5, 0.0, "4fd6ff", 2.0), "train": mat("e9edf3", 0.35, 0.0, "4fd6ff", 0.15),
		"tower": mat("eef3f8", 0.4), "spire": mat("c3d7ea", 0.55), "glow": mat("7fe3ff", 0.5, 0.0, "5fd8ff", 1.4),
		"white": mat("f1f4f7", 0.5), "navy": mat("2b4f7e", 0.6), "sea": mat("eef6fb", 1.0), "shell": mat("a7a9bd", 0.85),
	}
	mats.shell.cull_mode = BaseMaterial3D.CULL_DISABLED
	var gate := mat("ff2e7e", 0.5, 0.0, "ff2e7e", 1.6)
	gate.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	gate.albedo_color.a = 0.45
	mats.gate = gate

# Lit windows (the Undercity's towers): a small texture of warm and cyan panes, as landmarks.js windowTex
func _windows() -> StandardMaterial3D:
	var img := Image.create(64, 128, false, Image.FORMAT_RGBA8)
	img.fill(Color("1b1f33"))
	var y := 4
	while y < 128:
		var x := 4
		while x < 64:
			var on := rnd() < 0.42
			var c := (Color("ffd9a0") if rnd() < 0.7 else Color("9fe7ff")) if on else Color("262b45")
			img.fill_rect(Rect2i(x, y, 7, 5), c)
			x += 12
		y += 10
	var tex := ImageTexture.create_from_image(img)
	var m := mat("ffffff", 0.8)
	m.albedo_texture = tex
	m.emission_enabled = true; m.emission_texture = tex; m.emission_energy_multiplier = 1.3; m.emission = Color.WHITE
	m.emission_operator = BaseMaterial3D.EMISSION_OP_MULTIPLY   # (the default adds the colour to the texture: all white)
	m.texture_filter = BaseMaterial3D.TEXTURE_FILTER_NEAREST_WITH_MIPMAPS
	return m

# ---- Batching: primitive meshes transformed into one surface per material and chunk ----
# face_uv: for a box, UVs per face from 0 to face_uv (as three.js's box UVs, scaled) instead of Godot's atlas layout
func _add(key: String, mesh: PrimitiveMesh, xf: Transform3D, chunk := 0, face_uv := Vector2.ZERO) -> void:
	var id := "%s|%d" % [key, chunk]
	var t: SurfaceTool = _batches.get(id)
	if t == null:
		t = SurfaceTool.new(); t.begin(Mesh.PRIMITIVE_TRIANGLES)
		_batches[id] = t
	var arr := mesh.get_mesh_arrays()
	var verts: PackedVector3Array = arr[Mesh.ARRAY_VERTEX]
	var norms: PackedVector3Array = arr[Mesh.ARRAY_NORMAL]
	var uvs: PackedVector2Array = arr[Mesh.ARRAY_TEX_UV]
	var idx: PackedInt32Array = arr[Mesh.ARRAY_INDEX]
	var nb := xf.basis.inverse().transposed()
	var size: Vector3 = mesh.size if mesh is BoxMesh else Vector3.ONE
	for i in idx:
		t.set_normal((nb * norms[i]).normalized())
		if face_uv != Vector2.ZERO:
			var v: Vector3 = verts[i] / size
			var n: Vector3 = norms[i]
			var u := (v.z if absf(n.x) > 0.5 else v.x) + 0.5
			var w := (v.z + 0.5) if absf(n.y) > 0.5 else 0.5 - v.y
			t.set_uv(Vector2(u * face_uv.x, w * face_uv.y))
		elif uvs.size():
			t.set_uv(uvs[i])
		t.add_vertex(xf * verts[i])

func _commit() -> void:
	for id in _batches:
		var key: String = id.split("|")[0]
		var mi := MeshInstance3D.new()
		mi.mesh = _batches[id].commit()
		mi.material_override = mats[key]
		if key in ["trim", "glow", "molten", "hot", "neon", "cyan", "sea"]:
			mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		add_child(mi)
	_batches.clear()

static func box(w: float, h: float, d: float) -> BoxMesh:
	var b := BoxMesh.new(); b.size = Vector3(w, h, d)
	return b

static func cyl(top: float, bottom: float, h: float, seg := 24) -> CylinderMesh:
	var c := CylinderMesh.new(); c.top_radius = top; c.bottom_radius = bottom; c.height = h; c.radial_segments = seg; c.rings = 1
	return c

static func torus(r: float, tube: float, rings := 48) -> TorusMesh:
	var t := TorusMesh.new(); t.inner_radius = r - tube; t.outer_radius = r + tube; t.rings = rings; t.ring_segments = 8
	return t

# A transform at path position (x, y, depth toward the camera), facing along the path
static func on_path(x: float, y: float, depth := 0.0) -> Transform3D:
	return Transform3D(Basis(Vector3.UP, PathFrame.yaw(x)), PathFrame.point(x, y, depth))

static func chunk_of(x: float) -> int:
	return int(floor(x / CHUNK))

# ---- The boxes (render.js buildLevel) ----
func _level() -> void:
	for b in world.level.boxes:
		if b.type == "d":
			continue   # (breakable pieces have their own nodes: _breakables)
		var depth := 2.6 if b.type == "o" else (1.8 if b.tag in ["panel", "column", "pillar"] else (3.2 if b.type == "g" else 4.4))
		var h := b.y1 - b.y0
		var curved := PathFrame.curved_span(b.x0, b.x1)
		var segs := []
		if not curved:
			segs.append([b.x0, b.x1])
		else:
			var n := int(ceil((b.x1 - b.x0) / 0.9))
			for i in n:
				segs.append([b.x0 + (b.x1 - b.x0) * i / n, b.x0 + (b.x1 - b.x0) * (i + 1) / n])
		if b.type == "g":
			var g := Node3D.new()
			add_child(g)
			for s in segs:
				var xm: float = (s[0] + s[1]) / 2
				var mi := MeshInstance3D.new()
				mi.mesh = box((s[1] - s[0]) * (1.04 if curved else 1.0), h, depth)
				mi.material_override = mats.gate; mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
				mi.transform = on_path(xm, b.y0 + h / 2)
				g.add_child(mi)
			gates.append({ "node": g, "tag": b.tag })
			continue
		for s in segs:
			var x0: float = s[0]
			var x1: float = s[1]
			var xm := (x0 + x1) / 2
			var w := (x1 - x0) * (1.04 if curved else 1.0)
			var ch := chunk_of(xm)
			if b.tag == "bound":
				_add("dark", box(w, h, depth), on_path(xm, b.y0 + h / 2), ch)
				continue
			var cap_h := minf(0.22, h * 0.4)
			_add("dark" if b.type == "o" else "body", box(w, h - cap_h, depth), on_path(xm, b.y0 + (h - cap_h) / 2), ch)
			_add("cap", box(w + (0.0 if curved else 0.08), cap_h, depth + 0.1), on_path(xm, b.y1 - cap_h / 2), ch)
			if b.tag != "tunnel" and b.tag != "panel":
				_add("trim", box(w, 0.06, 0.06), on_path(xm, b.y1 - cap_h - 0.05, depth / 2 + 0.02), ch)
			if b.type == "o":
				_add("trim", box(w * 0.9, 0.05, depth * 0.8), on_path(xm, b.y0 - 0.01), ch)

# ---- Breakable pieces (landmarks.js Breakables): their own nodes, hidden when broken ----
func _breakables() -> void:
	var stripes := _stripes()
	for b in world.level.boxes:
		if b.type != "d":
			continue
		var w := b.x1 - b.x0
		var h := b.y1 - b.y0
		var xm := (b.x0 + b.x1) / 2
		var g := Node3D.new()
		g.transform = on_path(xm, b.y0 + h / 2)
		add_child(g)
		var own := []
		var piece := func(mesh: Mesh, m: StandardMaterial3D, y := 0.0, tint := true) -> void:
			var mi := MeshInstance3D.new()
			mi.mesh = mesh; mi.position.y = y
			var mm: StandardMaterial3D = m.duplicate() if tint else m
			mi.material_override = mm
			if tint:
				own.append([mm, mm.albedo_color])
			g.add_child(mi)
		match b.tag:
			"crate":
				var m := mat("c98b4a", 0.85)
				piece.call(box(w, h, maxf(w, 1)), m)
				piece.call(box(w * 1.02, 0.08, maxf(w, 1) * 1.02), mat("8a5a2b", 0.85), h / 2 - 0.04)
				piece.call(box(w * 1.02, 0.08, maxf(w, 1) * 1.02), mat("8a5a2b", 0.85), -h / 2 + 0.04)
			"barricade":
				var m := mat("ffffff", 0.6, 0.3)
				m.albedo_texture = stripes; m.uv1_triplanar = true; m.uv1_scale = Vector3(1.6, 1.6, 1.6)
				piece.call(box(w, h, 2.6), m)
			"glass":
				var gm := mat("bfe8ff", 0.05, 0.1)
				gm.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA; gm.albedo_color.a = 0.38
				piece.call(box(w * 0.6, h, 3.4), gm, 0.0, false)
				for y in [-h / 2, h / 2]:
					piece.call(box(w, 0.12, 3.5), mat("5f7897", 0.6), y)
			_:
				piece.call(cyl(w * 0.5, w * 0.58, h, 16), mat("b9c2cc", 0.8))
				piece.call(box(w * 1.25, 0.18, w * 1.25), mats.cyan, h / 2 - 0.09, false)
		breakables[b] = { "node": g, "home": g.position, "shake": 0.0, "mats": own, "hp": b.hp }

func _stripes() -> ImageTexture:
	var img := Image.create(64, 64, false, Image.FORMAT_RGBA8)
	img.fill(Color("2c3442"))
	for y in 64:
		for x in 64:
			if posmod(x + y, 22) < 11:
				img.set_pixel(x, y, Color("ffc23a"))
	return ImageTexture.create_from_image(img)

# ---- Set pieces (landmarks.js) ----
func _landmarks() -> void:
	var L := Tune.L
	for rid in ["foundry", "undercity"]:
		for g in L.SEGS_OF[rid]:
			var foundry: bool = rid == "foundry"
			var chunk := chunk_of((g.x0 + g.x1) / 2)
			if g.kind == "arc" and g.s > 0:
				# Whatever the path wraps round: a structure at the arc's centre
				var R: float = g.r - 4.2
				var C := Vector3(g.C.x, 0, g.C.z)
				if foundry and g.r < 18:
					# The reactor core inside the helix: a tall drum with glowing rings at each turn of the climb
					_add("core", cyl(R, R + 0.8, 70, 48), Transform3D(Basis(), C + Vector3(0, 18, 0)), chunk)
					var y := -2.0
					while y < 50:
						_add("molten", torus(R + 0.35, 0.28), Transform3D(Basis(), C + Vector3(0, y, 0)), chunk)
						y += 5.5
					for i in 10:
						var a := i / 10.0 * TAU
						_add("hot", box(0.5, 64, 0.5), Transform3D(Basis(), C + Vector3(sin(a) * (R + 0.2), 18, cos(a) * (R + 0.2))), chunk)
					_add("steel", cyl(R * 0.55, R, 6, 32), Transform3D(Basis(), C + Vector3(0, 56, 0)), chunk)
				elif foundry:
					# The furnace dome the path bends round, venting heat
					var sm := SphereMesh.new(); sm.radius = R; sm.height = R; sm.is_hemisphere = true; sm.radial_segments = 32; sm.rings = 12
					_add("rust", sm, Transform3D(Basis(), C + Vector3(0, -2, 0)), chunk)
					_add("steel", cyl(R, R, 4, 32), Transform3D(Basis(), C + Vector3(0, -4, 0)), chunk)
					for i in 6:
						var a := i / 6.0 * TAU
						var o := Vector3(sin(a) * R * 0.5, 0, cos(a) * R * 0.5)
						_add("steel", cyl(0.9, 1.2, 26, 12), Transform3D(Basis(), C + o + Vector3(0, 11, 0)), chunk)
						_add("molten", cyl(1.0, 1.0, 0.6, 12), Transform3D(Basis(), C + o + Vector3(0, 24.2, 0)), chunk)
				else:
					# The cooling tower the stair winds round: a hyperboloid shell (a stack of cones)
					for i in 12:
						var t0 := i / 12.0
						var t1 := (i + 1) / 12.0
						var r0 := R * (1 - 0.32 * sin(t0 * PI * 0.95))
						var r1 := R * (1 - 0.32 * sin(t1 * PI * 0.95))
						_add("shell", cyl(r1, r0, 56.0 / 12, 48), Transform3D(Basis(), C + Vector3(0, -20 + (t0 + t1) / 2 * 56, 0)), chunk)
					for y in [8, 24]:
						_add("neon", torus(R * (0.74 if y < 20 else 0.7) + 0.2, 0.18), Transform3D(Basis(), C + Vector3(0, y, 0)), chunk)
			# Along every piece: what stands behind the path (and, on a bend toward the camera, a wall that curves with it)
			var x: float = g.x0
			while x < g.x1:
				var back: bool = g.kind == "arc" and g.s < 0
				var ch := chunk_of(x)
				if foundry:
					if back:
						_add("steel", box(3.4, 22, 1.4), on_path(x, 2, -7.5), ch)
						if rnd() < 0.5:
							var q := on_path(x, 6 + rnd() * 8, -6.6)
							_add("molten", cyl(0.35, 0.35, 3.4, 8), Transform3D(q.basis * Basis(Vector3.RIGHT, PI / 2), q.origin), ch)
					elif rnd() < 0.55:
						var d := -(10 + rnd() * 14)
						var hh := 8 + rnd() * 22
						_add("steel" if rnd() < 0.5 else "rust", box(2.6, hh, 2.6), on_path(x, -10 + hh / 2, d), ch)
						if rnd() < 0.5:
							_add("molten", cyl(0.8, 0.8, 0.8, 10), on_path(x, -10 + hh + 0.4, d), ch)
					# the molten channel far below the walkways
					_add("molten", box(3.4, 0.4, 5), on_path(x, -9, -1), ch)
					x += 3.2
				else:
					if back:
						var hh := 26 + rnd() * 18
						_add("windows", box(4.6, hh, 4), on_path(x, -6 + hh / 2, -9), ch, Vector2.ONE)
					else:
						for d in [-(12 + rnd() * 10), -(30 + rnd() * 20)]:
							var hh := 20 + rnd() * 50
							var ww := 5 + rnd() * 5
							_add("windows", box(ww, hh, ww), on_path(x, -20 + hh / 2, d), ch, Vector2(ww / 6, hh / 12))
							if rnd() < 0.25:
								_add("neon", box(ww * 0.9, 0.3, 0.3), on_path(x, -20 + hh + 0.3, d), ch)
					x += 5
	# Lift pads on the foundry floor: a glowing disc with a ring
	for lp in L.LIFTS:
		var ch := chunk_of(lp[0])
		_add("hot", cyl(0.9, 1.0, 0.12, 24), on_path(lp[0], lp[1] + 0.06), ch)
		_add("cyan", torus(0.7, 0.06, 24), on_path(lp[0], lp[1] + 0.14), ch)
	# The Undercity's transit line: a rail overhead along it, and a train standing on the far track
	for g in L.SEGS_OF.undercity:
		if g.kind != "line" or g.x1 - g.x0 <= 70:
			continue
		var x: float = g.x0
		while x < g.x1:
			var ch := chunk_of(x)
			_add("concrete", box(2.6, 0.3, 0.5), on_path(x, 9, -1.5), ch)
			_add("cyan", box(2.6, 0.08, 0.12), on_path(x, 8.95, -1.5), ch)
			if U.jround(x - g.x0) % 10 == 0:
				_add("concrete", box(0.4, 14, 0.4), on_path(x, 2, -6.5), ch)
			x += 2.5
		var t0: float = g.x0 + 20
		for i in 3:
			var q := on_path(t0 + i * 9, 1.6, -5)
			var cap := CapsuleMesh.new(); cap.radius = 1.4; cap.height = 6.4 + 2.8
			_add("train", cap, Transform3D(q.basis * Basis(Vector3.FORWARD, PI / 2), q.origin), chunk_of(t0))

# ---- The Skyport route's surroundings (render.js buildSky / buildProps): the Storm Spire, distant spires, the
# cloud sea, and the relay beacon at the route's end ----
func _skyport() -> void:
	var L := Tune.L
	var TC := Vector3(L.TOWER_CENTER.x, 0, L.TOWER_CENTER.z)
	var R: float = L.ARC_R
	var ch := chunk_of(120)
	_add("tower", cyl(R - 3.4, R - 2.4, 110, 48), Transform3D(Basis(), TC + Vector3(0, 20, 0)), ch)
	for i in 12:
		var a := i / 12.0 * TAU
		_add("glow", box(0.3, 90, 0.3), Transform3D(Basis(), TC + Vector3(sin(a) * (R - 3.3), 25, cos(a) * (R - 3.3))), ch)
	for y in [-4, 18, 40, 62]:
		_add("glow", torus(R - 2.8, 0.35), Transform3D(Basis(), TC + Vector3(0, y, 0)), ch)
	var spots := [[-30, -120], [20, -150], [55, -105], [85, -175], [130, -135], [-70, -170], [175, -110], [215, -160], [250, -95], [10, -210], [110, -220], [290, -150]]
	for s in spots:
		var hh := 45 + rnd() * 65
		var w := 4 + rnd() * 6
		var c := chunk_of(s[0])
		_add("spire", box(w, hh, w), Transform3D(Basis(), Vector3(s[0], hh / 2 - 34, s[1])), c)
		_add("glow", box(w * 1.02, 0.5, w * 1.02), Transform3D(Basis(), Vector3(s[0], hh - 38, s[1])), c)
		_add("spire", cyl(w * 1.3, w * 1.3, 1.2, 24), Transform3D(Basis(), Vector3(s[0], hh * 0.6 - 34, s[1])), c)
	var sea := PlaneMesh.new(); sea.size = Vector2(1200, 1200)
	_add("sea", sea, Transform3D(Basis(), Vector3(60, -34, -40)), -100)
	# The relay beacon at the end of the Skyline Relay, and antenna masts behind the rooftops
	for m in [[195, 15.6, 6], [212, 15.6, 8], [226, 12.6, 5], [252, 18.6, 6], [262, 18.6, 7], [294, 18.6, 7]]:
		var p := PathFrame.point(m[0], m[1], -2.9)
		_add("navy", cyl(0.1, 0.15, m[2], 10), Transform3D(Basis(), Vector3(p.x, m[1] + m[2] / 2.0, p.z)), chunk_of(m[0]))
		var lamp := SphereMesh.new(); lamp.radius = 0.24; lamp.height = 0.48; lamp.radial_segments = 12; lamp.rings = 6
		_add("glow", lamp, Transform3D(Basis(), Vector3(p.x, m[1] + m[2] + 0.15, p.z)), chunk_of(m[0]))
	var bc := PathFrame.point(310, 18.6, -1.2)
	_add("white", cyl(0.45, 0.85, 14, 16), Transform3D(Basis(), Vector3(bc.x, 18.6 + 7, bc.z)), chunk_of(310))
	for k in [0.3, 0.55, 0.8]:
		_add("glow", torus(1.45 - k * 0.6, 0.12, 32), Transform3D(Basis(), Vector3(bc.x, 18.6 + 14 * k, bc.z)), chunk_of(310))

# ---- Each frame: gates open and close, breakable pieces shake, darken and break ----
func update(dt: float) -> void:
	for g in gates:
		g.node.visible = world.level.gates[g.tag]
	for b in breakables:
		var B: Dictionary = breakables[b]
		B.node.visible = not b.broken
		if b.broken:
			continue
		if b.hp < B.hp:
			B.shake = 1.0   # it took damage since the last frame
		B.hp = b.hp
		B.shake = maxf(0, B.shake - dt * 6)
		var off := Vector3.ZERO
		if B.shake > 0:
			off = Vector3(randf_range(-0.06, 0.06), 0, randf_range(-0.06, 0.06)) * B.shake
		B.node.position = B.home + off
		var k: float = b.hp / Tune.L.DESTRUCT[b.tag].hp   # darker as it takes damage
		for pair in B.mats:
			var base: Color = pair[1]
			pair[0].albedo_color = Color(base.r * (0.55 + 0.45 * k), base.g * (0.55 + 0.45 * k), base.b * (0.55 + 0.45 * k), base.a)
