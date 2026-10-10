// The simulation's data: players, enemies, hits, projectiles and the world's other objects. The prototype keeps
// these as plain objects with fields added as needed; here every field is declared, with the prototype's names,
// and a field the prototype leaves undefined starts at zero, false or null (or is nullable where the difference
// matters).
using System.Collections.Generic;

namespace NovaStriker.Sim
{
    public struct V2 { public double x, y; public V2(double x, double y) { this.x = x; this.y = y; } }

    // ---- Input: one command frame per player per tick ----
    public sealed class Buttons
    {
        public bool jump, dash, melee, fire, parry, sig, mode, @lock, sub, ult;
        public static readonly string[] Names = { "jump", "dash", "melee", "fire", "parry", "sig", "mode", "lock", "sub", "ult" };
        public bool this[int i]
        {
            get
            {
                switch (i)
                {
                    case 0: return jump; case 1: return dash; case 2: return melee; case 3: return fire; case 4: return parry;
                    case 5: return sig; case 6: return mode; case 7: return @lock; case 8: return sub; default: return ult;
                }
            }
            set
            {
                switch (i)
                {
                    case 0: jump = value; break; case 1: dash = value; break; case 2: melee = value; break; case 3: fire = value; break;
                    case 4: parry = value; break; case 5: sig = value; break; case 6: mode = value; break; case 7: @lock = value; break;
                    case 8: sub = value; break; default: ult = value; break;
                }
            }
        }
        public Buttons Clone() => (Buttons)MemberwiseClone();
        public void CopyFrom(Buttons b) { for (int i = 0; i < 10; i++) this[i] = b[i]; }
    }
    public sealed class Cmd
    {
        public double mx, my, ax, ay;
        public bool aimFree;
        public int lane;   // a depth-lane hop asked for this tick: -1 toward the back, 1 toward the camera
        public Buttons held = new Buttons(), pressed = new Buttons(), released = new Buttons();
        public static readonly Cmd EMPTY = new Cmd();
    }

    // ---- Actors ----
    public class Actor : Body
    {
        public string kind;
        public double prevX, prevY;
        public double facing = 1;
        public string state = "normal";
        public double st;
        public double hitstop, stun;
        public bool dead;
    }

    // The input buffers: ticks since each button was pressed (99 = not recently)
    public sealed class Buf
    {
        public double jump = 99, dash = 99, melee = 99, fire = 99, parry = 99, sig = 99, mode = 99, sub = 99;
    }

    public sealed class DashState
    {
        public double dx, dy, t, level, speed, iframes;
        public double? keep;
        public bool grounded;
        public int instance;
        public Enemy pursuit;
    }
    public sealed class LashState { public double tx, ty, len; public Actor target; public bool hit, pending; }
    public sealed class ZipState { public Enemy target; }
    public sealed class LeashState { public Enemy e; public double t; }
    public sealed class SlashState { public double tier, dx, dy; }
    public sealed class VBInfo { public double tier, dx, dy, keep, v0x, v0y; }
    public sealed class PoundState { public string phase; public double t, level, y0, c; public bool held, hit; public int inst; }
    public sealed class DodgeState { public double dx, t, speed; public bool air, perfect; }
    // Every projectile from one charged release shares a family: it earns Focus once and rocket-jumps Nova once
    public sealed class Family { public bool focused, rocketed, perfect; public double chargeT, level; public string attach; }
    public struct BeamSeg { public double x0, y0, x1, y1, nx, ny; public bool wall; }
    public sealed class BeamState
    {
        public double t, dx, dy, mult, pulse;
        public string attach;
        public Dictionary<int, double> armor = new Dictionary<int, double>();
        public Family family;
        public List<BeamSeg> segs = new List<BeamSeg>();
    }
    public sealed class AegisState { public double hp, max, t; public HashSet<string> seen = new HashSet<string>(); }
    public sealed class Cut { public Enemy e; public double at; public int i; }
    public sealed class UltRun
    {
        public string kind;
        public double t, power, dx, dy, pulse, slamT, podX, podY, fin, end, x0, y0;
        public List<BeamSeg> segs;
        public List<Enemy> carried;
        public HashSet<Enemy> hit;
        public bool blocked;
        public List<Enemy> targets;
        public List<Cut> cuts;
    }
    public sealed class RushState
    {
        public double dx, t, level, ticks, speed;
        public bool air;
        public List<Enemy> carried = new List<Enemy>();
        public HashSet<Enemy> hit = new HashSet<Enemy>();
    }
    public sealed class LinkState { public Player q; public double t; }
    public sealed class LeapState { public Player q; public double t, side, tx, ty; }
    public sealed class PatchState { public Player target; public double t; public bool self; }

    public sealed class Player : Actor
    {
        public int slot;
        public string device;
        public string @char;
        public double coyote, jumpsUsed, airDashes = 1;
        public bool crouch, wallSliding;
        public double controlLock;
        public DashState dash;
        public double dashCd, postDash = 99;
        public bool dashCarry, fastFall;
        public double launchedT, zipArriveT, boostT;
        public bool iframe;
        public MoveDef move;
        public string moveId, queued;
        public bool hitConfirm;
        public int instance;
        public double chargeT, fireCd, meleeHeldT;
        public bool meleeCharged;
        public double hp, maxHp, strain, strainT;
        public double mercy;
        public double parryT;
        public string parryResult;
        public double riposteT;
        public double bulwarkCd, lashCharges, lashRecharge;
        public LashState lash;
        public ZipState zip;
        public double resolve, calmT, lastResolveHitT, cells, tracerCd;
        public double snares, snareRecharge;
        public LeashState leash;
        public string scarfMode = "tether";
        public double modeCd;
        public bool veiled;
        public double veilCharge, veilBreakT, ambushT, targetedBy;
        public string attachment = "lance";
        public double focus, focusT, burstCd, burstT, shootT, carveT;
        public double fuel;
        public bool thrusting;
        public double rockets, rocketT, rocketPow;
        public AegisState aegis;
        public double aegisCd, overcharge, overT;
        public BeamState beam;
        public SlashState slash;
        public PoundState pound;
        public string sub = "scatter";
        public double subSwCd;
        public bool subArmed;
        public DodgeState dodge;
        public double dodgeCd;
        public bool airDodge = true, airRise = true;
        public V2 stick;
        public double ult;
        public UltRun ultRun;
        public double chordP = 99, chordF = 99;
        // RAM: the Rampart's Integrity and stored Kinetic, the guard, the Ram Charge, and his three abilities
        public double integrity, kinetic, guardT = 99, guardOffT = 99, blockT = 99;
        public V2 guardDir = new V2(1, 0);
        public bool guardBroken;
        public RushState rush;
        public double wallCd, linkCd, provokeCd;
        public LinkState link;
        public LeapState leap;
        public double braceT;
        // Fix: Scrap, the selected gadget and power-up, the Patch Beam, and a tossed power-up waiting for the button
        public double scrap;
        public string gadgetSel = "pylon", powerSel = "overclock";
        public PatchState patch;
        public bool tossArmed;
        public double rivetQ, rivetT;
        // Support anyone can carry
        public double plate, overclockT, tuneT, ampK = 1;
        public bool fixRevive;
        public double padCd, furyT;
        // Nova's absorbing shield (NOVA_SHIELD)
        public double nshieldT, nshieldOffT = 99, absorb, absorbIdle = 999, nshieldStab = 100, nshieldBlockT = 999;
        public bool nshieldBroken;
        public double aimX = 1, aimY;
        public bool aimFree;
        public double wallT, wallStick, wallCoyote, lastWallDir, dashChargeT, rifleT, rifleCd;
        public Enemy lockT;
        public double lockHeld;
        public bool lockHoldDone, lockSuspend, lockPicked;
        public double lockLost;
        public Buf buf = new Buf();
        public double downedT, revive, respawnT;
        public bool secondWind = true;
        public double lastSafeX, lastSafeY, offscreenT, vbTierShown;
        // added along the way
        public bool wallPrev, riseAir, plantPress;
        public Enemy lungeTo;
        public VBInfo vbInfo;
        public HashSet<Enemy> spinHit;
        public double rifleCdMax, tossT, barkCd, reviveGain, autoRevive;
        public double? patchOffT;
        public Player reviveBy;
    }

    // An attack under way
    public struct LaserSpan { public double x0, x1, y0, y1, y; }
    public sealed class EnemyAtk
    {
        public int inst;
        public string kind, cat;
        public double wind, x0, hops, rec, n, shots, edge, tx, ty, dx, dy;
        public bool heavy, high, low;
        public LaserSpan span;
    }

    public sealed class Enemy : Actor
    {
        public int id;
        public string type;
        public double shieldDir = -1;
        public double maxHp, poise, poiseMax, armor, armorMax;
        public EnemyAtk atk;
        public string token;
        public Player target;
        public double cd, flash, deathT, tagged, slamCd, cycle;
        public double aimX, aimY;
        public string label = "";
        public bool light, flier, boss;
        public double homeX, homeY;
        public double hp;
        // set by encounters, bosses and play
        public string zone, enc;
        public bool add;
        public double phase, invuln, staggerCd, parried;
        public string lastAtk;
        public bool laserHigh, landed, dizzy;
        public double crashFor, shockT, wellT, slowT;
        public Player catcher;
        public double catchSide;
        public Player plowBy;
        public Player taunter;
        public double tauntT;
    }

    // ---- Hits: melee hitboxes, and the hit any attack lands ----
    public class Hit
    {
        public Actor owner;
        public string team;
        public double x0, x1, y0, y1;
        public double dmg, poise;
        public double[] kb;
        public bool armorBreak, heavy, launcher, shove;
        public int? instance;
        public double vbTier;
        public bool dashSlash, dashStrike, pound, radial, scatter, ramKnock, spin, wrench, ram, aegisBurst, quake;
        public double cx;
        public string moveId;
        public bool bulwark, rail, amplified, furied, ult, shock, well, unblockable, ground, snipe, beam, gadget, blastHit;
        public double stun;
        public string cat;
        public V2? at;
        public Projectile proj;
        public double vx;

        public Hit Clone() => (Hit)MemberwiseClone();
    }

    public sealed class SeekState { public Enemy target; public double delay, until, turn, age; }
    public sealed class DiscState { public string phase; public double t, @out, hover, dx, dy, speed; }
    public sealed class PrismInfo { public double shards, bounces, mult; }
    public sealed class StickInfo { public double fuse; public Blast blast; }
    public sealed class StuckInfo { public Enemy e; public double ox, oy, t; }
    public sealed class FalloffInfo { public double x, y, d; }
    public sealed class ClusterDef { public double n, speed, lift, fuse; public Blast blast; }

    public sealed class Projectile : Hit
    {
        public int lane; public bool laneSet;   // (depth lanes: its shooter's lane)
        public double x, y, px, py, vy;
        public double ttl = 60, r = 0.15;
        public HashSet<object> hitSet = new HashSet<object>();
        public bool dead;
        public bool intercept;
        public bool? interceptHeavy;
        public bool homing;
        public double level, speed;
        public string kind;
        public bool pierce;
        public double? pierceLeft;
        public Blast splash, blast, endBlast;
        public bool perfect;
        public Family family;
        public SeekState seek;
        public double gravity;
        public PrismInfo prism;
        public double bounces;
        public double kbs, kbY;          // the prototype's scalar kb (and kbY) on a projectile
        public FalloffInfo falloff;
        public double bouncy;
        public bool rest;
        public ClusterDef cluster;
        public DiscState disc;
        public bool ghost, snare, tracer, mark, deflected, reflected;
        public StickInfo stick;
        public StuckInfo stuck;
        public double slowT;

        public Projectile()
        {
            dmg = 1; poise = 6;
        }
    }

    // ---- The world's other objects ----
    public sealed class Shockwave
    {
        public Actor owner;
        public string team;
        public double x, y, dir, speed, ttl, dmg, poise, h;
        public int instance;
    }
    public sealed class Barrier
    {
        public string kind;
        public Player owner;
        public double x, y, nx, ny, half, ttl, max, hp, maxHp, hitT;
    }
    public sealed class Snare { public Player owner; public int lane; public double x, y, armT, ttl; public bool dead; }
    public sealed class Well
    {
        public int lane;
        public Player owner;
        public double x, y, px, py, vx, vy, t, level, r, life, mult;
        public string phase;
        public bool perfect, collapse, dead;
    }
    public sealed class Gadget
    {
        public int id, lane; // placement snapshot; moving the owner never moves the deployed device
        public string kind;
        public Player owner;
        public double x, y, py, gy, vy, level, pts, t, life, hp, maxHp, cd, rocketCd, aim, aimY, h, hitT;
        public bool landed, dead;
        public Enemy target;
    }
    public sealed class Pickup
    {
        public int id, lane;
        public string kind;
        public Player owner;
        public double x, y, px, py, vx, vy, t, life;
        public Player target;
        public bool rest, dead, level;
    }
    public sealed class UltCast
    {
        public List<Player> members = new List<Player>();
        public string phase, name;
        public double t, len, power;
        public bool team;
    }
    public sealed class EncounterState { public EncounterDef def; public string state; public int wave; public Enemy boss; }
    public sealed class Cam { public double x, y, dist, halfW, halfH; }
    public struct ChainPt { public double x, y; public double? depth; public bool fizzle; } // snapshot each target's lane before damage/removal

    // ---- Events: what happened this tick, for rendering, audio, haptics and the interface ----
    public sealed class Ev
    {
        public double? depth;      // immutable presentation origin, metres toward camera; captured when emitted
        public string type;
        public Player p, q, by, saved;
        public Enemy e;
        public Actor owner;
        public Gadget g;
        public LevelBox box;
        public Barrier barrier;
        public Projectile pr;
        public List<Player> members;
        public List<Enemy> targets;
        public List<ChainPt> pts;
        public string[] chars;
        public double x, y, x0, y0, x1, y1, dx, dy, ax, ay, nx, ny, vy, r, k, h, f, n, i, dir, level, tier, ticks, dmg, frac, left, power, sp, slot, cost, top, ang, cone;
        public string kind, attach, mode, sub, id, text, name, title, why, what, reason, cat, source, surface;
        public bool perfect, over, heavy, big, charged, climb, closed, full, on, parried, secondWind, wasHidden, weak, wall, bolt, rivet, cannon, dashJump,
            armored, broken, max, spot, flourish, team, hard, pit, high, air;
        public double fall;
        public double crits;
    }
}
