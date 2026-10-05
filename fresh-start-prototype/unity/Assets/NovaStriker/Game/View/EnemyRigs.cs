// Enemy construct rigs (enemyRigs.js): pale ceramic plating, graphite joints, reserved hostile magenta energy.
using System.Collections.Generic;
using NovaStriker.Game.Three;
using NovaStriker.Sim;
using UnityEngine;
using static NovaStriker.Sim.Cfg;

namespace NovaStriker.Game
{
    public sealed class EnemyMats { public TMat plate, joint, energy; }
    public sealed class EnemyParts
    {
        public TObj core, eye, torso, shield, gun, muzzle, head, armN, armF, arm, rotor, tube, ram, chest, pod, hammer, blade, hull, cannon;
        public List<TMesh> plates, horns, tubes;
        public List<TObj> legsC, rotors;
        public List<(TObj hip, TObj knee)> legsW;
        public TMat shieldMat;
    }
    public sealed class EnemyRig
    {
        public TObj root, flip, body;
        public EnemyMats mats;
        public string type;
        public EnemyParts parts = new EnemyParts();
        public float lean, bob, phase;
        // set by the view
        public TMesh tag; public TObj dizzy; public List<(TMesh s, TMesh back)> stars;
    }

    public static class EnemyRigs
    {
        static Mesh RBox(float w, float h, float d, float r = 0.06f) =>
            Geo.RoundedBox(w, h, d, 3, Mathf.Min(r, Mathf.Min(w / 2 - 1e-3f, Mathf.Min(h / 2 - 1e-3f, d / 2 - 1e-3f))));
        static Mesh Cap(float r, float len) => Geo.Capsule(r, len, 4, 10);
        static EnemyMats Mats() => new EnemyMats
        {
            plate = new TMat(TMat.Kind.Physical) { colorHex = 0xe6e9f0, roughness = 0.36f, metalness = 0.15f, clearcoat = 0.5f, clearcoatRoughness = 0.2f, emissiveHex = 0xffffff, emissiveIntensity = 0 },
            joint = new TMat { colorHex = 0x2b2f3a, roughness = 0.6f, metalness = 0.2f },
            energy = new TMat { colorCss = HOSTILE, emissiveCss = HOSTILE, emissiveIntensity = 2.2f, roughness = 0.3f },
        };
        static EnemyMats RimAll(EnemyMats M) { Rigs.AddRim(M.plate, "#ff9cc5", 0.35f); Rigs.AddRim(M.joint, "#ff7fb2", 0.3f); return M; }
        static TMesh Add(TObj parent, Mesh geo, TMat mat, float x = 0, float y = 0, float z = 0)
        {
            var m = new TMesh(geo, mat, x, y, z); m.cast = true; parent.add(m); return m;
        }
        static TObj Grp(TObj parent, float x = 0, float y = 0, float z = 0) { var g = Group.Make(x, y, z); parent.add(g); return g; }
        const float PI = Mathf.PI;

        public static EnemyRig Build(string type)
        {
            var M = RimAll(Mats());
            var root = Group.Make(0, 0, 0, "enemy"); var flip = Grp(root); var body = Grp(flip);
            var R = new EnemyRig { root = root, flip = flip, body = body, mats = M, type = type };
            var P = R.parts;
            switch (type)
            {
                case "swarmer":
                    P.core = Grp(body, 0, 0.42f, 0);
                    Add(P.core, RBox(0.62f, 0.42f, 0.52f, 0.16f), M.plate);
                    Add(P.core, RBox(0.3f, 0.12f, 0.54f, 0.05f), M.joint, -0.05f, 0.2f);
                    P.eye = Add(P.core, Geo.Sphere(0.11f, 14, 10), M.energy, 0.29f, 0.03f);
                    foreach (var (x, z) in new[] { (0.18f, 0.2f), (-0.18f, 0.2f), (0.18f, -0.2f), (-0.18f, -0.2f) })
                    {
                        var l = Add(body, Cap(0.045f, 0.3f), M.joint, x, 0.2f, z); l.rotation.z = x > 0 ? -0.5f : 0.5f;
                    }
                    break;
                case "shield":
                    foreach (var z in new[] { 0.16f, -0.16f }) Add(body, Cap(0.09f, 0.62f), M.joint, 0, 0.42f, z);
                    P.torso = Grp(body, 0, 1.0f, 0);
                    Add(P.torso, RBox(0.55f, 0.78f, 0.6f, 0.12f), M.plate, 0, 0.25f);
                    P.eye = Add(P.torso, RBox(0.06f, 0.07f, 0.3f, 0.02f), M.energy, 0.28f, 0.58f);
                    P.shield = Grp(P.torso, 0.55f, 0.1f, 0);
                    Add(P.shield, RBox(0.14f, 1.55f, 1.05f, 0.08f), M.plate);
                    foreach (var y in new[] { -0.74f, 0.74f }) Add(P.shield, RBox(0.16f, 0.05f, 1.0f, 0.02f), M.energy, 0.02f, y);
                    foreach (var z in new[] { -0.5f, 0.5f }) Add(P.shield, RBox(0.16f, 1.45f, 0.05f, 0.02f), M.energy, 0.02f, 0, z);
                    break;
                case "sniper":
                {
                    foreach (var z in new[] { 0.12f, -0.12f }) Add(body, Cap(0.07f, 0.7f), M.joint, 0, 0.45f, z);
                    P.torso = Grp(body, 0, 1.05f, 0);
                    Add(P.torso, RBox(0.4f, 0.62f, 0.46f, 0.1f), M.plate, 0, 0.2f);
                    P.eye = Add(P.torso, Geo.Sphere(0.07f, 12, 10), M.energy, 0.2f, 0.52f);
                    P.gun = Grp(P.torso, 0.05f, 0.3f, 0.26f);
                    var barrel = Add(P.gun, Geo.Cylinder(0.05f, 0.06f, 1.5f, 10), M.joint, 0.75f, 0, 0); barrel.rotation.z = PI / 2;
                    Add(P.gun, RBox(0.4f, 0.14f, 0.12f, 0.03f), M.plate, 0.1f, 0, 0);
                    P.muzzle = Add(P.gun, Geo.Sphere(0.06f, 10, 8), M.energy, 1.52f, 0, 0);
                    break;
                }
                case "brute":
                    foreach (var z in new[] { 0.36f, -0.36f }) Add(body, Cap(0.2f, 0.8f), M.joint, 0, 0.62f, z);
                    P.torso = Grp(body, 0, 1.4f, 0);
                    Add(P.torso, RBox(1.1f, 1.05f, 1.1f, 0.25f), M.joint, 0, 0.35f);
                    P.core = Add(P.torso, Geo.Sphere(0.22f, 16, 12), M.energy, 0.5f, 0.4f);
                    P.plates = new List<TMesh> {
                        Add(P.torso, RBox(0.3f, 0.8f, 0.95f, 0.12f), M.plate, 0.52f, 0.35f),
                        Add(P.torso, RBox(0.7f, 0.3f, 0.45f, 0.12f), M.plate, 0, 0.98f, 0.42f),
                        Add(P.torso, RBox(0.7f, 0.3f, 0.45f, 0.12f), M.plate, 0, 0.98f, -0.42f),
                    };
                    P.head = Add(P.torso, RBox(0.34f, 0.3f, 0.36f, 0.1f), M.plate, 0.3f, 1.02f);
                    Add(P.head, RBox(0.06f, 0.06f, 0.28f, 0.02f), M.energy, 0.17f, 0.02f);
                    P.armN = Grp(P.torso, 0, 0.8f, 0.72f); P.armF = Grp(P.torso, 0, 0.8f, -0.72f);
                    foreach (var a in new[] { P.armN, P.armF })
                    {
                        Add(a, Cap(0.17f, 0.75f), M.joint, 0, -0.45f);
                        Add(a, RBox(0.5f, 0.45f, 0.45f, 0.14f), M.plate, 0.05f, -1.0f);
                    }
                    break;
                case "post":
                    Add(body, Geo.Cylinder(0.5f, 0.6f, 0.25f, 20), M.joint, 0, 0.12f);
                    Add(body, Cap(0.2f, 1.4f), M.plate, 0, 1.0f);
                    P.arm = Grp(body, 0, 1.45f, 0.3f);
                    Add(P.arm, RBox(1.3f, 0.2f, 0.2f, 0.06f), M.plate, 0.6f, 0);
                    P.eye = Add(body, Geo.Sphere(0.12f, 14, 10), M.energy, 0.18f, 1.8f);
                    break;
                case "turret":
                {
                    Add(body, Geo.Sphere(0.42f, 18, 12, 0, PI * 2, 0, PI / 2), M.plate, 0, 0);
                    P.gun = Grp(body, 0, 0.25f, 0);
                    var b = Add(P.gun, Geo.Cylinder(0.07f, 0.09f, 0.8f, 10), M.joint, 0.4f, 0, 0); b.rotation.z = PI / 2;
                    P.eye = Add(P.gun, Geo.Sphere(0.07f, 10, 8), M.energy, 0.82f, 0, 0);
                    break;
                }
                case "drone":
                {
                    P.core = Grp(body, 0, 0.3f, 0);
                    Add(P.core, Geo.Sphere(0.3f, 18, 12), M.plate).scale.set(1.2f, 0.72f, 1.2f);
                    P.eye = Add(P.core, Geo.Sphere(0.1f, 12, 10), M.energy, 0.31f, -0.02f);
                    Add(P.core, RBox(0.5f, 0.04f, 0.05f, 0.01f), M.energy, 0, -0.2f);
                    foreach (var z in new[] { 0.26f, -0.26f }) Add(P.core, RBox(0.3f, 0.12f, 0.05f, 0.02f), M.joint, -0.12f, -0.06f, z);
                    P.rotor = Grp(P.core, 0, 0.24f, 0);
                    var ring = Add(P.rotor, Geo.Torus(0.44f, 0.035f, 6, 28), M.joint); ring.rotation.x = PI / 2;
                    for (int i = 0; i < 3; i++) { var blade = Add(P.rotor, RBox(0.82f, 0.02f, 0.08f, 0.01f), M.plate); blade.rotation.y = i * PI / 3; }
                    break;
                }
                case "mortar":
                    Add(body, Geo.Cylinder(0.56f, 0.64f, 0.3f, 20), M.joint, 0, 0.15f);
                    P.torso = Grp(body, 0, 0.55f, 0);
                    Add(P.torso, RBox(0.82f, 0.5f, 0.82f, 0.14f), M.plate);
                    P.eye = Add(P.torso, RBox(0.05f, 0.07f, 0.5f, 0.02f), M.energy, 0.41f, 0.06f);
                    P.tube = Grp(P.torso, 0.05f, 0.22f, 0);
                    Add(P.tube, Geo.Cylinder(0.17f, 0.21f, 0.95f, 14), M.plate, 0, 0.47f);
                    P.muzzle = Add(P.tube, Geo.Torus(0.17f, 0.045f, 6, 16), M.energy, 0, 0.95f); P.muzzle.rotation.x = PI / 2;
                    P.tube.rotation.z = -0.45f;
                    break;
                case "charger":
                    P.legsC = new List<TObj>();
                    foreach (var (x, z) in new[] { (0.38f, 0.3f), (-0.38f, 0.3f), (0.38f, -0.3f), (-0.38f, -0.3f) })
                    {
                        var l = Grp(body, x, 0.62f, z); Add(l, Cap(0.09f, 0.42f), M.joint, 0, -0.3f); P.legsC.Add(l);
                    }
                    P.torso = Grp(body, 0, 0.9f, 0);
                    Add(P.torso, RBox(1.1f, 0.66f, 0.82f, 0.2f), M.joint, -0.05f, 0);
                    P.ram = Add(P.torso, RBox(0.3f, 0.78f, 0.92f, 0.12f), M.plate, 0.56f, 0.02f);
                    foreach (var y in new[] { -0.12f, 0.16f }) Add(P.ram, RBox(0.04f, 0.05f, 0.6f, 0.02f), M.energy, 0.16f, y);
                    P.horns = new List<TMesh>();
                    foreach (var z in new[] { 0.3f, -0.3f }) { var h = Add(P.torso, Geo.Cone(0.07f, 0.4f, 6), M.energy, 0.66f, 0.46f, z); h.rotation.z = -1.0f; P.horns.Add(h); }
                    P.plates = new List<TMesh> { Add(P.torso, RBox(0.72f, 0.16f, 0.86f, 0.07f), M.plate, -0.12f, 0.4f) };
                    break;
                case "warden":
                    P.legsW = new List<(TObj, TObj)>();
                    foreach (var z in new[] { 0.5f, -0.5f })
                    {
                        TObj hip = Grp(body, 0, 1.55f, z), knee = Grp(hip, 0.05f, -0.75f, 0);
                        Add(hip, Cap(0.24f, 0.55f), M.joint, 0, -0.38f); Add(hip, RBox(0.5f, 0.55f, 0.42f, 0.14f), M.plate, 0.08f, -0.3f);
                        Add(knee, Cap(0.2f, 0.5f), M.joint, 0, -0.35f); Add(knee, RBox(0.42f, 0.5f, 0.38f, 0.12f), M.plate, 0.12f, -0.35f);
                        Add(knee, RBox(0.8f, 0.22f, 0.5f, 0.08f), M.joint, 0.12f, -0.72f);
                        P.legsW.Add((hip, knee));
                    }
                    P.torso = Grp(body, 0, 1.65f, 0);
                    Add(P.torso, RBox(1.25f, 0.4f, 1.0f, 0.14f), M.joint, 0, 0);
                    P.chest = Grp(P.torso, 0, 0.3f, 0);
                    Add(P.chest, RBox(1.5f, 1.05f, 1.3f, 0.3f), M.joint, 0, 0.55f);
                    P.core = Add(P.chest, Geo.Sphere(0.26f, 18, 14), M.energy, 0.72f, 0.55f);
                    P.head = Grp(P.chest, 0.42f, 1.2f, 0);
                    Add(P.head, RBox(0.55f, 0.38f, 0.5f, 0.12f), M.plate);
                    P.eye = Add(P.head, RBox(0.06f, 0.08f, 0.42f, 0.02f), M.energy, 0.28f, 0.02f);
                    P.pod = Grp(P.chest, -0.7f, 1.05f, 0);
                    Add(P.pod, RBox(0.6f, 0.55f, 0.9f, 0.1f), M.plate);
                    P.tubes = new List<TMesh>();
                    for (int i = 0; i < 5; i++) P.tubes.Add(Add(P.pod, Geo.Cylinder(0.07f, 0.07f, 0.06f, 10), M.energy, -0.05f + (i % 2) * 0.12f, 0.29f, -0.3f + i * 0.15f));
                    P.plates = new List<TMesh> { Add(P.chest, RBox(0.3f, 0.8f, 1.05f, 0.12f), M.plate, 0.72f, 0.45f), Add(P.chest, RBox(0.8f, 0.35f, 0.55f, 0.14f), M.plate, 0, 1.12f, 0.62f),
                        Add(P.chest, RBox(0.8f, 0.35f, 0.55f, 0.14f), M.plate, 0, 1.12f, -0.62f), Add(P.head, RBox(0.4f, 0.14f, 0.3f, 0.05f), M.plate, -0.05f, 0.25f) };
                    P.armN = Grp(P.chest, 0.05f, 0.95f, 0.95f); P.armF = Grp(P.chest, 0.05f, 0.95f, -0.95f);
                    Add(P.armN, Cap(0.2f, 0.7f), M.joint, 0, -0.45f); P.hammer = Grp(P.armN, 0.05f, -1.05f, 0);
                    Add(P.hammer, RBox(0.85f, 0.72f, 0.72f, 0.16f), M.plate); Add(P.hammer, RBox(0.06f, 0.5f, 0.6f, 0.02f), M.energy, 0.44f, 0);
                    Add(P.armF, Cap(0.18f, 0.65f), M.joint, 0, -0.42f);
                    P.blade = Add(P.armF, RBox(0.22f, 1.7f, 0.1f, 0.04f), M.plate, 0.1f, -1.4f);
                    Add(P.blade, RBox(0.05f, 1.6f, 0.12f, 0.02f), M.energy, 0.12f, 0);
                    break;
                case "stormcaller":
                {
                    P.hull = Grp(body, 0, 0.75f, 0);
                    Add(P.hull, Geo.Sphere(0.62f, 24, 16), M.plate).scale.set(2.3f, 0.72f, 1.25f);
                    Add(P.hull, Geo.Sphere(0.5f, 20, 12), M.joint, -0.1f, -0.2f).scale.set(2.2f, 0.55f, 1.1f);
                    P.eye = Add(P.hull, Geo.Sphere(0.2f, 16, 12), M.energy, 1.28f, 0.02f);
                    Add(P.hull, RBox(1.8f, 0.05f, 0.05f, 0.02f), M.energy, 0.1f, 0.18f, 0.62f); Add(P.hull, RBox(1.8f, 0.05f, 0.05f, 0.02f), M.energy, 0.1f, 0.18f, -0.62f);
                    P.cannon = Grp(P.hull, 0.8f, -0.38f, 0);
                    var barrel = Add(P.cannon, Geo.Cylinder(0.09f, 0.12f, 0.9f, 12), M.joint, 0.45f, 0, 0); barrel.rotation.z = PI / 2;
                    P.muzzle = Add(P.cannon, Geo.Sphere(0.08f, 10, 8), M.energy, 0.92f, 0, 0);
                    foreach (var z in new[] { 0.5f, -0.5f }) { var fin = Add(P.hull, RBox(0.55f, 0.35f, 0.05f, 0.03f), M.plate, -1.25f, 0.28f, z * 0.5f); fin.rotation.z = 0.5f; }
                    P.rotors = new List<TObj>();
                    foreach (var z in new[] { 1.1f, -1.1f })
                    {
                        var nac = Grp(P.hull, -0.15f, 0.05f, z);
                        Add(nac, Geo.Cylinder(0.28f, 0.34f, 0.3f, 16), M.joint);
                        Add(nac, Geo.Cylinder(0.2f, 0.2f, 0.04f, 16), M.energy, 0, -0.17f);
                        var rotor = Grp(nac, 0, 0.2f, 0);
                        var ring = Add(rotor, Geo.Torus(0.62f, 0.04f, 6, 32), M.joint); ring.rotation.x = PI / 2;
                        for (int i = 0; i < 3; i++) { var b = Add(rotor, RBox(1.2f, 0.02f, 0.1f, 0.01f), M.plate); b.rotation.y = i * PI / 3; }
                        P.rotors.Add(rotor);
                    }
                    var shieldMat = new TMat { colorCss = HOSTILE, emissiveCss = HOSTILE, emissiveIntensity = 1.6f, transparent = true, opacity = 0.22f, depthWrite = false, side = Side.Double };
                    var sh = new TMesh(Geo.Icosahedron(1, 1), shieldMat); sh.scale.set(1.9f, 0.95f, 1.4f); sh.visible = false; P.hull.add(sh);
                    P.shield = sh; P.shieldMat = shieldMat;
                    break;
                }
            }
            return R;
        }

        static float Ease(float v, float target, float k) => v + (target - v) * k;

        public static void Animate(EnemyRig R, Enemy e, float dt, float t)
        {
            var P = R.parts; var M = R.mats;
            R.flip.scale.x = (float)(e.type == "shield" ? e.shieldDir : e.facing);
            float lean = 0, glow = 2.2f;
            string s = e.state;
            if (s == "windup" || s == "slamWindup" || s == "aim")
            {
                lean = -0.18f; glow = 3.5f + Mathf.Sin(t * 30) * 1.2f;
                if (s == "slamWindup") { lean = -0.3f; glow = 5 + Mathf.Sin(t * 45) * 2; R.body.position.x = Mathf.Sin(t * 70) * 0.03f; }
            }
            else if (s == "lock") glow = 6;
            else if (s == "attack") lean = 0.35f;
            else if (s == "charge") { lean = 0.28f; glow = 5; }
            else if (s == "dazed") { lean = -0.3f + Mathf.Sin(t * 7) * 0.1f; glow = 0.5f; }
            else if (s == "stagger") { lean = -0.35f + Mathf.Sin(t * 9) * 0.12f; glow = 0.6f; }
            else if (s == "hitstun") lean = -0.2f;
            else if (s == "launched") lean = Mathf.Sin(t * 12) * 0.5f;
            else if (s == "caught") lean = 0.3f;
            if (s != "slamWindup") R.body.position.x = 0;
            R.lean += (lean - R.lean) * 0.35f;
            R.body.rotation.z = -R.lean;
            M.energy.emissiveIntensity = glow;
            M.plate.emissiveIntensity = e.flash > 0 ? 0.9f : 0;
            float evx = (float)e.vx;

            switch (e.type)
            {
                case "swarmer":
                {
                    bool moving = Mathf.Abs(evx) > 0.5f && e.onGround;
                    R.bob = moving ? Mathf.Abs(Mathf.Sin(t * 16)) * 0.12f : R.bob * 0.8f;
                    P.core.position.y = 0.42f + R.bob + (s == "windup" ? -0.1f : 0);
                    break;
                }
                case "sniper":
                    if (s == "aim" || s == "lock")
                    {
                        float dx = (float)((e.aimX - e.x) * e.facing), dy = (float)(e.aimY - (e.y + 1.35));
                        P.gun.rotation.z = Mathf.Atan2(dy, Mathf.Max(0.1f, dx));
                    }
                    else P.gun.rotation.z *= 0.9f;
                    break;
                case "brute":
                {
                    for (int i = 0; i < P.plates.Count; i++) P.plates[i].visible = i < e.armor;
                    float swing = s == "windup" ? -1.2f : s == "attack" ? 1.4f : s == "slamWindup" ? -2.6f : s == "slamRecover" && e.st < 8 ? 1.2f : 0;
                    P.armN.rotation.z += (swing - P.armN.rotation.z) * 0.3f;
                    P.armF.rotation.z += ((s == "slamWindup" ? -2.6f : s == "slamRecover" && e.st < 8 ? 1.2f : 0.1f) - P.armF.rotation.z) * 0.3f;
                    M.energy.emissiveIntensity = glow + (3 - (float)e.armor) * 0.8f;
                    break;
                }
                case "post":
                {
                    float target = s == "windup" ? -0.9f : s == "attack" ? 1.1f : 0;
                    P.arm.rotation.z += (target - P.arm.rotation.z) * 0.3f;
                    break;
                }
                case "drone":
                    P.rotor.rotation.y += dt * (s == "windup" ? 40 : 22);
                    P.core.rotation.z = -Mathf.Clamp(evx * 0.06f * (float)e.facing, -0.4f, 0.4f);
                    break;
                case "mortar":
                {
                    float raise = s == "windup" ? Mathf.Min(1, (float)e.st / 20) : 0, kick = s == "recover" && e.st < 10 ? 1 - (float)e.st / 10 : 0;
                    P.tube.rotation.z = -0.45f + raise * 0.25f - kick * 0.2f;
                    P.tube.scale.y = 1 - kick * 0.18f;
                    break;
                }
                case "charger":
                {
                    foreach (var pl in P.plates) pl.visible = e.armor > 0;
                    float run = s == "charge" ? 26 : Mathf.Abs(evx) > 0.5f ? 12 : 0, paw = s == "windup" ? Mathf.Sin(t * 22) * 0.35f : 0;
                    for (int i = 0; i < P.legsC.Count; i++) P.legsC[i].rotation.z = run != 0 ? Mathf.Sin(t * run + i * PI / 2) * 0.55f : i == 0 ? paw : 0;
                    break;
                }
                case "turret":
                {
                    var tg = e.target;
                    if (tg != null) { float a = Mathf.Atan2((float)(tg.y + 1 - (e.y + 0.25)), (float)((tg.x - e.x) * e.facing)); P.gun.rotation.z += (a - P.gun.rotation.z) * 0.2f; }
                    break;
                }
            }

            if (e.type == "warden") AnimateWarden(R, e, dt, t);
            else if (e.type == "stormcaller") AnimateStorm(R, e, dt, t);

            if (e.dead && e.boss)
            {
                float k = Mathf.Min(1, (float)e.deathT / 68), gone = Mathf.Max(0, ((float)e.deathT - 68) / 8);
                R.body.position.x = Mathf.Sin(t * 60) * 0.06f * (1 - gone); R.body.rotation.z = -0.35f * k + Mathf.Sin(t * 23) * 0.04f;
                R.root.scale.setScalar(Mathf.Max(0.01f, 1 - Mathf.Min(1, gone)));
                M.energy.emissiveIntensity = 4 + Mathf.Sin(t * 40) * 3; M.plate.emissiveIntensity = Mathf.Max(0, Mathf.Sin(t * 25)) * 0.8f;
            }
            else if (e.dead)
            {
                float k = Mathf.Min(1, (float)e.deathT / 40);
                R.root.scale.setScalar(Mathf.Max(0.01f, 1 - k * 0.9f));
                R.body.rotation.z = -0.8f * k;
            }
        }

        // The Lockwarden: walks with heavy, bobbing strides; each attack has its own windup and strike pose
        static void AnimateWarden(EnemyRig R, Enemy e, float dt, float t)
        {
            var P = R.parts; var M = R.mats; string s = e.state; var A = e.atk; float k = Mathf.Min(1, dt * 14); string kind = A?.kind;
            for (int i = 0; i < P.plates.Count; i++) P.plates[i].visible = i < e.armor;
            bool walking = (s == "idle" || s == "approach") && System.Math.Abs(e.vx) > 0.4;
            R.phase += walking ? dt * Mathf.Abs((float)e.vx) * 1.6f : 0;
            float sw = walking ? Mathf.Sin(R.phase) : 0;
            float hipN = sw * 0.45f, hipF = -sw * 0.45f, knN = -Mathf.Max(0, -Mathf.Cos(R.phase)) * 0.6f * (walking ? 1 : 0), knF = -Mathf.Max(0, Mathf.Cos(R.phase)) * 0.6f * (walking ? 1 : 0);
            float armN = 0.15f, armF = -0.1f, twist = 0, crouch = walking ? Mathf.Abs(Mathf.Cos(R.phase)) * 0.08f : 0, lean = 0, head = 0, pod = 0;
            bool w = s == "windup"; float u = w && A != null ? Mathf.Min(1, (float)(e.st / System.Math.Max(1, A.wind))) : 0;
            float st = (float)e.st;
            if (s == "intro" && !e.onGround) { hipN = 0.3f; hipF = -0.2f; knN = -0.3f; knF = -0.4f; armN = -0.6f; armF = -0.6f; }
            else if (s == "intro" && e.st < 140) { crouch = Mathf.Max(0, 0.5f - st * 0.01f); armN = 0.9f; armF = 0.9f; }
            else if (s == "roar") { armN = -1.9f; armF = -1.9f; head = -0.4f; lean = -0.2f; twist = Mathf.Sin(t * 30) * 0.03f; }
            else if (s == "dazed" || s == "stagger") { lean = 0.35f + Mathf.Sin(t * 6) * 0.08f; head = 0.5f; armN = 0.4f; armF = 0.3f; crouch = 0.25f; }
            else if (kind == "sweep") { if (w) { armF = -1.5f * u; twist = 0.5f * u; } else if (s == "attack") { armF = 1.5f; twist = -0.55f; } else { armF = 0.9f; twist = -0.3f; } }
            else if (kind == "hammer") { if (w) { armN = -2.7f * u; lean = -0.15f * u; } else if (s == "attack") { armN = 1.35f; lean = 0.35f; crouch = 0.2f; } else { armN = 1.1f; lean = 0.25f; crouch = 0.15f; } }
            else if (kind == "stomp")
            {
                if (w) { crouch = 0.45f * u; armN = armF = -0.8f * u; }
                else if (s == "jump") { hipN = hipF = 0.9f; knN = knF = -1.4f; armN = armF = -1.2f; }
                else { crouch = Mathf.Max(0, 0.35f - st * 0.02f); armN = armF = 0.6f; }
            }
            else if (kind == "missiles") { lean = 0.25f * (w ? u : 1); pod = w ? u : Mathf.Max(0, 1 - st / 12); }
            else if (kind == "charge" || s == "charge") { lean = 0.5f; armN = 0.9f; armF = -0.9f; crouch = 0.15f; if (s == "charge") { hipN = Mathf.Sin(t * 20) * 0.7f; hipF = -hipN; } }
            else if (kind == "laser") { head = A != null && A.high ? -0.15f : 0.35f; lean = 0.1f; }
            var L = P.legsW;
            L[0].hip.rotation.z = Ease(L[0].hip.rotation.z, hipN, k); L[1].hip.rotation.z = Ease(L[1].hip.rotation.z, hipF, k);
            L[0].knee.rotation.z = Ease(L[0].knee.rotation.z, knN, k); L[1].knee.rotation.z = Ease(L[1].knee.rotation.z, knF, k);
            P.armN.rotation.z = Ease(P.armN.rotation.z, armN, s == "attack" ? Mathf.Min(1, dt * 30) : k);
            P.armF.rotation.z = Ease(P.armF.rotation.z, armF, s == "attack" ? Mathf.Min(1, dt * 30) : k);
            P.chest.rotation.y = Ease(P.chest.rotation.y, twist, k); P.head.rotation.z = Ease(P.head.rotation.z, -head, k);
            P.torso.position.y = 1.65f - crouch; foreach (var l in L) l.hip.position.y = 1.55f - crouch;
            R.body.rotation.z = Ease(R.body.rotation.z, -lean, k);
            float hurt = 1 - (float)(e.hp / e.maxHp); bool charging = w || s == "laser" || s == "charge" || s == "roar";
            M.energy.emissiveIntensity = 2.2f + hurt * 2 + (charging ? 2.5f + Mathf.Sin(t * 34) * 1.2f : 0) + (s == "dazed" || s == "stagger" ? -1.6f : 0);
            P.eye.scale.set(1, 1, s == "laser" ? 1.3f : 1); P.eye.scale.y = kind == "laser" && (w || s == "laser") ? 1.8f : 1;
            foreach (var tb in P.tubes) tb.scale.set(1 + pod * 0.6f, 1 + pod * 3, 1 + pod * 0.6f);
            P.core.scale.setScalar(1 + hurt * 0.4f + (charging ? Mathf.Sin(t * 30) * 0.08f : 0));
        }

        // The Stormcaller: banks with its speed, rotors spinning; the chin cannon tracks its target; it noses down to dive
        static void AnimateStorm(EnemyRig R, Enemy e, float dt, float t)
        {
            var P = R.parts; var M = R.mats; string s = e.state; var A = e.atk; float k = Mathf.Min(1, dt * 8);
            bool down = s == "crashed";
            float bank = -Mathf.Clamp((float)e.vx * 0.05f, -0.4f, 0.4f) * (float)e.facing, pitch = 0;
            if (s == "dive")
            {
                double ady = A != null && A.dy != 0 ? A.dy : -1, adx = A != null && A.dx != 0 ? A.dx : 0.3;
                pitch = -Mathf.Atan2((float)-ady, Mathf.Abs((float)adx)) * 0.6f;
            }
            if (down) { bank = 0.35f; pitch = -0.2f + Mathf.Sin(t * 5) * 0.03f; }
            if (s == "roar") bank = Mathf.Sin(t * 24) * 0.12f;
            P.hull.rotation.x = Ease(P.hull.rotation.x, down ? 0.25f : bank * 0.5f, k);
            P.hull.rotation.z = Ease(P.hull.rotation.z, pitch, k);
            P.hull.position.y = 0.75f + (down ? -0.15f : Mathf.Sin(t * 2.4f) * 0.05f);
            foreach (var r in P.rotors) r.rotation.y += dt * (down ? 4 : s == "dive" ? 40 : 26);
            var tg = e.target; float aim = 0;
            if (s == "laser" || (s == "windup" && A != null && A.kind == "sweep")) aim = -0.35f;
            else if (tg != null) aim = Mathf.Clamp(Mathf.Atan2((float)(tg.y + 1 - (e.y + 0.4)), Mathf.Max(0.5f, (float)((tg.x - e.x) * e.facing))), -1.2f, 0.4f);
            P.cannon.rotation.z = Ease(P.cannon.rotation.z, aim, k);
            P.shield.visible = e.armor > 0;
            if (P.shield.visible) { P.shield.rotation.y += dt * 0.6f; P.shieldMat.opacity = 0.18f + 0.08f * Mathf.Sin(t * 6) + (e.flash > 0 ? 0.25f : 0); }
            float hurt = 1 - (float)(e.hp / e.maxHp); bool charging = s == "windup" || s == "laser" || s == "dive" || s == "roar" || s == "volley";
            M.energy.emissiveIntensity = 2.2f + hurt * 2 + (charging ? 2.4f + Mathf.Sin(t * 30) * 1.1f : 0) - (down ? 1.4f : 0);
            P.eye.scale.setScalar(1 + (charging ? 0.25f : 0));
        }
    }
}
