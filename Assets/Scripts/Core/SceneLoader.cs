using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Contract.Core
{
    public sealed class SceneLoader : MonoBehaviour
    {
        public event Action<string> LoadStarted;
        public event Action<string> LoadCompleted;

        public bool IsLoading { get; private set; }

        /// <summary>What a loading screen shows for one load (destination name, preview, tip).</summary>
        public sealed class LoadingInfo
        {
            public string title;
            public string subtitle;
            public string tip;
            public Texture preview;
        }

        private LoadingInfo pendingInfo;
        private GameObject loadingScreen;
        private UnityEngine.UI.Image loadingFill;

        /// <summary>The next load shows this loading screen (used when travelling to a district).</summary>
        public void SetLoadingInfo(LoadingInfo info) => pendingInfo = info;

        public bool LoadingScreenVisible => loadingScreen != null && loadingScreen.activeSelf;
        public string LoadingTitle { get; private set; }

        private const float FadeOut = 0.25f, FadeIn = 0.35f;
        private CanvasGroup fader;

        /// <summary>Full-screen black overlay that survives scene loads (cinematic transitions).</summary>
        private CanvasGroup Fader
        {
            get
            {
                if (fader != null) return fader;
                GameObject go = new GameObject("ScreenFader", typeof(Canvas), typeof(CanvasGroup), typeof(UnityEngine.UI.Image));
                go.transform.SetParent(transform, false);
                Canvas canvas = go.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 1000;
                go.GetComponent<UnityEngine.UI.Image>().color = Color.black;
                fader = go.GetComponent<CanvasGroup>();
                fader.alpha = 0f;
                fader.blocksRaycasts = false;
                return fader;
            }
        }

        private IEnumerator Fade(float to, float duration)
        {
            CanvasGroup group = Fader;
            float from = group.alpha;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }

            group.alpha = to;
            group.blocksRaycasts = to > 0.5f;
        }

        private void ShowLoadingScreen(LoadingInfo info)
        {
            if (loadingScreen == null) BuildLoadingScreen();
            LoadingTitle = info.title;
            loadingScreen.transform.Find("Preview").GetComponent<UnityEngine.UI.RawImage>().texture = info.preview;
            loadingScreen.transform.Find("Preview").gameObject.SetActive(info.preview != null);
            loadingScreen.transform.Find("Title").GetComponent<TMPro.TMP_Text>().text = info.title;
            loadingScreen.transform.Find("Subtitle").GetComponent<TMPro.TMP_Text>().text = info.subtitle ?? "";
            loadingScreen.transform.Find("Tip").GetComponent<TMPro.TMP_Text>().text = info.tip ?? "";
            loadingFill.fillAmount = 0f;
            loadingScreen.SetActive(true);
        }

        /// <summary>Full-screen loading card above the fader: preview, destination name, tip, progress.</summary>
        private void BuildLoadingScreen()
        {
            loadingScreen = new GameObject("LoadingScreen", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            loadingScreen.transform.SetParent(transform, false);
            var canvas = loadingScreen.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1001;
            var scaler = loadingScreen.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand; // never smaller than 1920x1080: no overflow on 20:9 phones or 16:10 screens

            RectTransform Node(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
            {
                var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
                rt.SetParent(loadingScreen.transform, false);
                rt.anchorMin = anchorMin;
                rt.anchorMax = anchorMax;
                rt.pivot = new Vector2(0f, 0f);
                rt.anchoredPosition = position;
                rt.sizeDelta = size;
                return rt;
            }

            Node("Black", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<UnityEngine.UI.Image>().color = Color.black;
            var preview = Node("Preview", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<UnityEngine.UI.RawImage>();
            preview.color = new Color(0.55f, 0.55f, 0.6f, 1f); // dimmed so the text reads
            Node("Shade", Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 420f)).gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.75f);

            TMPro.TextMeshProUGUI Label(string name, float size, Vector2 position, Color color)
            {
                var t = Node(name, Vector2.zero, Vector2.zero, position, new Vector2(1700f, size * 1.6f)).gameObject.AddComponent<TMPro.TextMeshProUGUI>();
                t.fontSize = size;
                t.color = color;
                t.textWrappingMode = TMPro.TextWrappingModes.Normal;
                return t;
            }

            Label("Title", 72f, new Vector2(96f, 250f), Color.white).fontStyle = TMPro.FontStyles.Bold;
            Label("Subtitle", 28f, new Vector2(100f, 205f), new Color(1f, 0.3f, 0.35f));
            Label("Tip", 22f, new Vector2(100f, 110f), new Color(0.8f, 0.83f, 0.88f));
            RectTransform bar = Node("Bar", Vector2.zero, new Vector2(1f, 0f), new Vector2(100f, 60f), new Vector2(-200f, 8f));
            bar.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(0.2f, 0.21f, 0.25f);
            var fill = (RectTransform)new GameObject("Fill", typeof(RectTransform)).transform;
            fill.SetParent(bar, false);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            loadingFill = fill.gameObject.AddComponent<UnityEngine.UI.Image>();
            loadingFill.color = new Color(0.9f, 0.16f, 0.22f);
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            loadingFill.sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero);
            loadingFill.type = UnityEngine.UI.Image.Type.Filled;
            loadingFill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            loadingScreen.SetActive(false);
        }

        public void LoadMainMenu() => LoadScene(GameConstants.Scenes.MainMenu);
        public void LoadCharacterSelection() => LoadScene(GameConstants.Scenes.CharacterSelection);
        public void LoadMissionSelection() => LoadScene(GameConstants.Scenes.MissionSelection);
        public void LoadGameWorld() => LoadScene(GameConstants.Scenes.GameWorld);
        public void LoadWorldSelection() => LoadScene(GameConstants.Scenes.WorldSelection);

        /// <summary>The district scene a region (Gangnam, Myeongdong ...) is played in.</summary>
        public void LoadDistrict(string regionId) => LoadScene(GameConstants.Scenes.ForRegion(regionId));

        public void LoadScene(string sceneName)
        {
            if (IsLoading)
            {
                Debug.LogWarning($"[SceneLoader] Ignored load request for '{sceneName}' because a scene is already loading.");
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[SceneLoader] Scene '{sceneName}' is not present in the build settings.");
                return;
            }

            StartCoroutine(LoadSceneRoutine(sceneName));
        }

        private IEnumerator LoadSceneRoutine(string sceneName)
        {
            IsLoading = true;
            GameEvents.RaiseSceneLoadStarted(sceneName);
            LoadStarted?.Invoke(sceneName);

            yield return Fade(1f, FadeOut);
            Time.timeScale = 1f;
            LoadingInfo info = pendingInfo;
            pendingInfo = null;
            float shownAt = Time.realtimeSinceStartup;
            if (info != null) ShowLoadingScreen(info);
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (operation == null)
            {
                Debug.LogError($"[SceneLoader] Unity could not begin loading '{sceneName}'.");
                IsLoading = false;
                yield return Fade(0f, FadeIn);
                yield break;
            }

            operation.allowSceneActivation = true;
            while (!operation.isDone)
            {
                if (loadingFill != null) loadingFill.fillAmount = Mathf.Clamp01(operation.progress / 0.9f);
                yield return null;
            }

            if (info != null)
            {
                if (loadingFill != null) loadingFill.fillAmount = 1f;
                while (Time.realtimeSinceStartup - shownAt < 1.2f) yield return null; // long enough to read
                loadingScreen.SetActive(false);
            }

            IsLoading = false;
            GameEvents.RaiseSceneLoadCompleted(sceneName);
            LoadCompleted?.Invoke(sceneName);
            yield return Fade(0f, FadeIn);
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
