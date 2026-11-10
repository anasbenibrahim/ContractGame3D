using System;
using System.Collections;
using Contract.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Contract.UI
{
    /// <summary>
    /// Manages UI transitions, screen fades, and panel animations.
    /// </summary>
    public sealed class UITransitionManager : MonoBehaviour
    {
        public static UITransitionManager Instance { get; private set; }

        [Header("Screen Fade")]
        [SerializeField] private Image fadeImage;
        [SerializeField] private float defaultFadeDuration = 0.5f;

        [Header("Panel Animations")]
        [SerializeField] private float panelFadeDuration = 0.3f;
        [SerializeField] private float panelSlideDuration = 0.4f;
        [SerializeField] private AnimationCurve slideCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Hover Effects")]
        [SerializeField] private float hoverScale = 1.05f;
        [SerializeField] private float hoverDuration = 0.1f;

        private CanvasGroup fadeCanvasGroup;
        private Coroutine fadeCoroutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                DestroyImmediate(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (fadeImage != null)
            {
                fadeCanvasGroup = fadeImage.GetComponent<CanvasGroup>();
                if (fadeCanvasGroup == null) fadeCanvasGroup = fadeImage.gameObject.AddComponent<CanvasGroup>();
                fadeCanvasGroup.alpha = 0f;
                fadeImage.raycastTarget = false;
            }
        }

        // ===== Screen Fade =====
        public void FadeIn(float duration = -1f, Action onComplete = null)
        {
            if (fadeCanvasGroup == null) { onComplete?.Invoke(); return; }
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeCoroutine(1f, 0f, duration > 0 ? duration : defaultFadeDuration, onComplete));
        }

        public void FadeOut(float duration = -1f, Action onComplete = null)
        {
            if (fadeCanvasGroup == null) { onComplete?.Invoke(); return; }
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeCoroutine(0f, 1f, duration > 0 ? duration : defaultFadeDuration, onComplete));
        }

        public void FadeTo(float targetAlpha, float duration = -1f, Action onComplete = null)
        {
            if (fadeCanvasGroup == null) { onComplete?.Invoke(); return; }
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeCoroutine(fadeCanvasGroup.alpha, targetAlpha, duration > 0 ? duration : defaultFadeDuration, onComplete));
        }

        private IEnumerator FadeCoroutine(float from, float to, float duration, Action onComplete)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                fadeCanvasGroup.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }
            fadeCanvasGroup.alpha = to;
            onComplete?.Invoke();
        }

        // ===== Panel Transitions =====
        public void ShowPanel(GameObject panel, bool animate = true, Action onComplete = null)
        {
            if (panel == null) { onComplete?.Invoke(); return; }
            if (animate) StartCoroutine(ShowPanelCoroutine(panel, onComplete));
            else { panel.SetActive(true); onComplete?.Invoke(); }
        }

        public void HidePanel(GameObject panel, bool animate = true, Action onComplete = null)
        {
            if (panel == null) { onComplete?.Invoke(); return; }
            if (animate) StartCoroutine(HidePanelCoroutine(panel, onComplete));
            else { panel.SetActive(false); onComplete?.Invoke(); }
        }

        private IEnumerator ShowPanelCoroutine(GameObject panel, Action onComplete)
        {
            panel.SetActive(true);
            CanvasGroup cg = panel.GetComponent<CanvasGroup>();
            if (cg == null) cg = panel.AddComponent<CanvasGroup>();

            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;

            RectTransform rt = panel.GetComponent<RectTransform>();
            Vector2 startPos = rt.anchoredPosition + Vector2.up * 50f;
            Vector2 endPos = rt.anchoredPosition;
            rt.anchoredPosition = startPos;

            float elapsed = 0f;
            while (elapsed < panelFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / panelFadeDuration);
                float curvedT = slideCurve.Evaluate(t);
                cg.alpha = curvedT;
                rt.anchoredPosition = Vector2.Lerp(startPos, endPos, curvedT);
                yield return null;
            }

            cg.alpha = 1f;
            rt.anchoredPosition = endPos;
            cg.interactable = true;
            cg.blocksRaycasts = true;
            onComplete?.Invoke();
        }

        private IEnumerator HidePanelCoroutine(GameObject panel, Action onComplete)
        {
            CanvasGroup cg = panel.GetComponent<CanvasGroup>();
            if (cg == null) cg = panel.AddComponent<CanvasGroup>();

            cg.interactable = false;
            cg.blocksRaycasts = false;

            RectTransform rt = panel.GetComponent<RectTransform>();
            Vector2 startPos = rt.anchoredPosition;
            Vector2 endPos = rt.anchoredPosition + Vector2.up * 50f;

            float elapsed = 0f;
            while (elapsed < panelFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / panelFadeDuration);
                float curvedT = slideCurve.Evaluate(t);
                cg.alpha = 1f - curvedT;
                rt.anchoredPosition = Vector2.Lerp(startPos, endPos, curvedT);
                yield return null;
            }

            cg.alpha = 0f;
            rt.anchoredPosition = endPos;
            panel.SetActive(false);
            onComplete?.Invoke();
        }

        // ===== Button Hover Effects (using EventTrigger) =====
        public void SetupButtonHover(Button button, float customScale = -1f)
        {
            if (button == null) return;

            Vector3 originalScale = button.transform.localScale;
            float targetScale = customScale > 0 ? customScale : hoverScale;

            EventTrigger trigger = button.gameObject.GetComponent<EventTrigger>();
            if (trigger == null) trigger = button.gameObject.AddComponent<EventTrigger>();

            // Pointer Enter
            EventTrigger.Entry enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enterEntry.callback.AddListener((data) => StartCoroutine(ScaleCoroutine(button.transform, originalScale, originalScale * targetScale, hoverDuration)));
            trigger.triggers.Add(enterEntry);

            // Pointer Exit
            EventTrigger.Entry exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exitEntry.callback.AddListener((data) => StartCoroutine(ScaleCoroutine(button.transform, button.transform.localScale, originalScale, hoverDuration)));
            trigger.triggers.Add(exitEntry);
        }

        private IEnumerator ScaleCoroutine(Transform target, Vector3 from, Vector3 to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                target.localScale = Vector3.Lerp(from, to, t);
                yield return null;
            }
            target.localScale = to;
        }
    }
}