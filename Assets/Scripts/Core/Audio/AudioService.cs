using System.Collections;
using System.Collections.Generic;
using Contract.Contracts;
using Contract.Core;
using Contract.Data;
using UnityEngine;
using UnityEngine.Audio;

namespace Contract.Audio
{
    /// <summary>
    /// Persistent audio playback: music (crossfaded), ambience (crossfaded), 2D effects / UI and a
    /// pool of 3D one-shots. Every source is routed to its ContractMixer group, so the volume
    /// settings act on the mixer (Master / Music / SFX / Ambience / Dialogue). Fades use unscaled
    /// time so they keep working while the game is paused.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class AudioService : MonoBehaviour, IAudioService
    {
        public static AudioService Instance { get; private set; }

        [SerializeField] private AudioLibrary audioLibrary;
        [SerializeField] private AudioMixer audioMixer;
        [SerializeField, Range(4, 48)] private int pooled3DSources = 24;
        [SerializeField] private float musicLevel = 0.55f;
        [SerializeField] private float ambienceLevel = 0.8f;

        private AudioSource musicA, musicB, ambienceA, ambienceB, sfxSource, dialogueSource;
        private readonly Queue<AudioSource> pool = new Queue<AudioSource>();
        private Coroutine musicFade, ambienceFade;
        private float masterVolume = 1f, musicVolume = 0.8f, sfxVolume = 1f, dialogueVolume = 0.9f, ambienceVolume = 1f;
        private bool muted;

        public float MasterVolume => masterVolume;
        public float MusicVolume => musicVolume;
        public float SFXVolume => sfxVolume;
        public float DialogueVolume => dialogueVolume;
        public float AmbienceVolume => ambienceVolume;
        public bool IsMuted => muted;
        public string CurrentMusicId { get; private set; }
        public string CurrentAmbienceId { get; private set; }
        public string LastSfxId { get; private set; }
        public int SfxPlayCount { get; private set; }
        public AudioLibrary Library => audioLibrary;
        public AudioSource ActiveMusicSource { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateInstance()
        {
            if (Instance == null) new GameObject("[AudioService]").AddComponent<AudioService>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                DestroyImmediate(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            GameManager.Instance?.RegisterService<IAudioService>(this);

            GameDatabase database = GameDatabase.Load();
            if (audioLibrary == null && database != null) audioLibrary = database.AudioLibrary;
            if (audioMixer == null && database != null) audioMixer = database.AudioMixer;

            musicA = CreateSource("Music A", "Music", true);
            musicB = CreateSource("Music B", "Music", true);
            ambienceA = CreateSource("Ambience A", "Ambience", true);
            ambienceB = CreateSource("Ambience B", "Ambience", true);
            sfxSource = CreateSource("SFX", "SFX", false);
            dialogueSource = CreateSource("Dialogue", "Dialogue", false);
            ActiveMusicSource = musicA;
            for (int i = 0; i < pooled3DSources; i++) pool.Enqueue(Create3DSource(i));

            GameSettings settings = GameManager.Instance?.Session?.Settings;
            if (settings != null)
            {
                masterVolume = settings.masterVolume;
                musicVolume = settings.musicVolume;
                sfxVolume = settings.sfxVolume;
            }

            ApplyMixerVolumes();
        }

        private AudioMixerGroup Group(string name)
        {
            if (audioMixer == null) return null;
            AudioMixerGroup[] groups = audioMixer.FindMatchingGroups(name);
            return groups.Length > 0 ? groups[0] : null;
        }

        private AudioSource CreateSource(string name, string group, bool loop)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true; // menus keep their music while the game is paused
            source.outputAudioMixerGroup = Group(group);
            return source;
        }

        private AudioSource Create3DSource(int index)
        {
            AudioSource source = CreateSource($"SFX 3D {index}", "SFX", false);
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1.5f;
            source.dopplerLevel = 0f;
            source.ignoreListenerPause = false;
            return source;
        }

        // ---- volumes ---------------------------------------------------------------------------

        public bool OutputsToMixer => musicA.outputAudioMixerGroup != null && sfxSource.outputAudioMixerGroup != null && ambienceA.outputAudioMixerGroup != null;

        private void ApplyMixerVolumes()
        {
            if (audioMixer == null) return;
            audioMixer.SetFloat("MasterVolume", ToDecibel(muted ? 0f : masterVolume));
            audioMixer.SetFloat("MusicVolume", ToDecibel(musicVolume));
            audioMixer.SetFloat("SFXVolume", ToDecibel(sfxVolume));
            audioMixer.SetFloat("DialogueVolume", ToDecibel(dialogueVolume));
            audioMixer.SetFloat("AmbienceVolume", ToDecibel(ambienceVolume));
        }

        public static float ToDecibel(float linear) => Mathf.Log10(Mathf.Max(linear, 0.0001f)) * 20f;

        public float MixerDecibels(string parameter) => audioMixer != null && audioMixer.GetFloat(parameter, out float db) ? db : 0f;

        public void SetMasterVolume(float volume) { masterVolume = Mathf.Clamp01(volume); VolumesChanged(); }
        public void SetMusicVolume(float volume) { musicVolume = Mathf.Clamp01(volume); VolumesChanged(); }
        public void SetSFXVolume(float volume) { sfxVolume = Mathf.Clamp01(volume); VolumesChanged(); }
        public void SetDialogueVolume(float volume) { dialogueVolume = Mathf.Clamp01(volume); ApplyMixerVolumes(); }
        public void SetAmbienceVolume(float volume) { ambienceVolume = Mathf.Clamp01(volume); ApplyMixerVolumes(); }

        public void Mute(bool mute)
        {
            muted = mute;
            ApplyMixerVolumes();
        }

        private void VolumesChanged()
        {
            ApplyMixerVolumes();
            GameEvents.RaiseAudioVolumeChanged(masterVolume, musicVolume, sfxVolume);
        }

        // ---- music -------------------------------------------------------------------------------

        public void PlayMusic(string musicId, bool loop = true) => CrossfadeMusic(musicId, 1.5f);

        public void CrossfadeMusic(string newMusicId, float fadeDuration = 2f)
        {
            if (newMusicId == CurrentMusicId || audioLibrary == null) return;
            AudioClip clip = audioLibrary.FindMusic(newMusicId);
            if (clip == null) return;
            CurrentMusicId = newMusicId;
            AudioSource next = ActiveMusicSource == musicA ? musicB : musicA;
            if (musicFade != null) StopCoroutine(musicFade);
            musicFade = StartCoroutine(Crossfade(ActiveMusicSource, next, clip, musicLevel, fadeDuration));
            ActiveMusicSource = next;
        }

        public void StopMusic()
        {
            CurrentMusicId = null;
            if (musicFade != null) StopCoroutine(musicFade);
            musicFade = StartCoroutine(Crossfade(ActiveMusicSource, null, null, 0f, 1f));
        }

        // ---- ambience ----------------------------------------------------------------------------

        public void PlayAmbience(string ambienceId, bool loop = true) => CrossfadeAmbience(ambienceId, 2f);

        public void CrossfadeAmbience(string newAmbienceId, float fadeDuration = 3f)
        {
            if (newAmbienceId == CurrentAmbienceId || audioLibrary == null) return;
            AudioClip clip = audioLibrary.FindAmbience(newAmbienceId);
            if (clip == null) return;
            CurrentAmbienceId = newAmbienceId;
            AudioSource current = ambienceA.isPlaying && ambienceA.volume > 0f ? ambienceA : ambienceB;
            AudioSource next = current == ambienceA ? ambienceB : ambienceA;
            if (ambienceFade != null) StopCoroutine(ambienceFade);
            ambienceFade = StartCoroutine(Crossfade(current, next, clip, ambienceLevel, fadeDuration));
        }

        public void StopAmbience()
        {
            CurrentAmbienceId = null;
            if (ambienceFade != null) StopCoroutine(ambienceFade);
            AudioSource current = ambienceA.isPlaying ? ambienceA : ambienceB;
            ambienceFade = StartCoroutine(Crossfade(current, null, null, 0f, 1f));
        }

        private static IEnumerator Crossfade(AudioSource from, AudioSource to, AudioClip clip, float level, float duration)
        {
            float fromStart = from != null ? from.volume : 0f;
            if (to != null)
            {
                to.clip = clip;
                to.volume = 0f;
                to.Play();
            }

            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                if (from != null && from != to) from.volume = Mathf.Lerp(fromStart, 0f, k);
                if (to != null) to.volume = Mathf.Lerp(0f, level, k);
                yield return null;
            }

            if (from != null && from != to)
            {
                from.volume = 0f;
                from.Stop();
            }

            if (to != null) to.volume = level;
        }

        // ---- effects -----------------------------------------------------------------------------

        public void PlaySFX(string sfxId, Vector3 position = default, float pitch = 1f, float volume = 1f)
        {
            AudioEntry entry = audioLibrary != null ? audioLibrary.FindSFXEntry(sfxId) : null;
            if (entry == null) return;
            Track(sfxId);
            sfxSource.pitch = pitch * entry.pitch;
            sfxSource.PlayOneShot(entry.clip, volume * entry.volume);
        }

        public void PlayUI(string uiId) => PlaySFX(uiId);

        public void PlaySFX3D(string sfxId, Vector3 position, float maxDistance = 20f, float pitch = 1f, float volume = 1f)
        {
            AudioEntry entry = audioLibrary != null ? audioLibrary.FindSFXEntry(sfxId) : null;
            if (entry == null || pool.Count == 0) return;
            Track(sfxId);
            AudioSource source = pool.Dequeue();
            source.transform.position = position;
            source.clip = entry.clip;
            source.pitch = pitch * entry.pitch;
            source.volume = volume * entry.volume;
            source.maxDistance = maxDistance;
            source.Play();
            StartCoroutine(ReturnToPool(source, entry.clip.length / Mathf.Max(0.1f, Mathf.Abs(source.pitch))));
        }

        private void Track(string id)
        {
            LastSfxId = id;
            SfxPlayCount++;
        }

        private IEnumerator ReturnToPool(AudioSource source, float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds + 0.05f);
            source.Stop();
            source.clip = null;
            pool.Enqueue(source);
        }
    }
}
