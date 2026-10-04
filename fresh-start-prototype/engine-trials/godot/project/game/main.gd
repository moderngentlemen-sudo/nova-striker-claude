# Entry point. `-- --tests` runs the simulation test suites headless and quits (exit code 1 on a failure);
# otherwise the game starts.
extends Node

func _ready() -> void:
	var args := OS.get_cmdline_user_args()
	for a in args:
		if a == "--tests" or a.begins_with("--tests="):
			var failures := TestRunner.run_all(a.trim_prefix("--tests=") if a.begins_with("--tests=") else "")
			get_tree().quit(1 if failures > 0 else 0)
			return
