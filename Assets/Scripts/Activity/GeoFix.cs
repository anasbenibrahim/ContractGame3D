using System;

namespace Contract.Activity
{
    /// <summary>One location reading. Kept in memory only; precise positions are never saved.</summary>
    public readonly struct GeoFix
    {
        public readonly double Latitude;
        public readonly double Longitude;
        /// <summary>Radius (m) the device is ~68% confident the true position lies within.</summary>
        public readonly float HorizontalAccuracy;
        /// <summary>Seconds (device clock or simulation clock), used for speed checks and duplicate rejection.</summary>
        public readonly double Timestamp;

        public GeoFix(double latitude, double longitude, float horizontalAccuracy, double timestamp)
        {
            Latitude = latitude;
            Longitude = longitude;
            HorizontalAccuracy = horizontalAccuracy;
            Timestamp = timestamp;
        }

        public override string ToString() => $"({Latitude:F5}, {Longitude:F5}) ±{HorizontalAccuracy:F0} m @ {Timestamp:F1}";
    }

    /// <summary>Geodesy helpers (WGS84 sphere approximation, accurate to well under 1% at walking scale).</summary>
    public static class Geo
    {
        public const double EarthRadiusMeters = 6371008.8;

        /// <summary>Great-circle (haversine) distance in metres.</summary>
        public static double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
        {
            double p1 = lat1 * Math.PI / 180.0, p2 = lat2 * Math.PI / 180.0;
            double dp = (lat2 - lat1) * Math.PI / 180.0, dl = (lon2 - lon1) * Math.PI / 180.0;
            double a = Math.Sin(dp / 2) * Math.Sin(dp / 2) + Math.Cos(p1) * Math.Cos(p2) * Math.Sin(dl / 2) * Math.Sin(dl / 2);
            return 2.0 * EarthRadiusMeters * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
        }

        public static double DistanceMeters(GeoFix a, GeoFix b) => DistanceMeters(a.Latitude, a.Longitude, b.Latitude, b.Longitude);

        /// <summary>The point <paramref name="meters"/> away from (lat, lon) along <paramref name="bearingDegrees"/> (0 = north).</summary>
        public static (double lat, double lon) Offset(double lat, double lon, double meters, double bearingDegrees)
        {
            double d = meters / EarthRadiusMeters, b = bearingDegrees * Math.PI / 180.0;
            double p1 = lat * Math.PI / 180.0, l1 = lon * Math.PI / 180.0;
            double p2 = Math.Asin(Math.Sin(p1) * Math.Cos(d) + Math.Cos(p1) * Math.Sin(d) * Math.Cos(b));
            double l2 = l1 + Math.Atan2(Math.Sin(b) * Math.Sin(d) * Math.Cos(p1), Math.Cos(d) - Math.Sin(p1) * Math.Sin(p2));
            return (p2 * 180.0 / Math.PI, (l2 * 180.0 / Math.PI + 540.0) % 360.0 - 180.0);
        }
    }

    /// <summary>What the location layer is doing, from the player's point of view.</summary>
    public enum LocationStatus
    {
        Off,               // not requested by anything
        NeedsPermission,   // the player has not granted location access yet
        PermissionDenied,  // refused (possibly "don't ask again": settings needed)
        ServiceDisabled,   // device location is switched off
        Initializing,      // waiting for the first fix
        Running,           // fixes arriving
        NoSignal,          // running, but no fresh fix for a while (indoors, tunnel)
        Failed,            // the platform reported a failure
        Unsupported        // no location hardware / platform
    }

    /// <summary>A source of location fixes: the phone, or a simulated route for tests and the editor.</summary>
    public interface ILocationSource
    {
        string Name { get; }
        LocationStatus Status { get; }
        /// <summary>Starts updates (desired accuracy and minimum update distance in metres).</summary>
        void Start(float desiredAccuracyMeters, float updateDistanceMeters);
        void Stop();
        /// <summary>Called once per frame by the service.</summary>
        void Tick();
        /// <summary>The newest fix, if any has arrived since <see cref="Start"/>.</summary>
        bool TryGetLatest(out GeoFix fix);
    }
}
