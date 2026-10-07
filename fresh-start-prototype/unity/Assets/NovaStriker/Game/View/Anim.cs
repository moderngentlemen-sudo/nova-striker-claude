// Procedural animation for the player rigs (anim.js). Every pose aims for the same energy as the charged-dash
// coil: strong, asymmetric silhouettes, weight low and committed, anticipation before a strike and follow-through
// after it, the torso twisting into blows, and secondary motion (breathing, bob, head counter-motion). Attacks are
// keyframed per move (windup, a snapping strike, follow-through); everything eases toward its target at a rate
// set per state, independent of frame rate.
using System;
using System.Collections.Generic;
using System.Globalization;
using NovaStriker.Sim;
using UnityEngine;
using static NovaStriker.Sim.Cfg;

namespace NovaStriker.Game
{
    // Joints: spine pitch (+ leans forward), twist (torso turn), shoulders/elbows (near = weapon arm, far),
    // hips/knees, hip height, whole-body tilt, head pitch; and RAM's tower shield, placed directly in body space
    // (sx, sy) and tilted (sr), so it can stay upright in front of him
    public sealed class Pose
    {
        public static readonly string[] J = { "spine", "twist", "shN", "elN", "shF", "elF", "hipN", "knN", "hipF", "knF", "hipY", "bodyZ", "head", "sx", "sy", "sr" };
        public readonly float[] v = new float[Anim.NJ];
        public float spine { get => v[0]; set => v[0] = value; }
        public float twist { get => v[1]; set => v[1] = value; }
        public float shN { get => v[2]; set => v[2] = value; }
        public float elN { get => v[3]; set => v[3] = value; }
        public float shF { get => v[4]; set => v[4] = value; }
        public float elF { get => v[5]; set => v[5] = value; }
        public float hipN { get => v[6]; set => v[6] = value; }
        public float knN { get => v[7]; set => v[7] = value; }
        public float hipF { get => v[8]; set => v[8] = value; }
        public float knF { get => v[9]; set => v[9] = value; }
        public float hipY { get => v[10]; set => v[10] = value; }
        public float bodyZ { get => v[11]; set => v[11] = value; }
        public float head { get => v[12]; set => v[12] = value; }
        public float sx { get => v[13]; set => v[13] = value; }
        public float sy { get => v[14]; set => v[14] = value; }
        public float sr { get => v[15]; set => v[15] = value; }
        public Pose Copy(Pose o) { Array.Copy(o.v, v, v.Length); return this; }
        // Object.assign(P, {...}) with named arguments
        public Pose Set(float? spine = null, float? twist = null, float? shN = null, float? elN = null, float? shF = null, float? elF = null, float? hipN = null, float? knN = null,
            float? hipF = null, float? knF = null, float? hipY = null, float? bodyZ = null, float? head = null, float? sx = null, float? sy = null, float? sr = null)
        {
            float?[] a = { spine, twist, shN, elN, shF, elF, hipN, knN, hipF, knF, hipY, bodyZ, head, sx, sy, sr };
            for (int i = 0; i < a.Length; i++) if (a[i].HasValue) v[i] = a[i].Value;
            return this;
        }
        public Pose Set(float?[] partial) { for (int i = 0; i < partial.Length; i++) if (partial[i].HasValue) v[i] = partial[i].Value; return this; }
        public static Pose Of(float?[] partial) => new Pose().Set(partial);
        // A partial pose written the way the prototype writes it: "...AIR, spine: -0.1, hipN: 2.1"
        public static float?[] Parse(string s)
        {
            var o = new float?[Anim.NJ];
            foreach (var part in s.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var t = part.Trim();
                if (t.Length == 0) continue;
                if (t == "...AIR") { for (int i = 0; i < Anim.NJ; i++) if (Anim.AIR[i].HasValue) o[i] = Anim.AIR[i]; continue; }
                int c = t.IndexOf(':');
                int j = Array.IndexOf(J, t.Substring(0, c).Trim());
                o[j] = float.Parse(t.Substring(c + 1).Trim(), CultureInfo.InvariantCulture);
            }
            return o;
        }
    }

    public static class Anim
    {
        public const int NJ = 16;
        public static readonly float?[] REST = Pose.Parse("spine: 0.04, twist: 0, shN: 0.12, elN: 0.3, shF: -0.1, elF: 0.3, hipN: 0.04, knN: -0.08, hipF: -0.04, knF: -0.08, hipY: 0.95, bodyZ: 0, head: 0, sx: 0.32, sy: 0.92, sr: 0");
        // A ready fighting stance that attack poses build on
        public static readonly float?[] FIGHT = Pose.Parse("spine: 0.18, twist: 0, shN: 0.7, elN: 1.0, shF: 0.35, elF: 1.1, hipN: 0.4, knN: -0.5, hipF: -0.32, knF: -0.35, hipY: 0.9, bodyZ: 0, head: -0.1, sx: 0.55, sy: 1.0, sr: 0");
        public static readonly float?[] AIR = ParseNoAir("hipN: 0.9, knN: -1.35, hipF: 0.45, knF: -1.05, hipY: 0.95");
        static float?[] ParseNoAir(string s)
        {
            var o = new float?[NJ];
            foreach (var part in s.Split(','))
            {
                int c = part.IndexOf(':');
                o[Array.IndexOf(Pose.J, part.Substring(0, c).Trim())] = float.Parse(part.Substring(c + 1).Trim(), CultureInfo.InvariantCulture);
            }
            return o;
        }

        static float Ease(float k) => k * k * (3 - 2 * k);
        static float SnapEase(float k) => 1 - Mathf.Pow(1 - k, 3);
        static void MixPose(float[] a, float[] b, float k, Pose o) { for (int j = 0; j < NJ; j++) o.v[j] = a[j] + (b[j] - a[j]) * k; }

        // ---- Attack keyframes: [tick, partial pose over the move's base stance, snap?] ----
        struct Key { public float t; public float?[] pose; public bool snap; }
        static Key K(double t, string pose, bool snap) => new Key { t = (float)t, pose = Pose.Parse(pose), snap = snap };
        static double End(MoveDef m) => m.su + m.ac + m.rc;
        static readonly Dictionary<string, Func<MoveDef, Key[]>> KEYS = new Dictionary<string, Func<MoveDef, Key[]>>
        {
            ["nova_k1"] = m => new[] { K(0, "spine: 0.05, twist: -0.4, shN: -0.4, elN: 1.7, hipY: 0.9", false), K(m.su, "spine: 0.32, twist: 0.45, shN: 1.75, elN: 0.1, hipN: 0.65, knN: -0.7, hipF: -0.55, hipY: 0.86", true), K(m.su + m.ac + 3, "spine: 0.28, twist: 0.3, shN: 1.45, elN: 0.35, hipN: 0.6, knN: -0.65, hipF: -0.5", false), K(End(m), "", false) },
            ["nova_k2"] = m => new[] { K(0, "twist: 0.4, shF: -0.35, elF: 2.2, shN: 0.9, elN: 1.4", false), K(m.su, "spine: 0.38, twist: -0.5, shF: 1.45, elF: 1.65, shN: -0.3, elN: 1.4, hipN: 0.7, knN: -0.75, hipF: -0.55, hipY: 0.86", true), K(m.su + m.ac + 3, "spine: 0.3, twist: -0.35, shF: 1.2, elF: 1.4, shN: 0.2, elN: 1.3", false), K(End(m), "", false) },
            ["nova_k3"] = m => new[] { K(0, "spine: 0.1", false), K(m.su - 1, "spine: -0.18, twist: -0.65, shN: -0.95, elN: 1.9, shF: 0.9, elF: 1.2, hipN: 0.2, knN: -0.95, hipF: -0.85, knF: -0.2, hipY: 0.8", false), K(m.su + 1, "spine: 0.48, twist: 0.6, shN: 1.62, elN: 0.0, shF: -0.7, elF: 1.1, hipN: 0.95, knN: -0.72, hipF: -0.8, knF: -0.12, hipY: 0.8", true), K(m.su + m.ac + 4, "spine: 0.4, twist: 0.45, shN: 1.5, elN: 0.15, shF: -0.5, hipN: 0.9, knN: -0.7, hipF: -0.75, hipY: 0.82", false), K(End(m), "", false) },
            ["nova_kair"] = m => new[] { K(0, "...AIR, spine: -0.1, hipN: 2.1, knN: -0.35, shN: 0.9, shF: 1.3", false), K(m.su, "...AIR, spine: -0.25, hipN: 2.4, knN: -0.15, shN: 1.2, shF: 1.6", false), K(m.su + 2, "...AIR, spine: 0.45, hipN: 0.05, knN: -0.1, shN: 0.4, shF: 0.6, bodyZ: -0.2", true), K(End(m), "...AIR", false) },
            ["nova_rise"] = m => new[] { K(0, "hipY: 0.72, spine: 0.42, twist: -0.45, hipN: 1.25, knN: -1.9, hipF: 0.2, knF: -1.8, shN: -0.45, elN: 1.95, shF: 0.7, elF: 1.6", false), K(m.su, "hipY: 0.95, spine: -0.2, twist: 0.4, shN: 3.05, elN: 0.04, shF: -0.45, elF: 1.25, hipN: 0.35, knN: -0.45, hipF: -0.25, knF: -1.15, head: 0.35", true), K(m.su + m.ac, "hipY: 0.95, spine: -0.12, twist: 0.3, shN: 2.95, elN: 0.1, shF: -0.25, elF: 1.3, hipN: 0.6, knN: -1.0, hipF: 0.1, knF: -1.1, head: 0.25", false), K(End(m), "...AIR", false) },
            ["echo_b1"] = m => new[] { K(0, "spine: -0.05, twist: -0.35, shN: 2.6, elN: 1.0, hipN: 0.35, hipF: -0.25", false), K(m.su, "spine: 0.42, twist: 0.5, shN: 0.55, elN: 0.08, shF: -0.4, hipN: 0.78, knN: -0.72, hipF: -0.58, knF: -0.3, hipY: 0.84", true), K(m.su + m.ac + 2, "spine: 0.36, twist: 0.4, shN: 0.2, elN: 0.3, hipN: 0.72, knN: -0.7, hipF: -0.55, hipY: 0.85", false), K(End(m), "", false) },
            ["echo_b2"] = m => new[] { K(0, "twist: 0.45, shF: -0.55, elF: 0.6, shN: 0.9, elN: 1.2", false), K(m.su, "spine: 0.3, twist: -0.45, shF: 2.45, elF: 0.12, shN: -0.45, elN: 0.8, hipN: 0.72, knN: -0.7, hipF: -0.55, hipY: 0.85", true), K(m.su + m.ac + 2, "spine: 0.24, twist: -0.3, shF: 2.6, elF: 0.3, shN: -0.3", false), K(End(m), "", false) },
            ["echo_b3"] = m => new[] { K(0, "spine: -0.12, shN: 2.85, shF: 2.6, elN: 1.3, elF: 1.3, hipY: 0.92", false), K(m.su, "spine: 0.48, shN: 0.85, shF: 0.7, elN: 0.05, elF: 0.1, hipN: 0.82, knN: -0.85, hipF: -0.62, knF: -0.25, hipY: 0.82", true), K(m.su + m.ac + 2, "spine: 0.4, shN: 0.4, shF: 0.3, elN: 0.2, elF: 0.3, hipN: 0.8, knN: -0.82, hipF: -0.6, hipY: 0.83", false), K(End(m), "", false) },
            ["echo_b4"] = m => new[] { K(0, "twist: -0.9, shN: 1.2, shF: 1.1, elN: 0.3, elF: 0.4, spine: 0.1, hipY: 0.86", false), K(m.su, "twist: 0.2, shN: 1.62, shF: 1.58, elN: 0.05, elF: 0.05, spine: 0.3, hipN: 0.65, knN: -0.65, hipF: -0.62, hipY: 0.83", true), K(m.su + m.ac, "twist: 0.4, shN: 1.5, shF: 1.5, elN: 0.1, elF: 0.1, spine: 0.3, hipN: 0.65, knN: -0.65, hipF: -0.62, hipY: 0.83", false), K(End(m), "", false) },
            ["echo_rise"] = m => new[] { K(0, "hipY: 0.72, spine: 0.42, hipN: 1.2, knN: -1.85, hipF: 0.3, knF: -1.9, shN: -0.5, shF: -0.7", false), K(m.su, "hipY: 0.95, spine: -0.1, shN: 2.9, shF: 2.7, elN: 0.1, elF: 0.2, hipN: 0.3, knN: -0.6, hipF: -0.3, knF: -0.95", true), K(m.su + m.ac, "hipY: 0.95, spine: -0.05, shN: 2.8, shF: 2.6, elN: 0.2, elF: 0.3, hipN: 0.6, knN: -1.1, hipF: 0.2, knF: -1.0", false), K(End(m), "...AIR", false) },
            ["echo_ab1"] = m => new[] { K(0, "...AIR, twist: -0.35, shN: 2.6, elN: 1.0", false), K(m.su, "...AIR, spine: 0.4, twist: 0.45, shN: 0.55, elN: 0.08", true), K(End(m), "...AIR", false) },
            ["echo_ab2"] = m => new[] { K(0, "...AIR, twist: 0.45, shF: -0.55, elF: 0.6", false), K(m.su, "...AIR, spine: 0.3, twist: -0.45, shF: 2.45, elF: 0.12, shN: -0.4", true), K(End(m), "...AIR", false) },
            ["echo_ab3"] = m => new[] { K(0, "...AIR, spine: -0.25, shN: 2.7, shF: 2.5, elN: 0.4, elF: 0.5", false), K(m.su, "...AIR, spine: 0.65, shN: 0.5, shF: 0.4, elN: 0.1, elF: 0.15, bodyZ: -0.3", true), K(End(m), "...AIR", false) },
            ["echo_spin"] = m => new[] { K(0, "...AIR, shN: 1.6, shF: 1.6, elN: 0.1, elF: 0.1, hipN: 1.15, knN: -1.7, hipF: 0.95, knF: -1.6", false), K(End(m), "...AIR, shN: 1.2, shF: 1.1, elN: 0.4, elF: 0.4", false) },
            ["echo_wall"] = m => new[] { K(0, "spine: -0.1, twist: -0.4, shN: 2.5, elN: 0.9, shF: -2.2, elF: 0.35, hipN: 0.75, knN: -1.25, hipF: -0.35, knF: -0.45, hipY: 0.95", false), K(m.su, "spine: 0.35, twist: 0.5, shN: 0.4, elN: 0.05, shF: -2.2, elF: 0.35, hipN: 0.8, knN: -1.2, hipF: -0.35, knF: -0.45, hipY: 0.95", true), K(End(m), "spine: -0.1, shN: 0.6, elN: 0.8, shF: -2.2, elF: 0.35, hipN: 0.75, knN: -1.25, hipF: -0.35, knF: -0.45, hipY: 0.95", false) },
            ["echo_charged"] = m => new[] { K(0, "spine: 0.1", false), K(m.su - 1, "spine: -0.32, twist: -0.5, shN: 3.0, shF: 2.9, elN: 0.2, elF: 0.3, hipN: 0.6, knN: -0.8, hipF: -0.7, knF: -0.2, hipY: 0.84", false), K(m.su + 1, "spine: 0.62, twist: 0.45, shN: 0.7, shF: 0.6, elN: 0.1, elF: 0.2, hipN: 0.95, knN: -0.95, hipF: -0.75, knF: -0.15, hipY: 0.79", true), K(m.su + m.ac + 5, "spine: 0.5, twist: 0.3, shN: 0.5, shF: 0.4, hipN: 0.9, knN: -0.9, hipF: -0.72, hipY: 0.8", false), K(End(m), "", false) },
            ["echo_riposte"] = m => new[] { K(0, "shN: 0.4, elN: 1.8, twist: -0.3", false), K(m.su, "spine: 0.48, twist: 0.5, shN: 1.62, elN: 0.0, shF: -0.6, hipN: 0.95, knN: -0.8, hipF: -0.8, knF: -0.15, hipY: 0.8", true), K(End(m), "", false) },
            ["ram_b1"] = m => new[] { K(0, "twist: -0.3, sx: 0.2, sy: 1.0, shN: 0.3, elN: 1.2, spine: 0.05", false), K(m.su, "spine: 0.38, twist: 0.25, sx: 1.05, sy: 1.05, shN: 1.4, elN: 0.25, hipN: 0.75, knN: -0.7, hipF: -0.6, knF: -0.25, hipY: 0.86", true), K(m.su + m.ac + 3, "spine: 0.32, sx: 0.95, sy: 1.0, shN: 1.3, elN: 0.3, hipN: 0.7, knN: -0.7, hipF: -0.55, hipY: 0.87", false), K(End(m), "", false) },
            ["ram_b2"] = m => new[] { K(0, "twist: 0.35, sx: 0.4, sy: 1.15, sr: -0.75, shN: 1.0, elN: 0.8", false), K(m.su, "spine: 0.32, twist: -0.4, sx: 0.95, sy: 0.85, sr: 0.85, shN: 1.2, elN: 0.3, hipN: 0.7, knN: -0.75, hipF: -0.55, hipY: 0.86", true), K(m.su + m.ac + 3, "spine: 0.28, twist: -0.3, sx: 0.85, sy: 0.85, sr: 0.7, shN: 1.1, elN: 0.35", false), K(End(m), "", false) },
            ["ram_b3"] = m => new[] { K(0, "spine: 0.1, sx: 0.3", false), K(m.su - 1, "spine: -0.15, twist: -0.7, sx: 0.15, sy: 0.9, shF: -0.9, elF: 2.0, shN: 0.4, elN: 1.0, hipN: 0.25, knN: -0.95, hipF: -0.85, knF: -0.2, hipY: 0.8", false), K(m.su + 1, "spine: 0.5, twist: 0.65, sx: 0.25, sy: 0.85, shF: 1.65, elF: 0.0, shN: 0.2, elN: 1.1, hipN: 1.0, knN: -0.7, hipF: -0.85, knF: -0.1, hipY: 0.78", true), K(m.su + m.ac + 5, "spine: 0.42, twist: 0.5, shF: 1.5, elF: 0.1, hipN: 0.95, knN: -0.7, hipF: -0.8, hipY: 0.8", false), K(End(m), "", false) },
            ["ram_air"] = m => new[] { K(0, "...AIR, sx: 0.55, sy: 1.5, sr: -0.6, shN: 2.0, elN: 0.6, spine: -0.1", false), K(m.su, "...AIR, sx: 0.75, sy: 0.35, sr: 0.7, shN: 0.9, elN: 0.2, spine: 0.45", true), K(End(m), "...AIR, sx: 0.6, sy: 0.6, sr: 0.3", false) },
            ["ram_bash"] = m => new[] { K(0, "sx: 0.8, sy: 1.05, shN: 1.2, elN: 0.6, spine: 0.15, hipN: 0.55, knN: -0.8, hipF: -0.45, knF: -0.4, hipY: 0.86", false), K(m.su, "sx: 1.2, sy: 1.08, shN: 1.5, elN: 0.15, spine: 0.4, hipN: 0.85, knN: -0.75, hipF: -0.65, knF: -0.2, hipY: 0.84", true), K(End(m), "sx: 0.8, sy: 1.0", false) },
            ["ram_rise"] = m => new[] { K(0, "hipY: 0.66, spine: 0.55, sx: 0.85, sy: 0.4, sr: 0.5, shN: 0.9, elN: 0.4, hipN: 1.2, knN: -1.9, hipF: 0.2, knF: -1.8", false), K(m.su, "hipY: 0.95, spine: -0.2, sx: 0.6, sy: 2.0, sr: 1.25, shN: 2.7, elN: 0.2, shF: 2.4, elF: 0.4, hipN: 0.3, knN: -0.4, hipF: -0.25, knF: -0.9, head: 0.3", true), K(m.su + m.ac, "hipY: 0.95, spine: -0.1, sx: 0.55, sy: 2.05, sr: 1.35, shN: 2.75, elN: 0.25, shF: 2.5, elF: 0.4, hipN: 0.6, knN: -1.0, hipF: 0.1, knF: -1.0", false), K(End(m), "...AIR, sx: 0.6, sy: 1.2, sr: 0.3", false) },
            ["ram_slam"] = m => new[] { K(0, "spine: 0.1", false), K(m.su - 1, "spine: -0.35, sx: 0.25, sy: 2.15, sr: 1.45, shN: 2.9, elN: 0.3, shF: 2.8, elF: 0.4, hipN: 0.5, knN: -0.7, hipF: -0.5, knF: -0.3, hipY: 0.9, head: 0.3", false), K(m.su + 1, "spine: 0.75, sx: 1.05, sy: 0.4, sr: 0.0, shN: 1.25, elN: 0.1, shF: 1.15, elF: 0.2, hipN: 1.2, knN: -1.3, hipF: -0.7, knF: -0.5, hipY: 0.66", true), K(m.su + m.ac + 8, "spine: 0.65, sx: 1.0, sy: 0.42, shN: 1.2, elN: 0.15, shF: 1.1, hipN: 1.1, knN: -1.25, hipF: -0.7, hipY: 0.68", false), K(End(m), "", false) },
            ["fix_w1"] = m => new[] { K(0, "spine: 0.0, twist: -0.4, shN: 2.5, elN: 1.0, hipY: 0.92", false), K(m.su, "spine: 0.35, twist: 0.45, shN: 0.65, elN: 0.1, hipN: 0.65, knN: -0.7, hipF: -0.55, hipY: 0.86", true), K(m.su + m.ac + 3, "spine: 0.3, twist: 0.35, shN: 0.4, elN: 0.3, hipN: 0.6, knN: -0.65, hipF: -0.5", false), K(End(m), "", false) },
            ["fix_w2"] = m => new[] { K(0, "twist: 0.45, shN: -0.5, elN: 0.4, spine: 0.25", false), K(m.su, "spine: 0.2, twist: -0.45, shN: 2.3, elN: 0.15, hipN: 0.6, knN: -0.7, hipF: -0.5, hipY: 0.87", true), K(m.su + m.ac + 3, "twist: -0.3, shN: 2.5, elN: 0.3", false), K(End(m), "", false) },
            ["fix_w3"] = m => new[] { K(0, "spine: 0.05", false), K(m.su - 1, "spine: -0.3, twist: -0.3, shN: 3.05, elN: 0.7, shF: 2.6, elF: 0.8, hipN: 0.5, knN: -0.7, hipF: -0.5, hipY: 0.9", false), K(m.su + 1, "spine: 0.6, twist: 0.3, shN: 0.85, elN: 0.0, shF: 0.75, elF: 0.3, hipN: 0.95, knN: -0.95, hipF: -0.7, knF: -0.2, hipY: 0.8", true), K(m.su + m.ac + 4, "spine: 0.5, shN: 0.75, elN: 0.1, hipN: 0.9, knN: -0.9, hipF: -0.65, hipY: 0.81", false), K(End(m), "", false) },
            ["fix_air"] = m => new[] { K(0, "...AIR, shN: 2.2, elN: 0.5, spine: -0.1", false), K(m.su, "...AIR, spine: 0.35, shN: 0.6, elN: 0.1, bodyZ: -0.2", true), K(End(m), "...AIR", false) },
            ["fix_slam"] = m => new[] { K(0, "spine: 0.1", false), K(m.su - 1, "spine: -0.4, twist: -0.4, shN: 3.1, elN: 0.6, shF: 2.9, elF: 0.6, hipN: 0.55, knN: -0.8, hipF: -0.6, knF: -0.2, hipY: 0.86, head: 0.25", false), K(m.su + 1, "spine: 0.72, twist: 0.4, shN: 0.7, elN: 0.0, shF: 0.6, elF: 0.2, hipN: 1.05, knN: -1.0, hipF: -0.8, knF: -0.15, hipY: 0.76", true), K(m.su + m.ac + 6, "spine: 0.6, shN: 0.6, elN: 0.05, hipN: 1.0, knN: -0.95, hipF: -0.75, hipY: 0.78", false), K(End(m), "", false) },
            ["fix_rise"] = m => new[] { K(0, "hipY: 0.66, spine: 0.45, twist: -0.4, hipN: 1.25, knN: -1.9, hipF: 0.2, knF: -1.8, shN: -0.4, elN: 1.6, shF: 0.6, elF: 1.4", false), K(m.su, "hipY: 0.95, spine: -0.2, twist: 0.35, shN: 3.05, elN: 0.05, shF: -0.4, elF: 1.2, hipN: 0.3, knN: -0.4, hipF: -0.25, knF: -1.1, head: 0.3", true), K(m.su + m.ac, "hipY: 0.95, spine: -0.1, shN: 2.9, elN: 0.15, shF: -0.3, hipN: 0.6, knN: -1.0, hipF: 0.1, knF: -1.0", false), K(End(m), "...AIR", false) },

        };
        static Anim()
        {
            KEYS["nova_jab1"] = KEYS["nova_k1"];
            KEYS["nova_jab2"] = KEYS["nova_k2"];
            KEYS["nova_shove"] = KEYS["nova_k3"];
            KEYS["nova_brace"] = KEYS["nova_k3"];
            KEYS["nova_air"] = KEYS["nova_kair"];
            KEYS["echo_g1"] = KEYS["echo_b1"];
            KEYS["echo_g2"] = KEYS["echo_b2"];
            KEYS["echo_g3"] = KEYS["echo_b4"];
            KEYS["echo_launch"] = KEYS["echo_rise"];
            KEYS["echo_air1"] = KEYS["echo_ab1"];
            KEYS["echo_air2"] = KEYS["echo_ab2"];
            KEYS["echo_air3"] = KEYS["echo_ab3"];
        }
        // Whole-body spins spread over a move's active frames: 'y' a corkscrew, 'z' a somersault in the view plane
        static readonly Dictionary<string, (float turns, char axis)> SPIN = new Dictionary<string, (float, char)>
            { ["echo_b4"] = (1, 'y'), ["echo_rise"] = (2, 'y'), ["echo_spin"] = (2, 'z'), ["fix_air"] = (1, 'z') };

        struct CKey { public float t; public bool snap; public float[] pose; }
        static readonly Dictionary<string, CKey[]> keyCache = new Dictionary<string, CKey[]>();
        static void AttackPose(Player p, Pose o)
        {
            string id = p.moveId; var m = MOVES[id];
            if (!keyCache.TryGetValue(id, out var ks))
            {
                var bse = new Pose().Set(FIGHT); if (m.air) bse.Set(AIR);
                var src = (KEYS.TryGetValue(id, out var f) ? f : KEYS["echo_b1"])(m);
                ks = new CKey[src.Length];
                for (int i = 0; i < src.Length; i++) { var q = new Pose().Copy(bse).Set(src[i].pose); ks[i] = new CKey { t = src[i].t, snap = src[i].snap, pose = q.v }; }
                keyCache[id] = ks;
            }
            float u = (float)p.st;
            if (u <= ks[0].t) { Array.Copy(ks[0].pose, o.v, NJ); return; }
            for (int i = 1; i < ks.Length; i++)
            {
                var a = ks[i - 1]; var b = ks[i];
                if (u <= b.t) { float fk = (u - a.t) / Mathf.Max(1e-6f, b.t - a.t); MixPose(a.pose, b.pose, b.snap ? SnapEase(fk) : Ease(fk), o); return; }
            }
            Array.Copy(ks[ks.Length - 1].pose, o.v, NJ);
        }

        static bool StaffMove(string id) => id != null && MOVES.TryGetValue(id, out var m) && m.staff;
        public static int DashLevelOf(Player p) => p.state != "dashCharge" ? 0 : p.dashChargeT >= DASH_CHARGE.charge[2] ? 3 : p.dashChargeT >= DASH_CHARGE.charge[1] ? 2 : p.dashChargeT >= DASH_CHARGE.charge[0] ? 1 : 0;
        static bool In(string s, params string[] a) => Array.IndexOf(a, s) >= 0;
        static float R() => UnityEngine.Random.value;
        const float PI = Mathf.PI;

        static readonly Pose P = new Pose();
        static readonly Dictionary<string, int> LV = new Dictionary<string, int> { ["L1"] = 1, ["L2"] = 2, ["L3"] = 3, ["perfect"] = 3, ["L4"] = 4 };
        static int Lv(string s) => s != null && LV.TryGetValue(s, out var v) ? v : 0;

        // ---- The pose for this frame ----
        public static void AnimatePlayer(Rig rig, Player p, float dt, float t)
        {
            P.Set(REST);
            float speed = Mathf.Abs((float)p.vx); string st = p.state; bool echo = p.@char == "echo", ram = p.@char == "ram", fix = p.@char == "fix";
            bool mk = p.@char == "nova" && SETTINGS.novaKit == "marksman";
            float aimAng = Mathf.Atan2((float)p.aimY, Mathf.Abs((float)p.aimX) < 1e-3f ? 1e-3f : (float)(p.aimX * p.facing));
            float rate = 18, yaw = 0, roll = 0;
            float breathe = Mathf.Sin(t * 2.2f + (echo ? 1 : 0));
            float pvx = (float)p.vx, pvy = (float)p.vy, facing = (float)p.facing;

            if (st == "downed" || st == "dead")
            {
                P.Set(bodyZ: 1.45f, hipY: 0.2f, shN: 2.6f, shF: 2.2f, hipN: 0.2f, hipF: -0.1f, sx: 0.1f, sy: 0.5f, sr: 1.3f); rate = 10;
            }
            else if (st == "guard" && ram)
            {
                float nx = (float)p.guardDir.x, ny = (float)p.guardDir.y, a = Mathf.Atan2(ny, Mathf.Abs(nx)), u = Mathf.Clamp01(a / (PI / 2));
                bool walk = Mathf.Abs(pvx) > 0.4f; if (walk) rig.phase += dt * Mathf.Abs(pvx) * 2.2f;
                float s2 = walk ? Mathf.Sin(rig.phase) * 0.25f : 0, sc = (float)CHARS["ram"].scale;
                P.Set(spine: 0.22f - 0.3f * u, hipN: 0.6f + s2, knN: -0.85f, hipF: -0.5f - s2, knF: -0.45f, hipY: 0.84f, head: -0.05f + 0.3f * u,
                    shN: 1.15f + 1.3f * u, elN: 0.55f - 0.3f * u, shF: 1.0f + 1.2f * u, elF: 1.25f - 0.4f * u,
                    sx: Mathf.Abs(nx) * (float)RAM.guard.reach / sc, sy: ((float)CHARS["ram"].height * 0.62f + ny * (float)RAM.guard.reach) / sc, sr: a);
                rate = 30;
            }
            else if (st == "rush" && ram)
            {
                rig.phase += dt * Mathf.Max(8, speed) * 1.5f;
                float s = Mathf.Sin(rig.phase), c = Mathf.Cos(rig.phase), L = p.rush != null ? (float)p.rush.level : 0;
                P.Set(spine: 0.6f + 0.04f * L, twist: 0.15f, hipN: s * 0.8f + 0.25f, hipF: -s * 0.8f + 0.25f, knN: -Mathf.Max(0, -c) * 1.3f - 0.3f, knF: -Mathf.Max(0, c) * 1.3f - 0.3f,
                    shN: 1.25f, elN: 0.55f, shF: 0.9f, elF: 1.4f, hipY: 0.84f - Mathf.Abs(c) * 0.05f, head: -0.35f, sx: 0.98f, sy: 0.88f, sr: -0.12f);
                if (p.rush != null && p.rush.air) P.Set(hipN: 0.9f, knN: -1.4f, hipF: 0.3f, knF: -1.1f);
                rate = 30;
            }
            else if (st == "leap" && ram)
            {
                if (pvy > 0) P.Set(spine: -0.25f, shN: 2.5f, elN: 0.4f, shF: 2.3f, elF: 0.5f, hipN: 1.0f, knN: -1.5f, hipF: 0.4f, knF: -1.2f, head: 0.25f, sx: 0.4f, sy: 1.8f, sr: 1.0f);
                else P.Set(spine: 0.45f, shN: 1.1f, elN: 0.4f, shF: 0.9f, elF: 1.2f, hipN: 1.2f, knN: -1.7f, hipF: 0.6f, knF: -1.4f, sx: 0.8f, sy: 0.6f, sr: 0.2f);
                rate = 18;
            }
            else if (st == "patch" && fix)
            {
                var q = p.patch?.target; float c2y = (float)(p.y + p.h * 0.62);
                float ang = -0.9f;
                if (q != null) ang = Mathf.Atan2((float)(q.y + q.h * 0.55) - c2y, Mathf.Abs((float)(q.x - p.x)) < 1e-3f ? 1e-3f : (float)((q.x - p.x) * p.facing));
                bool walk = speed > 0.4f; if (walk) rig.phase += dt * speed * 2.2f;
                float s2 = walk ? Mathf.Sin(rig.phase) * 0.4f : 0;
                P.Set(spine: 0.12f, twist: -0.25f, shF: ang + PI / 2 + 0.12f, elF: 0.12f, shN: 0.2f, elN: 0.9f, hipN: 0.35f + s2, knN: -0.45f, hipF: -0.3f - s2, knF: -0.35f, hipY: 0.9f, head: -0.1f);
                if (q == null) P.Set(shF: 0.6f, elF: 1.6f, spine: 0.35f, head: 0.3f);
                rate = 24;
            }
            else if (st == "slide")
            {
                P.Set(hipY: 0.46f, spine: -0.45f, hipN: 1.38f, knN: -0.08f, hipF: -0.38f, knF: -1.95f, shN: 0.95f, elN: 0.7f, shF: -0.55f, elF: 0.2f, bodyZ: -0.06f, head: 0.3f);
                if (mk) P.Set(hipY: 0.52f, spine: 0.1f, hipN: 1.05f, knN: -1.0f, hipF: -0.9f, knF: -0.35f, shN: -0.8f, shF: -1.1f, elN: 0.3f, elF: 0.3f, head: -0.05f);
                if (ram) P.Set(hipY: 0.36f, spine: 0.95f, hipN: 1.3f, knN: -2.0f, hipF: 0.3f, knF: -2.1f, shN: 1.0f, elN: 0.6f, shF: 0.7f, elF: 1.2f, head: -0.4f, sx: 0.85f, sy: 0.55f, sr: -0.2f);
                rate = 30;
            }
            else if (st == "dashCharge" && ram)
            {
                float f = Mathf.Min(1, (float)(p.dashChargeT / DASH_CHARGE.charge[0])), paw = Mathf.Sin(t * 9) * 0.35f * f;
                P.Set(spine: 0.2f + 0.45f * f, hipY: 0.9f - 0.12f * f, hipN: 0.5f + paw, knN: -0.6f - 0.4f * f + paw * 0.5f, hipF: -0.5f, knF: -0.4f, shN: 1.2f, elN: 0.6f, shF: 0.9f, elF: 1.3f,
                    head: -0.3f * f, sx: 0.6f + 0.35f * f, sy: 1.0f - 0.12f * f, sr: -0.1f * f);
                if (p.dashChargeT >= DASH_CHARGE.charge[1]) P.hipY += (R() - 0.5f) * 0.03f;
                rate = 22;
            }
            else if (st == "dashCharge")
            {
                float f = Mathf.Min(1, (float)(p.dashChargeT / DASH_CHARGE.charge[0]));
                P.Set(hipY: 0.95f - 0.3f * f, spine: 0.1f + 0.45f * f, hipN: 0.4f + 0.9f * f, knN: -0.3f - 1.5f * f, hipF: -0.2f - 0.45f * f, knF: -0.2f - 0.5f * f,
                    shN: -0.4f - 0.7f * f, elN: 0.5f, shF: -0.6f - 0.7f * f, elF: 0.5f, head: -0.25f * f);
                if (p.dashChargeT >= DASH_CHARGE.charge[1]) P.hipY += (R() - 0.5f) * (p.dashChargeT >= DASH_CHARGE.charge[2] ? 0.035f : 0.018f);
                rate = 22;
            }
            else if (st == "dash" || st == "zip")
            {
                P.Set(spine: 0.58f, hipN: -0.35f, knN: -0.95f, hipF: -0.95f, knF: -0.45f, shN: -1.05f, shF: -1.25f, elN: 0.25f, elF: 0.3f, head: -0.3f);
                float dy = st == "dash" && p.dash != null ? (float)p.dash.dy : Mathf.Sign(pvy) * (pvy == 0 ? 0 : 1) * 0.4f;
                P.bodyZ = Mathf.Atan2(dy, 1) * 0.8f; rate = 34;
            }
            else if (st == "dashslash")
            {
                float u = (float)p.st, s = u < 2 ? 0 : u < 6 ? SnapEase((u - 2) / 4) : 1;
                P.Set(spine: 0.55f, twist: -0.5f + 1.1f * s, shN: -0.8f + 2.9f * s, shF: -0.9f + 2.3f * s, elN: 0.25f, elF: 0.3f, hipN: 1.0f, knN: -0.8f, hipF: -0.95f, knF: -0.2f, hipY: 0.8f, head: -0.3f,
                    bodyZ: p.slash != null ? Mathf.Atan2((float)p.slash.dy, 1) * 0.7f : 0);
                if (u > DASH_SLASH.ticks) P.Set(spine: 0.35f, twist: 0.3f, shN: 1.6f, shF: 1.2f, hipY: 0.86f);
                rate = 40;
            }
            else if (st == "pound" && p.pound != null)
            {
                var S = p.pound;
                if (S.phase == "hold")
                {
                    float k = Mathf.Min(1, (float)S.t / 6);
                    P.Set(hipY: 0.95f, spine: -0.18f * k, hipN: 1.55f, knN: -2.2f, hipF: 1.25f, knF: -2.05f, head: 0.18f,
                        shN: echo || ram || fix ? 2.95f : 2.75f, elN: echo || ram ? 0.25f : 1.35f, shF: echo || ram ? 2.75f : 1.2f, elF: echo || ram ? 0.35f : 1.5f, sx: 0.25f, sy: 2.1f, sr: 1.5f);
                    if (S.level > 0) { float j = (R() - 0.5f) * 0.025f * (float)S.level; P.spine += j; P.hipY += j; }
                    if (echo && S.t <= 9) yaw = PI * 2 * SnapEase((float)S.t / 9);
                    rate = 26;
                }
                else if (S.phase == "drop")
                {
                    P.Set(spine: 0.18f, bodyZ: 0, hipN: 0.9f, knN: -1.6f, hipF: -0.15f, knF: -0.35f, head: -0.5f, hipY: 0.95f,
                        shN: echo || fix ? 0.12f : ram ? 0.6f : -0.05f, elN: 0.02f, shF: echo ? 0.2f : ram ? 0.5f : 1.1f, elF: 1.2f, sx: 0.5f, sy: 0.1f, sr: 0);
                    rate = 42;
                }
                else
                {
                    float up = Mathf.Clamp01(((float)S.t - 5) / 7);
                    P.Set(hipY: 0.5f + 0.3f * up, spine: 0.72f - 0.4f * up, hipN: 1.55f - 0.8f * up, knN: -2.3f + 1.3f * up, hipF: -0.25f, knF: -2.1f + 1.4f * up,
                        shN: 0.45f, elN: 0.05f, shF: -0.65f, elF: 0.45f, head: 0.1f, sx: 0.85f, sy: 0.35f, sr: 0);
                    rate = 40;
                }
            }
            else if (st == "dive")
            {
                P.Set(spine: 0.55f, bodyZ: -0.35f, hipN: 1.3f, knN: -2.0f, hipF: 1.0f, knF: -1.8f, shN: 0.25f, elN: 0.4f, shF: 0.15f, elF: 0.5f, head: -0.3f); rate = 30;
            }
            else if (st == "vb")
            {
                float f = Mathf.Min(1, (float)p.st / 3);
                P.Set(spine: 0.35f * f, hipN: 0.75f, knN: -0.65f, hipF: -0.65f, knF: -0.3f, hipY: 0.82f, twist: 0.4f * f);
                if (!echo) P.Set(shN: 1.57f, elN: 0.05f, shF: 0.9f, elF: 1.2f);
                else P.Set(shN: 2.6f - 1.6f * f, shF: 2.4f - 1.5f * f, elN: 0.3f, elF: 0.4f);
                rate = 40;
            }
            else if (st == "attack" && p.move != null)
            {
                AttackPose(p, P);
                var m = p.move;
                if (p.moveId != null && SPIN.TryGetValue(p.moveId, out var sp))
                {
                    float a = Mathf.Clamp01((float)((p.st - m.su) / m.ac)), ang = PI * 2 * sp.turns * SnapEase(a);
                    if (sp.axis == 'z') roll = -ang; else yaw = ang;
                }
                rate = 48;
            }
            else if (st == "dodge" && p.dodge != null)
            {
                bool back = p.dodge.dx * p.facing < 0; float u = Mathf.Min(1, (float)p.dodge.t / 4);
                if (back) P.Set(spine: -0.3f * u, hipN: 0.75f, knN: -1.35f, hipF: -0.35f, knF: -1.0f, shN: 1.25f, elN: 1.7f, shF: 0.35f, elF: 1.3f, hipY: 0.84f, head: 0.2f, bodyZ: 0.1f * u);
                else P.Set(spine: 0.5f * u, hipN: 1.05f, knN: -1.45f, hipF: -0.65f, knF: -0.55f, shN: -0.7f, shF: -0.9f, elN: 0.35f, elF: 0.3f, hipY: 0.78f, head: -0.2f, bodyZ: -0.12f * u, twist: 0.3f * u);
                rate = 34;
            }
            else if (st == "ult" && ram)
            {
                var Rr = p.ultRun;
                if (Rr == null) P.Set(spine: -0.35f, shN: 2.6f, elN: 0.35f, shF: 2.4f, elF: 0.4f, sx: 0.35f, sy: 2.05f, sr: 1.4f, hipN: 0.45f, knN: -0.6f, hipF: -0.45f, knF: -0.4f, hipY: 0.88f, head: 0.5f);
                else if (Rr.slamT != 0) P.Set(spine: 0.8f, sx: 1.1f, sy: 0.4f, sr: 0, shN: 1.25f, elN: 0.1f, shF: 1.15f, elF: 0.2f, hipN: 1.2f, knN: -1.3f, hipF: -0.7f, knF: -0.5f, hipY: 0.64f);
                else if (Rr.t <= ULT.ram.brace) P.Set(spine: 0.55f, sx: 0.9f, sy: 0.9f, sr: -0.1f, shN: 1.2f, elN: 0.6f, shF: 0.95f, elF: 1.3f, hipN: 0.8f, knN: -1.3f, hipF: -0.6f, knF: -0.6f, hipY: 0.74f, head: -0.3f);
                else
                {
                    rig.phase += dt * 20; float s = Mathf.Sin(rig.phase), c = Mathf.Cos(rig.phase);
                    P.Set(spine: 0.65f, twist: 0.15f, hipN: s * 0.85f + 0.25f, hipF: -s * 0.85f + 0.25f, knN: -Mathf.Max(0, -c) * 1.3f - 0.3f, knF: -Mathf.Max(0, c) * 1.3f - 0.3f,
                        shN: 1.25f, elN: 0.55f, shF: 0.9f, elF: 1.4f, hipY: 0.82f, head: -0.35f, sx: 1.0f, sy: 0.9f, sr: -0.12f);
                }
                rate = 26;
            }
            else if (st == "ult" && fix)
            {
                var Rr = p.ultRun;
                if (Rr == null || Rr.t <= ULT.fix.drop) P.Set(spine: -0.3f, shN: 3.0f, elN: 0.3f, shF: 2.5f, elF: 0.15f, hipN: 0.35f, knN: -0.5f, hipF: -0.3f, knF: -0.3f, hipY: 0.92f, head: 0.55f);
                else P.Set(spine: 0.15f, shN: 0.4f, elN: 1.2f, shF: 1.45f, elF: 0.05f, hipN: 0.55f, knN: -0.75f, hipF: -0.45f, knF: -0.35f, hipY: 0.88f, head: -0.05f, twist: -0.2f);
                rate = 24;
            }
            else if (st == "ult")
            {
                var Rr = p.ultRun;
                if (echo)
                {
                    if (Rr == null) P.Set(spine: 0.45f, hipN: 1.15f, knN: -1.7f, hipF: -0.55f, knF: -0.65f, shN: -0.9f, elN: 0.4f, shF: -1.1f, elF: 0.4f, hipY: 0.7f, head: -0.2f, twist: -0.3f);
                    else P.Set(spine: 0.5f, twist: 0.55f, shN: 1.62f, elN: 0.05f, shF: 1.35f, elF: 0.2f, hipN: 0.95f, knN: -0.9f, hipF: -0.8f, knF: -0.2f, hipY: 0.8f, head: -0.1f);
                }
                else
                {
                    double t2 = Rr != null ? Rr.t : 0;
                    if (Rr == null) P.Set(spine: -0.32f, shN: 2.35f, elN: 0.25f, shF: 2.2f, elF: 0.3f, hipN: 0.3f, knN: -0.45f, hipF: -0.3f, knF: -0.35f, hipY: 0.92f, head: 0.45f);
                    else if (t2 <= ULT.nova.gather) P.Set(spine: -0.22f, shN: 3.0f, elN: 0.25f, shF: 2.9f, elF: 0.3f, hipN: 0.55f, knN: -0.95f, hipF: 0.2f, knF: -0.8f, hipY: 0.95f, head: 0.4f);
                    else if (Rr.segs != null)
                    {
                        float j = (R() - 0.5f) * 0.04f, a = Mathf.Atan2((float)Rr.dy, Mathf.Abs((float)Rr.dx) < 1e-3f ? 1e-3f : (float)(Rr.dx * p.facing));
                        P.Set(spine: -0.18f + j, shN: a + PI / 2, elN: 0.02f, shF: a + PI / 2 - 0.15f, elF: 0.1f, hipN: 0.7f, knN: -1.0f, hipF: -0.5f, knF: -0.6f, hipY: 0.95f + j, head: -0.1f);
                    }
                    else P.Set(spine: -0.35f, shN: 2.2f, elN: 0.1f, shF: 2.0f, elF: 0.1f, hipN: 0.4f, knN: -0.7f, hipF: -0.2f, knF: -0.5f, hipY: 0.95f, head: 0.3f);
                }
                rate = 26;
            }
            else if (st == "parry")
            {
                if (echo) P.Set(spine: 0.1f, shN: 1.5f, elN: 0.45f, shF: 1.25f, elF: 0.9f, hipN: 0.45f, knN: -0.55f, hipF: -0.4f, knF: -0.3f, hipY: 0.88f);
                else P.Set(shN: 1.3f, elN: 1.7f, shF: 1.1f, elF: 1.8f, spine: -0.08f, hipN: 0.3f, knN: -0.4f, hipY: 0.9f);
                rate = 40;
            }
            else if (st == "hitstun")
            {
                P.Set(spine: -0.5f, head: 0.35f, shN: 1.0f, shF: 1.4f, elN: 0.7f, elF: 0.4f, hipN: 0.35f, knN: -0.55f, hipF: -0.1f, twist: -0.3f); rate = 30;
            }
            else if (st == "nshield")
            {
                // Nova's absorbing shield: the bracer arm thrust out where he aims (the shield stands off it), braced low
                float ga = Mathf.Atan2((float)p.guardDir.y, Mathf.Abs((float)p.guardDir.x));
                bool walk = Mathf.Abs(pvx) > 0.3f; if (walk) rig.phase += dt * Mathf.Abs(pvx) * 2.4f;
                float s2 = walk ? Mathf.Sin(rig.phase) * 0.22f : 0;
                P.Set(spine: 0.18f, shN: ga + PI / 2 + 0.05f, elN: 0.12f, shF: 0.9f, elF: 1.3f, hipN: 0.55f + s2, knN: -0.8f, hipF: -0.45f - s2, knF: -0.4f, hipY: 0.86f, head: -0.08f + 0.2f * ga);
                if (!p.onGround) P.Set(hipN: 0.6f, knN: -0.9f, hipF: -0.2f, knF: -0.7f, hipY: 0.95f);
                rate = 36;
            }
            else if (st == "bulwark")
            {
                P.Set(spine: 0.15f, shN: aimAng + PI / 2 + 0.15f, elN: 0.02f, shF: 0.7f, elF: 1.1f, hipN: 0.5f, knN: -0.4f, hipF: -0.4f); rate = 40;
            }
            else if (st == "beam")
            {
                float j = (R() - 0.5f) * 0.03f;
                P.Set(spine: -0.12f + j, shN: aimAng + PI / 2 - 0.12f, elN: 0.02f, shF: aimAng + PI / 2 - 0.3f, elF: 0.7f, hipN: 0.85f, knN: -0.95f, hipF: -0.65f, knF: -0.35f, hipY: 0.8f + j, head: -0.1f);
                if (!p.onGround) P.Set(hipN: 0.6f, knN: -0.9f, hipF: -0.2f, knF: -0.7f, hipY: 0.95f);
                rate = 30;
            }
            else if (st == "lash")
            {
                P.Set(spine: 0.2f, shN: aimAng + PI / 2 + 0.2f, elN: 0.05f, shF: 0.3f, twist: 0.3f); rate = 36;
            }
            else if (!p.onGround)
            {
                if (p.wallSliding) P.Set(shF: -2.2f, elF: 0.35f, shN: 0.55f, elN: 0.9f, hipN: 0.75f, knN: -1.25f, hipF: -0.35f, knF: -0.45f, spine: -0.12f, head: 0.1f);
                else if (p.rocketT > 0 && pvy > 4) P.Set(hipN: 0.45f, knN: -0.95f, hipF: -0.25f, knF: -0.6f, shN: -0.55f, shF: -0.75f, elN: 0.2f, elF: 0.2f, spine: -0.08f, head: 0.15f);
                else if (pvy > 3) P.Set(hipN: 0.95f, knN: -1.45f, hipF: 0.2f, knF: -0.85f, shN: 1.7f, shF: 1.25f, elN: 0.5f, spine: 0.12f, head: 0.05f);
                else if (pvy > -3) P.Set(hipN: 0.7f, knN: -1.2f, hipF: 0.35f, knF: -1.1f, shN: 1.2f, shF: 1.4f, elN: 0.6f, elF: 0.6f, spine: 0.08f);
                else P.Set(hipN: 0.3f, knN: -0.4f, hipF: -0.3f, knF: -0.75f, shN: 1.05f, shF: 0.85f, elN: 0.5f, elF: 0.4f, spine: 0.04f, head: -0.1f);
                rate = 16;
            }
            else if (p.crouch)
            {
                bool walk = speed > 0.3f;
                if (walk) rig.phase += dt * speed * 3.2f;
                float s = walk ? Mathf.Sin(rig.phase) : 0;
                P.Set(hipY: 0.56f + breathe * 0.012f, spine: 0.42f + breathe * 0.02f, hipN: 1.25f + s * 0.25f, knN: -2.0f - s * 0.15f, hipF: 0.25f - s * 0.25f, knF: -2.3f + s * 0.15f,
                    shN: echo ? 0.95f : 1.1f, elN: echo ? 1.4f : 1.6f, shF: -0.4f, elF: 0.6f, head: -0.25f, twist: -0.12f);
                if (ram) P.Set(hipY: 0.34f + breathe * 0.01f, spine: 1.05f, shN: 0.95f, elN: 0.6f, shF: 0.6f, elF: 1.3f, head: -0.5f, sx: 0.78f, sy: 0.5f, sr: -0.15f);
                rate = 20;
            }
            else if (speed > 0.6f)
            {
                bool back = pvx * facing < 0; float amp = Mathf.Min(1, speed / 7);
                if (mk)
                {
                    rig.phase += dt * speed * 0.95f * (back ? -1 : 1);
                    float s = Mathf.Sin(rig.phase), c = Mathf.Cos(rig.phase);
                    float dv = pvx - rig.prevVx; if (float.IsNaN(dv) || dv == 0) dv = 0;
                    float coast = Mathf.Max(0, 1 - Mathf.Abs(dv) * 20) * (speed > 7 ? 1 : 0);
                    P.Set(hipN: 0.25f + s * 0.35f * amp, knN: -0.75f - Mathf.Max(0, c) * 0.4f, hipF: -0.3f - s * 0.45f * amp, knF: -0.55f - Mathf.Max(0, -c) * 0.3f,
                        shN: -s * 0.9f * amp + 0.1f, shF: s * 0.9f * amp - 0.1f, elN: 0.5f, elF: 0.5f, spine: 0.36f * amp, hipY: 0.84f - Mathf.Abs(c) * 0.03f, twist: s * 0.18f * amp, head: -0.2f * amp);
                    if (coast > 0.5f) P.Set(hipN: 0.35f, knN: -0.95f, hipF: 0.1f, knF: -0.9f, shN: -0.6f, shF: -0.8f, spine: 0.42f, hipY: 0.8f);
                    rig.prevVx = pvx;
                }
                else
                {
                    rig.phase += dt * speed * (ram ? 1.45f : 1.8f) * (back ? -1 : 1);
                    float s = Mathf.Sin(rig.phase), c = Mathf.Cos(rig.phase);
                    P.Set(hipN: s * 0.95f * amp + 0.05f, hipF: -s * 0.95f * amp + 0.05f, knN: -Mathf.Max(0, -c) * 1.45f * amp - 0.15f, knF: -Mathf.Max(0, c) * 1.45f * amp - 0.15f,
                        shN: -s * 0.95f * amp, shF: s * 0.95f * amp, elN: 1.15f, elF: 1.15f, spine: (back ? 0.05f : 0.3f) * amp, hipY: 0.95f - Mathf.Abs(c) * 0.07f, twist: -s * 0.15f * amp, head: -0.2f * amp);
                    if (ram) P.Set(hipN: s * 0.7f * amp + 0.1f, hipF: -s * 0.7f * amp + 0.1f, knN: -Mathf.Max(0, -c) * 1.1f * amp - 0.25f, knF: -Mathf.Max(0, c) * 1.1f * amp - 0.25f,
                        shN: 0.55f, elN: 0.7f, shF: s * 0.6f * amp, elF: 1.2f, spine: (back ? 0.08f : 0.28f) * amp, hipY: 0.92f - Mathf.Abs(c) * 0.08f, sx: 0.5f, sy: 0.98f + Mathf.Abs(c) * 0.05f, sr: 0.05f);
                }
                rate = 22;
            }
            else
            {
                if (echo) P.Set(spine: 0.14f + breathe * 0.02f, hipN: 0.3f, knN: -0.35f, hipF: -0.2f, knF: -0.25f, hipY: 0.92f + breathe * 0.006f, shN: 0.35f, elN: 0.6f, shF: 0.1f, elF: 0.7f, head: -0.05f, twist: 0.08f);
                else if (ram) P.Set(spine: 0.1f + breathe * 0.015f, hipN: 0.28f, knN: -0.32f, hipF: -0.24f, knF: -0.28f, hipY: 0.9f + breathe * 0.006f, shN: 0.45f, elN: 0.55f, shF: -0.1f, elF: 0.6f, head: -0.08f,
                    sx: 0.42f, sy: 0.6f + breathe * 0.005f, sr: 0.05f);
                else if (fix) P.Set(spine: 0.08f + breathe * 0.02f, hipN: 0.18f, knN: -0.12f, hipF: -0.14f, knF: -0.3f, hipY: 0.93f + breathe * 0.006f, shN: 2.4f, elN: 2.2f, shF: -0.1f, elF: 0.5f, head: 0.02f, twist: -0.05f, bodyZ: 0.03f);
                else P.Set(spine: 0.06f + breathe * 0.015f, hipN: 0.12f, knN: -0.15f, hipF: -0.1f, knF: -0.12f, hipY: 0.94f + breathe * 0.005f, shN: 0.3f, elN: 0.7f, shF: -0.05f, elF: 0.4f);
                rate = 10;
            }

            // Aiming layer: Nova's bracer arm, Echo's rifle and Fix's rivet gun follow the aim while shooting
            bool rifle = echo && (p.rifleT >= HUNTER.rifle.raise || (p.rifleCd > 0 && p.rifleCdMax - p.rifleCd < 14));
            bool shooting = (p.chargeT > 0 || p.fireCd > 0 || p.shootT > 0 || rifle || (p.aimFree && p.@char == "nova") || (fix && p.rivetQ > 0)) && In(st, "normal", "dash", "slide");
            if (shooting && !ram)
            {
                P.shN = aimAng + PI / 2 + P.spine; P.elN = 0.02f;
                if (echo)
                {
                    P.shF = aimAng + PI / 2 + P.spine - 0.28f; P.elF = 0.75f;
                    if (p.rifleT >= HUNTER.rifle.raise && p.onGround && st == "normal" && speed < 1.5f) P.Set(hipY: 0.66f, hipN: 0.95f, knN: -1.7f, hipF: -0.15f, knF: -2.1f, spine: 0.12f, head: -0.12f);
                }
            }
            // Head counters the spine so the gaze stays level
            P.head += -P.spine * 0.45f;

            // Ease every joint toward its target (frame-rate independent)
            var cur = rig.cur; float ea = 1 - Mathf.Exp(-rate * dt);
            for (int j = 0; j < NJ; j++) cur[j] = !rig.curSet ? P.v[j] : cur[j] + (P.v[j] - cur[j]) * ea;
            rig.curSet = true;

            // Squash and stretch: a stretch on launches and jumps, a squash on every landing (bigger the harder)
            if (p.rocketT > rig.lastRocketT) rig.stretch = 0.12f + 0.14f * (float)p.rocketPow;
            if (!p.onGround && rig.wasGround && pvy > 8) rig.stretch = Mathf.Max(rig.stretch, 0.07f);
            if (p.onGround && !rig.wasGround) rig.stretch = -Mathf.Min(0.16f, Mathf.Max(0.03f, (-rig.lastVy - 4) * 0.008f));
            if (p.crouch && !rig.wasCrouch && p.onGround) rig.stretch = Mathf.Min(rig.stretch, -0.07f);
            rig.lastRocketT = (float)p.rocketT; rig.wasGround = p.onGround; rig.wasCrouch = p.crouch; if (!p.onGround) rig.lastVy = pvy;
            rig.stretch *= Mathf.Exp(-dt * 10);
            float sy = 1 + rig.stretch, sxz = 1 / Mathf.Sqrt(sy);
            rig.body.scale.set(sxz, sy, sxz);

            float C(int j) => cur[j];
            rig.spine.rotation.set(0, C(1), -C(0));
            rig.head.rotation.z = -C(12);
            rig.armN.top.rotation.z = C(2); rig.armN.joint.rotation.z = C(3);
            rig.armF.top.rotation.z = C(4); rig.armF.joint.rotation.z = C(5);
            rig.legN.top.rotation.z = C(6); rig.legN.joint.rotation.z = C(7);
            rig.legF.top.rotation.z = C(8); rig.legF.joint.rotation.z = C(9);
            rig.hips.position.y = C(10);
            // Whole-body spins are driven directly (easing would unwind them); between spins it settles to 0
            rig.yaw = yaw != 0 ? yaw : rig.yaw * Mathf.Exp(-dt * 20);
            if (yaw == 0 && Mathf.Abs(rig.yaw) > 0.01f) { rig.yaw = JsMod(rig.yaw, PI * 2); if (rig.yaw > PI) rig.yaw -= PI * 2; }
            rig.body.rotation.set(0, rig.yaw, C(11));
            rig.roll = roll != 0 ? roll : rig.roll * Mathf.Exp(-dt * 20);
            if (roll == 0 && Mathf.Abs(rig.roll) > 0.01f) { rig.roll = JsMod(rig.roll, PI * 2); if (rig.roll < -PI) rig.roll += PI * 2; }
            rig.hips.rotation.z = rig.roll;
            rig.body.position.y = st == "downed" || st == "dead" ? 0.1f : 0;

            // ---- Suit details ----
            if (ram || fix) { NewSuits(rig, p, cur, t, dt, aimAng, shooting); return; }
            var ex = rig.extra;
            if (!echo)
            {
                float open = st == "bulwark" ? Mathf.Min(1, (float)p.st / 3) * (p.st < 12 ? 1 : Mathf.Max(0, 1 - ((float)p.st - 12) / 3)) : 0;
                float s = Mathf.Max(0.001f, open);
                ex.shield.scale.set(s, s, s);
                string stage = PlayerSim.ChargeStage(p), bstage = mk ? PlayerSim.BurstStage(p) : "";
                float charge = Mathf.Max(Mathf.Max(Lv(stage), Lv(bstage)), Mathf.Max(DashLevelOf(p), Mathf.Max(st == "beam" ? 4 : 0, st == "pound" && p.pound != null ? (float)p.pound.level : 0)));
                bool flash = stage == "perfect" || bstage == "perfect";
                rig.mats.energy.emissiveIntensity = 2.2f + charge * 1.2f + (p.chargeT > 0 || p.burstT > 0 || p.dashChargeT > 0 || st == "beam" ? Mathf.Sin(t * 30) * 0.4f : 0) + (flash ? 2.5f : 0)
                    + (p.overcharge > 0 ? 1.2f + Mathf.Sin(t * 12) * 0.5f : 0) + (st == "ult" ? 4 + Mathf.Sin(t * 36) * 0.8f : 0)
                    + AbsorbGlow(rig, p, t, dt);
                rig.setHead(SETTINGS.novaHead ?? "bare");
                foreach (var j in ex.jets) { j.visible = p.thrusting; j.scale.set(1, 0.8f + R() * 0.5f, 1); }
                ex.module.visible = mk; foreach (var b in ex.blades) b.visible = mk;
                if (mk && ex.moduleTint != p.attachment)
                {
                    var c = ATTACH_LOOK[p.attachment].tint; ex.moduleTint = p.attachment; ex.moduleMat.colorCss = c; ex.moduleMat.emissiveCss = c;
                }
                bool pound = st == "pound" && p.pound != null;
                bool fist = (st == "attack" && p.move != null && p.move.fist) || pound, kick = fist && !pound && p.moveId == "nova_kair";
                rig.hard += ((fist ? 1 : 0) - rig.hard) * (1 - Mathf.Exp(-dt * (fist ? 34 : 10)));
                bool strike = pound ? p.pound.phase != "hold" || p.pound.level > 0 : fist && p.st >= p.move.su && p.st < p.move.su + p.move.ac + 2;
                float hs = rig.hard * (strike ? 1.25f : 1) * (pound && p.pound.phase == "hold" ? 1 + 0.12f * (float)p.pound.level : 1);
                foreach (var g in ex.gauntlets) { g.visible = rig.hard > 0.04f && !kick; g.scale.set(1.1f * hs, 1.3f * hs, hs); }
                ex.greave.visible = rig.hard > 0.04f && kick; ex.greave.scale.set(1.7f * hs, 0.9f * hs, hs);
                ex.hardMat.emissiveIntensity = strike ? 5.5f : 2.6f; ex.hardMat.opacity = 0.35f + 0.5f * rig.hard;
            }
            else
            {
                bool hunter = SETTINGS.echoKit == "hunter";
                bool staffOut = (st == "attack" && StaffMove(p.moveId)) || st == "vb" || st == "dive" || st == "pound" || st == "ult" || (st == "parry" && hunter) ||
                    ((p.fireCd > 0 || p.chargeT > 0 || p.tracerCd > 60 || rifle) && In(st, "normal", "dash", "slide"));
                ex.handStaff.visible = staffOut; ex.backStaff.visible = !staffOut;
                bool rifleHold = rifle && staffOut && !(st == "attack" || st == "vb" || st == "dive" || st == "parry" || st == "pound");
                float? pz = st == "pound" && p.pound != null ? (p.pound.phase == "hold" ? PI / 2 : -PI / 2) : (float?)null;
                ex.hand.rotation.z = pz ?? (rifleHold ? -PI / 2 : st == "parry" && hunter ? t * 34 : 0);
                ex.hand.position.y = rifleHold ? -0.32f : -0.02f;
                ex.glaive[1].visible = !rifleHold && hunter;
                bool bladeOut = (st == "attack" && p.move != null && p.move.blade) || st == "dashslash";
                ex.blade.visible = bladeOut && (hunter ? true : p.move == null || !p.move.offhand);
                ex.bladeF.visible = bladeOut && hunter;
                ex.glaive[0].visible = hunter;
                foreach (var g in ex.backTips) g.visible = hunter;
                for (int i = 0; i < ex.beltSnares.Count; i++) ex.beltSnares[i].visible = hunter && i < p.snares;
                rig.setHead(SETTINGS.echoHead ?? "helmet");
                bool flare = p.scarfMode == "flare" && p.state != "downed";
                bool focus = p.rifleT >= HUNTER.rifle.raise + HUNTER.rifle.focus;
                rig.mats.energy.emissiveIntensity = 2.0f + (float)p.resolve / 50 + (flare ? 1.1f + Mathf.Sin(t * 9) * 0.45f : 0) + DashLevelOf(p) * 1.1f + (st == "pound" && p.pound != null ? (float)p.pound.level * 1.1f : 0)
                    + (focus ? 1.2f + Mathf.Sin(t * 24) * 0.5f : 0) + (st == "attack" || st == "dashslash" ? 0.8f : 0) + (st == "ult" ? 4 + Mathf.Sin(t * 36) * 0.8f : 0);
                float veil = p.scarfMode == "veil" && p.state != "downed" ? Mathf.Min(1, (float)(p.veilCharge / SCARF.veilFade)) : 0;
                float ck = rig.cloak + (veil - rig.cloak) * (veil > rig.cloak ? 0.25f : 0.45f);
                if (Mathf.Abs(ck - veil) < 0.01f) ck = veil;
                rig.setCloak(ck);
            }
        }
        static float JsMod(float a, float b) => a % b;   // (C#'s % keeps the sign of the dividend, like JavaScript's)

        // RAM's shield, cannon and glows; Fix's wrench, crane arm, welder and ponytail
        static void NewSuits(Rig rig, Player p, float[] cur, float t, float dt, float aimAng, bool shooting)
        {
            var ex = rig.extra; string st = p.state; int charge = Lv(PlayerSim.ChargeStage(p));
            if (p.@char == "ram")
            {
                float sx = cur[13], sy = cur[14], sr = cur[15], spine = cur[0];
                float front = Mathf.Clamp01((sx - 0.35f) / 0.55f);
                ex.shield.position.set(sx, sy, 0.46f - 0.2f * front); ex.shield.rotation.z = sr;
                bool aimNow = shooting || p.aimFree || p.chargeT > 0;
                float want = aimNow ? aimAng + spine : 0.25f + spine * 0.5f;
                rig.cannonA = (rig.cannonA ?? want) + (want - (rig.cannonA ?? want)) * (1 - Mathf.Exp(-dt * 22));
                ex.cannon.rotation.z = rig.cannonA.Value;
                float kin = (float)p.kinetic / 100, guard = st == "guard" ? 0.6f + Mathf.Max(0, 1 - (float)p.guardT / 8) * 2.5f : 0;
                rig.mats.energy.emissiveIntensity = 2.0f + kin * 2.6f + charge * 1.1f + guard + (p.chargeT > 0 ? Mathf.Sin(t * 30) * 0.35f : 0) + (st == "rush" ? 1.5f : 0)
                    + (st == "ult" ? 4 + Mathf.Sin(t * 36) * 0.8f : 0) + (p.braceT > 0 ? 0.8f + Mathf.Sin(t * 10) * 0.4f : 0);
                bool hot = st == "rush" || (st == "ult" && p.ultRun != null && p.ultRun.slamT == 0) || (st == "attack" && p.moveId == "ram_rise");
                foreach (var s in ex.stacks) s.scale.setScalar(hot ? 1.3f + R() * 0.2f : 1);
                foreach (var r in ex.pistons) r.scale.setScalar(hot || (st == "pound" && p.pound != null && p.pound.phase != "hold") ? 1.4f : 1);
                return;
            }
            bool melee = st == "attack" || st == "pound" || st == "vb" || st == "ult";
            bool inHand = melee || !(shooting || st == "patch");
            ex.wrench.visible = inHand; ex.slung.visible = !inHand;
            ex.tipMat.emissiveIntensity = st == "patch" ? 4 + Mathf.Sin(t * 40) * 1.2f : 1.6f;
            rig.craneT = Mathf.Max(0, rig.craneT - dt);
            float open = rig.craneT > 0 ? Mathf.Min(1, rig.craneT / 0.15f) : 0;
            rig.craneK += (open - rig.craneK) * (1 - Mathf.Exp(-dt * 16));
            ex.crane.rotation.z = 1.9f - 2.2f * rig.craneK; ex.craneFore.rotation.z = -2.6f + 1.7f * rig.craneK;
            float w = 0.5f - Mathf.Clamp((float)(p.vx * p.facing) * 0.05f, -0.6f, 0.6f) + Mathf.Clamp((float)-p.vy * 0.04f, -0.5f, 0.8f);
            rig.tail = (rig.tail ?? w) + (w - (rig.tail ?? w)) * (1 - Mathf.Exp(-dt * 9));
            ex.ponytail.rotation.z = rig.tail.Value - 0.5f + Mathf.Sin(t * 3) * 0.04f;
            rig.mats.energy.emissiveIntensity = 2.0f + charge * 1.1f + (p.chargeT > 0 ? Mathf.Sin(t * 30) * 0.35f : 0) + (st == "patch" ? 1 : 0) + (st == "ult" ? 4 + Mathf.Sin(t * 36) * 0.8f : 0)
                + (p.overclockT > 0 ? 0.8f + Mathf.Sin(t * 14) * 0.4f : 0);
        }
    
        // The energy Nova's shield has absorbed (Settings: Nova's LT move): his energy lines burn brighter and his
        // armour takes on a blue rim of light (the shield's colour), rising with each hit it takes and breathing slowly; a block makes it
        // flare for a moment. Returns the energy lines' extra emission.
        static float AbsorbGlow(Rig rig, Player p, float t, float dt)
        {
            float k = Mathf.Clamp((float)p.absorb / 100, 0, 1.5f);   // (past full, as it overfills to 150%, it keeps growing)
            rig.absorbGlow += (k - rig.absorbGlow) * (1 - Mathf.Exp(-dt * 6));
            rig.absorbFlash = Mathf.Max(0, rig.absorbFlash - dt * 3);
            float g = rig.absorbGlow, breathe = 1 + 0.18f * Mathf.Sin(t * (2.2f + 2.5f * g)) * g;
            if (rig.cloak < 0.001f)
                foreach (var m in new[] { rig.mats.@base, rig.mats.trim })
                {
                    if (m.rim == null) continue;
                    float rb = (float)m.userData["rimBase"]; var rc = (Color)m.userData["rimCol"];
                    float w = Mathf.Min(1, g * 0.9f + rig.absorbFlash * 0.5f);
                    m.SetRim(rb + (1.1f * g + 0.8f * rig.absorbFlash) * breathe, 0, w > 0.001f ? Color.Lerp(rc.linear, ABSORB_RIM.linear, w).gamma : rc);
                }
            return (2.6f * g + 2 * rig.absorbFlash) * breathe;
        }
        static readonly Color ABSORB_RIM = new Color(0.55f, 0.88f, 1f);   // (the shield's blue)
}
}
