// Math with JavaScript's semantics, so the C# simulation computes exactly what the browser prototype does.
// Math.round rounds halves up (C# rounds them to even), Math.sign keeps NaN and zero, Math.max/min return NaN
// when any argument is NaN, and Math.hypot is V8's (a scaled, compensated sum). Every bit of the simulation's
// math goes through here, and sin, cos, tan, atan2 and pow use V8's own algorithms (Fdlibm.cs).
using System;

namespace NovaStriker.Sim
{
    public static class JMath
    {
        public const double PI = Math.PI;
        public const double Infinity = double.PositiveInfinity;

        public static double Abs(double x) => Math.Abs(x);
        public static double Floor(double x) => Math.Floor(x);
        public static double Ceil(double x) => Math.Ceiling(x);
        public static double Sqrt(double x) => Math.Sqrt(x);
        // V8's own algorithms (fdlibm), not the platform's libm: the same last bit everywhere
        public static double Sin(double x) => Fdlibm.Sin(x);
        public static double Cos(double x) => Fdlibm.Cos(x);
        public static double Tan(double x) => Fdlibm.Tan(x);
        public static double Atan2(double y, double x) => Fdlibm.Atan2(y, x);
        public static double Pow(double x, double y) => Fdlibm.Pow(x, y);

        // Math.round: the nearest integer, halves toward +Infinity (V8: ceil, then step back if that overshot)
        public static double Round(double x)
        {
            if (double.IsNaN(x) || double.IsInfinity(x)) return x;
            double r = Math.Ceiling(x);
            if (r - 0.5 > x) r -= 1.0;
            return r;
        }

        public static double Sign(double x) => x > 0 ? 1 : x < 0 ? -1 : x;

        public static double Max(double a, double b)
        {
            if (double.IsNaN(a) || double.IsNaN(b)) return double.NaN;
            if (a > b) return a;
            if (b > a) return b;
            return a == 0 && IsNegZero(a) ? b : a;   // max(-0, +0) is +0
        }
        public static double Min(double a, double b)
        {
            if (double.IsNaN(a) || double.IsNaN(b)) return double.NaN;
            if (a < b) return a;
            if (b < a) return b;
            return a == 0 && !IsNegZero(a) ? b : a;  // min(+0, -0) is -0
        }
        public static double Max(double a, double b, double c) => Max(Max(a, b), c);
        public static double Min(double a, double b, double c) => Min(Min(a, b), c);
        public static double Max(double a, double b, double c, double d) => Max(Max(Max(a, b), c), d);
        public static double Min(double a, double b, double c, double d) => Min(Min(Min(a, b), c), d);

        static bool IsNegZero(double x) => x == 0 && BitConverter.DoubleToInt64Bits(x) < 0;

        // V8's Math.hypot: normalise by the largest magnitude, Kahan-sum the squares, scale back
        public static double Hypot(double a, double b)
        {
            if (double.IsInfinity(a) || double.IsInfinity(b)) return double.PositiveInfinity;
            if (double.IsNaN(a) || double.IsNaN(b)) return double.NaN;
            double x = Math.Abs(a), y = Math.Abs(b);
            double max = x > y ? x : y;
            if (max == 0) return 0;
            double sum = 0, comp = 0;
            Kahan(x / max, ref sum, ref comp);
            Kahan(y / max, ref sum, ref comp);
            return Math.Sqrt(sum) * max;
        }
        public static double Hypot(double a, double b, double c)
        {
            if (double.IsInfinity(a) || double.IsInfinity(b) || double.IsInfinity(c)) return double.PositiveInfinity;
            if (double.IsNaN(a) || double.IsNaN(b) || double.IsNaN(c)) return double.NaN;
            double x = Math.Abs(a), y = Math.Abs(b), z = Math.Abs(c);
            double max = x;
            if (y > max) max = y;
            if (z > max) max = z;
            if (max == 0) return 0;
            double sum = 0, comp = 0;
            Kahan(x / max, ref sum, ref comp);
            Kahan(y / max, ref sum, ref comp);
            Kahan(z / max, ref sum, ref comp);
            return Math.Sqrt(sum) * max;
        }
        static void Kahan(double n, ref double sum, ref double comp)
        {
            double summand = n * n - comp;
            double preliminary = sum + summand;
            comp = (preliminary - sum) - summand;
            sum = preliminary;
        }

        // JavaScript truthiness of a number (0 and NaN are false)
        public static bool Truthy(double x) => x != 0 && !double.IsNaN(x);
        // a || b for numbers
        public static double Or(double a, double b) => Truthy(a) ? a : b;
    }
}
