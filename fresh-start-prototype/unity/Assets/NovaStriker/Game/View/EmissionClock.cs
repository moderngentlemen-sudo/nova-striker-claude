// Presentation timing only; no simulation state or random numbers. Pause never consumes pending time.
using System;
namespace NovaStriker.Game {
 public sealed class EmissionClock {
  double pending;
  public int TakeSteps(double seconds) {
   if (seconds <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) return 0;
   pending += seconds;
   int steps = (int)Math.Min(8, Math.Floor(pending * 60 + 0.000001));
   pending = Math.Min(8.0 / 60, Math.Max(0, pending - steps / 60.0)); // discard long-stall debt
   return steps;
  }
 }
}
