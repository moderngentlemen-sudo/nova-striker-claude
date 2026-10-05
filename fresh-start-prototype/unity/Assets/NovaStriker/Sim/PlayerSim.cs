// Player controller (port of player.js): movement, actions, cancel rules. Operates on plain data; the world
// supplies spawning helpers and events. Frame data lives in Config.cs.
using static NovaStriker.Sim.Cfg;
using static NovaStriker.Sim.U;

namespace NovaStriker.Sim
{
    public static partial class PlayerSim
    {
        // Nova with the Marksman kit (bracer attachments, secondary weapons, dodge, skate glide)
        public static bool marksman(Player p) => p.@char == "nova" && SETTINGS.novaKit == "marksman";

        public static V2? Snap8(double x, double y)
        {
            if (JMath.Hypot(x, y) < 0.35) return null;
            double a = JMath.Round(JMath.Atan2(y, x) / (JMath.PI / 4)) * (JMath.PI / 4);
            return new V2(JMath.Cos(a), JMath.Sin(a));
        }

        public static Player CreatePlayer(int slot, string device, string charId, double x, double y)
        {
            var c = CHARS[charId];
            return new Player
            {
                kind = "player", slot = slot, device = device, @char = charId,
                x = x, y = y, vx = 0, vy = 0, w = c.width, h = c.height, prevX = x, prevY = y,
                facing = 1, onGround = false, wallDir = 0, coyote = 0, jumpsUsed = 0, airDashes = 1,
                state = "normal", st = 0,
                hp = c.hp, maxHp = c.hp,
                lashCharges = ECHO.lashCharges, cells = ECHO.cellsMax,
                snares = HUNTER.snareCharges,
                fuel = MARKSMAN.boost.fuel,
                integrity = RAM.guard.integrity,
                scrap = FIX.scrap.start,
                lastSafeX = x, lastSafeY = y,
            };
        }

        public static void SetCharacter(Player p, string charId)
        {
            var c = CHARS[charId];
            p.@char = charId; p.w = c.width; p.h = c.height; p.maxHp = c.hp;
            p.hp = JMath.Min(p.hp, p.maxHp); p.chargeT = 0; p.resolve = 0; p.strain = 0;
            p.cells = ECHO.cellsMax; p.lashCharges = ECHO.lashCharges; p.state = "normal"; p.st = 0;
            p.veiled = false; p.veilCharge = 0; p.veilBreakT = 0; p.ambushT = 0; p.targetedBy = 0;
            p.focus = 0; p.focusT = 0; p.burstCd = 0; p.burstT = 0; p.shootT = 0;
            p.fuel = MARKSMAN.boost.fuel; p.thrusting = false; p.rockets = 0; p.rocketT = 0;
            p.rifleT = 0; p.rifleCd = 0; p.dashChargeT = 0; p.overcharge = 0; p.overT = 0; p.beam = null; p.aegis = null;
            p.subArmed = false; p.dodge = null; p.pound = null;
            p.integrity = RAM.guard.integrity; p.kinetic = 0; p.guardBroken = false; p.rush = null; p.leap = null; p.braceT = 0;
            p.patch = null; p.tossArmed = false; p.rivetQ = 0; p.scrap = JMath.Max(p.scrap, FIX.scrap.start);
        }

        public static V2 Chest(Player p) => new V2(p.x, p.y + p.h * 0.62);

        // Which Velocity Break tier is available right now (0 = none)?
        public static double VbTier(Player p)
        {
            if (p.state == "pound") return 0;   // the pound's drop is fast, but it is not a Velocity Break
            if (p.boostT > 0 || p.launchedT > 0) return 3;
            if (p.dash != null && p.dash.level >= 2 && (p.state == "dash" || p.postDash <= 6)) return p.dash.level;
            if (p.zipArriveT > 0) return 2;
            if (p.state == "dash" || p.state == "slide" || p.postDash <= 6) return 1;
            if (p.fastFall && p.vy < -18) return 2;
            if (p.dashCarry && !p.onGround && JMath.Hypot(p.vx, p.vy) > HIGH_VEL) return 2;
            return 0;
        }

        static void SetState(Player p, string s) { p.state = s; p.st = 0; }

        static void UpdateAim(Player p, Cmd cmd, World world)
        {
            V2 d;
            if (cmd.aimFree) { d = new V2(cmd.ax, cmd.ay); p.aimFree = true; }
            else
            {
                p.aimFree = false;
                var s = Snap8(cmd.mx, cmd.my);
                if (s != null && p.onGround && s.Value.y < 0) s = new V2(p.facing, 0);   // down on the ground means crouch
                d = s ?? new V2(p.facing, 0);
                if (SETTINGS.aimAssist && p.device != "kbm")
                {
                    var c = Chest(p);
                    var e = world.NearestEnemyInCone(c.x, c.y, d.x, d.y, 14, JMath.PI / 8);
                    if (e != null) { double dx = e.x - c.x, dy = e.y + e.h / 2 - c.y, m = JMath.Hypot(dx, dy); d = new V2(dx / m, dy / m); }
                }
            }
            // Locked on: aim straight at the target. With automatic lock-on, free aim still aims where it points, and
            // holding the stick up or down aims that way.
            if (p.lockT != null && (SETTINGS.lockMode == "manual" || (!cmd.aimFree && JMath.Abs(cmd.my) < 0.55)))
            {
                var c = Chest(p); var t = p.lockT;
                double dx = t.x - c.x, dy = t.y + t.h * 0.55 - c.y, m = or(JMath.Hypot(dx, dy), 1);
                d = new V2(dx / m, dy / m);
            }
            else if (p.wallSliding && truthy(p.wallDir) && d.x * p.wallDir > 0) d = new V2(-d.x, d.y);   // on a wall: shoot out from it
            p.aimX = d.x; p.aimY = d.y;
        }

        public static void UpdatePlayer(Player p, Cmd cmd, World world)
        {
            p.prevX = p.x; p.prevY = p.y;
            var B = p.buf; var P = cmd.pressed;
            B.jump = P.jump ? 0 : JMath.Min(99, B.jump + 1);
            B.dash = P.dash ? 0 : JMath.Min(99, B.dash + 1);
            B.melee = P.melee ? 0 : JMath.Min(99, B.melee + 1);
            B.fire = P.fire ? 0 : JMath.Min(99, B.fire + 1);
            B.parry = P.parry ? 0 : JMath.Min(99, B.parry + 1);
            B.sig = P.sig ? 0 : JMath.Min(99, B.sig + 1);
            B.mode = P.mode ? 0 : JMath.Min(99, B.mode + 1);
            B.sub = P.sub ? 0 : JMath.Min(99, B.sub + 1);
            TrackChord(p, cmd); p.stick = new V2(cmd.mx, cmd.my);
            if (p.state == "dead") return;
            // Mode switches are instant, so a press during hitstop is never lost
            if (cmd.pressed.mode && p.modeCd == 0 && p.state != "downed" && p.state != "ult")
            {
                if (p.@char == "echo") CycleScarf(p, world);
                else if (marksman(p)) CycleAttachment(p, world);
                else if (p.@char == "fix") CycleGadget(p, world);
            }
            if (cmd.pressed.sub && p.subSwCd == 0 && p.state != "downed" && p.state != "ult")
            {
                if (marksman(p)) CycleSub(p, world);
                else if (p.@char == "fix") CyclePower(p, world);
            }
            if (p.hitstop > 0) { p.hitstop--; return; }
            // Both triggers together with a full bar: the ultimate (the world takes over from here)
            if (p.ult >= ULT.max && ChordReady(p, cmd) && world.ultCast == null && p.state != "downed" && p.state != "ult") { world.StartUlt(p); return; }

            p.st++;
            dec(ref p.mercy); dec(ref p.dashCd); dec(ref p.fireCd); dec(ref p.bulwarkCd); dec(ref p.tracerCd); dec(ref p.controlLock); dec(ref p.launchedT);
            dec(ref p.zipArriveT); dec(ref p.boostT); dec(ref p.dropT); dec(ref p.riposteT); dec(ref p.coyote); dec(ref p.modeCd); dec(ref p.ambushT);
            dec(ref p.shootT); dec(ref p.carveT); dec(ref p.rocketT); dec(ref p.rifleCd); dec(ref p.wallCoyote); dec(ref p.subSwCd); dec(ref p.dodgeCd);
            dec(ref p.overclockT); dec(ref p.tuneT); dec(ref p.braceT); dec(ref p.padCd); dec(ref p.furyT);
            // Ability cooldowns recharge faster under Fix's boosts
            double rate = BoostRate(p);
            if (p.aegisCd > 0) p.aegisCd = JMath.Max(0, p.aegisCd - rate);
            if (p.burstCd > 0) p.burstCd = JMath.Max(0, p.burstCd - rate);
            if (p.wallCd > 0) p.wallCd = JMath.Max(0, p.wallCd - rate);
            if (p.linkCd > 0) p.linkCd = JMath.Max(0, p.linkCd - rate);
            if (p.provokeCd > 0) p.provokeCd = JMath.Max(0, p.provokeCd - rate);
            // Aegis time, and Overcharge draining once it has not grown for a while
            if (p.aegis != null && --p.aegis.t <= 0) world.EndAegis(p, "expire");
            if (p.overcharge > 0) { if (p.overT > 0) p.overT--; else p.overcharge = JMath.Max(0, p.overcharge - AEGIS.over.drain); }
            p.postDash = JMath.Min(99, p.postDash + 1);
            if (p.@char == "echo") TickEcho(p, world, cmd);
            else if (p.@char == "ram") TickRam(p, world);
            else if (p.@char == "fix") TickFix(p, world);
            else TickFocus(p, world);

            if (p.state == "downed") { UpdateDowned(p, cmd, world); return; }
            UpdateLock(p, cmd, world);
            UpdateAim(p, cmd, world);
            p.meleeHeldT = cmd.held.melee ? p.meleeHeldT + 1 : 0;
            // RAM's abilities and Fix's gadgets are instant and work from most states
            if (p.@char == "ram") RamAbilities(p, world);
            else if (p.@char == "fix") FixAbilities(p, world);

            bool wasSliding = p.wallSliding;
            p.wallPrev = wasSliding; p.wallSliding = false;   // set again by WallCling in the states that allow a wall slide
            switch (p.state)
            {
                case "normal": StateNormal(p, cmd, world); break;
                case "dashCharge": StateDashCharge(p, cmd, world); break;
                case "beam": StateBeam(p, cmd, world); break;
                case "dashslash": StateDashSlash(p, cmd, world); break;
                case "dash": StateDash(p, cmd, world); break;
                case "slide": StateSlide(p, cmd, world); break;
                case "vb": StateVB(p, cmd, world); break;
                case "attack": StateAttack(p, cmd, world); break;
                case "parry": StateParry(p, cmd, world); break;
                case "hitstun": StateHitstun(p, cmd, world); break;
                case "bulwark": StateBulwark(p, cmd, world); break;
                case "lash": StateLash(p, cmd, world); break;
                case "zip": StateZip(p, cmd, world); break;
                case "dive": StateDive(p, cmd, world); break;
                case "pound": StatePound(p, cmd, world); break;
                case "dodge": StateDodge(p, cmd, world); break;
                case "guard": StateGuard(p, cmd, world); break;
                case "rush": StateRush(p, cmd, world); break;
                case "leap": StateLeap(p, cmd, world); break;
                case "patch": StatePatch(p, cmd, world); break;
                case "ult": world.UltStep(p, cmd); break;
            }
            if (p.state != "ult") HandleFire(p, cmd, world);
            if (p.tossArmed) FixToss(p, cmd, world);
            // Boosters only run in the normal state; anything else cuts them
            if (p.thrusting && (p.state != "normal" || p.onGround)) { p.thrusting = false; world.Emit("thrustOff", new Ev { p = p }); }
            if (p.wallSliding != wasSliding) world.Emit("wallSlide", new Ev { p = p, on = p.wallSliding, dir = p.wallSliding ? p.wallDir : p.lastWallDir });

            bool wasGround = p.onGround; double fallV = p.vy;
            // (RAM stays standing, braced, while he charges a Battering Ram)
            bool low = p.crouch || p.state == "slide" || (p.state == "dashCharge" && p.@char != "ram");
            if (p.state != "dash" && p.state != "zip") p.h = low ? CHARS[p.@char].crouchH : CHARS[p.@char].height;
            Level.MoveBody(p, DT);
            if (p.onGround)
            {
                p.coyote = COYOTE; p.jumpsUsed = 0; p.airDashes = 1; p.fastFall = false; p.dashCarry = false; p.airDodge = true; p.airRise = true;
                p.rockets = 0; p.rocketT = 0; p.wallCoyote = 0; if (p.fuel < MARKSMAN.boost.fuel) p.fuel = JMath.Min(MARKSMAN.boost.fuel, p.fuel + MARKSMAN.boost.refill);
                if (!wasGround && p.st > 1) world.Emit("land", new Ev { p = p, vy = fallV });
                p.lastSafeX = p.x; p.lastSafeY = p.y;
            }
            if (truthy(p.wallDir) && !p.onGround)
            {
                // Touching a wall gives back the air dash and the double jump, and remembers the wall for a late wall jump
                p.airDashes = 1; p.jumpsUsed = 0; p.airDodge = true; p.airRise = true; p.lastWallDir = p.wallDir; p.wallCoyote = WALL.coyote;
            }
            p.iframe = (SETTINGS.dashIframes && p.state == "dash" && p.st <= 8) || (p.state == "dash" && p.dash != null && p.st <= p.dash.iframes) ||
                (p.state == "dodge" && p.dodge != null && p.dodge.t <= DODGE.iframes) || p.state == "ult";
        }

        // ---- Shared action starters ----

        static bool TryJump(Player p, Cmd cmd, World world)
        {
            if (p.buf.jump > JUMP_BUFFER) return false;
            if (p.onGround && p.crouch && cmd.my < -0.6 && world.OnOneWay(p))
            {
                p.dropT = 12; p.y -= 0.05; p.buf.jump = 99; p.onGround = false; return true;
            }
            if (p.onGround || p.coyote > 0)
            {
                var c = CHARS[p.@char];
                p.vy = c.jumpV; p.coyote = 0; p.onGround = false; p.crouch = false;
                if (p.postDash <= 8) p.dashCarry = true;
                p.buf.jump = 99; SetState(p, "normal"); world.Emit("jump", new Ev { p = p });
                return true;
            }
            double wd = p.wallDir != 0 ? p.wallDir : p.wallCoyote > 0 ? p.lastWallDir : 0;
            if (wd != 0)
            {
                // Holding away from the wall leaps off it; toward it or neutral is a climb kick that rises high
                var w = CHARS[p.@char].wall; bool away = cmd.mx * wd < -0.3;
                p.vx = -wd * (away ? w.jumpVx : WALL.climb.vx); p.vy = w.jumpVy * (away ? WALL.leapVy : 1);
                p.controlLock = away ? w.@lock : WALL.climb.@lock; p.facing = -wd;
                p.wallCoyote = 0; p.wallStick = 0; p.wallSliding = false;
                p.buf.jump = 99; p.fastFall = false; SetState(p, "normal"); world.Emit("walljump", new Ev { p = p, climb = !away, dir = -wd });
                return true;
            }
            if (p.jumpsUsed < 1)
            {
                // While a rocket launch still climbs faster than a double jump would, the press waits in the buffer
                if (p.rocketT > 0 && p.vy > CHARS[p.@char].dblV) return false;
                p.vy = CHARS[p.@char].dblV; p.jumpsUsed = 1; p.fastFall = false;
                p.buf.jump = 99; SetState(p, "normal"); world.Emit("djump", new Ev { p = p });
                return true;
            }
            return false;
        }

        static bool TryDash(Player p, Cmd cmd, World world)
        {
            if (p.buf.dash > ACTION_BUFFER || p.dashCd > 0) return false;
            var c = CHARS[p.@char];
            if (p.onGround && cmd.my < -0.5)
            {
                p.buf.dash = 99; p.dashCd = c.dash.cooldown;
                double dir = JMath.Abs(cmd.mx) > 0.3 ? sign(cmd.mx) : p.facing;
                p.facing = dir; p.vx = dir * c.slide.speed; p.crouch = true;
                SetState(p, "slide"); world.Emit("slide", new Ev { p = p });
                return true;
            }
            // Charged dash: on the ground with no direction held, holding dash plants the feet and charges
            if (SETTINGS.dashCharge && p.onGround && p.state == "normal" && cmd.held.dash && JMath.Abs(cmd.mx) < 0.3 && JMath.Abs(cmd.my) < 0.5)
            {
                p.buf.dash = 99; p.dashChargeT = 0; p.crouch = false;
                SetState(p, "dashCharge"); world.Emit("dashChargeStart", new Ev { p = p });
                return true;
            }
            if (!p.onGround && p.airDashes <= 0) return false;
            V2 d = Snap8(cmd.mx, cmd.my) ?? new V2(p.wallSliding ? -p.wallDir : p.facing, 0);
            if (p.onGround && d.y < 0) d = new V2(or(sign(d.x), p.facing), 0);
            if (truthy(p.wallDir) && d.x * p.wallDir > 0) d = new V2(-d.x, d.y);   // from a wall, a dash goes out from it
            if (!p.onGround) p.airDashes--;
            StartDash(p, d, world, 0);
            return true;
        }

        // Level 0 is an ordinary dash; 1-3 come from a charged release. RAM's dash is the Ram Charge.
        static void StartDash(Player p, V2? d, World world, int level)
        {
            if (p.@char == "ram") { StartRush(p, world, level, d); return; }
            var c = CHARS[p.@char]; int L = level - 1;
            var D = d.Value;
            p.buf.dash = 99; p.dashCd = c.dash.cooldown;
            if (D.x != 0) p.facing = sign(D.x);
            p.dash = new DashState
            {
                dx = D.x, dy = D.y, t = JMath.Round(c.dash.ticks * (level != 0 ? DASH_CHARGE.ticks[L] : 1)), grounded = p.onGround, level = level,
                speed = c.dash.speed * (level != 0 ? DASH_CHARGE.speed[L] : 1), keep = level != 0 ? DASH_CHARGE.exitKeep[L] : c.dash.exitKeep,
                iframes = level != 0 ? DASH_CHARGE.iframes[L] : 0, instance = level == 3 ? world.NewInstance() : 0,
            };
            p.fastFall = false; p.crouch = false; p.dashChargeT = 0;
            SetState(p, "dash"); world.Emit("dash", new Ev { p = p, level = level, dx = D.x, dy = D.y });
        }

        // Planted and charging: skid to a stop, aim with the stick, let go to launch. Jump or parry cancel it;
        // a tap (released before DASH_CHARGE.tap) is an ordinary dash toward where you face.
        static void StateDashCharge(Player p, Cmd cmd, World world)
        {
            var C = DASH_CHARGE.charge;
            // The tap window counts in real ticks; the charge itself grows faster under Fix's boosts
            double t0 = p.dashChargeT; p.dashChargeT += p.dashChargeT < DASH_CHARGE.tap ? 1 : BoostRate(p);
            // For the first few ticks nothing changes, so a quick tap reads as an ordinary dash; then he plants
            if (p.dashChargeT >= DASH_CHARGE.tap) p.vx = approach(p.vx, 0, 70 * DT);
            ApplyGravity(p, cmd);
            Crossed(world, p, t0, p.dashChargeT, C, "dashLevel");
            if (JMath.Abs(cmd.mx) > 0.3) p.facing = sign(cmd.mx);
            if (TryParry(p, world)) { p.dashChargeT = 0; return; }
            if (p.buf.jump <= JUMP_BUFFER || !p.onGround)
            {
                p.dashChargeT = 0; SetState(p, "normal"); world.Emit("dashChargeEnd", new Ev { p = p });
                if (p.onGround) TryJump(p, cmd, world);
                return;
            }
            if (cmd.held.dash) return;
            double t = p.dashChargeT; int level = t >= C[2] ? 3 : t >= C[1] ? 2 : t >= C[0] ? 1 : 0;
            V2 d = Snap8(cmd.mx, cmd.my) ?? new V2(p.facing, 0);
            if (d.y < 0) d = new V2(or(sign(d.x), p.facing), 0);   // no digging into the floor
            StartDash(p, d, world, level);
        }

        static bool TryParry(Player p, World world)
        {
            if (p.buf.parry > PARRY_BUFFER) return false;
            if (marksman(p)) return TryDodge(p, world);        // Nova's Marksman kit dodges instead
            if (p.@char == "ram") return StartGuard(p, world);   // RAM raises the Rampart
            if (p.@char == "fix") return StartPatch(p, world);   // Fix runs the Patch Beam
            p.buf.parry = 99; p.parryT = 0; p.parryResult = null;
            SetState(p, "parry"); world.Emit("parryStart", new Ev { p = p });
            return true;
        }

        static bool TrySignature(Player p, Cmd cmd, World world)
        {
            if (p.buf.sig > ACTION_BUFFER) return false;
            if (p.@char == "ram" || p.@char == "fix") return false;   // (their abilities: RamAbilities, FixAbilities)
            if (p.@char == "nova")
            {
                if (marksman(p))
                {
                    // Marksman kit: the hard-light Aegis. Instant: he keeps moving and shooting. Pressing again detonates it.
                    if (p.aegis != null) { p.buf.sig = 99; world.DetonateAegis(p); return false; }
                    if (p.aegisCd > 0) return false;
                    p.buf.sig = 99; world.RaiseAegis(p);
                    return false;
                }
                if (p.bulwarkCd > 0) return false;
                p.buf.sig = 99; p.bulwarkCd = NOVA.bulwarkCd;
                SetState(p, "bulwark");
                return true;
            }
            // Every scarf Signature spends a scarf charge; Vanish is not spent while already hidden
            if (p.lashCharges <= 0 || (p.scarfMode == "veil" && p.veiled)) return false;
            p.buf.sig = 99; p.lashCharges--;
            if (p.lashRecharge <= 0) p.lashRecharge = ECHO.lashRecharge;
            // Vanish and Challenge are instant: no state change, so the current action carries on
            if (p.scarfMode == "veil")
            {
                p.veiled = true; p.veilCharge = SCARF.veilFade; p.veilBreakT = 0;
                world.ShakeOffTrackers(p); world.Emit("vanish", new Ev { p = p });
                return false;
            }
            if (p.scarfMode == "flare") { world.Challenge(p); return false; }
            var c = Chest(p);
            var target = world.FindLashTarget(p, c.x, c.y, p.aimX, p.aimY, ECHO.lashRange);
            p.lash = new LashState { tx = c.x + p.aimX * ECHO.lashRange, ty = c.y + p.aimY * ECHO.lashRange, target = target, len = 0, hit = false };
            if (target != null) { p.lash.tx = target.x; p.lash.ty = target.y + target.h * 0.55; }
            if (JMath.Abs(p.aimX) > 0.1) p.facing = sign(p.aimX);
            SetState(p, "lash"); world.Emit("lash", new Ev { p = p });
            return true;
        }

        static bool TryMelee(Player p, Cmd cmd, World world)
        {
            if (p.buf.melee > ACTION_BUFFER) return false;
            bool hunter = p.@char == "echo" && SETTINGS.echoKit == "hunter";
            // Echo on a wall: Wall Slash (before anything else, so a slide never turns it into something else)
            if (hunter && p.wallSliding && !p.onGround) { p.buf.melee = 99; StartMove(p, "echo_wall", world); return true; }
            // In the air, the secondary aimed down: the ground pound
            bool down = cmd.my < -0.55 || (p.aimFree && p.aimY < POUND.aimDown);
            bool moving = p.state == "dash" || p.state == "slide" || p.postDash <= 6 || p.boostT > 0 || p.launchedT > 0 || p.zipArriveT > 0;
            if (!p.onGround && down && !moving && (p.@char != "echo" || hunter)) { p.buf.melee = 99; StartPound(p, world); return true; }
            double tier = VbTier(p);
            if (tier > 0) { VelocityBreak(p, tier, world); return true; }
            // Rising attacks (up + melee; once per airtime in the air)
            if (p.@char != "echo" && cmd.my > 0.55 && (p.onGround || p.airRise))
            {
                p.buf.melee = 99; if (!p.onGround) p.airRise = false;
                StartMove(p, p.@char + "_rise", world); return true;
            }
            if (p.@char == "fix") return FixMelee(p, world);
            if (marksman(p))
            {
                // Marksman kit: close to an enemy, his bracer combo; otherwise his secondary weapon
                if (MeleeTarget(p, world) != null) { p.buf.melee = 99; StartMove(p, p.onGround ? "nova_k1" : "nova_kair", world); return true; }
                return PressSub(p, world);
            }
            p.buf.melee = 99;
            if (p.@char == "echo" && p.riposteT > 0) { StartMove(p, "echo_riposte", world); p.riposteT = 0; return true; }
            if (p.@char == "echo" && !p.onGround && cmd.my < -0.55)
            {
                // Pursuit kit: the dive (fast fall into a Velocity Break on landing)
                BreakVeil(p, world, "attack");
                p.vy = -FAST_FALL; p.vx = p.facing * 5;
                p.hitConfirm = false; p.instance = world.NewInstance();
                SetState(p, "dive"); world.Emit("dive", new Ev { p = p });
                return true;
            }
            string id;
            if (p.@char == "ram") id = p.onGround ? "ram_b1" : "ram_air";
            else if (p.@char == "nova") id = p.onGround ? "nova_jab1" : "nova_air";
            else if (!p.onGround) id = hunter ? (cmd.my > 0.55 ? "echo_spin" : "echo_ab1") : "echo_air1";
            else if (cmd.my > 0.55) id = "echo_rise";   // Echo's rising attack in either kit
            else id = hunter ? "echo_b1" : "echo_g1";
            StartMove(p, id, world);
            return true;
        }

        // Marksman kit: an enemy close enough in front for the bracer combo (or the lock-on target in reach)
        static Enemy MeleeTarget(Player p, World world)
        {
            double face = p.aimFree && JMath.Abs(p.aimX) > 0.2 ? sign(p.aimX) : p.facing;
            foreach (var e in world.enemies.Live())
            {
                if (e.dead) continue;
                double ahead = (e.x - p.x) * face, gap = ahead - e.w / 2 - p.w / 2;
                double dy = JMath.Abs(e.y + e.h / 2 - (p.y + p.h * 0.5));
                if (ahead > -0.2 && gap < MARKSMAN.melee.reach - 0.8 && dy < MARKSMAN.melee.up) return e;
                if (e == p.lockT && LockChosen(p) && JMath.Abs(e.x - p.x) < LOCK.magnet && dy < MARKSMAN.melee.up) return e;
            }
            return null;
        }

        // Echo's Dash Slash (Hunter kit's Velocity Break): a lunging cut along the dash that carries him through
        static void StartDashSlash(Player p, double tier, World world)
        {
            BreakVeil(p, world, "attack");
            p.buf.melee = 99;
            double sp = JMath.Hypot(p.vx, p.vy);
            double dx = sp > 0.5 ? p.vx / sp : p.facing, dy = sp > 0.5 ? p.vy / sp : 0;
            if (p.state == "dash" && p.dash != null) { dx = p.dash.dx; dy = p.dash.dy; }
            if (JMath.Abs(dy) < 0.45) { dy = 0; dx = or(sign(dx), p.facing); }
            double m = or(JMath.Hypot(dx, dy), 1); dx /= m; dy /= m;
            if (JMath.Abs(dx) > 0.2) p.facing = sign(dx);
            p.slash = new SlashState { tier = tier, dx = dx, dy = dy };
            p.hitConfirm = false; p.instance = world.NewInstance();
            p.boostT = 0; p.launchedT = 0; p.zipArriveT = 0; p.postDash = 99;
            SetState(p, "dashslash"); world.Emit("dashSlash", new Ev { p = p, tier = tier, dx = dx, dy = dy });
        }

        static void StateDashSlash(Player p, Cmd cmd, World world)
        {
            var s = p.slash; double t = p.st; int T = (int)s.tier - 1;
            if (t <= DASH_SLASH.ticks) { double v = DASH_SLASH.speed[T] * JMath.Pow(DASH_SLASH.keep, t); p.vx = s.dx * v; p.vy = s.dy * v; }
            else if (!p.onGround) { p.vx = approach(p.vx, cmd.mx * CHARS[p.@char].run * 0.5, 30 * DT); ApplyGravity(p, cmd); }
            else { p.vx *= 0.8; p.vy = -0.5; }
            if (t >= 2 && t <= DASH_SLASH.ticks - 3)
            {
                double cx = p.x + p.facing * DASH_SLASH.box.fx, cy = p.y + 0.95 + s.dy * 0.5;
                world.SpawnHitbox(new Hit
                {
                    owner = p, team = "p", x0 = cx - DASH_SLASH.box.w / 2, x1 = cx + DASH_SLASH.box.w / 2, y0 = cy - DASH_SLASH.box.h / 2, y1 = cy + DASH_SLASH.box.h / 2,
                    dmg = DASH_SLASH.dmg[T], poise = DASH_SLASH.poise[T], kb = new[] { p.facing * 7, 3.0 }, armorBreak = DASH_SLASH.armorBreak[T], instance = p.instance,
                    vbTier = s.tier, dashSlash = true,
                });
            }
            if (p.hitConfirm && t >= 4)
            {
                if (SETTINGS.vbRefund && !p.onGround) p.airDashes = 1;
                if (CancelInto(p, cmd, world)) return;
            }
            if (t >= DASH_SLASH.ticks + (p.hitConfirm ? DASH_SLASH.hitRecover : DASH_SLASH.recover)) SetState(p, "normal");
        }

        static void StartMove(Player p, string id, World world)
        {
            BreakVeil(p, world, "attack");
            p.moveId = id; p.move = MOVES[id]; p.queued = null; p.hitConfirm = false; p.instance = world.NewInstance();
            p.crouch = false; p.riseAir = !p.onGround;
            if (JMath.Abs(p.aimX) > 0.2 && p.aimFree) p.facing = sign(p.aimX);
            // Lock-on: turn to a target that is close, and step in toward it during the swing (lungeTo)
            p.lungeTo = null;
            var t = p.lockT;
            if (t != null && !t.dead && JMath.Abs(t.x - p.x) < LOCK.magnet && JMath.Abs(t.y - p.y) < 2.5)
            {
                p.facing = or(sign(t.x - p.x), p.facing); p.lungeTo = t;
                if (p.onGround && JMath.Abs(t.x - p.x) - t.w / 2 - p.w / 2 > 0.3) p.vx = p.facing * LOCK.lunge;   // the step starts at once
            }
            SetState(p, "attack"); world.Emit("swing", new Ev { p = p, id = id });
        }

        // A Velocity Break: Echo's Hunter kit turns it into the Dash Slash
        static void VelocityBreak(Player p, double tier, World world)
        {
            if (p.@char == "echo" && SETTINGS.echoKit == "hunter") StartDashSlash(p, JMath.Max(1, tier), world); else StartVB(p, tier, world);
        }

        static void StartVB(Player p, double tier, World world)
        {
            BreakVeil(p, world, "attack");
            p.buf.melee = 99;
            double sp = JMath.Hypot(p.vx, p.vy);
            double dx = sp > 0.5 ? p.vx / sp : p.facing, dy = sp > 0.5 ? p.vy / sp : 0;
            if (p.state == "dash" && p.dash != null) { dx = p.dash.dx; dy = p.dash.dy; }
            if (JMath.Abs(dx) > 0.2) p.facing = sign(dx);
            p.vbInfo = new VBInfo { tier = tier, dx = dx, dy = dy, keep = SETTINGS.vbStop == "keep30" ? 0.3 : 0, v0x = p.vx, v0y = p.vy };
            p.hitConfirm = false; p.instance = world.NewInstance();
            p.boostT = 0; p.launchedT = 0; p.zipArriveT = 0; p.postDash = 99;
            SetState(p, "vb"); world.Emit("vbStart", new Ev { p = p, tier = tier });
        }

        // Anything that may interrupt a state early (on hit-confirm or late whiff recovery)
        static bool CancelInto(Player p, Cmd cmd, World world, bool jump = true, bool dash = true, bool parry = true, bool sig = true, bool melee = true)
        {
            if (parry && TryParry(p, world)) return true;
            if (dash && TryDash(p, cmd, world)) return true;
            if (jump && TryJump(p, cmd, world)) return true;
            if (sig && TrySignature(p, cmd, world)) return true;
            if (melee && TryMelee(p, cmd, world)) return true;
            return false;
        }

        // ---- States ----

        static void HorizontalControl(Player p, Cmd cmd, World world, double scale = 1)
        {
            var c = CHARS[p.@char]; bool skates = marksman(p);
            double tgt = cmd.mx * (skates ? MARKSMAN.skate.top : c.run) * (p.crouch ? c.crouchSpeed : 1) * (p.thrusting ? MARKSMAN.boost.air : 1) * scale;
            bool rifle = p.rifleT >= HUNTER.rifle.raise;   // Echo's staff-rifle up: slower on foot
            if (rifle && p.onGround) tgt *= HUNTER.rifle.slow;
            bool firing = p.chargeT > 0 || p.fireCd > 0 || p.shootT > 0 || rifle;
            bool facingAim = (p.aimFree || firing) && JMath.Abs(p.aimX) > 0.2;
            if (facingAim)
            {
                if (cmd.mx != 0 && sign(cmd.mx) != sign(p.aimX)) tgt *= skates ? MARKSMAN.skate.backpedal : c.backpedal;
                p.facing = sign(p.aimX);
            }
            else if (p.lockT != null && JMath.Abs(cmd.mx) <= 0.1 && JMath.Abs(p.aimX) > 0.05)
            {
                p.facing = sign(p.aimX);   // standing still while locked on: face the target
            }
            else if (JMath.Abs(cmd.mx) > 0.1 && p.controlLock == 0)
            {
                p.facing = sign(cmd.mx);
            }
            if (p.controlLock > 0) return;
            if (p.onGround && skates) { SkateGround(p, tgt, world); return; }
            if (p.onGround)
            {
                double accel = (tgt != 0 && sign(tgt) == sign(p.vx)) || JMath.Abs(p.vx) < 0.1 ? c.accelG : c.decelG;
                p.vx = approach(p.vx, tgt, accel * DT);
            }
            else
            {
                // Keep dash-carried momentum in the air unless the player steers against it
                if (p.dashCarry && JMath.Abs(p.vx) > JMath.Abs(tgt) && sign(tgt) != -sign(p.vx)) return;
                p.vx = approach(p.vx, tgt, c.accelA * DT);
            }
        }

        // Skate-blade glide: a little slower to reach top speed, keeps momentum when the stick is let go, and carves
        // to a stop when reversed. Crouching at speed tucks into a low glide.
        static void SkateGround(Player p, double tgt, World world)
        {
            double speed = JMath.Abs(p.vx), a;
            if (p.crouch && speed > MARKSMAN.skate.tuckMin) { tgt = 0; a = MARKSMAN.skate.tuck; }
            else if (tgt == 0) a = MARKSMAN.skate.coast;
            else if (sign(tgt) != sign(p.vx) && speed > 0.5)
            {
                a = MARKSMAN.skate.carve;
                if (speed > 5 && p.carveT == 0) { p.carveT = 10; world.Emit("carve", new Ev { p = p }); }
            }
            else a = speed < JMath.Abs(tgt) ? MARKSMAN.skate.accel : MARKSMAN.skate.coast;
            p.vx = approach(p.vx, tgt, a * DT);
        }

        static void ApplyGravity(Player p, Cmd cmd, double mult = 1)
        {
            if (p.onGround && p.vy <= 0) { p.vy = -0.5; return; }
            double g = GRAVITY * mult;
            if (p.vy < 0) g *= FALL_MULT;
            else if (!cmd.held.jump && !p.dashCarry) g *= RISE_CUT_MULT;
            p.vy -= g * DT;
            double cap = p.fastFall ? FAST_FALL : MAX_FALL;
            if (p.vy < -cap) p.vy = -cap;
        }

        static string Charged(string ch) => ch == "nova" ? "nova_brace" : ch == "echo" ? "echo_charged" : ch == "ram" ? "ram_slam" : "fix_slam";

        static void StateNormal(Player p, Cmd cmd, World world)
        {
            var c = CHARS[p.@char];
            // RAM raises the Rampart, and Fix runs the Patch Beam, for as long as the button is held
            if (cmd.held.parry && p.@char == "ram" && !p.guardBroken) { StartGuard(p, world); StateGuard(p, cmd, world); return; }
            if (cmd.held.parry && p.@char == "fix") { StartPatch(p, world); StatePatch(p, cmd, world); return; }
            if (p.onGround && cmd.my < -0.55) p.crouch = true;
            else if (p.crouch && Level.HasHeadroom(p.x, p.y, p.w, c.height)) p.crouch = false;

            HorizontalControl(p, cmd, world);
            if (!p.onGround && cmd.my < -0.7 && p.vy < 3 && p.wallDir == 0) p.fastFall = true;
            if (!Thrust(p, cmd, world)) ApplyGravity(p, cmd);
            WallCling(p, cmd, true);
            CancelInto(p, cmd, world);
            if (p.state != "normal") { if (!cmd.held.melee) p.meleeCharged = false; return; }
            if (marksman(p)) return;   // the Marksman kit charges its secondary blaster instead (FireMarksman)
            // Charged melee: release after holding
            if (p.meleeCharged && !cmd.held.melee)
            {
                p.meleeCharged = false;
                p.tossArmed = false;
                StartMove(p, Charged(p.@char), world);
            }
            if (p.meleeHeldT >= 30 && p.state == "normal" && !p.meleeCharged) { p.meleeCharged = true; world.Emit("meleeCharged", new Ev { p = p }); }
        }

        // Wall slide (every character, in the normal, attack and parry states)
        static bool WallCling(Player p, Cmd cmd, bool turn)
        {
            var c = CHARS[p.@char]; bool was = p.wallPrev;
            bool on = false;
            if (!p.onGround && p.wallDir != 0 && p.vy <= 0.5)
            {
                bool toward = cmd.mx * p.wallDir > 0.3;
                if (toward) p.wallStick = WALL.stick;
                else if (was && p.wallStick > 0) p.wallStick--;
                on = toward || (was && p.wallStick > 0);
            }
            if (!on) { p.wallT = 0; return false; }
            p.wallT = was ? p.wallT + 1 : 0;
            double ramp = JMath.Max(0, JMath.Min(1, (p.wallT - WALL.grip) / WALL.ease));
            double target = cmd.my < -0.6 ? c.wall.slide * WALL.fast : WALL.gripSpeed + (c.wall.slide - WALL.gripSpeed) * ramp;
            if (p.vy < -target) p.vy = JMath.Min(-target, p.vy + (WALL.brake + GRAVITY * FALL_MULT) * DT);   // brake a fall into the slide
            else p.vy = JMath.Max(p.vy, -target);
            if (cmd.mx * p.wallDir <= 0.3) p.vx = p.wallDir * 0.5;              // grip: stay against the wall
            p.wallSliding = true; p.fastFall = false;
            if (turn) p.facing = -p.wallDir;
            return true;
        }

        // Marksman kit: light boosters. Returns true while thrusting.
        static bool Thrust(Player p, Cmd cmd, World world)
        {
            if (!marksman(p)) return false;
            bool start = cmd.pressed.jump && p.jumpsUsed >= 1 && p.wallDir == 0 && p.fuel >= MARKSMAN.boost.minStart;
            bool on = !p.onGround && !p.wallSliding && p.fuel > 0 && cmd.held.jump && (p.thrusting || start);
            if (on != p.thrusting) { p.thrusting = on; world.Emit(on ? "thrustOn" : "thrustOff", new Ev { p = p }); }
            if (!on) return false;
            p.fuel = JMath.Max(0, p.fuel - 1); p.fastFall = false;
            // They only add lift below their climb speed, so they never cut a rising jump short
            if (p.vy > MARKSMAN.boost.rise) p.vy -= GRAVITY * DT; else p.vy = approach(p.vy, MARKSMAN.boost.rise, MARKSMAN.boost.thrust * DT);
            return true;
        }

        static void StateDash(Player p, Cmd cmd, World world)
        {
            var c = CHARS[p.@char]; var d = p.dash;
            if (d.pursuit != null)
            {
                var t = d.pursuit;
                if (t.dead) d.pursuit = null;
                else
                {
                    double tx = t.x - sign(t.x - p.x) * (t.w / 2 + p.w / 2 + 0.2), ty = t.y + t.h * 0.3;
                    double dx = tx - p.x, dy = ty - p.y, m = JMath.Hypot(dx, dy);
                    if (m < 0.9) { p.zipArriveT = 12; d.t = 0; } else { d.dx = dx / m; d.dy = dy / m; if (JMath.Abs(d.dx) > 0.2) p.facing = sign(d.dx); }
                }
            }
            double boost = p.boostT > 0 ? 1.35 : 1, speed = or(d.speed, c.dash.speed);
            p.vx = d.dx * speed * boost; p.vy = d.dy * speed * boost;
            d.t--;
            if (d.level == 3)
            {
                // A full charge turns the dash into a strike through everything in its path (each enemy once)
                var S = DASH_CHARGE.strike;
                world.SpawnHitbox(new Hit
                {
                    owner = p, team = "p", x0 = p.x - 0.8, x1 = p.x + 0.8, y0 = p.y, y1 = p.y + p.h + 0.2, dmg = S.dmg, poise = S.poise,
                    kb = new[] { or(sign(d.dx), p.facing) * S.kb, 3.0 }, armorBreak = true, instance = d.instance, dashStrike = true,
                });
            }
            if (p.buf.jump <= JUMP_BUFFER && (d.grounded || p.coyote > 0) && d.dy <= 0)
            {
                // Dash-jump: a charged dash carries more speed into the jump, up to DASH_CHARGE.jumpCarry
                p.vy = c.jumpV; p.vx = d.dx * JMath.Min(speed, DASH_CHARGE.jumpCarry) * 0.85; p.dashCarry = true; p.onGround = false;
                p.buf.jump = 99; SetState(p, "normal"); world.Emit("jump", new Ev { p = p, dashJump = true });
                return;
            }
            if (p.buf.melee <= ACTION_BUFFER) { VelocityBreak(p, VbTier(p), world); return; }
            if (TryParry(p, world)) return;
            if (d.t <= 0 || truthy(p.hitWall))
            {
                p.vx = d.dx * speed * (d.keep ?? c.dash.exitKeep); p.vy = d.dy > 0 ? d.dy * speed * 0.4 : 0;
                p.postDash = 0; SetState(p, "normal");
            }
        }

        static void StateSlide(Player p, Cmd cmd, World world)
        {
            var c = CHARS[p.@char];
            p.vx *= c.slide.decay; ApplyGravity(p, cmd);
            if (TryJump(p, cmd, world)) { p.dashCarry = true; return; }
            if (p.buf.melee <= ACTION_BUFFER) { VelocityBreak(p, 1, world); return; }
            if (TryParry(p, world)) return;
            if (p.st >= c.slide.ticks || JMath.Abs(p.vx) < 2 || !p.onGround)
            {
                p.crouch = !Level.HasHeadroom(p.x, p.y, p.w, c.height);
                SetState(p, "normal");
            }
        }

        static void StateVB(Player p, Cmd cmd, World world)
        {
            var v = p.vbInfo; double t = p.st;
            if (t <= VB.stopTicks)
            {
                double k = t >= VB.stopTicks ? v.keep : 1 - (1 - v.keep) * (t / VB.stopTicks);
                p.vx = v.v0x * k; p.vy = v.v0y * k;
            }
            else if (!p.onGround)
            {
                if (t < VB.activeTo) p.vy = JMath.Max(p.vy, 0);         // brief hang for precision stops
                else ApplyGravity(p, cmd);
            }
            else { p.vx *= 0.8; p.vy = -0.5; }
            if (t >= VB.activeFrom && t < VB.activeTo)
            {
                bool down = v.dy < -0.6;
                double x0, x1, y0, y1;
                if (down) { x0 = p.x - 1.0; x1 = p.x + 1.0; y0 = p.y - 0.4; y1 = p.y + 1.0; }
                else { x0 = p.x + (p.facing > 0 ? 0 : -1.7); x1 = p.x + (p.facing > 0 ? 1.7 : 0); y0 = p.y + 0.2; y1 = p.y + 1.6; }
                var tier = VB.tiers[(int)v.tier];
                world.SpawnHitbox(new Hit
                {
                    owner = p, team = "p", x0 = x0, x1 = x1, y0 = y0, y1 = y1, dmg = tier.dmg, poise = tier.poise, kb = new[] { p.facing * tier.kb, down ? 4.0 : 3.0 },
                    armorBreak = tier.armorBreak, instance = p.instance, vbTier = v.tier,
                });
            }
            if (p.hitConfirm && t >= VB.activeFrom)
            {
                if (SETTINGS.vbRefund && !p.onGround) p.airDashes = 1;
                if (CancelInto(p, cmd, world)) return;
            }
            double end = VB.activeTo + (p.hitConfirm ? 4 : VB.whiffRecovery);
            if (t >= VB.activeTo + VB.driftAfter && !p.onGround) p.vx = approach(p.vx, cmd.mx * CHARS[p.@char].run * 0.5, 30 * DT);
            if (t >= end) SetState(p, "normal");
        }

        static void StateDive(Player p, Cmd cmd, World world)
        {
            p.vy = -FAST_FALL; p.fastFall = true;
            var hitBelow = world.EnemyBelow(p, 1.2);
            if (p.onGround || hitBelow != null || p.st > 90)
            {
                p.vbInfo = new VBInfo { tier = 2, dx = 0, dy = -1, keep = 0, v0x = p.vx, v0y = p.vy };
                p.hitConfirm = false; p.instance = world.NewInstance();
                SetState(p, "vb"); world.Emit("vbStart", new Ev { p = p, tier = 2 });
            }
        }

        // Ground pound. Phases: 'hold' (he hangs; holding the button charges it), 'drop' (a fast fall that hits what
        // it passes through), 'land' (the scatter blast has gone off; a short recovery).
        static void StartPound(Player p, World world)
        {
            BreakVeil(p, world, "attack");
            p.pound = new PoundState { phase = "hold", t = 0, level = 0, held = true, y0 = p.y };
            p.hitConfirm = false; p.instance = world.NewInstance();
            p.dashCarry = false; p.fastFall = false; p.lungeTo = null;
            if (JMath.Abs(p.aimX) > 0.2 && p.aimFree) p.facing = sign(p.aimX);
            SetState(p, "pound"); world.Emit("poundStart", new Ev { p = p });
        }

        static void StatePound(Player p, Cmd cmd, World world)
        {
            var S = p.pound;
            if (S == null) { SetState(p, "normal"); return; }
            S.t++;
            if (S.phase == "hold")
            {
                // The mid-air slowdown: his rise and drift die away fast and he sinks slowly while it charges
                p.vx = approach(p.vx, 0, 50 * DT); p.vy = approach(p.vy, -POUND.hang, 80 * DT); p.fastFall = false;
                if (!cmd.held.melee) S.held = false;
                if (S.held)
                {
                    S.c = S.c + BoostRate(p);
                    int lv = S.c >= POUND.charge[2] ? 3 : S.c >= POUND.charge[1] ? 2 : S.c >= POUND.charge[0] ? 1 : 0;
                    if (lv > S.level) { S.level = lv; world.Emit("poundLevel", new Ev { p = p, level = lv }); }
                }
                if (TryParry(p, world) || TryDash(p, cmd, world)) { p.pound = null; return; }
                if (p.onGround) { LandPound(p, world); return; }
                if ((!S.held && S.t >= POUND.windup) || S.t >= POUND.maxHold) { S.phase = "drop"; S.t = 0; S.y0 = p.y; p.instance = world.NewInstance(); world.Emit("poundDrop", new Ev { p = p, level = S.level }); }
                return;
            }
            if (S.phase == "drop")
            {
                // Echo's quick pound bounces off what it hits
                if (p.@char == "echo" && S.level == 0 && p.hitConfirm)
                {
                    p.vy = POUND.bounce; p.vx *= 0.5; p.airDashes = 1; p.jumpsUsed = 0; p.fastFall = false; p.dashCarry = false;
                    p.pound = null; SetState(p, "normal"); world.Emit("pogo", new Ev { p = p });
                    return;
                }
                p.vy = -POUND.speed; p.fastFall = true; p.vx *= 0.9;
                double k = 1 + 0.25 * S.level;
                world.SpawnHitbox(new Hit
                {
                    owner = p, team = "p", x0 = p.x - POUND.box.w / 2, x1 = p.x + POUND.box.w / 2, y0 = p.y - 0.7, y1 = p.y + POUND.box.h - 0.7,
                    dmg = POUND.drop.dmg * k, poise = POUND.drop.poise * k, kb = new[] { 0.0, -4 }, instance = p.instance, pound = true,
                });
                if (p.onGround) { LandPound(p, world); return; }
                if (S.t > POUND.maxDrop) { p.pound = null; SetState(p, "normal"); }   // fell a long way (a pit)
                return;
            }
            // 'land': the blast has gone off; a short recovery, cancellable once it has hit something
            p.vx *= 0.7; p.vy = -0.5;
            if (p.hitConfirm) S.hit = true;
            if (S.hit && S.t >= POUND.hitRecover && CancelInto(p, cmd, world, melee: false)) { p.pound = null; return; }
            if (S.t >= POUND.recover) { p.pound = null; SetState(p, "normal"); }
        }

        // The scatter blast: every enemy in reach is hit and thrown outward, away from the impact
        static void LandPound(Player p, World world)
        {
            var S = p.pound; var L = POUND.land[(int)S.level]; double fall = JMath.Max(0, S.y0 - p.y);
            // RAM's Meteor Drop lands harder and wider; Fix's sends out a repair pulse as well
            double K = p.@char == "ram" ? RAM.pound : 1;
            double r = (L.r + POUND.fallBonus * JMath.Min(1, fall / 12)) * K;
            int inst = world.NewInstance();
            world.SpawnHitbox(new Hit
            {
                owner = p, team = "p", x0 = p.x - r, x1 = p.x + r, y0 = p.y - 0.3, y1 = p.y + 1.6 + 0.3 * S.level, dmg = L.dmg * K, poise = L.poise * K,
                kb = new[] { L.kb, L.up }, radial = true, cx = p.x, armorBreak = L.armorBreak || K > 1, instance = inst, scatter = true, ramKnock = p.@char == "ram",
            });
            if (p.@char == "fix") world.RepairPulse(p, p.x, p.y, r + 1, FIX.poundHeal[(int)S.level]);
            S.phase = "land"; S.t = 0; S.inst = inst; S.hit = false;
            p.vx = 0; p.hitConfirm = false; p.hitstop = 2 + S.level;   // a beat of impact freeze, longer the bigger the pound
            world.Emit("poundLand", new Ev { p = p, x = p.x, y = p.y, level = S.level, r = r, f = fall });
        }

        static void StateAttack(Player p, Cmd cmd, World world)
        {
            var m = p.move; double t = p.st;
            double activeStart = m.su, activeEnd = m.su + m.ac, end = m.su + m.ac + m.rc;
            // A rising attack takes off (no rise cut); from the air it climbs a little less
            if (truthy(m.rise) && t == activeStart) { p.vy = m.rise * (p.riseAir ? or(m.airRise, 1) : 1); p.onGround = false; p.vx = p.facing * (m.fist ? 1.5 : 2.5); p.dashCarry = true; p.fastFall = false; }
            if (m.riseBlast != null && t == activeEnd)
            {
                // The Solar Uppercut's flare: a burst of light off the fist at the top of the climb
                var spec = m.riseBlast.Clone(); spec.armorBreak = false;
                world.Explode(new ExplodeArgs { owner = p, x = p.x + p.facing * 0.35, y = p.y + p.h + 0.55, spec = spec, kind = m.blastKind ?? "riseBlast", level = 1 });
            }
            if (t == activeStart)
            {
                if (m.jack && !p.riseAir) world.PlacePad(p);            // Jack-Up: the jack stays behind as a spring pad
                if (m.quake != null) world.SpawnQuake(p, m.quake);        // Seismic Slam: shockwaves both ways along the floor
                if (m.spark != null) world.SparkRing(p, m.spark);         // Torque Slam: a ring of sparks
            }
            if (m.sweep && t >= activeStart && t < activeEnd) world.SweepShots(p);   // Hydraulic Uplift: shots over him are swept away
            // On the ground an attack keeps pressing into the floor, so it never reads as airborne mid-swing
            if (p.onGround) { p.vx *= 0.82; p.vy = -0.5; }
            else { ApplyGravity(p, cmd, truthy(m.hoverAll) ? m.hoverAll : (m.hover && p.hitConfirm ? 0.25 : 1)); WallCling(p, cmd, false); }
            if (m.hover && p.hitConfirm && p.vy < 1.5) p.vy = 1.5;
            if (truthy(m.hoverAll) && p.vy < -3) p.vy = -3;
            if (t == activeStart && p.onGround && !m.launcher) p.vx += p.facing * 2.2;
            if (truthy(m.multi) && t > activeStart && t < activeEnd && (t - activeStart) % m.multi == 0) p.instance = world.NewInstance();
            if (t == activeStart && m.blastFist != null)
            {
                var spec = m.blastFist.Clone(); spec.armorBreak = false;
                world.Explode(new ExplodeArgs { owner = p, x = p.x + p.facing * 1.2, y = p.y + 1.1, spec = spec, kind = "blast", level = 1 });
            }
            if (t == activeStart && m.wave != null && SETTINGS.echoKit == "hunter")
            {
                // Crescent wave: the charged swing looses an energy crescent that flies on and cuts through shots
                var W = m.wave;
                world.SpawnProjectile(new Projectile
                {
                    team = "p", owner = p, x = p.x + p.facing * 1.0, y = p.y + 1.0, vx = p.facing * W.speed, vy = 0, ttl = W.ttl, r = W.r,
                    dmg = W.dmg, poise = W.poise, kbs = 6, pierce = true, intercept = true, interceptHeavy = true, kind = "wave",
                });
                world.Emit("crescent", new Ev { p = p, x = p.x + p.facing * 1.0, y = p.y + 1.0 });
            }
            if (p.lungeTo != null && t < activeEnd && p.onGround)
            {
                // Locked on: close the gap to the target until the swing lands
                var e = p.lungeTo; double gap = JMath.Abs(e.x - p.x) - e.w / 2 - p.w / 2;
                if (!e.dead && gap > 0.3) p.vx = p.facing * JMath.Min(LOCK.lunge, gap * 30); else p.lungeTo = null;
            }
            if (t >= activeStart && t < activeEnd)
            {
                var b = m.box; double cx = m.spin ? p.x : p.x + p.facing * b.fx;
                world.SpawnHitbox(new Hit
                {
                    owner = p, team = "p", x0 = cx - b.w / 2, x1 = cx + b.w / 2, y0 = p.y + b.y - b.h / 2, y1 = p.y + b.y + b.h / 2,
                    dmg = m.dmg, poise = m.poise, kb = new[] { p.facing * m.kb[0], m.kb[1] }, armorBreak = m.armorBreak, heavy = m.heavy,
                    launcher = m.launcher, shove = m.shove, instance = p.instance, moveId = p.moveId, spin = m.spin, cx = p.x,
                    wrench = m.wrench, ram = p.@char == "ram" && m.shield, ramKnock = p.@char == "ram",
                });
            }
            if (m.launcher && t == activeEnd && p.hitConfirm) p.vy = 9;   // Echo hops after a launched enemy
            if (p.buf.melee <= ACTION_BUFFER && m.next != null && t >= activeStart) p.queued = m.next;
            if (t >= activeEnd)
            {
                if (p.queued != null && t >= activeEnd + 2)
                {
                    string nxt = p.queued;
                    if (MOVES[nxt].air == !p.onGround || !MOVES[nxt].air) { p.buf.melee = 99; StartMove(p, nxt, world); return; }
                }
                bool lateWhiff = t >= activeEnd + JMath.Floor(m.rc * 0.6);
                if (p.hitConfirm) { if (CancelInto(p, cmd, world, melee: false)) return; }
                else if (lateWhiff) { if (CancelInto(p, cmd, world, jump: false, sig: false, melee: false)) return; }
            }
            if (t >= end) SetState(p, "normal");
        }

        static void StateParry(Player p, Cmd cmd, World world)
        {
            p.parryT++;
            if (p.onGround) p.vx *= 0.7; else { ApplyGravity(p, cmd); p.vx = approach(p.vx, cmd.mx * 2, 20 * DT); WallCling(p, cmd, true); }
            if (p.parryResult != null)
            {
                // Successful parry: short, cancellable recovery
                if (p.st > 3 && CancelInto(p, cmd, world)) return;
                if (p.st > 10) SetState(p, "normal");
                return;
            }
            if (p.parryT >= PARRY.window + PARRY.whiff) SetState(p, "normal");
        }

        static void StateHitstun(Player p, Cmd cmd, World world)
        {
            if (p.onGround) p.vx *= 0.85;
            ApplyGravity(p, cmd);
            if (p.st >= p.stun) SetState(p, "normal");
        }

        static void StateBulwark(Player p, Cmd cmd, World world)
        {
            if (p.onGround) p.vx *= 0.7; else { p.vy = JMath.Max(p.vy - 10 * DT, -2); }
            if (p.st == 3) world.BulwarkPulse(p);
            if (p.st >= 8 && CancelInto(p, cmd, world, sig: false)) return;
            if (p.st >= 14) SetState(p, "normal");
        }

        static void StateLash(Player p, Cmd cmd, World world)
        {
            var L = p.lash;
            if (p.onGround) p.vx *= 0.75; else { p.vy = JMath.Max(p.vy - 20 * DT, -3); }
            L.len = JMath.Min(1, p.st / 8);
            bool hunter = SETTINGS.echoKit == "hunter";
            if (p.st == 8 && L.target != null)
            {
                L.hit = true;
                bool heavy = L.target is Enemy te && !(te.light && te.armor <= 0);
                // Hunter kit: keep holding to reel a light enemy in; a heavy one waits a moment to tell yank from zip
                if (hunter && heavy && cmd.held.sig) L.pending = true;
                else { world.LashConnect(p, L.target, hunter && cmd.held.sig); if (p.state != "lash") return; }
            }
            if (L.pending && p.st == 13)
            {
                L.pending = false;
                world.LashConnect(p, L.target, cmd.held.sig);
                if (p.state != "lash") return;
            }
            if (p.st >= 8 && L.hit && !L.pending && CancelInto(p, cmd, world, sig: false)) { p.lash = null; return; }
            if (p.st >= (L.hit ? 14 : 20)) { p.lash = null; SetState(p, "normal"); }
        }

        static void StateZip(Player p, Cmd cmd, World world)
        {
            var z = p.zip; var tgt = z.target;
            double tx = tgt.x - sign(tgt.x - p.x) * (tgt.w / 2 + p.w / 2 + 0.1), ty = tgt.y + 0.2;
            double dx = tx - p.x, dy = ty - p.y, d = JMath.Hypot(dx, dy);
            if (d < 0.6 || p.st > 30 || tgt.dead || truthy(p.hitWall))
            {
                p.vx *= 0.6; p.vy *= 0.6; p.zipArriveT = 12; p.lash = null;
                SetState(p, "normal");
                return;
            }
            p.vx = dx / d * ECHO.zipSpeed; p.vy = dy / d * ECHO.zipSpeed;
            p.facing = or(sign(dx), p.facing);
            if (p.buf.melee <= ACTION_BUFFER && d < 3) { p.zipArriveT = 12; VelocityBreak(p, 2, world); }
        }

        static void UpdateDowned(Player p, Cmd cmd, World world)
        {
            p.vx = cmd.mx * 1.2; p.crouch = false;
            ApplyGravity(p, cmd);
            p.h = 0.6;
            Level.MoveBody(p, DT);
            p.downedT--;
            if (p.downedT <= 0) world.BleedOut(p);
        }

        // Echo's utility belt: throw a snare, or plant one at his feet (setting a trap keeps Veil up)
        static void UseSnare(Player p, World world, bool plant)
        {
            if (p.snares <= 0 || !CanFire(p)) return;
            if (plant) world.PlantSnare(p);
            else { world.ThrowSnare(p); BreakVeil(p, world, "attack"); }
            p.snares--;
            if (p.snareRecharge <= 0) p.snareRecharge = HUNTER.snareRecharge;
        }
        // Echo's sniper focus (0-1) after holding fire for t ticks
        public static double RifleFocus(double t) => JMath.Max(0, JMath.Min(1, (t - HUNTER.rifle.raise) / HUNTER.rifle.focus));

        // Charge levels reached going from t0 to t1 (a charge can grow by more than one a tick with Overcharge)
        static void Crossed(World world, Player p, double t0, double t1, double[] marks, string type, string sub = null)
        {
            for (int i = 0; i < marks.Length; i++)
                if (t0 < marks[i] && t1 >= marks[i]) world.Emit(type, new Ev { p = p, level = i + 1, sub = sub });
        }
        static int LevelOf(double t, double[] C) => t >= C[2] ? 3 : t >= C[1] ? 2 : t >= C[0] ? 1 : 0;
    }
}
