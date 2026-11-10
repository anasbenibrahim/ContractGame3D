using System.Collections.Generic;
using System.Linq;
using Contract.Activity;

namespace Contract.Tests
{
    /// <summary>Synthetic GPS traces (1 Hz) shared by the distance tests: walks, standing, spikes, vehicles.</summary>
    public static class GpsTraces
    {
        public const double Lat = 37.5, Lon = 127.0;

        private static double Gauss(System.Random r) => System.Math.Sqrt(-2.0 * System.Math.Log(1.0 - r.NextDouble())) * System.Math.Cos(2.0 * System.Math.PI * r.NextDouble());

        /// <summary>Legs of (metres, bearing); jitter = 1-sigma noise in metres.</summary>
        public static List<GeoFix> Walk((double meters, double bearing)[] legs, double speed = 1.4, float accuracy = 5f, double jitter = 2,
            int seed = 1, double lat = Lat, double lon = Lon, double t0 = 0)
        {
            var r = new System.Random(seed);
            var fixes = new List<GeoFix>();
            double t = t0;
            foreach ((double meters, double bearing) in legs)
            {
                int steps = (int)System.Math.Round(meters / speed);
                for (int i = 0; i < steps; i++)
                {
                    (lat, lon) = Geo.Offset(lat, lon, speed, bearing);
                    t += 1;
                    (double jl, double jo) = Geo.Offset(lat, lon, System.Math.Abs(Gauss(r) * jitter), r.NextDouble() * 360);
                    fixes.Add(new GeoFix(jl, jo, accuracy, t));
                }
            }

            return fixes;
        }

        public static List<GeoFix> Still(int seconds, double jitter, float accuracy, int seed)
        {
            var r = new System.Random(seed);
            return Enumerable.Range(1, seconds).Select(t =>
            {
                (double la, double lo) = Geo.Offset(Lat, Lon, System.Math.Abs(Gauss(r) * jitter), r.NextDouble() * 360);
                return new GeoFix(la, lo, accuracy, t);
            }).ToList();
        }

        /// <summary>
        /// Standing still with realistic GPS error: slow correlated wander (multipath, satellite
        /// geometry; AR(1) with time constant <paramref name="tau"/>) plus white jitter.
        /// </summary>
        public static List<GeoFix> Wander(int seconds, double sigma, double tau, double white, float accuracy, int seed)
        {
            var r = new System.Random(seed);
            double ex = 0, ey = 0, a = System.Math.Exp(-1 / tau), k = sigma * System.Math.Sqrt(1 - a * a);
            var fixes = new List<GeoFix>();
            for (int t = 1; t <= seconds; t++)
            {
                ex = a * ex + Gauss(r) * k;
                ey = a * ey + Gauss(r) * k;
                double dx = ex + Gauss(r) * white, dy = ey + Gauss(r) * white;
                (double la, double lo) = Geo.Offset(Lat, Lon, System.Math.Sqrt(dx * dx + dy * dy), System.Math.Atan2(dx, dy) * 180 / System.Math.PI);
                fixes.Add(new GeoFix(la, lo, accuracy, t));
            }

            return fixes;
        }

        public static double Run(IEnumerable<GeoFix> fixes, MotionEvidence motion, WalkingDistanceFilter filter = null)
        {
            filter ??= new WalkingDistanceFilter();
            foreach (GeoFix f in fixes) filter.Add(f, motion);
            filter.Flush();
            return filter.TotalMeters;
        }

        public static double Run(IEnumerable<GeoFix> fixes, WalkingDistanceFilter filter = null)
        {
            filter ??= new WalkingDistanceFilter();
            foreach (GeoFix f in fixes) filter.Add(f);
            filter.Flush();
            return filter.TotalMeters;
        }
    }
}
