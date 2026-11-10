using System;
using System.Collections.Generic;
using UnityEngine;

namespace Contract.Data
{
    [CreateAssetMenu(menuName = "CONTRACT/Audio Library", fileName = "AudioLibrary")]
    public sealed class AudioLibrary : ScriptableObject
    {
        [Header("Music")]
        [field: SerializeField] public AudioEntry[] musicTracks { get; set; }

        [Header("Sound Effects")]
        [field: SerializeField] public AudioEntry[] sfxEntries { get; set; }

        [Header("Ambience")]
        [field: SerializeField] public AudioEntry[] ambienceEntries { get; set; }

        [Header("Defaults")]
        [field: SerializeField] public AudioEntry defaultMusic { get; set; }
        [field: SerializeField] public AudioEntry defaultAmbience { get; set; }

        public AudioClip FindMusic(string id) => Find(musicTracks, id)?.clip;

        public AudioClip FindSFX(string id) => FindSFXEntry(id)?.clip;

        public AudioClip FindAmbience(string id) => Find(ambienceEntries, id)?.clip;

        /// <summary>
        /// Exact id, or a random variant: "footstep" picks one of "footstep_1", "footstep_2", ...
        /// so repeated sounds never play the identical sample twice in a row as often.
        /// </summary>
        public AudioEntry FindSFXEntry(string id)
        {
            AudioEntry exact = Find(sfxEntries, id);
            if (exact != null || sfxEntries == null || string.IsNullOrEmpty(id)) return exact;
            string prefix = id + "_";
            int count = 0;
            foreach (AudioEntry e in sfxEntries) if (e != null && e.clip != null && e.id.StartsWith(prefix)) count++;
            if (count == 0) return null;
            int pick = UnityEngine.Random.Range(0, count);
            foreach (AudioEntry e in sfxEntries)
            {
                if (e == null || e.clip == null || !e.id.StartsWith(prefix)) continue;
                if (pick-- == 0) return e;
            }

            return null;
        }

        private static AudioEntry Find(AudioEntry[] entries, string id)
        {
            if (entries == null || string.IsNullOrEmpty(id)) return null;
            foreach (AudioEntry entry in entries)
            {
                if (entry != null && entry.id == id && entry.clip != null) return entry;
            }

            return null;
        }
    }

    [System.Serializable]
    public sealed class AudioEntry
    {
        public string id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        [Range(-3f, 3f)] public float pitch = 1f;
    }
}