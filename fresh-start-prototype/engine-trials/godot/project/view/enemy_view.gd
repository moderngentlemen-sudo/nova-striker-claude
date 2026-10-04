# One enemy's picture: its construct rig (pale ceramic plating, graphite joints, reserved hostile magenta
# energy), posed each frame from the simulation, with the telegraph glint, stun stars and a health bar. A port
# of enemyRigs.js. Presentation only.
class_name EnemyView
extends Node3D

const HOSTILE := "ff2e7e"
const TELEGRAPH := { "standard": Color("ffe066"), "heavy": Color("ff8a2a"), "unblockable": Color("ff2e7e") }

var e: EnemySim
var flip: Node3D
var body: Node3D
var P := {}
var plate: StandardMaterial3D
var joint: StandardMaterial3D
var energy: StandardMaterial3D
var lean := 0.0
var bob := 0.0
var phase := 0.0
var glint: MeshInstance3D
var glint_mat: StandardMaterial3D
var tele_t := 0.0
var tele_len := 1.0
var stars: Node3D
var bar: Node3D
var bar_fill: MeshInstance3D

func setup(enemy: EnemySim) -> void:
	e = enemy
	plate = Rig.std("e6e9f0", 0.34, 0.06)
	plate.emission_enabled = true; plate.emission = Color.WHITE; plate.emission_energy_multiplier = 0.0
	joint = Rig.std("2b2f3a", 0.6, 0.2)
	energy = Rig.glow(HOSTILE, 2.2)
	flip = Rig.grp(self)
	body = Rig.grp(flip)
	_build()
	# Telegraph glint over the head: yellow standard, orange heavy, magenta unblockable
	glint_mat = Rig.clear("ffffff", 0.0, 3.0)
	glint_mat.billboard_mode = BaseMaterial3D.BILLBOARD_ENABLED
	glint = MeshInstance3D.new()
	var q := QuadMesh.new(); q.size = Vector2(0.5, 0.5)
	glint.mesh = q; glint.material_override = glint_mat
	glint.position = Vector3(0, e.h + 0.45, 0.3)
	glint.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(glint)
	# Stun stars: three gold sparks circling the head
	stars = Rig.grp(self, 0, e.h + 0.2, 0)
	var sm := Rig.glow("ffd23f", 3.0)
	for i in 3:
		var s := Rig.part(stars, Rig.sphere(0.07, 8), sm)
		s.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	stars.visible = false
	# A thin health bar once it has been hurt (not for bosses: the HUD shows theirs)
	bar = Rig.grp(self, 0, e.h + 0.3, 0.5)
	var back := Rig.part(bar, Rig.rbox(0.9, 0.07, 0.01), Rig.clear("1b1f2a", 0.7, 0.0))
	back.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	bar_fill = Rig.part(bar, Rig.rbox(0.9, 0.07, 0.02), Rig.clear(HOSTILE, 0.95, 1.4))
	bar_fill.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	bar.visible = false

func telegraph(cat: String, ticks: int) -> void:
	glint_mat.albedo_color = TELEGRAPH.get(cat, Color.WHITE)
	glint_mat.emission = glint_mat.albedo_color
	tele_t = ticks / 60.0; tele_len = maxf(0.1, tele_t)

func _build() -> void:
	match e.type:
		"swarmer":
			P.core = Rig.grp(body, 0, 0.42, 0)
			Rig.part(P.core, Rig.rbox(0.62, 0.42, 0.52), plate)
			Rig.part(P.core, Rig.rbox(0.3, 0.12, 0.54), joint, -0.05, 0.2)
			P.eye = Rig.part(P.core, Rig.sphere(0.11), energy, 0.29, 0.03)
			for xz in [[0.18, 0.2], [-0.18, 0.2], [0.18, -0.2], [-0.18, -0.2]]:
				var l := Rig.part(body, Rig.cap(0.045, 0.3), joint, xz[0], 0.2, xz[1])
				l.rotation.z = -0.5 if xz[0] > 0 else 0.5
		"shield":
			for z in [0.16, -0.16]:
				Rig.part(body, Rig.cap(0.09, 0.62), joint, 0, 0.42, z)
			P.torso = Rig.grp(body, 0, 1.0, 0)
			Rig.part(P.torso, Rig.rbox(0.55, 0.78, 0.6), plate, 0, 0.25)
			P.eye = Rig.part(P.torso, Rig.rbox(0.06, 0.07, 0.3), energy, 0.28, 0.58)
			P.shield = Rig.grp(P.torso, 0.55, 0.1, 0)
			Rig.part(P.shield, Rig.rbox(0.14, 1.55, 1.05), plate)
			for y in [-0.74, 0.74]:
				Rig.part(P.shield, Rig.rbox(0.16, 0.05, 1.0), energy, 0.02, y)
			for z in [-0.5, 0.5]:
				Rig.part(P.shield, Rig.rbox(0.16, 1.45, 0.05), energy, 0.02, 0, z)
		"sniper":
			for z in [0.12, -0.12]:
				Rig.part(body, Rig.cap(0.07, 0.7), joint, 0, 0.45, z)
			P.torso = Rig.grp(body, 0, 1.05, 0)
			Rig.part(P.torso, Rig.rbox(0.4, 0.62, 0.46), plate, 0, 0.2)
			P.eye = Rig.part(P.torso, Rig.sphere(0.07), energy, 0.2, 0.52)
			P.gun = Rig.grp(P.torso, 0.05, 0.3, 0.26)
			var barrel := Rig.part(P.gun, Rig.cyl(0.05, 0.06, 1.5, 10), joint, 0.75, 0, 0)
			barrel.rotation.z = PI / 2
			Rig.part(P.gun, Rig.rbox(0.4, 0.14, 0.12), plate, 0.1, 0, 0)
			P.muzzle = Rig.part(P.gun, Rig.sphere(0.06), energy, 1.52, 0, 0)
		"brute":
			for z in [0.36, -0.36]:
				Rig.part(body, Rig.cap(0.2, 0.8), joint, 0, 0.62, z)
			P.torso = Rig.grp(body, 0, 1.4, 0)
			Rig.part(P.torso, Rig.rbox(1.1, 1.05, 1.1), joint, 0, 0.35)
			P.core = Rig.part(P.torso, Rig.sphere(0.22), energy, 0.5, 0.4)
			P.plates = [Rig.part(P.torso, Rig.rbox(0.3, 0.8, 0.95), plate, 0.52, 0.35), Rig.part(P.torso, Rig.rbox(0.7, 0.3, 0.45), plate, 0, 0.98, 0.42),
				Rig.part(P.torso, Rig.rbox(0.7, 0.3, 0.45), plate, 0, 0.98, -0.42)]
			P.head = Rig.part(P.torso, Rig.rbox(0.34, 0.3, 0.36), plate, 0.3, 1.02)
			Rig.part(P.head, Rig.rbox(0.06, 0.06, 0.28), energy, 0.17, 0.02)
			P.armN = Rig.grp(P.torso, 0, 0.8, 0.72); P.armF = Rig.grp(P.torso, 0, 0.8, -0.72)
			for a in [P.armN, P.armF]:
				Rig.part(a, Rig.cap(0.17, 0.75), joint, 0, -0.45)
				Rig.part(a, Rig.rbox(0.5, 0.45, 0.45), plate, 0.05, -1.0)
		"post":
			Rig.part(body, Rig.cyl(0.5, 0.6, 0.25, 20), joint, 0, 0.12)
			Rig.part(body, Rig.cap(0.2, 1.4), plate, 0, 1.0)
			P.arm = Rig.grp(body, 0, 1.45, 0.3)
			Rig.part(P.arm, Rig.rbox(1.3, 0.2, 0.2), plate, 0.6, 0)
			P.eye = Rig.part(body, Rig.sphere(0.12), energy, 0.18, 1.8)
		"turret":
			var dome := SphereMesh.new(); dome.radius = 0.42; dome.height = 0.42; dome.is_hemisphere = true
			Rig.part(body, dome, plate)
			P.gun = Rig.grp(body, 0, 0.25, 0)
			var b := Rig.part(P.gun, Rig.cyl(0.07, 0.09, 0.8, 10), joint, 0.4, 0, 0)
			b.rotation.z = PI / 2
			P.eye = Rig.part(P.gun, Rig.sphere(0.07), energy, 0.82, 0, 0)
		"drone":
			P.core = Rig.grp(body, 0, 0.3, 0)
			Rig.part(P.core, Rig.sphere(0.3), plate).scale = Vector3(1.2, 0.72, 1.2)
			P.eye = Rig.part(P.core, Rig.sphere(0.1), energy, 0.31, -0.02)
			Rig.part(P.core, Rig.rbox(0.5, 0.04, 0.05), energy, 0, -0.2)
			for z in [0.26, -0.26]:
				Rig.part(P.core, Rig.rbox(0.3, 0.12, 0.05), joint, -0.12, -0.06, z)
			P.rotor = Rig.grp(P.core, 0, 0.24, 0)
			Rig.part(P.rotor, Rig.torus(0.44, 0.035, 28), joint)
			for i in 3:
				Rig.part(P.rotor, Rig.rbox(0.82, 0.02, 0.08), plate).rotation.y = i * PI / 3
		"mortar":
			Rig.part(body, Rig.cyl(0.56, 0.64, 0.3, 20), joint, 0, 0.15)
			P.torso = Rig.grp(body, 0, 0.55, 0)
			Rig.part(P.torso, Rig.rbox(0.82, 0.5, 0.82), plate)
			P.eye = Rig.part(P.torso, Rig.rbox(0.05, 0.07, 0.5), energy, 0.41, 0.06)
			P.tube = Rig.grp(P.torso, 0.05, 0.22, 0)
			Rig.part(P.tube, Rig.cyl(0.17, 0.21, 0.95, 14), plate, 0, 0.47)
			Rig.part(P.tube, Rig.torus(0.17, 0.045, 16), energy, 0, 0.95)
			P.tube.rotation.z = -0.45
		"charger":
			P.legs = []
			for xz in [[0.38, 0.3], [-0.38, 0.3], [0.38, -0.3], [-0.38, -0.3]]:
				var l := Rig.grp(body, xz[0], 0.62, xz[1])
				Rig.part(l, Rig.cap(0.09, 0.42), joint, 0, -0.3)
				P.legs.append(l)
			P.torso = Rig.grp(body, 0, 0.9, 0)
			Rig.part(P.torso, Rig.rbox(1.1, 0.66, 0.82), joint, -0.05, 0)
			P.ram = Rig.part(P.torso, Rig.rbox(0.3, 0.78, 0.92), plate, 0.56, 0.02)
			for y in [-0.12, 0.16]:
				Rig.part(P.ram, Rig.rbox(0.04, 0.05, 0.6), energy, 0.16, y)
			for z in [0.3, -0.3]:
				Rig.part(P.torso, Rig.cone(0.07, 0.4, 6), energy, 0.66, 0.46, z).rotation.z = -1.0
			P.plates = [Rig.part(P.torso, Rig.rbox(0.72, 0.16, 0.86), plate, -0.12, 0.4)]
		"warden":
			P.legs = []
			for z in [0.5, -0.5]:
				var hip := Rig.grp(body, 0, 1.55, z)
				var knee := Rig.grp(hip, 0.05, -0.75, 0)
				Rig.part(hip, Rig.cap(0.24, 0.55), joint, 0, -0.38); Rig.part(hip, Rig.rbox(0.5, 0.55, 0.42), plate, 0.08, -0.3)
				Rig.part(knee, Rig.cap(0.2, 0.5), joint, 0, -0.35); Rig.part(knee, Rig.rbox(0.42, 0.5, 0.38), plate, 0.12, -0.35)
				Rig.part(knee, Rig.rbox(0.8, 0.22, 0.5), joint, 0.12, -0.72)
				P.legs.append({ "hip": hip, "knee": knee })
			P.torso = Rig.grp(body, 0, 1.65, 0)
			Rig.part(P.torso, Rig.rbox(1.25, 0.4, 1.0), joint)
			P.chest = Rig.grp(P.torso, 0, 0.3, 0)
			Rig.part(P.chest, Rig.rbox(1.5, 1.05, 1.3), joint, 0, 0.55)
			P.core = Rig.part(P.chest, Rig.sphere(0.26), energy, 0.72, 0.55)
			P.head = Rig.grp(P.chest, 0.42, 1.2, 0)
			Rig.part(P.head, Rig.rbox(0.55, 0.38, 0.5), plate)
			P.eye = Rig.part(P.head, Rig.rbox(0.06, 0.08, 0.42), energy, 0.28, 0.02)
			P.pod = Rig.grp(P.chest, -0.7, 1.05, 0)
			Rig.part(P.pod, Rig.rbox(0.6, 0.55, 0.9), plate)
			P.tubes = []
			for i in 5:
				P.tubes.append(Rig.part(P.pod, Rig.cyl(0.07, 0.07, 0.06, 10), energy, -0.05 + (i % 2) * 0.12, 0.29, -0.3 + i * 0.15))
			P.plates = [Rig.part(P.chest, Rig.rbox(0.3, 0.8, 1.05), plate, 0.72, 0.45), Rig.part(P.chest, Rig.rbox(0.8, 0.35, 0.55), plate, 0, 1.12, 0.62),
				Rig.part(P.chest, Rig.rbox(0.8, 0.35, 0.55), plate, 0, 1.12, -0.62), Rig.part(P.head, Rig.rbox(0.4, 0.14, 0.3), plate, -0.05, 0.25)]
			P.armN = Rig.grp(P.chest, 0.05, 0.95, 0.95); P.armF = Rig.grp(P.chest, 0.05, 0.95, -0.95)
			Rig.part(P.armN, Rig.cap(0.2, 0.7), joint, 0, -0.45)
			var hammer := Rig.grp(P.armN, 0.05, -1.05, 0)
			Rig.part(hammer, Rig.rbox(0.85, 0.72, 0.72), plate); Rig.part(hammer, Rig.rbox(0.06, 0.5, 0.6), energy, 0.44, 0)
			Rig.part(P.armF, Rig.cap(0.18, 0.65), joint, 0, -0.42)
			var blade := Rig.part(P.armF, Rig.rbox(0.22, 1.7, 0.1), plate, 0.1, -1.4)
			Rig.part(blade, Rig.rbox(0.05, 1.6, 0.12), energy, 0.12, 0)
		"stormcaller":
			P.hull = Rig.grp(body, 0, 0.75, 0)
			Rig.part(P.hull, Rig.sphere(0.62, 24), plate).scale = Vector3(2.3, 0.72, 1.25)
			Rig.part(P.hull, Rig.sphere(0.5, 20), joint, -0.1, -0.2).scale = Vector3(2.2, 0.55, 1.1)
			P.eye = Rig.part(P.hull, Rig.sphere(0.2), energy, 1.28, 0.02)
			for z in [0.62, -0.62]:
				Rig.part(P.hull, Rig.rbox(1.8, 0.05, 0.05), energy, 0.1, 0.18, z)
			P.cannon = Rig.grp(P.hull, 0.8, -0.38, 0)
			Rig.part(P.cannon, Rig.cyl(0.09, 0.12, 0.9, 12), joint, 0.45, 0, 0).rotation.z = PI / 2
			Rig.part(P.cannon, Rig.sphere(0.08), energy, 0.92, 0, 0)
			P.rotors = []
			for z in [1.1, -1.1]:
				var nac := Rig.grp(P.hull, -0.15, 0.05, z)
				Rig.part(nac, Rig.cyl(0.28, 0.34, 0.3, 16), joint)
				Rig.part(nac, Rig.cyl(0.2, 0.2, 0.04, 16), energy, 0, -0.17)
				var rotor := Rig.grp(nac, 0, 0.2, 0)
				Rig.part(rotor, Rig.torus(0.62, 0.04, 32), joint)
				for i in 3:
					Rig.part(rotor, Rig.rbox(1.2, 0.02, 0.1), plate).rotation.y = i * PI / 3
				P.rotors.append(rotor)
			P.shield = Rig.part(P.hull, Rig.sphere(1.0, 12), Rig.clear(HOSTILE, 0.22, 1.6))
			P.shield.scale = Vector3(1.9, 0.95, 1.4); P.shield.visible = false

static func _ease(v: float, target: float, k: float) -> float:
	return v + (target - v) * k

# Each frame: place it (interpolated between ticks) and pose it (enemyRigs.js animateEnemy)
func animate(alpha: float, dt: float, t: float) -> void:
	var x := lerpf(e.prev_x, e.x, alpha)
	var y := lerpf(e.prev_y, e.y, alpha)
	position = PathFrame.point(x, y)
	rotation.y = PathFrame.yaw(x)
	flip.scale.x = e.shield_dir if e.type == "shield" else e.facing
	var target_lean := 0.0
	var g := 2.2
	var s := e.state
	if s == "windup" or s == "slamWindup" or s == "aim":
		target_lean = -0.18; g = 3.5 + sin(t * 30) * 1.2
		if s == "slamWindup":
			target_lean = -0.3; g = 5 + sin(t * 45) * 2; body.position.x = sin(t * 70) * 0.03
	elif s == "lock": g = 6
	elif s == "attack": target_lean = 0.35
	elif s == "charge":
		target_lean = 0.28; g = 5
	elif s == "dazed":
		target_lean = -0.3 + sin(t * 7) * 0.1; g = 0.5
	elif s == "stagger":
		target_lean = -0.35 + sin(t * 9) * 0.12; g = 0.6
	elif s == "hitstun": target_lean = -0.2
	elif s == "launched": target_lean = sin(t * 12) * 0.5
	if s != "slamWindup":
		body.position.x = 0
	lean += (target_lean - lean) * 0.35
	body.rotation.z = -lean
	energy.emission_energy_multiplier = g
	plate.emission_energy_multiplier = 0.9 if e.flash > 0 else 0.0
	match e.type:
		"swarmer":
			var moving := absf(e.vx) > 0.5 and e.on_ground
			bob = absf(sin(t * 16)) * 0.12 if moving else bob * 0.8
			P.core.position.y = 0.42 + bob + (-0.1 if s == "windup" else 0.0)
		"sniper":
			if s == "aim" or s == "lock":
				var dx := (e.aim_x - e.x) * e.facing
				var dy := e.aim_y - (e.y + 1.35)
				P.gun.rotation.z = atan2(dy, maxf(0.1, dx))
			else:
				P.gun.rotation.z *= 0.9
		"brute":
			for i in P.plates.size():
				P.plates[i].visible = i < e.armor
			var swing := -1.2 if s == "windup" else (1.4 if s == "attack" else (-2.6 if s == "slamWindup" else (1.2 if s == "slamRecover" and e.st < 8 else 0.0)))
			P.armN.rotation.z += (swing - P.armN.rotation.z) * 0.3
			var sf := -2.6 if s == "slamWindup" else (1.2 if s == "slamRecover" and e.st < 8 else 0.1)
			P.armF.rotation.z += (sf - P.armF.rotation.z) * 0.3
			energy.emission_energy_multiplier = g + (3 - e.armor) * 0.8
		"post":
			var tg := -0.9 if s == "windup" else (1.1 if s == "attack" else 0.0)
			P.arm.rotation.z += (tg - P.arm.rotation.z) * 0.3
		"drone":
			P.rotor.rotation.y += dt * (40 if s == "windup" else 22)
			P.core.rotation.z = -clampf(e.vx * 0.06 * e.facing, -0.4, 0.4)
		"mortar":
			var raise := minf(1, e.st / 20.0) if s == "windup" else 0.0
			var kick := 1 - e.st / 10.0 if s == "recover" and e.st < 10 else 0.0
			P.tube.rotation.z = -0.45 + raise * 0.25 - kick * 0.2
			P.tube.scale.y = 1 - kick * 0.18
		"charger":
			P.plates[0].visible = e.armor > 0
			var run := 26.0 if s == "charge" else (12.0 if absf(e.vx) > 0.5 else 0.0)
			var paw := sin(t * 22) * 0.35 if s == "windup" else 0.0
			for i in P.legs.size():
				P.legs[i].rotation.z = sin(t * run + i * PI / 2) * 0.55 if run else (paw if i == 0 else 0.0)
		"turret":
			var tg = e.target
			if tg != null:
				var a := atan2(tg.y + 1 - (e.y + 0.25), (tg.x - e.x) * e.facing)
				P.gun.rotation.z += (a - P.gun.rotation.z) * 0.2
		"warden": _warden(dt, t)
		"stormcaller": _storm(dt, t)
	if e.dead and e.boss:
		# A boss shudders and sags through its explosions, then goes in the last blast
		var k := minf(1, e.death_t / 68.0)
		var gone := maxf(0, (e.death_t - 68) / 8.0)
		body.position.x = sin(t * 60) * 0.06 * (1 - gone); body.rotation.z = -0.35 * k + sin(t * 23) * 0.04
		scale = Vector3.ONE * maxf(0.01, 1 - minf(1, gone))
		energy.emission_energy_multiplier = 4 + sin(t * 40) * 3
	elif e.dead:
		var k := minf(1, e.death_t / 40.0)
		scale = Vector3.ONE * maxf(0.01, 1 - k * 0.9)
		body.rotation.z = -0.8 * k
	# Telegraph glint, stun stars, health bar
	tele_t = maxf(0, tele_t - dt)
	var gk := tele_t / tele_len
	glint_mat.albedo_color.a = (0.6 + 0.4 * sin(t * 40)) * minf(1, gk * 3) if tele_t > 0 and not e.dead else 0.0
	glint.scale = Vector3.ONE * (0.6 + 0.8 * (1 - gk))
	var stunned := not e.dead and (e.dizzy or s == "stagger")
	stars.visible = stunned
	if stunned:
		var left := clampf(1.0 - float(e.st) / maxf(1, e.stun), 0, 1)
		var r := 0.25 + 0.2 * left
		for i in 3:
			var a := t * 6 + i * TAU / 3
			stars.get_child(i).position = Vector3(cos(a) * r, 0.1 * sin(a * 2), sin(a) * r)
	var hurt: bool = e.hp < e.max_hp and e.max_hp != INF and not e.boss and not e.dead
	bar.visible = hurt
	if hurt:
		var f := clampf(e.hp / e.max_hp, 0, 1)
		bar_fill.scale.x = maxf(0.01, f); bar_fill.position.x = -0.45 * (1 - f)

func _warden(dt: float, t: float) -> void:
	var s := e.state
	var A = e.atk
	var k := minf(1, dt * 14)
	var kind = A.kind if A != null else null
	for i in P.plates.size():
		P.plates[i].visible = i < e.armor
	var walking: bool = (s == "idle" or s == "approach") and absf(e.vx) > 0.4
	phase += dt * absf(e.vx) * 1.6 if walking else 0.0
	var sw := sin(phase) if walking else 0.0
	var hipN := sw * 0.45
	var hipF := -sw * 0.45
	var knN := -maxf(0, -cos(phase)) * 0.6 * (1.0 if walking else 0.0)
	var knF := -maxf(0, cos(phase)) * 0.6 * (1.0 if walking else 0.0)
	var armN := 0.15
	var armF := -0.1
	var twist := 0.0
	var crouch := absf(cos(phase)) * 0.08 if walking else 0.0
	var lean_w := 0.0
	var head := 0.0
	var pod := 0.0
	var w := s == "windup"
	var u: float = minf(1, e.st / maxf(1, A.wind)) if w and A != null else 0.0
	if s == "intro" and not e.on_ground:
		hipN = 0.3; hipF = -0.2; knN = -0.3; knF = -0.4; armN = -0.6; armF = -0.6
	elif s == "intro" and e.st < 140:
		crouch = maxf(0, 0.5 - e.st * 0.01); armN = 0.9; armF = 0.9
	elif s == "roar":
		armN = -1.9; armF = -1.9; head = -0.4; lean_w = -0.2; twist = sin(t * 30) * 0.03
	elif s == "dazed" or s == "stagger":
		lean_w = 0.35 + sin(t * 6) * 0.08; head = 0.5; armN = 0.4; armF = 0.3; crouch = 0.25
	elif kind == "sweep":
		if w: armF = -1.5 * u; twist = 0.5 * u
		elif s == "attack": armF = 1.5; twist = -0.55
		else: armF = 0.9; twist = -0.3
	elif kind == "hammer":
		if w: armN = -2.7 * u; lean_w = -0.15 * u
		elif s == "attack": armN = 1.35; lean_w = 0.35; crouch = 0.2
		else: armN = 1.1; lean_w = 0.25; crouch = 0.15
	elif kind == "stomp":
		if w: crouch = 0.45 * u; armN = -0.8 * u; armF = armN
		elif s == "jump": hipN = 0.9; hipF = 0.9; knN = -1.4; knF = -1.4; armN = -1.2; armF = -1.2
		else: crouch = maxf(0, 0.35 - e.st * 0.02); armN = 0.6; armF = 0.6
	elif kind == "missiles":
		lean_w = 0.25 * (u if w else 1.0); pod = u if w else maxf(0, 1 - e.st / 12.0)
	elif kind == "charge" or s == "charge":
		lean_w = 0.5; armN = 0.9; armF = -0.9; crouch = 0.15
		if s == "charge": hipN = sin(t * 20) * 0.7; hipF = -hipN
	elif kind == "laser":
		head = -0.15 if A.get("high") else 0.35; lean_w = 0.1
	var L: Array = P.legs
	L[0].hip.rotation.z = _ease(L[0].hip.rotation.z, hipN, k); L[1].hip.rotation.z = _ease(L[1].hip.rotation.z, hipF, k)
	L[0].knee.rotation.z = _ease(L[0].knee.rotation.z, knN, k); L[1].knee.rotation.z = _ease(L[1].knee.rotation.z, knF, k)
	var ka := minf(1, dt * 30) if s == "attack" else k
	P.armN.rotation.z = _ease(P.armN.rotation.z, armN, ka); P.armF.rotation.z = _ease(P.armF.rotation.z, armF, ka)
	P.chest.rotation.y = _ease(P.chest.rotation.y, twist, k); P.head.rotation.z = _ease(P.head.rotation.z, -head, k)
	P.torso.position.y = 1.65 - crouch
	for l in L:
		l.hip.position.y = 1.55 - crouch
	body.rotation.z = _ease(body.rotation.z, -lean_w, k)
	var hurt := 1 - e.hp / e.max_hp
	var charging := w or s == "laser" or s == "charge" or s == "roar"
	energy.emission_energy_multiplier = 2.2 + hurt * 2 + (2.5 + sin(t * 34) * 1.2 if charging else 0.0) + (-1.6 if s == "dazed" or s == "stagger" else 0.0)
	for tb in P.tubes:
		tb.scale = Vector3(1 + pod * 0.6, 1 + pod * 3, 1 + pod * 0.6)
	P.core.scale = Vector3.ONE * (1 + hurt * 0.4 + (sin(t * 30) * 0.08 if charging else 0.0))

func _storm(dt: float, t: float) -> void:
	var s := e.state
	var A = e.atk
	var k := minf(1, dt * 8)
	var down := s == "crashed"
	var bank := -clampf(e.vx * 0.05, -0.4, 0.4) * e.facing
	var pitch := 0.0
	if s == "dive" and A != null:
		pitch = -atan2(-A.get("dy", -1.0), absf(A.get("dx", 0.3))) * 0.6
	if down:
		bank = 0.35; pitch = -0.2 + sin(t * 5) * 0.03
	if s == "roar":
		bank = sin(t * 24) * 0.12
	P.hull.rotation.x = _ease(P.hull.rotation.x, 0.25 if down else bank * 0.5, k)
	P.hull.rotation.z = _ease(P.hull.rotation.z, pitch, k)
	P.hull.position.y = 0.75 + (-0.15 if down else sin(t * 2.4) * 0.05)
	for r in P.rotors:
		r.rotation.y += dt * (4 if down else (40 if s == "dive" else 26))
	var aim := 0.0
	var tg = e.target
	if s == "laser" or (s == "windup" and A != null and A.kind == "sweep"):
		aim = -0.35
	elif tg != null:
		aim = clampf(atan2(tg.y + 1 - (e.y + 0.4), maxf(0.5, (tg.x - e.x) * e.facing)), -1.2, 0.4)
	P.cannon.rotation.z = _ease(P.cannon.rotation.z, aim, k)
	P.shield.visible = e.armor > 0
	if P.shield.visible:
		P.shield.rotation.y += dt * 0.6
	var hurt := 1 - e.hp / e.max_hp
	var charging := s in ["windup", "laser", "dive", "roar", "volley"]
	energy.emission_energy_multiplier = 2.2 + hurt * 2 + (2.4 + sin(t * 30) * 1.1 if charging else 0.0) - (1.4 if down else 0.0)
