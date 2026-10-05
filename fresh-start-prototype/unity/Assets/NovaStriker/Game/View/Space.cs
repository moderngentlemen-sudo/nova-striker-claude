// Gameplay space to world space (space.js). The sim is 2D (x along the path, y up); the path curves, so a sim
// point maps onto the path frame, with `depth` toward the camera. Results are in the prototype's (three.js)
// space, which TObj mirrors into Unity.
using NovaStriker.Game.Three;
using NovaStriker.Sim;
using UnityEngine;

namespace NovaStriker.Game
{
    public static class S
    {
        public static Vector3 W(double x, double y, double depth = 0)
        {
            var f = Level.Frame(x);
            return new Vector3((float)(f.px + f.nx * depth), (float)y, (float)(f.pz + f.nz * depth));
        }
        public static Vector3 Dir(double x, double dx, double dy)
        {
            var f = Level.Frame(x);
            return new Vector3((float)(f.tx * dx), (float)dy, (float)(f.tz * dx));
        }
        public static float YawAt(double x) { var f = Level.Frame(x); return Mathf.Atan2((float)-f.tz, (float)f.tx); }
        // CSS colours as three.js keeps them (linear)
        public static Color Lin(string css) => Th.Hex(css).linear;
        public static float Rnd() => Random.value;
        // A world point of an object in three.js space
        public static Vector3 WorldOf(TObj o) => o.worldPos;
        public static Vector3 ToUnity(Vector3 three) => new Vector3(three.x, three.y, -three.z);
        public static Vector3 FromUnity(Vector3 u) => new Vector3(u.x, u.y, -u.z);
    }
}
