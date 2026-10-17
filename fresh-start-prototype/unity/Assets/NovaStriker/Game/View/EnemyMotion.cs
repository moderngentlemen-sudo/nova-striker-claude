// Animation polish for enemies (Settings: animPolish; presentation only), layered over whatever pose
// EnemyRigs.Animate set this frame: a recoil away from each hit with a flinch that grows with the damage, breathing
// and a hover bob at rest, a crouch that winds up before an attack and springs past upright as it releases, a squash
// on landing, and the first moment of a death collapse.
//
// Everything goes on the rig's flip node. Animate writes only flip.scale.x there, and absolutely, every frame; it
// never writes the flip's position, rotation or y/z scale. Many of the nodes below it are eased from their own last
// value instead (the Lockwarden's body tilt, the brute's and the post's arms, the turret's gun), so an offset added
// to them would be read back next frame and compound. On the flip, each frame's offset simply replaces the last.
// The flip's origin is the enemy's feet, so its tilt hinges at the ground and its squash keeps the feet down.
using System.Runtime.CompilerServices;
using NovaStriker.Sim;
using UnityEngine;
using static NovaStriker.Sim.Cfg;

namespace NovaStriker.Game
{
    public static class EnemyMotion
    {
        // Hits: the top tilts away from the attacker (radians) and the body is pushed back (metres), then springs
        // back; HIT_FULL is the share of health at which a hit reads at full size; a hit that only flashes (armour,
        // poise) counts as HIT_FLASH_ONLY of one. Heavy enemies and bosses react at HEAVY_SCALE.
        const float HIT_FULL = 0.2f, HIT_FLASH_ONLY = 0.3f, HIT_TILT = 0.07f, HIT_TILT_PER = 0.16f, HIT_PUSH = 0.06f, HIT_SQUASH = 0.06f;
        const float HIT_HZ = 3.4f, HIT_DAMP = 0.38f, HEAVY_SCALE = 0.5f;
        // Rest: breathing (a share of height) and a hover bob for fliers (metres)
        const float BREATH = 0.012f, BREATH_RATE = 2.4f, HOVER_BOB = 0.05f, HOVER_RATE = 1.7f;
        // Windup: a crouch and a lean back over the attack's windup, then a stretch and a lunge as it releases
        const float WINDUP_SQUASH = 0.09f, WINDUP_TILT = 0.07f, RELEASE_STRETCH = 0.08f, RELEASE_TILT = 0.1f, WINDUP_TICKS = 20f;
        // Landing: squash per m/s of fall speed, capped
        const float LAND_SQUASH_PER_SPEED = 0.006f, LAND_SQUASH_MAX = 0.14f;
        // Death: the first slump (the rig shrinks away under Animate) and a tip in the direction of the killing blow
        const float DEATH_SQUASH = 0.14f, DEATH_TICKS = 12f, DEATH_TIP = 0.12f;
        const float SQUASH_HZ = 4f, SQUASH_DAMP = 0.32f, TILT_HZ = 2.6f, TILT_DAMP = 0.42f;

        struct Spr { public float x, v; }
        const float TAU = Mathf.PI * 2;
        static void Step(ref Spr s, float target, float hz, float damp, float dt)
        {
            float w = TAU * hz; int n = Mathf.Clamp(Mathf.CeilToInt(dt * 240), 1, 8); float h = dt / n;
            for (int i = 0; i < n; i++) { s.v += (-w * w * (s.x - target) - 2 * damp * w * s.v) * h; s.x += s.v * h; }
        }
        static void Kick(ref Spr s, float amp, float hz) => s.v += amp * TAU * hz;
        static float Ease(float k) => k * k * (3 - 2 * k);

        sealed class St
        {
            public Spr tilt, push, sq;
            public bool init, applied, wasGround, wasDead;
            public float lastHp, lastFlash, airVy, lastDir = 1;
            public string lastState;
        }
        // (one per rig, gone with it: the view drops a rig when its enemy leaves the world)
        static readonly ConditionalWeakTable<EnemyRig, St> ALL = new ConditionalWeakTable<EnemyRig, St>();

        public static void Apply(EnemyRig R, Enemy e, float dt, float t)
        {
            if (!ALL.TryGetValue(R, out var s)) { s = new St(); ALL.Add(R, s); }
            var F = R.flip; float side = F.scale.x < 0 ? -1 : 1;
            if (!SETTINGS.animPolish)
            {
                if (s.applied) { s.applied = false; s.init = false; F.position.set(0, 0, 0); F.rotation.set(0, 0, 0); F.scale.set(side, 1, 1); }
                return;
            }
            dt = Mathf.Clamp(dt, 0, 0.1f);
            float hp = (float)e.hp, flash = (float)e.flash, facing = e.facing < 0 ? -1 : 1, vx = (float)e.vx;
            string st = e.state;
            if (!s.init) { s.init = true; s.tilt = s.push = s.sq = default; s.lastHp = hp; s.lastFlash = flash; s.wasGround = e.onGround; s.wasDead = e.dead; s.lastState = st; }
            bool heavy = e.boss || e.type == "brute" || e.type == "warden" || e.type == "charger";
            float size = heavy ? HEAVY_SCALE : 1, id = e.id * 1.37f;

            // Hits: health lost, or a flash on a hit that took none; pushed the way the knockback carries it (else back)
            bool hurt = hp < s.lastHp - 0.01f, flashed = flash > 0 && s.lastFlash <= 0;
            if (!e.dead && (hurt || flashed))
            {
                float sev = hurt && e.maxHp > 0 ? Mathf.Clamp01((s.lastHp - hp) / (float)e.maxHp / HIT_FULL) : 0;
                sev = Mathf.Max(sev, HIT_FLASH_ONLY) * size;
                float dir = Mathf.Abs(vx) > 1 ? Mathf.Sign(vx) : -facing; s.lastDir = dir;
                Kick(ref s.tilt, dir * (HIT_TILT + HIT_TILT_PER * sev), TILT_HZ); Kick(ref s.push, dir * HIT_PUSH * sev, HIT_HZ);
                Kick(ref s.sq, -HIT_SQUASH * sev, SQUASH_HZ);
            }
            s.lastHp = hp; s.lastFlash = flash;

            // Landing
            if (!e.flier && e.onGround && !s.wasGround && !e.dead) Kick(ref s.sq, -Mathf.Min(LAND_SQUASH_MAX, Mathf.Max(0, -s.airVy) * LAND_SQUASH_PER_SPEED) * (heavy ? 0.7f : 1), SQUASH_HZ);
            if (!e.onGround) s.airVy = (float)e.vy;
            s.wasGround = e.onGround;

            // Windup: crouch and lean back as it builds (springs that overshoot when the target lets go), then the release
            float sqT = 0, tiltT = 0;
            bool wind = st == "windup" || st == "slamWindup";
            if (wind && !e.dead)
            {
                double w = e.atk != null && e.atk.wind > 0 ? e.atk.wind : WINDUP_TICKS;
                float u = Ease(Mathf.Clamp01((float)(e.st / w)));
                sqT = -WINDUP_SQUASH * u * size; tiltT = -facing * WINDUP_TILT * u;
            }
            if (!wind && (s.lastState == "windup" || s.lastState == "slamWindup") && !e.dead)
            {
                Kick(ref s.sq, RELEASE_STRETCH * size, SQUASH_HZ); Kick(ref s.tilt, facing * RELEASE_TILT, TILT_HZ);
            }
            s.lastState = st;

            // Death: the first slump and a tip with the killing blow (the rig may be dropped at any moment after)
            if (e.dead)
            {
                if (!s.wasDead) Kick(ref s.tilt, s.lastDir * DEATH_TIP, TILT_HZ);
                sqT = -DEATH_SQUASH * Ease(Mathf.Clamp01((float)e.deathT / DEATH_TICKS));
            }
            s.wasDead = e.dead;

            Step(ref s.tilt, tiltT, TILT_HZ, TILT_DAMP, dt); Step(ref s.push, 0, HIT_HZ, HIT_DAMP, dt); Step(ref s.sq, sqT, SQUASH_HZ, SQUASH_DAMP, dt);

            // Rest: breathing for everyone still standing, and a slow bob for fliers
            bool rest = !e.dead && (st == "idle" || st == "approach" || st == "reposition") && Mathf.Abs(vx) < 0.5f;
            float breath = rest ? BREATH * Mathf.Sin(t * BREATH_RATE + id) : 0;
            float bob = e.flier && !e.dead ? HOVER_BOB * Mathf.Sin(t * HOVER_RATE + id) : 0;

            float sy = Mathf.Max(0.5f, 1 + s.sq.x + breath), sxz = 1 / Mathf.Sqrt(sy);
            // (the flip's own position and rotation sit outside its mirroring scale, so they are in world directions)
            F.position.set(s.push.x, bob, 0);
            F.rotation.set(0, 0, -s.tilt.x);
            F.scale.set(side * sxz, sy, sxz);
            s.applied = true;
        }
    }
}
