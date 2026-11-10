using System;
using System.Collections.Generic;

namespace Contract.Activity
{
    /// <summary>Why a fix did not add distance (dashboard diagnostics and tests).</summary>
    public enum FixVerdict
    {
        Counted,        // added walking distance
        Pending,        // accepted, movement not established yet (anchor kept: counted later if real)
        Standing,       // accepted, the player is standing still (drift discarded)
        First,          // first fix of a track
        PoorAccuracy,   // accuracy worse than the threshold
        Duplicate,      // same or older timestamp
        Implausible,    // a jump faster than walking / running (outlier)
        Vehicle         // sustained vehicle speed: track restarted, nothing counted
    }

    /// <summary>Independent evidence of walking from the phone's motion sensors (step detector).</summary>
    public enum MotionEvidence
    {
        Unknown,   // no step sensor / no permission: GPS only (stricter, less accurate)
        Stepping,  // steps detected recently
        Still      // no steps for a while: whatever GPS says, the player is not walking
    }

    /// <summary>Movement-detection thresholds for one evidence level.</summary>
    [Serializable]
    public struct MovementTest
    {
        public float window;        // s of raw fixes compared (two halves)
        public float minWalkSpeed;  // m/s between the halves' centres
        public float significance;  // separation needed, in standard errors of the GPS scatter
    }

    /// <summary>Tunable values for <see cref="WalkingDistanceFilter"/> (defaults tuned on synthetic traces).</summary>
    [Serializable]
    public struct WalkingFilterSettings
    {
        public float maxAccuracy;      // m: fixes worse than this are ignored
        public float minMove;          // m: smallest distance step that is counted
        public float accuracyFactor;   // step threshold = clamp(accuracy * factor, minMove, maxStep)
        public float maxStep;          // m
        public float maxSpeed;         // m/s: faster jumps are outliers (running up to ~16 km/h is allowed)
        public float processNoise;     // m/s: how quickly the smoothed path may follow the fixes
        public int minFixes;           // fixes needed before movement can be judged
        public int confirmFixes;       // consecutive "moving" judgements needed to start counting
        public int vehicleFixes;       // consecutive implausible fixes that mean "in a vehicle"
        public MovementTest gpsOnly;   // without step evidence: strict (GPS drift can look like slow walking)
        public MovementTest stepping;  // with steps detected: responsive

        public static WalkingFilterSettings Default => new WalkingFilterSettings
        {
            maxAccuracy = 25f, minMove = 3f, accuracyFactor = 0.6f, maxStep = 12f, maxSpeed = 4.5f, processNoise = 2.5f,
            minFixes = 8, confirmFixes = 3, vehicleFixes = 3,
            gpsOnly = new MovementTest { window = 45f, minWalkSpeed = 0.8f, significance = 3.5f },
            stepping = new MovementTest { window = 20f, minWalkSpeed = 0.4f, significance = 3.0f }
        };
    }

    /// <summary>
    /// Turns raw GPS fixes into real walking distance without counting GPS drift.
    /// Pipeline: accuracy gate → duplicate rejection → speed plausibility (outliers, vehicles) →
    /// "is the player moving?" → Kalman-smoothed path counted in steps from an anchor point.
    /// Movement is a statistical test on the raw fixes: the two halves of a recent window must be
    /// further apart than their (motion-detrended) scatter explains. Step-sensor evidence, when
    /// available, decides standing outright and relaxes the test while stepping. While movement is
    /// undecided the anchor is kept, so the first metres of a real walk are counted once it is
    /// confirmed rather than lost. Pure C#, no Unity.
    /// </summary>
    public sealed class WalkingDistanceFilter
    {
        private readonly WalkingFilterSettings s;
        private readonly List<(double t, double x, double y)> raw = new List<(double, double, double)>();
        private bool started;
        private double lat0, lon0, cosLat0;
        private double x, y, variance, anchorX, anchorY, lastT;
        private double rawLat, rawLon;
        private int implausibleRun, movingRun;
        private bool steppedSinceAnchor; // the step sensor saw walking since the anchor was set

        public double TotalMeters { get; private set; }
        public FixVerdict LastVerdict { get; private set; }
        public bool IsWalking { get; private set; }
        public int Counted { get; private set; }
        public int Rejected { get; private set; }

        public WalkingDistanceFilter(WalkingFilterSettings settings) => s = settings;

        public WalkingDistanceFilter() : this(WalkingFilterSettings.Default) { }

        /// <summary>Feeds one fix with the current motion evidence; returns the metres it added.</summary>
        public double Add(GeoFix fix, MotionEvidence motion = MotionEvidence.Unknown)
        {
            if (fix.HorizontalAccuracy <= 0f || fix.HorizontalAccuracy > s.maxAccuracy) return Reject(FixVerdict.PoorAccuracy);
            if (!started)
            {
                Restart(fix);
                return Verdict(FixVerdict.First);
            }

            if (fix.Timestamp <= lastT) return Reject(FixVerdict.Duplicate);

            double dt = fix.Timestamp - lastT;
            double jump = Geo.DistanceMeters(rawLat, rawLon, fix.Latitude, fix.Longitude);
            if ((jump - fix.HorizontalAccuracy) / dt > s.maxSpeed)
            {
                if (++implausibleRun >= s.vehicleFixes)
                {
                    double before = Flush(); // the walk before the vehicle still counts ...
                    Restart(fix);            // ... the ride does not
                    Reject(FixVerdict.Vehicle);
                    return before;
                }

                return Reject(FixVerdict.Implausible);
            }

            implausibleRun = 0;
            rawLat = fix.Latitude;
            rawLon = fix.Longitude;
            lastT = fix.Timestamp;

            // Smoothed path: 1D Kalman per axis in local metres, measurement noise = accuracy².
            (double mx, double my) = Local(fix.Latitude, fix.Longitude);
            variance += s.processNoise * s.processNoise * dt;
            double gain = variance / (variance + fix.HorizontalAccuracy * fix.HorizontalAccuracy);
            x += gain * (mx - x);
            y += gain * (my - y);
            variance *= 1.0 - gain;

            if (motion == MotionEvidence.Stepping) steppedSinceAnchor = true;
            MovementTest test = motion == MotionEvidence.Stepping ? s.stepping : s.gpsOnly;
            raw.Add((fix.Timestamp, mx, my));
            while (raw.Count > 2 && fix.Timestamp - raw[0].t > test.window) raw.RemoveAt(0);

            if (motion == MotionEvidence.Still)
            {
                double last = Flush();       // finish the walk that just ended ...
                movingRun = 0;
                IsWalking = false;
                steppedSinceAnchor = false;
                anchorX = x;                 // ... then ignore whatever GPS drift does meanwhile
                anchorY = y;
                LastVerdict = last > 0 ? FixVerdict.Counted : FixVerdict.Standing;
                return last;
            }

            bool? moving = Moving(test);
            if (moving == null) return Verdict(FixVerdict.Pending);
            if (moving == false)
            {
                movingRun = 0;
                IsWalking = false;
                if (fix.Timestamp - raw[0].t >= test.window * 0.9 && !steppedSinceAnchor)
                {
                    anchorX = x;             // confirmed standing: discard the drift
                    anchorY = y;
                    return Verdict(FixVerdict.Standing);
                }

                return Verdict(FixVerdict.Pending);
            }

            if (++movingRun < s.confirmFixes) return Verdict(FixVerdict.Pending);
            IsWalking = true;
            double threshold = Math.Min(s.maxStep, Math.Max(s.minMove, s.accuracyFactor * fix.HorizontalAccuracy));
            double fromAnchor = Hypot(x - anchorX, y - anchorY);
            return fromAnchor >= threshold ? Count(fromAnchor) : Verdict(FixVerdict.Pending);
        }

        /// <summary>Counts the remaining walked distance since the anchor (pause / stop / before a vehicle).</summary>
        public double Flush()
        {
            if (!started) return 0;
            double fromAnchor = Hypot(x - anchorX, y - anchorY);
            bool walked = IsWalking || steppedSinceAnchor; // short walks: steps prove it even if GPS had too few fixes
            steppedSinceAnchor = false;
            if (!walked || fromAnchor < s.minMove)
            {
                anchorX = x;
                anchorY = y;
                return 0;
            }

            return Count(fromAnchor);
        }

        /// <summary>Forgets the track (after a gap long enough to break continuity).</summary>
        public void Reset()
        {
            started = false;
            IsWalking = false;
            raw.Clear();
        }

        /// <summary>
        /// Two-half test: are the centres of the window's halves further apart than the GPS scatter
        /// explains, at walking speed? Scatter is measured after removing each half's own motion.
        /// Null while there are too few fixes.
        /// </summary>
        private bool? Moving(MovementTest test)
        {
            int n = raw.Count;
            if (n < s.minFixes) return null;
            int h = n / 2;
            (double ta, double xa, double ya, double ra) = Half(0, h);
            (double tb, double xb, double yb, double rb) = Half(h, n);
            double separation = Hypot(xb - xa, yb - ya);
            double sd = Math.Sqrt((ra + rb) / Math.Max(1, 2 * n - 8)); // per-axis noise
            double standardError = sd * Math.Sqrt(1.0 / h + 1.0 / (n - h)) * Math.Sqrt(2.0);
            return separation >= test.significance * standardError && separation / Math.Max(1e-6, tb - ta) >= test.minWalkSpeed;
        }

        /// <summary>Mean time / position of raw[from..to) and the squared residuals after a linear fit per axis.</summary>
        private (double t, double x, double y, double residual) Half(int from, int to)
        {
            int n = to - from;
            double tm = 0, xm = 0, ym = 0;
            for (int i = from; i < to; i++)
            {
                tm += raw[i].t;
                xm += raw[i].x;
                ym += raw[i].y;
            }

            tm /= n;
            xm /= n;
            ym /= n;
            double den = 0, bx = 0, by = 0;
            for (int i = from; i < to; i++)
            {
                double dt = raw[i].t - tm;
                den += dt * dt;
                bx += dt * (raw[i].x - xm);
                by += dt * (raw[i].y - ym);
            }

            if (den < 1e-9) den = 1e-9;
            bx /= den;
            by /= den;
            double residual = 0;
            for (int i = from; i < to; i++)
            {
                double dt = raw[i].t - tm;
                double ex = raw[i].x - (xm + bx * dt), ey = raw[i].y - (ym + by * dt);
                residual += ex * ex + ey * ey;
            }

            return (tm, xm, ym, residual);
        }

        private double Count(double meters)
        {
            TotalMeters += meters;
            Counted++;
            steppedSinceAnchor = false;
            anchorX = x;
            anchorY = y;
            LastVerdict = FixVerdict.Counted;
            return meters;
        }

        private double Verdict(FixVerdict verdict)
        {
            LastVerdict = verdict;
            return 0;
        }

        private double Reject(FixVerdict verdict)
        {
            Rejected++;
            return Verdict(verdict);
        }

        private void Restart(GeoFix fix)
        {
            started = true;
            lat0 = fix.Latitude;
            lon0 = fix.Longitude;
            cosLat0 = Math.Cos(lat0 * Math.PI / 180.0);
            x = y = anchorX = anchorY = 0;
            variance = fix.HorizontalAccuracy * fix.HorizontalAccuracy;
            lastT = fix.Timestamp;
            rawLat = fix.Latitude;
            rawLon = fix.Longitude;
            implausibleRun = movingRun = 0;
            IsWalking = false;
            steppedSinceAnchor = false;
            raw.Clear();
            raw.Add((fix.Timestamp, 0, 0));
        }

        private static double Hypot(double a, double b) => Math.Sqrt(a * a + b * b);

        private (double, double) Local(double lat, double lon) =>
            ((lon - lon0) * Math.PI / 180.0 * Geo.EarthRadiusMeters * cosLat0, (lat - lat0) * Math.PI / 180.0 * Geo.EarthRadiusMeters);
    }
}
