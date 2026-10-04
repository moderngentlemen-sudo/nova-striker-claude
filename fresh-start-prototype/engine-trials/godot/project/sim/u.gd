# Small maths helpers that behave exactly like the JavaScript the simulation was ported from
# (Math.sign, Math.round, the prototype's approach and clamp).
class_name U
extends RefCounted

# Math.sign as the prototype uses it: -1, 0 or 1, as an int
static func sgn(v: float) -> int:
	return 1 if v > 0 else (-1 if v < 0 else 0)

# Math.round: halves round up (toward +infinity), unlike Godot's round()
static func jround(v: float) -> int:
	return int(floor(v + 0.5))

static func approach(v: float, t: float, d: float) -> float:
	return minf(v + d, t) if v < t else maxf(v - d, t)

static func clampf_(v: float, a: float, b: float) -> float:
	return maxf(a, minf(b, v))

static func hypot(x: float, y: float) -> float:
	return sqrt(x * x + y * y)

# A sign that falls back when the value is 0 (the prototype's `sign(x) || fallback`)
static func sgn_or(v: float, fallback: int) -> int:
	var s := sgn(v)
	return s if s != 0 else fallback

# Wraps an angle difference to [-PI, PI] the way the prototype does (one step at a time)
static func wrap_angle(da: float) -> float:
	while da > PI:
		da -= TAU
	while da < -PI:
		da += TAU
	return da
