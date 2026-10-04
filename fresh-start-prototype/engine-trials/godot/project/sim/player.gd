# A player: movement, actions and cancel rules, one tick at a time. A port of player.js, function by function
# (the same order and the same names, in snake_case), so the two can be read side by side. The World supplies
# spawning helpers and collects events. Frame data lives in Tune.C (config.js).
#
# Milestone 1 carries Nova with the Marksman kit (bracer attachments, secondary weapons, the dodge, the Aegis,
# the beam, skate glide) and everything every character shares. Echo's, RAM's and Fix's branches come with
# their kits (marked "M2"); the Pass 1 Sentinel kit comes with the settings.
class_name PlayerSim
extends RefCounted

const BUF_KEYS := ["jump", "dash", "melee", "fire", "parry", "sig", "mode", "sub"]
const CHARGED := { "nova": "nova_brace", "echo": "echo_charged", "ram": "ram_slam", "fix": "fix_slam" }

var kind := "player"
var slot := 0
var device := ""
var char := "nova"
var x := 0.0
var y := 0.0
var vx := 0.0
var vy := 0.0
var w := 0.72
var h := 1.72
var prev_x := 0.0
var prev_y := 0.0
var facing := 1
var on_ground := false
var wall_dir := 0
var hit_wall := 0
var hit_ceil := false
var coyote := 0
var jumps_used := 0
var air_dashes := 1
var state := "normal"
var st := 0
var crouch := false
var drop_t := 0
var control_lock := 0
var wall_sliding := false
var wall_prev := false
var dash = null
var dash_cd := 0
var post_dash := 99
var dash_carry := false
var fast_fall := false
var launched_t := 0
var zip_arrive_t := 0
var boost_t := 0
var iframe := false
var move = null
var move_id = null
var queued = null
var hit_confirm := false
var instance := 0
var rise_air := false
var lunge_to = null
var vb_info = null
var slash = null
var charge_t := 0.0
var fire_cd := 0
var melee_held_t := 0
var melee_charged := false
var hp := 100.0
var max_hp := 100.0
var strain := 0.0
var strain_t := 0
var mercy := 0
var hitstop := 0
var stun := 0
var parry_t := 0
var parry_result = null
var riposte_t := 0
var bulwark_cd := 0.0
# Echo's (M2): read by shared code (enemies cannot see a veiled player; Flare draws them)
var veiled := false
var scarf_mode := "tether"
var resolve := 0.0
var targeted_by := 0
var ambush_t := 0
var rifle_t := 0.0
var leash = null
var lash = null
var zip = null
# Nova's Marksman kit
var attachment := "lance"
var focus := 0.0
var focus_t := 0
var burst_cd := 0.0
var burst_t := 0.0
var shoot_t := 0
var carve_t := 0
var fuel := 60.0
var thrusting := false
var rockets := 0
var rocket_t := 0
var rocket_pow := 0.0
var aegis = null
var aegis_cd := 0.0
var overcharge := 0.0
var over_t := 0
var beam = null
var pound = null
var sub := "scatter"
var sub_sw_cd := 0
var sub_armed := false
var dodge = null
var dodge_cd := 0
var air_dodge := true
var air_rise := true
var stick := [0.0, 0.0]
var mode_cd := 0
# Ultimates
var ult := 0.0
var ult_run = null
var chord_p := 99
var chord_f := 99
# Support anyone can carry: Plating, Overclock, the Patch Beam's Tune-Up, an Amp Coil's field, Fury
var plate := 0.0
var overclock_t := 0
var tune_t := 0
var amp_k := 1.0
var pad_cd := 0
var fury_t := 0
var brace_t := 0
var aim_x := 1.0
var aim_y := 0.0
var aim_free := false
var wall_t := 0
var wall_stick := 0
var wall_coyote := 0
var last_wall_dir := 0
var dash_charge_t := 0.0
# Lock-on
var lock_t = null
var lock_held := 0
var lock_hold_done := false
var lock_lost := 0
var lock_suspend := false
var lock_picked := false
var buf := {}
# Downed, revived, respawned
var downed_t := 0
var revive := 0.0
var revive_gain := 0.0
var revive_by = null
var fix_revive := false
var auto_revive := 0
var respawn_t := 0
var second_wind := true
var last_safe_x := 0.0
var last_safe_y := 0.0
var offscreen_t := 0
var bark_cd := 0
var stuck_ticks := 0

static func create(slot_: int, device_: String, char_id: String, x_: float, y_: float) -> PlayerSim:
	var p := PlayerSim.new()
	var c: Dictionary = Tune.C.CHARS[char_id]
	p.slot = slot_; p.device = device_; p.char = char_id
	p.x = x_; p.y = y_; p.prev_x = x_; p.prev_y = y_
	p.w = c.width; p.h = c.height; p.hp = c.hp; p.max_hp = c.hp
	p.fuel = Tune.C.MARKSMAN.boost.fuel
	p.last_safe_x = x_; p.last_safe_y = y_
	for b in BUF_KEYS:
		p.buf[b] = 99
	return p

func set_character(char_id: String) -> void:
	var c: Dictionary = Tune.C.CHARS[char_id]
	char = char_id; w = c.width; h = c.height; max_hp = c.hp
	hp = minf(hp, max_hp); charge_t = 0; resolve = 0; strain = 0
	state = "normal"; st = 0
	veiled = false; ambush_t = 0; targeted_by = 0
	focus = 0; focus_t = 0; burst_cd = 0; burst_t = 0; shoot_t = 0
	fuel = Tune.C.MARKSMAN.boost.fuel; thrusting = false; rockets = 0; rocket_t = 0
	rifle_t = 0; dash_charge_t = 0; overcharge = 0; over_t = 0; beam = null; aegis = null
	sub_armed = false; dodge = null; pound = null

# Nova with the Marksman kit (bracer attachments, secondary weapons, dodge, skate glide)
func marksman() -> bool:
	return char == "nova" and Tune.settings.novaKit == "marksman"

func chest() -> Vector2:
	return Vector2(x, y + h * 0.62)

func chest_x() -> float:
	return x

func chest_y() -> float:
	return y + h * 0.62

static func snap8(sx: float, sy: float):
	if U.hypot(sx, sy) < 0.35:
		return null
	var a := roundf(atan2(sy, sx) / (PI / 4)) * (PI / 4)
	return [cos(a), sin(a)]

# Which Velocity Break tier is available right now (0 = none)?
func vb_tier() -> int:
	if state == "pound":
		return 0   # the pound's drop is fast, but it is not a Velocity Break
	if boost_t > 0 or launched_t > 0:
		return 3
	if dash != null and dash.level >= 2 and (state == "dash" or post_dash <= 6):
		return int(dash.level)
	if zip_arrive_t > 0:
		return 2
	if state == "dash" or state == "slide" or post_dash <= 6:
		return 1
	if fast_fall and vy < -18:
		return 2
	if dash_carry and not on_ground and U.hypot(vx, vy) > Tune.C.HIGH_VEL:
		return 2
	return 0

func set_state(s: String) -> void:
	state = s; st = 0

func update_aim(cmd: Cmd, world) -> void:
	var d
	if cmd.aim_free:
		d = [cmd.ax, cmd.ay]; aim_free = true
	else:
		aim_free = false
		d = snap8(cmd.mx, cmd.my)
		if d != null and on_ground and d[1] < 0:
			d = [float(facing), 0.0]   # down on the ground means crouch
		if d == null:
			d = [float(facing), 0.0]
		if Tune.settings.aimAssist and device != "kbm":
			var e = world.nearest_enemy_in_cone(chest_x(), chest_y(), d[0], d[1], 14, PI / 8)
			if e:
				var dx: float = e.x - chest_x()
				var dy: float = e.y + e.h / 2 - chest_y()
				var m := U.hypot(dx, dy)
				d = [dx / m, dy / m]
	# Locked on: aim straight at the target. With automatic lock-on, free aim (right stick, mouse) still
	# aims where it points, and holding the stick up or down aims that way.
	if lock_t and (Tune.settings.lockMode == "manual" or (not cmd.aim_free and absf(cmd.my) < 0.55)):
		var t = lock_t
		var dx: float = t.x - chest_x()
		var dy: float = t.y + t.h * 0.55 - chest_y()
		var m := U.hypot(dx, dy)
		if m == 0:
			m = 1
		d = [dx / m, dy / m]
	elif wall_sliding and wall_dir != 0 and d[0] * wall_dir > 0:
		d = [-d[0], d[1]]   # on a wall: shoot out from it
	aim_x = d[0]; aim_y = d[1]

func update(cmd: Cmd, world) -> void:
	prev_x = x; prev_y = y
	for b in BUF_KEYS:
		buf[b] = 0 if cmd.pressed[b] else mini(99, buf[b] + 1)
	track_chord(cmd); stick = [cmd.mx, cmd.my]
	if state == "dead":
		return
	# Mode switches (Nova's bracer attachment and secondary weapon) are instant, so a press during hitstop
	# is never lost
	if cmd.pressed.mode and mode_cd == 0 and state != "downed" and state != "ult":
		if marksman():
			cycle_attachment(world)
	if cmd.pressed.sub and sub_sw_cd == 0 and state != "downed" and state != "ult":
		if marksman():
			cycle_sub(world)
	if hitstop > 0:
		hitstop -= 1
		return
	# Both triggers together with a full bar: the ultimate (the world takes over from here)
	if ult >= Tune.C.ULT.max and chord_ready(cmd) and world.ult_cast == null and state != "downed" and state != "ult":
		world.start_ult(self)
		return

	st += 1
	if mercy > 0: mercy -= 1
	if dash_cd > 0: dash_cd -= 1
	if fire_cd > 0: fire_cd -= 1
	if control_lock > 0: control_lock -= 1
	if launched_t > 0: launched_t -= 1
	if zip_arrive_t > 0: zip_arrive_t -= 1
	if boost_t > 0: boost_t -= 1
	if drop_t > 0: drop_t -= 1
	if riposte_t > 0: riposte_t -= 1
	if coyote > 0: coyote -= 1
	if mode_cd > 0: mode_cd -= 1
	if ambush_t > 0: ambush_t -= 1
	if shoot_t > 0: shoot_t -= 1
	if carve_t > 0: carve_t -= 1
	if rocket_t > 0: rocket_t -= 1
	if wall_coyote > 0: wall_coyote -= 1
	if sub_sw_cd > 0: sub_sw_cd -= 1
	if dodge_cd > 0: dodge_cd -= 1
	if overclock_t > 0: overclock_t -= 1
	if tune_t > 0: tune_t -= 1
	if brace_t > 0: brace_t -= 1
	if pad_cd > 0: pad_cd -= 1
	if fury_t > 0: fury_t -= 1
	# Ability cooldowns recharge faster under Fix's boosts (Overclock, Tune-Up, an Amp Coil)
	var rate := boost_rate()
	if aegis_cd > 0: aegis_cd = maxf(0, aegis_cd - rate)
	if burst_cd > 0: burst_cd = maxf(0, burst_cd - rate)
	if bulwark_cd > 0: bulwark_cd -= 1
	# Aegis time, and Overcharge draining once it has not grown for a while
	if aegis != null:
		aegis.t -= 1
		if aegis.t <= 0:
			world.end_aegis(self, "expire")
	if overcharge > 0:
		if over_t > 0:
			over_t -= 1
		else:
			overcharge = maxf(0, overcharge - Tune.C.AEGIS.over.drain)
	post_dash = mini(99, post_dash + 1)
	tick_focus(world)   # (Echo, RAM and Fix tick their own resources: M2)

	if state == "downed":
		update_downed(cmd, world)
		return
	update_lock(cmd, world)
	update_aim(cmd, world)
	melee_held_t = melee_held_t + 1 if cmd.held.melee else 0

	var was_sliding := wall_sliding
	wall_prev = was_sliding; wall_sliding = false   # set again by wall_cling in the states that allow a wall slide
	match state:
		"normal": state_normal(cmd, world)
		"dashCharge": state_dash_charge(cmd, world)
		"beam": state_beam(cmd, world)
		"dash": state_dash(cmd, world)
		"slide": state_slide(cmd, world)
		"vb": state_vb(cmd, world)
		"attack": state_attack(cmd, world)
		"parry": state_parry(cmd, world)
		"hitstun": state_hitstun(cmd, world)
		"pound": state_pound(cmd, world)
		"dodge": state_dodge(cmd, world)
		"ult": world.ult_step(self, cmd)
	if state != "ult":
		handle_fire(cmd, world)
	# Boosters only run in the normal state; anything else (dash, hitstun, a burst...) cuts them
	if thrusting and (state != "normal" or on_ground):
		thrusting = false; world.emit("thrustOff", { "p": self })
	if wall_sliding != was_sliding:
		world.emit("wallSlide", { "p": self, "on": wall_sliding, "dir": wall_dir if wall_sliding else last_wall_dir })

	var was_ground := on_ground
	var fall_v := vy
	var low := crouch or state == "slide" or state == "dashCharge"
	if state != "dash" and state != "zip":
		h = Tune.C.CHARS[char].crouchH if low else Tune.C.CHARS[char].height
	world.level.move_body(self, Tune.DT)
	if on_ground:
		coyote = int(Tune.C.COYOTE); jumps_used = 0; air_dashes = 1; fast_fall = false; dash_carry = false; air_dodge = true; air_rise = true
		rockets = 0; rocket_t = 0; wall_coyote = 0
		var B: Dictionary = Tune.C.MARKSMAN.boost
		if fuel < B.fuel:
			fuel = minf(B.fuel, fuel + B.refill)
		if not was_ground and st > 1:
			world.emit("land", { "p": self, "vy": fall_v })
		last_safe_x = x; last_safe_y = y
	if wall_dir != 0 and not on_ground:
		# Touching a wall gives back the air dash and the double jump, and remembers the wall for a late wall jump
		air_dashes = 1; jumps_used = 0; air_dodge = true; air_rise = true; last_wall_dir = wall_dir; wall_coyote = int(Tune.C.WALL.coyote)
	iframe = (Tune.settings.dashIframes and state == "dash" and st <= 8) or (state == "dash" and dash != null and st <= dash.iframes) \
		or (state == "dodge" and dodge != null and dodge.t <= Tune.C.DODGE.iframes) or state == "ult"

# ---- Shared action starters ------------------------------------------------------------

func try_jump(cmd: Cmd, world) -> bool:
	if buf.jump > Tune.C.JUMP_BUFFER:
		return false
	if on_ground and crouch and cmd.my < -0.6 and world.level.on_one_way(x, y):
		drop_t = 12; y -= 0.05; buf.jump = 99; on_ground = false
		return true
	var c: Dictionary = Tune.C.CHARS[char]
	if on_ground or coyote > 0:
		vy = c.jumpV; coyote = 0; on_ground = false; crouch = false
		if post_dash <= 8:
			dash_carry = true
		buf.jump = 99; set_state("normal"); world.emit("jump", { "p": self })
		return true
	var wd := wall_dir if wall_dir != 0 else (last_wall_dir if wall_coyote > 0 else 0)
	if wd != 0:
		# Holding away from the wall leaps off it; toward it or neutral is a climb kick that rises high and
		# lets you come straight back to the same wall
		var W: Dictionary = Tune.C.WALL
		var cw: Dictionary = c.wall
		var away := cmd.mx * wd < -0.3
		vx = -wd * (cw.jumpVx if away else W.climb.vx); vy = cw.jumpVy * (W.leapVy if away else 1.0)
		control_lock = int(cw.lock if away else W.climb.lock); facing = -wd
		wall_coyote = 0; wall_stick = 0; wall_sliding = false
		buf.jump = 99; fast_fall = false; set_state("normal"); world.emit("walljump", { "p": self, "climb": not away, "dir": -wd })
		return true
	if jumps_used < 1:
		# While a rocket launch still climbs faster than a double jump would, the press waits in the buffer
		if rocket_t > 0 and vy > c.dblV:
			return false
		vy = c.dblV; jumps_used = 1; fast_fall = false
		buf.jump = 99; set_state("normal"); world.emit("djump", { "p": self })
		return true
	return false

func try_dash(cmd: Cmd, world) -> bool:
	if buf.dash > Tune.C.ACTION_BUFFER or dash_cd > 0:
		return false
	var c: Dictionary = Tune.C.CHARS[char]
	if on_ground and cmd.my < -0.5:
		buf.dash = 99; dash_cd = int(c.dash.cooldown)
		var dir := U.sgn(cmd.mx) if absf(cmd.mx) > 0.3 else facing
		facing = dir; vx = dir * c.slide.speed; crouch = true
		set_state("slide"); world.emit("slide", { "p": self })
		return true
	# Charged dash: on the ground with no direction held, holding dash plants the feet and charges
	if Tune.settings.dashCharge and on_ground and state == "normal" and cmd.held.dash and absf(cmd.mx) < 0.3 and absf(cmd.my) < 0.5:
		buf.dash = 99; dash_charge_t = 0; crouch = false
		set_state("dashCharge"); world.emit("dashChargeStart", { "p": self })
		return true
	if not on_ground and air_dashes <= 0:
		return false
	var d = snap8(cmd.mx, cmd.my)
	if d == null:
		d = [float(-wall_dir if wall_sliding else facing), 0.0]
	if on_ground and d[1] < 0:
		d = [float(U.sgn_or(d[0], facing)), 0.0]
	if wall_dir != 0 and d[0] * wall_dir > 0:
		d = [-d[0], d[1]]   # from a wall, a dash goes out from it
	if not on_ground:
		air_dashes -= 1
	start_dash(d, world, 0)
	return true

# Level 0 is an ordinary dash; 1-3 come from a charged release (DASH_CHARGE). (RAM's dash is the Ram Charge: M2.)
func start_dash(d: Array, world, level: int) -> void:
	var c: Dictionary = Tune.C.CHARS[char]
	var D: Dictionary = Tune.C.DASH_CHARGE
	var L := level - 1
	buf.dash = 99; dash_cd = int(c.dash.cooldown)
	if d[0] != 0:
		facing = U.sgn(d[0])
	dash = { "dx": d[0], "dy": d[1], "t": U.jround(c.dash.ticks * (D.ticks[L] if level else 1.0)), "grounded": on_ground, "level": level,
		"speed": c.dash.speed * (D.speed[L] if level else 1.0), "keep": D.exitKeep[L] if level else c.dash.exitKeep,
		"iframes": D.iframes[L] if level else 0, "instance": world.new_instance() if level == 3 else 0, "pursuit": null }
	fast_fall = false; crouch = false; dash_charge_t = 0
	set_state("dash"); world.emit("dash", { "p": self, "level": level, "dx": d[0], "dy": d[1] })

# Planted and charging: skid to a stop, aim with the stick, let go to launch. Jump or parry cancel it;
# a tap (released before DASH_CHARGE.tap) is an ordinary dash toward where you face.
func state_dash_charge(cmd: Cmd, world) -> void:
	var D: Dictionary = Tune.C.DASH_CHARGE
	var C: Array = D.charge
	# The tap window counts in real ticks; the charge itself grows faster under Fix's boosts
	var t0 := dash_charge_t
	dash_charge_t += 1.0 if dash_charge_t < D.tap else boost_rate()
	# For the first few ticks nothing changes, so a quick tap reads as an ordinary dash; then he plants
	if dash_charge_t >= D.tap:
		vx = U.approach(vx, 0, 70 * Tune.DT)
	apply_gravity(cmd)
	for level in crossed(t0, dash_charge_t, C):
		world.emit("dashLevel", { "p": self, "level": level })
	if absf(cmd.mx) > 0.3:
		facing = U.sgn(cmd.mx)
	if try_parry(world):
		dash_charge_t = 0
		return
	if buf.jump <= Tune.C.JUMP_BUFFER or not on_ground:
		dash_charge_t = 0; set_state("normal"); world.emit("dashChargeEnd", { "p": self })
		if on_ground:
			try_jump(cmd, world)
		return
	if cmd.held.dash:
		return
	var t := dash_charge_t
	var level := 3 if t >= C[2] else (2 if t >= C[1] else (1 if t >= C[0] else 0))
	var d = snap8(cmd.mx, cmd.my)
	if d == null:
		d = [float(facing), 0.0]
	if d[1] < 0:
		d = [float(U.sgn_or(d[0], facing)), 0.0]   # no digging into the floor
	start_dash(d, world, level)

func try_parry(world) -> bool:
	if buf.parry > Tune.C.PARRY_BUFFER:
		return false
	if marksman():
		return try_dodge(world)   # Nova's Marksman kit dodges instead
	# (RAM's guard and Fix's Patch Beam: M2)
	buf.parry = 99; parry_t = 0; parry_result = null
	set_state("parry"); world.emit("parryStart", { "p": self })
	return true

func try_signature(_cmd: Cmd, world) -> bool:
	if buf.sig > Tune.C.ACTION_BUFFER:
		return false
	if char == "nova" and marksman():
		# Marksman kit: the hard-light Aegis. Instant: no state change, he keeps moving and shooting.
		# Pressing again while it is up detonates it outward.
		if aegis != null:
			buf.sig = 99; world.detonate_aegis(self)
			return false
		if aegis_cd > 0:
			return false
		buf.sig = 99; world.raise_aegis(self)
		return false
	# (the Sentinel kit's Bulwark and Echo's scarf: later milestones)
	return false

func try_melee(cmd: Cmd, world) -> bool:
	if buf.melee > Tune.C.ACTION_BUFFER:
		return false
	# In the air, the secondary aimed down: the ground pound. Holding down to fast-fall must not turn it into
	# a Velocity Break; a dash, slide, launch or zip still does.
	var down: bool = cmd.my < -0.55 or (aim_free and aim_y < Tune.C.POUND.aimDown)
	var moving := state == "dash" or state == "slide" or post_dash <= 6 or boost_t > 0 or launched_t > 0 or zip_arrive_t > 0
	if not on_ground and down and not moving:
		buf.melee = 99; start_pound(world)
		return true
	var tier := vb_tier()
	if tier > 0:
		velocity_break(tier, world)
		return true
	# Rising attacks (up + melee; once per airtime in the air): Nova's Solar Uppercut
	if char != "echo" and cmd.my > 0.55 and (on_ground or air_rise):
		buf.melee = 99
		if not on_ground:
			air_rise = false
		start_move(char + "_rise", world)
		return true
	if marksman():
		# Marksman kit: close to an enemy, his bracer combo; otherwise his secondary weapon (it cancels
		# whatever it interrupts)
		if melee_target(world) != null:
			buf.melee = 99; start_move("nova_k1" if on_ground else "nova_kair", world)
			return true
		return press_sub(world)
	buf.melee = 99
	start_move("nova_jab1" if on_ground else "nova_air", world)   # (Sentinel Nova; Echo, RAM, Fix: M2)
	return true

# Marksman kit: an enemy close enough in front for the bracer combo (or the lock-on target in reach)
func melee_target(world):
	var R: Dictionary = Tune.C.MARKSMAN.melee
	var face := U.sgn(aim_x) if aim_free and absf(aim_x) > 0.2 else facing
	for e in world.enemies:
		if e.dead:
			continue
		var ahead: float = (e.x - x) * face
		var gap: float = ahead - e.w / 2 - w / 2
		var dy: float = absf(e.y + e.h / 2 - (y + h * 0.5))
		if ahead > -0.2 and gap < R.reach - 0.8 and dy < R.up:
			return e
		if e == lock_t and lock_chosen() and absf(e.x - x) < Tune.C.LOCK.magnet and dy < R.up:
			return e
	return null

func start_move(id: String, world) -> void:
	move_id = id; move = Tune.C.MOVES[id]; queued = null; hit_confirm = false; instance = world.new_instance()
	crouch = false; rise_air = not on_ground
	if absf(aim_x) > 0.2 and aim_free:
		facing = U.sgn(aim_x)
	# Lock-on: turn to a target that is close, and step in toward it during the swing (lunge_to)
	lunge_to = null
	var t = lock_t
	if t and not t.dead and absf(t.x - x) < Tune.C.LOCK.magnet and absf(t.y - y) < 2.5:
		facing = U.sgn_or(t.x - x, facing); lunge_to = t
		if on_ground and absf(t.x - x) - t.w / 2 - w / 2 > 0.3:
			vx = facing * Tune.C.LOCK.lunge   # the step starts at once
	set_state("attack"); world.emit("swing", { "p": self, "id": id })

# A Velocity Break (Echo's Hunter kit turns it into the Dash Slash: M2)
func velocity_break(tier: int, world) -> void:
	start_vb(tier, world)

func start_vb(tier: int, world) -> void:
	buf.melee = 99
	var sp := U.hypot(vx, vy)
	var dx: float = vx / sp if sp > 0.5 else float(facing)
	var dy: float = vy / sp if sp > 0.5 else 0.0
	if state == "dash" and dash != null:
		dx = dash.dx; dy = dash.dy
	if absf(dx) > 0.2:
		facing = U.sgn(dx)
	vb_info = { "tier": tier, "dx": dx, "dy": dy, "keep": 0.3 if Tune.settings.vbStop == "keep30" else 0.0, "v0x": vx, "v0y": vy }
	hit_confirm = false; instance = world.new_instance()
	boost_t = 0; launched_t = 0; zip_arrive_t = 0; post_dash = 99
	set_state("vb"); world.emit("vbStart", { "p": self, "tier": tier })

# Anything that may interrupt a state early (on hit-confirm or late whiff recovery).
func cancel_into(cmd: Cmd, world, jump := true, dash_ := true, parry := true, sig := true, melee := true) -> bool:
	if parry and try_parry(world): return true
	if dash_ and try_dash(cmd, world): return true
	if jump and try_jump(cmd, world): return true
	if sig and try_signature(cmd, world): return true
	if melee and try_melee(cmd, world): return true
	return false

# ---- States ----------------------------------------------------------------------------

func horizontal_control(cmd: Cmd, world, scale := 1.0) -> void:
	var c: Dictionary = Tune.C.CHARS[char]
	var M: Dictionary = Tune.C.MARKSMAN
	var skates := marksman()
	var tgt: float = cmd.mx * (M.skate.top if skates else c.run) * (c.crouchSpeed if crouch else 1.0) * (M.boost.air if thrusting else 1.0) * scale
	var firing := charge_t > 0 or fire_cd > 0 or shoot_t > 0
	var facing_aim := (aim_free or firing) and absf(aim_x) > 0.2
	if facing_aim:
		if cmd.mx != 0 and U.sgn(cmd.mx) != U.sgn(aim_x):
			tgt *= M.skate.backpedal if skates else c.backpedal
		facing = U.sgn(aim_x)
	elif lock_t and absf(cmd.mx) <= 0.1 and absf(aim_x) > 0.05:
		facing = U.sgn(aim_x)   # standing still while locked on: face the target
	elif absf(cmd.mx) > 0.1 and control_lock == 0:
		facing = U.sgn(cmd.mx)
	if control_lock > 0:
		return
	if on_ground and skates:
		skate_ground(tgt, world)
		return
	if on_ground:
		var accel: float = c.accelG if (tgt != 0 and U.sgn(tgt) == U.sgn(vx)) or absf(vx) < 0.1 else c.decelG
		vx = U.approach(vx, tgt, accel * Tune.DT)
	else:
		# Keep dash-carried momentum in the air unless the player steers against it
		if dash_carry and absf(vx) > absf(tgt) and U.sgn(tgt) != -U.sgn(vx):
			return
		vx = U.approach(vx, tgt, c.accelA * Tune.DT)

# Skate-blade glide: a little slower to reach top speed, keeps momentum when the stick is let go,
# and carves to a stop when reversed. Crouching at speed tucks into a low glide.
func skate_ground(tgt: float, world) -> void:
	var S: Dictionary = Tune.C.MARKSMAN.skate
	var speed := absf(vx)
	var a: float
	if crouch and speed > S.tuckMin:
		tgt = 0; a = S.tuck
	elif tgt == 0:
		a = S.coast
	elif U.sgn(tgt) != U.sgn(vx) and speed > 0.5:
		a = S.carve
		if speed > 5 and carve_t == 0:
			carve_t = 10; world.emit("carve", { "p": self })
	else:
		a = S.accel if speed < absf(tgt) else S.coast
	vx = U.approach(vx, tgt, a * Tune.DT)

func apply_gravity(cmd: Cmd, mult := 1.0) -> void:
	if on_ground and vy <= 0:
		vy = -0.5
		return
	var g: float = Tune.C.GRAVITY * mult
	if vy < 0:
		g *= Tune.C.FALL_MULT
	elif not cmd.held.jump and not dash_carry:
		g *= Tune.C.RISE_CUT_MULT
	vy -= g * Tune.DT
	var cap: float = Tune.C.FAST_FALL if fast_fall else Tune.C.MAX_FALL
	if vy < -cap:
		vy = -cap

func state_normal(cmd: Cmd, world) -> void:
	var c: Dictionary = Tune.C.CHARS[char]
	if on_ground and cmd.my < -0.55:
		crouch = true
	elif crouch and world.level.has_headroom(x, y, w, c.height):
		crouch = false
	horizontal_control(cmd, world)
	if not on_ground and cmd.my < -0.7 and vy < 3 and wall_dir == 0:
		fast_fall = true
	if not thrust(cmd, world):
		apply_gravity(cmd)
	wall_cling(cmd, true)
	cancel_into(cmd, world)
	if state != "normal":
		if not cmd.held.melee:
			melee_charged = false
		return
	if marksman():
		return   # the Marksman kit charges its secondary weapon instead (fire_marksman)
	# Charged melee: release after holding
	if melee_charged and not cmd.held.melee:
		melee_charged = false
		start_move(CHARGED[char], world)
	if melee_held_t >= 30 and state == "normal" and not melee_charged:
		melee_charged = true; world.emit("meleeCharged", { "p": self })

# Wall slide (every character, in the normal, attack and parry states). Holding toward a wall in the air
# while not rising starts it; it grips for a moment, then eases up to the slide speed, braking a fall on
# the way in. Letting go of the stick keeps the grip for WALL.stick ticks (so press away, then jump, is
# still a wall jump). The character faces out from the wall; `turn` is off during an attack so a swing
# already under way keeps its direction.
func wall_cling(cmd: Cmd, turn: bool) -> bool:
	var c: Dictionary = Tune.C.CHARS[char]
	var W: Dictionary = Tune.C.WALL
	var was := wall_prev
	var on := false
	if not on_ground and wall_dir != 0 and vy <= 0.5:
		var toward := cmd.mx * wall_dir > 0.3
		if toward:
			wall_stick = int(W.stick)
		elif was and wall_stick > 0:
			wall_stick -= 1
		on = toward or (was and wall_stick > 0)
	if not on:
		wall_t = 0
		return false
	wall_t = wall_t + 1 if was else 0
	var ramp := clampf((wall_t - W.grip) / W.ease, 0, 1)
	var target: float = c.wall.slide * W.fast if cmd.my < -0.6 else W.gripSpeed + (c.wall.slide - W.gripSpeed) * ramp
	if vy < -target:
		vy = minf(-target, vy + (W.brake + Tune.C.GRAVITY * Tune.C.FALL_MULT) * Tune.DT)   # brake a fall into the slide
	else:
		vy = maxf(vy, -target)
	if cmd.mx * wall_dir <= 0.3:
		vx = wall_dir * 0.5   # grip: stay against the wall
	wall_sliding = true; fast_fall = false
	if turn:
		facing = -wall_dir
	return true

# Marksman kit: light boosters. Once the double jump is spent, pressing jump again fires them and holding
# keeps them on: he hovers and climbs gently on a small tank of fuel that refills on the ground. Ordinary
# jumps are untouched. Returns true while thrusting.
func thrust(cmd: Cmd, world) -> bool:
	if not marksman():
		return false
	var B: Dictionary = Tune.C.MARKSMAN.boost
	var start: bool = cmd.pressed.jump and jumps_used >= 1 and wall_dir == 0 and fuel >= B.minStart
	var on: bool = not on_ground and not wall_sliding and fuel > 0 and cmd.held.jump and (thrusting or start)
	if on != thrusting:
		thrusting = on; world.emit("thrustOn" if on else "thrustOff", { "p": self })
	if not on:
		return false
	fuel = maxf(0, fuel - 1); fast_fall = false
	# They only add lift below their climb speed, so they never cut a rising jump short
	if vy > B.rise:
		vy -= Tune.C.GRAVITY * Tune.DT
	else:
		vy = U.approach(vy, B.rise, B.thrust * Tune.DT)
	return true

func state_dash(cmd: Cmd, world) -> void:
	var c: Dictionary = Tune.C.CHARS[char]
	var d: Dictionary = dash
	if d.pursuit != null:   # (Echo's pursuit of a tagged enemy: M2)
		var t = d.pursuit
		if t.dead:
			d.pursuit = null
		else:
			var tx: float = t.x - U.sgn(t.x - x) * (t.w / 2 + w / 2 + 0.2)
			var ty: float = t.y + t.h * 0.3
			var dx := tx - x
			var dy := ty - y
			var m := U.hypot(dx, dy)
			if m < 0.9:
				zip_arrive_t = 12; d.t = 0
			else:
				d.dx = dx / m; d.dy = dy / m
				if absf(d.dx) > 0.2:
					facing = U.sgn(d.dx)
	var boost := 1.35 if boost_t > 0 else 1.0
	var speed: float = d.speed if d.speed else c.dash.speed
	vx = d.dx * speed * boost; vy = d.dy * speed * boost
	d.t -= 1
	if d.level == 3:
		# A full charge turns the dash into a strike through everything in its path (each enemy once)
		var S: Dictionary = Tune.C.DASH_CHARGE.strike
		world.spawn_hitbox({ "owner": self, "team": "p", "x0": x - 0.8, "x1": x + 0.8, "y0": y, "y1": y + h + 0.2, "dmg": S.dmg, "poise": S.poise,
			"kb": [U.sgn_or(d.dx, facing) * S.kb, 3.0], "armorBreak": true, "instance": d.instance, "dashStrike": true })
	if buf.jump <= Tune.C.JUMP_BUFFER and (d.grounded or coyote > 0) and d.dy <= 0:
		# Dash-jump: a charged dash carries more speed into the jump, up to DASH_CHARGE.jumpCarry
		vy = c.jumpV; vx = d.dx * minf(speed, Tune.C.DASH_CHARGE.jumpCarry) * 0.85; dash_carry = true; on_ground = false
		buf.jump = 99; set_state("normal"); world.emit("jump", { "p": self, "dashJump": true })
		return
	if buf.melee <= Tune.C.ACTION_BUFFER:
		velocity_break(vb_tier(), world)
		return
	if try_parry(world):
		return
	if d.t <= 0 or hit_wall != 0:
		var keep: float = d.keep if d.keep != null else c.dash.exitKeep
		vx = d.dx * speed * keep; vy = d.dy * speed * 0.4 if d.dy > 0 else 0.0
		post_dash = 0; set_state("normal")

func state_slide(cmd: Cmd, world) -> void:
	var c: Dictionary = Tune.C.CHARS[char]
	vx *= c.slide.decay; apply_gravity(cmd)
	if try_jump(cmd, world):
		dash_carry = true
		return
	if buf.melee <= Tune.C.ACTION_BUFFER:
		velocity_break(1, world)
		return
	if try_parry(world):
		return
	if st >= c.slide.ticks or absf(vx) < 2 or not on_ground:
		crouch = not world.level.has_headroom(x, y, w, c.height)
		set_state("normal")

func state_vb(cmd: Cmd, world) -> void:
	var VB: Dictionary = Tune.C.VB
	var v: Dictionary = vb_info
	var t := st
	if t <= VB.stopTicks:
		var k: float = v.keep if t >= VB.stopTicks else 1 - (1 - v.keep) * (t / VB.stopTicks)
		vx = v.v0x * k; vy = v.v0y * k
	elif not on_ground:
		if t < VB.activeTo:
			vy = maxf(vy, 0)   # brief hang for precision stops
		else:
			apply_gravity(cmd)
	else:
		vx *= 0.8; vy = -0.5
	if t >= VB.activeFrom and t < VB.activeTo:
		var down: bool = v.dy < -0.6
		var tier: Dictionary = VB.tiers[int(v.tier)]
		var hb := { "owner": self, "team": "p", "dmg": tier.dmg, "poise": tier.poise, "kb": [facing * tier.kb, 4.0 if down else 3.0],
			"armorBreak": tier.armorBreak, "instance": instance, "vbTier": v.tier }
		if down:
			hb.x0 = x - 1.0; hb.x1 = x + 1.0; hb.y0 = y - 0.4; hb.y1 = y + 1.0
		else:
			hb.x0 = x + (0.0 if facing > 0 else -1.7); hb.x1 = x + (1.7 if facing > 0 else 0.0); hb.y0 = y + 0.2; hb.y1 = y + 1.6
		world.spawn_hitbox(hb)
	if hit_confirm and t >= VB.activeFrom:
		if Tune.settings.vbRefund and not on_ground:
			air_dashes = 1
		if cancel_into(cmd, world):
			return
	var end: float = VB.activeTo + (4 if hit_confirm else VB.whiffRecovery)
	if t >= VB.activeTo + VB.driftAfter and not on_ground:
		vx = U.approach(vx, cmd.mx * Tune.C.CHARS[char].run * 0.5, 30 * Tune.DT)
	if t >= end:
		set_state("normal")

# Ground pound (POUND). Phases: 'hold' (he hangs; holding the button charges it), 'drop' (a fast fall that
# hits what it passes through), 'land' (the scatter blast has gone off; a short recovery). A parry or dash
# cancels the hold.
func start_pound(world) -> void:
	pound = { "phase": "hold", "t": 0, "level": 0, "held": true, "y0": y, "c": 0.0, "hit": false }
	hit_confirm = false; instance = world.new_instance()
	dash_carry = false; fast_fall = false; lunge_to = null
	if absf(aim_x) > 0.2 and aim_free:
		facing = U.sgn(aim_x)
	set_state("pound"); world.emit("poundStart", { "p": self })

func state_pound(cmd: Cmd, world) -> void:
	var P: Dictionary = Tune.C.POUND
	var S = pound
	if S == null:
		set_state("normal")
		return
	S.t += 1
	if S.phase == "hold":
		# The mid-air slowdown: his rise and drift die away fast and he sinks slowly while it charges
		vx = U.approach(vx, 0, 50 * Tune.DT); vy = U.approach(vy, -P.hang, 80 * Tune.DT); fast_fall = false
		if not cmd.held.melee:
			S.held = false
		if S.held:
			S.c += boost_rate()
			var lv := 3 if S.c >= P.charge[2] else (2 if S.c >= P.charge[1] else (1 if S.c >= P.charge[0] else 0))
			if lv > S.level:
				S.level = lv; world.emit("poundLevel", { "p": self, "level": lv })
		if try_parry(world) or try_dash(cmd, world):
			pound = null
			return
		if on_ground:
			land_pound(world)
			return
		if (not S.held and S.t >= P.windup) or S.t >= P.maxHold:
			S.phase = "drop"; S.t = 0; S.y0 = y; instance = world.new_instance(); world.emit("poundDrop", { "p": self, "level": S.level })
		return
	if S.phase == "drop":
		# (Echo's quick pound bounces off what it hits: M2)
		vy = -P.speed; fast_fall = true; vx *= 0.9
		var k: float = 1 + 0.25 * S.level
		world.spawn_hitbox({ "owner": self, "team": "p", "x0": x - P.box.w / 2, "x1": x + P.box.w / 2, "y0": y - 0.7, "y1": y + P.box.h - 0.7,
			"dmg": P.drop.dmg * k, "poise": P.drop.poise * k, "kb": [0.0, -4.0], "instance": instance, "pound": true })
		if on_ground:
			land_pound(world)
			return
		if S.t > P.maxDrop:
			pound = null; set_state("normal")   # fell a long way (a pit)
		return
	# 'land': the blast has gone off; a short recovery, cancellable once it has hit something
	vx *= 0.7; vy = -0.5
	if hit_confirm:
		S.hit = true
	if S.hit and S.t >= P.hitRecover and cancel_into(cmd, world, true, true, true, true, false):
		pound = null
		return
	if S.t >= P.recover:
		pound = null; set_state("normal")

# The scatter blast: every enemy in reach is hit and thrown outward, away from the impact
func land_pound(world) -> void:
	var P: Dictionary = Tune.C.POUND
	var S: Dictionary = pound
	var L: Dictionary = P.land[int(S.level)]
	var fall := maxf(0, S.y0 - y)
	var K := 1.0   # (RAM's Meteor Drop lands harder and wider; Fix's sends out a repair pulse: M2)
	var r: float = (L.r + P.fallBonus * minf(1, fall / 12)) * K
	var inst: int = world.new_instance()
	world.spawn_hitbox({ "owner": self, "team": "p", "x0": x - r, "x1": x + r, "y0": y - 0.3, "y1": y + 1.6 + 0.3 * S.level, "dmg": L.dmg * K, "poise": L.poise * K,
		"kb": [L.kb, L.up], "radial": true, "cx": x, "armorBreak": bool(L.get("armorBreak", false)) or K > 1, "instance": inst, "scatter": true, "ramKnock": char == "ram" })
	S.phase = "land"; S.t = 0; S.inst = inst; S.hit = false
	vx = 0; hit_confirm = false; hitstop = 2 + int(S.level)   # a beat of impact freeze, longer the bigger the pound
	world.emit("poundLand", { "p": self, "x": x, "y": y, "level": S.level, "r": r, "fall": fall })

func state_attack(cmd: Cmd, world) -> void:
	var m: Dictionary = move
	var t := st
	var active_start: int = int(m.su)
	var active_end: int = int(m.su + m.ac)
	var end: int = int(m.su + m.ac + m.rc)
	# A rising attack takes off (no rise cut); from the air it climbs a little less
	if m.has("rise") and t == active_start:
		vy = m.rise * (m.get("airRise", 1.0) if rise_air else 1.0); on_ground = false; vx = facing * (1.5 if m.get("fist") else 2.5); dash_carry = true; fast_fall = false
	if m.has("riseBlast") and t == active_end:
		# The Solar Uppercut's flare: a burst of light off the fist at the top of the climb
		var spec: Dictionary = m.riseBlast.duplicate(); spec.armorBreak = false
		world.explode({ "owner": self, "x": x + facing * 0.35, "y": y + h + 0.55, "spec": spec, "kind": m.get("blastKind", "riseBlast"), "level": 1 })
	# (Jack-Up's pad, the Seismic Slam's quake, the Torque Slam's sparks, the Uplift's sweep: M2)
	# On the ground an attack keeps pressing into the floor, so it never reads as airborne mid-swing
	if on_ground:
		vx *= 0.82; vy = -0.5
	else:
		apply_gravity(cmd, m.hoverAll if m.has("hoverAll") else (0.25 if m.get("hover") and hit_confirm else 1.0)); wall_cling(cmd, false)
	if m.get("hover") and hit_confirm and vy < 1.5:
		vy = 1.5
	if m.has("hoverAll") and vy < -3:
		vy = -3
	if t == active_start and on_ground and not m.get("launcher"):
		vx += facing * 2.2
	if m.has("multi") and t > active_start and t < active_end and (t - active_start) % int(m.multi) == 0:
		instance = world.new_instance()
	if t == active_start and m.has("blastFist"):
		var spec: Dictionary = m.blastFist.duplicate(); spec.armorBreak = false
		world.explode({ "owner": self, "x": x + facing * 1.2, "y": y + 1.1, "spec": spec, "kind": "blast", "level": 1 })
	if lunge_to != null and t < active_end and on_ground:
		# Locked on: close the gap to the target until the swing lands
		var e = lunge_to
		var gap: float = absf(e.x - x) - e.w / 2 - w / 2
		if not e.dead and gap > 0.3:
			vx = facing * minf(Tune.C.LOCK.lunge, gap * 30)
		else:
			lunge_to = null
	if t >= active_start and t < active_end:
		var b: Dictionary = m.box
		var cx: float = x if m.get("spin") else x + facing * b.fx
		world.spawn_hitbox({ "owner": self, "team": "p", "x0": cx - b.w / 2, "x1": cx + b.w / 2, "y0": y + b.y - b.h / 2, "y1": y + b.y + b.h / 2,
			"dmg": m.dmg, "poise": m.poise, "kb": [facing * m.kb[0], m.kb[1]], "armorBreak": bool(m.get("armorBreak", false)), "heavy": bool(m.get("heavy", false)),
			"launcher": bool(m.get("launcher", false)), "shove": bool(m.get("shove", false)), "instance": instance, "moveId": move_id, "spin": bool(m.get("spin", false)), "cx": x,
			"wrench": bool(m.get("wrench", false)), "ram": char == "ram" and bool(m.get("shield", false)), "ramKnock": char == "ram" })
	if m.get("launcher") and t == active_end and hit_confirm:
		vy = 9   # a hop after a launched enemy
	if buf.melee <= Tune.C.ACTION_BUFFER and m.has("next") and t >= active_start:
		queued = m.next
	if t >= active_end:
		if queued != null and t >= active_end + 2:
			var nxt: String = queued
			var nm: Dictionary = Tune.C.MOVES[nxt]
			if bool(nm.get("air", false)) == (not on_ground) or not nm.get("air", false):
				buf.melee = 99; start_move(nxt, world)
				return
		var late_whiff := t >= active_end + int(floor(m.rc * 0.6))
		if hit_confirm:
			if cancel_into(cmd, world, true, true, true, true, false):
				return
		elif late_whiff:
			if cancel_into(cmd, world, false, true, true, false, false):
				return
	if t >= end:
		set_state("normal")

func state_parry(cmd: Cmd, world) -> void:
	parry_t += 1
	if on_ground:
		vx *= 0.7
	else:
		apply_gravity(cmd); vx = U.approach(vx, cmd.mx * 2, 20 * Tune.DT); wall_cling(cmd, true)
	if parry_result != null:
		# Successful parry: short, cancellable recovery
		if st > 3 and cancel_into(cmd, world):
			return
		if st > 10:
			set_state("normal")
		return
	if parry_t >= Tune.C.PARRY.window + Tune.C.PARRY.whiff:
		set_state("normal")

func state_hitstun(cmd: Cmd, _world) -> void:
	if on_ground:
		vx *= 0.85
	apply_gravity(cmd)
	if st >= stun:
		set_state("normal")

func update_downed(cmd: Cmd, world) -> void:
	vx = cmd.mx * 1.2; crouch = false
	apply_gravity(cmd)
	h = 0.6
	world.level.move_body(self, Tune.DT)
	downed_t -= 1
	if downed_t <= 0:
		world.bleed_out(self)

# ---- Lock-on ---------------------------------------------------------------------------

# A lock the player chose (any lock in manual mode; with automatic lock-on, one picked with the button)
func lock_chosen() -> bool:
	return Tune.settings.lockMode == "manual" or lock_picked

# A press locks onto the best target at once. While locked, a tap cycles to the next target and holding
# for LOCK.hold ticks lets go. The world keeps the lock valid (target death, range, line of sight).
# Automatic lock-on (the default) also locks the nearest enemy in sight whenever there is no target;
# letting go by holding the button pauses that until the next press.
func update_lock(cmd: Cmd, world) -> void:
	if not Tune.settings.lockOn:
		if lock_t:
			world.set_lock(self, null, "off")
		lock_suspend = false
		return
	var auto: bool = Tune.settings.lockMode != "manual"
	var held: bool = cmd.held.lock
	var pressed: bool = cmd.pressed.lock
	if pressed:
		lock_held = 1; lock_hold_done = false
		if not lock_t or lock_suspend:
			lock_suspend = false; world.set_lock(self, world.best_lock_target(self), "on"); lock_hold_done = true
	elif held and lock_held > 0:
		lock_held += 1
		if lock_held >= Tune.C.LOCK.hold and not lock_hold_done:
			lock_hold_done = true; world.set_lock(self, null, "release")
			if auto:
				lock_suspend = true
	if not held and lock_held > 0:
		if not lock_hold_done and lock_t:
			world.set_lock(self, world.next_lock_target(self), "cycle")
		lock_held = 0; lock_hold_done = false
	world.validate_lock(self)
	if auto and not lock_t and not lock_suspend:
		var t = world.auto_lock_target(self)
		if t:
			world.set_lock(self, t, "auto")
	if not auto:
		lock_suspend = false

# ---- Firing ----------------------------------------------------------------------------

func can_fire() -> bool:
	return state in ["normal", "dash", "slide", "lash", "dodge"] or (state == "attack" and hit_confirm)

func handle_fire(cmd: Cmd, world) -> void:
	if marksman():
		fire_marksman(cmd, world)
		return
	# (The Sentinel kit, Echo, RAM and Fix: later milestones)
	charge_t = 0

static func level_of(t: float, C: Array) -> int:
	return 3 if t >= C[2] else (2 if t >= C[1] else (1 if t >= C[0] else 0))

# Charge levels reached going from t0 to t1 (a charge can grow by more than one a tick with Overcharge)
static func crossed(t0: float, t1: float, marks: Array) -> Array:
	var out := []
	for i in marks.size():
		if t0 < marks[i] and t1 >= marks[i]:
			out.append(i + 1)
	return out

# Marksman kit: tap for basic rounds, hold to charge the loaded attachment through three levels. Letting go
# within MARKSMAN.perfectWindow ticks of level 3 is a Perfect Release. The secondary weapon works the same way
# on the melee button: the press fires a quick burst (try_melee) and holding charges a bigger one.
func fire_marksman(cmd: Cmd, world) -> void:
	var M: Dictionary = Tune.C.MARKSMAN
	var C: Array = M.charge
	var B: Dictionary = M.burst
	var L4: float = M.beam.at
	var rate: float = (Tune.C.AEGIS.over.charge if overcharge > 0 else 1.0) * boost_rate()
	if cmd.pressed.fire and fire_cd == 0 and can_fire():
		world.fire_shot(self, 0); fire_cd = int(Tune.C.NOVA.shotCd)
	if cmd.held.fire and can_fire():
		var t0 := charge_t
		charge_t += rate
		var marks := C.duplicate(); marks.append(L4)
		for level in crossed(t0, charge_t, marks):
			world.emit("chargeLevel", { "p": self, "level": level })
	if cmd.released.fire or (not cmd.held.fire and charge_t > 0):
		var t := charge_t
		var level := level_of(t, C)
		charge_t = 0
		if t >= L4:
			if can_fire():
				start_beam(world)
		elif level:
			world.fire_attachment(self, attachment, level, level == 3 and t < C[2] + M.perfectWindow, t)
	# Secondary weapon: holding charges it (not while a disc or well of his is still out); letting go fires
	# the charged level, or a tap (the Scatter fired its tap on the press, in try_melee)
	var ready := sub_ready(world)
	if cmd.held.melee and can_fire() and ready:
		var t0 := burst_t
		burst_t += rate
		for level in crossed(t0, burst_t, B.charge):
			world.emit("burstLevel", { "p": self, "level": level, "sub": sub })
	if not cmd.held.melee and (burst_t > 0 or sub_armed):
		var t := burst_t
		var level := level_of(t, B.charge)
		var armed := sub_armed
		burst_t = 0; sub_armed = false
		if level and can_fire() and ready:
			world.fire_sub(self, level, level == 3 and t < B.charge[2] + B.perfectWindow)
		elif not level and armed and can_fire() and ready and sub != "scatter":
			world.fire_sub(self, 0, false)

# The secondary button pressed with no enemy close enough for the combo. The Scatter fires at once; the
# others fire when it is let go (fire_marksman). A disc or well already out is called back or collapsed.
func press_sub(world) -> bool:
	if not sub_ready(world):
		buf.melee = 99; world.recall_sub(self, sub)
		return false
	if burst_cd > 0:
		return false
	buf.melee = 99
	if state != "normal":
		if state == "dodge":
			dodge = null
		set_state("normal")
	if sub == "scatter":
		world.fire_sub(self, 0, false)
	else:
		sub_armed = true
	return true

# A disc or a well: one of each at a time
func sub_ready(world) -> bool:
	return not ((sub == "disc" or sub == "well") and world.sub_out(self, sub))

func cycle_sub(world) -> void:
	var S: Array = Tune.C.SUBS
	sub = S[(S.find(sub) + 1) % S.size()]; sub_sw_cd = int(Tune.C.SUB.switchCd); burst_t = 0; sub_armed = false
	world.emit("subSwitch", { "p": self, "sub": sub })

# ---- Nova: the dodge (Marksman kit, on the parry button) --------------------------------------
func try_dodge(world) -> bool:
	if dodge_cd > 0 or (not on_ground and not air_dodge and not wall_sliding):
		return false
	var D: Dictionary = Tune.C.DODGE
	var mx: float = stick[0]
	# The way the stick points; with it centred, a backstep. Off a wall it always goes out from the wall.
	var dx: int = -wall_dir if wall_sliding else (U.sgn(mx) if absf(mx) > 0.3 else -facing)
	buf.parry = 99; dodge_cd = int(D.cd)
	if not on_ground:
		air_dodge = false
	dodge = { "dx": dx, "t": 0, "air": not on_ground, "speed": D.speed if on_ground else D.airSpeed, "perfect": false }
	crouch = false; fast_fall = false; dash_carry = false; wall_sliding = false
	set_state("dodge"); world.emit("dodge", { "p": self, "dx": dx, "air": dodge.air })
	return true

func state_dodge(cmd: Cmd, world) -> void:
	var D: Dictionary = Tune.C.DODGE
	var d = dodge
	if d == null:
		set_state("normal")
		return
	d.t += 1
	if d.t <= D.ticks - 4:
		vx = d.dx * d.speed * pow(D.keep, maxf(0, d.t - 3))
	else:
		vx = U.approach(vx, cmd.mx * Tune.C.CHARS[char].run * 0.6, 60 * Tune.DT)
	if d.air and d.t <= 8:
		vy = 0
	else:
		apply_gravity(cmd)
	# It can be cut short: a jump from the fourth tick, anything else once he is hittable again
	if d.t >= 4 and try_jump(cmd, world):
		dodge = null
		return
	if d.t > D.iframes and cancel_into(cmd, world, false, true, false, true, true):
		if state != "dodge":
			dodge = null
		return
	if d.t >= D.ticks:
		dodge = null; set_state("normal")

# ---- Ultimates -----------------------------------------------------------------------------
# Both triggers pulled together: each press counted from when it happened, both within ULT.chord ticks of
# each other and both still held (so pulling the second trigger long after the first never counts)
func track_chord(cmd: Cmd) -> void:
	chord_p = 0 if cmd.pressed.parry else mini(99, chord_p + 1)
	chord_f = 0 if cmd.pressed.fire else mini(99, chord_f + 1)

func chord_ready(cmd: Cmd) -> bool:
	return cmd.pressed.ult or (cmd.held.parry and cmd.held.fire and chord_p <= Tune.C.ULT.chord and chord_f <= Tune.C.ULT.chord)

func gain_ult(amount: float, world) -> void:
	if not (amount > 0) or state == "ult":
		return
	var was := ult
	ult = minf(Tune.C.ULT.max, ult + amount * boost_rate())
	if was < Tune.C.ULT.max and ult >= Tune.C.ULT.max:
		world.emit("ultReady", { "p": self })

# Level 4: the sustained beam. He braces (slow on the ground, hovering in the air), the beam follows the aim
# at a limited turn rate, and the world deals its damage (world.beam_tick). Dash or parry cut it short; a hit
# ends it (Combat.hit_player). (RAM's Breach Beam: M2.)
func beam_spec() -> Dictionary:
	return Tune.C.MARKSMAN.beam

func start_beam(world) -> void:
	var B := beam_spec()
	beam = { "t": B.ticks, "dx": aim_x, "dy": aim_y, "mult": focus_mult() * spend_overcharge(), "attach": attachment, "pulse": 0,
		"armor": {}, "family": { "focused": false, "rocketed": true, "perfect": false }, "segs": [] }
	charge_t = 0
	set_state("beam"); world.emit("beamStart", { "p": self, "attach": beam.attach, "over": beam.mult > focus_mult() })

func state_beam(cmd: Cmd, world) -> void:
	var B := beam_spec()
	var b = beam
	if b == null:
		set_state("normal")
		return
	var a0 := atan2(b.dy, b.dx)
	var da := U.wrap_angle(atan2(aim_y, aim_x) - a0)
	var a: float = a0 + clampf(da, -B.turn, B.turn)
	b.dx = cos(a); b.dy = sin(a)
	if absf(b.dx) > 0.2:
		facing = U.sgn(b.dx)
	# No push-back: on the ground he can creep along; in the air he hangs, sinking slowly, while it fires
	if on_ground:
		vx = U.approach(vx, cmd.mx * Tune.C.CHARS[char].run * B.slow, 40 * Tune.DT); vy = -0.5
	else:
		vx = U.approach(vx, 0, 20 * Tune.DT); vy = U.approach(vy, -B.hover, 40 * Tune.DT); fast_fall = false
	if try_parry(world) or try_dash(cmd, world):
		world.end_beam(self, "cancel")
		return
	world.beam_tick(self)
	b.t -= 1
	if b.t <= 0:
		world.end_beam(self, "done"); set_state("normal")

# Overcharge (from the Aegis) makes a charged release hit harder, at a cost; returns the damage multiplier
func spend_overcharge() -> float:
	if not (overcharge > 0):
		return 1.0
	overcharge = maxf(0, overcharge - Tune.C.AEGIS.over.cost)
	return Tune.C.AEGIS.over.dmg

# Charge stage from a charge counter: '', 'charging', 'L1', 'L2', 'perfect' (the release window), 'L3',
# or 'L4' (Marksman primary only: the beam)
static func stage_of(t: float, C: Array, win: float, l4 := INF) -> String:
	if t <= 0: return ""
	if t < C[0]: return "charging"
	if t < C[1]: return "L1"
	if t < C[2]: return "L2"
	if t >= l4: return "L4"
	return "perfect" if t < C[2] + win else "L3"

func charge_stage() -> String:
	var M: Dictionary = Tune.C.MARKSMAN
	if marksman():
		return stage_of(charge_t, M.charge, M.perfectWindow, M.beam.at)
	var N: Dictionary = Tune.C.NOVA
	return "" if charge_t <= 0 else ("charging" if charge_t < N.charge1 else ("L1" if charge_t < N.charge2 else "L2"))

func burst_stage() -> String:
	var B: Dictionary = Tune.C.MARKSMAN.burst
	return stage_of(burst_t, B.charge, B.perfectWindow)

# Rocket jump height (m, for a burst at his feet) earned by a shot charged for t ticks: it climbs steadily
# from charge level 1 to level 3; a Perfect Release reaches the highest. Attachments scale it.
static func rocket_height(t: float, attach: String, perfect: bool) -> float:
	var R: Dictionary = Tune.C.MARKSMAN.rocket
	var C: Array = Tune.C.MARKSMAN.charge
	var f := clampf((t - C[0]) / (C[2] - C[0]), 0, 1)
	return (R.perfect if perfect else R.h[0] + (R.h[1] - R.h[0]) * f) * R.attach.get(attach, 1.0)

# ---- Nova: bracer attachments and Focus ---------------------------------------------------

func cycle_attachment(world) -> void:
	var A: Array = Tune.C.MARKSMAN.attachments
	attachment = A[(A.find(attachment) + 1) % A.size()]; mode_cd = int(Tune.C.MARKSMAN.switchCd)
	world.emit("attach", { "p": self, "attach": attachment })

func focus_mult() -> float:
	return 1 + Tune.C.MARKSMAN.focus.dmgPer * floor(focus) if marksman() else 1.0

func gain_focus(amount: float, world) -> void:
	if not marksman():
		return
	var F: Dictionary = Tune.C.MARKSMAN.focus
	var before := floorf(focus)
	focus = minf(F.max, focus + amount); focus_t = int(F.decay)
	if floorf(focus) > before:
		world.emit("focusUp", { "p": self, "level": int(floor(focus)) })

func lose_focus(world) -> void:
	if focus >= 1:
		world.emit("focusLost", { "p": self })
	focus = 0; focus_t = 0

# Focus drains one level after a quiet stretch, then another every decayStep ticks
func tick_focus(_world) -> void:
	if focus <= 0:
		return
	focus_t -= 1
	if focus_t > 0:
		return
	focus = maxf(0, focus - 1); focus_t = int(Tune.C.MARKSMAN.focus.decayStep)

# ---- Shared hooks other characters fill in (M2) ----

func add_resolve(_amount: float) -> void:
	pass

func on_dealt_damage(_dmg: float, _is_melee: bool) -> void:
	pass

func break_veil(_world, _reason: String) -> void:
	pass

func parry_windows() -> Dictionary:
	return { "window": Tune.C.PARRY.window, "perfect": Tune.C.PARRY.perfect }

# How much faster this player charges, recharges and fills their bars right now: Overclock (a power-up),
# Tune-Up and an Amp Coil's field multiply, up to FIX.maxRate
func boost_rate() -> float:
	var F: Dictionary = Tune.C.FIX
	var k := 1.0
	if overclock_t > 0: k *= F.power.overclock.rate
	if tune_t > 0: k *= F.beam.tune
	if amp_k > 1: k *= amp_k
	return minf(F.maxRate, k)

# Plating: an overshield that takes damage before health (never past `cap`, and never taking away Plating
# someone already has above it)
func add_plate(amount: float, cap := -1.0) -> void:
	if cap < 0:
		cap = Tune.C.PLATE_MAX
	if plate < cap:
		plate = minf(cap, plate + amount)
