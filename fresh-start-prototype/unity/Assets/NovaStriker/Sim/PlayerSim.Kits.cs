// Player controller, part two (port of player.js): lock-on, firing, and each character's kit.
using static NovaStriker.Sim.Cfg;
using static NovaStriker.Sim.U;

namespace NovaStriker.Sim
{
    public static partial class PlayerSim
    {
        // ---- Lock-on ----

        // A lock the player chose (any lock in manual mode; with automatic lock-on, one picked with the button)
        public static bool LockChosen(Player p) => SETTINGS.lockMode == "manual" || p.lockPicked;

        // A press locks onto the best target at once. While locked, a tap cycles to the next target and holding for
        // LOCK.hold ticks lets go. Automatic lock-on also locks the nearest enemy in sight whenever there is no target.
        static void UpdateLock(Player p, Cmd cmd, World world)
        {
            if (!SETTINGS.lockOn) { if (p.lockT != null) world.SetLock(p, null, "off"); p.lockSuspend = false; return; }
            bool auto = SETTINGS.lockMode != "manual";
            bool held = cmd.held != null && cmd.held.@lock, pressed = cmd.pressed != null && cmd.pressed.@lock;
            if (pressed)
            {
                p.lockHeld = 1; p.lockHoldDone = false;
                if (p.lockT == null || p.lockSuspend) { p.lockSuspend = false; world.SetLock(p, world.BestLockTarget(p), "on"); p.lockHoldDone = true; }
            }
            else if (held && p.lockHeld > 0)
            {
                p.lockHeld++;
                if (p.lockHeld >= LOCK.hold && !p.lockHoldDone) { p.lockHoldDone = true; world.SetLock(p, null, "release"); if (auto) p.lockSuspend = true; }
            }
            if (!held && p.lockHeld > 0)
            {
                if (!p.lockHoldDone && p.lockT != null) world.SetLock(p, world.NextLockTarget(p), "cycle");
                p.lockHeld = 0; p.lockHoldDone = false;
            }
            world.ValidateLock(p);
            if (auto && p.lockT == null && !p.lockSuspend) { var t = world.AutoLockTarget(p); if (t != null) world.SetLock(p, t, "auto"); }
            if (!auto) p.lockSuspend = false;
        }

        // ---- Firing ----

        public static bool CanFire(Player p)
        {
            var s = p.state;
            return s == "normal" || s == "dash" || s == "slide" || s == "lash" || s == "dodge" || (s == "attack" && p.hitConfirm);
        }

        static readonly double[] NOVA_SENTINEL_CHARGE = { NOVA.charge1, NOVA.charge2 };
        static readonly double[] MARKSMAN_CHARGE_L4 = { MARKSMAN.charge[0], MARKSMAN.charge[1], MARKSMAN.charge[2], MARKSMAN.beam.at };
        static readonly double[] RAM_CHARGE_L4 = { RAM.cannon.charge[0], RAM.cannon.charge[1], RAM.cannon.charge[2], RAM.beam.at };

        static void HandleFire(Player p, Cmd cmd, World world)
        {
            if (marksman(p)) { FireMarksman(p, cmd, world); return; }
            if (p.@char == "ram") { FireRam(p, cmd, world); return; }
            if (p.@char == "fix") { FireFix(p, cmd, world); return; }
            if (p.@char == "nova")
            {
                if (cmd.pressed.fire && p.fireCd == 0 && CanFire(p)) { world.FireShot(p, 0); p.fireCd = NOVA.shotCd; }
                if (cmd.held.fire && CanFire(p))
                {
                    double t0 = p.chargeT; p.chargeT += BoostRate(p);
                    Crossed(world, p, t0, p.chargeT, NOVA_SENTINEL_CHARGE, "chargeLevel");
                }
                if (cmd.released.fire || (!cmd.held.fire && p.chargeT > 0))
                {
                    if (p.chargeT >= NOVA.charge2) world.FireShot(p, 2);
                    else if (p.chargeT >= NOVA.charge1) world.FireShot(p, 1);
                    p.chargeT = 0;
                }
                return;
            }
            // Echo, Hunter kit: tap to throw a snare (crouch + tap plants one at your feet); hold to raise the
            // staff-rifle and let go to fire a long shot, or hold longer for a marking shot. Settings: Echo's utility
            // belt on LB moves the snares to LB, and a tap of fire is then a quick unscoped rifle shot.
            if (SETTINGS.echoKit == "hunter")
            {
                bool beltLB = SETTINGS.echoBelt == "lb";
                if (beltLB && cmd.pressed.sub) UseSnare(p, world, p.onGround && cmd.my < -0.55);
                if (cmd.pressed.fire) p.plantPress = p.onGround && cmd.my < -0.55;   // crouched when pressed: plant on release
                if (cmd.held.fire && CanFire(p))
                {
                    // Raising the rifle takes real time; the focus builds faster under Fix's boosts
                    double t0 = p.rifleT; p.rifleT += p.rifleT < HUNTER.rifle.raise ? 1 : BoostRate(p);
                    if (t0 < HUNTER.rifle.raise && p.rifleT >= HUNTER.rifle.raise) world.Emit("rifleRaise", new Ev { p = p });
                    else if (t0 < HUNTER.rifle.raise + HUNTER.rifle.focus && p.rifleT >= HUNTER.rifle.raise + HUNTER.rifle.focus) world.Emit("rifleFocus", new Ev { p = p });
                }
                if (!cmd.held.fire && p.rifleT > 0)
                {
                    double t = p.rifleT; p.rifleT = 0;
                    if (t < HUNTER.rifle.raise && !beltLB) UseSnare(p, world, p.onGround && (p.plantPress || cmd.my < -0.55));
                    else if (t < HUNTER.rifle.raise)
                    {
                        if (p.rifleCd == 0 && CanFire(p)) { world.FireSniper(p, 0); p.rifleCd = p.rifleCdMax = HUNTER.rifle.cd; BreakVeil(p, world, "attack"); }
                    }
                    else if (p.rifleCd == 0 && CanFire(p))
                    {
                        world.FireSniper(p, RifleFocus(t)); p.rifleCd = p.rifleCdMax = HUNTER.rifle.cd; BreakVeil(p, world, "attack");
                    }
                    else world.Emit("rifleLower", new Ev { p = p });
                }
                p.chargeT = 0;
                return;
            }
            // Echo, Pursuit kit: ranged options under test
            string mode = SETTINGS.echoRanged;
            if (mode == "C") { p.chargeT = 0; return; }
            if (mode == "A")
            {
                if (cmd.pressed.fire && p.tracerCd == 0 && CanFire(p)) { world.FireTracer(p); p.tracerCd = ECHO.tracerCd; BreakVeil(p, world, "attack"); }
                return;
            }
            if (cmd.pressed.fire && p.fireCd == 0 && p.cells > 0 && CanFire(p))
            {
                world.FireBolt(p); p.cells--; p.fireCd = ECHO.boltCd; BreakVeil(p, world, "attack");
            }
            if (cmd.held.fire) p.chargeT++;
            if (!cmd.held.fire && p.chargeT > 0)
            {
                if (p.chargeT >= ECHO.tracerHold && p.tracerCd == 0 && CanFire(p)) { world.FireTracer(p); p.tracerCd = ECHO.tracerCd; BreakVeil(p, world, "attack"); }
                p.chargeT = 0;
            }
        }

        // Marksman kit: tap for basic rounds, hold to charge the loaded attachment through three levels. Letting go
        // within MARKSMAN.perfectWindow ticks of level 3 is a Perfect Release. The secondary weapon works the same way
        // on the melee button.
        static void FireMarksman(Player p, Cmd cmd, World world)
        {
            var C = MARKSMAN.charge; double L4 = MARKSMAN.beam.at;
            double rate = (p.overcharge > 0 ? AEGIS.over.charge : 1) * BoostRate(p);
            if (cmd.pressed.fire && p.fireCd == 0 && CanFire(p)) { world.FireShot(p, 0); p.fireCd = NOVA.shotCd; }
            if (cmd.held.fire && CanFire(p))
            {
                double t0 = p.chargeT; p.chargeT += rate;
                Crossed(world, p, t0, p.chargeT, MARKSMAN_CHARGE_L4, "chargeLevel");
            }
            if (cmd.released.fire || (!cmd.held.fire && p.chargeT > 0))
            {
                double t = p.chargeT; int level = LevelOf(t, C); p.chargeT = 0;
                if (t >= L4) { if (CanFire(p)) StartBeam(p, world); }
                else if (level != 0) world.FireAttachment(p, p.attachment, level, level == 3 && t < C[2] + MARKSMAN.perfectWindow, t);
            }
            // Secondary weapon: holding charges it (not while a disc or well of his is still out); letting go fires the
            // charged level, or a tap (the Scatter fired its tap on the press, in TryMelee)
            bool ready = SubReady(p, world);
            if (cmd.held.melee && CanFire(p) && ready)
            {
                double t0 = p.burstT; p.burstT += rate;
                Crossed(world, p, t0, p.burstT, MARKSMAN.burst.charge, "burstLevel", p.sub);
            }
            if (!cmd.held.melee && (p.burstT > 0 || p.subArmed))
            {
                double t = p.burstT; int level = LevelOf(t, MARKSMAN.burst.charge); bool armed = p.subArmed; p.burstT = 0; p.subArmed = false;
                if (level != 0 && CanFire(p) && ready) world.FireSub(p, level, level == 3 && t < MARKSMAN.burst.charge[2] + MARKSMAN.burst.perfectWindow);
                else if (level == 0 && armed && CanFire(p) && ready && p.sub != "scatter") world.FireSub(p, 0, false);
            }
        }

        // The secondary button pressed with no enemy close enough for the combo. The Scatter fires at once; the others
        // fire when it is let go. A disc or well already out is called back or collapsed.
        static bool PressSub(Player p, World world)
        {
            if (!SubReady(p, world)) { p.buf.melee = 99; world.RecallSub(p, p.sub); return false; }
            if (p.burstCd > 0) return false;
            p.buf.melee = 99;
            if (p.state != "normal") { if (p.state == "dodge") p.dodge = null; SetState(p, "normal"); }
            if (p.sub == "scatter") world.FireSub(p, 0, false); else p.subArmed = true;
            return true;
        }
        // A disc or a well: one of each at a time
        static bool SubReady(Player p, World world) => !((p.sub == "disc" || p.sub == "well") && world.SubOut(p, p.sub));

        static void CycleSub(Player p, World world)
        {
            p.sub = SUBS[(System.Array.IndexOf(SUBS, p.sub) + 1) % SUBS.Length]; p.subSwCd = SUB.switchCd; p.burstT = 0; p.subArmed = false;
            world.Emit("subSwitch", new Ev { p = p, sub = p.sub });
        }

        // ---- Nova: the dodge (Marksman kit, on the parry button) ----
        static bool TryDodge(Player p, World world)
        {
            if (p.dodgeCd > 0 || (!p.onGround && !p.airDodge && !p.wallSliding)) return false;
            double mx = p.stick.x;
            // The way the stick points; with it centred, a backstep. Off a wall it always goes out from the wall.
            double dx = p.wallSliding ? -p.wallDir : JMath.Abs(mx) > 0.3 ? sign(mx) : -p.facing;
            p.buf.parry = 99; p.dodgeCd = DODGE.cd;
            if (!p.onGround) p.airDodge = false;
            p.dodge = new DodgeState { dx = dx, t = 0, air = !p.onGround, speed = p.onGround ? DODGE.speed : DODGE.airSpeed, perfect = false };
            p.crouch = false; p.fastFall = false; p.dashCarry = false; p.wallSliding = false;
            SetState(p, "dodge"); world.Emit("dodge", new Ev { p = p, dx = dx, air = p.dodge.air });
            return true;
        }
        static void StateDodge(Player p, Cmd cmd, World world)
        {
            var d = p.dodge;
            if (d == null) { SetState(p, "normal"); return; }
            d.t++;
            if (d.t <= DODGE.ticks - 4) p.vx = d.dx * d.speed * JMath.Pow(DODGE.keep, JMath.Max(0, d.t - 3));
            else p.vx = approach(p.vx, cmd.mx * CHARS[p.@char].run * 0.6, 60 * DT);
            if (d.air && d.t <= 8) p.vy = 0; else ApplyGravity(p, cmd);
            // It can be cut short: a jump from the fourth tick, anything else once he is hittable again
            if (d.t >= 4 && TryJump(p, cmd, world)) { p.dodge = null; return; }
            if (d.t > DODGE.iframes && CancelInto(p, cmd, world, parry: false, jump: false)) { if (p.state != "dodge") p.dodge = null; return; }
            if (d.t >= DODGE.ticks) { p.dodge = null; SetState(p, "normal"); }
        }

        // ---- Ultimates ----
        // Both triggers pulled together: both presses within ULT.chord ticks of each other and both still held
        public static void TrackChord(Player p, Cmd cmd)
        {
            p.chordP = cmd.pressed.parry ? 0 : JMath.Min(99, p.chordP + 1);
            p.chordF = cmd.pressed.fire ? 0 : JMath.Min(99, p.chordF + 1);
        }
        public static bool ChordReady(Player p, Cmd cmd) => cmd.pressed.ult ||
            (cmd.held.parry && cmd.held.fire && p.chordP <= ULT.chord && p.chordF <= ULT.chord);
        public static void GainUlt(Actor a, double amount, World world)
        {
            var p = a as Player;
            if (p == null || !(amount > 0) || p.state == "ult") return;
            double was = p.ult; p.ult = JMath.Min(ULT.max, p.ult + amount * BoostRate(p));
            if (was < ULT.max && p.ult >= ULT.max) world.Emit("ultReady", new Ev { p = p });
        }

        // Level 4: the sustained beam (Nova's, and RAM's Breach Beam)
        public static BeamDef BeamSpec(Player p) => p.@char == "ram" ? RAM.beam : MARKSMAN.beam;
        static void StartBeam(Player p, World world)
        {
            var B = BeamSpec(p); bool ram = p.@char == "ram";
            p.beam = new BeamState
            {
                t = B.ticks, dx = p.aimX, dy = p.aimY, mult = ram ? 1 : FocusMult(p) * SpendOvercharge(p), attach = ram ? "breach" : p.attachment, pulse = 0,
                family = new Family { focused = false, rocketed = true, perfect = false },
            };
            p.chargeT = 0;
            SetState(p, "beam"); world.Emit("beamStart", new Ev { p = p, attach = p.beam.attach, over = !ram && p.beam.mult > FocusMult(p) });
        }
        static void StateBeam(Player p, Cmd cmd, World world)
        {
            var B = BeamSpec(p); var b = p.beam;
            if (b == null) { SetState(p, "normal"); return; }
            double a0 = JMath.Atan2(b.dy, b.dx);
            double da = JMath.Atan2(p.aimY, p.aimX) - a0;
            while (da > JMath.PI) da -= 2 * JMath.PI; while (da < -JMath.PI) da += 2 * JMath.PI;
            double a = a0 + JMath.Max(-B.turn, JMath.Min(B.turn, da));
            b.dx = JMath.Cos(a); b.dy = JMath.Sin(a);
            if (JMath.Abs(b.dx) > 0.2) p.facing = sign(b.dx);
            // No push-back: on the ground he can creep along; in the air he hangs, sinking slowly, while it fires
            if (p.onGround) { p.vx = approach(p.vx, cmd.mx * CHARS[p.@char].run * B.slow, 40 * DT); p.vy = -0.5; }
            else { p.vx = approach(p.vx, 0, 20 * DT); p.vy = approach(p.vy, -B.hover, 40 * DT); p.fastFall = false; }
            if (TryParry(p, world) || TryDash(p, cmd, world)) { world.EndBeam(p, "cancel"); return; }
            world.BeamTick(p);
            if (--b.t <= 0) { world.EndBeam(p, "done"); SetState(p, "normal"); }
        }

        // Overcharge (from the Aegis) makes a charged release hit harder, at a cost; returns the damage multiplier
        public static double SpendOvercharge(Player p)
        {
            if (!(p.overcharge > 0)) return 1;
            p.overcharge = JMath.Max(0, p.overcharge - AEGIS.over.cost);
            return AEGIS.over.dmg;
        }

        // Charge stage from a charge counter: '', 'charging', 'L1', 'L2', 'perfect' (the release window), 'L3', or
        // 'L4' (the beam)
        static string StageOf(double t, double[] C, double win, double l4 = double.PositiveInfinity)
        {
            if (t <= 0) return "";
            if (t < C[0]) return "charging";
            if (t < C[1]) return "L1";
            if (t < C[2]) return "L2";
            if (t >= l4) return "L4";
            return t < C[2] + win ? "perfect" : "L3";
        }
        public static string ChargeStage(Player p)
        {
            if (marksman(p)) return StageOf(p.chargeT, MARKSMAN.charge, MARKSMAN.perfectWindow, MARKSMAN.beam.at);
            if (p.@char == "ram") return StageOf(p.chargeT, RAM.cannon.charge, 0, RAM.beam.at);
            if (p.@char == "fix") return StageOf(p.chargeT, FIX.rivet.charge, 0);
            // Sentinel kit: two levels (lance, rail) and no Perfect Release
            return p.chargeT <= 0 ? "" : p.chargeT < NOVA.charge1 ? "charging" : p.chargeT < NOVA.charge2 ? "L1" : "L2";
        }
        public static string BurstStage(Player p) => StageOf(p.burstT, MARKSMAN.burst.charge, MARKSMAN.burst.perfectWindow);

        // Rocket jump height (m, for a burst at his feet) earned by a shot charged for t ticks
        public static double RocketHeight(double t, string attach, bool perfect)
        {
            var C = MARKSMAN.charge;
            double f = JMath.Max(0, JMath.Min(1, (t - C[0]) / (C[2] - C[0])));
            double k = attach != null && MARKSMAN.rocket.attach.TryGetValue(attach, out var v) ? v : 1;
            return (perfect ? MARKSMAN.rocket.perfect : MARKSMAN.rocket.h[0] + (MARKSMAN.rocket.h[1] - MARKSMAN.rocket.h[0]) * f) * k;
        }

        // ---- Nova: bracer attachments and Focus ----

        static void CycleAttachment(Player p, World world)
        {
            var A = MARKSMAN.attachments;
            p.attachment = A[(System.Array.IndexOf(A, p.attachment) + 1) % A.Length]; p.modeCd = MARKSMAN.switchCd;
            world.Emit("attach", new Ev { p = p, attach = p.attachment });
        }

        public static double FocusMult(Player p) => marksman(p) ? 1 + MARKSMAN.focus.dmgPer * JMath.Floor(p.focus) : 1;

        public static void GainFocus(Player p, double amount, World world)
        {
            if (!marksman(p)) return;
            double before = JMath.Floor(p.focus);
            p.focus = JMath.Min(MARKSMAN.focus.max, p.focus + amount); p.focusT = MARKSMAN.focus.decay;
            if (JMath.Floor(p.focus) > before) world.Emit("focusUp", new Ev { p = p, level = JMath.Floor(p.focus) });
        }

        public static void LoseFocus(Player p, World world)
        {
            if (p.focus >= 1) world.Emit("focusLost", new Ev { p = p });
            p.focus = 0; p.focusT = 0;
        }

        // Focus drains one level after a quiet stretch, then another every decayStep ticks
        static void TickFocus(Player p, World world)
        {
            if (p.focus <= 0 || --p.focusT > 0) return;
            p.focus = JMath.Max(0, p.focus - 1); p.focusT = MARKSMAN.focus.decayStep;
        }

        // ---- Echo: Resolve and Rally ----

        static void TickEcho(Player p, World world, Cmd cmd)
        {
            double rate = BoostRate(p);
            if (p.snares < HUNTER.snareCharges)
            {
                p.snareRecharge -= rate;
                if (p.snareRecharge <= 0) { p.snares++; p.snareRecharge = p.snares < HUNTER.snareCharges ? HUNTER.snareRecharge : 0; }
            }
            if (p.leash != null)
            {
                var L = p.leash; L.t++;
                var e = L.e;
                if (!cmd.held.sig || L.t > HUNTER.leashTicks || e.dead || e.state != "caught" || p.state == "hitstun" || p.state == "downed" || p.state == "dead") world.ReleaseLeash(p);
            }
            if (p.lashCharges < ECHO.lashCharges)
            {
                p.lashRecharge -= rate;
                if (p.lashRecharge <= 0) { p.lashCharges++; p.lashRecharge = p.lashCharges < ECHO.lashCharges ? ECHO.lashRecharge : 0; }
            }
            TickScarf(p, world);
            bool near = world.NearestEnemyDist(p.x, p.y + 1) < 6 || (p.scarfMode == "flare" && p.targetedBy > 0);
            p.calmT = near ? 0 : p.calmT + 1;
            if (p.calmT > 120) p.resolve = JMath.Max(0, p.resolve - 5.0 / 60);
            if (p.strainT > 0) { p.strainT--; if (p.strainT == 0) p.strain = 0; }
        }

        public static void AddResolve(Player p, double amount)
        {
            if (p.@char != "echo") return;
            amount *= BoostRate(p);
            if (p.scarfMode == "flare") amount *= SCARF.flareResolve;
            p.resolve = JMath.Min(100, p.resolve + amount);
        }

        // Called when this player deals damage (Rally recovery + ranged refills)
        public static void OnDealtDamage(Player p, double dmg, bool isMelee)
        {
            if (p.@char == "fix") { p.scrap = JMath.Min(FIX.scrap.max, p.scrap + dmg * FIX.scrap.perDmg); return; }
            if (p.@char != "echo") return;
            if (p.strain > 0)
            {
                double heal = JMath.Min(p.strain, dmg * 3);
                p.strain -= heal; p.hp = JMath.Min(p.maxHp, p.hp + heal);
            }
            if (isMelee) { AddResolve(p, 6); if (p.cells < ECHO.cellsMax) p.cells++; }
        }

        // ---- Echo: scarf modes ----

        public static void SetScarfMode(Player p, string mode, World world)
        {
            if (p.leash != null) world.ReleaseLeash(p);
            p.scarfMode = mode; p.modeCd = SCARF.switchCd;
            p.veiled = false; p.veilCharge = 0; p.veilBreakT = 0;
            world.Emit("scarfMode", new Ev { p = p, mode = mode });
        }

        static void CycleScarf(Player p, World world)
        {
            var M = SCARF.modes;
            SetScarfMode(p, M[(System.Array.IndexOf(M, p.scarfMode) + 1) % M.Length], world);
        }

        // Attacking or taking a hit drops Veil; it re-arms after SCARF.veilRearm quiet ticks. Breaking it with an
        // attack while fully hidden makes that attack's first hit an ambush.
        public static void BreakVeil(Player p, World world, string reason)
        {
            if (p.@char != "echo" || p.scarfMode != "veil") return;
            bool wasHidden = p.veiled, fading = p.veilCharge > 0;
            p.veilBreakT = SCARF.veilRearm;
            if (!wasHidden && !fading) return;
            p.veiled = false; p.veilCharge = 0;
            if (wasHidden && reason == "attack") p.ambushT = SCARF.ambushWindow;
            world.Emit("veilBreak", new Ev { p = p, reason = reason, wasHidden = wasHidden });
        }

        // Flare widens the parry windows while at least one enemy is targeting Echo
        public static void ParryWindows(Player p, out double window, out double perfect)
        {
            bool on = p.@char == "echo" && p.scarfMode == "flare" && p.targetedBy > 0;
            window = PARRY.window + (on ? SCARF.flareParry : 0); perfect = PARRY.perfect + (on ? SCARF.flarePerfect : 0);
        }

        static void TickScarf(Player p, World world)
        {
            p.targetedBy = 0;
            foreach (var e in world.enemies.Live()) if (!e.dead && e.target == p) p.targetedBy++;
            if (p.state == "downed") { p.veiled = false; p.veilCharge = 0; return; }
            if (p.scarfMode == "veil")
            {
                if (p.veilBreakT > 0) p.veilBreakT--;
                else if (!p.veiled && ++p.veilCharge >= SCARF.veilFade) { p.veiled = true; world.Emit("veilOn", new Ev { p = p }); }
            }
            else if (p.scarfMode == "flare" && p.targetedBy > 0)
            {
                p.resolve = JMath.Min(100, p.resolve + SCARF.flareTrickle * JMath.Min(3, p.targetedBy) / 60);
            }
        }

        // ---- Fix's boosts on anyone ----
        // How much faster this player charges, recharges and fills their bars right now, up to FIX.maxRate
        public static double BoostRate(Player p)
        {
            double k = 1;
            if (p.overclockT > 0) k *= FIX.power.overclock.rate;
            if (p.tuneT > 0) k *= FIX.beam.tune;
            if (p.ampK > 1) k *= p.ampK;
            return JMath.Min(FIX.maxRate, k);
        }
        // Plating: an overshield that takes damage before health (never past `cap`)
        public static void AddPlate(Player p, double amount, double cap = PLATE_MAX) { if (p.plate < cap) p.plate = JMath.Min(cap, p.plate + amount); }

        // ---- RAM: the Rampart ----

        static void TickRam(Player p, World world)
        {
            p.guardOffT = JMath.Min(99, p.guardOffT + 1); p.blockT = JMath.Min(999, p.blockT + 1);
            // Integrity grows back once the shield has gone a while without blocking, and never while it is up
            if (p.state != "guard" && p.blockT > RAM.guard.delay && p.integrity < RAM.guard.integrity)
            {
                p.integrity = JMath.Min(RAM.guard.integrity, p.integrity + RAM.guard.regen / 60 * BoostRate(p));
                if (p.guardBroken && p.integrity >= RAM.guard.recover) { p.guardBroken = false; world.Emit("rampartReady", new Ev { p = p }); }
            }
            if (p.link != null) world.TickLink(p);
        }

        static bool StartGuard(Player p, World world)
        {
            if (p.guardBroken) return false;
            p.buf.parry = 99; p.crouch = false;
            // Raised again right after it came down: no fresh Perfect Guard window, and no new sound
            bool again = p.guardOffT < 12;
            p.guardT = again ? RAM.guard.perfect + 1 : 0;
            GuardAim(p);
            SetState(p, "guard");
            if (!again) world.Emit("guardOn", new Ev { p = p });
            return true;
        }
        // The Rampart faces where he aims: from straight ahead up to overhead, never lower than RAM.guard.minNy
        static void GuardAim(Player p)
        {
            double ax = p.aimX, ay = JMath.Max(RAM.guard.minNy, p.aimY);
            if (JMath.Abs(ax) < 0.15) ax = or(sign(ax), p.facing) * 0.15;   // overhead still leans the way he faces
            double m = or(JMath.Hypot(ax, ay), 1);
            p.guardDir = new V2(ax / m, ay / m); p.facing = sign(ax);
        }
        static void StateGuard(Player p, Cmd cmd, World world)
        {
            var c = CHARS["ram"];
            p.guardT++;
            if (!cmd.held.parry || p.guardBroken) { p.guardOffT = 0; SetState(p, "normal"); world.Emit("guardOff", new Ev { p = p }); return; }
            GuardAim(p);
            // Behind the shield he walks slowly either way, still facing out
            if (p.onGround) p.vx = approach(p.vx, cmd.mx * c.run * RAM.guard.walk, c.accelG * DT);
            else p.vx = approach(p.vx, cmd.mx * c.run * 0.6, c.accelA * DT);
            ApplyGravity(p, cmd);
            // Out of the guard: a shield shove (melee) or the Ram Charge (dash). A jump keeps the shield up.
            if (p.buf.melee <= ACTION_BUFFER) { p.buf.melee = 99; p.guardOffT = 0; world.Emit("guardOff", new Ev { p = p }); StartMove(p, "ram_bash", world); return; }
            if (TryDash(p, cmd, world)) { p.guardOffT = 0; world.Emit("guardOff", new Ev { p = p }); return; }
            if (TryJump(p, cmd, world)) p.state = "guard";
        }

        // RAM's abilities, usable from most states: Bulwark Wall (suit ability), Guardian Link (mode), Provoke (LB)
        static void RamAbilities(Player p, World world)
        {
            var s = p.state;
            if (s == "hitstun" || s == "ult" || s == "leap" || s == "dashCharge") return;
            if (p.buf.sig <= ACTION_BUFFER) { p.buf.sig = 99; if (p.wallCd <= 0) world.RaiseWall(p); else world.Emit("notReady", new Ev { p = p, what = "wall" }); }
            if (p.buf.mode <= ACTION_BUFFER) { p.buf.mode = 99; if (p.linkCd <= 0) world.StartLink(p); else world.Emit("notReady", new Ev { p = p, what = "link" }); }
            if (p.buf.sub <= ACTION_BUFFER) { p.buf.sub = 99; if (p.provokeCd <= 0) world.Provoke(p); else world.Emit("notReady", new Ev { p = p, what = "provoke" }); }
        }

        // The Ram Charge (an ordinary dash, level 0) and the Battering Ram (a charged dash, levels 1-3)
        static void StartRush(Player p, World world, int level, V2? d)
        {
            double dir = d != null && JMath.Abs(d.Value.x) > 0.2 ? sign(d.Value.x) : p.facing;
            p.buf.dash = 99; p.dashCd = CHARS["ram"].dash.cooldown; p.facing = dir;
            p.dashChargeT = 0; p.crouch = false; p.fastFall = false; p.dash = null;
            p.rush = new RushState { dx = dir, t = 0, level = level, air = !p.onGround, ticks = RAM.rush.ticks[level], speed = RAM.rush.speed[level] };
            SetState(p, "rush"); world.Emit("rush", new Ev { p = p, level = level, dx = dir });
        }
        static void StateRush(Player p, Cmd cmd, World world)
        {
            var r = p.rush;
            if (r == null) { SetState(p, "normal"); return; }
            r.t++;
            p.vx = r.dx * r.speed * (r.t > r.ticks - 3 ? 0.8 : 1);
            if (r.air) p.vy = 0; else ApplyGravity(p, cmd);   // run off a ledge and he falls, still charging
            // A jump out of a grounded charge carries its speed
            if (!r.air && p.buf.jump <= JUMP_BUFFER && (p.onGround || p.coyote > 0))
            {
                world.EndRush(p, "jump");
                p.vy = CHARS["ram"].jumpV; p.vx = r.dx * JMath.Min(r.speed, 16) * 0.85; p.dashCarry = true; p.onGround = false; p.coyote = 0; p.buf.jump = 99;
                SetState(p, "normal"); world.Emit("jump", new Ev { p = p, dashJump = true });
                return;
            }
            if (r.t >= r.ticks) { world.EndRush(p, "done"); p.vx = r.dx * r.speed * RAM.rush.keep; p.postDash = 0; SetState(p, "normal"); }
        }

        // Guardian Link's leap to a teammate's side: a single bound, steered all the way to where they are now
        static void StateLeap(Player p, Cmd cmd, World world)
        {
            var L = p.leap;
            if (L == null) { SetState(p, "normal"); return; }
            L.t++;
            var q = L.q; bool ok = q != null && world.players.Contains(q) && q.state != "dead";
            if (ok) { L.tx = q.x - L.side * (q.w / 2 + p.w / 2 + 0.25); L.ty = q.y; }
            double left = JMath.Max(1, RAM.link.leapTicks - L.t) * DT;
            p.vx = JMath.Max(-26, JMath.Min(26, (L.tx - p.x) / left));
            p.vy -= GRAVITY * DT; if (p.vy < -MAX_FALL * 1.4) p.vy = -MAX_FALL * 1.4;
            p.facing = or(sign(L.tx - p.x), p.facing); p.fastFall = false;
            if ((p.onGround && L.t > 4) || L.t > RAM.link.leapTicks + 30) world.LandLeap(p);
        }

        // The cannon: a tap fires a slug at once; holding charges a Breach Shot, fired when let go. While guarding,
        // fire is the Kinetic Release instead.
        static void FireRam(Player p, Cmd cmd, World world)
        {
            if (p.state == "guard")
            {
                if (cmd.pressed.fire && p.kinetic >= RAM.release.min) world.KineticRelease(p);
                else if (cmd.pressed.fire) world.Emit("notReady", new Ev { p = p, what = "kinetic" });
                p.chargeT = 0; return;
            }
            if (cmd.pressed.fire && p.fireCd == 0 && CanFire(p)) { world.FireSlug(p, 0); p.fireCd = RAM.cannon.cd; }
            if (cmd.held.fire && CanFire(p))
            {
                double t0 = p.chargeT; p.chargeT += BoostRate(p);
                Crossed(world, p, t0, p.chargeT, RAM_CHARGE_L4, "chargeLevel");
            }
            if (!cmd.held.fire && p.chargeT > 0)
            {
                double t = p.chargeT; int level = LevelOf(t, RAM.cannon.charge); p.chargeT = 0;
                if (t >= RAM.beam.at) { if (CanFire(p)) StartBeam(p, world); }   // Level 4: the Breach Beam
                else if (level != 0 && CanFire(p)) world.FireSlug(p, level);
            }
        }

        // ---- Fix: gadgets, power-ups, the Patch Beam and the Rivet Gun ----

        static void TickFix(Player p, World world)
        {
            if (p.scrap < FIX.scrap.max) p.scrap = JMath.Min(FIX.scrap.max, p.scrap + FIX.scrap.regen / 60 * BoostRate(p));
        }
        static void FixAbilities(Player p, World world)
        {
            var s = p.state;
            if (s == "hitstun" || s == "ult" || s == "dashCharge") return;
            if (p.buf.sig <= ACTION_BUFFER) { p.buf.sig = 99; world.DeployGadget(p); }
        }
        static void CycleGadget(Player p, World world)
        {
            var G = FIX.gadgets; p.gadgetSel = G[(System.Array.IndexOf(G, p.gadgetSel) + 1) % G.Length]; p.modeCd = SUB.switchCd;
            world.Emit("gadgetSelect", new Ev { p = p, kind = p.gadgetSel });
        }
        static void CyclePower(Player p, World world)
        {
            var P = FIX.powers; p.powerSel = P[(System.Array.IndexOf(P, p.powerSel) + 1) % P.Length]; p.subSwCd = SUB.switchCd;
            world.Emit("powerSelect", new Ev { p = p, kind = p.powerSel });
        }
        // Melee: close to an enemy or one of her gadgets it is the wrench. Otherwise the press readies a power-up: it is
        // tossed when the button comes up, unless it is held on into the Torque Slam. Without the Scrap it is the wrench.
        static bool FixMelee(Player p, World world)
        {
            p.buf.melee = 99;
            if (MeleeTarget(p, world) != null || world.GadgetNear(p) || p.scrap < FIX.power.cost) { StartMove(p, p.onGround ? "fix_w1" : "fix_air", world); return true; }
            p.tossArmed = true; p.tossT = 0;
            return true;
        }
        static void FixToss(Player p, Cmd cmd, World world)
        {
            p.tossT++;
            if (p.state == "hitstun" || p.state == "downed" || p.state == "ult" || p.meleeHeldT >= 30) { p.tossArmed = false; return; }
            if (!cmd.held.melee) { p.tossArmed = false; world.TossPower(p); }
        }

        static bool StartPatch(Player p, World world)
        {
            p.buf.parry = 99; p.crouch = false; p.chargeT = 0; p.rivetQ = 0; p.tossArmed = false;
            bool again = p.patch != null && p.patchOffT != null && world.tick - p.patchOffT.Value < 12;
            p.patch = new PatchState { target = world.PatchTarget(p, again ? p.patch.target : null), t = 0, self = false };
            SetState(p, "patch");
            if (!again) world.Emit("patchOn", new Ev { p = p, q = p.patch.target });
            return true;
        }
        static void StatePatch(Player p, Cmd cmd, World world)
        {
            if (!cmd.held.parry || p.patch == null) { p.patchOffT = world.tick; SetState(p, "normal"); world.Emit("patchOff", new Ev { p = p }); return; }
            var q = p.patch.target;
            HorizontalControl(p, cmd, world, FIX.beam.slow);
            if (q != null && JMath.Abs(cmd.mx) < 0.1) p.facing = or(sign(q.x - p.x), p.facing);   // standing still she turns to whoever she patches
            ApplyGravity(p, cmd);
            if (TryDash(p, cmd, world)) { p.patchOffT = world.tick; world.Emit("patchOff", new Ev { p = p }); return; }
            if (TryJump(p, cmd, world)) p.state = "patch";   // she keeps the beam on through a jump
            world.PatchTick(p);
        }

        // The Rivet Gun: a tap fires a burst of rivets; holding charges a Hot Rivet, fired when let go
        static void FireFix(Player p, Cmd cmd, World world)
        {
            if (p.state == "patch") { p.chargeT = 0; p.rivetQ = 0; return; }   // the beam takes both hands
            if (cmd.pressed.fire && p.fireCd == 0 && CanFire(p)) { p.rivetQ = FIX.rivet.n; p.rivetT = 0; p.fireCd = FIX.rivet.cd; }
            if (p.rivetQ > 0 && --p.rivetT <= 0) { if (CanFire(p)) world.FireRivet(p, FIX.rivet.n - p.rivetQ); p.rivetQ--; p.rivetT = FIX.rivet.every; }
            if (cmd.held.fire && CanFire(p))
            {
                double t0 = p.chargeT; p.chargeT += BoostRate(p);
                Crossed(world, p, t0, p.chargeT, FIX.rivet.charge, "chargeLevel");
            }
            if (!cmd.held.fire && p.chargeT > 0)
            {
                int level = LevelOf(p.chargeT, FIX.rivet.charge); p.chargeT = 0;
                if (level != 0 && CanFire(p)) world.FireHotRivet(p, level);
            }
        }
    }
}
