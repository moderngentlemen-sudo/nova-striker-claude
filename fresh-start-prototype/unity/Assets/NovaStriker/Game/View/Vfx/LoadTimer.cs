// How long each part of loading took: logged to the Console once the game is ready, and shown under the frame time
// readout (Settings › Effects), so slow loading can be traced to the part that needs work.
using System.Diagnostics;
using System.Text;

namespace NovaStriker.Game
{
    public static class LoadTimer
    {
        static readonly Stopwatch all = Stopwatch.StartNew(), part = Stopwatch.StartNew();
        static readonly StringBuilder sb = new StringBuilder();
        public static string Report = "";
        public static void Begin() { }   // (the clocks start the first time this class is touched: call it first thing)
        public static void Mark(string what) { sb.Append(what).Append(' ').Append(part.ElapsedMilliseconds).Append(" ms · "); part.Restart(); }
        public static void Done()
        {
            Report = $"loaded in {all.ElapsedMilliseconds} ms: " + sb.ToString().TrimEnd(' ', '·');
            UnityEngine.Debug.Log("Nova Striker " + Report);
        }
    }
}
