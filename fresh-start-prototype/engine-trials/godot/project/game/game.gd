# The game (main.js): owns the World and steps it at the simulation's fixed 60 Hz in _physics_process (the
# project's physics tick rate is 60, so Godot's fixed step is the sim's step), hands each tick's events to the
# views, HUD and camera, and draws every frame in between with the interpolation fraction, so a 120 or 144 Hz
# screen glides instead of stepping. Pausing stops the tree; the pause menu keeps running.
#
# Command line (after `--`): --zone=<gym|arena|tower|skyline|foundry|undercity> starts in that zone,
# --boss=<warden|stormcaller> in a boss fight, --hold=<action,...> holds actions down (a quick demo for
# screenshots), --screenshot=<path> saves a frame after --frames=<n> (default 90) and quits. On the web, the
# page's query string takes the same options (?zone=foundry).
class_name Game
extends Node3D

@onready var level_view: LevelView = $LevelView
@onready var actors: Actors = $Actors
@onready var fx: Fx = $Fx
@onready var rig: CameraRig = $CameraRig
@onready var router: InputRouter = $InputRouter
@onready var hud: Hud = $Hud
@onready var pause_menu: PauseMenu = $PauseMenu

var world: World
var t := 0.0
var frames := 0
var shot_path := ""
var shot_frames := 90
var held_actions: Array = []

func _ready() -> void:
	Tune.ensure()
	PauseMenu.load_settings()
	world = World.new(int(Time.get_unix_time_from_system()) & 0x7fffffff)
	level_view.build(world)
	actors.setup(world)
	rig.setup(world)
	hud.setup(world, rig.camera)
	pause_menu.resume.connect(_resume)
	pause_menu.zone.connect(func(id): world.teleport(id); _resume())
	pause_menu.boss.connect(func(id): world.boss_rush(id); _resume())
	world.add_player("kbm", "nova")
	var args := Array(OS.get_cmdline_user_args())
	if OS.has_feature("web"):   # on the web the page's query string does the same: ?zone=foundry, ?boss=warden
		var q = JavaScriptBridge.eval("location.search")
		if q is String:
			for kv in (q as String).trim_prefix("?").split("&", false):
				args.append("--" + kv)
	for a in args:
		if a.begins_with("--zone="):
			world.teleport(a.trim_prefix("--zone="))
		elif a.begins_with("--boss="):
			world.boss_rush(a.trim_prefix("--boss="))
		elif a.begins_with("--hold="):
			held_actions = Array(a.trim_prefix("--hold=").split(","))
		elif a.begins_with("--screenshot="):
			shot_path = a.trim_prefix("--screenshot=")
		elif a.begins_with("--frames="):
			shot_frames = int(a.trim_prefix("--frames="))
	for a in held_actions:
		Input.action_press(a)
	_dispatch()

func _physics_process(_dt: float) -> void:
	var cmds := {}
	for p in world.players:
		cmds[p.slot] = router.sample(p, rig.camera)
	world.step(cmds)
	_dispatch()

func _dispatch() -> void:
	for ev in world.events:
		actors.on_event(ev)
		fx.on_event(ev)
		rig.on_event(ev)
		hud.on_event(ev)
	world.events.clear()

func _process(dt: float) -> void:
	t += dt
	var alpha := Engine.get_physics_interpolation_fraction()
	level_view.update(dt)
	actors.sync(alpha, dt, t)
	rig.update(alpha, dt)
	hud.update(dt)
	frames += 1
	if shot_path != "" and frames == shot_frames:
		await RenderingServer.frame_post_draw
		get_viewport().get_texture().get_image().save_png(shot_path)
		print("screenshot saved: ", shot_path)
		get_tree().quit()

func _unhandled_input(ev: InputEvent) -> void:
	if get_tree().paused:
		return
	if ev.is_action_pressed("pause") or ev.is_action_pressed("help"):
		get_viewport().set_input_as_handled()
		get_tree().paused = true
		pause_menu.open()

func _resume() -> void:
	pause_menu.close()
	get_tree().paused = false
	router.swallow_all()
