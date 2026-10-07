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
            new SettingDef { key = "novaHead", label = "Nova's head (look only)", opts = O("bare", "Face, as in his concept art", "helmet", "Full helmet, gold visor") },
            new SettingDef { key = "echoHead", label = "Echo's head (look only)", opts = O("helmet", "Full helmet, amber visor", "mask", "Survival mask", "bare", "Bare face") },
            new SettingDef { key = "echoKit", label = "Echo's kit", opts = O("hunter", "Hunter: blades, glaive, snares, reel", "pursuit", "Pursuit: Pass 1 kit") },
            new SettingDef { key = "echoBelt", label = "Echo's utility belt (snares), Hunter kit", opts = O("fire", "Tap fire (crouch + tap plants)", "lb", "LB / T (crouch + LB plants); tap fire is a quick shot") },
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
            new SettingDef { key = "lockOn", label = "Lock-on (F / R3)", @bool = true },
            new SettingDef { key = "lockMode", label = "Lock-on mode", opts = O("auto", "Automatic: nearest enemy, R3 switches", "manual", "Press R3 to lock") },
            new SettingDef { key = "dashCharge", label = "Charged dash (hold dash while standing still)", @bool = true },
            new SettingDef { key = "haptics", label = "Rumble and vibration", @bool = true },
            new SettingDef { key = "hapticStrength", label = "Rumble strength", range = new[] { 0f, 1, 0.05f } },
            new SettingDef { key = "p1Aim", label = "Keyboard player aims with", opts = O("mouse", "Mouse", "keys", "Movement keys (8-way)") },
            new SettingDef { key = "aiTeammates", label = "AI teammates (fill empty slots; a player joining takes one over)", opts = O("0", "Off", "1", "1", "2", "2", "3", "3") },
            new SettingDef { key = "aiSkill", label = "AI teammate skill", opts = O("rookie", "Rookie: slow to react, basic plays", "veteran", "Veteran: reads the fight, uses the whole kit", "elite", "Elite: sharp reactions, every advanced play") },
            new SettingDef { key = "difficulty", label = "Difficulty", opts = O("easy", "Easy", "normal", "Normal", "hard", "Hard") },
            new SettingDef { key = "barks", label = "Character lines (CP-09 test)", @bool = true },
            new SettingDef { key = "shake", label = "Screen shake", @bool = true },
            new SettingDef { key = "charModels", label = "Character models", opts = O("builtin", "Built-in rigs", "models", "3D models where available (Resources/NovaStriker/Models)") },
            new SettingDef { key = "quality", label = "Graphics quality", opts = O("ultra", "Ultra (adds ambient occlusion)", "high", "High (bloom, shadows, grading)", "low", "Low") },
            new SettingDef { key = "hdr", label = "HDR output (needs an HDR display with HDR on in the system)", @bool = true },
            new SettingDef { key = "volume", label = "Sound effects volume", range = new[] { 0f, 1, 0.05f } },
            new SettingDef { key = "music", label = "Music volume", range = new[] { 0f, 1, 0.05f } },
        };
        static readonly (string kind, string a, string b, string c)[] HELP_ROWS =
        {
            ("R", "Move · crouch", "Left stick", "A/D · S"),
            ("R", "Jump · double jump · wall jump", "A", "Space"),
            ("R", "Dash (8-way) · slide (down + dash)", "B", "Shift"),
            ("R", "Charged dash: hold dash while standing still, aim, let go. Each level goes further; level 2 is briefly invulnerable, level 3 cuts through enemies (afterimages show the level)", "Hold B", "Hold Shift"),
            ("R", "Wall slide and wall jump: hold toward a wall to slide down it. Jump while holding toward it (or neutral) to kick up it; hold away to leap off. You can shoot and strike while sliding", "Toward the wall · A", "A/D toward the wall · Space"),
            ("R", "Lock-on (automatic by default): whenever you have no target, the nearest enemy in sight is locked. Tap to switch to the next target; hold to let go (it stays off until you tap again). Homing shots go to the target and melee steps in toward it; free aim (right stick, mouse) still aims where you point. Settings: Lock-on mode, to lock only when you press", "R3 (click the right stick)", "F, O, or mouse forward button"),
            ("S2", "Velocity Break (Echo's Hunter kit: the Dash Slash, a lunging cut that carries him through)", "Melee while dashing or sliding, or just after a dash", ""),
            ("R", "Melee · charged melee (hold, then let go)", "X · hold X", "Right click or J · hold"),
            ("R", "Ground pound: in the air, melee with down held (or aimed straight down). Hold it to charge through three levels while you hang in the air; the landing throws enemies outward", "Down + X in the air · hold", "S + melee in the air · hold"),
            ("R", "Rising attack: every character has their own. Nova: the Solar Uppercut (his boots fire and a hard-light fist drives up, three hits, a flare at the top; once per jump in the air). Echo: the Rising Glaive (a spinning uppercut that carries him up). RAM: the Hydraulic Uplift (his shield scoops up everything in front, sweeps shots out of the air over him, and bursts at the top; it reaches drones). Fix: Jack-Up (a pneumatic jack fires her up behind a wrench uppercut and stays as a spring pad any teammate can bounce on)", "Up + X", "W + melee"),
            ("R", "Echo, Hunter kit: blade and glaive chain · Spin Slash in the air · Wall Slash on a wall · charged glaive swing that looses a crescent wave", "X · up + X in the air · X on a wall · hold X", "Melee · W + melee in the air · melee on a wall · hold"),
            ("R", "Echo deflects: his parry and his glaive swings knock enemy shots back at whoever fired them. A perfect deflect opens a Riposte: melee straight after", "LT · X", "Q or L · melee"),
            ("R", "Fire · charge", "RT · hold RT", "Left click or K · hold"),
            ("R", "Aim", "Right stick (free) or left stick (8-way)", "Mouse"),
            ("R", "Parry, Echo and Nova's Sentinel kit (first 4 frames are perfect)", "LT", "Q or L"),
            ("R", "Nova, Marksman kit: dodge. A quick hop the way you push the stick (a backstep with it centred), untouchable at the start; one in the air per jump. Dodge an attack at the last moment for a perfect dodge: enemies close by slow down, and you gain Overcharge and ultimate charge. You keep charging through it", "LT", "Q or L"),
            ("R", "Nova, Marksman kit, absorbing shield (Settings: Nova's LT move): hold to raise a hard-light shield where you aim. It blocks strikes, shots and blasts from in front, and every hit it takes is absorbed as charge: his attacks hit harder (+100% at a full charge, and it overfills to 150% for +150%) and he glows brighter the more he holds. Raise it just before a hit for a perfect block (no cost, more energy). Each block wears its stability down (it grows back once lowered; broken, he reels). The energy holds for 6 s after the last block, then fades; an unguarded hit spills half of it. Walk, jump, dash and fire (tap or charge) with it up; melee, or a Level 4 beam, lowers it", "Hold LT", "Hold Q or L"),
            ("S2", "Nova's perfect parry stuns (Settings): a perfect parry (Sentinel kit) or perfect shield block (Marksman kit) leaves the attacker stunned for a moment (heavy enemies more briefly; bosses only reel)", "Settings", ""),
            ("R", "Suit ability: Nova (Marksman kit) raises the hard-light Aegis for 5 s: it blocks every attack, and the damage it takes Overcharges his weapons (faster charging, harder hits). Press again to detonate it. Nova (Sentinel kit): Bulwark Pulse. Echo: his scarf ability for the current mode", "Y", "E, I, or middle click"),
            ("R", "Switch mode: Nova's bracer attachment (Lance, Volley, Arc, Prism) · Echo's scarf mode (Tether, Veil, Flare)", "RB", "R, U, or mouse back button"),
            ("R", "Nova, Marksman kit: fire · hold to charge the loaded attachment through three levels · let go on the flash after level 3 for a Perfect Release · keep holding to the Level 4 flash and let go for a sustained beam (steer it with your aim; dash or parry cuts it short)", "RT · hold RT", "Left click or K · hold"),
            ("R", "Nova, Marksman kit: close to an enemy, melee is his hard-light combo (backhand, elbow, blast punch; an axe kick in the air). Otherwise it fires his secondary weapon; none has any recoil. Tap to fire, hold to charge through three levels. <b>Scatter</b>: point-blank pellets. <b>Grenade</b>: bounces, and bursts on its fuse or on an enemy (level 3 scatters bomblets). <b>Chain</b>: lightning that leaps from enemy to enemy, round shields, and stuns. <b>Disc</b>: flies out and back cutting everything (from level 2 it hovers at the end); press again to call it back. <b>Gravity Well</b>: pulls enemies in and holds them, swallows their shots, then collapses; press again to open it early, and again to collapse it", "X · hold X", "Right click or J · hold"),
            ("R", "Switch Nova's secondary weapon: Scatter, Grenade, Chain, Disc, Gravity Well", "LB", "T or Y"),
            ("R", "Ultimate: the bar under your health fills as you fight (dealing and taking damage, kills, perfect parries, dodges, deflects and guards, and Fix's healing). When it is full, pull both triggers together. Nova: <b>Supernova</b>, a colossal beam you steer, then a nova of light. Echo: <b>Thousand Cuts</b>, a storm of blinking cuts on every enemy close by. RAM: <b>Siege Breaker</b>, the team is Fortified with Plating and he charges behind a colossal ram's head of hard light, scooping up everything in his path, then slams the pile down. Fix: <b>Overhaul</b>, a supply pod drops and pulses repair light across the screen (it brings back anyone who is down and hurts every enemy), then the team is Overclocked and Plated and her gadgets jump to level 3. Enemies freeze while it plays out and you can't be hurt", "LT + RT together", "V or N (or Q + left click together)"),
            ("R", "Team ultimate: while a teammate's ultimate is being called (its name on screen), pull both triggers with a full bar to join in. Everyone who joins unleashes theirs together, stronger, then a team finisher hits every enemy on screen. Pairs have their own names: Echo + Nova, Eclipse Protocol; Nova + RAM, Starbreaker; Echo + RAM, Shatterpoint; Fix + Nova, Solar Overdrive; Echo + Fix, Razorwire; Fix + RAM, Heavy Metal; and two of the same, Binary Star, Twin Phantom, Stampede, Assembly Line. Three or more: Full Resonance", "LT + RT during the call", "V during the call"),
            ("S2", "Nova: every shot bursts where it lands and splashes nearby enemies. A charged shot bursting on the ground or a wall close to you launches you: aim at your feet to rocket jump. The longer the charge, the higher you go (a gold line shows the height); a Perfect Release goes highest", "Aim down, charge, let go", ""),
            ("R", "Nova: light boosters. After your double jump, press and hold jump to hover and climb (the gold bar under his health)", "A (third press)", "Space (third press)"),
            ("S2", "Nova's skates: you glide and keep your speed; reverse to carve to a stop; crouch at speed for a low glide", "Move as usual", ""),
            ("S2", "Nova's Focus: each charged shot that lands adds a level (a Perfect Release adds two) and more damage; getting hit clears it", "Shown under his health bar", ""),
            ("R", "Echo, Hunter kit: tap to throw a snare (down + tap plants one) · hold to scope the sniper rifle. Focus builds while you hold: the laser narrows, flickers until it rests on a target, holds solid on one and turns red at full focus. Let go for an instant shot; upper-body hits are critical, and at full focus it pierces everything in line, breaks armor and tags", "Tap RT · hold RT", "Tap / hold left click or K"),
            ("R", "Tether mode: tap pulls light enemies or zips you to heavy ones; hold reels a light enemy in or yanks a heavy one off balance", "Y (tap / hold)", "E (tap / hold)"),
            ("R", "Veil mode: you fade out while you're not attacking and enemies lose track of you; your first strike from hiding is an ambush that staggers. Attacking or getting hit shows you again. Vanish hides you at once", "Y: Vanish", "E: Vanish"),
            ("R", "Flare mode: nearby enemies go for you instead of your team; while they do, parries are easier and Resolve builds faster. Challenge pulls every enemy close by onto you", "Y: Challenge", "E: Challenge"),
            ("H", "RAM, Vanguard (the tank)", "", ""),
            ("R", "Guard: hold to raise the Rampart, a tower shield. It blocks strikes, shots and blasts from in front (shockwaves along the floor still go under it) and covers everyone behind him. The damage comes off its Integrity (the blue bar), which grows back once he lowers it; broken, he reels and must wait for it. Raise it just before a hit for a Perfect Guard: no cost, a shot goes back the way it came, a striker reels. Aim up to hold it overhead. He walks slowly behind it and can jump with it up", "Hold LT", "Hold Q or L"),
            ("R", "Kinetic Release: every point the shield blocks is stored as Kinetic; fire while guarding lets it out as a cone of force, stronger the more is stored", "RT while guarding", "Left click or K while guarding"),
            ("R", "Ram Charge (his dash): a shoulder charge behind the shield that scoops up light enemies and slams them into the next wall. Hold it while standing still for the Battering Ram: three levels, further and faster, and from level 2 it carries heavy enemies too and breaks armor. Nothing knocks him about while he charges", "B · hold B", "Shift · hold Shift"),
            ("R", "Breach Cannon: tap for a heavy slug; hold to charge a Breach Shot that punches through enemies (level 3 bursts at the end); keep holding to Level 4 for the Breach Beam, a sustained column of hard light that shoves everything in its line back", "RT · hold RT", "Left click or K · hold"),
            ("R", "Melee: shield bash, edge strike, Piston Punch; a shield swat in the air; hold for the Seismic Slam (shockwaves run both ways along the floor). From a guard, melee is a quick shove. His ground pound lands wider and harder", "X · hold X", "Right click or J · hold"),
            ("S2", "Stalwart: ordinary hits don't knock him about (heavy hits and blasts still do). He is big and slow, with lower jumps and much more health", "Always", ""),
            ("R", "Bulwark Wall: a hard-light wall in front of him for 8 s. Enemy shots stop at it and enemies can't get through until they break it; your team's shots pass through it boosted", "Y", "E, I, or middle click"),
            ("R", "Guardian Link: links him to the teammate who needs it most, leaping to their side if they are far. For 8 s he takes 60% of the damage they take, and they get Plating", "RB", "R, U, or mouse back button"),
            ("R", "Provoke: a war cry. Enemies close by turn on him for 4 s, he braces (takes 40% less), and enemies right beside him are shoved back", "LB", "T or Y"),
            ("H", "Fix, Mechanic (the support)", "", ""),
            ("R", "Patch Beam: hold to beam the teammate who needs it most. It heals fast, then adds Plating, and Tunes Up whoever it holds: they charge, recharge and fill their bars 1.5 times as fast. On a downed teammate it revives them from range; with no one near she welds herself. She moves slowly while it runs", "Hold LT", "Hold Q or L"),
            ("S2", "Field Mechanic: beside a downed teammate she revives three times as fast as anyone else, and whoever she brings back has 60% of their health", "Stand next to them", ""),
            ("R", "Gadgets (cost Scrap): build the selected one in front of her; building it again moves it. <b>Patch Pylon</b>: heals everyone in its field, and a downed teammate inside gets back up on their own. <b>Sentry</b>: shoots the nearest enemy in sight (rockets too at level 3). <b>Amp Coil</b>: teammates in its field charge and fill their bars faster. Two wrench hits raise a gadget a level (up to 3) and refresh it", "Y build · RB pick", "E build · R pick"),
            ("R", "Team commands to the AI teammates (Settings: AI teammates): <b>Attack my target</b> (all go for your lock-on target), <b>Cover me</b> (RAM shields you, Fix beams you, the others take what comes for you), <b>Regroup on me</b> (they close in for a few seconds), <b>Hold here</b> (they stand their ground where you were). The same command again cancels it", "D-pad up: tap Attack · hold Cover · D-pad down: tap Regroup · hold Hold", "Z Attack · G Cover · X Regroup · C Hold"),
            ("R", "Breakable pieces and power-ups along the routes: crates, barricades, glass and pillars break under attacks (a pillar only under heavy blows; RAM's charge goes through), and some crates hold a power-up. Power-ups wait along the way under a column of light: <b>Medkit</b>, <b>Plating</b>, <b>Overclock</b>, an <b>Ult Cell</b> (40 ultimate) and <b>Fury</b> (+50% damage and knockback for 12 s)", "Walk into it", "Walk into it"),
            ("R", "Power-ups (cost Scrap): melee with no enemy or gadget of hers close tosses the selected one to the nearest teammate in front (or drops it at her feet; anyone can pick it up). <b>Overclock</b>: everything charges, recharges and fills 1.6 times as fast for 10 s. <b>Plating</b>: an overshield over the health bar. <b>Medkit</b>: 40 health", "X (nothing close) · LB pick", "Right click or J · T pick"),
            ("R", "Rivet Gun: tap for a burst of rivets; hold for a Hot Rivet that sticks in what it hits and bursts", "RT · hold RT", "Left click or K · hold"),
            ("R", "Wrench: swing, backswing and a clanging overhead (close to an enemy or one of her gadgets); hold for the Torque Slam, a ring of sparks that stuns light enemies and drones. Her ground pound's landing heals teammates close by", "X · hold X", "Right click or J · hold"),
            ("S2", "Scrap (the yellow bar): it trickles in, and comes from her hits and from enemies falling near her", "Shown under her health bar", ""),
            ("H", "Everyone", "", ""),
            ("R", "Swap character (Nova, Echo, RAM, Fix)", "D-pad left/right", "1–4 / Tab"),
            ("S2", "Revive a downed ally", "Stand next to them (faster with Fix)", ""),
        };
        static readonly string[] HELP_NOTES =
        {
            "New enemies: <b>Drones</b> fly above you and shoot (parry, or pull them down with Echo's Tether). <b>Mortars</b> lob shells at a magenta ring on the ground: you can't parry the burst, so move, or shoot the shell down with a charged shot. <b>Chargers</b> telegraph a heavy charge: perfect-parry it or jump over, and they daze themselves on walls.",
            "Controllers rumble with hits, charges and launches (Settings: Rumble). On Android phones the first player's phone can vibrate; iPhones do not support vibration from a web page, and a page embedded in another site may be blocked from it.",
            "Threats: a white glint means you can parry it. A double glint marks a heavy attack: a perfect parry negates it fully. A magenta jagged strip and a rising tone mean you cannot parry it; jump or move.",
            "<b>Close:</b> B, A, Start or View on a controller (the D-pad scrolls) · <kbd>H</kbd>, <kbd>Esc</kbd> or a click on the keyboard. The game waits while this is open.",
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
                foreach (var l in lines) start.Para("•  " + l, 14, Pal.text, false, FontStyle.Normal, c);
            }
            Col("Keyboard + mouse", new[] { "<kbd>A</kbd><kbd>D</kbd> move · <kbd>S</kbd> crouch · <kbd>Space</kbd> jump · <kbd>Shift</kbd> dash", "Left click fire (hold to charge) · Right click melee",
                "<kbd>Q</kbd> parry / dodge / guard / beam · <kbd>E</kbd> suit ability · <kbd>R</kbd> mode", "<kbd>T</kbd> LB action · <kbd>F</kbd> switch target · <kbd>V</kbd> ultimate · <kbd>1</kbd>–<kbd>4</kbd> character" });
            Col("Gamepad", new[] { "Left stick move · Right stick aim · R3 switch target", "A jump · B dash · X melee · Y suit ability · RB mode", "RT fire · LT parry / dodge / guard / beam · LB character action", "LT + RT ultimate · D-pad swap character · Start pause" });
            foreach (var f in START_NOTES) start.Para(f, 13, Pal.muted);
            notice = start.Para("", 13, Pal.warm); notice.gameObject.SetActive(false);
            var qrow = start.Row();
            Btn.Make(qrow, "Quit game", QuitGame, 13, true);
        }
        static readonly string[] START_NOTES =
        {
            "This is the Unity port of Version 13: the same game, simulation and look, on URP. New in Version 13: a new look. Armour that reflects its surroundings, ink outlines, each route with its own light and colour grade, riveted and plated surfaces, shockwaves on the biggest hits, and an Ultra setting with ambient occlusion. Character models can be brought in (Settings; see the README).",
            "New in Version 12: team commands for AI teammates (D-pad up/down, or <kbd>Z</kbd> <kbd>G</kbd> <kbd>X</kbd> <kbd>C</kbd>), two long new levels that wind through 3D (the <b>Helix Foundry</b> and the <b>Undercity Descent</b>, in <kbd>Esc</kbd>/Start), breakable crates, barricades, glass and pillars, and power-ups along the way.",
            "New in Version 11: smarter AI teammates with a skill setting, a controller-friendly pause menu, RAM's Level 4 <b>Breach Beam</b> (keep holding fire) and a shield that cracks and shatters, seven impact frame styles that can take your player colour, and Echo's snares on LB as an option. All in <kbd>Esc</kbd>/Start.",
            "New in Version 10: two new characters. <b>RAM</b>, the tank: hold LT to raise his tower shield (it blocks, covers everyone behind him and stores Kinetic; fire while guarding releases it), a dash that plows enemies into walls, a hard-light wall, a guardian link and a war cry. <b>Fix</b>, the support: hold LT for a beam that heals, revives from range and tunes teammates up (faster charging and bars), gadgets on Y, power-ups tossed with X. Players join as Nova, Echo, RAM and Fix; swap with the D-pad or <kbd>1</kbd>–<kbd>4</kbd>.",
            "Up to four players: extra gamepads join by pressing any button. <kbd>H</kbd>/View shows every control; <kbd>Esc</kbd>/Start opens settings, zones and the boss fights.",
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
            Btn.Make(row, "Controls", () => ToggleHelp(true));
            // Quit asks for a second press, so a stray A or click doesn't end the session
            quitBtn = Btn.Make(row, "Quit game", () =>
            {
                if (quitArmed > 0) { QuitGame(); return; }
                quitArmed = 3; quitBtn.GetComponentInChildren<Text>().text = "PRESS AGAIN TO QUIT"; Btn.SetOn(quitBtn, true);
            });
            pause.Para("<b>Controller:</b> D-pad or left stick to move · left/right changes a list or slider · A to select · B to resume · LB top · RB settings", 13, Pal.muted);
            playerList = W.Rect(pause.content, "players");
            var pl = playerList.gameObject.AddComponent<VerticalLayoutGroup>(); pl.spacing = 6; pl.childControlWidth = pl.childControlHeight = true; pl.childForceExpandHeight = false;
            var grid = W.Rect(pause.content, "settings");
            var gl = grid.gameObject.AddComponent<GridLayoutGroup>(); gl.cellSize = new Vector2(434, 46); gl.spacing = new Vector2(18, 8); gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount = 2;
            foreach (var d in SETTING_DEFS)
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
        void Collect() { focusables.Clear(); focusables.AddRange(pause.content.GetComponentsInChildren<Selectable>().Where(s => s.gameObject.activeInHierarchy && s.interactable)); }
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
            if (!paused) return;
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
            var vp = pause.scroll.viewport; var r = (RectTransform)s.transform;
            var c = new Vector3[4]; r.GetWorldCorners(c); var v = new Vector3[4]; vp.GetWorldCorners(v);
            float scale = canvas.scaleFactor, dy = 0;
            if (c[1].y > v[1].y) dy = c[1].y - v[1].y + 8 * scale;        // above the view: scroll up
            else if (c[0].y < v[0].y) dy = c[0].y - v[0].y - 8 * scale;   // below: scroll down
            if (dy != 0) { var p = pause.content.anchoredPosition; p.y -= dy / scale; pause.content.anchoredPosition = p; }
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
            bool show = paused && padnav && focusEl != null && focusEl.gameObject.activeInHierarchy && !helpOpen;
            ring.gameObject.SetActive(show);
            if (show)
            {
                var r = (RectTransform)focusEl.transform; var c = new Vector3[4]; r.GetWorldCorners(c);
                ring.position = (c[0] + c[2]) / 2; ring.sizeDelta = new Vector2((c[2].x - c[0].x) / canvas.scaleFactor + 10, (c[2].y - c[0].y) / canvas.scaleFactor + 10);
                ring.SetAsLastSibling();
            }
        }

        // ---- Help ----
        Card help;
        void BuildHelp()
        {
            help = new Card(root, "help", 940);
            var head = W.Rect(help.content, "head"); head.gameObject.AddComponent<LayoutElement>().preferredHeight = 70;
            var eb = W.Txt(head, "CONTROLS", 13, Pal.warm, true, FontStyle.Bold); W.At(eb.rectTransform, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(400, 18));
            var h2 = W.Txt(head, "WHAT EACH BUTTON DOES", 30, Pal.text, true, FontStyle.Bold); W.At(h2.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -20), new Vector2(520, 40));
            var badge = W.Rect(head, "close"); W.At(badge, new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(300, 56));
            W.Fill(W.Box(badge, Pal.C("#ffffff", 0.08f)).rectTransform);
            var bt = W.Txt(badge, "<b><color=#ffb547>B</color></b> OR <b><color=#ffb547>VIEW</color></b> TO CLOSE\n<size=12><color=#a8b7ca>H or Esc on the keyboard · D-pad scrolls</color></size>", 16, Pal.text, true, FontStyle.Bold, TextAnchor.MiddleRight);
            W.Fill(bt.rectTransform).offsetMax = new Vector2(-12, 0);
            Row3("Action", "Gamepad", "Keyboard + mouse", true, false);
            foreach (var (kind, a, b, c) in HELP_ROWS)
            {
                if (kind == "H") Row3(a, "", "", true, true);
                else if (kind == "S2") Row3(a, b, null, false, false);
                else Row3(a, b, c, false, false);
            }
            foreach (var n in HELP_NOTES) help.Para(n, 13, Pal.muted);
            // a click anywhere closes it, as in the prototype
            foreach (var t in new[] { help.overlay, help.card }) t.gameObject.AddComponent<Button>().onClick.AddListener(() => ToggleHelp(false));
            help.visible = false;
        }
        void Row3(string a, string b, string c, bool head, bool section)
        {
            var row = W.Rect(help.content, "row"); var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>(); hl.spacing = 12; hl.childControlWidth = hl.childControlHeight = true; hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
            hl.padding = new RectOffset(4, 4, section ? 10 : 4, 6);
            Color col = head ? (section ? Pal.C("#e8f0fa") : Pal.muted) : Pal.text;
            void Cell(string s, float w)
            {
                var t = W.Txt(row, W.Rich(head ? s.ToUpperInvariant() : s), head ? 13 : 14, col, head, head ? FontStyle.Bold : FontStyle.Normal);
                var le = t.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = w; le.flexibleWidth = 0;
            }
            if (section) Cell(a, 880);
            else if (c == null) { Cell(a, 440); Cell(b, 428); }
            else { Cell(a, 440); Cell(b, 210); Cell(c, 210); }
            var line = W.Box(help.content, Pal.edge, "rule", false); line.gameObject.AddComponent<LayoutElement>().preferredHeight = 1;
        }
        public void ToggleHelp(bool? on = null)
        {
            helpOpen = on ?? !helpOpen; help.visible = helpOpen;
            if (helpOpen) help.scroll.verticalNormalizedPosition = 1;
        }
        public void ScrollHelp(int dir) { var p = help.content.anchoredPosition; p.y = Mathf.Max(0, p.y + dir * 180); help.content.anchoredPosition = p; }
    }
}
