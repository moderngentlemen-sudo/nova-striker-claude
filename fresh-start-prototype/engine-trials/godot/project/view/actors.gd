# Everything that moves: a PlayerView per player and an EnemyView per enemy (made and freed as they come and
# go), and the transient things the world holds: projectiles (one MultiMesh, coloured per kind), beams and
# Supernova, the Aegis dome, gravity wells, pickups, shockwaves, mortar landing markers, boss lasers and chain
# lightning. Presentation only: it reads the World each frame and listens to its events.
class_name Actors
extends Node3D

const PLAYER_SHOT := Color("ffb547")
const ENEMY_SHOT := Color("ff2e7e")
const POWER_TINT := { "overclock": "6fe3ff", "plating": "a9c8ff", "medkit": "5cf2a6", "ultcell": "ffd23f", "fury": "ff5a4a" }

var world: World
var players := {}      # PlayerSim -> PlayerView
var enemies := {}      # EnemySim -> EnemyView
var proj_mm: MultiMeshInstance3D
var beams: Array = []  # pooled MeshInstance3D (stretched cylinders)
var beam_mat: StandardMaterial3D
var ult_mat: StandardMaterial3D
var domes := {}        # PlayerSim -> MeshInstance3D (Aegis)
var well_nodes: Array = []
var pickup_nodes := {} # pickup id -> Node3D
var wave_nodes: Array = []
var markers: Array = []  # { node, t } mortar landing rings
var lasers: Array = []
var chains: Array = []   # { node, t }

func setup(w: World) -> void:
	world = w
	var mm := MultiMesh.new()
	mm.transform_format = MultiMesh.TRANSFORM_3D
	mm.use_colors = true
	mm.instance_count = 400
	mm.visible_instance_count = 0
	mm.mesh = Rig.sphere(1.0, 10)
	proj_mm = MultiMeshInstance3D.new()
	proj_mm.multimesh = mm
	var pm := StandardMaterial3D.new()
	pm.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	pm.vertex_color_use_as_albedo = true
	pm.albedo_color = Color(2.2, 2.2, 2.2)   # (overbright: glows under the tone mapper's bloom)
	proj_mm.material_override = pm
	proj_mm.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	proj_mm.extra_cull_margin = 16384   # its instances are everywhere: never cull the whole set
	add_child(proj_mm)
	beam_mat = Rig.clear("ffd889", 0.85, 4.0)
	ult_mat = Rig.clear("fff3c4", 0.9, 6.0)

func clear_all() -> void:
	for v in players.values(): v.queue_free()
	for v in enemies.values(): v.queue_free()
	players.clear(); enemies.clear()

# ---- Events: telegraph glints, mortar markers, chain lightning ----
func on_event(ev: Dictionary) -> void:
	match ev.type:
		"telegraph":
			var v = enemies.get(ev.e)
			if v != null:
				v.telegraph(ev.cat, ev.ticks)
		"mortarShot":
			var ring := MeshInstance3D.new()
			ring.mesh = Rig.torus(ev.r, 0.06, 32)
			ring.material_override = Rig.clear("ff2e7e", 0.8, 2.0)
			ring.position = PathFrame.point(ev.x, ev.y + 0.05)
			ring.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
			add_child(ring)
			markers.append({ "node": ring, "t": ev.ticks / 60.0, "len": ev.ticks / 60.0 })
		"chain":
			var im := ImmediateMesh.new()
			var mi := MeshInstance3D.new()
			mi.mesh = im; mi.material_override = Rig.clear("ffe066", 1.0, 5.0)
			mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
			im.surface_begin(Mesh.PRIMITIVE_LINES)
			var pts: Array = ev.pts
			for i in range(1, pts.size()):
				var a := PathFrame.point(pts[i - 1].x, pts[i - 1].y, 0.3)
				var b := PathFrame.point(pts[i].x, pts[i].y, 0.3)
				# a jagged bolt: a few kinked segments between each pair
				var prev := a
				for k in range(1, 5):
					var q := a.lerp(b, k / 4.0) + (Vector3(randf_range(-0.2, 0.2), randf_range(-0.2, 0.2), 0) if k < 4 else Vector3.ZERO)
					im.surface_add_vertex(prev); im.surface_add_vertex(q)
					prev = q
			im.surface_end()
			add_child(mi)
			chains.append({ "node": mi, "t": 0.18 })

# ---- Each frame ----
func sync(alpha: float, dt: float, t: float) -> void:
	_sync_players(alpha, dt, t)
	_sync_enemies(alpha, dt, t)
	_projectiles(alpha)
	_beams(t)
	_aegis(t)
	_wells(alpha, t)
	_pickups(alpha, t)
	_shockwaves()
	_lasers(t)
	for m in markers:
		m.t -= dt
		m.node.scale = Vector3.ONE * (0.6 + 0.4 * (1 - m.t / m.len))
		m.node.visible = m.t > 0
	for m in markers.filter(func(q): return q.t <= 0):
		m.node.queue_free()
	markers = markers.filter(func(q): return q.t > 0)
	for c in chains:
		c.t -= dt
	for c in chains.filter(func(q): return q.t <= 0):
		c.node.queue_free()
	chains = chains.filter(func(q): return q.t > 0)

func _sync_players(alpha: float, dt: float, t: float) -> void:
	var seen := {}
	for p in world.players:
		seen[p] = true
		var v: PlayerView = players.get(p)
		if v == null or v.p.char != p.char:
			if v != null:
				v.queue_free()
			v = PlayerView.new()
			add_child(v)
			v.setup(p)
			players[p] = v
		v.animate(alpha, dt, t)
	for p in players.keys():
		if not seen.has(p):
			players[p].queue_free(); players.erase(p)

func _sync_enemies(alpha: float, dt: float, t: float) -> void:
	var seen := {}
	for e in world.enemies:
		seen[e] = true
		var v: EnemyView = enemies.get(e)
		if v == null:
			v = EnemyView.new()
			add_child(v)
			v.setup(e)
			enemies[e] = v
		v.animate(alpha, dt, t)
	for e in enemies.keys():
		if not seen.has(e):
			enemies[e].queue_free(); enemies.erase(e)

func _projectiles(alpha: float) -> void:
	var mm := proj_mm.multimesh
	var n := mini(world.projectiles.size(), mm.instance_count)
	for i in n:
		var pr: Dictionary = world.projectiles[i]
		var x := lerpf(pr.px, pr.x, alpha)
		var y := lerpf(pr.py, pr.y, alpha)
		var r: float = maxf(0.08, pr.r)
		var c := PLAYER_SHOT if pr.team == "p" else ENEMY_SHOT
		var k: String = pr.get("kind", "")
		if k == "heavy" or k == "mortar":
			c = Color("ff5a9a")
		elif k == "disc":
			c = Color("ffd36b")
		elif k in ["grenade", "bomblet"]:
			c = Color("ff9a3d")
		elif pr.team == "p" and pr.get("family") != null:
			c = Color(Tune.C.ATTACH_LOOK.get(pr.family.attach, { "tint": "ffb547" }).tint)
		# Stretched along its flight for fast shots, so they read as streaks
		var v := Vector2(pr.vx, pr.vy)
		var sp := v.length()
		var stretch := clampf(sp / 20.0, 1.0, 3.0) if k != "disc" and k != "grenade" else 1.0
		var basis := Basis.from_scale(Vector3(r * stretch, r, r))
		if sp > 0.1 and stretch > 1:
			basis = Basis(Vector3.FORWARD, -atan2(pr.vy, pr.vx)) * basis
		basis = Basis(Vector3.UP, PathFrame.yaw(x)) * basis
		mm.set_instance_transform(i, Transform3D(basis, PathFrame.point(x, y)))
		mm.set_instance_color(i, c)
	mm.visible_instance_count = n

# A cylinder stretched from a to b (path coordinates), for beams and lasers
func _span(mi: MeshInstance3D, ax: float, ay: float, bx: float, by: float, width: float) -> void:
	var a := PathFrame.point(ax, ay)
	var b := PathFrame.point(bx, by)
	var d := b - a
	var len := d.length()
	if len < 1e-3:
		mi.visible = false
		return
	var y := d / len
	var x := y.cross(Vector3.UP).normalized() if absf(y.y) < 0.99 else Vector3.RIGHT
	var z := x.cross(y)
	mi.transform = Transform3D(Basis(x * width, y * len, z * width), (a + b) / 2)
	mi.visible = true

func _beam_node(i: int) -> MeshInstance3D:
	while beams.size() <= i:
		var mi := MeshInstance3D.new()
		mi.mesh = Rig.cyl(1.0, 1.0, 1.0, 12)
		mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		add_child(mi)
		beams.append(mi)
	return beams[i]

func _beams(t: float) -> void:
	var i := 0
	for p in world.players:
		if p.beam != null and p.beam.segs.size():
			for g in p.beam.segs:
				var mi := _beam_node(i)
				mi.material_override = beam_mat
				_span(mi, g.x0, g.y0, g.x1, g.y1, Tune.C.MARKSMAN.beam.width * (0.9 + 0.2 * sin(t * 40)))
				i += 1
		if p.ult_run != null and p.ult_run.get("segs") != null:
			for g in p.ult_run.segs:
				var mi := _beam_node(i)
				mi.material_override = ult_mat
				_span(mi, g.x0, g.y0, g.x1, g.y1, Tune.C.ULT.nova.width * (0.9 + 0.15 * sin(t * 50)))
				i += 1
	for j in range(i, beams.size()):
		beams[j].visible = false

func _aegis(t: float) -> void:
	for p in world.players:
		var dome: MeshInstance3D = domes.get(p)
		if p.aegis != null and p.state != "dead":
			if dome == null:
				dome = MeshInstance3D.new()
				dome.mesh = Rig.sphere(Tune.C.AEGIS.radius, 24)
				dome.material_override = Rig.clear("ffd889", 0.22, 1.8)
				dome.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
				add_child(dome)
				domes[p] = dome
			var v: PlayerView = players.get(p)
			var base := v.global_position if v else PathFrame.point(p.x, p.y)
			dome.position = base + Vector3(0, p.h * 0.62, 0)
			var frac: float = p.aegis.hp / p.aegis.max
			dome.material_override.albedo_color.a = 0.12 + 0.18 * frac + 0.04 * sin(t * 8)
			dome.visible = true
		elif dome != null:
			dome.visible = false

func _wells(alpha: float, t: float) -> void:
	while well_nodes.size() < world.wells.size():
		var mi := MeshInstance3D.new()
		mi.mesh = Rig.sphere(1.0, 20)
		mi.material_override = Rig.clear("ffb547", 0.25, 2.0)
		mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		add_child(mi)
		well_nodes.append(mi)
	for i in well_nodes.size():
		var mi: MeshInstance3D = well_nodes[i]
		if i >= world.wells.size():
			mi.visible = false
			continue
		var w: Dictionary = world.wells[i]
		mi.visible = true
		mi.position = PathFrame.point(lerpf(w.px, w.x, alpha), lerpf(w.py, w.y, alpha))
		var r: float = 0.3 if w.phase == "orb" else w.r * (0.95 + 0.05 * sin(t * 12))
		mi.scale = Vector3.ONE * r
		mi.material_override.albedo_color = Color(0.1, 0.05, 0.15, 0.55) if w.phase == "open" else Color("ffb547", 0.8)

func _pickups(alpha: float, t: float) -> void:
	var seen := {}
	for k in world.pickups:
		seen[k.id] = true
		var n: Node3D = pickup_nodes.get(k.id)
		if n == null:
			n = Node3D.new()
			var tint: String = POWER_TINT.get(k.kind, "ffffff")
			Rig.part(n, Rig.rbox(0.36, 0.36, 0.36), Rig.glow(tint, 2.0))
			var col := Rig.part(n, Rig.cyl(0.35, 0.35, 3.0, 12), Rig.clear(tint, 0.12, 1.0), 0, 1.4)
			col.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
			add_child(n)
			pickup_nodes[k.id] = n
		n.position = PathFrame.point(lerpf(k.px, k.x, alpha), lerpf(k.py, k.y, alpha) + 0.1 * sin(t * 3 + k.id))
		n.get_child(0).rotation = Vector3(t * 0.7, t * 1.3, 0)
		n.get_child(1).visible = k.rest
	for id in pickup_nodes.keys():
		if not seen.has(id):
			pickup_nodes[id].queue_free(); pickup_nodes.erase(id)

func _shockwaves() -> void:
	while wave_nodes.size() < world.shockwaves.size():
		var mi := MeshInstance3D.new()
		mi.mesh = Rig.rbox(0.9, 1.0, 1.6)
		mi.material_override = Rig.clear("ff2e7e", 0.5, 2.5)
		mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		add_child(mi)
		wave_nodes.append(mi)
	for i in wave_nodes.size():
		var mi: MeshInstance3D = wave_nodes[i]
		mi.visible = i < world.shockwaves.size()
		if mi.visible:
			var s: Dictionary = world.shockwaves[i]
			mi.transform = Transform3D(Basis(Vector3.UP, PathFrame.yaw(s.x)).scaled(Vector3(1, s.h, 1)), PathFrame.point(s.x, s.y + s.h / 2))

# Boss lasers (the Lockwarden's and the Stormcaller's), drawn while they fire
func _lasers(t: float) -> void:
	var n := 0
	for e in world.enemies:
		if e.dead or e.atk == null or not e.atk.has("span"):
			continue
		if not (e.state == "laser" or e.state == "windup"):
			continue
		while lasers.size() <= n:
			var mi := MeshInstance3D.new()
			mi.mesh = Rig.rbox(1, 1, 1)
			mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
			mi.material_override = Rig.clear("ff2e7e", 0.6, 4.0)
			add_child(mi)
			lasers.append(mi)
		var L: Dictionary = e.atk.span
		var mi: MeshInstance3D = lasers[n]
		var firing: bool = e.state == "laser"
		var h: float = (L.y1 - L.y0) if firing else 0.06
		var xm: float = (L.x0 + L.x1) / 2
		mi.transform = Transform3D(Basis(Vector3.UP, PathFrame.yaw(xm)).scaled(Vector3(absf(L.x1 - L.x0), h, 0.4 if firing else 0.1)), PathFrame.point(xm, (L.y0 + L.y1) / 2))
		mi.material_override.albedo_color.a = 0.7 + 0.2 * sin(t * 40) if firing else 0.35
		mi.visible = true
		n += 1
	for i in range(n, lasers.size()):
		lasers[i].visible = false
