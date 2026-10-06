// Nova's absorbing shield (Unity build option; Settings: Nova's defence = shield). On the parry button, in place of
// the Marksman kit's dodge: held, a hard-light shield faces where he aims and blocks what comes from in front. Every
// blocked hit is absorbed as energy that makes his attacks hit harder (NOVA_SHIELD), and he glows brighter the more
// he holds. He can fire from behind it. The shield's stability wears down with each block and grows back while it is lowered.
using static NovaStriker.Sim.Cfg;
using static NovaStriker.Sim.U;

namespace NovaStriker.Sim
{
    public static partial class PlayerSim
    {
        public static bool NovaShieldOn => SETTINGS.novaDefense == "shield" && SETTINGS.novaKit == "marksman";

        // The damage multiplier the absorbed energy gives his attacks: +100% at a full 100, +150% overfilled to 150
        public static double AbsorbMult(Player p) => p.@char == "nova" && p.absorb > 0 ? 1 + NOVA_SHIELD.bonus * JMath.Min(NOVA_SHIELD.max, p.absorb) / 100 : 1;

        static bool StartNovaShield(Player p, World world)
        {
            if (p.nshieldBroken) return false;
            p.buf.parry = 99; p.crouch = false; p.dashChargeT = 0;
            // Raised again right after it came down: no fresh perfect window
            bool again = p.nshieldOffT < 12;
            p.nshieldT = again ? NOVA_SHIELD.perfect + 1 : 0;
            NovaShieldAim(p);
            SetState(p, "nshield");
            if (!again) world.Emit("nshieldOn", new Ev { p = p });
            return true;
        }
        static void NovaShieldAim(Player p)
        {
            double ax = p.aimX, ay = JMath.Max(-0.35, p.aimY);
            if (JMath.Abs(ax) < 0.15) ax = or(sign(ax), p.facing) * 0.15;
            double m = or(JMath.Hypot(ax, ay), 1);
            p.guardDir = new V2(ax / m, ay / m); p.facing = sign(ax);
        }
        static void EndNovaShield(Player p, World world)
        {
            p.nshieldOffT = 0;
            if (p.state == "nshield") SetState(p, "normal");
            world.Emit("nshieldOff", new Ev { p = p });
        }
        static void StateNovaShield(Player p, Cmd cmd, World world)
        {
            var c = CHARS["nova"];
            p.nshieldT++;
            if (!cmd.held.parry || p.nshieldBroken) { EndNovaShield(p, world); return; }
            NovaShieldAim(p);
            if (p.onGround) p.vx = approach(p.vx, cmd.mx * c.run * NOVA_SHIELD.walk, c.accelG * DT);
            else p.vx = approach(p.vx, cmd.mx * c.run * 0.6, c.accelA * DT);
            ApplyGravity(p, cmd);
            // He fires from behind it as usual (CanFire; a Level 4 beam lowers it). Out of the shield: melee, or a dash.
            // A jump keeps it up.
            if (p.buf.melee <= ACTION_BUFFER) { EndNovaShield(p, world); return; }
            if (TryDash(p, cmd, world)) { p.nshieldOffT = 0; world.Emit("nshieldOff", new Ev { p = p }); return; }
            if (TryJump(p, cmd, world)) p.state = "nshield";
        }

        // Every tick (Nova): stability grows back while the shield is down; absorbed energy holds, then drains
        static void TickNovaShield(Player p, World world)
        {
            p.nshieldOffT = JMath.Min(99, p.nshieldOffT + 1); p.nshieldBlockT = JMath.Min(999, p.nshieldBlockT + 1);
            if (p.state != "nshield" && p.nshieldBlockT > NOVA_SHIELD.delay && p.nshieldStab < NOVA_SHIELD.stability)
            {
                p.nshieldStab = JMath.Min(NOVA_SHIELD.stability, p.nshieldStab + NOVA_SHIELD.regen / 60 * BoostRate(p));
                if (p.nshieldBroken && p.nshieldStab >= NOVA_SHIELD.recover) { p.nshieldBroken = false; world.Emit("nshieldReady", new Ev { p = p }); }
            }
            if (p.absorb > 0)
            {
                p.absorbIdle = JMath.Min(999, p.absorbIdle + 1);
                if (p.absorbIdle > NOVA_SHIELD.hold) p.absorb = JMath.Max(0, p.absorb - NOVA_SHIELD.drain);
            }
        }

        // A hit his shield took (Combat.HitPlayer): absorbed as energy; stability spent unless it was a perfect block
        public static void NovaShieldBlock(Player p, World world, double dmg, bool perfect, double x, double y, bool heavy)
        {
            p.nshieldBlockT = 0; p.absorbIdle = 0;
            double was = p.absorb;
            p.absorb = JMath.Min(NOVA_SHIELD.max, p.absorb + (perfect ? NOVA_SHIELD.perfectGain : dmg * NOVA_SHIELD.gainPerDmg) * BoostRate(p));
            if (perfect) GainUlt(p, ULT.gain.perfect, world);
            else
            {
                p.nshieldStab -= dmg * NOVA_SHIELD.cost;
                GainUlt(p, dmg * ULT.gain.blocked, world);
                if (heavy && p.onGround) p.vx = -p.facing * 3;
            }
            world.Emit("nshieldBlock", new Ev { p = p, x = x, y = y, dmg = dmg, perfect = perfect, heavy = heavy, k = p.absorb / 100, level = JMath.Floor(p.absorb / 25),
                full = was < 100 && p.absorb >= 100, max = was < NOVA_SHIELD.max && p.absorb >= NOVA_SHIELD.max, frac = JMath.Max(0, p.nshieldStab / NOVA_SHIELD.stability) });
            if (perfect) world.Bark(p, "perfect", 0.3);
            if (p.nshieldStab <= 0)
            {
                p.nshieldStab = 0; p.nshieldBroken = true; p.nshieldOffT = 0;
                p.state = "hitstun"; p.st = 0; p.stun = NOVA_SHIELD.brokenStun; p.vx = -p.facing * 4; p.vy = 2;
                world.Emit("nshieldBreak", new Ev { p = p, x = x, y = y });
            }
        }

        // An unguarded hit spills some of the energy
        public static void NovaSpill(Player p, World world)
        {
            if (p.absorb <= 0) return;
            p.absorb *= 1 - NOVA_SHIELD.spill;
            world.Emit("absorbSpill", new Ev { p = p, k = p.absorb / 100 });
        }
    }
}
