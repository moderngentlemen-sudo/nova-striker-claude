# Building blocks for the procedural placeholder rigs (characters and enemies): groups, parts and materials.
# The prototype draws its characters this way (rigs.js, enemyRigs.js); real art replaces these later.
class_name Rig
extends RefCounted

static func grp(parent: Node3D, x := 0.0, y := 0.0, z := 0.0) -> Node3D:
	var g := Node3D.new()
	g.position = Vector3(x, y, z)
	parent.add_child(g)
	return g

static func part(parent: Node3D, mesh: Mesh, m: Material, x := 0.0, y := 0.0, z := 0.0) -> MeshInstance3D:
	var mi := MeshInstance3D.new()
	mi.mesh = mesh; mi.material_override = m
	mi.position = Vector3(x, y, z)
	parent.add_child(mi)
	return mi

# A box with softened edges is approximated by a plain box (the prototype rounds them); good enough for blocking
static func rbox(w: float, h: float, d: float) -> BoxMesh:
	var b := BoxMesh.new(); b.size = Vector3(w, h, d)
	return b

static func cap(r: float, length: float) -> CapsuleMesh:
	var c := CapsuleMesh.new(); c.radius = r; c.height = length + r * 2; c.radial_segments = 12; c.rings = 4
	return c

static func sphere(r: float, seg := 16) -> SphereMesh:
	var s := SphereMesh.new(); s.radius = r; s.height = r * 2; s.radial_segments = seg; s.rings = seg / 2
	return s

static func cyl(top: float, bottom: float, h: float, seg := 16) -> CylinderMesh:
	var c := CylinderMesh.new(); c.top_radius = top; c.bottom_radius = bottom; c.height = h; c.radial_segments = seg; c.rings = 1
	return c

static func cone(r: float, h: float, seg := 8) -> CylinderMesh:
	return cyl(0.0, r, h, seg)

static func torus(r: float, tube: float, seg := 24) -> TorusMesh:
	var t := TorusMesh.new(); t.inner_radius = r - tube; t.outer_radius = r + tube; t.rings = seg; t.ring_segments = 6
	return t

static func std(color, rough := 0.5, metal := 0.08) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = Color(color); m.roughness = rough; m.metallic = metal
	# A soft rim light so characters separate from bright backgrounds (the prototype's Fresnel rim)
	m.rim_enabled = true; m.rim = 0.35; m.rim_tint = 0.6
	return m

static func glow(color, energy := 2.2) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = Color(color); m.roughness = 0.3
	m.emission_enabled = true; m.emission = Color(color); m.emission_energy_multiplier = energy
	return m

static func clear(color, alpha: float, energy := 1.6) -> StandardMaterial3D:
	var m := glow(color, energy)
	m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA; m.albedo_color.a = alpha
	m.cull_mode = BaseMaterial3D.CULL_DISABLED
	m.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	return m
