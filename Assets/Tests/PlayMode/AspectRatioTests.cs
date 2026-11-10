using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Contract.Core;
using Contract.NPCs;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Contract.Tests
{
    /// <summary>
    /// Phase 12 QA: every visible button of every screen lies fully on screen at the common PC and
    /// phone aspect ratios (16:9, 16:10, 4:3, 20:9, 21:9, 720p), with touch controls on and off.
    /// The overlay canvases are laid out against a camera render target of each size (screen-space
    /// camera follows its target's size), so no real window resize is needed.
    /// </summary>
    public sealed class AspectRatioTests
    {
        private static readonly Vector2Int[] Sizes =
        {
            new Vector2Int(1920, 1080), new Vector2Int(1920, 1200), new Vector2Int(1600, 1200),
            new Vector2Int(2408, 1080), new Vector2Int(2560, 1080), new Vector2Int(1280, 720)
        };

        [TearDown]
        public void TearDown()
        {
            GameManager.Instance?.SetPaused(false);
            GameManager.Instance?.Session.Reset();
        }

        [UnityTest]
        public IEnumerator EveryVisibleButton_IsOnScreen_AtEveryAspectRatio(
            [Values("MainMenu", "CharacterSelection", "WorldSelection", "MissionSelection", "GameWorld", "Myeongdong")] string scene,
            [Values(0, 1)] int touch)
        {
            SceneManager.LoadScene(GameConstants.Scenes.MainMenu);
            yield return null;
            GameSession session = GameManager.Instance.Session;
            session.Reset();
            session.hasStarted = true;
            session.selectedCharacterId = "char_alex_morgan";
            session.Settings.touchControls = touch == 1 ? 1 : 2;
            if (scene != GameConstants.Scenes.MainMenu) SceneManager.LoadScene(scene);
            for (int i = 0; i < 8; i++) yield return null;
            NpcManager.Instance?.DespawnAllCivilians();

            var problems = new List<string>();
            // In the city with touch controls off only the GPS strip is pressable until a menu opens.
            int minimum = GameConstants.Scenes.IsGameplay(scene) && touch == 0 ? 1 : 3;
            foreach (Vector2Int size in Sizes)
            {
                yield return Check(size, problems, "", minimum);
                if (GameConstants.Scenes.IsGameplay(scene))
                {
                    // The pause menu and the settings card too.
                    GameManager.Instance.SetPaused(true);
                    var settings = Object.FindAnyObjectByType<Contract.UI.SettingsController>(FindObjectsInactive.Include);
                    yield return Check(size, problems, "paused");
                    settings.Open(null);
                    yield return Check(size, problems, "settings");
                    settings.Close();
                    GameManager.Instance.SetPaused(false);
                }
            }

            CollectionAssert.IsEmpty(problems, $"{scene}, touch {(touch == 1 ? "on" : "off")}: buttons off screen");
        }

        private static IEnumerator Check(Vector2Int size, List<string> problems, string state = "", int minimum = 3)
        {
            Camera cam = Camera.main;
            var rt = new RenderTexture(size.x, size.y, 16);
            var changed = new List<(Canvas canvas, float distance)>();
            foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!c.isRootCanvas || !c.isActiveAndEnabled || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                changed.Add((c, c.planeDistance));
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = 0.5f + (1000 - c.sortingOrder) * 0.0001f;
            }

            RenderTexture previous = cam.targetTexture;
            cam.targetTexture = rt;
            yield return null; // scalers, safe area and layouts react to the new size
            yield return null;
            Canvas.ForceUpdateCanvases();

            var corners = new Vector3[4];
            int checkedButtons = 0;
            // Everything a finger or the mouse presses: buttons, toggles, sliders, steppers and the touch buttons.
            IEnumerable<Behaviour> pressables = Object.FindObjectsByType<Selectable>(FindObjectsSortMode.None).Where(x => x.interactable).Cast<Behaviour>()
                .Concat(Object.FindObjectsByType<Contract.UI.Touch.TouchButton>(FindObjectsSortMode.None));
            foreach (Behaviour button in pressables)
            {
                if (!button.isActiveAndEnabled) continue;
                var group = button.GetComponentInParent<CanvasGroup>();
                if (group != null && (group.alpha < 0.01f || !group.blocksRaycasts)) continue; // hidden layer
                Canvas canvas = button.GetComponentInParent<Canvas>();
                if (canvas == null || canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceCamera) continue;
                if (button.GetComponentInParent<ScrollRect>() != null || button.GetComponentInParent<Mask>() != null || button.GetComponentInParent<RectMask2D>() != null) continue; // scrolled lists
                checkedButtons++;
                ((RectTransform)button.transform).GetWorldCorners(corners);
                Vector3 min = cam.WorldToViewportPoint(corners[0]), max = cam.WorldToViewportPoint(corners[2]);
                const float tolerance = 0.003f;
                if (min.x < -tolerance || min.y < -tolerance || max.x > 1f + tolerance || max.y > 1f + tolerance)
                    problems.Add($"{size.x}x{size.y} {state} {Path(button.transform)} ({min.x:F2},{min.y:F2})-({max.x:F2},{max.y:F2})");
            }

            if (checkedButtons < minimum) problems.Add($"{size.x}x{size.y} {state}: only {checkedButtons} buttons checked (test not seeing the UI)");
            cam.targetTexture = previous;
            foreach ((Canvas canvas, float distance) in changed)
            {
                if (canvas == null) continue;
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.planeDistance = distance;
            }

            rt.Release();
            Object.Destroy(rt);
            yield return null;
        }

        private static string Path(Transform t)
        {
            var names = new List<string>();
            for (; t != null && names.Count < 4; t = t.parent) names.Add(t.name);
            names.Reverse();
            return string.Join("/", names);
        }
    }
}
