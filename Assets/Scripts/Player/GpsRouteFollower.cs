using Contract.Activity;
using Contract.Core;
using UnityEngine;

namespace Contract.Player
{
    /// <summary>
    /// GPS Life Mode for the character: while GPS mode is on, the operative walks the district's
    /// <see cref="GpsRoute"/> by the virtual distance the player has really walked. Movement goes
    /// through the normal <see cref="ThirdPersonController"/> (no teleporting, collisions and
    /// animation as usual): pace matches the remaining distance, jogging only when far behind, and
    /// progress counts only the ground actually covered, so a blocked character catches up later
    /// instead of jumping. Joystick / keyboard movement is ignored meanwhile.
    /// </summary>
    [RequireComponent(typeof(ThirdPersonController))]
    public sealed class GpsRouteFollower : MonoBehaviour, IMovementDriver
    {
        private const float LookAhead = 1.6f;
        private const float ArriveTolerance = 0.25f;
        private const float WalkingPace = 1.4f; // m/s, an ordinary walk

        private ThirdPersonController controller;
        private double progress;      // route metres the character has covered
        private Vector3 lastPosition;
        private bool placed;

        public bool DriverActive { get; private set; }
        public Vector3 DriveDirection { get; private set; }
        public bool DriveSprint { get; private set; }

        /// <summary>Route metres still to walk to match the real distance.</summary>
        public double Remaining => Target - progress;
        public double Progress => progress;
        /// <summary>Route position the real walking leads to (progress + where on the loop it started).</summary>
        private static double Target
        {
            get
            {
                GameSession session = GameManager.Instance?.Session;
                return session == null ? 0 : session.gpsRouteMeters + session.gpsRouteOffset;
            }
        }
        private static bool ModeOn => ActivityController.Instance != null && ActivityController.Instance.GpsModeActive;

        private void Awake()
        {
            controller = GetComponent<ThirdPersonController>();
            controller.Driver = this;
        }

        private void OnDestroy()
        {
            if (controller != null && ReferenceEquals(controller.Driver, this)) controller.Driver = null;
        }

        private void Update()
        {
            GpsRoute route = GpsRoute.Current;
            DriverActive = ModeOn && route != null && route.Length > 1f;
            if (!DriverActive)
            {
                placed = false;
                DriveDirection = Vector3.zero;
                DriveSprint = false;
                return;
            }

            if (!placed)
            {
                // Scene start: stand on the route. GPS mode switched on mid-city: no teleport, continue
                // from the nearest route point (the character walks there itself).
                if (Time.timeSinceLevelLoad < 1f) PlaceOnRoute(route);
                else AlignToNearest(route);
            }

            // Count only the ground really covered along the route direction.
            Vector3 moved = transform.position - lastPosition;
            moved.y = 0f;
            lastPosition = transform.position;
            Vector3 along = route.PointAt(progress + LookAhead) - route.PointAt(progress);
            along.y = 0f;
            if (along.sqrMagnitude > 0.0001f) progress += Mathf.Max(0f, Vector3.Dot(moved, along.normalized));
            progress = System.Math.Min(progress, Target); // never ahead of the real distance

            double remaining = Target - progress;
            if (remaining <= ArriveTolerance)
            {
                DriveDirection = Vector3.zero;
                DriveSprint = false;
                return;
            }

            Vector3 aim = route.PointAt(progress + LookAhead) - transform.position;
            aim.y = 0f;
            float walk = Mathf.Max(0.1f, controller.WalkSpeed);
            float catchUp = ActivityConfig.Load().catchUpDistance;
            DriveSprint = remaining > catchUp;
            // Walk at a natural pace; slow down for the last metre so the character settles exactly.
            float pace = DriveSprint ? 1f : Mathf.Clamp(Mathf.Max(WalkingPace, (float)remaining) / walk, 0.25f, 1f);
            if (remaining < 1.0) pace = Mathf.Max(0.25f, (float)remaining / walk * 2f);
            DriveDirection = aim.sqrMagnitude > 0.0001f ? aim.normalized * pace : Vector3.zero;
        }

        private void AlignToNearest(GpsRoute route)
        {
            placed = true;
            GameSession session = GameManager.Instance.Session;
            double nearest = route.Project(transform.position);
            session.gpsRouteOffset = nearest - session.gpsRouteMeters;
            progress = nearest;
            lastPosition = transform.position;
        }

        /// <summary>Scene load in GPS mode: stand on the route where the walked distance leads.</summary>
        private void PlaceOnRoute(GpsRoute route)
        {
            placed = true;
            progress = Target;
            Vector3 spot = route.PointAt(progress);
            Vector3 ahead = route.PointAt(progress + LookAhead) - spot;
            ahead.y = 0f;
            var characterController = GetComponent<CharacterController>();
            if (characterController != null) characterController.enabled = false;
            transform.SetPositionAndRotation(spot + Vector3.up * 0.1f, ahead.sqrMagnitude > 0.001f ? Quaternion.LookRotation(ahead) : transform.rotation);
            if (characterController != null) characterController.enabled = true;
            Physics.SyncTransforms();
            lastPosition = transform.position;
        }
    }
}
