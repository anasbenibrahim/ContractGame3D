using System;
using UnityEngine;

namespace Contract.Activity
{
    /// <summary>
    /// Session steps from the hardware step counter: counted from a baseline taken when the
    /// session starts or resumes (never the phone's lifetime total, never steps taken while
    /// paused), surviving a counter reset after a reboot. Also turns recent steps into motion
    /// evidence for the GPS distance filter. When no sensor or permission is available it says
    /// so and reports no steps - it never estimates steps from GPS.
    /// </summary>
    public sealed class StepTracker
    {
        public const float SteppingWindow = 6f;   // s: steps within this mean "walking"
        public const float StillAfter = 10f;      // s without steps means "standing"

        private readonly IStepSource source;
        private long? baseline;
        private long lastCumulative;
        private long carried;               // steps from before a counter reset (reboot)
        private float lastStepAt = float.NegativeInfinity;
        private float startedAt;
        private bool running;

        public StepTracker(IStepSource source) => this.source = source;

        /// <summary>Steps since the session (re)started tracking.</summary>
        public long Steps => carried + (baseline.HasValue ? Math.Max(0, lastCumulative - baseline.Value) : 0);
        public StepSensorStatus Status => source.Status;
        public bool Available => source.Status == StepSensorStatus.Running || source.Status == StepSensorStatus.Waiting;

        /// <summary>Begins counting from the current hardware total (session start or resume).</summary>
        public void Begin(float now)
        {
            source.Start();
            baseline = null;
            carried = 0;
            running = true;
            startedAt = now;
            lastStepAt = float.NegativeInfinity;
        }

        public void End()
        {
            source.Stop();
            running = false;
        }

        /// <summary>Polls the sensor; returns the new steps since the last call.</summary>
        public long Tick(float now)
        {
            if (!running || !source.TryRead(out long cumulative)) return 0;
            if (!baseline.HasValue)
            {
                // First reading: some phones only report on the first step, so the step that triggered
                // it is not counted. Accurate to within one step.
                baseline = cumulative;
                lastCumulative = cumulative;
                return 0;
            }

            if (cumulative < lastCumulative)
            {
                // The counter restarted (the phone rebooted): keep what was counted, restart from zero.
                carried = Steps;
                baseline = 0;
                lastCumulative = cumulative;
                if (cumulative > 0) lastStepAt = now;
                return cumulative;
            }

            long added = cumulative - lastCumulative;
            lastCumulative = cumulative;
            if (added > 0) lastStepAt = now;
            return added;
        }

        /// <summary>Evidence for the distance filter from the steps of the last few seconds.</summary>
        public MotionEvidence Evidence(float now)
        {
            // Only a sensor that has actually reported can testify. A silent one ("waiting") may be
            // broken or batching: then the GPS-only rules apply rather than "standing still".
            if (!running || source.Status != StepSensorStatus.Running || !baseline.HasValue) return MotionEvidence.Unknown;
            if (now - lastStepAt <= SteppingWindow) return MotionEvidence.Stepping;
            return now - Math.Max(startedAt, lastStepAt) >= StillAfter ? MotionEvidence.Still : MotionEvidence.Unknown;
        }
    }
}
