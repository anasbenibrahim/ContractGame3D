using UnityEngine;

namespace Contract.UI
{
    /// <summary>Slow cinematic camera for menu backdrops: eases between shots (position + look target) and loops.</summary>
    public sealed class MenuCameraRig : MonoBehaviour
    {
        [System.Serializable]
        public struct Shot
        {
            public Vector3 position;
            public Vector3 lookAt;
        }

        [SerializeField] private Shot[] shots;
        [SerializeField] private float secondsPerShot = 14f;

        private float time;

        public void Configure(Shot[] cameraShots, float seconds)
        {
            shots = cameraShots;
            secondsPerShot = seconds;
        }

        private void LateUpdate()
        {
            if (shots == null || shots.Length < 2) return;
            time += Time.unscaledDeltaTime;
            float t = time / secondsPerShot;
            int i = (int)t % shots.Length;
            Shot a = shots[i], b = shots[(i + 1) % shots.Length];
            float k = Mathf.SmoothStep(0f, 1f, t - Mathf.Floor(t));
            transform.position = Vector3.Lerp(a.position, b.position, k);
            transform.rotation = Quaternion.LookRotation(Vector3.Lerp(a.lookAt, b.lookAt, k) - transform.position);
        }
    }
}
