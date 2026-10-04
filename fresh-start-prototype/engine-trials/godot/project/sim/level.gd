# The level's collision and queries: every route's boxes on one long 2D axis (x along the path, y up), the
# gates, the breakable pieces, and the helpers the whole simulation leans on (moving a body, headroom, the
# ground below a point, line of sight, ray casts). A port of level.js; each World owns one, so broken crates
# and closed gates belong to that world.
#
# Boxes: x0, x1, y0, y1, type ('s' solid, 'o' one-way, 'g' gate: solid while closed, 'd' breakable: solid
# until broken), tag, loot. Queries only look at boxes in the x buckets they touch, in the original order,
# so the results are the same as walking the whole list.
class_name Level
extends RefCounted

class Box:
	var id: int
	var x0: float
	var x1: float
	var y0: float
	var y1: float
	var type: String
	var tag: String
	var loot = null
	var hp := 0.0
	var broken := false
	var hit_t := -1

const BUCKET := 8.0

var boxes: Array[Box] = []
var gates := {}          # tag -> closed
var level_x0 := 0.0
var level_x1 := 0.0
var _buckets := {}       # bucket index -> Array of boxes (in id order)
var _bmin := 0
var _bmax := 0

func _init() -> void:
	Tune.ensure()
	var L := Tune.L
	for raw in L.BOXES:
		var b := Box.new()
		b.id = int(raw.id); b.x0 = raw.x0; b.x1 = raw.x1; b.y0 = raw.y0; b.y1 = raw.y1
		b.type = raw.type; b.tag = raw.tag; b.loot = raw.loot
		if b.type == "d":
			b.hp = L.DESTRUCT[b.tag].hp
		boxes.append(b)
	for k in L.GATES:
		gates[k] = false
	level_x0 = INF; level_x1 = -INF
	for b in boxes:
		level_x0 = minf(level_x0, b.x0); level_x1 = maxf(level_x1, b.x1)
	_bmin = int(floor(level_x0 / BUCKET)); _bmax = int(floor(level_x1 / BUCKET))
	for b in boxes:
		for i in range(int(floor(b.x0 / BUCKET)), int(floor(b.x1 / BUCKET)) + 1):
			if not _buckets.has(i):
				_buckets[i] = []
			_buckets[i].append(b)

# Every breakable piece back as it was (a reset to a checkpoint, a zone load)
func restore_boxes() -> void:
	for b in boxes:
		if b.type == "d":
			b.broken = false
			b.hp = Tune.L.DESTRUCT[b.tag].hp

# Boxes overlapping the x span [xa, xb], in their original order
func near(xa: float, xb: float) -> Array:
	var i0 := clampi(int(floor(xa / BUCKET)), _bmin, _bmax)
	var i1 := clampi(int(floor(xb / BUCKET)), _bmin, _bmax)
	if i0 == i1:
		return _buckets.get(i0, [])
	var seen := {}
	var out := []
	for i in range(i0, i1 + 1):
		for b in _buckets.get(i, []):
			if not seen.has(b.id):
				seen[b.id] = true
				out.append(b)
	out.sort_custom(func(a, c): return a.id < c.id)
	return out

func is_solid(b: Box) -> bool:
	return b.type == "s" or (b.type == "g" and gates[b.tag]) or (b.type == "d" and not b.broken)

static func _overlaps(x0: float, x1: float, y0: float, y1: float, b: Box) -> bool:
	return x0 < b.x1 and x1 > b.x0 and y0 < b.y1 and y1 > b.y0

# The breakable piece at a point, if any
func breakable_at(x: float, y: float, pad := 0.0) -> Box:
	for b in near(x - pad, x + pad):
		if b.type == "d" and not b.broken and x > b.x0 - pad and x < b.x1 + pad and y > b.y0 - pad and y < b.y1 + pad:
			return b
	return null

# Moves a body (x centre, y feet, w, h, vx, vy, drop_t) by its velocity for dt, horizontal first, then
# vertical. Sets on_ground, hit_wall (-1, 0, 1), hit_ceil and wall_dir (a wall within 6 cm, for wall play).
func move_body(body, dt: float) -> void:
	var hw: float = body.w / 2
	body.hit_wall = 0; body.hit_ceil = false
	var x_from: float = body.x
	# Horizontal
	body.x += body.vx * dt
	var cands := near(minf(x_from, body.x) - hw - 1.0, maxf(x_from, body.x) + hw + 1.0)
	for b in cands:
		if not is_solid(b):
			continue
		if _overlaps(body.x - hw, body.x + hw, body.y, body.y + body.h, b):
			if body.x < (b.x0 + b.x1) / 2:
				body.x = b.x0 - hw - 1e-4; body.hit_wall = 1
			else:
				body.x = b.x1 + hw + 1e-4; body.hit_wall = -1
			body.vx = 0.0
	# Vertical
	var prev_y: float = body.y
	body.y += body.vy * dt
	body.on_ground = false
	for b in cands:
		var solid := is_solid(b)
		if not solid and b.type != "o":
			continue
		if not _overlaps(body.x - hw, body.x + hw, body.y, body.y + body.h, b):
			continue
		if b.type == "o":
			if body.vy <= 0 and prev_y >= b.y1 - 0.02 and not (body.drop_t > 0):
				body.y = b.y1; body.vy = 0.0; body.on_ground = true
			continue
		if body.vy <= 0 and prev_y >= b.y1 - 0.08:
			body.y = b.y1; body.vy = 0.0; body.on_ground = true
		elif body.vy > 0:
			body.y = b.y0 - body.h - 1e-4; body.vy = 0.0; body.hit_ceil = true
		else:
			# Embedded (e.g. stood up under a ceiling): push toward the nearer vertical side
			var up: float = b.y1 - body.y
			var down: float = body.y + body.h - b.y0
			if up < down:
				body.y = b.y1; body.on_ground = true
			else:
				body.y = b.y0 - body.h
			body.vy = 0.0
	# Wall contact probe (for wall cling while airborne). Gates are energy barriers: nothing to cling to.
	body.wall_dir = 0
	if not body.on_ground:
		for b in cands:
			if not is_solid(b) or b.type == "g":
				continue
			var ya: float = body.y + 0.3
			var yb: float = body.y + body.h - 0.2
			if ya < b.y1 and yb > b.y0:
				if absf(body.x + hw - b.x0) < 0.06:
					body.wall_dir = 1
				elif absf(body.x - hw - b.x1) < 0.06:
					body.wall_dir = -1

# Can a body of height h stand at (x, y)?
func has_headroom(x: float, y: float, w: float, h: float) -> bool:
	var hw := w / 2
	for b in near(x - hw, x + hw):
		if not is_solid(b):
			continue
		if _overlaps(x - hw, x + hw, y + 0.05, y + h, b):
			return false
	return true

# The highest floor (solid or one-way top) at or below y, or -INF
func ground_below(x: float, y: float) -> float:
	var best := -INF
	for b in near(x, x):
		if not (is_solid(b) or b.type == "o"):
			continue
		if x > b.x0 and x < b.x1 and b.y1 <= y + 0.01 and b.y1 > best:
			best = b.y1
	return best

# Segment-vs-solid test for line of sight (slab method)
func segment_blocked(ax: float, ay: float, bx: float, by: float) -> bool:
	var dx := bx - ax
	var dy := by - ay
	for b in near(minf(ax, bx), maxf(ax, bx)):
		if not is_solid(b):
			continue
		var t := [0.0, 1.0]
		if _clip(-dx, ax - b.x0, t) and _clip(dx, b.x1 - ax, t) and _clip(-dy, ay - b.y0, t) and _clip(dy, b.y1 - ay, t):
			if t[0] <= t[1]:
				return true
	return false

static func _clip(p: float, q: float, t: Array) -> bool:
	if absf(p) < 1e-9:
		return q >= 0
	var r := q / p
	if p < 0:
		if r > t[1]:
			return false
		if r > t[0]:
			t[0] = r
	else:
		if r < t[0]:
			return false
		if r < t[1]:
			t[1] = r
	return true

# Slab test of a ray from (x, y) along (dx, dy) against a box: { t, nx, ny } (entry distance and the face
# normal hit), or an empty Dictionary. A ray that starts inside the box enters at 0.
static func ray_box_t(x: float, y: float, dx: float, dy: float, x0: float, y0: float, x1: float, y1: float) -> Dictionary:
	var tin := -INF
	var tout := INF
	var nx := 0.0
	var ny := 0.0
	if absf(dx) < 1e-9:
		if x <= x0 or x >= x1:
			return {}
	else:
		var ta := (x0 - x) / dx
		var tb := (x1 - x) / dx
		var tn := minf(ta, tb)
		if tn > tin:
			tin = tn; nx = -1.0 if dx > 0 else 1.0; ny = 0.0
		tout = minf(tout, maxf(ta, tb))
	if absf(dy) < 1e-9:
		if y <= y0 or y >= y1:
			return {}
	else:
		var ta := (y0 - y) / dy
		var tb := (y1 - y) / dy
		var tn := minf(ta, tb)
		if tn > tin:
			tin = tn; nx = 0.0; ny = -1.0 if dy > 0 else 1.0
		tout = minf(tout, maxf(ta, tb))
	if tin > tout or tout < 0:
		return {}
	return { "t": maxf(0.0, tin), "nx": nx, "ny": ny }

# The first solid surface along a ray (unit direction), up to range m:
# { t, x, y, nx, ny, wall (false when nothing is hit within range), box }
func ray_cast(x: float, y: float, dx: float, dy: float, range_: float) -> Dictionary:
	var best := range_
	var nx := 0.0
	var ny := 0.0
	var box: Box = null
	var xe := x + dx * range_
	for b in near(minf(x, xe), maxf(x, xe)):
		if not is_solid(b):
			continue
		var h := ray_box_t(x, y, dx, dy, b.x0, b.y0, b.x1, b.y1)
		if not h.is_empty() and h.t < best:
			best = h.t; nx = h.nx; ny = h.ny; box = b
	return { "t": best, "x": x + dx * best, "y": y + dy * best, "nx": nx, "ny": ny, "wall": best < range_, "box": box }

func point_in_solid(x: float, y: float) -> bool:
	for b in near(x, x):
		if is_solid(b) and x > b.x0 and x < b.x1 and y > b.y0 and y < b.y1:
			return true
	return false

# The top of a one-way platform crossed going down between two heights, if any (grenades land on them)
func one_way_top(x: float, y0: float, y1: float):
	for b in near(x, x):
		if b.type == "o" and x > b.x0 and x < b.x1 and y0 >= b.y1 - 0.02 and y1 < b.y1:
			return b.y1
	return null

func on_one_way(px: float, py: float) -> bool:
	for b in near(px, px):
		if b.type == "o" and absf(py - b.y1) < 0.03 and px > b.x0 and px < b.x1:
			return true
	return false

# ---- Routes, zones and the fall-out line ----

static func route_at(x: float) -> Dictionary:
	for r in Tune.L.ROUTES:
		if x >= r.x0 and x < r.x1:
			return r
	return Tune.L.ROUTES[0]

static func zone_at(x: float) -> Dictionary:
	for z in Tune.L.ZONES:
		if x >= z.x0 and x < z.x1:
			return z
	return Tune.L.ZONES[0]

static func kill_y_at(x: float) -> float:
	for k in Tune.L.KILL:
		if x >= k[0] and x < k[1]:
			return k[2]
	return Tune.L.KILL_Y
