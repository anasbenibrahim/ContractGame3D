using Contract.Player;
using UnityEngine;
using UnityEngine.AI;

namespace Contract.NPCs
{
    /// <summary>Feeds NavMeshAgent movement to the shared rig/animation layer.</summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class NpcLocomotion : MonoBehaviour, ILocomotionSource
    {
        private NavMeshAgent agent;

        private void Awake() => agent = GetComponent<NavMeshAgent>();

        public float PlanarSpeed
        {
            get
            {
                if (agent == null || !agent.enabled) return 0f;
                Vector3 v = agent.velocity;
                return new Vector2(v.x, v.z).magnitude;
            }
        }

        public bool Sprinting => PlanarSpeed > 3f;
        public float CrouchAmount => 0f;
        public bool Grounded => true;
    }
}
