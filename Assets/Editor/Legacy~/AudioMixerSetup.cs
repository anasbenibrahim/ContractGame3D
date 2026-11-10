using System;
using Contract.Audio;
using UnityEngine;
using UnityEditor;
using UnityEngine.Audio;
using Object = UnityEngine.Object;

namespace Contract.EditorTools
{
    /// <summary>
    /// Creates and configures the Audio Mixer for the game.
    /// Run: CONTRACT/Setup/Create Audio Mixer
    /// </summary>
    public static class AudioMixerSetup
    {
        private const string MixerPath = "Assets/Audio/ContractMixer.mixer";

        [MenuItem("CONTRACT/Setup/Create Audio Mixer")]
        public static void CreateAudioMixer()
        {
            // Create Audio folder if needed
            if (!AssetDatabase.IsValidFolder("Assets/Audio"))
            {
                AssetDatabase.CreateFolder("Assets", "Audio");
            }

            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (mixer == null)
            {
                Debug.LogWarning(
                    "[CONTRACT] Audio Mixer must be created manually: Assets > Create > Audio Mixer, " +
                    "save it as Assets/Audio/ContractMixer.mixer, then add Master > Music, SFX, Dialogue " +
                    "and Ambience groups and expose the volume parameters.");
                return;
            }

            Debug.Log("[CONTRACT] Audio Mixer already exists at " + MixerPath);
        }

        [MenuItem("CONTRACT/Setup/Setup Audio Sources")]
        public static void SetupAudioSources()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/GameWorld.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);

            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (mixer == null)
            {
                Debug.LogError("[CONTRACT] Audio Mixer not found. Run 'Create Audio Mixer' first.");
                return;
            }

            // Find or create AudioService
            AudioService audioService = Object.FindAnyObjectByType<AudioService>();
            if (audioService == null)
            {
                GameObject go = new GameObject("[AudioService]");
                audioService = go.AddComponent<AudioService>();
                GameObject.DontDestroyOnLoad(go);
            }

            // Create audio sources
            CreateAudioSource(audioService, "MusicSource", mixer, "Music", true, false);
            CreateAudioSource(audioService, "SFXSource", mixer, "SFX", false, false);
            CreateAudioSource(audioService, "DialogueSource", mixer, "Dialogue", false, false);
            CreateAudioSource(audioService, "AmbienceSource", mixer, "Ambience", true, false);

            // Assign to AudioService via SerializedObject
            SerializedObject so = new SerializedObject(audioService);
            so.FindProperty("musicSource").objectReferenceValue = FindSource("MusicSource");
            so.FindProperty("sfxSource").objectReferenceValue = FindSource("SFXSource");
            so.FindProperty("dialogueSource").objectReferenceValue = FindSource("DialogueSource");
            so.FindProperty("ambienceSource").objectReferenceValue = FindSource("AmbienceSource");
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(audioService);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            Debug.Log("[CONTRACT] Audio Sources configured and assigned to AudioService.");
        }

        private static AudioSource CreateAudioSource(AudioService service, string name, AudioMixer mixer, string groupName, bool loop, bool playOnAwake)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(service.transform, false);

            AudioSource source = go.AddComponent<AudioSource>();
            source.loop = loop;
            source.playOnAwake = playOnAwake;
            source.spatialize = false;
            source.rolloffMode = AudioRolloffMode.Linear;

            AudioMixerGroup group = mixer.FindMatchingGroups(groupName)[0];
            source.outputAudioMixerGroup = group;

            return source;
        }

        private static AudioSource FindSource(string name)
        {
            foreach (AudioSource source in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include))
            {
                if (source.name == name) return source;
            }
            return null;
        }
    }
}