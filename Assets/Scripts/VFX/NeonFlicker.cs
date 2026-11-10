using UnityEngine;

namespace Contract.VFX
{
    /// <summary>
    /// An ageing neon sign: on most of the time, with occasional short bursts of flicker.
    /// Toggles the renderer only during a burst (no per-frame material changes).
    /// </summary>
    public sealed class NeonFlicker : MonoBehaviour
    {
        [SerializeField] private Vector2 calmSeconds = new Vector2(3f, 11f);
        [SerializeField] private Vector2 burstSeconds = new Vector2(0.15f, 0.7f);
        [SerializeField] private int seed;

        private Renderer target;
        private System.Random random;
        private float timer;
        private float burstLeft;

        public int Bursts { get; private set; }
        public bool Lit => target == null || target.enabled;

        public void Configure(int randomSeed, Vector2 calm)
        {
            seed = randomSeed;
            calmSeconds = calm;
            if (random == null) return; // Awake will initialise
            random = new System.Random(seed);
            timer = Range(calmSeconds) * 0.5f;
        }

        private void Awake()
        {
            target = GetComponent<Renderer>();
            random = new System.Random(seed);
            timer = Range(calmSeconds) * 0.5f;
        }

        private void OnDisable()
        {
            if (target != null) target.enabled = true;
        }

        private float Range(Vector2 range) => range.x + (float)random.NextDouble() * (range.y - range.x);

        private void Update()
        {
            if (target == null) return;
            float dt = Time.deltaTime;
            if (burstLeft > 0f)
            {
                burstLeft -= dt;
                target.enabled = burstLeft <= 0f || random.NextDouble() > 0.45;
                if (burstLeft <= 0f) timer = Range(calmSeconds);
                return;
            }

            timer -= dt;
            if (timer > 0f) return;
            burstLeft = Range(burstSeconds);
            Bursts++;
        }
    }
}
