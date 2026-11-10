using UnityEngine;
using UnityEngine.InputSystem;

namespace Contract.Activity
{
    public enum StepSensorStatus
    {
        Off,               // not requested
        Unsupported,       // no step-counter hardware (or not a phone)
        NeedsPermission,   // "Physical activity" permission not granted
        Waiting,           // enabled, no reading yet (the sensor reports on the first step)
        Running            // readings arriving
    }

    /// <summary>A cumulative hardware step counter (steps since the device booted).</summary>
    public interface IStepSource
    {
        StepSensorStatus Status { get; }
        void Start();
        void Stop();
        /// <summary>The device's cumulative count, if a reading has arrived.</summary>
        bool TryRead(out long cumulativeSteps);
    }

    /// <summary>
    /// Android's TYPE_STEP_COUNTER through the Input System (no native plugin). Needs the
    /// "Physical activity" (ACTIVITY_RECOGNITION) permission. The value is the total since the
    /// phone booted, so callers count from a baseline.
    /// </summary>
    public sealed class DeviceStepSource : IStepSource
    {
        private bool started;
        private bool hasReading;

        public StepSensorStatus Status
        {
            get
            {
                if (!started) return StepSensorStatus.Off;
                if (DevicePermissions.IsAndroidDevice && DevicePermissions.Activity != PermissionState.Granted) return StepSensorStatus.NeedsPermission;
                if (StepCounter.current == null) return StepSensorStatus.Unsupported;
                return hasReading ? StepSensorStatus.Running : StepSensorStatus.Waiting;
            }
        }

        public static bool HardwarePresent => StepCounter.current != null;

        public void Start()
        {
            started = true;
            hasReading = false;
            if (StepCounter.current != null && !StepCounter.current.enabled) InputSystem.EnableDevice(StepCounter.current);
        }

        public void Stop()
        {
            started = false;
            hasReading = false;
            if (StepCounter.current != null && StepCounter.current.enabled) InputSystem.DisableDevice(StepCounter.current); // battery
        }

        public bool TryRead(out long cumulativeSteps)
        {
            cumulativeSteps = 0;
            StepCounter counter = StepCounter.current;
            if (!started || counter == null || !counter.enabled) return false;
            // A sensor that has never reported keeps its default state; the first event arrives with the first step.
            if (counter.lastUpdateTime <= 0) return false;
            cumulativeSteps = counter.stepCounter.ReadValue();
            hasReading = true;
            return true;
        }
    }

    /// <summary>Scripted step counter for the editor and tests (a cumulative total the test drives).</summary>
    public sealed class SimulatedStepSource : IStepSource
    {
        private bool started;

        public long Cumulative { get; set; }
        public bool Supported { get; set; } = true;
        public bool HasReading { get; set; } = true;

        public SimulatedStepSource(long bootTotal) => Cumulative = bootTotal;

        public StepSensorStatus Status => !started ? StepSensorStatus.Off : !Supported ? StepSensorStatus.Unsupported
            : HasReading ? StepSensorStatus.Running : StepSensorStatus.Waiting;

        public void Start() => started = true;
        public void Stop() => started = false;

        public bool TryRead(out long cumulativeSteps)
        {
            cumulativeSteps = Cumulative;
            return started && Supported && HasReading;
        }
    }
}
