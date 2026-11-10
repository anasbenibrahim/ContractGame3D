using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Contract.Audio;
using Contract.Contracts;
using Contract.Core;
using Contract.Data;
using Contract.NPCs;
using Contract.Player;
using Contract.UI;
using Contract.VFX;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Contract.Tests
{
    /// <summary>
    /// Stage 9 verification: generated audio covers every cue the code plays, mixer routing,
    /// scene music / ambience, tension music, event cues, footsteps, UI sounds, readable night
    /// lighting, neon flicker, food-stall steam, wet roads, close-quarters camera and panel
    /// animations, plus a frame-time guard for the added effects.
    /// </summary>
    public sealed class AtmosphereTests
    {
        private Gamepad gamepad;
        private InputSettings.BackgroundBehavior savedBackground;

        private static AudioService Audio => AudioService.Instance;
        private static GameSession Session => GameManager.Instance.Session;

        [SetUp]
        public void Setup()
        {
            savedBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            gamepad = InputSystem.AddDevice<Gamepad>("AtmosphereTestGamepad");
            GameManager.Instance.SetPaused(false);
            Session.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            if (gamepad != null) InputSystem.RemoveDevice(gamepad);
            InputSystem.settings.backgroundBehavior = savedBackground;
            GameManager.Instance.SetPaused(false);
            Audio.SetMasterVolume(1f);
            Audio.SetMusicVolume(0.8f);
            Session.Reset();
        }

        private static IEnumerator Load(string scene, int frames = 6)
        {
            Session.selectedCharacterId = "char_alex_morgan";
            SceneManager.LoadScene(scene, LoadSceneMode.Single);
            for (int i = 0; i < frames; i++) yield return null;
            NpcManager.Instance?.DespawnAllCivilians();
        }

        // ---- audio content -------------------------------------------------------------------

        [Test]
        public void EveryCueTheCodePlays_ExistsInTheAudioLibrary()
        {
            var cue = new Regex("(?:PlaySFX3D|PlaySFX|PlayUI|Sound|Play|CrossfadeMusic|CrossfadeAmbience)\\(\"([a-z0-9_]+)\"");
            string[] files = Directory.GetFiles(Path.Combine(Application.dataPath, "Scripts"), "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.EndsWith("PlayerAnimationDriver.cs")) // legacy Animator-event path, unused with the procedural rig
                .ToArray();
            // Ids picked with a ternary (success ? "contract_complete" : "contract_failed") in the audio scripts.
            var choice = new Regex("[?:]\\s*\"([a-z]+_[a-z0-9_]+)\"");
            string[] audioScripts = files.Where(f => f.EndsWith("GameAudioDirector.cs") || f.EndsWith("FootstepAudio.cs") || f.EndsWith("UIButtonFX.cs")).ToArray();
            Assert.AreEqual(3, audioScripts.Length, "audio scripts moved?");
            string[] ids = files.SelectMany(f => cue.Matches(File.ReadAllText(f)).Cast<Match>().Select(m => m.Groups[1].Value))
                .Concat(audioScripts.SelectMany(f => choice.Matches(File.ReadAllText(f)).Cast<Match>().Select(m => m.Groups[1].Value)))
                .Distinct().ToArray();
            Assert.Greater(ids.Length, 20, "cue scan found too little; regex broken?");
            CollectionAssert.Contains(ids, "contract_failed", "ternary cues not scanned");

            AudioLibrary library = GameDatabase.Load().AudioLibrary;
            string[] missing = ids.Where(id => library.FindMusic(id) == null && library.FindAmbience(id) == null && library.FindSFXEntry(id) == null).ToArray();
            CollectionAssert.IsEmpty(missing, "cues without a clip");
        }

        [Test]
        public void GeneratedClips_AreLoadedAndLoopsAreLong()
        {
            AudioLibrary library = GameDatabase.Load().AudioLibrary;
            Assert.AreEqual(3, library.musicTracks.Length);
            Assert.AreEqual(2, library.ambienceEntries.Length);
            foreach (AudioEntry e in library.musicTracks) Assert.Greater(e.clip.length, 30f, e.id);
            foreach (AudioEntry e in library.ambienceEntries) Assert.Greater(e.clip.length, 15f, e.id);
            foreach (AudioEntry e in library.sfxEntries)
            {
                Assert.IsNotNull(e.clip, e.id);
                Assert.Greater(e.clip.length, 0.02f, e.id);
                Assert.Less(e.clip.length, 5f, e.id);
            }

            Assert.AreEqual(4, library.sfxEntries.Count(e => e.id.StartsWith("footstep_")), "footstep variants");
        }

        [Test]
        public void VolumeSettings_DriveTheMixer()
        {
            Assert.IsTrue(Audio.OutputsToMixer, "audio sources are not routed to the mixer");
            Audio.SetMusicVolume(0.5f);
            Assert.AreEqual(-6.02f, Audio.MixerDecibels("MusicVolume"), 0.05f);
            Session.Settings.masterVolume = 0.25f;
            SettingsController.Apply(Session.Settings); // what the settings sliders call
            Assert.AreEqual(-12.04f, Audio.MixerDecibels("MasterVolume"), 0.05f);
        }

        // ---- music direction ---------------------------------------------------------------------

        [UnityTest]
        public IEnumerator EachScene_HasItsMusicAndAmbience()
        {
            yield return Load(GameConstants.Scenes.MainMenu, 3);
            Assert.AreEqual("music_menu", Audio.CurrentMusicId);
            Assert.AreEqual("amb_city_night", Audio.CurrentAmbienceId);
            yield return Load(GameConstants.Scenes.CharacterSelection, 3);
            Assert.AreEqual("music_menu", Audio.CurrentMusicId);
            Assert.AreEqual("amb_interior", Audio.CurrentAmbienceId);
            yield return Load(GameConstants.Scenes.GameWorld);
            Assert.AreEqual("music_city", Audio.CurrentMusicId);
            Assert.AreEqual("amb_city_night", Audio.CurrentAmbienceId);
            yield return new WaitForSecondsRealtime(2.2f);
            Assert.IsTrue(Audio.ActiveMusicSource.isPlaying, "music source not playing");
            Assert.Greater(Audio.ActiveMusicSource.volume, 0.3f, "music did not fade in");
        }

        [UnityTest]
        public IEnumerator SuspiciousGuards_BringInTensionMusic_ThenItCalmsDown()
        {
            yield return Load(GameConstants.Scenes.GameWorld);
            GameAudioDirector director = GameAudioDirector.Instance;
            Assert.IsFalse(director.Tense);
            GameEvents.RaiseSuspicionChanged("test_guard", 0.5f);
            yield return null;
            Assert.IsTrue(director.Tense);
            Assert.AreEqual("music_tension", Audio.CurrentMusicId);
            Assert.AreEqual("suspicion", Audio.LastSfxId, "suspicion cue");
            GameEvents.RaiseNpcAlerted("test_guard");
            Assert.AreEqual("alert", Audio.LastSfxId);

            GameEvents.RaiseSuspicionChanged("test_guard", 0f);
            yield return new WaitForSecondsRealtime(2f);
            Assert.IsTrue(director.Tense, "calmed down too quickly");
            yield return new WaitForSecondsRealtime(2.5f);
            Assert.IsFalse(director.Tense);
            Assert.AreEqual("music_city", Audio.CurrentMusicId);
        }

        [UnityTest]
        public IEnumerator GameEvents_PlayTheirCues()
        {
            yield return Load(GameConstants.Scenes.GameWorld);
            (System.Action raise, string expected)[] cases =
            {
                (() => GameEvents.RaiseObjectiveCompleted("obj"), "objective_complete"),
                (() => GameEvents.RaiseItemCollected("item"), "pickup"),
                (() => GameEvents.RaiseInteractionPerformed("photo:evidence"), "photo_shutter"),
                (() => GameEvents.RaiseInteractionPerformed("terminal"), "hold_complete"),
                (() => GameEvents.RaiseUpgradePurchased("upgrade_move_speed_1"), "purchase"),
                (() => GameEvents.RaiseMissionResultReady(new MissionResult { success = true }), "contract_complete"),
                (() => GameEvents.RaiseMissionResultReady(new MissionResult { success = false }), "contract_failed"),
            };
            foreach ((System.Action raise, string expected) in cases)
            {
                int before = Audio.SfxPlayCount;
                raise();
                Assert.AreEqual(expected, Audio.LastSfxId);
                Assert.AreEqual(before + 1, Audio.SfxPlayCount, expected);
            }

            GameEvents.RaiseMenuBack(); // close the result screen opened above
            GameEvents.RaiseMenuBack();
            GameManager.Instance.SetPaused(true);
            Assert.IsTrue(AudioListener.pause, "world sounds keep playing while paused");
            GameManager.Instance.SetPaused(false);
            Assert.IsFalse(AudioListener.pause);
        }

        [UnityTest]
        public IEnumerator Walking_PlaysFootsteps_StandingDoesNot()
        {
            yield return Load(GameConstants.Scenes.GameWorld);
            FootstepAudio steps = GameObject.FindGameObjectWithTag("Player").GetComponent<FootstepAudio>();
            Assert.IsNotNull(steps, "player has no footsteps");
            yield return new WaitForSeconds(0.5f);
            int idle = steps.StepCount;
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(idle, steps.StepCount, "footsteps while standing still");

            InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = new Vector2(0f, 1f) });
            yield return new WaitForSeconds(1f);
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return null;
            Assert.GreaterOrEqual(steps.StepCount - idle, 3, "too few steps for a second of walking");
            Assert.LessOrEqual(steps.StepCount - idle, 8, "too many steps for a second of walking");
        }

        [UnityTest]
        public IEnumerator Buttons_ClickAndHoverSounds()
        {
            yield return Load(GameConstants.Scenes.MainMenu, 3);
            Button settings = Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude).First(b => b.name == "Settings");
            var pointer = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(settings.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            Assert.AreEqual("ui_hover", Audio.LastSfxId);
            ExecuteEvents.Execute(settings.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Assert.AreEqual("ui_click", Audio.LastSfxId);
            Assert.IsTrue(Object.FindAnyObjectByType<SettingsController>(FindObjectsInactive.Include).IsOpen, "the click also performed the action");
        }

        // ---- visuals ---------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator City_IsReadableAtNight()
        {
            yield return Load(GameConstants.Scenes.GameWorld);
            yield return new WaitForSecondsRealtime(1f);
            // The editor compiles shaders asynchronously (placeholders render black/cyan on the first
            // run after an import or platform switch): measure until two readings agree, up to 8 s.
            float luminance = MeanLuminance(Camera.main), previous = -1f;
            for (float waited = 0f; waited < 8f && Mathf.Abs(luminance - previous) > 0.005f; waited += 0.5f)
            {
                yield return new WaitForSecondsRealtime(0.5f);
                previous = luminance;
                luminance = MeanLuminance(Camera.main);
            }
            Debug.Log($"[AtmosphereTests] Mean scene luminance: {luminance:F3}");
            Assert.Greater(luminance, 0.10f, "the street view is too dark to play");
            Assert.Less(luminance, 0.6f, "night scene washed out");
        }

        private static float MeanLuminance(Camera camera)
        {
            var rt = new RenderTexture(480, 270, 24);
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            var texture = new Texture2D(480, 270, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 480, 270), 0, 0);
            camera.targetTexture = null;
            RenderTexture.active = null;
            float sum = texture.GetPixels().Sum(c => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b);
            float mean = sum / (480 * 270);
            Object.Destroy(texture);
            rt.Release();
            return mean;
        }

        [UnityTest]
        public IEnumerator Street_HasFlickeringNeon_Steam_AndWetRoads()
        {
            yield return Load(GameConstants.Scenes.GameWorld);
            NeonFlicker[] neon = Object.FindObjectsByType<NeonFlicker>();
            Assert.GreaterOrEqual(neon.Length, 8, "neon flicker");
            ParticleSystem[] steam = Object.FindObjectsByType<ParticleSystem>().Where(p => p.name == "Steam").ToArray();
            Assert.Greater(steam.Length, 0, "no food-stall steam");
            Assert.IsTrue(steam.All(p => p.isPlaying));
            Material asphalt = Object.FindObjectsByType<MeshRenderer>().Select(r => r.sharedMaterial).First(m => m != null && m.name.StartsWith("M_Asphalt"));
            Assert.GreaterOrEqual(asphalt.GetFloat("_Smoothness"), 0.7f, "road is not wet-looking");

            // One sign with a short calm period: it must flicker and end lit.
            GameObject sign = GameObject.CreatePrimitive(PrimitiveType.Quad);
            sign.SetActive(false);
            NeonFlicker flicker = sign.AddComponent<NeonFlicker>();
            flicker.Configure(7, new Vector2(0.05f, 0.1f));
            sign.SetActive(true);
            yield return new WaitForSeconds(1.5f);
            Assert.Greater(flicker.Bursts, 0, "never flickered");
            Object.Destroy(sign);
        }

        [UnityTest]
        public IEnumerator Camera_HidesTheOperative_WhenAWallPushesItClose()
        {
            yield return Load(GameConstants.Scenes.GameWorld);
            Transform player = GameObject.FindGameObjectWithTag("Player").transform;
            Contract.CameraSystem.ThirdPersonCamera cam = Object.FindAnyObjectByType<Contract.CameraSystem.ThirdPersonCamera>();
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.IsFalse(cam.PlayerHidden, "hidden in the open");

            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = player.position + Vector3.up * 1.5f - cam.transform.forward.normalized * 0.9f;
            wall.transform.rotation = Quaternion.LookRotation(cam.transform.forward);
            wall.transform.localScale = new Vector3(5f, 4f, 0.2f);
            Physics.SyncTransforms();
            yield return new WaitForSecondsRealtime(1f);
            Assert.IsTrue(cam.PlayerHidden, "camera pressed against the operative but the body is still drawn");
            Renderer body = player.GetComponentsInChildren<Renderer>().First(r => !(r is ParticleSystemRenderer));
            Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly, body.shadowCastingMode, "shadow kept, body hidden");

            Object.Destroy(wall);
            yield return new WaitForSecondsRealtime(1f);
            Assert.IsFalse(cam.PlayerHidden);
            Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.On, body.shadowCastingMode);
        }

        [UnityTest]
        public IEnumerator Panels_AnimateOpen()
        {
            yield return Load(GameConstants.Scenes.GameWorld);
            PauseMenuController pause = Object.FindAnyObjectByType<PauseMenuController>();
            pause.OnPause();
            UIPanelTransition card = Field<GameObject>(pause, "pausePanel").GetComponentInChildren<UIPanelTransition>();
            Assert.IsNotNull(card, "pause card has no opening animation");
            yield return null;
            Assert.Less(card.GetComponent<CanvasGroup>().alpha, 1f, "no fade-in");
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.IsTrue(card.Finished);
            Assert.AreEqual(1f, card.GetComponent<CanvasGroup>().alpha, 0.001f);
            Assert.AreEqual(1f, card.transform.localScale.x, 0.001f);
            pause.OnResume();
        }

        private static T Field<T>(object target, string name) =>
            (T)target.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(target);

        [UnityTest]
        public IEnumerator FrameTime_StaysPlayable_WithTheCrowdAndEffects()
        {
            Session.selectedCharacterId = "char_alex_morgan";
            SceneManager.LoadScene(GameConstants.Scenes.GameWorld, LoadSceneMode.Single);
            yield return new WaitForSecondsRealtime(2f); // crowd spawns, shaders warm up
            var frames = new System.Collections.Generic.List<float>();
            for (float t = 0f; t < 3f; t += Time.unscaledDeltaTime)
            {
                yield return null;
                frames.Add(Time.unscaledDeltaTime * 1000f);
            }

            frames.Sort();
            float median = frames[frames.Count / 2];
            Debug.Log($"[AtmosphereTests] Frame time median {median:F1} ms, p95 {frames[(int)(frames.Count * 0.95f)]:F1} ms over {frames.Count} frames");
            Assert.Less(median, 33f, "median frame time above 30 fps in the editor");
        }
    }
}
