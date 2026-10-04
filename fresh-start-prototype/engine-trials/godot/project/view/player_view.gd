# One player's picture: the character rig (rigs.js) posed procedurally from the simulation each frame
# (anim.js): a target pose per state, eased joint by joint at a rate set per state; attacks keyframed per
# move; squash and stretch; the aiming layer; the bracer's energy brightening with charge. Nova is ported in
# full; the other characters reuse this skeleton when their kits arrive.
#
# The prototype computes poses from live values (the aim angle, the charge level, the pound's phase), so they
# are built here rather than authored as fixed Animation clips; an AnimationTree takes over when skinned
# character art replaces these placeholder rigs.
class_name PlayerView
extends Node3D

const PLAYER_COLORS := ["5ac8fa", "7ed957", "f5f5f5", "4dd0b8"]
const J := ["spine", "twist", "shN", "elN", "shF", "elF", "hipN", "knN", "hipF", "knF", "hipY", "bodyZ", "head"]
const REST := { "spine": 0.04, "twist": 0.0, "shN": 0.12, "elN": 0.3, "shF": -0.1, "elF": 0.3, "hipN": 0.04, "knN": -0.08, "hipF": -0.04, "knF": -0.08, "hipY": 0.95, "bodyZ": 0.0, "head": 0.0 }
const FIGHT := { "spine": 0.18, "twist": 0.0, "shN": 0.7, "elN": 1.0, "shF": 0.35, "elF": 1.1, "hipN": 0.4, "knN": -0.5, "hipF": -0.32, "knF": -0.35, "hipY": 0.9, "bodyZ": 0.0, "head": -0.1 }
const AIR := { "hipN": 0.9, "knN": -1.35, "hipF": 0.45, "knF": -1.05, "hipY": 0.95 }
const TURN_TIME := 0.08

var p: PlayerSim
var flip: Node3D
var body: Node3D
var hips: Node3D
var spine: Node3D
var head: Node3D
var arm_n := {}
var arm_f := {}
var leg_n := {}
var leg_f := {}
var mats := {}
var ex := {}
var ring: MeshInstance3D
var cur := {}
var phase := 0.0
var turn := 1.0
var stretch := 0.0
var was_ground := true
var was_crouch := false
var last_vy := 0.0
var last_rocket_t := 0
var hard := 0.0
var prev_vx := 0.0
var module_tint := ""
var _keys := {}

func setup(player: PlayerSim) -> void:
	p = player
	turn = p.facing
	_build()

# ---- The rig (rigs.js buildPlayerRig, Nova) ----
func _limb(parent: Node3D, upper: float, lower: float, r: float, z: float) -> Dictionary:
	var top := Rig.grp(parent, 0, 0, z)
	Rig.part(top, Rig.cap(r, upper - r), mats.under, 0, -upper / 2)
	var jnt := Rig.grp(top, 0, -upper, 0)
	Rig.part(jnt, Rig.cap(r * 0.92, lower - r), mats.under, 0, -lower / 2)
	var end := Rig.grp(jnt, 0, -lower, 0)
	return { "top": top, "joint": jnt, "end": end }

func _build() -> void:
	var c: Dictionary = Tune.C.CHARS[p.char]
	mats = {
		"base": Rig.std(c.base, 0.36), "trim": Rig.std(c.trim, 0.42, 0.18), "under": Rig.std(c.under, 0.72),
		"energy": Rig.glow(c.energy, 2.4), "visor": Rig.std("0b1018", 0.12, 0.7),
	}
	var M := mats
	flip = Rig.grp(self)
	body = Rig.grp(flip)
	hips = Rig.grp(body, 0, 0.95, 0)
	Rig.part(hips, Rig.rbox(0.34, 0.2, 0.38), M.trim, 0, 0.02)
	spine = Rig.grp(hips, 0, 0.08, 0)
	Rig.part(spine, Rig.rbox(0.3, 0.3, 0.34), M.under, 0, 0.18)
	Rig.part(spine, Rig.rbox(0.38, 0.34, 0.5), M.base, 0.02, 0.42)
	Rig.part(spine, Rig.rbox(0.05, 0.05, 0.35), M.energy, 0.2, 0.44)
	for z in [0.28, -0.28]:
		Rig.part(spine, Rig.rbox(0.26, 0.16, 0.2), M.base, 0, 0.55, z * 1.05)
	head = Rig.grp(spine, 0, 0.66, 0)
	Rig.part(head, Rig.sphere(0.15, 20), M.base, 0, 0.1)
	Rig.part(head, Rig.rbox(0.2, 0.08, 0.28), M.visor, 0.07, 0.11)
	Rig.part(head, Rig.rbox(0.04, 0.02, 0.26), M.energy, 0.17, 0.11)
	Rig.part(head, Rig.rbox(0.18, 0.05, 0.1), M.trim, -0.05, 0.25)
	arm_n = _limb(spine, 0.3, 0.29, 0.065, 0.3)
	arm_f = _limb(spine, 0.3, 0.29, 0.065, -0.3)
	arm_n.top.position.y = 0.53; arm_f.top.position.y = 0.53
	for a in [arm_n, arm_f]:
		Rig.part(a.end, Rig.sphere(0.07, 12), M.under, 0, -0.03)
	leg_n = _limb(hips, 0.46, 0.46, 0.085, 0.13)
	leg_f = _limb(hips, 0.46, 0.46, 0.085, -0.13)
	for l in [leg_n, leg_f]:
		Rig.part(l.top, Rig.rbox(0.2, 0.26, 0.2), M.base, 0.02, -0.18)
		Rig.part(l.joint, Rig.rbox(0.19, 0.3, 0.18), M.base, 0.03, -0.24)
		Rig.part(l.end, Rig.rbox(0.26, 0.1, 0.16), M.trim, 0.06, -0.02)
	# Marksman kit: skate blades (a fine gold line along each sole), light-booster jets under the boots
	ex.jets = []; ex.blades = []
	var jet_mat := Rig.clear("fff1c9", 0.85, 3.0)
	for l in [leg_n, leg_f]:
		var j := Rig.part(l.end, Rig.cone(0.07, 0.34, 10), jet_mat, 0.04, -0.26, 0)
		j.rotation.z = PI; j.visible = false; j.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		ex.jets.append(j)
		for z in [0.083, -0.083]:
			ex.blades.append(Rig.part(l.end, Rig.rbox(0.28, 0.018, 0.01), Rig.glow(c.energy, 1.6), 0.06, -0.066, z))
	# The Sentinel Bracer on the near forearm, with the loaded attachment tinted as on the HUD
	var bracer := Rig.grp(arm_n.joint, 0, -0.14, 0)
	Rig.part(bracer, Rig.rbox(0.16, 0.34, 0.2), M.base, 0.02, 0)
	Rig.part(bracer, Rig.rbox(0.05, 0.3, 0.14), M.trim, 0.1, 0)
	Rig.part(bracer, Rig.rbox(0.03, 0.22, 0.03), M.energy, 0.12, -0.02, 0.06)
	ex.muzzle = Rig.part(bracer, Rig.cyl(0.045, 0.06, 0.08, 12), M.energy, 0.0, -0.2, 0)
	ex.module_mat = Rig.glow(Tune.C.ATTACH_LOOK.lance.tint, 2.2)
	ex.module = Rig.part(bracer, Rig.rbox(0.07, 0.1, 0.09), ex.module_mat, 0.11, 0.09, 0)
	# Close range: hard light forms a faceted gauntlet over each fist, and a greave over the lead boot
	ex.hard_mat = Rig.clear("fff4d6", 0.82, 2.6)
	ex.hard_mat.emission = Color(c.energy)
	ex.gauntlets = []
	for a in [arm_n, arm_f]:
		var g := Rig.part(a.end, Rig.sphere(0.13, 6), ex.hard_mat, 0.02, -0.05, 0)
		g.visible = false; ex.gauntlets.append(g)
	ex.greave = Rig.part(leg_n.end, Rig.sphere(0.15, 6), ex.hard_mat, 0.08, -0.03, 0)
	ex.greave.visible = false
	Rig.part(spine, Rig.rbox(0.14, 0.3, 0.3), M.trim, -0.22, 0.4)   # back pack
	# The player's ring at the feet, in their colour
	ring = MeshInstance3D.new()
	var rm := Rig.torus(0.485, 0.065, 32)
	ring.mesh = rm
	ring.material_override = Rig.clear(PLAYER_COLORS[p.slot % 4], 0.65, 1.2)
	ring.position.y = 0.03
	ring.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(ring)
	for j in J:
		cur[j] = REST[j]

# ---- Attack keyframes (anim.js KEYS): [[tick, pose, snap], ...] over the move's base stance ----
func _keys_for(id: String) -> Array:
	if _keys.has(id):
		return _keys[id]
	var m: Dictionary = Tune.C.MOVES[id]
	var su: float = m.su
	var ac: float = m.ac
	var end: float = m.su + m.ac + m.rc
	var raw := []
	match id:
		"nova_k1", "nova_jab1":
			raw = [[0, { "spine": 0.05, "twist": -0.4, "shN": -0.4, "elN": 1.7, "hipY": 0.9 }], [su, { "spine": 0.32, "twist": 0.45, "shN": 1.75, "elN": 0.1, "hipN": 0.65, "knN": -0.7, "hipF": -0.55, "hipY": 0.86 }, true],
				[su + ac + 3, { "spine": 0.28, "twist": 0.3, "shN": 1.45, "elN": 0.35, "hipN": 0.6, "knN": -0.65, "hipF": -0.5 }], [end, {}]]
		"nova_k2", "nova_jab2":
			raw = [[0, { "twist": 0.4, "shF": -0.35, "elF": 2.2, "shN": 0.9, "elN": 1.4 }], [su, { "spine": 0.38, "twist": -0.5, "shF": 1.45, "elF": 1.65, "shN": -0.3, "elN": 1.4, "hipN": 0.7, "knN": -0.75, "hipF": -0.55, "hipY": 0.86 }, true],
				[su + ac + 3, { "spine": 0.3, "twist": -0.35, "shF": 1.2, "elF": 1.4, "shN": 0.2, "elN": 1.3 }], [end, {}]]
		"nova_kair", "nova_air":
			raw = [[0, _with(AIR, { "spine": -0.1, "hipN": 2.1, "knN": -0.35, "shN": 0.9, "shF": 1.3 })], [su, _with(AIR, { "spine": -0.25, "hipN": 2.4, "knN": -0.15, "shN": 1.2, "shF": 1.6 })],
				[su + 2, _with(AIR, { "spine": 0.45, "hipN": 0.05, "knN": -0.1, "shN": 0.4, "shF": 0.6, "bodyZ": -0.2 }), true], [end, AIR.duplicate()]]
		"nova_rise":
			raw = [[0, { "hipY": 0.72, "spine": 0.42, "twist": -0.45, "hipN": 1.25, "knN": -1.9, "hipF": 0.2, "knF": -1.8, "shN": -0.45, "elN": 1.95, "shF": 0.7, "elF": 1.6 }],
				[su, { "hipY": 0.95, "spine": -0.2, "twist": 0.4, "shN": 3.05, "elN": 0.04, "shF": -0.45, "elF": 1.25, "hipN": 0.35, "knN": -0.45, "hipF": -0.25, "knF": -1.15, "head": 0.35 }, true],
				[su + ac, { "hipY": 0.95, "spine": -0.12, "twist": 0.3, "shN": 2.95, "elN": 0.1, "shF": -0.25, "elF": 1.3, "hipN": 0.6, "knN": -1.0, "hipF": 0.1, "knF": -1.1, "head": 0.25 }],
				[end, AIR.duplicate()]]
		_:   # nova_k3, the shove and the brace: the blast punch
			raw = [[0, { "spine": 0.1 }], [su - 1, { "spine": -0.18, "twist": -0.65, "shN": -0.95, "elN": 1.9, "shF": 0.9, "elF": 1.2, "hipN": 0.2, "knN": -0.95, "hipF": -0.85, "knF": -0.2, "hipY": 0.8 }],
				[su + 1, { "spine": 0.48, "twist": 0.6, "shN": 1.62, "elN": 0.0, "shF": -0.7, "elF": 1.1, "hipN": 0.95, "knN": -0.72, "hipF": -0.8, "knF": -0.12, "hipY": 0.8 }, true],
				[su + ac + 4, { "spine": 0.4, "twist": 0.45, "shN": 1.5, "elN": 0.15, "shF": -0.5, "hipN": 0.9, "knN": -0.7, "hipF": -0.75, "hipY": 0.82 }], [end, {}]]
	var base := _with(FIGHT, AIR) if m.get("air") else FIGHT
	var ks := []
	for r in raw:
		ks.append({ "t": float(r[0]), "pose": _with(base, r[1]), "snap": r.size() > 2 and r[2] })
	_keys[id] = ks
	return ks

static func _with(a: Dictionary, b: Dictionary) -> Dictionary:
	var o := a.duplicate()
	o.merge(b, true)
	return o

static func _ease(k: float) -> float:
	return k * k * (3 - 2 * k)

static func _snap(k: float) -> float:
	return 1 - pow(1 - k, 3)

func _attack_pose(P: Dictionary) -> void:
	var ks := _keys_for(p.move_id)
	var u := float(p.st)
	if u <= ks[0].t:
		P.merge(ks[0].pose, true)
		return
	for i in range(1, ks.size()):
		var a: Dictionary = ks[i - 1]
		var b: Dictionary = ks[i]
		if u <= b.t:
			var f: float = (u - a.t) / maxf(1e-6, b.t - a.t)
			var k := _snap(f) if b.snap else _ease(f)
			for j in J:
				P[j] = a.pose.get(j, REST[j]) + (b.pose.get(j, REST[j]) - a.pose.get(j, REST[j])) * k
			return
	P.merge(ks[ks.size() - 1].pose, true)

# ---- Each frame ----
func animate(alpha: float, dt: float, t: float) -> void:
	var x := lerpf(p.prev_x, p.x, alpha)
	var y := lerpf(p.prev_y, p.y, alpha)
	position = PathFrame.point(x, y)
	# Turning round: the rig swings through the turn over TURN_TIME with a twist of the body
	var was := turn
	turn += clampf(p.facing - turn, -dt * 2 / TURN_TIME, dt * 2 / TURN_TIME)
	var tw := 1 - absf(turn)
	var spin := signf(turn - was)
	rotation.y = PathFrame.yaw(x) - spin * tw * 0.9
	flip.scale.x = (signf(turn) if turn != 0 else float(p.facing)) * 0.12 if absf(turn) < 0.12 else turn
	_pose(dt, t)
	var phasing: bool = p.state == "dodge" and p.dodge != null and p.dodge.t <= 11 and int(floor(t * 30)) % 3 == 0
	var blink := p.mercy > 0 and p.state != "downed" and p.state != "ult" and int(floor(t * 14)) % 2 == 0
	flip.visible = p.state != "dead" and not phasing and not blink
	ring.visible = p.state != "downed" and p.state != "dead"

func _pose(dt: float, t: float) -> void:
	var P := REST.duplicate()
	var speed := absf(p.vx)
	var st := p.state
	var mk := p.marksman()
	var aim_ang := atan2(p.aim_y, 1e-3 if absf(p.aim_x) < 1e-3 else p.aim_x * p.facing)
	var rate := 18.0
	var breathe := sin(t * 2.2)
	var DC: Dictionary = Tune.C.DASH_CHARGE
	if st == "downed" or st == "dead":
		P.merge({ "bodyZ": 1.45, "hipY": 0.2, "shN": 2.6, "shF": 2.2, "hipN": 0.2, "hipF": -0.1 }, true); rate = 10
	elif st == "slide":
		P.merge({ "hipY": 0.46, "spine": -0.45, "hipN": 1.38, "knN": -0.08, "hipF": -0.38, "knF": -1.95, "shN": 0.95, "elN": 0.7, "shF": -0.55, "elF": 0.2, "bodyZ": -0.06, "head": 0.3 }, true)
		if mk:
			P.merge({ "hipY": 0.52, "spine": 0.1, "hipN": 1.05, "knN": -1.0, "hipF": -0.9, "knF": -0.35, "shN": -0.8, "shF": -1.1, "elN": 0.3, "elF": 0.3, "head": -0.05 }, true)
		rate = 30
	elif st == "dashCharge":
		var f := minf(1, p.dash_charge_t / DC.charge[0])
		P.merge({ "hipY": 0.95 - 0.3 * f, "spine": 0.1 + 0.45 * f, "hipN": 0.4 + 0.9 * f, "knN": -0.3 - 1.5 * f, "hipF": -0.2 - 0.45 * f, "knF": -0.2 - 0.5 * f,
			"shN": -0.4 - 0.7 * f, "elN": 0.5, "shF": -0.6 - 0.7 * f, "elF": 0.5, "head": -0.25 * f }, true)
		if p.dash_charge_t >= DC.charge[1]:
			P.hipY += randf_range(-0.5, 0.5) * (0.035 if p.dash_charge_t >= DC.charge[2] else 0.018)
		rate = 22
	elif st == "dash":
		P.merge({ "spine": 0.58, "hipN": -0.35, "knN": -0.95, "hipF": -0.95, "knF": -0.45, "shN": -1.05, "shF": -1.25, "elN": 0.25, "elF": 0.3, "head": -0.3 }, true)
		P.bodyZ = atan2(p.dash.dy if p.dash != null else 0.0, 1) * 0.8; rate = 34
	elif st == "pound" and p.pound != null:
		var S: Dictionary = p.pound
		if S.phase == "hold":
			var k := minf(1, S.t / 6.0)
			P.merge({ "hipY": 0.95, "spine": -0.18 * k, "hipN": 1.55, "knN": -2.2, "hipF": 1.25, "knF": -2.05, "head": 0.18, "shN": 2.75, "elN": 1.35, "shF": 1.2, "elF": 1.5 }, true)
			if S.level:
				var jj: float = randf_range(-0.5, 0.5) * 0.025 * S.level
				P.spine += jj; P.hipY += jj
			rate = 26
		elif S.phase == "drop":
			P.merge({ "spine": 0.18, "bodyZ": 0.0, "hipN": 0.9, "knN": -1.6, "hipF": -0.15, "knF": -0.35, "head": -0.5, "hipY": 0.95, "shN": -0.05, "elN": 0.02, "shF": 1.1, "elF": 1.2 }, true)
			rate = 42
		else:
			var up := clampf((S.t - 5) / 7.0, 0, 1)
			P.merge({ "hipY": 0.5 + 0.3 * up, "spine": 0.72 - 0.4 * up, "hipN": 1.55 - 0.8 * up, "knN": -2.3 + 1.3 * up, "hipF": -0.25, "knF": -2.1 + 1.4 * up,
				"shN": 0.45, "elN": 0.05, "shF": -0.65, "elF": 0.45, "head": 0.1 }, true)
			rate = 40
	elif st == "vb":
		var f := minf(1, p.st / 3.0)
		P.merge({ "spine": 0.35 * f, "hipN": 0.75, "knN": -0.65, "hipF": -0.65, "knF": -0.3, "hipY": 0.82, "twist": 0.4 * f, "shN": 1.57, "elN": 0.05, "shF": 0.9, "elF": 1.2 }, true)
		rate = 40
	elif st == "attack" and p.move != null:
		_attack_pose(P); rate = 48
	elif st == "dodge" and p.dodge != null:
		var back: bool = p.dodge.dx * p.facing < 0
		var u := minf(1, p.dodge.t / 4.0)
		if back:
			P.merge({ "spine": -0.3 * u, "hipN": 0.75, "knN": -1.35, "hipF": -0.35, "knF": -1.0, "shN": 1.25, "elN": 1.7, "shF": 0.35, "elF": 1.3, "hipY": 0.84, "head": 0.2, "bodyZ": 0.1 * u }, true)
		else:
			P.merge({ "spine": 0.5 * u, "hipN": 1.05, "knN": -1.45, "hipF": -0.65, "knF": -0.55, "shN": -0.7, "shF": -0.9, "elN": 0.35, "elF": 0.3, "hipY": 0.78, "head": -0.2, "bodyZ": -0.12 * u, "twist": 0.3 * u }, true)
		rate = 34
	elif st == "ult":
		# Supernova: arms flung wide while it is called; both hands raised to the gathering light; braced behind
		# the beam; arms thrown open by the nova
		var R = p.ult_run
		var N: Dictionary = Tune.C.ULT.nova
		if R == null:
			P.merge({ "spine": -0.32, "shN": 2.35, "elN": 0.25, "shF": 2.2, "elF": 0.3, "hipN": 0.3, "knN": -0.45, "hipF": -0.3, "knF": -0.35, "hipY": 0.92, "head": 0.45 }, true)
		elif R.t <= N.gather:
			P.merge({ "spine": -0.22, "shN": 3.0, "elN": 0.25, "shF": 2.9, "elF": 0.3, "hipN": 0.55, "knN": -0.95, "hipF": 0.2, "knF": -0.8, "hipY": 0.95, "head": 0.4 }, true)
		elif R.segs != null:
			var a := atan2(R.dy, 1e-3 if absf(R.dx) < 1e-3 else R.dx * p.facing)
			P.merge({ "spine": -0.18, "shN": a + PI / 2, "elN": 0.02, "shF": a + PI / 2 - 0.15, "elF": 0.1, "hipN": 0.7, "knN": -1.0, "hipF": -0.5, "knF": -0.6, "hipY": 0.95, "head": -0.1 }, true)
		else:
			P.merge({ "spine": -0.35, "shN": 2.2, "elN": 0.1, "shF": 2.0, "elF": 0.1, "hipN": 0.4, "knN": -0.7, "hipF": -0.2, "knF": -0.5, "hipY": 0.95, "head": 0.3 }, true)
		rate = 26
	elif st == "parry":
		P.merge({ "shN": 1.3, "elN": 1.7, "shF": 1.1, "elF": 1.8, "spine": -0.08, "hipN": 0.3, "knN": -0.4, "hipY": 0.9 }, true); rate = 40
	elif st == "hitstun":
		P.merge({ "spine": -0.5, "head": 0.35, "shN": 1.0, "shF": 1.4, "elN": 0.7, "elF": 0.4, "hipN": 0.35, "knN": -0.55, "hipF": -0.1, "twist": -0.3 }, true); rate = 30
	elif st == "beam":
		P.merge({ "spine": -0.12, "shN": aim_ang + PI / 2 - 0.12, "elN": 0.02, "shF": aim_ang + PI / 2 - 0.3, "elF": 0.7, "hipN": 0.85, "knN": -0.95, "hipF": -0.65, "knF": -0.35, "hipY": 0.8, "head": -0.1 }, true)
		if not p.on_ground:
			P.merge({ "hipN": 0.6, "knN": -0.9, "hipF": -0.2, "knF": -0.7, "hipY": 0.95 }, true)
		rate = 30
	elif not p.on_ground:
		if p.wall_sliding:
			P.merge({ "shF": -2.2, "elF": 0.35, "shN": 0.55, "elN": 0.9, "hipN": 0.75, "knN": -1.25, "hipF": -0.35, "knF": -0.45, "spine": -0.12, "head": 0.1 }, true)
		elif p.rocket_t > 0 and p.vy > 4:
			P.merge({ "hipN": 0.45, "knN": -0.95, "hipF": -0.25, "knF": -0.6, "shN": -0.55, "shF": -0.75, "elN": 0.2, "elF": 0.2, "spine": -0.08, "head": 0.15 }, true)
		elif p.vy > 3:
			P.merge({ "hipN": 0.95, "knN": -1.45, "hipF": 0.2, "knF": -0.85, "shN": 1.7, "shF": 1.25, "elN": 0.5, "spine": 0.12, "head": 0.05 }, true)
		elif p.vy > -3:
			P.merge({ "hipN": 0.7, "knN": -1.2, "hipF": 0.35, "knF": -1.1, "shN": 1.2, "shF": 1.4, "elN": 0.6, "elF": 0.6, "spine": 0.08 }, true)
		else:
			P.merge({ "hipN": 0.3, "knN": -0.4, "hipF": -0.3, "knF": -0.75, "shN": 1.05, "shF": 0.85, "elN": 0.5, "elF": 0.4, "spine": 0.04, "head": -0.1 }, true)
		rate = 16
	elif p.crouch:
		var walk := speed > 0.3
		if walk:
			phase += dt * speed * 3.2
		var s := sin(phase) if walk else 0.0
		P.merge({ "hipY": 0.56 + breathe * 0.012, "spine": 0.42 + breathe * 0.02, "hipN": 1.25 + s * 0.25, "knN": -2.0 - s * 0.15, "hipF": 0.25 - s * 0.25, "knF": -2.3 + s * 0.15,
			"shN": 1.1, "elN": 1.6, "shF": -0.4, "elF": 0.6, "head": -0.25, "twist": -0.12 }, true)
		rate = 20
	elif speed > 0.6:
		var back := p.vx * p.facing < 0
		var amp := minf(1, speed / 7)
		if mk:
			# Skate stride: long, low pushes with arms swinging wide; at full glide both feet come together
			phase += dt * speed * 0.95 * (-1.0 if back else 1.0)
			var s := sin(phase)
			var c := cos(phase)
			var coast := maxf(0, 1 - absf(p.vx - prev_vx) * 20) * (1.0 if speed > 7 else 0.0)
			P.merge({ "hipN": 0.25 + s * 0.35 * amp, "knN": -0.75 - maxf(0, c) * 0.4, "hipF": -0.3 - s * 0.45 * amp, "knF": -0.55 - maxf(0, -c) * 0.3,
				"shN": -s * 0.9 * amp + 0.1, "shF": s * 0.9 * amp - 0.1, "elN": 0.5, "elF": 0.5, "spine": 0.36 * amp, "hipY": 0.84 - absf(c) * 0.03, "twist": s * 0.18 * amp, "head": -0.2 * amp }, true)
			if coast > 0.5:
				P.merge({ "hipN": 0.35, "knN": -0.95, "hipF": 0.1, "knF": -0.9, "shN": -0.6, "shF": -0.8, "spine": 0.42, "hipY": 0.8 }, true)
		else:
			phase += dt * speed * 1.8 * (-1.0 if back else 1.0)
			var s := sin(phase)
			var c := cos(phase)
			P.merge({ "hipN": s * 0.95 * amp + 0.05, "hipF": -s * 0.95 * amp + 0.05, "knN": -maxf(0, -c) * 1.45 * amp - 0.15, "knF": -maxf(0, c) * 1.45 * amp - 0.15,
				"shN": -s * 0.95 * amp, "shF": s * 0.95 * amp, "elN": 1.15, "elF": 1.15, "spine": (0.05 if back else 0.3) * amp, "hipY": 0.95 - absf(c) * 0.07, "twist": -s * 0.15 * amp, "head": -0.2 * amp }, true)
		rate = 22
	else:
		P.merge({ "spine": 0.06 + breathe * 0.015, "hipN": 0.12, "knN": -0.15, "hipF": -0.1, "knF": -0.12, "hipY": 0.94 + breathe * 0.005, "shN": 0.3, "elN": 0.7, "shF": -0.05, "elF": 0.4 }, true)
		rate = 10
	prev_vx = p.vx
	# Aiming layer: the bracer arm follows the aim while shooting
	var shooting := (p.charge_t > 0 or p.fire_cd > 0 or p.shoot_t > 0 or (p.aim_free and p.char == "nova")) and st in ["normal", "dash", "slide"]
	if shooting:
		P.shN = aim_ang + PI / 2 + P.spine; P.elN = 0.02
	P.head += -P.spine * 0.45   # the head counters the spine so the gaze stays level
	var a := 1 - exp(-rate * dt)
	for j in J:
		cur[j] += (P[j] - cur[j]) * a
	# Squash and stretch: a stretch on launches and jumps, a squash on every landing (bigger the harder)
	if p.rocket_t > last_rocket_t:
		stretch = 0.12 + 0.14 * p.rocket_pow
	if not p.on_ground and was_ground and p.vy > 8:
		stretch = maxf(stretch, 0.07)
	if p.on_ground and not was_ground:
		stretch = -minf(0.16, maxf(0.03, (-last_vy - 4) * 0.008))
	if p.crouch and not was_crouch and p.on_ground:
		stretch = minf(stretch, -0.07)
	last_rocket_t = p.rocket_t; was_ground = p.on_ground; was_crouch = p.crouch
	if not p.on_ground:
		last_vy = p.vy
	stretch *= exp(-dt * 10)
	var sy := 1 + stretch
	var sxz := 1 / sqrt(sy)
	body.scale = Vector3(sxz, sy, sxz)
	spine.rotation = Vector3(0, cur.twist, -cur.spine)
	head.rotation.z = -cur.head
	arm_n.top.rotation.z = cur.shN; arm_n.joint.rotation.z = cur.elN
	arm_f.top.rotation.z = cur.shF; arm_f.joint.rotation.z = cur.elF
	leg_n.top.rotation.z = cur.hipN; leg_n.joint.rotation.z = cur.knN
	leg_f.top.rotation.z = cur.hipF; leg_f.joint.rotation.z = cur.knF
	hips.position.y = cur.hipY
	body.rotation.z = cur.bodyZ
	body.position.y = 0.1 if st == "downed" or st == "dead" else 0.0
	_suit(t, dt, mk)

# Energy lines brighten with each charge level, flash in the Perfect window, blaze at Level 4 and while the
# beam fires, and glow gold-white with Overcharge; jets, the attachment tint and the hard-light gauntlets
func _suit(t: float, dt: float, mk: bool) -> void:
	var LV := { "L1": 1, "L2": 2, "L3": 3, "perfect": 3, "L4": 4 }
	var stage := p.charge_stage()
	var bstage := p.burst_stage() if mk else ""
	var DC: Dictionary = Tune.C.DASH_CHARGE
	var dash_lv := 0
	if p.state == "dashCharge":
		dash_lv = 3 if p.dash_charge_t >= DC.charge[2] else (2 if p.dash_charge_t >= DC.charge[1] else (1 if p.dash_charge_t >= DC.charge[0] else 0))
	var charge: int = maxi(maxi(LV.get(stage, 0), LV.get(bstage, 0)), maxi(dash_lv, 4 if p.state == "beam" else (int(p.pound.level) if p.state == "pound" and p.pound != null else 0)))
	var flash := stage == "perfect" or bstage == "perfect"
	var pulsing := p.charge_t > 0 or p.burst_t > 0 or p.dash_charge_t > 0 or p.state == "beam"
	mats.energy.emission_energy_multiplier = 2.2 + charge * 1.2 + (sin(t * 30) * 0.4 if pulsing else 0.0) + (2.5 if flash else 0.0) \
		+ (1.2 + sin(t * 12) * 0.5 if p.overcharge > 0 else 0.0) + (4 + sin(t * 36) * 0.8 if p.state == "ult" else 0.0)
	for j in ex.jets:
		j.visible = p.thrusting
		j.scale = Vector3(1, 0.8 + randf() * 0.5, 1)
	ex.module.visible = mk
	for b in ex.blades:
		b.visible = mk
	if mk and module_tint != p.attachment:
		module_tint = p.attachment
		var c := Color(Tune.C.ATTACH_LOOK[p.attachment].tint)
		ex.module_mat.albedo_color = c; ex.module_mat.emission = c
	var pound: bool = p.state == "pound" and p.pound != null
	var fist: bool = (p.state == "attack" and p.move != null and p.move.get("fist", false)) or pound
	var kick: bool = fist and not pound and p.move_id == "nova_kair"
	hard += ((1.0 if fist else 0.0) - hard) * (1 - exp(-dt * (34 if fist else 10)))
	var strike := false
	if pound:
		strike = p.pound.phase != "hold" or p.pound.level > 0
	elif fist:
		strike = p.st >= p.move.su and p.st < p.move.su + p.move.ac + 2
	var hs := hard * (1.25 if strike else 1.0)
	for g in ex.gauntlets:
		g.visible = hard > 0.04 and not kick
		g.scale = Vector3(1.1, 1.3, 1) * maxf(0.01, hs)
	ex.greave.visible = hard > 0.04 and kick
	ex.greave.scale = Vector3(1.7, 0.9, 1) * maxf(0.01, hs)
	ex.hard_mat.emission_energy_multiplier = 5.5 if strike else 2.6
	ex.hard_mat.albedo_color.a = 0.35 + 0.5 * hard
