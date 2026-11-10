using System;
using System.Collections.Generic;
using System.IO;
using Contract.Data;
using UnityEditor;
using UnityEngine;

namespace Contract.Editor
{
    /// <summary>
    /// Stage 9: renders the game's original audio (no third-party samples): three music loops
    /// (menu, city, tension), two ambience loops (Gangnam night street, interior room tone) and
    /// the sound effects (footsteps, UI, interactions, rewards, alerts). Everything is synthesized
    /// from oscillators and filtered noise with fixed seeds, so the output is reproducible.
    /// Loops wrap their note tails and reverb around the loop point so they repeat seamlessly.
    /// Writes WAVs to Assets/Audio/Generated, sets import settings and fills the AudioLibrary.
    /// Headless: -executeMethod Contract.Editor.AudioSynthBuilder.Build
    /// </summary>
    public static class AudioSynthBuilder
    {
        private const string Folder = "Assets/Audio/Generated";
        private const string LibraryPath = "Assets/ScriptableObjects/AudioLibrary.asset";
        private const int SfxRate = 44100;
        private const int LoopRate = 32000;

        private static readonly List<(string id, string path, float volume)> music = new List<(string, string, float)>();
        private static readonly List<(string id, string path, float volume)> ambience = new List<(string, string, float)>();
        private static readonly List<(string id, string path, float volume)> sfx = new List<(string, string, float)>();

        [MenuItem("CONTRACT/Setup/Build Audio")]
        public static void Build()
        {
            Directory.CreateDirectory(Folder);
            music.Clear();
            ambience.Clear();
            sfx.Clear();

            Music("music_menu", MenuTheme());
            Music("music_city", CityLoFi());
            Music("music_tension", Tension());
            Ambience("amb_city_night", CityNight());
            Ambience("amb_interior", Interior());

            for (int i = 1; i <= 4; i++) Sfx($"footstep_{i}", Footstep(i), 0.7f);
            Sfx("land_impact", Land(), 0.8f);
            Sfx("ui_hover", Blip(1900f, 0.035f, 0.25f), 0.12f);
            Sfx("ui_click", TwoTone(1200f, 1800f, 0.045f), 0.25f);
            Sfx("ui_back", TwoTone(1500f, 900f, 0.05f), 0.25f);
            Sfx("ui_error", Buzz(), 0.25f);
            Sfx("interact", TwoTone(880f, 1320f, 0.06f), 0.3f);
            Sfx("hold_complete", Arp(new[] { 76, 81, 88 }, 0.06f, 0.5f), 0.6f);
            Sfx("pickup", Arp(new[] { 79, 84, 91 }, 0.05f, 0.45f), 0.6f);
            Sfx("photo_shutter", Shutter(), 0.8f);
            Sfx("objective_complete", Arp(new[] { 72, 76, 79 }, 0.11f, 0.9f), 0.6f);
            Sfx("contract_complete", Arp(new[] { 69, 73, 76, 81, 85 }, 0.12f, 1.6f), 0.7f);
            Sfx("contract_failed", Arp(new[] { 64, 60, 57 }, 0.18f, 1.2f, soft: true), 0.6f);
            Sfx("alert", Alert(), 0.75f);
            Sfx("suspicion", Pulse(), 0.45f);
            Sfx("cash", Coins(), 0.6f);
            Sfx("purchase", Register(), 0.6f);
            Sfx("save", Arp(new[] { 84, 91 }, 0.08f, 0.6f, soft: true), 0.4f);
            Sfx("victory", Arp(new[] { 69, 73, 76, 81, 76, 81, 85, 88 }, 0.14f, 2.6f), 0.8f);
            Sfx("page", Swish(), 0.3f);

            AssetDatabase.Refresh();
            ConfigureImports();
            FillLibrary();
            Debug.Log($"[AudioSynthBuilder] Done: {music.Count} music, {ambience.Count} ambience, {sfx.Count} effects.");
        }

        // =====================================================================================
        // Output
        // =====================================================================================

        private sealed class Clip
        {
            public float[] data;
            public int rate;
            public Clip(float seconds, int rate) { this.rate = rate; data = new float[Mathf.CeilToInt(seconds * rate)]; }
            public int Length => data.Length;
            public float Seconds => data.Length / (float)rate;
        }

        private static void Music(string id, Clip clip) => Write(id, clip, music, 0.9f, 0.85f);
        private static void Ambience(string id, Clip clip) => Write(id, clip, ambience, 0.9f, 0.8f);
        private static void Sfx(string id, Clip clip, float volume) => Write(id, clip, sfx, volume, 0.9f);

        private static void Write(string id, Clip clip, List<(string, string, float)> list, float volume, float peak)
        {
            Normalize(clip.data, peak);
            string path = $"{Folder}/{id}.wav";
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                int bytes = clip.Length * 2;
                writer.Write(new[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + bytes);
                writer.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(clip.rate);
                writer.Write(clip.rate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(new[] { 'd', 'a', 't', 'a' });
                writer.Write(bytes);
                foreach (float s in clip.data) writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(s, -1f, 1f) * 32767f));
            }

            list.Add((id, path, volume));
        }

        private static void Normalize(float[] data, float peak)
        {
            float max = 0f;
            foreach (float s in data) max = Mathf.Max(max, Mathf.Abs(s));
            if (max < 1e-6f) return;
            float gain = peak / max;
            for (int i = 0; i < data.Length; i++) data[i] *= gain;
        }

        private static void ConfigureImports()
        {
            void Set(string path, AudioClipLoadType load, AudioCompressionFormat format, float quality)
            {
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                settings.loadType = load;
                settings.compressionFormat = format;
                settings.quality = quality;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = true;
                importer.loadInBackground = load == AudioClipLoadType.Streaming;
                importer.SaveAndReimport();
            }

            foreach (var (_, path, _) in music) Set(path, AudioClipLoadType.Streaming, AudioCompressionFormat.Vorbis, 0.6f);
            foreach (var (_, path, _) in ambience) Set(path, AudioClipLoadType.CompressedInMemory, AudioCompressionFormat.Vorbis, 0.5f);
            foreach (var (_, path, _) in sfx) Set(path, AudioClipLoadType.DecompressOnLoad, AudioCompressionFormat.ADPCM, 1f);
        }

        private static void FillLibrary()
        {
            AudioLibrary library = AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryPath);
            AudioEntry[] Entries(List<(string id, string path, float volume)> list)
            {
                var entries = new AudioEntry[list.Count];
                for (int i = 0; i < list.Count; i++)
                {
                    entries[i] = new AudioEntry { id = list[i].id, clip = AssetDatabase.LoadAssetAtPath<AudioClip>(list[i].path), volume = list[i].volume, pitch = 1f };
                }

                return entries;
            }

            library.musicTracks = Entries(music);
            library.ambienceEntries = Entries(ambience);
            library.sfxEntries = Entries(sfx);
            library.defaultMusic = null; // the GameAudioDirector chooses per scene
            library.defaultAmbience = null;
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
        }

        // =====================================================================================
        // Building blocks
        // =====================================================================================

        private static float Hz(float midi) => 440f * Mathf.Pow(2f, (midi - 69f) / 12f);

        private static float Saw(double phase) => (float)(2.0 * (phase - Math.Floor(phase + 0.5)));
        private static float Tri(double phase) => 1f - 4f * Mathf.Abs((float)(phase - Math.Floor(phase + 0.5)));
        private static float Sine(double phase) => Mathf.Sin((float)(phase * 2.0 * Math.PI));

        /// <summary>Adds a voice at <paramref name="start"/>, wrapping past the end when <paramref name="loop"/>.</summary>
        private static void Add(Clip clip, float start, float seconds, Func<float, float> voice, bool loop)
        {
            int from = Mathf.RoundToInt(start * clip.rate);
            int count = Mathf.CeilToInt(seconds * clip.rate);
            for (int i = 0; i < count; i++)
            {
                int index = from + i;
                if (loop) index %= clip.Length;
                else if (index >= clip.Length) break;
                clip.data[index] += voice(i / (float)clip.rate);
            }
        }

        private static float Adsr(float t, float length, float attack, float release)
        {
            if (t < attack) return t / attack;
            if (t > length - release) return Mathf.Max(0f, (length - t) / release);
            return 1f;
        }

        /// <summary>One-pole low-pass (in place); <paramref name="wrap"/> pre-rolls so loops are seamless.</summary>
        private static void LowPass(float[] data, float cutoff, int rate, bool wrap = false)
        {
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / rate);
            float y = 0f;
            if (wrap) for (int i = Mathf.Max(0, data.Length - rate); i < data.Length; i++) y += a * (data[i] - y);
            for (int i = 0; i < data.Length; i++) data[i] = y += a * (data[i] - y);
        }

        private static void HighPass(float[] data, float cutoff, int rate)
        {
            float[] low = (float[])data.Clone();
            LowPass(low, cutoff, rate);
            for (int i = 0; i < data.Length; i++) data[i] -= low[i];
        }

        /// <summary>Small Schroeder reverb. For loops the tail wraps around the loop point.</summary>
        private static void Reverb(Clip clip, float wet, float size, bool loop)
        {
            int n = clip.Length;
            int passes = loop ? 2 : 1;
            float[] input = clip.data;
            float[] output = new float[n];
            int[] combs = { 1557, 1617, 1491, 1422 };
            int[] allpasses = { 225, 556 };
            float scale = clip.rate / 44100f;
            float[][] combBuf = new float[combs.Length][];
            int[] combIdx = new int[combs.Length];
            for (int c = 0; c < combs.Length; c++) combBuf[c] = new float[Mathf.Max(1, Mathf.RoundToInt(combs[c] * scale * size))];
            float[][] apBuf = new float[allpasses.Length][];
            int[] apIdx = new int[allpasses.Length];
            for (int a = 0; a < allpasses.Length; a++) apBuf[a] = new float[Mathf.Max(1, Mathf.RoundToInt(allpasses[a] * scale))];

            for (int pass = 0; pass < passes; pass++)
            {
                for (int i = 0; i < n; i++)
                {
                    float x = input[i] * 0.25f;
                    float sum = 0f;
                    for (int c = 0; c < combs.Length; c++)
                    {
                        float[] b = combBuf[c];
                        float y = b[combIdx[c]];
                        b[combIdx[c]] = x + y * 0.82f;
                        combIdx[c] = (combIdx[c] + 1) % b.Length;
                        sum += y;
                    }

                    for (int a = 0; a < allpasses.Length; a++)
                    {
                        float[] b = apBuf[a];
                        float bufOut = b[apIdx[a]];
                        b[apIdx[a]] = sum + bufOut * 0.5f;
                        sum = bufOut - sum;
                        apIdx[a] = (apIdx[a] + 1) % b.Length;
                    }

                    if (pass == passes - 1) output[i] = sum;
                }
            }

            for (int i = 0; i < n; i++) clip.data[i] = input[i] * (1f - wet * 0.5f) + output[i] * wet;
        }

        private static float[] Noise(int length, int seed)
        {
            var random = new System.Random(seed);
            var data = new float[length];
            for (int i = 0; i < length; i++) data[i] = (float)(random.NextDouble() * 2.0 - 1.0);
            return data;
        }

        // =====================================================================================
        // Music
        // =====================================================================================

        /// <summary>Night-time Seoul title theme: A minor pad, pentatonic plucks, soft sub (72 BPM, 16 bars).</summary>
        private static Clip MenuTheme()
        {
            const float beat = 60f / 72f, bar = beat * 4f;
            var clip = new Clip(bar * 16f, LoopRate);
            int[][] chords = { new[] { 57, 60, 64, 71 }, new[] { 53, 57, 60, 64 }, new[] { 48, 55, 60, 64 }, new[] { 55, 59, 62, 67 } };
            int[] roots = { 45, 41, 48, 43 };
            for (int b = 0; b < 16; b += 2)
            {
                int c = (b / 2) % 4;
                float start = b * bar, length = bar * 2f + 0.6f;
                foreach (int note in chords[c])
                {
                    float f = Hz(note);
                    Add(clip, start, length, t => Adsr(t, length, 1.4f, 1.6f) * 0.11f *
                        (Saw(f * t) + Saw(f * 1.004f * t) + 0.6f * Saw(f * 0.996f * t + 0.3)), true);
                }

                float r = Hz(roots[c]);
                Add(clip, start, length, t => Adsr(t, length, 0.3f, 1f) * 0.32f * Sine(r * t), true);
            }

            LowPass(clip.data, 1400f, clip.rate, true);

            var random = new System.Random(7);
            int[] scale = { 69, 72, 74, 76, 79, 81, 84 };
            for (int step = 0; step < 16 * 8; step++)
            {
                if (random.NextDouble() < 0.45) continue;
                float f = Hz(scale[random.Next(scale.Length)]);
                float vel = 0.12f + (float)random.NextDouble() * 0.1f;
                Add(clip, step * beat / 2f, 1.4f, t => vel * Mathf.Exp(-t * 4.5f) * (Sine(f * t) + 0.3f * Sine(2f * f * t)), true);
            }

            Reverb(clip, 0.45f, 1.3f, true);
            return clip;
        }

        /// <summary>Lo-fi city groove: electric-piano chords, bass, swung drums, vinyl crackle (84 BPM, 16 bars).</summary>
        private static Clip CityLoFi()
        {
            const float beat = 60f / 84f, bar = beat * 4f;
            var clip = new Clip(bar * 16f, LoopRate);
            int[][] chords = { new[] { 62, 65, 69, 72, 76 }, new[] { 55, 59, 65, 69, 76 }, new[] { 60, 64, 67, 71, 74 }, new[] { 57, 60, 64, 67, 72 } };
            int[] roots = { 38, 43, 36, 45 };
            var random = new System.Random(21);

            for (int b = 0; b < 16; b++)
            {
                int c = b % 4;
                float start = b * bar;
                foreach (int note in chords[c])
                {
                    float f = Hz(note);
                    float delay = (float)random.NextDouble() * 0.03f;
                    Add(clip, start + delay, bar, t => 0.07f * Mathf.Exp(-t * 0.9f) * (1f + 0.15f * Mathf.Sin(t * 2f * Mathf.PI * 4.5f)) *
                        (Sine(f * t) + 0.25f * Sine(2f * f * t) * Mathf.Exp(-t * 6f)), true);
                }

                float r = Hz(roots[c]);
                foreach (float at in new[] { 0f, 1.5f, 2.5f })
                {
                    Add(clip, start + at * beat, beat * 0.9f, t => 0.3f * Adsr(t, beat * 0.9f, 0.01f, 0.1f) * (Tri(r * t) + 0.3f * Sine(r * t)), true);
                }

                for (int q = 0; q < 4; q++)
                {
                    float qStart = start + q * beat;
                    if (q == 0 || q == 2 || (q == 3 && b % 2 == 1)) Add(clip, qStart, 0.4f, t => 0.55f * Mathf.Exp(-t * 9f) * Sine(45f * t + 12f * (1f - Mathf.Exp(-t * 30f)) / 30f * 6f), true);
                    for (int h = 0; h < 2; h++)
                    {
                        float hStart = qStart + (h == 0 ? 0f : beat * 0.58f);
                        float v = h == 0 ? 0.07f : 0.045f;
                        int seed = random.Next();
                        float[] hiss = Noise(2000, seed);
                        Add(clip, hStart, 0.05f, t => v * Mathf.Exp(-t * 90f) * hiss[Mathf.Min(hiss.Length - 1, (int)(t * 40000f))], true);
                    }
                }

                foreach (int q in new[] { 1, 3 })
                {
                    float[] snare = Noise(20000, random.Next());
                    Add(clip, start + q * beat, 0.25f, t => 0.2f * Mathf.Exp(-t * 18f) * (snare[(int)(t * 30000f) % snare.Length] * 0.8f + 0.4f * Sine(185f * t)), true);
                }
            }

            float[] crackle = Noise(clip.Length, 99);
            var pops = new System.Random(5);
            for (int i = 0; i < clip.Length; i++)
            {
                clip.data[i] += crackle[i] * 0.006f;
                if (pops.NextDouble() < 0.00012) clip.data[i] += (float)(pops.NextDouble() - 0.5) * 0.25f;
            }

            LowPass(clip.data, 5200f, clip.rate, true);
            Reverb(clip, 0.22f, 0.9f, true);
            return clip;
        }

        /// <summary>Tension (guards are suspicious): low drone, pulsing filtered bass, dissonant shimmer, ticking (110 BPM, 16 bars).</summary>
        private static Clip Tension()
        {
            const float beat = 60f / 110f, bar = beat * 4f;
            var clip = new Clip(bar * 16f, LoopRate);
            float length = clip.Seconds;
            Add(clip, 0f, length, t => 0.12f * (Saw(Hz(26) * t) + Saw(Hz(33) * 1.002f * t)) * (0.7f + 0.3f * Mathf.Sin(t * 0.4f)), true);
            LowPass(clip.data, 260f, clip.rate, true);

            for (int e = 0; e < 16 * 8; e++)
            {
                float f = Hz(e % 16 == 14 ? 39 : 38);
                Add(clip, e * beat / 2f, beat * 0.45f, t => 0.22f * Mathf.Exp(-t * 7f) * Saw(f * t) * (0.4f + 0.6f * Mathf.Exp(-t * 25f)), true);
            }

            for (int b = 0; b < 16; b += 4)
            {
                float start = b * bar;
                float len = bar * 4f;
                Add(clip, start, len, t => 0.03f * Mathf.Sin(Mathf.PI * t / len) * (Sine(Hz(74) * t) + Sine(Hz(75) * t)), true);
            }

            for (int s = 0; s < 16 * 16; s++)
            {
                float[] tick = Noise(400, s);
                float v = s % 4 == 0 ? 0.05f : 0.025f;
                Add(clip, s * beat / 4f, 0.02f, t => v * Mathf.Exp(-t * 200f) * tick[Mathf.Min(399, (int)(t * 20000f))], true);
            }

            for (int b = 0; b < 16; b += 2)
            {
                Add(clip, b * bar, 1.2f, t => 0.5f * Mathf.Exp(-t * 4f) * Sine(55f * t * (1f + 0.5f * Mathf.Exp(-t * 20f))), true);
            }

            Reverb(clip, 0.3f, 1.2f, true);
            return clip;
        }

        // =====================================================================================
        // Ambience
        // =====================================================================================

        /// <summary>Gangnam at night: traffic rumble, passing cars, distant horns, crowd murmur (24 s loop).</summary>
        private static Clip CityNight()
        {
            var clip = new Clip(24f, LoopRate);
            float[] rumble = Noise(clip.Length, 1);
            float brown = 0f;
            for (int i = 0; i < rumble.Length; i++) rumble[i] = brown = Mathf.Clamp(brown + rumble[i] * 0.02f, -1f, 1f) * 0.998f;
            HighPass(rumble, 25f, clip.rate); // no DC offset from the brown noise
            LowPass(rumble, 220f, clip.rate, true);
            for (int i = 0; i < clip.Length; i++) clip.data[i] += rumble[i] * 1.6f;

            var random = new System.Random(3);
            for (int car = 0; car < 9; car++)
            {
                float start = (float)random.NextDouble() * 24f, len = 3f + (float)random.NextDouble() * 3f;
                float[] tyre = Noise(Mathf.CeilToInt(len * clip.rate) + 10, random.Next());
                LowPass(tyre, 600f + (float)random.NextDouble() * 500f, clip.rate);
                float gain = 0.5f + (float)random.NextDouble() * 0.6f;
                Add(clip, start, len, t => gain * Mathf.Pow(Mathf.Sin(Mathf.PI * t / len), 3f) * tyre[(int)(t * clip.rate)], true);
            }

            for (int horn = 0; horn < 3; horn++)
            {
                float start = 2f + horn * 8f + (float)random.NextDouble() * 4f, len = 0.25f + (float)random.NextDouble() * 0.3f;
                Add(clip, start, len, t => 0.06f * Adsr(t, len, 0.02f, 0.08f) * (Mathf.Sign(Sine(410f * t)) + Mathf.Sign(Sine(512f * t))), true);
            }

            float[] murmur = Noise(clip.Length, 11);
            HighPass(murmur, 280f, clip.rate);
            LowPass(murmur, 1300f, clip.rate, true);
            for (int i = 0; i < clip.Length; i++)
            {
                float t = i / (float)clip.rate;
                float swell = 0.55f + 0.25f * Mathf.Sin(t * 2f * Mathf.PI / 24f * 3f) + 0.2f * Mathf.Sin(t * 2f * Mathf.PI / 24f * 7f + 1f);
                clip.data[i] += murmur[i] * 0.35f * swell;
            }

            LowPass(clip.data, 3500f, clip.rate, true);
            return clip;
        }

        /// <summary>Interior room tone: mains hum, fluorescent buzz, air handling (16 s loop).</summary>
        private static Clip Interior()
        {
            var clip = new Clip(16f, LoopRate);
            Add(clip, 0f, 16f, t => 0.25f * Sine(60f * t) + 0.12f * Sine(120f * t) + 0.05f * Sine(180f * t), true);
            float[] air = Noise(clip.Length, 8);
            LowPass(air, 900f, clip.rate, true);
            for (int i = 0; i < clip.Length; i++) clip.data[i] += air[i] * 0.5f;
            return clip;
        }

        // =====================================================================================
        // Effects
        // =====================================================================================

        private static Clip Footstep(int variant)
        {
            var clip = new Clip(0.16f, SfxRate);
            float[] grit = Noise(clip.Length, 100 + variant);
            LowPass(grit, 1800f + variant * 350f, SfxRate);
            float thump = 85f + variant * 9f;
            for (int i = 0; i < clip.Length; i++)
            {
                float t = i / (float)SfxRate;
                clip.data[i] = grit[i] * Mathf.Exp(-t * 45f) * 1.4f + 0.6f * Mathf.Exp(-t * 60f) * Sine(thump * t);
            }

            return clip;
        }

        private static Clip Land()
        {
            var clip = new Clip(0.35f, SfxRate);
            float[] grit = Noise(clip.Length, 300);
            LowPass(grit, 900f, SfxRate);
            for (int i = 0; i < clip.Length; i++)
            {
                float t = i / (float)SfxRate;
                clip.data[i] = grit[i] * Mathf.Exp(-t * 18f) * 1.2f + Mathf.Exp(-t * 22f) * Sine(62f * t);
            }

            return clip;
        }

        private static Clip Blip(float f, float length, float level)
        {
            var clip = new Clip(length, SfxRate);
            Add(clip, 0f, length, t => level * Adsr(t, length, 0.002f, length * 0.8f) * Sine(f * t), false);
            return clip;
        }

        private static Clip TwoTone(float a, float b, float each)
        {
            var clip = new Clip(each * 2f + 0.05f, SfxRate);
            Add(clip, 0f, each, t => Adsr(t, each, 0.002f, each * 0.6f) * (Sine(a * t) + 0.2f * Sine(2f * a * t)), false);
            Add(clip, each, each + 0.05f, t => Adsr(t, each + 0.05f, 0.002f, each) * (Sine(b * t) + 0.2f * Sine(2f * b * t)), false);
            return clip;
        }

        private static Clip Buzz()
        {
            var clip = new Clip(0.22f, SfxRate);
            Add(clip, 0f, 0.22f, t => Adsr(t, 0.22f, 0.005f, 0.06f) * Saw(140f * t) * 0.6f, false);
            LowPass(clip.data, 1500f, SfxRate);
            return clip;
        }

        /// <summary>Bell-like arpeggio (soft = sine only).</summary>
        private static Clip Arp(int[] notes, float spacing, float tail, bool soft = false)
        {
            var clip = new Clip(spacing * notes.Length + tail, SfxRate);
            for (int n = 0; n < notes.Length; n++)
            {
                float f = Hz(notes[n]);
                Add(clip, n * spacing, tail, t => Mathf.Exp(-t * 5f) * Mathf.Min(1f, t * 400f) *
                    (Sine(f * t) + (soft ? 0f : 0.35f * Sine(2.76f * f * t) * Mathf.Exp(-t * 9f))), false);
            }

            Reverb(clip, 0.3f, 0.8f, false);
            return clip;
        }

        private static Clip Shutter()
        {
            var clip = new Clip(0.25f, SfxRate);
            float[] noise = Noise(clip.Length, 42);
            HighPass(noise, 2500f, SfxRate);
            foreach (float at in new[] { 0f, 0.075f })
            {
                Add(clip, at, 0.06f, t => Mathf.Exp(-t * 90f) * noise[(int)(t * SfxRate) % noise.Length] * 1.5f + Mathf.Exp(-t * 120f) * Sine(1100f * t) * 0.5f, false);
            }

            return clip;
        }

        private static Clip Alert()
        {
            var clip = new Clip(0.9f, SfxRate);
            foreach ((float at, float f) in new[] { (0f, Hz(81)), (0.14f, Hz(86)), (0.28f, Hz(81)), (0.42f, Hz(86)) })
            {
                Add(clip, at, 0.12f, t => Adsr(t, 0.12f, 0.004f, 0.03f) * (Saw(f * t) * 0.4f + Sine(f * t) * 0.6f), false);
            }

            Add(clip, 0f, 0.9f, t => 0.8f * Mathf.Exp(-t * 6f) * Sine(70f * t), false);
            LowPass(clip.data, 4000f, SfxRate);
            Reverb(clip, 0.25f, 0.8f, false);
            return clip;
        }

        private static Clip Pulse()
        {
            var clip = new Clip(0.5f, SfxRate);
            Add(clip, 0f, 0.15f, t => Mathf.Exp(-t * 25f) * Sine(58f * t), false);
            Add(clip, 0.18f, 0.15f, t => 0.7f * Mathf.Exp(-t * 25f) * Sine(52f * t), false);
            return clip;
        }

        private static Clip Coins()
        {
            var clip = new Clip(0.7f, SfxRate);
            var random = new System.Random(9);
            for (int c = 0; c < 5; c++)
            {
                float f = 2400f + (float)random.NextDouble() * 1800f;
                Add(clip, c * 0.05f + (float)random.NextDouble() * 0.02f, 0.5f, t => 0.5f * Mathf.Exp(-t * 14f) * (Sine(f * t) + 0.6f * Sine(f * 2.41f * t) + 0.3f * Sine(f * 3.7f * t)), false);
            }

            return clip;
        }

        private static Clip Register()
        {
            var clip = new Clip(0.9f, SfxRate);
            Add(clip, 0f, 0.06f, t => Mathf.Exp(-t * 80f) * Saw(300f * t), false);
            Add(clip, 0.08f, 0.8f, t => Mathf.Exp(-t * 4f) * (Sine(Hz(88) * t) + 0.5f * Sine(Hz(88) * 2.76f * t) * Mathf.Exp(-t * 8f)), false);
            Reverb(clip, 0.25f, 0.8f, false);
            return clip;
        }

        private static Clip Swish()
        {
            var clip = new Clip(0.18f, SfxRate);
            float[] noise = Noise(clip.Length, 77);
            HighPass(noise, 1200f, SfxRate);
            for (int i = 0; i < clip.Length; i++)
            {
                float t = i / (float)SfxRate;
                clip.data[i] = noise[i] * Mathf.Sin(Mathf.PI * t / 0.18f) * 0.6f;
            }

            return clip;
        }
    }
}
