using System;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace Contract.Activity
{
    public enum PermissionState
    {
        NotRequired,        // platform without runtime permissions (PC, editor)
        NotAsked,           // never requested in this install
        Granted,
        Denied,             // refused; asking again shows the system dialog
        DeniedPermanently   // "don't ask again": only the app's settings page can change it
    }

    /// <summary>
    /// Runtime permissions for the activity mode: precise location (walking distance, checkpoints)
    /// and physical activity (the step counter). The game always explains why before showing the
    /// system dialog, and offers the settings page when Android will no longer ask.
    /// </summary>
    public static class DevicePermissions
    {
        public const string FineLocation = "android.permission.ACCESS_FINE_LOCATION";
        public const string CoarseLocation = "android.permission.ACCESS_COARSE_LOCATION";
        public const string ActivityRecognition = "android.permission.ACTIVITY_RECOGNITION";

        private const string AskedKey = "contract.permission.asked.";
        private const string PermanentKey = "contract.permission.permanent.";

        public static bool IsAndroidDevice => Application.platform == RuntimePlatform.Android;

        public static PermissionState Location => State(FineLocation);
        public static PermissionState Activity => State(ActivityRecognition);

        public static PermissionState State(string permission)
        {
            if (!IsAndroidDevice) return PermissionState.NotRequired;
#if UNITY_ANDROID
            if (Permission.HasUserAuthorizedPermission(permission)) return PermissionState.Granted;
#endif
            if (PlayerPrefs.GetInt(PermanentKey + permission, 0) == 1) return PermissionState.DeniedPermanently;
            return PlayerPrefs.GetInt(AskedKey + permission, 0) == 1 ? PermissionState.Denied : PermissionState.NotAsked;
        }

        /// <summary>Shows the system dialog (call after the in-game explanation). The callback runs on the main thread.</summary>
        public static void Request(string permission, Action<PermissionState> done)
        {
            if (!IsAndroidDevice)
            {
                done?.Invoke(PermissionState.NotRequired);
                return;
            }
#if UNITY_ANDROID
            if (Permission.HasUserAuthorizedPermission(permission))
            {
                done?.Invoke(PermissionState.Granted);
                return;
            }

            PlayerPrefs.SetInt(AskedKey + permission, 1);
            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += _ => { PlayerPrefs.DeleteKey(PermanentKey + permission); done?.Invoke(PermissionState.Granted); };
            callbacks.PermissionDenied += _ => done?.Invoke(PermissionState.Denied);
            callbacks.PermissionDeniedAndDontAskAgain += _ =>
            {
                PlayerPrefs.SetInt(PermanentKey + permission, 1);
                done?.Invoke(PermissionState.DeniedPermanently);
            };
            Permission.RequestUserPermission(permission, callbacks);
#endif
        }

        /// <summary>The game's page in Android settings (to re-enable a permanently denied permission).</summary>
        public static void OpenAppSettings() => StartSettingsActivity("android.settings.APPLICATION_DETAILS_SETTINGS", true);

        /// <summary>Android's location settings (to switch device location on).</summary>
        public static void OpenLocationSettings() => StartSettingsActivity("android.settings.LOCATION_SOURCE_SETTINGS", false);

        private static void StartSettingsActivity(string action, bool forThisApp)
        {
            if (!IsAndroidDevice) return;
#if UNITY_ANDROID
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var intent = new AndroidJavaObject("android.content.Intent", action);
                if (forThisApp)
                {
                    using var uriClass = new AndroidJavaClass("android.net.Uri");
                    using AndroidJavaObject uri = uriClass.CallStatic<AndroidJavaObject>("fromParts", "package", Application.identifier, null);
                    intent.Call<AndroidJavaObject>("setData", uri);
                }

                activity.Call("startActivity", intent);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Permissions] Could not open settings ({action}): {e.Message}");
            }
#endif
        }
    }
}
