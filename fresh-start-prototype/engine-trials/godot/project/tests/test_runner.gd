# Runs every suite in res://tests (the test_*.gd scripts with a run(t: TestKit) function) and prints a total,
# like the prototype's tests/run-all.mjs. `godot --headless --path project -- --tests` runs it and exits with
# code 1 if anything fails; `-- --tests=name` runs only the suites whose file name contains `name`.
class_name TestRunner
extends RefCounted

static func run_all(filter := "") -> int:
	var files := []
	for f in DirAccess.get_files_at("res://tests"):
		var name := f.trim_suffix(".remap")
		if name.begins_with("test_") and name.ends_with(".gd") and name != "test_kit.gd" and name != "test_runner.gd":
			if filter == "" or name.contains(filter):
				files.append(name)
	files.sort()
	var total_pass := 0
	var total_fail := 0
	for f in files:
		Tune.ensure()
		Tune.reset_settings()
		var t := TestKit.new()
		t.suite = f
		print("=== " + f)
		var script: GDScript = load("res://tests/" + f)
		var suite = script.new()
		suite.run(t)
		total_pass += t.passed
		total_fail += t.failed
		print("%s %s: %d passed%s" % ["ok  " if t.failed == 0 else "FAIL", f, t.passed, (", %d failed" % t.failed) if t.failed else ""])
	print("\n%d checks passed, %d failed" % [total_pass, total_fail])
	return total_fail
