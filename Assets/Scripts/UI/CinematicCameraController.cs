using UnityEngine;
using UnityEngine.InputSystem;

namespace Contract.UI
{
    /// <summary>
    /// Controls cinematic camera movement for main menu background.
    /// </summary>
    public sealed class CinematicCameraController : MonoBehaviour
    {
        [Header("Cinematic Settings")]
        [SerializeField] private Transform[] waypoints;
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private float rotationSpeed = 10f;
        [SerializeField] private bool loop = true;

        [Header("Mouse Parallax")]
        [SerializeField] private float parallaxAmount = 0.5f;

        private int currentWaypoint = 0;
        private float progress;
        private Camera mainCamera;

        private void Awake()
        {
            mainCamera = GetComponent<Camera>();
        }

        public void StartCinematic()
        {
            if (waypoints == null || waypoints.Length == 0) return;
            currentWaypoint = 0;
            progress = 0f;
        }

        private void Update()
        {
            if (waypoints == null || waypoints.Length == 0) return;

            HandleParallax();
            MoveAlongPath();
        }

        private void HandleParallax()
        {
            if (mainCamera == null || Mouse.current == null) return;

            Vector2 mousePosition = Mouse.current.position.ReadValue();
            Vector3 mousePos = new Vector3(mousePosition.x, mousePosition.y, 0f);
            Vector3 center = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
            Vector3 offset = (mousePos - center) * parallaxAmount * 0.001f;

            mainCamera.transform.localRotation = Quaternion.Slerp(
                mainCamera.transform.localRotation,
                Quaternion.Euler(-offset.y, offset.x, 0f),
                Time.deltaTime * 2f);
        }

        private void MoveAlongPath()
        {
            if (waypoints.Length < 2) return;

            Transform target = waypoints[currentWaypoint];
            Transform next = waypoints[(currentWaypoint + 1) % waypoints.Length];

            progress += Time.deltaTime * moveSpeed / Vector3.Distance(target.position, next.position);

            if (progress >= 1f)
            {
                progress = 0f;
                currentWaypoint = (currentWaypoint + 1) % waypoints.Length;

                if (!loop && currentWaypoint == 0)
                {
                    enabled = false;
                    return;
                }
            }

            Vector3 pos = Vector3.Lerp(target.position, next.position, progress);
            Quaternion rot = Quaternion.Slerp(target.rotation, next.rotation, progress);

            transform.SetPositionAndRotation(pos, rot);
        }

        private void OnDrawGizmos()
        {
            if (waypoints == null || waypoints.Length < 2) return;

            Gizmos.color = Color.cyan;
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] != null)
                {
                    Gizmos.DrawWireSphere(waypoints[i].position, 0.5f);
                    int next = (i + 1) % waypoints.Length;
                    if (waypoints[next] != null)
                    {
                        Gizmos.DrawLine(waypoints[i].position, waypoints[next].position);
                    }
                }
            }
        }
    }
}