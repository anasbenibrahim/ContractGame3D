using System;
using System.Linq;
using UnityEngine;

namespace Contract.Core
{
    /// <summary>
    /// Launch flags on every platform: the command line on PC, and on Android the "unity" intent
    /// extra (adb shell am start ... -e unity "-contract-gps-selftest").
    /// </summary>
    public static class LaunchArguments
    {
        private static string[] cached;

        public static string[] All
        {
            get
            {
                if (cached != null) return cached;
                cached = Environment.GetCommandLineArgs();
                if (Application.platform == RuntimePlatform.Android)
                {
                    try
                    {
                        using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                        using AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                        using AndroidJavaObject intent = activity.Call<AndroidJavaObject>("getIntent");
                        string extra = intent.Call<string>("getStringExtra", "unity");
                        if (!string.IsNullOrEmpty(extra)) cached = cached.Concat(extra.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)).ToArray();
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("[LaunchArguments] Could not read the Android intent: " + e.Message);
                    }
                }

                return cached;
            }
        }

        public static bool Has(string flag) => All.Contains(flag);

        public static string Value(string flag)
        {
            int i = Array.IndexOf(All, flag);
            return i >= 0 && i + 1 < All.Length ? All[i + 1] : null;
        }
    }
}
