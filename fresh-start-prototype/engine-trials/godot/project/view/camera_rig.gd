# The camera and the light: the follow camera (render.js updateCamera: the sim's camera target interpolated
# between ticks, a lead ahead of the running team, the ultimate's push-in, trauma shake) and each route's
# atmosphere (landmarks.js ATMOS: fog, sky gradient, sun and ambient colour), blended in as the camera arrives.
class_name CameraRig
extends Node3D

const LEAD := { "per": 0.22, "max": 2.2, "rate": 1.8 }
const ATMOS := {
	"skyport": { "fog": "c6e2f4", "near": 70.0, "far": 260.0, "top": "2b7fd3", "mid": "7fbfee", "bot": "d9eefa", "sun": "ffeed6", "hemi": "d8ecff" },
	"foundry": { "fog": "e6c3a0", "near": 50.0, "far": 210.0, "top": "3a4f78", "mid": "d99a6a", "bot": "f2c79a", "sun": "ffc690", "hemi": "ffe0c2" },
	"undercity": { "fog": "8e8fb8", "near": 45.0, "far": 200.0, "top": "1c2147", "mid": "6d5f9e", "bot": "d6a0b8", "sun": "ffb7c9", "hemi": "bfc6ff" },
}
const SKY_SHADER := """
shader_type sky;
uniform vec3 top : source_color;
uniform vec3 mid : source_color;
uniform vec3 bot : source_color;
uniform vec3 hemi : source_color;
uniform vec3 ground : source_color;
void sky() {
	float h = EYEDIR.y;
	// The ambient light is taken from the radiance pass: render.js's hemisphere light, its sky colour from
	// above and its ground colour from below
	vec3 c = h > 0.15 ? mix(mid, top, smoothstep(0.15, 0.7, h)) : mix(bot, mid, smoothstep(-0.2, 0.15, h));
	COLOR = AT_CUBEMAP_PASS ? mix(ground, hemi, 0.5 + 0.5 * h) : c;
}
"""

var world: World
var camera: Camera3D
var sun: DirectionalLight3D
var env: Environment
var sky_mat: ShaderMaterial
var cam := { "x": 0.0, "y": 3.0, "dist": 16.0 }
var cam_prev := {}
var cam_cur := {}
var cam_tick := -1
var lead := 0.0
var trauma := 0.0
var punch := 0.0
var atmos := {}

func setup(w: World) -> void:
	world = w
	camera = Camera3D.new()
	camera.fov = Tune.settings.get("fov", 34.0)
	camera.near = 0.5; camera.far = 600
	add_child(camera)
	camera.make_current()

	sky_mat = ShaderMaterial.new()
	var sh := Shader.new(); sh.code = SKY_SHADER
	sky_mat.shader = sh
	var sky := Sky.new(); sky.sky_material = sky_mat
	env = Environment.new()
	env.background_mode = Environment.BG_SKY
	env.sky = sky
	sky.radiance_size = Sky.RADIANCE_SIZE_32
	sky_mat.set_shader_parameter("ground", Color("7a6f63"))
	env.ambient_light_source = Environment.AMBIENT_SOURCE_SKY
	env.reflected_light_source = Environment.REFLECTION_SOURCE_DISABLED
	env.ambient_light_energy = 0.30   # three.js's hemisphere light 0.95 / pi (its lights are physically scaled)
	env.tonemap_mode = Environment.TONE_MAPPER_ACES
	# three.js's ACES (exposure 0.95) is fit(x * 0.95 / 0.6); Godot's is fit(x * exposure * 1.8) / fit(white * 1.8).
	# So exposure about 0.88 (0.8 matches the prototype's frames best) and a white point far enough out that its divisor is about 1
	env.tonemap_exposure = 0.8
	env.tonemap_white = 16.0
	env.fog_enabled = true
	env.fog_mode = Environment.FOG_MODE_DEPTH
	env.fog_density = 1.0
	env.fog_sky_affect = 0.0
	env.glow_enabled = true
	# (render.js: UnrealBloomPass strength 0.65, threshold 1.5: only emissive light blooms)
	env.glow_intensity = 0.45
	env.glow_bloom = 0.0
	env.glow_hdr_threshold = 1.5
	env.glow_hdr_scale = 1.0
	env.glow_blend_mode = Environment.GLOW_BLEND_MODE_ADDITIVE
	# A tight bloom (the bloom radius there is 0.5): the small blur levels only, so big lit surfaces do not haze
	for i in 7:
		env.set_glow_level(i, [0.0, 1.0, 0.6, 0.3, 0.0, 0.0, 0.0][i])
	var we := WorldEnvironment.new(); we.environment = env
	add_child(we)

	sun = DirectionalLight3D.new()
	sun.light_energy = 0.68   # 2.1 / pi
	sun.shadow_enabled = true
	sun.directional_shadow_mode = DirectionalLight3D.SHADOW_ORTHOGONAL
	sun.directional_shadow_max_distance = 60
	sun.shadow_bias = 0.05
	add_child(sun)
	var rim := DirectionalLight3D.new()
	rim.light_color = Color("a9dbff"); rim.light_energy = 0.45   # 1.4 / pi
	add_child(rim)
	rim.look_at_from_position(Vector3(14, 12, -26), Vector3.ZERO)

	# The Compatibility renderer (the web build) lights from the sky differently: a flat ambient there, a little lower
	if RenderingServer.get_rendering_device() == null:
		env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
		env.ambient_light_color = Color("b7c3d2")
		env.ambient_light_energy = 0.15
		env.tonemap_exposure = 0.7
	cam = { "x": world.cam.x, "y": world.cam.y, "dist": world.cam.dist }
	_apply_atmos(_route_atmos(), 1.0)

func add_trauma(amount: float) -> void:
	trauma = minf(1.0, trauma + amount)

func thump(amount: float) -> void:
	punch = minf(punch, -amount)

# How hard each event shakes the frame, and the big moments that thump it down (render.js onEvent)
func on_event(ev: Dictionary) -> void:
	var lv: float = ev.get("level", 0) if ev.get("level") != null else 0.0
	var s := 0.0
	match ev.type:
		"boxBreak": s = 0.4 if ev.b.tag == "pillar" else (0.12 if ev.b.tag == "glass" else 0.2)
		"armorBreak": s = 0.5
		"slam": s = 0.45
		"guardBreak": s = 0.3
		"ambush": s = 0.35
		"playerHit": s = 0.35 if ev.get("heavy") else 0.15
		"blast": s = 0.14 + (lv if lv else 1.0) * 0.06 + (0.1 if ev.get("perfect") else 0.0)
		"burst": s = 0.06 + lv * 0.04 if ev.get("charged") else 0.05
		"perfectRelease": s = 0.1
		"splash": s = 0.04 + lv * 0.03 if lv else 0.0
		"rocketJump": s = 0.2 + 0.42 * ev.get("power", 0.5)
		"enemyBlast", "chargeCrash": s = 0.3
		"shot": s = 0.03 + lv * 0.04 if lv else 0.0
		"dash": s = 0.04 + lv * 0.05 if lv else 0.0
		"dashLevel": s = 0.02 + lv * 0.02
		"chargeLevel": s = 0.1 if lv >= 4 else (0.04 if lv >= 3 else 0.0)
		"walljump": s = 0.03
		"land": s = minf(0.3, (-ev.vy - 16) * 0.025) if ev.vy < -16 else 0.0
		"poundLand": s = 0.22 + 0.12 * lv
		"poundDrop": s = 0.03
		"aegisHit": s = 0.05
		"beamStart": s = 0.3
		"aegisOff": s = 0.3 if ev.why == "break" else (0.4 if ev.why == "detonate" else 0.0)
		"bossSlam": s = 0.55 if ev.get("big") else 0.35
		"bossPhase": s = 0.6
		"bossDown": s = 0.9
		"bossCrash": s = 0.45
		"bossIntro": s = 0.15
		"frag": s = 0.12 + 0.05 * lv
		"cluster": s = 0.1
		"chain": s = 0.04 + 0.03 * lv
		"wellOpen": s = 0.08
		"wellCollapse": s = 0.18 + 0.06 * (lv if lv else 1.0)
		"riseBlast": s = 0.14
		"perfectDodge": s = 0.2
		"ultCast": s = 0.35
		"ultJoin": s = 0.3
		"ultNova": s = 0.95
		"ultCut": s = 0.06
		"ultFinisher": s = 0.8
		"teamFinisher": s = 0.3
	if s > 0:
		add_trauma(s)
	match ev.type:
		"rocketJump": thump(0.25 + 0.5 * ev.get("power", 0.5))
		"poundLand": thump(0.15 + 0.12 * lv)
		"ultNova": thump(0.6)

func _route_atmos() -> Dictionary:
	var r = Level.route_at(cam.x)
	var id: String = r.id if r != null else "skyport"
	return ATMOS.get(id, ATMOS.skyport)

func _lerp_col(a: Color, hex: String, k: float) -> Color:
	return a.lerp(Color(hex), k)

func _apply_atmos(A: Dictionary, k: float) -> void:
	if atmos.is_empty():
		atmos = { "fog": Color(A.fog), "near": A.near, "far": A.far, "top": Color(A.top), "mid": Color(A.mid), "bot": Color(A.bot), "sun": Color(A.sun), "hemi": Color(A.hemi) }
	for key in ["fog", "top", "mid", "bot", "sun", "hemi"]:
		atmos[key] = _lerp_col(atmos[key], A[key], k)
	atmos.near += (A.near - atmos.near) * k
	atmos.far += (A.far - atmos.far) * k
	env.fog_light_color = atmos.fog
	env.fog_depth_begin = atmos.near
	env.fog_depth_end = atmos.far
	sky_mat.set_shader_parameter("top", atmos.top)
	sky_mat.set_shader_parameter("mid", atmos.mid)
	sky_mat.set_shader_parameter("bot", atmos.bot)
	sun.light_color = atmos.sun
	sky_mat.set_shader_parameter("hemi", atmos.hemi)

func update(alpha: float, dt: float) -> void:
	# The frame follows the sim's camera target interpolated between ticks, and leads a little ahead of the team
	if world.tick != cam_tick:
		cam_prev = cam_cur if not cam_cur.is_empty() else world.cam.duplicate()
		cam_cur = world.cam.duplicate()
		cam_tick = world.tick
	var T := { "x": lerpf(cam_prev.x, cam_cur.x, alpha), "y": lerpf(cam_prev.y, cam_cur.y, alpha), "dist": lerpf(cam_prev.dist, cam_cur.dist, alpha) }
	var rate := 5.5
	var act: Array = world.players.filter(func(p): return p.state != "dead" and p.state != "downed")
	if not act.is_empty():
		var vx := 0.0
		var lo := INF
		var hi := -INF
		for p in act:
			vx += p.vx; lo = minf(lo, p.x); hi = maxf(hi, p.x)
		vx /= act.size()
		var want := clampf(vx * LEAD.per, -LEAD.max, LEAD.max) * maxf(0, 1 - (hi - lo) / 8)
		lead += (want - lead) * (1 - exp(-dt * LEAD.rate))
		if world.ult_cast == null:
			T.x += lead
	# An ultimate's call pushes in on whoever is calling it; while it plays out the frame eases back
	var Uc = world.ult_cast
	if Uc != null and not Uc.members.is_empty():
		var n: int = Uc.members.size()
		var mx := 0.0
		var my := 0.0
		for m in Uc.members:
			mx += m.x; my += m.y
		mx /= n; my = my / n + 1.1
		if Uc.phase == "cast":
			T = { "x": mx, "y": my + 0.4, "dist": maxf(8.5, T.dist * 0.6) }; rate = 9
		elif Uc.phase == "run":
			T = { "x": T.x * 0.7 + mx * 0.3, "y": T.y * 0.7 + my * 0.3, "dist": T.dist * 1.04 }
	var k := 1 - exp(-dt * rate)
	# Vertical follow speeds up the further behind it falls, so a big launch never leaves the frame
	var ky := 1 - exp(-dt * (rate + maxf(0, absf(T.y - cam.y) - 1.2) * 5))
	cam.x += (T.x - cam.x) * k; cam.y += (T.y - cam.y) * ky; cam.dist += (T.dist - cam.dist) * k
	trauma = maxf(0, trauma - dt * 1.8)
	for p in world.players:
		if p.state == "beam" and p.beam != null:
			trauma = maxf(trauma, 0.22)
		if p.state == "ult" and p.ult_run != null and p.ult_run.get("segs"):
			trauma = maxf(trauma, 0.4)
	punch *= exp(-dt * 10)
	_apply_atmos(_route_atmos(), 1 - exp(-dt * 2.5))

	var f := PathFrame.frame(cam.x)
	var d: float = cam.dist
	var look := Vector3(f.px, cam.y + punch * 0.6, f.pz)
	var pos := Vector3(f.px + f.nx * d, cam.y + d * 0.1 + punch, f.pz + f.nz * d)
	if Tune.settings.get("shake", true) and trauma > 0:
		var s := trauma * trauma * 0.35
		pos.x += randf_range(-0.5, 0.5) * s; pos.y += randf_range(-0.5, 0.5) * s
	camera.fov = Tune.settings.get("fov", 34.0)
	camera.look_at_from_position(pos, look)
	# Keep the sun's shadows centred on the action
	sun.look_at_from_position(look + Vector3(-18, 30, 22), look)
