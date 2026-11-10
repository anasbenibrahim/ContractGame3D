using System.Collections.Generic;
using UnityEngine;

namespace Contract.Activity
{
    /// <summary>
    /// Replays a GPS trace for the editor and automated tests: either a recorded list of fixes, or
    /// a synthetic walk (speed, heading, jitter, accuracy, optional signal gaps). Uses its own
    /// clock so tests can advance time deterministically.
    /// </summary>
    public sealed class SimulatedLocationSource : ILocationSource
    {
        private readonly List<GeoFix> trace = new List<GeoFix>();
        private int index = -1;
        private double clock;
        private bool running;

        public string Name => "Simulated route";
        public LocationStatus Status { get; private set; } = LocationStatus.Off;
        public bool AutoAdvance { get; set; } = true;
        public int FixesDelivered => index + 1;
        public double Clock => clock;

        /// <summary>Overrides the status (to simulate a disabled service, denied permission...).</summary>
        public LocationStatus? ForcedStatus { get; set; }

        public SimulatedLocationSource(IEnumerable<GeoFix> fixes) => trace.AddRange(fixes);

        /// <summary>A straight walk: one fix per second, gaussian-ish jitter of <paramref name="jitter"/> metres.</summary>
        public static SimulatedLocationSource Walk(double lat, double lon, float bearing, float metersPerSecond, int seconds,
            float accuracy = 6f, float jitter = 0f, int seed = 1)
        {
            var random = new System.Random(seed);
            var fixes = new List<GeoFix>();
            for (int s = 0; s <= seconds; s++)
            {
                (double la, double lo) = Geo.Offset(lat, lon, metersPerSecond * s, bearing);
                if (jitter > 0f)
                {
                    double j = (random.NextDouble() + random.NextDouble() - 1.0) * jitter;
                    (la, lo) = Geo.Offset(la, lo, j, random.NextDouble() * 360.0);
                }

                fixes.Add(new GeoFix(la, lo, accuracy, s));
            }

            return new SimulatedLocationSource(fixes);
        }

        private bool everStarted;

        /// <summary>Starts (or, like a real GPS after a pause, continues) the trace in time.</summary>
        public void Start(float desiredAccuracyMeters, float updateDistanceMeters)
        {
            running = true;
            if (!everStarted)
            {
                index = -1;
                clock = trace.Count > 0 ? trace[0].Timestamp : 0;
                everStarted = true;
            }

            Status = ForcedStatus ?? LocationStatus.Initializing;
        }

        public void Stop()
        {
            running = false;
            Status = LocationStatus.Off;
        }

        /// <summary>Moves the simulation clock (tests); fixes whose timestamp has passed become current.</summary>
        public void Advance(double seconds)
        {
            clock += seconds;
            while (index + 1 < trace.Count && trace[index + 1].Timestamp <= clock) index++;
        }

        public void Tick()
        {
            if (!running) return;
            if (ForcedStatus.HasValue)
            {
                Status = ForcedStatus.Value;
                return;
            }

            if (AutoAdvance) Advance(Time.unscaledDeltaTime);
            Status = index < 0 ? LocationStatus.Initializing : clock - trace[index].Timestamp > 12.0 ? LocationStatus.NoSignal : LocationStatus.Running;
        }

        public bool TryGetLatest(out GeoFix fix)
        {
            bool ok = running && !ForcedStatus.HasValue && index >= 0;
            fix = ok ? trace[index] : default;
            return ok;
        }
    }
}
