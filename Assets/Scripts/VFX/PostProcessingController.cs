using System;
using Contract.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Contract.VFX
{
    /// <summary>
    /// Handles post-processing effects: screen flash, vignette, chromatic aberration.
    /// </summary>
    public sealed class PostProcessingController : MonoBehaviour
    {
        public static PostProcessingController Instance { get; private set; }

        [Header("Volume")]
        [SerializeField] private Volume globalVolume;
        [SerializeField] private VolumeProfile profile;

        public Volume GlobalVolume { get => globalVolume; set => globalVolume = value; }
        public VolumeProfile Profile { get => profile; set => profile = value; }

        [Header("Effect Intensity")]
        [SerializeField] private float maxVignetteIntensity = 0.5f;
        [SerializeField] private float maxChromaticAberration = 1f;

        private Vignette vignette;
        private ChromaticAberration chromaticAberration;
        private ColorAdjustments colorAdjustments;

        private Coroutine flashCoroutine;
        private Coroutine vignetteCoroutine;
        private Coroutine chromaticAberrationCoroutine;

        private float baseVignetteIntensity;
        private float baseChromaticAberration;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                DestroyImmediate(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            SetupVolume();
        }

        private void Start()
        {
            SubscribeToEvents();
            CacheBaseValues();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        private void SetupVolume()
        {
            if (globalVolume == null)
            {
                globalVolume = GetComponent<Volume>();
            }

            if (globalVolume == null)
            {
                globalVolume = gameObject.AddComponent<Volume>();
                globalVolume.isGlobal = true;
                globalVolume.priority = 100;
            }

            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                globalVolume.profile = profile;
            }

            // Get or add effects
            if (!profile.TryGet(out vignette)) vignette = profile.Add<Vignette>(true);
            if (!profile.TryGet(out chromaticAberration)) chromaticAberration = profile.Add<ChromaticAberration>(true);
            if (!profile.TryGet(out colorAdjustments)) colorAdjustments = profile.Add<ColorAdjustments>(true);

            // Enable effects
            vignette.active = true;
            chromaticAberration.active = true;
            colorAdjustments.active = true;
        }

        private void CacheBaseValues()
        {
            baseVignetteIntensity = vignette.intensity.value;
            baseChromaticAberration = chromaticAberration.intensity.value;
        }

        private void SubscribeToEvents()
        {
            GameEvents.ScreenFlash += OnScreenFlash;
            GameEvents.VignetteChange += OnVignetteChange;
            GameEvents.ChromaticAberrationChange += OnChromaticAberrationChange;
        }

        private void UnsubscribeFromEvents()
        {
            GameEvents.ScreenFlash -= OnScreenFlash;
            GameEvents.VignetteChange -= OnVignetteChange;
            GameEvents.ChromaticAberrationChange -= OnChromaticAberrationChange;
        }

        private void OnScreenFlash(Color color, float duration)
        {
            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
            }
            flashCoroutine = StartCoroutine(ScreenFlashCoroutine(color, duration));
        }

        private System.Collections.IEnumerator ScreenFlashCoroutine(Color color, float duration)
        {
            // Use ColorAdjustments for flash effect
            float elapsed = 0f;
            Color originalFilter = colorAdjustments.colorFilter.value;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float intensity = Mathf.Sin(t * Mathf.PI) * 0.5f; // Pulse
                colorAdjustments.colorFilter.value = Color.Lerp(originalFilter, color, intensity);
                yield return null;
            }

            colorAdjustments.colorFilter.value = originalFilter;
        }

        private void OnVignetteChange(float targetIntensity, float duration)
        {
            if (vignetteCoroutine != null)
            {
                StopCoroutine(vignetteCoroutine);
            }
            vignetteCoroutine = StartCoroutine(VignetteCoroutine(targetIntensity, duration));
        }

        private System.Collections.IEnumerator VignetteCoroutine(float targetIntensity, float duration)
        {
            float startIntensity = vignette.intensity.value;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                vignette.intensity.value = Mathf.Lerp(startIntensity, targetIntensity, t);
                yield return null;
            }

            vignette.intensity.value = targetIntensity;
        }

        private void OnChromaticAberrationChange(float targetIntensity, float duration)
        {
            if (chromaticAberrationCoroutine != null)
            {
                StopCoroutine(chromaticAberrationCoroutine);
            }
            chromaticAberrationCoroutine = StartCoroutine(ChromaticAberrationCoroutine(targetIntensity, duration));
        }

        private System.Collections.IEnumerator ChromaticAberrationCoroutine(float targetIntensity, float duration)
        {
            float startIntensity = chromaticAberration.intensity.value;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                chromaticAberration.intensity.value = Mathf.Lerp(startIntensity, targetIntensity, t);
                yield return null;
            }

            chromaticAberration.intensity.value = targetIntensity;
        }

        public void ResetToBase()
        {
            if (vignetteCoroutine != null) StopCoroutine(vignetteCoroutine);
            if (chromaticAberrationCoroutine != null) StopCoroutine(chromaticAberrationCoroutine);

            vignette.intensity.value = baseVignetteIntensity;
            chromaticAberration.intensity.value = baseChromaticAberration;
        }
    }
}