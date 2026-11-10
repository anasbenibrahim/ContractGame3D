using UnityEngine;

namespace Contract.Core
{
    /// <summary>
    /// Short haptic ticks for touch-button presses (Android vibrator, API 26+ one-shot with
    /// amplitude; a plain short vibrate below that). No-op on other platforms, when the device has
    /// no vibrator, or when the player turned haptics off. Needs android.permission.VIBRATE
    /// (declared by the manifest post-processor).
    /// </summary>
    public static class Haptics
    {
        /// <summary>Number of ticks requested (tests / diagnostics; counted even where no vibrator exists).</summary>
        public static int RequestCount { get; private set; }

        public static bool Enabled => GameManager.Instance?.Session?.Settings?.haptics ?? true;

        public static void Tick(long milliseconds = 12, int amplitude = 90)
        {
            if (!Enabled) return;
            RequestCount++;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                Vibrator ??= CreateVibrator();
                if (Vibrator == null) return;
                if (Sdk >= 26)
                {
                    using var effectClass = new AndroidJavaClass("android.os.VibrationEffect");
                    using AndroidJavaObject effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", milliseconds, Mathf.Clamp(amplitude, 1, 255));
                    Vibrator.Call("vibrate", effect);
                }
                else
                {
                    Vibrator.Call("vibrate", milliseconds);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Haptics] Vibration unavailable: " + e.Message);
                unavailable = true;
                Vibrator = null;
            }
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject Vibrator;
        private static bool unavailable;
        private static int Sdk => new AndroidJavaClass("android.os.Build$VERSION").GetStatic<int>("SDK_INT");

        private static AndroidJavaObject CreateVibrator()
        {
            if (unavailable) return null;
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            AndroidJavaObject vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            if (vibrator == null || !vibrator.Call<bool>("hasVibrator"))
            {
                unavailable = true;
                return null;
            }

            return vibrator;
        }
#endif
    }
}
