using System.Collections.Generic;
using System.Linq;
using Contract.Activity;
using NUnit.Framework;

namespace Contract.Tests
{
    /// <summary>
    /// The GPS walking filter on synthetic traces: walks are measured within a few percent and
    /// GPS drift, spikes, vehicles, bad fixes and duplicates add (almost) nothing.
    /// </summary>
    public sealed class WalkingDistanceTests
    {
        private static readonly (double, double)[] Square1Km = { (250, 0), (250, 90), (250, 180), (250, 270) };

        [Test]
        public void StraightWalk_100m() => Assert.AreEqual(100, GpsTraces.Run(GpsTraces.Walk(new[] { (100.0, 90.0) })), 5);

        [Test]
        public void ShortWalk_10m_WithSteps_IsCounted() =>
            Assert.AreEqual(10, GpsTraces.Run(GpsTraces.Walk(new[] { (10.0, 0.0) }), MotionEvidence.Stepping), 3);

        [Test]
        public void ShortWalk_10m_GpsOnly_IsNeverOvercounted()
        {
            // ~7 s of walking is too short for GPS alone to tell from drift: it may be missed, never inflated.
            Assert.LessOrEqual(GpsTraces.Run(GpsTraces.Walk(new[] { (10.0, 0.0) })), 10.5);
        }

        [Test]
        public void Square_1km_GoodAndRoughSignal()
        {
            Assert.AreEqual(1000, GpsTraces.Run(GpsTraces.Walk(Square1Km, jitter: 3, accuracy: 8, seed: 3)), 30, "good signal");
            Assert.AreEqual(1000, GpsTraces.Run(GpsTraces.Walk(Square1Km, jitter: 6, accuracy: 15, seed: 4)), 50, "rough signal");
        }

        [Test]
        public void SlowStroll_WithSteps_IsCounted() =>
            Assert.AreEqual(100, GpsTraces.Run(GpsTraces.Walk(new[] { (100.0, 45.0) }, speed: 0.9, seed: 9), MotionEvidence.Stepping), 6);

        [Test]
        public void UnhurriedWalk_GpsOnly_IsCounted() =>
            Assert.AreEqual(100, GpsTraces.Run(GpsTraces.Walk(new[] { (100.0, 45.0) }, speed: 1.0, seed: 9)), 12);

        [TestCase(300, 6, 10f, 5)]
        [TestCase(600, 8, 12f, 7)]
        [TestCase(600, 4, 6f, 8)]
        public void StandingStill_DriftIsNotWalking(int seconds, double jitter, float accuracy, int seed)
        {
            Assert.Less(GpsTraces.Run(GpsTraces.Still(seconds, jitter, accuracy, seed)), 2.0, "GPS drift counted as walking");
        }

        [Test]
        public void SingleSpike_IsRejected()
        {
            List<GeoFix> walk = GpsTraces.Walk(new[] { (60.0, 0.0) });
            (double la, double lo) = Geo.Offset(walk[30].Latitude, walk[30].Longitude, 200, 90);
            walk.Insert(30, new GeoFix(la, lo, 5f, walk[30].Timestamp - 0.5));
            var filter = new WalkingDistanceFilter();
            Assert.AreEqual(60, GpsTraces.Run(walk, filter), 7);
            Assert.Greater(filter.Rejected, 0);
        }

        [Test]
        public void VehicleRide_IsNotCounted_ButTheWalksAroundItAre()
        {
            List<GeoFix> before = GpsTraces.Walk(new[] { (50.0, 0.0) });
            GeoFix a = before.Last();
            List<GeoFix> ride = GpsTraces.Walk(new[] { (900.0, 0.0) }, speed: 15, lat: a.Latitude, lon: a.Longitude, t0: a.Timestamp);
            GeoFix b = ride.Last();
            List<GeoFix> after = GpsTraces.Walk(new[] { (50.0, 0.0) }, lat: b.Latitude, lon: b.Longitude, t0: b.Timestamp);
            Assert.AreEqual(100, GpsTraces.Run(before.Concat(ride).Concat(after)), 10, "car distance leaked or walking lost");
        }

        [Test]
        public void SignalGap_StraightAcross_IsCountedWhenPlausible()
        {
            List<GeoFix> gap = GpsTraces.Walk(new[] { (100.0, 0.0) }).Where(f => !(f.Timestamp > 30 && f.Timestamp <= 60)).ToList();
            Assert.AreEqual(100, GpsTraces.Run(gap), 7);
        }

        [Test]
        public void InaccurateFixes_AreIgnored()
        {
            List<GeoFix> mixed = GpsTraces.Walk(new[] { (100.0, 0.0) }).Select((f, i) => i % 2 == 1 ? new GeoFix(f.Latitude, f.Longitude, 60f, f.Timestamp) : f).ToList();
            var filter = new WalkingDistanceFilter();
            Assert.AreEqual(100, GpsTraces.Run(mixed, filter), 7);
            Assert.AreEqual(mixed.Count / 2, filter.Rejected, 1, "poor-accuracy fixes rejected");
        }

        [Test]
        public void DuplicateFixes_AreIgnored()
        {
            List<GeoFix> doubled = GpsTraces.Walk(new[] { (100.0, 0.0) }).SelectMany(f => new[] { f, f }).ToList();
            Assert.AreEqual(100, GpsTraces.Run(doubled), 7);
        }

        // ---- realistic drift and motion-sensor evidence -----------------------------------------

        [Test]
        public void CorrelatedDrift_GpsOnly_IsBounded()
        {
            // GPS alone cannot fully separate slow drift from slow walking: the GPS-only test keeps it small.
            double[] drift = Enumerable.Range(1, 12).Select(seed => GpsTraces.Run(GpsTraces.Wander(600, 8, 30, 3, 12f, seed))).ToArray();
            Assert.Less(drift.Average(), 12.0, $"mean drift per 10 min: {drift.Average():F1} m");
            Assert.Less(drift.Max(), 45.0);
        }

        [Test]
        public void CorrelatedDrift_WithNoSteps_IsZero()
        {
            for (int seed = 1; seed <= 12; seed++)
            {
                Assert.AreEqual(0, GpsTraces.Run(GpsTraces.Wander(600, 12, 45, 4, 18f, seed), MotionEvidence.Still), 1e-9, $"seed {seed}");
            }
        }

        [Test]
        public void Walking_WithSteps_IsAccurate_EvenWithRoughGps()
        {
            for (int seed = 1; seed <= 6; seed++)
            {
                double meters = GpsTraces.Run(GpsTraces.Walk(Square1Km, jitter: 6, accuracy: 15, seed: seed), MotionEvidence.Stepping);
                Assert.AreEqual(1000, meters, 50, $"seed {seed}");
            }

            Assert.AreEqual(10, GpsTraces.Run(GpsTraces.Walk(new[] { (10.0, 0.0) }), MotionEvidence.Stepping), 3, "a 10 m walk");
        }

        [Test]
        public void StoppingAfterAWalk_CountsTheWalk_ThenNothing()
        {
            List<GeoFix> walk = GpsTraces.Walk(new[] { (50.0, 0.0) });
            var filter = new WalkingDistanceFilter();
            foreach (GeoFix f in walk) filter.Add(f, MotionEvidence.Stepping);
            GeoFix end = walk.Last();
            foreach (GeoFix f in GpsTraces.Wander(300, 8, 30, 3, 12f, 4))
            {
                (double la, double lo) = (end.Latitude + (f.Latitude - GpsTraces.Lat), end.Longitude + (f.Longitude - GpsTraces.Lon));
                filter.Add(new GeoFix(la, lo, f.HorizontalAccuracy, end.Timestamp + f.Timestamp), MotionEvidence.Still);
            }

            Assert.AreEqual(50, filter.TotalMeters, 5);
        }

        [Test]
        public void TeleportingFixes_NeverAddMoreThanAWalkCould()
        {
            // Alternating far-apart positions (a broken GPS): a filter must not turn this into distance.
            var r = new System.Random(3);
            var fixes = Enumerable.Range(1, 120).Select(t =>
            {
                (double la, double lo) = Geo.Offset(GpsTraces.Lat, GpsTraces.Lon, t % 2 == 0 ? 0 : 400, r.NextDouble() * 360);
                return new GeoFix(la, lo, 8f, t);
            });
            Assert.Less(GpsTraces.Run(fixes), 30);
        }
    }
}
