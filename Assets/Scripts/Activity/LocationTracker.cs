using System;
using System.Collections.Generic;
using UnityEngine;

namespace Contract.Activity
{
    /// <summary>
    /// The game's single entry point to location. Systems call <see cref="Acquire"/> while they need
    /// fixes and <see cref="Release"/> when done; updates run only in between (battery), only in the
    /// foreground (stopped on pause, restarted on resume), and only with permission. Permission is
    /// re-checked on every resume, so revoking it in Android settings is noticed.
    /// Fixes stay in memory; logs never contain coordinates.
    /// </summary>
    public sealed class LocationTracker : MonoBehaviour
    {
        public const float DesiredAccuracy = 5f;     // ask for GPS-grade fixes
        public const float UpdateDistance = 2f;      // the OS may skip fixes closer than this

        public static LocationTracker Instance { get; private set; }

        public event Action<LocationStatus> StatusChanged;
        public event Action<GeoFix> FixReceived;

        private readonly HashSet<object> owners = new HashSet<object>();
        private ILocationSource source;
        private bool sourceRunning;
        private bool paused;
        private double lastFixTimestamp = double.NaN;

        public LocationStatus Status { get; private set; } = LocationStatus.Off;
        public bool IsActive => owners.Count > 0;
        public int FixCount { get; private set; }
        public GeoFix? LatestFix { get; private set; }
        public string SourceName => source?.Name ?? "none";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateInstance()
        {
            if (Instance == null) new GameObject("[LocationTracker]").AddComponent<LocationTracker>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            if (DevicePermissions.IsAndroidDevice) source = new DeviceLocationSource();
        }

        /// <summary>Replaces the source (simulated routes in the editor and tests). Restarts if active.</summary>
        public void UseSource(ILocationSource replacement)
        {
            StopSource();
            source = replacement;
            Refresh();
        }

        public void Acquire(object owner)
        {
            if (owner != null && owners.Add(owner)) Refresh();
        }

        public void Release(object owner)
        {
            if (owner != null && owners.Remove(owner)) Refresh();
        }

        /// <summary>Re-evaluates permission and device state (after a permission dialog or returning from settings).</summary>
        public void Refresh()
        {
            if (!IsActive || paused)
            {
                StopSource();
                SetStatus(LocationStatus.Off);
                return;
            }

            if (source == null)
            {
                SetStatus(LocationStatus.Unsupported);
                return;
            }

            if (source is DeviceLocationSource)
            {
                switch (DevicePermissions.Location)
                {
                    case PermissionState.NotAsked:
                        StopSource();
                        SetStatus(LocationStatus.NeedsPermission);
                        return;
                    case PermissionState.Denied:
                    case PermissionState.DeniedPermanently:
                        StopSource();
                        SetStatus(LocationStatus.PermissionDenied);
                        return;
                }
            }

            if (!sourceRunning)
            {
                FixCount = 0;
                LatestFix = null;
                source.Start(DesiredAccuracy, UpdateDistance);
                sourceRunning = true;
                lastFixTimestamp = double.NaN;
            }

            SetStatus(source.Status);
        }

        private void StopSource()
        {
            LatestFix = null; // a stopped source has no current position
            if (!sourceRunning) return;
            source?.Stop();
            sourceRunning = false;
        }

        /// <summary>Drops every owner (end of a test, or leaving GPS mode entirely).</summary>
        public void ReleaseAll()
        {
            owners.Clear();
            Refresh();
        }

        private float recheckTimer;

        private void Update()
        {
            if (!sourceRunning)
            {
                // Location switched off from the quick-settings shade: notice when it comes back.
                if (IsActive && !paused && Status == LocationStatus.ServiceDisabled && (recheckTimer -= Time.unscaledDeltaTime) <= 0f)
                {
                    recheckTimer = 2f;
                    Refresh();
                }

                return;
            }

            source.Tick();
            SetStatus(source.Status);
            if (source.Status == LocationStatus.ServiceDisabled)
            {
                StopSource(); // restarted by Refresh once the player switches location on
                return;
            }

            if (!source.TryGetLatest(out GeoFix fix) || fix.Timestamp.Equals(lastFixTimestamp)) return;
            lastFixTimestamp = fix.Timestamp;
            LatestFix = fix;
            FixCount++;
            FixReceived?.Invoke(fix);
        }

        private void SetStatus(LocationStatus status)
        {
            if (status == Status) return;
            Status = status;
            Debug.Log($"[GPS] {status} (source: {SourceName})");
            StatusChanged?.Invoke(status);
        }

        // Foreground-only design: no location while the game is in the background.
        private void OnApplicationPause(bool pause)
        {
            paused = pause;
            Refresh();
        }

        // Returning from the permission dialog or the notification shade (no pause involved).
        private void OnApplicationFocus(bool focus)
        {
            if (focus && !paused) Refresh();
        }
    }
}
