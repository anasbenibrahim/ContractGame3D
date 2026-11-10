using Contract.Activity;
using Contract.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Contract.UI
{
    /// <summary>
    /// City HUD strip: the current control mode, and in GPS Life Mode the live walk (distance,
    /// steps, GPS state). Tapping it opens the activity dashboard. Refreshes four times a second.
    /// </summary>
    public sealed class ActivityHudStrip : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Image statusLight;
        [SerializeField] private ActivityDashboardView dashboard;

        private float timer;

        public string Label => label.text;

        private void Awake()
        {
            button.onClick.AddListener(() => dashboard.Open());
            // The strip belongs to the city HUD; in menus the dashboard is opened from the menu.
            if (!GameConstants.Scenes.InGameplay) gameObject.SetActive(false);
        }

        private void Update()
        {
            if ((timer -= Time.unscaledDeltaTime) > 0f) return;
            timer = 0.25f;
            ActivityController a = ActivityController.Instance;
            if (a == null) return;
            if (!a.GpsModeActive)
            {
                label.text = "<b>NORMAL</b>  ·  GPS 활동 · GPS activity";
                statusLight.color = new Color(0.45f, 0.48f, 0.55f);
                return;
            }

            ActivitySessionState s = a.State;
            string state = a.IsRunning ? "" : a.IsPaused ? "  ·  일시정지 · PAUSED" : "  ·  시작 대기 · not started";
            string stepsText = (s?.steps ?? 0) > 0 ? $"  ·  {s.steps:N0} 걸음" : "";
            label.text = $"<b><color=#FF4D5A>GPS LIFE</color></b>  ·  {Meters(s?.realMeters ?? 0)}{stepsText}{state}";
            statusLight.color = a.IsRunning && a.GpsStatus == LocationStatus.Running ? new Color(0.3f, 0.83f, 0.54f)
                : a.IsRunning ? new Color(1f, 0.69f, 0.13f) : new Color(0.45f, 0.48f, 0.55f);
        }

        private static string Meters(double m) => m >= 1000 ? $"{m / 1000:F2} km" : $"{m:F0} m";
    }
}
