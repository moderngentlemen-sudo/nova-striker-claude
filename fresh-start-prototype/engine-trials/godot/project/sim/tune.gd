# Tuning and level data, exported from the three.js prototype by tools/export-data.mjs (config.js, level.js and
# the enemy and boss tables), so every number here is the prototype's. Loaded once, on first use.
#
# Tune.C holds config.js by its own names (Tune.C.MARKSMAN.lance[1].dmg, Tune.C.CHARS.nova.run ...), Tune.L the
# level data. Numbers are floats, as in JavaScript; object keys that are numbers ("1", "2", "3") become ints.
# Tune.settings is the live settings dictionary (config.js SETTINGS), reset with reset_settings().
class_name Tune
extends RefCounted

static var C: Dictionary = {}
static var L: Dictionary = {}
static var settings: Dictionary = {}
static var _loaded := false

static func ensure() -> void:
	if _loaded:
		return
	_loaded = true
	C = _read("res://data/tuning.json")
	L = _read("res://data/level.json")
	reset_settings()

static func reset_settings() -> void:
	settings = C.DEFAULT_SETTINGS.duplicate(true)

static func _read(path: String) -> Dictionary:
	var f := FileAccess.open(path, FileAccess.READ)
	assert(f != null, "missing data file " + path + " (run tools/export-data.mjs)")
	var data = JSON.parse_string(f.get_as_text())
	return _fix(data)

# "inf" back to INF, and numeric object keys to ints (so MARKSMAN.lance[1] works)
static func _fix(v):
	if v is Dictionary:
		var out := {}
		for k in v:
			var key = k
			if k is String and k.is_valid_int():
				key = int(k)
			out[key] = _fix(v[k])
		return out
	if v is Array:
		var a := []
		for x in v:
			a.append(_fix(x))
		return a
	if v is String:
		if v == "inf":
			return INF
		if v == "-inf":
			return -INF
	return v

# The numbers every system reads often
static var DT := 1.0 / 60.0
static func g(key: String):
	ensure()
	return C[key]
