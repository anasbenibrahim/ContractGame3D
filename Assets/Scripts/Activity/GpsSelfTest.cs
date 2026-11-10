using Contract.Core;
using UnityEngine;

namespace Contract.Activity
{
    /// <summary>
    /// Device check for the location layer, started with <c>-contract-gps-selftest</c>
    /// (Android: adb shell am start -n &lt;package&gt;/&lt;activity&gt; -e unity "-contract-gps-selftest").
    /// Requests location for 60 s and logs permission, status, fix count and accuracy every 5 s
    /// (never coordinates). Read with: adb logcat -s Unity
    /// </summary>
    public sealed class GpsSelfTest : MonoBehaviour
    {
        private const float Duration = 60f;
        private float started, nextLog;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            if (!LaunchArguments.Has("-contract-gps-selftest")) return;
            var go = new GameObject("[GpsSelfTest]");
            DontDestroyOnLoad(go);
            go.AddComponent<GpsSelfTest>();
        }

        private void Start()
        {
            started = Time.realtimeSinceStartup;
            Debug.Log($"[GPS selftest] platform {Application.platform}, location permission {DevicePermissions.Location}, " +
                      $"activity permission {DevicePermissions.Activity}, device location on: {Input.location.isEnabledByUser}");
            LocationTracker.Instance.Acquire(this);
        }

        private void Update()
        {
            float t = Time.realtimeSinceStartup - started;
            if (t >= nextLog)
            {
                nextLog += 5f;
                LocationTracker tracker = LocationTracker.Instance;
                string accuracy = tracker.LatestFix.HasValue ? $"±{tracker.LatestFix.Value.HorizontalAccuracy:F0} m ({GpsQuality.Label(tracker.LatestFix.Value.HorizontalAccuracy)})" : "-";
                Debug.Log($"[GPS selftest] t={t:F0}s status {tracker.Status}, fixes {tracker.FixCount}, accuracy {accuracy}");
            }

            if (t > Duration)
            {
                LocationTracker.Instance.Release(this);
                Debug.Log("[GPS selftest] done");
                Destroy(gameObject);
            }
        }
    }

    /// <summary>Player-facing GPS accuracy rating.</summary>
    public static class GpsQuality
    {
        public const float Good = 10f, Fair = 25f;

        public static string Label(float accuracyMeters) =>
            accuracyMeters <= 0f ? "Unknown" : accuracyMeters <= Good ? "Good" : accuracyMeters <= Fair ? "Fair" : "Poor";

        public static string KoreanLabel(float accuracyMeters) =>
            accuracyMeters <= 0f ? "알 수 없음" : accuracyMeters <= Good ? "좋음" : accuracyMeters <= Fair ? "보통" : "나쁨";
    }
}
