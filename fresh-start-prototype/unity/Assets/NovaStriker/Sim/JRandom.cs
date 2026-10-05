// The simulation's one source of randomness (the prototype's Math.random): mulberry32, a small seeded generator,
// so a run can be replayed exactly, and checked against the JavaScript prototype running with the same seed.
namespace NovaStriker.Sim
{
    public static class JRandom
    {
        static uint state = 0x2F6B3C1Du;

        public static uint State { get => state; set => state = value; }

        public static void Seed(uint seed) { state = seed; }

        // A number in [0, 1)
        public static double Next()
        {
            unchecked
            {
                state += 0x6D2B79F5u;
                uint t = state;
                t = (t ^ (t >> 15)) * (1u | t);
                t = (t + (t ^ (t >> 7)) * (61u | t)) ^ t;
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }
    }
}
