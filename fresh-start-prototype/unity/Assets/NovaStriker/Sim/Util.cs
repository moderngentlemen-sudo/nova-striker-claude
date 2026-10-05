// Small helpers the prototype defines in several of its modules
namespace NovaStriker.Sim
{
    public static class U
    {
        // (v > 0 ? 1 : v < 0 ? -1 : 0): unlike Math.sign, NaN gives 0
        public static double sign(double v) => v > 0 ? 1 : v < 0 ? -1 : 0;
        public static double approach(double v, double t, double d) => v < t ? JMath.Min(v + d, t) : JMath.Max(v - d, t);
        public static double clamp(double v, double a, double b) => JMath.Max(a, JMath.Min(b, v));
        // a || b for numbers (0 and NaN are false)
        public static double or(double a, double b) => JMath.Truthy(a) ? a : b;
        public static bool truthy(double a) => JMath.Truthy(a);
        public static void dec(ref double v) { if (v > 0) v--; }
    }
}

namespace NovaStriker.Sim
{
    public static class LiveIter
    {
        // for...of over a JS array: an index walk that sees elements pushed (or spliced) during the loop
        public static System.Collections.Generic.IEnumerable<T> Live<T>(this System.Collections.Generic.List<T> l)
        {
            for (int i = 0; i < l.Count; i++) yield return l[i];
        }
    }
}
