// The start screen, the pause menu (every setting, the zones and boss fights, the players) and the controls
// screen (ui.js), with controller navigation: the D-pad or left stick moves to the nearest control in that
// direction (across the button rows and the settings grid, not just down a list); on a list or a slider,
// left/right changes its value instead. A presses a button or ticks a box (and cycles a list), B goes back to the
// game, LB jumps to the top and RB to the settings. Held directions repeat (Controls).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NovaStriker.Sim;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static NovaStriker.Sim.Cfg;

namespace NovaStriker.Game.UI
{
    public sealed partial class Ui
    {
        sealed class SettingDef { public string key, label; public (string v, string t)[] opts; public bool @bool; public float[] range; public Func<float, string> fmt; }
        static (string, string)[] O(params string[] kv) { var o = new (string, string)[kv.Length / 2]; for (int i = 0; i < o.Length; i++) o[i] = (kv[i * 2], kv[i * 2 + 1]); return o; }
        static readonly SettingDef[] SETTING_DEFS =
        {
            new SettingDef { key = "novaKit", label = "Nova's kit", opts = O("marksman", "Marksman: attachments, secondary weapons, dodge, skates", "sentinel", "Sentinel: Pass 1 kit") },
            new SettingDef { key = "novaDefense", label = "Nova's LT move, Marksman kit", opts = O("dodge", "Dodge", "shield", "Absorbing shield: blocks from in front; what it takes powers up his attacks") },
            new SettingDef { key = "novaParryStun", label = "Nova's perfect parry or shield block stuns the attacker", @bool = true },
            new SettingDef { key = "novaHead", label = "Nova's head (look only)", opts = O("helmet", "Full helmet, gold visor: knocked off at critical health", "bare", "Face, as in his concept art") },
            new SettingDef { key = "ramHead", label = "RAM's head (look only)", opts = O("helmet", "Full helmet, T visor: knocked off at critical health", "bare", "His face") },
            new SettingDef { key = "echoHead", label = "Echo's head (look only)", opts = O("helmet", "Full helmet, amber visor", "mask", "Survival mask", "bare", "Bare face") },
            new SettingDef { key = "echoKit", label = "Echo's kit", opts = O("hunter", "Hunter: blades, glaive, snares, reel", "pursuit", "Pursuit: Pass 1 kit") },
            new SettingDef { key = "echoBelt", label = "Echo's utility belt (snares), Hunter kit", opts = O("fire", "Tap fire (crouch + tap plants)", "lb", "Character action button (crouch + it plants); tap fire is a quick shot") },
            new SettingDef { key = "echoRanged", label = "Echo's ranged option, Pursuit kit only (Q-A test)", opts = O("A", "A: Tracer shot only", "B", "B: Bolts + Tracer", "C", "C: No ranged attack") },
            new SettingDef { key = "vbStop", label = "Velocity Break stop", opts = O("hard", "Hard stop", "keep30", "Keep 30% momentum") },
            new SettingDef { key = "vbRefund", label = "Velocity Break refunds air dash on hit", @bool = true },
            new SettingDef { key = "dashIframes", label = "Dash invulnerability (A/B test)", @bool = true },
            new SettingDef { key = "echoSpinStun", label = "Echo's deflect spin stun (light enemies; heavy about half)", range = new[] { 0.25f, 3, 0.05f }, fmt = v => $"{v:0.00} s" },
            new SettingDef { key = "impactFrames", label = "Impact frames on big moments (Q-C test)", @bool = true },
            new SettingDef { key = "impactStyle", label = "Impact frame style", opts = O("scifi", "Sci-fi hologram", "comic", "Comic ink (original)", "eclipse", "Eclipse", "shatter", "Shatter", "thunder", "Thunderclap", "sumi", "Sumi ink", "warp", "Gravity well") },
            new SettingDef { key = "impactDuration", label = "Impact frame duration", range = new[] { 0.3f, 5, 0.05f }, fmt = v => $"{v:0.00} s" },
            new SettingDef { key = "impactColor", label = "Impact frame colour", opts = O("style", "The style's own", "player", "Colour of the player who set it off", "character", "That player's character colour") },
            new SettingDef { key = "camera", label = "Camera projection", opts = O("persp", "Perspective", "ortho", "Orthographic") },
            new SettingDef { key = "fov", label = "Camera field of view", range = new[] { 24f, 50, 1 } },
            new SettingDef { key = "aimAssist", label = "Aim assist (gamepad 8-way aim)", @bool = true },
            new SettingDef { key = "lockOn", label = "Lock-on", @bool = true },
            new SettingDef { key = "lockMode", label = "Lock-on mode", opts = O("auto", "Automatic: nearest enemy, the lock-on button switches", "manual", "Press the lock-on button to lock") },
            new SettingDef { key = "dashCharge", label = "Charged dash (hold dash while standing still)", @bool = true },
            new SettingDef { key = "haptics", label = "Rumble and vibration", @bool = true },
            new SettingDef { key = "hapticStrength", label = "Rumble strength", range = new[] { 0f, 1, 0.05f } },
            new SettingDef { key = "p1Aim", label = "Keyboard player aims with", opts = O("mouse", "Mouse", "keys", "Movement keys (8-way)") },
            new SettingDef { key = "aiTeammates", label = "AI teammates (fill empty slots; a player joining takes one over)", opts = O("0", "Off", "1", "1", "2", "2", "3", "3") },
            new SettingDef { key = "aiSkill", label = "AI teammate skill", opts = O("rookie", "Rookie: slow to react, basic plays", "veteran", "Veteran: reads the fight, uses the whole kit", "elite", "Elite: sharp reactions, every advanced play") },
            new SettingDef { key = "difficulty", label = "Difficulty", opts = O("easy", "Easy", "normal", "Normal", "hard", "Hard") },
            new SettingDef { key = "barks", label = "Character lines (CP-09 test)", @bool = true },
            new SettingDef { key = "shake", label = "Screen shake", @bool = true },
            new SettingDef { key = "reflections", label = "Reflections on the deck (High and Ultra quality)", @bool = true },
            new SettingDef { key = "charModels", label = "Character models", opts = O("builtin", "Built-in rigs", "models", "3D models where available (Resources/NovaStriker/Models)") },
            new SettingDef { key = "quality", label = "Graphics quality", opts = O("ultra", "Ultra (adds ambient occlusion)", "high", "High (bloom, shadows, grading)", "low", "Low") },
            new SettingDef { key = "cineCamera", label = "Cinematic camera on big moments (bosses, finishers, ultimates)", @bool = true },
            new SettingDef { key = "slowMo", label = "Slow motion on big moments", @bool = true },
            new SettingDef { key = "timeOfDay", label = "Time of day", opts = O("route", "Each route's own light", "cycle", "Shifting: a full day every 12 minutes", "sunset", "Sunset", "night", "Night") },
            new SettingDef { key = "hdr", label = "HDR output (needs an HDR display with HDR on in the system)", @bool = true },
            new SettingDef { key = "volume", label = "Sound effects volume", range = new[] { 0f, 1, 0.05f } },
            new SettingDef { key = "music", label = "Music volume", range = new[] { 0f, 1, 0.05f } },
            new SettingDef { key = "fxPreset", label = "Effects preset", opts = O("cinematic", "Cinematic: everything", "balanced", "Balanced: lighter on the GPU", "classic", "Classic: the original effects", "custom", "Custom: the settings below") },
            new SettingDef { key = "fxExplosions", label = "Explosions (Custom preset)", opts = O("volumetric", "Volumetric: fire, smoke, embers, debris", "plasma", "Plasma: energy implosion and burst", "stylised", "Stylised: crisp toon puffs", "classic", "Classic") },
            new SettingDef { key = "fxProjectiles", label = "Projectiles (Custom preset)", opts = O("energy", "Energy: glowing cores and trails", "tracer", "Tracer: bright streaks", "classic", "Classic") },
            new SettingDef { key = "fxTrails", label = "Projectile trails (Custom preset)", opts = O("long", "Long", "short", "Short", "off", "Off") },
            new SettingDef { key = "fxDensity", label = "Particle density (Custom preset)", opts = O("high", "High", "medium", "Medium", "low", "Low") },
            new SettingDef { key = "fxDebris", label = "Debris (Custom preset)", opts = O("physics", "Physics: chunks bounce; enemies break into their own pieces", "simple", "Simple", "off", "Off") },
            new SettingDef { key = "fxSmoke", label = "Smoke (Custom preset)", opts = O("rich", "Rich", "light", "Light", "off", "Off") },
            new SettingDef { key = "fxLights", label = "Light from shots and blasts (Custom preset)", @bool = true },
            new SettingDef { key = "fxDistortion", label = "Heat haze and shockwave distortion (Custom preset)", @bool = true },
            new SettingDef { key = "fxDecals", label = "Scorch marks and scuffs (Custom preset)", @bool = true },
            new SettingDef { key = "fxSmokeLife", label = "Smoke duration (how long smoke hangs)", range = new[] { 0.25f, 3, 0.05f }, fmt = v => $"{v * 100:0}%" },
            new SettingDef { key = "fxFlash", label = "Screen flashes (beams, ultimates, big blasts)", @bool = true },
            new SettingDef { key = "fxScreen", label = "Screen effects (Custom preset)", opts = O("cinematic", "Cinematic: lens dirt, grain, aberration kicks", "clean", "Clean") },
            new SettingDef { key = "weather", label = "Weather and atmosphere", @bool = true },
            new SettingDef { key = "clouds", label = "Clouds", opts = O("volumetric", "Volumetric, lit by the sun", "classic", "Classic") },
            new SettingDef { key = "birds", label = "Birds", opts = O("realistic", "Realistic gulls", "classic", "Classic", "off", "Off") },
            new SettingDef { key = "enemyModels", label = "Enemy 3D models (where available)", @bool = true },
            new SettingDef { key = "perfOverlay", label = "Frame time readout", @bool = true },
            new SettingDef { key = "levelTiers", label = "Multi-tier levels (when a zone loads)", @bool = true },
            new SettingDef { key = "levelHazards", label = "Level hazards (when a zone loads)", @bool = true },
            new SettingDef { key = "depthLanes", label = "Depth lanes (when a zone loads; keys in Controls)", @bool = true },
        };
        // The pause menu's sections, in order, and which settings go in each (the rest are Game)
        static readonly string[] SECTIONS = { "Game", "Level features", "Effects", "Graphics", "Controls", "Audio" };
        static string SectionOf(string key) =>
            key.StartsWith("fx") || key == "weather" || key == "clouds" || key == "birds" || key == "enemyModels" || key == "perfOverlay" ? "Effects"
            : key == "levelTiers" || key == "levelHazards" || key == "depthLanes" ? "Level features"
            : key == "camera" || key == "fov" || key == "quality" || key == "reflections" || key == "charModels" || key == "cineCamera" || key == "slowMo" || key == "timeOfDay" || key == "hdr" || key == "shake" || key.StartsWith("impact") ? "Graphics"
            : key == "aimAssist" || key == "lockOn" || key == "lockMode" || key == "dashCharge" || key == "haptics" || key == "hapticStrength" || key == "p1Aim" ? "Controls"
            : key == "volume" || key == "music" ? "Audio" : "Game";
        static readonly (string kind, string a, string b, string c)[] HELP_ROWS =
        {
            ("R", "Move · crouch", "Left stick", "{k1:moveLeft}/{k1:moveRight} · {k1:moveDown}"),
            ("R", "Depth lanes (in marked stretches): hop to the lane behind or in front. Shots and strikes only reach their own lane; blasts reach all three", "{p:laneHop} (stick up = back, down = front)", "{k:laneBack} back · {k:laneFront} front"),
            ("R", "Jump · double jump · wall jump", "{p:jump}", "{k:jump}"),
            ("R", "Dash (8-way) · slide (down + dash)", "{p:dash}", "{k:dash}"),
            ("R", "Charged dash: hold dash while standing still, aim, let go. Each level goes further; level 2 is briefly invulnerable, level 3 cuts through enemies (afterimages show the level)", "Hold {p:dash}", "Hold {k:dash}"),
            ("R", "Wall slide and wall jump: hold toward a wall to slide down it. Jump while holding toward it (or neutral) to kick up it; hold away to leap off. You can shoot and strike while sliding", "Toward the wall · {p:jump}", "{k1:moveLeft}/{k1:moveRight} toward the wall · {k:jump}"),
            ("R", "Lock-on (automatic by default): whenever you have no target, the nearest enemy in sight is locked. Tap to switch to the next target; hold to let go (it stays off until you tap again). Homing shots go to the target and melee steps in toward it; free aim (right stick, mouse) still aims where you point. Settings: Lock-on mode, to lock only when you press", "{p:lock}", "{k:lock}"),
            ("S2", "Velocity Break (Echo's Hunter kit: the Dash Slash, a lunging cut that carries him through)", "Melee while dashing or sliding, or just after a dash", ""),
            ("R", "Melee · charged melee (hold, then let go)", "{p:melee} · hold {p:melee}", "{k:melee} · hold"),
            ("R", "Ground pound: in the air, melee with down held (or aimed straight down). Hold it to charge through three levels while you hang in the air; the landing throws enemies outward", "Down + {p:melee} in the air · hold", "{k1:moveDown} + melee in the air · hold"),
            ("R", "Rising attack: every character has their own. Nova: the Solar Uppercut (his boots fire and a hard-light fist drives up, three hits, a flare at the top; once per jump in the air). Echo: the Rising Glaive (a spinning uppercut that carries him up). RAM: the Hydraulic Uplift (his shield scoops up everything in front, sweeps shots out of the air over him, and bursts at the top; it reaches drones). Fix: Jack-Up (a pneumatic jack fires her up behind a wrench uppercut and stays as a spring pad any teammate can bounce on)", "Up + {p:melee}", "{k1:moveUp} + melee"),
            ("R", "Echo, Hunter kit: blade and glaive chain · Spin Slash in the air · Wall Slash on a wall · charged glaive swing that looses a crescent wave", "{p:melee} · up + {p:melee} in the air · {p:melee} on a wall · hold {p:melee}", "Melee · {k1:moveUp} + melee in the air · melee on a wall · hold"),
            ("R", "Echo deflects: his parry and his glaive swings knock enemy shots back at whoever fired them. A perfect deflect opens a Riposte: melee straight after", "{p:parry} · {p:melee}", "{k:parry} · melee"),
            ("R", "Fire · charge", "{p:fire} · hold {p:fire}", "{k:fire} · hold"),
            ("R", "Aim", "Right stick (free) or left stick (8-way)", "Mouse"),
            ("R", "Parry, Echo and Nova's Sentinel kit (first 4 frames are perfect)", "{p:parry}", "{k:parry}"),
            ("R", "Nova, Marksman kit: dodge. A quick hop the way you push the stick (a backstep with it centred), untouchable at the start; one in the air per jump. Dodge an attack at the last moment for a perfect dodge: enemies close by slow down, and you gain Overcharge and ultimate charge. You keep charging through it", "{p:parry}", "{k:parry}"),
            ("R", "Nova, Marksman kit, absorbing shield (Settings: Nova's LT move): hold to raise a hard-light shield where you aim. It blocks strikes, shots and blasts from in front, and every hit it takes is absorbed as charge: his attacks hit harder (+100% at a full charge, and it overfills to 150% for +150%) and he glows brighter the more he holds. Raise it just before a hit for a perfect block (no cost, more energy). Each block wears its stability down (it grows back once lowered; broken, he reels). The energy holds for 6 s after the last block, then fades; an unguarded hit spills half of it. Walk, jump, dash and fire (tap or charge) with it up; melee, or a Level 4 beam, lowers it", "Hold {p:parry}", "Hold {k:parry}"),
            ("S2", "Nova's perfect parry stuns (Settings): a perfect parry (Sentinel kit) or perfect shield block (Marksman kit) leaves the attacker stunned for a moment (heavy enemies more briefly; bosses only reel)", "Settings", ""),
            ("R", "Suit ability: Nova (Marksman kit) raises the hard-light Aegis for 5 s: it blocks every attack, and the damage it takes Overcharges his weapons (faster charging, harder hits). Press again to detonate it. Nova (Sentinel kit): Bulwark Pulse. Echo: his scarf ability for the current mode", "{p:sig}", "{k:sig}"),
            ("R", "Switch mode: Nova's bracer attachment (Lance, Volley, Arc, Prism) · Echo's scarf mode (Tether, Veil, Flare)", "{p:mode}", "{k:mode}"),
            ("R", "Nova, Marksman kit: fire · hold to charge the loaded attachment through three levels · let go on the flash after level 3 for a Perfect Release · keep holding to the Level 4 flash and let go for a sustained beam (steer it with your aim; dash or parry cuts it short)", "{p:fire} · hold {p:fire}", "{k:fire} · hold"),
            ("R", "Nova, Marksman kit: close to an enemy, melee is his hard-light combo (backhand, elbow, blast punch; an axe kick in the air). Otherwise it fires his secondary weapon; none has any recoil. Tap to fire, hold to charge through three levels. <b>Scatter</b>: point-blank pellets. <b>Grenade</b>: bounces, and bursts on its fuse or on an enemy (level 3 scatters bomblets). <b>Chain</b>: lightning that leaps from enemy to enemy, round shields, and stuns. <b>Disc</b>: flies out and back cutting everything (from level 2 it hovers at the end); press again to call it back. <b>Gravity Well</b>: pulls enemies in and holds them, swallows their shots, then collapses; press again to open it early, and again to collapse it", "{p:melee} · hold {p:melee}", "{k:melee} · hold"),
            ("R", "Switch Nova's secondary weapon: Scatter, Grenade, Chain, Disc, Gravity Well", "{p:sub}", "{k:sub}"),
            ("R", "Ultimate: the bar under your health fills as you fight (dealing and taking damage, kills, perfect parries, dodges, deflects and guards, and Fix's healing). When it is full, pull both triggers together. Nova: <b>Supernova</b>, a colossal beam you steer, then a nova of light. Echo: <b>Thousand Cuts</b>, a storm of blinking cuts on every enemy close by. RAM: <b>Siege Breaker</b>, the team is Fortified with Plating and he charges behind a colossal ram's head of hard light, scooping up everything in his path, then slams the pile down. Fix: <b>Overhaul</b>, a supply pod drops and pulses repair light across the screen (it brings back anyone who is down and hurts every enemy), then the team is Overclocked and Plated and her gadgets jump to level 3. Enemies freeze while it plays out and you can't be hurt", "{p:parry} + {p:fire} together", "{k:ult} (or parry + fire together)"),
            ("R", "Team ultimate: while a teammate's ultimate is being called (its name on screen), pull both triggers with a full bar to join in. Everyone who joins unleashes theirs together, stronger, then a team finisher hits every enemy on screen. Pairs have their own names: Echo + Nova, Eclipse Protocol; Nova + RAM, Starbreaker; Echo + RAM, Shatterpoint; Fix + Nova, Solar Overdrive; Echo + Fix, Razorwire; Fix + RAM, Heavy Metal; and two of the same, Binary Star, Twin Phantom, Stampede, Assembly Line. Three or more: Full Resonance", "{p:parry} + {p:fire} during the call", "{k:ult} during the call"),
            ("S2", "Nova: every shot bursts where it lands and splashes nearby enemies. A charged shot bursting on the ground or a wall close to you launches you: aim at your feet to rocket jump. The longer the charge, the higher you go (a gold line shows the height); a Perfect Release goes highest", "Aim down, charge, let go", ""),
            ("R", "Nova: light boosters. After your double jump, press and hold jump to hover and climb (the gold bar under his health)", "{p:jump} (third press)", "{k:jump} (third press)"),
            ("S2", "Nova's skates: you glide and keep your speed; reverse to carve to a stop; crouch at speed for a low glide", "Move as usual", ""),
            ("S2", "Nova's Focus: each charged shot that lands adds a level (a Perfect Release adds two) and more damage; getting hit clears it", "Shown under his health bar", ""),
            ("R", "Echo, Hunter kit: tap to throw a snare (down + tap plants one) · hold to scope the sniper rifle. Focus builds while you hold: the laser narrows, flickers until it rests on a target, holds solid on one and turns red at full focus. Let go for an instant shot; upper-body hits are critical, and at full focus it pierces everything in line, breaks armor and tags", "Tap {p:fire} · hold {p:fire}", "Tap / hold {k:fire}"),
            ("R", "Tether mode: tap pulls light enemies or zips you to heavy ones; hold reels a light enemy in or yanks a heavy one off balance", "{p:sig} (tap / hold)", "{k:sig} (tap / hold)"),
            ("R", "Veil mode: you fade out while you're not attacking and enemies lose track of you; your first strike from hiding is an ambush that staggers. Attacking or getting hit shows you again. Vanish hides you at once", "{p:sig}: Vanish", "{k:sig}: Vanish"),
            ("R", "Flare mode: nearby enemies go for you instead of your team; while they do, parries are easier and Resolve builds faster. Challenge pulls every enemy close by onto you", "{p:sig}: Challenge", "{k:sig}: Challenge"),
            ("H", "RAM, Vanguard (the tank)", "", ""),
            ("R", "Guard: hold to raise the Rampart, a tower shield. It blocks strikes, shots and blasts from in front (shockwaves along the floor still go under it) and covers everyone behind him. The damage comes off its Integrity (the blue bar), which grows back once he lowers it; broken, he reels and must wait for it. Raise it just before a hit for a Perfect Guard: no cost, a shot goes back the way it came, a striker reels. Aim up to hold it overhead. He walks slowly behind it and can jump with it up", "Hold {p:parry}", "Hold {k:parry}"),
            ("R", "Kinetic Release: every point the shield blocks is stored as Kinetic; fire while guarding lets it out as a cone of force, stronger the more is stored", "{p:fire} while guarding", "{k:fire} while guarding"),
            ("R", "Ram Charge (his dash): a shoulder charge behind the shield that scoops up light enemies and slams them into the next wall. Hold it while standing still for the Battering Ram: three levels, further and faster, and from level 2 it carries heavy enemies too and breaks armor. Nothing knocks him about while he charges", "{p:dash} · hold {p:dash}", "{k:dash} · hold {k:dash}"),
            ("R", "Breach Cannon: tap for a heavy slug; hold to charge a Breach Shot that punches through enemies (level 3 bursts at the end); keep holding to Level 4 for the Breach Beam, a sustained column of hard light that shoves everything in its line back", "{p:fire} · hold {p:fire}", "{k:fire} · hold"),
            ("R", "Melee: shield bash, edge strike, Piston Punch; a shield swat in the air; hold for the Seismic Slam (shockwaves run both ways along the floor). From a guard, melee is a quick shove. His ground pound lands wider and harder", "{p:melee} · hold {p:melee}", "{k:melee} · hold"),
            ("S2", "Stalwart: ordinary hits don't knock him about (heavy hits and blasts still do). He is big and slow, with lower jumps and much more health", "Always", ""),
            ("R", "Bulwark Wall: a hard-light wall in front of him for 8 s. Enemy shots stop at it and enemies can't get through until they break it; your team's shots pass through it boosted", "{p:sig}", "{k:sig}"),
            ("R", "Guardian Link: links him to the teammate who needs it most, leaping to their side if they are far. For 8 s he takes 60% of the damage they take, and they get Plating", "{p:mode}", "{k:mode}"),
            ("R", "Provoke: a war cry. Enemies close by turn on him for 4 s, he braces (takes 40% less), and enemies right beside him are shoved back", "{p:sub}", "{k:sub}"),
            ("H", "Fix, Mechanic (the support)", "", ""),
            ("R", "Patch Beam: hold to beam the teammate who needs it most. It heals fast, then adds Plating, and Tunes Up whoever it holds: they charge, recharge and fill their bars 1.5 times as fast. On a downed teammate it revives them from range; with no one near she welds herself. She moves slowly while it runs", "Hold {p:parry}", "Hold {k:parry}"),
            ("S2", "Field Mechanic: beside a downed teammate she revives three times as fast as anyone else, and whoever she brings back has 60% of their health", "Stand next to them", ""),
            ("R", "Gadgets (cost Scrap): build the selected one in front of her; building it again moves it. <b>Patch Pylon</b>: heals everyone in its field, and a downed teammate inside gets back up on their own. <b>Sentry</b>: shoots the nearest enemy in sight (rockets too at level 3). <b>Amp Coil</b>: teammates in its field charge and fill their bars faster. Two wrench hits raise a gadget a level (up to 3) and refresh it", "{p:sig} build · {p:mode} pick", "{k:sig} build · {k:mode} pick"),
            ("R", "Team commands to the AI teammates (Settings: AI teammates): <b>Attack my target</b> (all go for your lock-on target), <b>Cover me</b> (RAM shields you, Fix beams you, the others take what comes for you), <b>Regroup on me</b> (they close in for a few seconds), <b>Hold here</b> (they stand their ground where you were). The same command again cancels it", "D-pad up: tap Attack · hold Cover · D-pad down: tap Regroup · hold Hold", "{k:orderAttack} Attack · {k:orderCover} Cover · {k:orderRegroup} Regroup · {k:orderHold} Hold"),
            ("R", "Breakable pieces and power-ups along the routes: crates, barricades, glass and pillars break under attacks (a pillar only under heavy blows; RAM's charge goes through), and some crates hold a power-up. Power-ups wait along the way under a column of light: <b>Medkit</b>, <b>Plating</b>, <b>Overclock</b>, an <b>Ult Cell</b> (40 ultimate) and <b>Fury</b> (+50% damage and knockback for 12 s)", "Walk into it", "Walk into it"),
            ("R", "Power-ups (cost Scrap): melee with no enemy or gadget of hers close tosses the selected one to the nearest teammate in front (or drops it at her feet; anyone can pick it up). <b>Overclock</b>: everything charges, recharges and fills 1.6 times as fast for 10 s. <b>Plating</b>: an overshield over the health bar. <b>Medkit</b>: 40 health", "{p:melee} (nothing close) · {p:sub} pick", "{k:melee} · {k:sub} pick"),
            ("R", "Rivet Gun: tap for a burst of rivets; hold for a Hot Rivet that sticks in what it hits and bursts", "{p:fire} · hold {p:fire}", "{k:fire} · hold"),
            ("R", "Wrench: swing, backswing and a clanging overhead (close to an enemy or one of her gadgets); hold for the Torque Slam, a ring of sparks that stuns light enemies and drones. Her ground pound's landing heals teammates close by", "{p:melee} · hold {p:melee}", "{k:melee} · hold"),
            ("S2", "Scrap (the yellow bar): it trickles in, and comes from her hits and from enemies falling near her", "Shown under her health bar", ""),
            ("H", "Everyone", "", ""),
            ("R", "Swap character (Nova, Echo, RAM, Fix)", "D-pad left/right", "{k:pick1}–{k:pick4} / {k:swap}"),
            ("S2", "Revive a downed ally", "Stand next to them (faster with Fix)", ""),
        };
        static readonly string[] HELP_NOTES =
        {
            "New enemies: <b>Drones</b> fly above you and shoot (parry, or pull them down with Echo's Tether). <b>Mortars</b> lob shells at a magenta ring on the ground: you can't parry the burst, so move, or shoot the shell down with a charged shot. <b>Chargers</b> telegraph a heavy charge: perfect-parry it or jump over, and they daze themselves on walls.",
            "Controllers rumble with hits, charges and launches (Settings: Rumble). On Android phones the first player's phone can vibrate; iPhones do not support vibration from a web page, and a page embedded in another site may be blocked from it.",
            "Threats: a white glint means you can parry it. A double glint marks a heavy attack: a perfect parry negates it fully. A magenta jagged strip and a rising tone mean you cannot parry it; jump or move.",
            "<b>Close:</b> B, Start or View on a controller · <kbd>{k:help}</kbd> or <kbd>Esc</kbd> on the keyboard, or a click outside this card. The game waits while this is open.",
        };

        static FieldInfo Field(string key) => typeof(Settings).GetField(key);
        static object GetSetting(string key) => Field(key).GetValue(SETTINGS);
        static void SetSetting(string key, object v)
        {
            var f = Field(key);
            if (f.FieldType == typeof(double)) f.SetValue(SETTINGS, Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture));
            else if (f.FieldType == typeof(bool)) f.SetValue(SETTINGS, Convert.ToBoolean(v));
            else f.SetValue(SETTINGS, Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture));
            SettingsStore.Save();
        }
        static string SettingText(string key)
        {
            var v = GetSetting(key);
            return v is double d ? d.ToString(System.Globalization.CultureInfo.InvariantCulture) : Convert.ToString(v);
        }

        // ---- Start ----
        Card start; Text notice; Image pulse;
        void BuildStart()
        {
            start = new Card(root, "start", 760);
            start.Eyebrow("FRESH-START TRACK · UNITY PORT · PASS 1");
            start.H1("Nova Striker");
            start.Para("Movement, combat and co-op feel test. Placeholder art and sound; nothing here is final.", 15, Pal.muted);
            var join = W.Rect(start.content, "join"); join.gameObject.AddComponent<LayoutElement>().preferredHeight = 34;
            pulse = W.Box(join, Pal.warm, "pulse"); W.At(pulse.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(6, 0), new Vector2(12, 12));
            var jt = W.Txt(join, "CLICK, PRESS ANY KEY, OR PRESS A GAMEPAD BUTTON TO JOIN", 20, Pal.text, true, FontStyle.Bold, TextAnchor.MiddleLeft); W.Fill(jt.rectTransform).offsetMin = new Vector2(30, 0);
            var cols = W.Rect(start.content, "cols"); var hl = cols.gameObject.AddComponent<HorizontalLayoutGroup>(); hl.spacing = 18; hl.childControlWidth = hl.childControlHeight = true; hl.childForceExpandWidth = true; hl.childForceExpandHeight = false;
            void Col(string head, string[] lines)
            {
                var c = W.Rect(cols, "col"); var vl = c.gameObject.AddComponent<VerticalLayoutGroup>(); vl.spacing = 4; vl.childControlWidth = vl.childControlHeight = true; vl.childForceExpandHeight = false;
                start.Para(head.ToUpperInvariant(), 15, Pal.muted, true, FontStyle.Bold, c);
                foreach (var l in lines) start.Para("•  " + Bindings.Expand(l), 14, Pal.text, false, FontStyle.Normal, c);
            }
            Col("Keyboard + mouse", new[] { "<kbd>{k1:moveLeft}</kbd><kbd>{k1:moveRight}</kbd> move · <kbd>{k1:moveDown}</kbd> crouch · <kbd>{k:jump}</kbd> jump · <kbd>{k:dash}</kbd> dash", "{k:fire}: fire (hold to charge) · {k:melee}: melee",
                "<kbd>{k1:parry}</kbd> parry / dodge / guard / beam · <kbd>{k1:sig}</kbd> suit ability · <kbd>{k1:mode}</kbd> mode", "<kbd>{k1:sub}</kbd> character action · <kbd>{k1:lock}</kbd> switch target · <kbd>{k1:ult}</kbd> ultimate · <kbd>{k:pick1}</kbd>–<kbd>{k:pick4}</kbd> character" });
            Col("Gamepad", new[] { "Left stick move · Right stick aim · {p:lock} switch target", "{p:jump} jump · {p:dash} dash · {p:melee} melee · {p:sig} suit ability · {p:mode} mode", "{p:fire} fire · {p:parry} parry / dodge / guard / beam · {p:sub} character action", "{p:parry} + {p:fire} ultimate · D-pad swap character · Start pause" });
            foreach (var f in START_NOTES) start.Para(Bindings.Expand(f), 13, Pal.muted);
            notice = start.Para("", 13, Pal.warm); notice.gameObject.SetActive(false);
            var qrow = start.Row();
            Btn.Make(qrow, "Quit game", QuitGame, 13, true);
        }
        static readonly string[] START_NOTES =
        {
            "This is the Unity port of Version 13: the same game, simulation and look, on URP. New in Version 13: a new look. Armour that reflects its surroundings, ink outlines, each route with its own light and colour grade, riveted and plated surfaces, shockwaves on the biggest hits, and an Ultra setting with ambient occlusion. Character models can be brought in (Settings; see the README).",
            "New in Version 12: team commands for AI teammates (D-pad up/down, or <kbd>{k1:orderAttack}</kbd> <kbd>{k1:orderCover}</kbd> <kbd>{k1:orderRegroup}</kbd> <kbd>{k1:orderHold}</kbd>), two long new levels that wind through 3D (the <b>Helix Foundry</b> and the <b>Undercity Descent</b>, in <kbd>Esc</kbd>/Start), breakable crates, barricades, glass and pillars, and power-ups along the way.",
            "New in Version 11: smarter AI teammates with a skill setting, a controller-friendly pause menu, RAM's Level 4 <b>Breach Beam</b> (keep holding fire) and a shield that cracks and shatters, seven impact frame styles that can take your player colour, and Echo's snares on LB as an option. All in <kbd>Esc</kbd>/Start.",
            "New in Version 10: two new characters. <b>RAM</b>, the tank: hold LT to raise his tower shield (it blocks, covers everyone behind him and stores Kinetic; fire while guarding releases it), a dash that plows enemies into walls, a hard-light wall, a guardian link and a war cry. <b>Fix</b>, the support: hold LT for a beam that heals, revives from range and tunes teammates up (faster charging and bars), gadgets on Y, power-ups tossed with X. Players join as Nova, Echo, RAM and Fix; swap with the D-pad or <kbd>1</kbd>–<kbd>4</kbd>.",
            "Up to four players: extra gamepads join by pressing any button. <kbd>{k1:help}</kbd>/View shows every control and lets you change them; <kbd>Esc</kbd>/Start opens settings, zones and the boss fights.",
        };
        public void HideStart() => start.visible = false;
        public void GamepadNotice(bool show) { notice.gameObject.SetActive(show); notice.text = "No gamepad is connected. Keyboard and mouse work; connect a controller to play with one."; }

        // ---- Pause / settings ----
        Card pause; RectTransform playerList; World pauseWorld;
        readonly List<Selectable> focusables = new List<Selectable>();
        Selectable focusEl; int focusIdx; bool padnav; RectTransform ring; Card settingsCard;
        void BuildPause()
        {
            pause = new Card(root, "pause", 940);
            pause.Eyebrow("PAUSED");
            pause.H2("Settings and test toggles");
            var row = pause.Row();
            Btn.Make(row, "Resume", () => H.resume());
            foreach (var (label, id) in new[] { ("Movement Gym", "gym"), ("Concourse Lock", "arena"), ("Storm Spire Climb", "tower"), ("Skyline Relay", "skyline"), ("Helix Foundry", "foundry"), ("Undercity Descent", "undercity") })
                Btn.Make(row, label, () => H.zone(id));
            Btn.Make(row, "Boss: Lockwarden", () => H.boss("warden"));
            Btn.Make(row, "Boss: Stormcaller", () => H.boss("stormcaller"));
            Btn.Make(row, "Controls", OpenControls);
            // Quit asks for a second press, so a stray A or click doesn't end the session
            quitBtn = Btn.Make(row, "Quit game", () =>
            {
                if (quitArmed > 0) { QuitGame(); return; }
                quitArmed = 3; quitBtn.GetComponentInChildren<Text>().text = "PRESS AGAIN TO QUIT"; Btn.SetOn(quitBtn, true);
            });
            pause.Para("<b>Controller:</b> D-pad or left stick to move · left/right changes a list or slider · A to select · B to resume · LB top · RB settings", 13, Pal.muted);
            playerList = W.Rect(pause.content, "players");
            var pl = playerList.gameObject.AddComponent<VerticalLayoutGroup>(); pl.spacing = 6; pl.childControlWidth = pl.childControlHeight = true; pl.childForceExpandHeight = false;
            foreach (var section in SECTIONS)
            {
                var defs = SETTING_DEFS.Where(d => SectionOf(d.key) == section).ToArray();
                if (defs.Length == 0) continue;
                pause.Para("<b>" + section.ToUpperInvariant() + "</b>", 15, Pal.text);
                var grid = W.Rect(pause.content, "settings " + section);
                var gl = grid.gameObject.AddComponent<GridLayoutGroup>(); gl.cellSize = new Vector2(434, 46); gl.spacing = new Vector2(18, 8); gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount = 2;
                foreach (var d in defs)
                {
                    var cell = W.Rect(grid, "setting " + d.key);
                    W.Fill(W.Box(cell, Pal.C("#ffffff", 0.04f), "bg").rectTransform);
                    var lab = W.Txt(cell, d.label, 13, Pal.text, false, FontStyle.Normal, TextAnchor.MiddleLeft); W.Fill(lab.rectTransform);
                    lab.rectTransform.offsetMin = new Vector2(10, 0); lab.rectTransform.offsetMax = new Vector2(-190, 0);
                    Selectable ctl;
                    if (d.@bool)
                    {
                        Button b = null;
                        b = Btn.Make(cell, (bool)GetSetting(d.key) ? "On" : "Off", () => { bool v = !(bool)GetSetting(d.key); SetSetting(d.key, v); b.GetComponentInChildren<Text>().text = v ? "ON" : "OFF"; Btn.SetOn(b, v); }, 13, true);
                        Btn.SetOn(b, (bool)GetSetting(d.key)); ctl = b;
                    }
                    else if (d.range != null)
                        ctl = RangeCtl.Make(cell, d.range[0], d.range[1], d.range[2], Convert.ToSingle(GetSetting(d.key)), d.fmt, v => SetSetting(d.key, (double)Math.Round(v, 4)));
                    else ctl = Cycler.Make(cell, d.opts, SettingText(d.key), v => SetSetting(d.key, v));
                    var cr = (RectTransform)ctl.transform;
                    var le = ctl.GetComponent<LayoutElement>();
                    W.At(cr, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-8, 0), new Vector2(d.@bool ? 64 : 176, le.preferredHeight));
                }
            }
            pause.Para("Character lines are placeholder writing for the CP-09 test, not canon. Settings are remembered on this machine (PlayerPrefs).", 13, Pal.muted);
            pause.visible = false;
            // The controller focus ring
            ring = W.Rect(root, "focus ring"); var ri = ring.gameObject.AddComponent<Image>(); ri.sprite = RingSprite; ri.type = Image.Type.Sliced; ri.color = Pal.warm; ri.raycastTarget = false;
            ring.anchorMin = ring.anchorMax = new Vector2(0.5f, 0.5f); ring.gameObject.SetActive(false);
        }
        static Sprite ringSprite;
        static Sprite RingSprite
        {
            get
            {
                if (ringSprite != null) return ringSprite;
                const int S = 32, R = 8, T = 3;
                var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[S * S];
                for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Max(R - x - 0.5f, x + 0.5f - (S - R))), dy = Mathf.Max(0, Mathf.Max(R - y - 0.5f, y + 0.5f - (S - R)));
                    float d = Mathf.Sqrt(dx * dx + dy * dy), edge = (dx > 0 && dy > 0) ? R - d : Mathf.Min(Mathf.Min(x, S - 1 - x), Mathf.Min(y, S - 1 - y)) + 0.5f;
                    float a = Mathf.Clamp01(edge + 0.5f) * Mathf.Clamp01(T - edge + 0.5f);
                    px[y * S + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
                t.SetPixels32(px); t.Apply();
                return ringSprite = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(R + 1, R + 1, R + 1, R + 1));
            }
        }

        Button quitBtn; float quitArmed;
        void DisarmQuit() { quitArmed = 0; quitBtn.GetComponentInChildren<Text>().text = "QUIT GAME"; Btn.SetOn(quitBtn, false); }
        public static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void SetPaused(bool on, World world)
        {
            paused = on; pause.visible = on; pauseWorld = world;
            DisarmQuit();
            if (on) { RenderPlayerList(world); Collect(); focusIdx = 0; focusEl = focusables.FirstOrDefault(); pause.scroll.verticalNormalizedPosition = 1; padnav = true; }
        }
        // The menu the controller moves through: the controls screen when it is open, else the pause menu
        Card NavCard => helpOpen ? help : pause;
        void Collect() { focusables.Clear(); focusables.AddRange(NavCard.content.GetComponentsInChildren<Selectable>().Where(s => s.gameObject.activeInHierarchy && s.interactable)); }
        void RenderPlayerList(World world)
        {
            foreach (Transform c in playerList) UnityEngine.Object.Destroy(c.gameObject);
            foreach (var p in world.players)
            {
                var row = pause.Row(playerList);
                string dev = Bots.IsBot(p) ? "AI teammate" : p.device == "kbm" ? "Keyboard + mouse" : "Gamepad";
                var mark = W.Txt(row, $"<b><color={W.Hex(PC(p.slot))}>{PLAYER_MARKS[p.slot]} P{p.slot + 1}</color></b> {dev}", 15, Pal.muted, false, FontStyle.Normal, TextAnchor.MiddleLeft);
                var le = mark.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = 220; le.preferredHeight = 26;
                foreach (var c in ROSTER)
                {
                    var b = Btn.Make(row, CHARS[c].name, () => { H.pick(p, c); Rebuild(); }, 13, true);
                    Btn.SetOn(b, p.@char == c);
                }
                if (p.slot != 0) { var r = Btn.Make(row, "Remove", () => { H.remove(p); Rebuild(); }, 13, true); r.GetComponent<Image>().color = new Color(0, 0, 0, 0); }
            }
            void Rebuild()
            {
                int at = focusables.IndexOf(focusEl);
                RenderPlayerList(world); Canvas.ForceUpdateCanvases();
                Collect();   // (a character pick redraws the player list: keep the focus on the same spot)
                if (at >= 0 && at < focusables.Count) focusEl = focusables[at];
            }
        }

        public void MenuNav(MenuEv ev)
        {
            if (!paused && !helpOpen) return;
            Collect();
            if (focusables.Count == 0) return;
            padnav = true;
            var el = focusables.Contains(focusEl) ? focusEl : null;
            void Go(Selectable to) { if (to == null) return; focusEl = to; focusIdx = focusables.IndexOf(to); ScrollTo(to); }
            if (el == null && (ev.type == "up" || ev.type == "down" || ev.type == "swap" || ev.type == "confirm")) { Go(focusables[Mathf.Clamp(focusIdx, 0, focusables.Count - 1)]); return; }
            if (ev.type == "swap" && el is Cycler cy) { cy.Step(ev.dir); return; }
            if (ev.type == "swap" && el is RangeCtl rc) { rc.SetValue(rc.value + rc.step * ev.dir); return; }
            if (ev.type == "up" || ev.type == "down" || ev.type == "swap")
            {
                float dx = ev.type == "swap" ? ev.dir : 0, dy = ev.type == "up" ? -1 : ev.type == "down" ? 1 : 0;
                var a = Center(el); Selectable best = null; float bs = float.MaxValue;
                foreach (var c in focusables)
                {
                    if (c == el) continue;
                    var b = Center(c);
                    float along = (b.x - a.x) * dx + (b.y - a.y) * dy, across = Mathf.Abs((b.x - a.x) * dy) + Mathf.Abs((b.y - a.y) * dx);
                    if (along <= 4) continue;
                    float s = along + across * (dy != 0 ? 0.6f : 2.5f);   // up/down: the next row, then the nearest in it
                    if (s < bs) { bs = s; best = c; }
                }
                Go(best);
            }
            else if (ev.type == "confirm")
            {
                if (el is Button b) b.onClick.Invoke();
                else if (el is Cycler c) c.Step(1, true);
                else if (el is RangeCtl r) { float v = r.value + r.step * 2; r.SetValue(v > r.max + 1e-4f ? r.min : v); }
            }
            else if (ev.type == "prevTab") Go(focusables[0]);
            else if (ev.type == "nextTab") Go(focusables.FirstOrDefault(s => s is Cycler || s is RangeCtl || s.transform.parent.name.StartsWith("setting")) ?? focusables[focusables.Count - 1]);
            else if (ev.type == "back") H.resume();
        }
        // A control's centre in screen space, y downward (as the prototype's client rects)
        static Vector2 Center(Selectable s)
        {
            var r = (RectTransform)s.transform; var c = new Vector3[4]; r.GetWorldCorners(c);
            var m = (c[0] + c[2]) / 2; return new Vector2(m.x, -m.y);
        }
        void ScrollTo(Selectable s)
        {
            var card = NavCard; var vp = card.scroll.viewport; var r = (RectTransform)s.transform;
            var c = new Vector3[4]; r.GetWorldCorners(c); var v = new Vector3[4]; vp.GetWorldCorners(v);
            float scale = canvas.scaleFactor, dy = 0;
            if (c[1].y > v[1].y) dy = c[1].y - v[1].y + 8 * scale;        // above the view: scroll up
            else if (c[0].y < v[0].y) dy = c[0].y - v[0].y - 8 * scale;   // below: scroll down
            if (dy != 0) { var p = card.content.anchoredPosition; p.y -= dy / scale; card.content.anchoredPosition = p; }
        }

        void UpdateMenus(float dt)
        {
            pulse.color = new Color(Pal.warm.r, Pal.warm.g, Pal.warm.b, 0.55f + 0.45f * Mathf.PingPong(Time.unscaledTime / 0.7f, 1));
            if (quitArmed > 0) { quitArmed -= dt; if (quitArmed <= 0) DisarmQuit(); }
            // The mouse takes over: no controller focus ring; a click focuses what it clicked
            var ms = UnityEngine.InputSystem.Mouse.current;
            if (ms != null && ms.delta.ReadValue().sqrMagnitude > 4) padnav = false;
            var sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (sel != null) { var s = sel.GetComponent<Selectable>(); if (s != null && focusables.Contains(s)) focusEl = s; }
            UpdateControls(dt);
            bool show = (paused || helpOpen) && padnav && focusEl != null && focusEl.gameObject.activeInHierarchy;
            ring.gameObject.SetActive(show);
            if (show)
            {
                var r = (RectTransform)focusEl.transform; var c = new Vector3[4]; r.GetWorldCorners(c);
                ring.position = (c[0] + c[2]) / 2; ring.sizeDelta = new Vector2((c[2].x - c[0].x) / canvas.scaleFactor + 10, (c[2].y - c[0].y) / canvas.scaleFactor + 10);
                ring.SetAsLastSibling();
            }
        }

        // ---- Controls (the help screen): change the controls, or read what each button does ----
        Card help; RectTransform bindPanel, guidePanel; Button tabEdit, tabGuide; Text bindStatus, helpTitle; bool guideTab;
        readonly List<(Text t, string raw, bool upper)> guideCells = new List<(Text, string, bool)>();
        readonly List<(Button b, string id, int slot)> bindCells = new List<(Button, string, int)>();
        int bindsShown = -1;   // the Bindings.Version the screen shows
        // A binding being changed: the action, its slot (0, 1 keys; 2 mouse; 3 gamepad), how long it waits
        string listenId; int listenSlot, listenFrame; float listenT, clickLock;
        public bool Listening => listenId != null;
        static readonly string[] SLOT_HEADS = { "Key", "Key 2", "Mouse", "Gamepad" };

        void BuildHelp()
        {
            help = new Card(root, "help", 940);
            var head = W.Rect(help.content, "head"); head.gameObject.AddComponent<LayoutElement>().preferredHeight = 70;
            var eb = W.Txt(head, "CONTROLS", 13, Pal.warm, true, FontStyle.Bold); W.At(eb.rectTransform, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(400, 18));
            helpTitle = W.Txt(head, "CHANGE YOUR CONTROLS", 30, Pal.text, true, FontStyle.Bold); W.At(helpTitle.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -20), new Vector2(560, 40));
            var badge = W.Rect(head, "close"); W.At(badge, new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(300, 56));
            W.Fill(W.Box(badge, Pal.C("#ffffff", 0.08f)).rectTransform);
            var bt = W.Txt(badge, "<b><color=#ffb547>B</color></b> OR <b><color=#ffb547>VIEW</color></b> TO CLOSE\n<size=12><color=#a8b7ca>Esc on the keyboard · A selects · LB / RB tabs</color></size>", 16, Pal.text, true, FontStyle.Bold, TextAnchor.MiddleRight);
            W.Fill(bt.rectTransform).offsetMax = new Vector2(-12, 0);
            var tabs = help.Row();
            tabEdit = Btn.Make(tabs, "Change controls", () => ShowTab(false), 13, true);
            tabGuide = Btn.Make(tabs, "What each button does", () => ShowTab(true), 13, true);
            Btn.Make(tabs, "Reset all to defaults", () => { StopListen(null); Bindings.ResetAll(); bindStatus.text = "Every control is back to its default."; }, 13, true);
            VerticalLayoutGroup Panel(RectTransform r) { var v = r.gameObject.AddComponent<VerticalLayoutGroup>(); v.spacing = 6; v.childControlWidth = v.childControlHeight = true; v.childForceExpandHeight = false; v.childForceExpandWidth = true; return v; }
            bindPanel = W.Rect(help.content, "bindings"); Panel(bindPanel);
            guidePanel = W.Rect(help.content, "guide"); Panel(guidePanel).spacing = 10;
            BuildBinds();
            Row3("Action", "Gamepad", "Keyboard + mouse", true, false);
            foreach (var (kind, a, b, c) in HELP_ROWS)
            {
                if (kind == "H") Row3(a, "", "", true, true);
                else if (kind == "S2") Row3(a, b, null, false, false);
                else Row3(a, b, c, false, false);
            }
            foreach (var n in HELP_NOTES) guideCells.Add((help.Para(Bindings.Expand(n), 13, Pal.muted, false, FontStyle.Normal, guidePanel), n, false));
            // a click outside the card closes it (clicks on the card stay on it)
            help.overlay.gameObject.AddComponent<Button>().onClick.AddListener(() => { if (!Listening && Time.unscaledTime >= clickLock) ToggleHelp(false); });
            help.card.gameObject.AddComponent<ClickSink>();
            ShowTab(false);
            help.visible = false;
        }

        void BuildBinds()
        {
            help.Para("Click a box (or move to it and press A), then press the key, mouse button or gamepad button you want. <b>Esc</b> cancels; <b>Delete</b> or <b>Backspace</b> clears it. Each input belongs to one action: giving it to another moves it there.", 13, Pal.muted, false, FontStyle.Normal, bindPanel);
            bindStatus = help.Para("", 14, Pal.warm, false, FontStyle.Bold, bindPanel);
            RectTransform Row()
            {
                var row = W.Rect(bindPanel, "bind row"); var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>(); hl.spacing = 8; hl.childControlWidth = hl.childControlHeight = true; hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
                hl.childAlignment = TextAnchor.MiddleLeft; hl.padding = new RectOffset(4, 4, 2, 2);
                return row;
            }
            void Fixed(RectTransform row, Text t, float w) { var le = t.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = w; le.flexibleWidth = 0; }
            var hr = Row();
            Fixed(hr, W.Txt(hr, "ACTION", 13, Pal.muted, true, FontStyle.Bold), 300);
            foreach (var h in SLOT_HEADS) Fixed(hr, W.Txt(hr, h.ToUpperInvariant(), 13, Pal.muted, true, FontStyle.Bold, TextAnchor.MiddleCenter), 120);
            foreach (var g in Bindings.GROUPS)
            {
                help.Para("<b>" + g.ToUpperInvariant() + "</b>", 14, Pal.C("#e8f0fa"), false, FontStyle.Normal, bindPanel);
                foreach (var d in Bindings.DEFS.Where(x => x.group == g))
                {
                    var row = Row();
                    Fixed(row, W.Txt(row, d.label, 14, Pal.text, false, FontStyle.Normal, TextAnchor.MiddleLeft), 300);
                    for (int slot = 0; slot < 4; slot++)
                    {
                        bool has = slot <= 1 ? d.kb : slot == 2 ? d.ms : d.pd;
                        if (!has) { var sp = W.Rect(row, "none"); var sle = sp.gameObject.AddComponent<LayoutElement>(); sle.preferredWidth = 120; sle.preferredHeight = 30; continue; }
                        string id = d.id; int sl = slot;
                        var b = Btn.Make(row, "-", () => StartListen(id, sl), 13, true);
                        var le = b.GetComponent<LayoutElement>(); le.preferredWidth = 120; le.preferredHeight = 30;
                        bindCells.Add((b, id, sl));
                    }
                    string rid = d.id;
                    var rb = Btn.Make(row, "Reset", () => { StopListen(null); Bindings.Reset(rid); bindStatus.text = Bindings.Of(rid).label + ": back to its default."; }, 12, true);
                    rb.GetComponent<Image>().color = new Color(0, 0, 0, 0);
                }
            }
            help.Para("<b>Always the same:</b> Esc pauses; Enter and the arrow keys work the menus; ` opens the debug view. On a gamepad: Start pauses, View shows this screen, A, B, LB and RB work the menus, the left stick moves, the right stick aims, the D-pad gives team commands in play (up: tap Attack, hold Cover · down: tap Regroup, hold Hold) and swaps character (left/right), and pulling parry and fire together is the ultimate.", 13, Pal.muted, false, FontStyle.Normal, bindPanel);
        }

        void RefreshBinds()
        {
            bindsShown = Bindings.Version;
            foreach (var (b, id, slot) in bindCells)
            {
                var B = Bindings.Get(id); bool on = listenId == id && listenSlot == slot;
                string name = slot == 0 ? Bindings.KeyName(B.k1) : slot == 1 ? Bindings.KeyName(B.k2) : slot == 2 ? Bindings.MouseName(B.mouse) : Bindings.PadName(B.pad);
                b.GetComponentInChildren<Text>().text = on ? (slot == 3 ? "PRESS A BUTTON" : slot == 2 ? "CLICK A BUTTON" : "PRESS A KEY") : string.IsNullOrEmpty(name) ? "-" : name.ToUpperInvariant();
                Btn.SetOn(b, on);
            }
            foreach (var (t, raw, upper) in guideCells) { var x = W.Rich(Bindings.Expand(raw)); t.text = upper ? x.ToUpperInvariant() : x; }
        }

        void ShowTab(bool guide)
        {
            StopListen(null);
            guideTab = guide;
            bindPanel.gameObject.SetActive(!guide); guidePanel.gameObject.SetActive(guide);
            Btn.SetOn(tabEdit, !guide); Btn.SetOn(tabGuide, guide);
            helpTitle.text = guide ? "WHAT EACH BUTTON DOES" : "CHANGE YOUR CONTROLS";
            help.scroll.verticalNormalizedPosition = 1; help.content.anchoredPosition = Vector2.zero;
            if (helpOpen) { Canvas.ForceUpdateCanvases(); Collect(); focusEl = guide ? tabGuide : tabEdit; }
        }

        void StartListen(string id, int slot)
        {
            if (Time.unscaledTime < clickLock) return;   // (the release of the click that set the last one)
            listenId = id; listenSlot = slot; listenFrame = Time.frameCount; listenT = 8;
            string what = slot == 3 ? "Press a gamepad button" : slot == 2 ? "Click a mouse button" : "Press a key";
            bindStatus.text = $"{what} for “{Bindings.Of(id).label}”. Esc cancels; Delete or Backspace clears it.";
            RefreshBinds();
        }
        void StopListen(string msg)
        {
            if (listenId == null) return;
            listenId = null; clickLock = Time.unscaledTime + 0.35f;
            if (msg != null && bindStatus != null) bindStatus.text = msg;
            RefreshBinds();
        }
        void ApplyBind(int v, string name)
        {
            string label = Bindings.Of(listenId).label;
            var took = Bindings.Assign(listenId, listenSlot, v);
            StopListen(v < 0 ? $"“{label}”: cleared." : took != null ? $"{name} now does “{label}” (taken from “{took}”)." : $"{name} now does “{label}”.");
        }

        // Each frame: the screen follows the bindings, and a binding being changed waits for its input
        void UpdateControls(float dt)
        {
            if (bindsShown != Bindings.Version) RefreshBinds();
            if (!Listening) return;
            if (!helpOpen) { StopListen(null); return; }
            if (Time.frameCount == listenFrame) return;   // (the press that started it)
            listenT -= dt;
            if (listenT <= 0) { StopListen("Nothing was pressed: it stays as it was."); return; }
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) { StopListen("Cancelled."); return; }
            if (kb != null && (kb.deleteKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame)) { ApplyBind(-1, null); return; }
            if (listenSlot <= 1)
            {
                if (kb == null) return;
                foreach (var kc in kb.allKeys)
                {
                    if (kc == null || !kc.wasPressedThisFrame) continue;
                    var k = kc.keyCode; if (k == UnityEngine.InputSystem.Key.None) continue;
                    if (Bindings.ReservedKey(k)) { bindStatus.text = Bindings.KeyName(k) + " is kept for the menus: pick another key."; return; }
                    ApplyBind((int)k, Bindings.KeyName(k)); return;
                }
            }
            else if (listenSlot == 2)
            {
                var ms = UnityEngine.InputSystem.Mouse.current; if (ms == null) return;
                var bs = new[] { ms.leftButton, ms.middleButton, ms.rightButton, ms.backButton, ms.forwardButton };
                for (int i = 0; i < bs.Length; i++) if (bs[i].wasPressedThisFrame) { ApplyBind(i, Bindings.MouseName(i)); return; }
            }
            else
            {
                foreach (var p in UnityEngine.InputSystem.Gamepad.all)
                    for (int i = 0; i < 16; i++)
                    {
                        if (!Controls.Btn(p, i).wasPressedThisFrame) continue;
                        if (i == 8 || i == 9) { StopListen("Cancelled."); return; }
                        if (Bindings.ReservedPad(i)) { bindStatus.text = "The D-pad keeps its jobs (team commands, character swaps, menus): pick another button."; return; }
                        ApplyBind(i, Bindings.PadName(i)); return;
                    }
            }
        }

        // Controller input while the screen is open (GameMain): LB / RB switch tabs; on the guide the D-pad scrolls
        public void HelpNav(MenuEv ev)
        {
            if (ev.type == "prevTab") { ShowTab(false); return; }
            if (ev.type == "nextTab") { ShowTab(true); return; }
            if (guideTab && (ev.type == "up" || ev.type == "down")) { ScrollHelp(ev.type == "down" ? 1 : -1); return; }
            MenuNav(ev);
        }

        void Row3(string a, string b, string c, bool head, bool section)
        {
            var row = W.Rect(guidePanel, "row"); var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>(); hl.spacing = 12; hl.childControlWidth = hl.childControlHeight = true; hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
            hl.padding = new RectOffset(4, 4, section ? 10 : 4, 6);
            Color col = head ? (section ? Pal.C("#e8f0fa") : Pal.muted) : Pal.text;
            void Cell(string s, float w)
            {
                var x = W.Rich(Bindings.Expand(s));
                var t = W.Txt(row, head ? x.ToUpperInvariant() : x, head ? 13 : 14, col, head, head ? FontStyle.Bold : FontStyle.Normal);
                var le = t.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = w; le.flexibleWidth = 0;
                if (s.IndexOf('{') >= 0) guideCells.Add((t, s, head));
            }
            if (section) Cell(a, 880);
            else if (c == null) { Cell(a, 440); Cell(b, 428); }
            else { Cell(a, 440); Cell(b, 210); Cell(c, 210); }
            var line = W.Box(guidePanel, Pal.edge, "rule", false); line.gameObject.AddComponent<LayoutElement>().preferredHeight = 1;
        }
        public void ToggleHelp(bool? on = null)
        {
            bool was = helpOpen;
            helpOpen = on ?? !helpOpen; help.visible = helpOpen;
            if (helpOpen && !was)
            {
                help.scroll.verticalNormalizedPosition = 1; help.content.anchoredPosition = Vector2.zero;
                pauseFocus = focusEl; Canvas.ForceUpdateCanvases(); Collect(); focusEl = guideTab ? tabGuide : tabEdit; padnav = true;
            }
            else if (!helpOpen && was)
            {
                StopListen(null);
                if (paused) { Collect(); focusEl = pauseFocus != null && focusables.Contains(pauseFocus) ? pauseFocus : focusables.FirstOrDefault(); }
            }
        }
        Selectable pauseFocus;
        // The controls screen from the pause menu opens on changing them; H (or View) in play opens it as last left
        void OpenControls() { if (!helpOpen) ToggleHelp(true); ShowTab(false); }
        public void ScrollHelp(int dir) { var p = help.content.anchoredPosition; p.y = Mathf.Max(0, p.y + dir * 180); help.content.anchoredPosition = p; }
    }
}
