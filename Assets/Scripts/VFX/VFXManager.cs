using System;
using System.Collections.Generic;
using Contract.Core;
using UnityEngine;

namespace Contract.VFX
{
    /// <summary>
    /// Central VFX manager for particle effects, screen effects, and visual feedback.
    /// </summary>
    public sealed class VFXManager : MonoBehaviour
    {
        public static VFXManager Instance { get; private set; }

        [Header("Particle Prefabs")]
        [SerializeField] private GameObject interactionParticles;
        [SerializeField] private GameObject suspicionParticles;
        [SerializeField] private GameObject alertParticles;
        [SerializeField] private GameObject extractionParticles;
        [SerializeField] private GameObject footstepParticles;
        [SerializeField] private GameObject impactParticles;
        [SerializeField] private GameObject hackParticles;
        [SerializeField] private GameObject pickupParticles;

        public GameObject InteractionParticles { get => interactionParticles; set => interactionParticles = value; }
        public GameObject SuspicionParticles { get => suspicionParticles; set => suspicionParticles = value; }
        public GameObject AlertParticles { get => alertParticles; set => alertParticles = value; }
        public GameObject ExtractionParticles { get => extractionParticles; set => extractionParticles = value; }
        public GameObject FootstepParticles { get => footstepParticles; set => footstepParticles = value; }
        public GameObject ImpactParticles { get => impactParticles; set => impactParticles = value; }
        public GameObject HackParticles { get => hackParticles; set => hackParticles = value; }
        public GameObject PickupParticles { get => pickupParticles; set => pickupParticles = value; }

        [Header("Screen Effects")]
        [SerializeField] private Material vignetteMaterial;
        [SerializeField] private Material chromaticAberrationMaterial;

        [Header("Pool Settings")]
        [SerializeField, Range(1, 50)] private int particlePoolSize = 20;

        private Dictionary<string, Queue<ParticleSystem>> particlePools = new Dictionary<string, Queue<ParticleSystem>>();
        private Dictionary<string, GameObject> particlePrefabs = new Dictionary<string, GameObject>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                DestroyImmediate(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            RegisterPrefabs();
            InitializePools();
        }

        private void RegisterPrefabs()
        {
            particlePrefabs["interaction"] = interactionParticles;
            particlePrefabs["suspicion"] = suspicionParticles;
            particlePrefabs["alert"] = alertParticles;
            particlePrefabs["extraction"] = extractionParticles;
            particlePrefabs["footstep"] = footstepParticles;
            particlePrefabs["impact"] = impactParticles;
            particlePrefabs["hack"] = hackParticles;
            particlePrefabs["pickup"] = pickupParticles;
        }

        private void InitializePools()
        {
            foreach (var kvp in particlePrefabs)
            {
                if (kvp.Value != null)
                {
                    Queue<ParticleSystem> pool = new Queue<ParticleSystem>();
                    for (int i = 0; i < particlePoolSize; i++)
                    {
                        GameObject go = Instantiate(kvp.Value, transform);
                        go.name = $"{kvp.Key}_Pool_{i}";
                        go.SetActive(false);
                        ParticleSystem ps = go.GetComponent<ParticleSystem>();
                        if (ps != null) pool.Enqueue(ps);
                    }
                    particlePools[kvp.Key] = pool;
                }
            }
        }

        public void PlayParticles(string effectName, Vector3 position, Quaternion rotation = default, Vector3 scale = default)
        {
            if (!particlePools.TryGetValue(effectName, out Queue<ParticleSystem> pool)) return;
            if (pool.Count == 0) return;

            ParticleSystem ps = pool.Dequeue();
            if (ps == null) return;

            ps.transform.SetPositionAndRotation(position, rotation == default ? Quaternion.identity : rotation);
            if (scale != default) ps.transform.localScale = scale;

            var main = ps.main;
            main.stopAction = ParticleSystemStopAction.Callback;

            ps.gameObject.SetActive(true);
            ps.Play();

            // Return to pool after duration
            StartCoroutine(ReturnToPool(effectName, ps, main.duration + main.startLifetime.constantMax));
        }

        private System.Collections.IEnumerator ReturnToPool(string effectName, ParticleSystem ps, float delay)
        {
            yield return new WaitForSeconds(delay + 0.5f);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.gameObject.SetActive(false);
            if (particlePools.ContainsKey(effectName))
            {
                particlePools[effectName].Enqueue(ps);
            }
        }

        public void PlayInteraction(Vector3 position)
        {
            PlayParticles("interaction", position);
        }

        public void PlaySuspicion(Vector3 position, float intensity = 1f)
        {
            PlayParticles("suspicion", position, scale: Vector3.one * intensity);
        }

        public void PlayAlert(Vector3 position)
        {
            PlayParticles("alert", position);
        }

        public void PlayExtraction(Vector3 position)
        {
            PlayParticles("extraction", position);
        }

        public void PlayFootstep(Vector3 position, float intensity = 1f)
        {
            PlayParticles("footstep", position, scale: Vector3.one * intensity);
        }

        public void PlayImpact(Vector3 position, Vector3 normal)
        {
            Quaternion rot = Quaternion.LookRotation(normal);
            PlayParticles("impact", position, rot);
        }

        public void PlayHack(Vector3 position)
        {
            PlayParticles("hack", position);
        }

        public void PlayPickup(Vector3 position)
        {
            PlayParticles("pickup", position);
        }

        // Screen space effects
        public void TriggerScreenFlash(Color color, float duration = 0.2f)
        {
            // Would integrate with post-processing volume
            GameEvents.RaiseScreenFlash(color, duration);
        }

        public void TriggerVignette(float intensity, float duration = 1f)
        {
            GameEvents.RaiseVignetteChange(intensity, duration);
        }

        public void TriggerChromaticAberration(float intensity, float duration = 0.5f)
        {
            GameEvents.RaiseChromaticAberrationChange(intensity, duration);
        }
    }
}