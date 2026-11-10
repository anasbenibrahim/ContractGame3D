using UnityEngine;

namespace Contract.Activity
{
    /// <summary>
    /// The phone's location through Unity's LocationService (GPS + network). Foreground only:
    /// updates run between Start and Stop, which the service ties to an active session.
    /// </summary>
    public sealed class DeviceLocationSource : ILocationSource
    {
        private const float FirstFixTimeout = 30f;
        private const float StaleAfter = 12f;

        private float startedAt;
        private double lastTimestamp = -1;
        private float lastFixAt;
        private GeoFix latest;
        private bool hasFix;
        private bool started;

        public string Name => "Device GPS";
        public LocationStatus Status { get; private set; } = LocationStatus.Off;

        public void Start(float desiredAccuracyMeters, float updateDistanceMeters)
        {
            hasFix = false;
            lastTimestamp = -1;
            if (!Input.location.isEnabledByUser)
            {
                Status = LocationStatus.ServiceDisabled;
                started = false;
                return;
            }

            Input.location.Start(desiredAccuracyMeters, updateDistanceMeters);
            started = true;
            startedAt = Time.realtimeSinceStartup;
            Status = LocationStatus.Initializing;
        }

        public void Stop()
        {
            if (started) Input.location.Stop();
            started = false;
            hasFix = false;
            Status = LocationStatus.Off;
        }

        public void Tick()
        {
            if (!started) return;
            switch (Input.location.status)
            {
                case LocationServiceStatus.Failed:
                    // Most often the player switched location off while playing.
                    Status = Input.location.isEnabledByUser ? LocationStatus.Failed : LocationStatus.ServiceDisabled;
                    return;
                case LocationServiceStatus.Stopped:
                    Status = Input.location.isEnabledByUser ? LocationStatus.Off : LocationStatus.ServiceDisabled;
                    return;
                case LocationServiceStatus.Initializing:
                    Status = Time.realtimeSinceStartup - startedAt > FirstFixTimeout ? LocationStatus.NoSignal : LocationStatus.Initializing;
                    return;
            }

            LocationInfo info = Input.location.lastData;
            if (info.timestamp > 0 && info.timestamp != lastTimestamp)
            {
                lastTimestamp = info.timestamp;
                latest = new GeoFix(info.latitude, info.longitude, info.horizontalAccuracy, info.timestamp);
                hasFix = true;
                lastFixAt = Time.realtimeSinceStartup;
            }

            float age = hasFix ? Time.realtimeSinceStartup - lastFixAt : Time.realtimeSinceStartup - startedAt;
            Status = age > (hasFix ? StaleAfter : FirstFixTimeout) ? LocationStatus.NoSignal : hasFix ? LocationStatus.Running : LocationStatus.Initializing;
        }

        public bool TryGetLatest(out GeoFix fix)
        {
            fix = latest;
            return hasFix;
        }
    }
}
