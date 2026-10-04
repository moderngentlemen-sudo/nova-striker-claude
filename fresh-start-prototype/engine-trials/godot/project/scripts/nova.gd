# Nova's movement, ported from player.js: run, jump (buffered, with coyote time and a cut on release), double
# jump, wall slide and wall jump, dash (eight directions, dash-jump), slide, crouch, drop through one-way
# platforms, the foundry's lift pads, and a melee swing that breaks crates. The numbers come from config.js
# (through level.json).
#
# The body is Godot's own CharacterBody2D: move_and_slide does the collision, one-way platforms are a
# collision-shape flag. The 2D world is in centimetres (S px per metre, y down); this script thinks in metres
# with y up, like the original, and converts at the edges.
extends CharacterBody2D

signal event(type: String, data: Dictionary)

const S := 100.0
const DT := 1.0 / 60.0

var C: Dictionary          # Nova's numbers (level.json nova)
var K: Dictionary          # physics constants (level.json physics)
var lifts: Array = []

var vx := 0.0
var vy := 0.0
var facing := 1
var state := "normal"
var st := 0
var on_ground := false
var crouch := false
var h := 1.72
var coyote := 0
var jumps_used := 0
var air_dashes := 1
var fast_fall := false
var dash_carry := false
var dash_cd := 0
var post_dash := 99
var control_lock := 0
var wall_dir := 0
var last_wall_dir := 0
var wall_coyote := 0
var wall_sliding := false
var wall_t := 0
var wall_stick := 0
var buf_jump := 99
var buf_dash := 99
var buf_melee := 99
var pad_cd := 0
var swing_t := 0
var dash := {}
var last_safe := Vector2.ZERO
var prev_pos := Vector2.ZERO     # metres, y up: the last two ticks, for interpolated drawing
var cur_pos := Vector2.ZERO
var shape: RectangleShape2D
var col: CollisionShape2D
var mx := 0.0
var my := 0.0

func setup(data: Dictionary, spawn: Vector2) -> void:
	C = data.nova
	K = data.physics
	lifts = data.lifts
	h = C.height
	shape = RectangleShape2D.new()
	col = CollisionShape2D.new()
	col.shape = shape
	add_child(col)
	_set_height(h)
	safe_margin = 0.5
	floor_snap_length = 8.0
	floor_max_angle = deg_to_rad(50)
	place(spawn)

func place(p: Vector2) -> void:
	position = Vector2(p.x * S, -p.y * S)
	vx = 0; vy = 0
	cur_pos = p; prev_pos = p; last_safe = p

func _set_height(nh: float) -> void:
	h = nh
	shape.size = Vector2(C.width * S, nh * S)
	col.position = Vector2(0, -nh * S / 2)   # the node sits at the feet

func feet() -> Vector2:
	return Vector2(position.x / S, -position.y / S)

static func approach(v: float, t: float, d: float) -> float:
	return minf(v + d, t) if v < t else maxf(v - d, t)

func _snap8() -> Vector2:
	if absf(mx) < 0.3 and absf(my) < 0.3:
		return Vector2.ZERO
	var a := snappedf(atan2(my, mx), PI / 4)
	return Vector2(roundf(cos(a) * 1000) / 1000, roundf(sin(a) * 1000) / 1000).normalized()

func _set_state(s: String) -> void:
	state = s
	st = 0

func _physics_process(_delta: float) -> void:
	prev_pos = cur_pos
	mx = Input.get_axis("left", "right")
	my = Input.get_axis("down", "up")
	buf_jump = 0 if Input.is_action_just_pressed("jump") else buf_jump + 1
	buf_dash = 0 if Input.is_action_just_pressed("dash") else buf_dash + 1
	buf_melee = 0 if Input.is_action_just_pressed("melee") else buf_melee + 1
	st += 1
	for n in ["dash_cd", "control_lock", "pad_cd", "swing_t"]:
		set(n, maxi(0, get(n) - 1))
	post_dash += 1
	wall_dir = _touching_wall()
	var was_sliding := wall_sliding
	wall_sliding = false

	match state:
		"normal": _state_normal()
		"dash": _state_dash()
		"slide": _state_slide()

	var low := crouch or state == "slide"
	var want_h: float = C.crouchH if low else C.height
	if want_h != h and state != "dash":
		_set_height(want_h)
	if wall_sliding != was_sliding:
		event.emit("wallSlide", { "on": wall_sliding })

	var was_ground := on_ground
	var fall_v := vy
	velocity = Vector2(vx * S, -vy * S)
	move_and_slide()
	vx = velocity.x / S
	vy = -velocity.y / S
	on_ground = is_on_floor()
	if on_ground:
		coyote = K.COYOTE; jumps_used = 0; air_dashes = 1; fast_fall = false; dash_carry = false
		wall_coyote = 0
		if not was_ground and st > 1:
			event.emit("land", { "vy": fall_v })
		last_safe = feet()
	else:
		coyote = maxi(0, coyote - 1)
		wall_coyote = maxi(0, wall_coyote - 1)
	if wall_dir != 0 and not on_ground:
		air_dashes = 1; jumps_used = 0; last_wall_dir = wall_dir; wall_coyote = K.WALL.coyote
	_lift_pads()
	if feet().y < -10.0:
		place(last_safe + Vector2(0, 0.5))
		event.emit("respawn", {})
	cur_pos = feet()

# A wall within 2 cm either side (move_and_slide only reports walls it was pushed into)
func _touching_wall() -> int:
	if on_ground and state != "normal":
		return 0
	if test_move(global_transform, Vector2(2, 0)):
		return 1
	if test_move(global_transform, Vector2(-2, 0)):
		return -1
	return 0

func _gravity(mult: float = 1.0) -> void:
	if on_ground and vy <= 0:
		vy = -0.5
		return
	var g: float = K.GRAVITY * mult
	if vy < 0:
		g *= K.FALL_MULT
	elif not Input.is_action_pressed("jump") and not dash_carry:
		g *= K.RISE_CUT_MULT
	vy -= g * DT
	var cap: float = K.FAST_FALL if fast_fall else K.MAX_FALL
	vy = maxf(vy, -cap)

func _horizontal() -> void:
	var tgt: float = mx * C.run * (0.4 if crouch else 1.0)
	if absf(mx) > 0.1 and control_lock == 0:
		facing = signi(int(signf(mx)))
	if control_lock > 0:
		return
	if on_ground:
		var accel: float = C.accelG if (tgt != 0 and signf(tgt) == signf(vx)) or absf(vx) < 0.1 else C.decelG
		vx = approach(vx, tgt, accel * DT)
	else:
		if dash_carry and absf(vx) > absf(tgt) and signf(tgt) != -signf(vx):
			return
		vx = approach(vx, tgt, C.accelA * DT)

func _wall_cling() -> void:
	var W: Dictionary = K.WALL
	var was := wall_t > 0 or wall_sliding
	var on := false
	if not on_ground and wall_dir != 0 and vy <= 0.5:
		var toward := mx * wall_dir > 0.3
		if toward:
			wall_stick = W.stick
		elif wall_stick > 0:
			wall_stick -= 1
		on = toward or wall_stick > 0
	if not on:
		wall_t = 0
		return
	wall_t += 1
	var ramp := clampf((wall_t - float(W.grip)) / float(W.ease), 0, 1)
	var target: float = C.wall.slide * W.fast if my < -0.6 else W.gripSpeed + (C.wall.slide - W.gripSpeed) * ramp
	if vy < -target:
		vy = minf(-target, vy + (W.brake + K.GRAVITY * K.FALL_MULT) * DT)
	else:
		vy = maxf(vy, -target)
	if mx * wall_dir <= 0.3:
		vx = wall_dir * 0.5
	wall_sliding = true
	fast_fall = false
	facing = -wall_dir

func _state_normal() -> void:
	if on_ground and my < -0.55:
		crouch = true
	elif crouch and not test_move(global_transform, Vector2(0, -(C.height - h) * S)):
		crouch = false
	_horizontal()
	if not on_ground and my < -0.7 and vy < 3 and wall_dir == 0:
		fast_fall = true
	_gravity()
	_wall_cling()
	if _try_dash(): return
	if _try_jump(): return
	if buf_melee <= 8 and swing_t == 0:
		buf_melee = 99
		_swing()

func _try_jump() -> bool:
	if buf_jump > K.JUMP_BUFFER:
		return false
	if on_ground and crouch and my < -0.6:
		var floor_body := _floor_body()
		if floor_body and floor_body.has_meta("oneway"):
			position.y += 6     # drop through the platform
			buf_jump = 99; on_ground = false
			return true
	if on_ground or coyote > 0:
		vy = C.jumpV; coyote = 0; on_ground = false; crouch = false
		if post_dash <= 8:
			dash_carry = true
		buf_jump = 99; _set_state("normal")
		event.emit("jump", {})
		return true
	var wd := wall_dir if wall_dir != 0 else (last_wall_dir if wall_coyote > 0 else 0)
	if wd != 0:
		var W: Dictionary = K.WALL
		var away := mx * wd < -0.3
		vx = -wd * (C.wall.jumpVx if away else W.climb.vx)
		vy = C.wall.jumpVy * (W.leapVy if away else 1.0)
		control_lock = C.wall.lock if away else W.climb.lock
		facing = -wd; wall_coyote = 0; wall_stick = 0; wall_sliding = false
		buf_jump = 99; fast_fall = false; _set_state("normal")
		event.emit("walljump", {})
		return true
	if jumps_used < 1:
		vy = C.dblV; jumps_used = 1; fast_fall = false
		buf_jump = 99; _set_state("normal")
		event.emit("djump", {})
		return true
	return false

func _try_dash() -> bool:
	if buf_dash > 8 or dash_cd > 0:
		return false
	if on_ground and my < -0.5:
		buf_dash = 99; dash_cd = C.dash.cooldown
		var dir := signi(int(signf(mx))) if absf(mx) > 0.3 else facing
		facing = dir; vx = dir * C.slide.speed; crouch = true
		_set_state("slide")
		event.emit("slide", {})
		return true
	if not on_ground and air_dashes <= 0:
		return false
	var d := _snap8()
	if d == Vector2.ZERO:
		d = Vector2(-wall_dir if wall_sliding else facing, 0)
	if on_ground and d.y < 0:
		d = Vector2(signf(d.x) if d.x != 0 else facing, 0)
	if wall_dir != 0 and d.x * wall_dir > 0:
		d.x = -d.x
	if not on_ground:
		air_dashes -= 1
	buf_dash = 99; dash_cd = C.dash.cooldown
	if d.x != 0:
		facing = signi(int(signf(d.x)))
	dash = { "d": d, "t": C.dash.ticks, "grounded": on_ground }
	fast_fall = false; crouch = false
	_set_state("dash")
	event.emit("dash", { "d": d })
	return true

func _state_dash() -> void:
	var d: Vector2 = dash.d
	var speed: float = C.dash.speed
	vx = d.x * speed; vy = d.y * speed
	dash.t -= 1
	if buf_jump <= K.JUMP_BUFFER and (dash.grounded or coyote > 0) and d.y <= 0:
		vy = C.jumpV; vx = d.x * minf(speed, 25.0) * 0.85; dash_carry = true; on_ground = false
		buf_jump = 99; _set_state("normal")
		event.emit("jump", { "dashJump": true })
		return
	if dash.t <= 0 or (st > 1 and is_on_wall()):
		vx = d.x * speed * C.dash.exitKeep
		vy = d.y * speed * 0.4 if d.y > 0 else 0.0
		post_dash = 0; _set_state("normal")

func _state_slide() -> void:
	vx *= C.slide.decay
	_gravity()
	if _try_jump():
		dash_carry = true
		return
	if st >= C.slide.ticks or absf(vx) < 2 or not on_ground:
		crouch = test_move(global_transform, Vector2(0, -(C.height - h) * S))
		_set_state("normal")

func _floor_body() -> Object:
	for i in get_slide_collision_count():
		var c := get_slide_collision(i)
		if c.get_normal().y < -0.5:
			return c.get_collider()
	return null

# The foundry's lift pads: step on one to be thrown up to the next ring (world.js liftTick)
func _lift_pads() -> void:
	if pad_cd > 0 or state != "normal" or vy > 0.5:
		return
	var p := feet()
	for L in lifts:
		if absf(p.x - L[0]) > 0.8 + C.width / 2 or p.y < L[1] - 0.05 or p.y > L[1] + 0.45:
			continue
		vy = sqrt(2 * K.GRAVITY * (L[2] - L[1] + 1.6))
		on_ground = false; coyote = 0; jumps_used = 0; air_dashes = 1; fast_fall = false; dash_carry = true; pad_cd = 30
		event.emit("lift", { "x": L[0], "y": L[1] })
		return

# Melee: a box in front, asked of the physics space; anything breakable in it takes the blow
func _swing() -> void:
	swing_t = 14
	event.emit("swing", {})
	var q := PhysicsShapeQueryParameters2D.new()
	var r := RectangleShape2D.new()
	r.size = Vector2(1.5 * S, 1.4 * S)
	q.shape = r
	q.transform = Transform2D(0, position + Vector2(facing * 0.95 * S, -0.85 * S))
	q.exclude = [get_rid()]
	for hit in get_world_2d().direct_space_state.intersect_shape(q, 8):
		var body: Object = hit.collider
		if body and body.has_meta("breakable"):
			event.emit("strike", { "body": body, "dmg": 6.0 })
