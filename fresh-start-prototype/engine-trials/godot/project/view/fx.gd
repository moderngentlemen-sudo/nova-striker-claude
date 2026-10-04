# Hit sparks, dust, debris, blasts and flashes, made from the world's events. CPU particles (they run on every
# renderer, the web's included), each a one-shot burst returned to a pool when done, plus short-lived expanding
# spheres for blasts. (The camera's shake is CameraRig's.) A first pass at the prototype's fx.js; the
# impact frames, trails and charge effects come with the effects milestone.
class_name Fx
extends Node3D


var pool: Array = []
var flashes: Array = []   # { node, t, len, r }
var _quad: QuadMesh

func _ready() -> void:
	_quad = QuadMesh.new()
	_quad.size = Vector2(1, 1)
	var m := StandardMaterial3D.new()
	m.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	m.billboard_mode = BaseMaterial3D.BILLBOARD_PARTICLES
	m.vertex_color_use_as_albedo = true
	m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	_quad.material = m

func _particles() -> CPUParticles3D:
	for ps in pool:
		if not ps.emitting:
			return ps
	var ps := CPUParticles3D.new()
	ps.mesh = _quad
	ps.one_shot = true
	ps.explosiveness = 1.0
	ps.lifetime = 0.5
	ps.local_coords = false
	ps.direction = Vector3(0, 1, 0)
	ps.spread = 180
	ps.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	var curve := Curve.new()
	curve.add_point(Vector2(0, 1)); curve.add_point(Vector2(1, 0))
	ps.scale_amount_curve = curve
	add_child(ps)
	pool.append(ps)
	return ps

# A burst of `count` sparks at path position (x, y): speed, size, life, gravity, and an optional aim
func burst(x: float, y: float, color: Color, count: int, speed: float, size: float, life := 0.45, grav := -14.0, spread := 180.0, dir := Vector3.UP) -> void:
	var ps := _particles()
	ps.position = PathFrame.point(x, y, 0.3)
	ps.amount = maxi(1, count)
	ps.lifetime = life
	ps.initial_velocity_min = speed * 0.4
	ps.initial_velocity_max = speed
	ps.scale_amount_min = size * 0.6
	ps.scale_amount_max = size
	ps.gravity = Vector3(0, grav, 0)
	ps.spread = spread
	ps.direction = dir
	ps.color = Color(color.r * 2.0, color.g * 2.0, color.b * 2.0, 1.0)   # (overbright for the glow)
	ps.restart()
	ps.emitting = true

# An expanding, fading sphere (blasts, splash, the nova)
func flash(x: float, y: float, r: float, color: Color, life := 0.25) -> void:
	var mi := MeshInstance3D.new()
	mi.mesh = Rig.sphere(1.0, 16)
	mi.material_override = Rig.clear(color.to_html(false), 0.6, 3.0)
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	mi.position = PathFrame.point(x, y)
	add_child(mi)
	flashes.append({ "node": mi, "t": life, "len": life, "r": r })

func _process(dt: float) -> void:
	for f in flashes:
		f.t -= dt
		var k: float = 1 - f.t / f.len
		f.node.scale = Vector3.ONE * maxf(0.01, f.r * (0.4 + 0.6 * k))
		f.node.material_override.albedo_color.a = 0.6 * (1 - k)
	for f in flashes.filter(func(q): return q.t <= 0):
		f.node.queue_free()
	flashes = flashes.filter(func(q): return q.t > 0)

func on_event(ev: Dictionary) -> void:
	match ev.type:
		"hit":
			var c := Color("ffffff") if ev.get("heavy") else Color("ffe9a8")
			burst(ev.x, ev.y, c, 14 if ev.get("heavy") else 8, 7.0, 0.14)
		"kill":
			burst(ev.x, ev.y, Color("ff2e7e"), 26, 9.0, 0.2, 0.6)
			flash(ev.x, ev.y, 1.2, Color("ff7fb2"), 0.2)
		"blocked", "armorHit":
			burst(ev.x, ev.y, Color("bfe8ff"), 6, 5.0, 0.1)
		"armorBreak", "guardBreak":
			burst(ev.x, ev.y, Color("e6e9f0"), 18, 8.0, 0.18, 0.6)
		"stagger":
			burst(ev.x, ev.y, Color("ffd23f"), 12, 5.0, 0.15)
		"splash":
			flash(ev.x, ev.y, ev.r, Color("ffb547"), 0.18)
		"blast", "frag", "riseBlast", "wellCollapse":
			flash(ev.x, ev.y, ev.r, Color("ffcf7a"), 0.28)
			burst(ev.x, ev.y, Color("ffb547"), 22, 10.0, 0.2, 0.5)
		"enemyBlast":
			flash(ev.x, ev.y, ev.r, Color("ff2e7e"), 0.3)
			burst(ev.x, ev.y, Color("ff5a9a"), 20, 9.0, 0.2, 0.5)
		"rocketJump":
			flash(ev.x, ev.y, 1.4, Color("fff1c9"), 0.22)
			burst(ev.x, ev.y, Color("ffd889"), 24, 8.0, 0.2, 0.5, -6.0)
		"land":
			if ev.vy < -14:
				burst(ev.p.x, ev.p.y + 0.05, Color("d9dfe7"), 12, 3.0, 0.16, 0.4, -4.0, 70.0)
		"dash", "dodge":
			burst(ev.p.x, ev.p.y + 0.9, Color("fff1c9"), 10, 4.0, 0.1, 0.3, 0.0)
		"jump", "djump", "walljump":
			burst(ev.p.x, ev.p.y + 0.1, Color("d9dfe7"), 6, 2.5, 0.1, 0.3, -2.0, 60.0)
		"liftBounce":
			burst(ev.x, ev.y + 0.2, Color("ffd28a"), 18, 6.0, 0.18, 0.45, -6.0, 40.0)
		"poundLand":
			flash(ev.x, ev.y + 0.3, ev.r, Color("ffcf7a"), 0.3)
			burst(ev.x, ev.y + 0.1, Color("ffd889"), 30, 9.0, 0.2, 0.5, -10.0, 70.0)
		"perfectRelease":
			flash(ev.x, ev.y, 0.8, Color("ffffff"), 0.15)
		"perfectDodge":
			flash(ev.x, ev.y, 2.4, Color("bfe8ff"), 0.35)
		"aegisHit":
			burst(ev.x, ev.y, Color("ffd889"), 10, 5.0, 0.14)
		"aegisOff":
			if ev.why == "break" or ev.why == "detonate":
				flash(ev.x, ev.y, 3.0, Color("ffd889"), 0.3)
				burst(ev.x, ev.y, Color("fff1c9"), 30, 10.0, 0.2, 0.5)
		"intercept":
			burst(ev.x, ev.y, Color("ffffff"), 8, 6.0, 0.12)
		"erase":
			burst(ev.x, ev.y, Color("fff1c9"), 4, 3.0, 0.1, 0.25, 0.0)
		"playerHit":
			burst(ev.x, ev.y, Color("ff5a4a"), 12, 6.0, 0.16)
		"boxChip":
			var D: Dictionary = Tune.L.DESTRUCT[ev.b.tag]
			burst(ev.x, ev.y, Color("ffffff") if ev.get("hard") else Color(D.color), 5, 4.0, 0.14, 0.35)
		"boxBreak":
			var D: Dictionary = Tune.L.DESTRUCT[ev.b.tag]
			burst(ev.x, ev.y, Color(D.color), 34, 8.0, 0.24, 0.9, -18.0)
		"bossSlam", "chargeCrash":
			burst(ev.get("x", ev.e.x), ev.get("y", ev.e.y) + 0.1, Color("d9dfe7"), 20, 6.0, 0.22, 0.5, -8.0, 60.0)
		"bossPhase", "bossDown":
			flash(ev.x, ev.y, 4.0, Color("ff2e7e"), 0.5)
		"ultNova":
			flash(ev.x, ev.y, ev.r, Color("fff3c4"), 0.6)
		"powerUp":
			burst(ev.p.x, ev.p.y + 1.0, Color(PlayerView.PLAYER_COLORS[ev.p.slot % 4]), 16, 4.0, 0.14, 0.5, 2.0)
