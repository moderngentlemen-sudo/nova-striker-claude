# The simulation's random numbers: mulberry32, a small seeded generator that is easy to reproduce exactly in
# JavaScript. tools/trace-js.mjs replaces Math.random with the same generator and seed, and the port draws
# numbers at the same points and in the same order as the prototype, so a scripted run plays out identically
# in both (the trace tests compare them tick by tick).
class_name Rng
extends RefCounted

const MASK := 0xFFFFFFFF

var state := 0

func _init(seed_ := 1) -> void:
	state = seed_ & MASK

# Math.imul: the low 32 bits of a 32-bit by 32-bit product (split so a 64-bit int never overflows)
static func imul(a: int, b: int) -> int:
	a &= MASK; b &= MASK
	var ah := (a >> 16) & 0xFFFF
	var al := a & 0xFFFF
	return (al * b + (((ah * b) & 0xFFFF) << 16)) & MASK

# A float in [0, 1), like Math.random()
func next() -> float:
	state = (state + 0x6D2B79F5) & MASK
	var t := imul(state ^ (state >> 15), 1 | state)
	t = ((t + imul(t ^ (t >> 7), 61 | t)) & MASK) ^ t
	return float((t ^ (t >> 14)) & MASK) / 4294967296.0

# Math.floor(Math.random() * n)
func below(n: int) -> int:
	return int(floor(next() * n))
