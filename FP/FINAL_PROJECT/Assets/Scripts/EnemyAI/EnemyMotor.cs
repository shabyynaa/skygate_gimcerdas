using UnityEngine;
using UnityEngine.AI;

namespace SkyGate.EnemyAI
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyMotor : MonoBehaviour
    {
        // Animator Parameter Hashes (ASSET_CONTRACT.md Section 5.2)
        private static readonly int HashSpeed = Animator.StringToHash("Speed");
        private static readonly int HashIsMoving = Animator.StringToHash("IsMoving");

        [SerializeField] private Animator animator;

        private NavMeshAgent agent;
        private Bounds roomBounds;
        private bool hasRoomBounds;
        private float staggerUntilTime;
        private bool isUnstoppable;

        public NavMeshAgent Agent => agent;
        public bool IsStaggered => Time.time < staggerUntilTime;
        public bool IsUnstoppable { get => isUnstoppable; set => isUnstoppable = value; }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            if (animator != null)
                animator.applyRootMotion = false; // Mandatory per AI_RULES.md & ASSET_CONTRACT.md
        }

        public void SetRoomBounds(Bounds bounds)
        {
            roomBounds = bounds;
            hasRoomBounds = bounds.size.sqrMagnitude > 1f;
        }

        public Vector3 ClampPositionToRoom(Vector3 candidate)
        {
            if (hasRoomBounds)
            {
                float margin = 1.25f;
                candidate.x = Mathf.Clamp(candidate.x, roomBounds.min.x + margin, roomBounds.max.x - margin);
                candidate.z = Mathf.Clamp(candidate.z, roomBounds.min.z + margin, roomBounds.max.z - margin);
                candidate.y = transform.position.y;
            }

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
            {
                return hit.position;
            }
            return transform.position;
        }

        public void MoveTo(Vector3 destination, float speed)
        {
            if (agent == null || !agent.isOnNavMesh || IsStaggered) return;

            agent.speed = speed;
            agent.isStopped = false;
            Vector3 clamped = ClampPositionToRoom(destination);
            agent.SetDestination(clamped);
        }

        public void StopMoving()
        {
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
                agent.velocity = Vector3.zero;
            }
        }

        public void FacePosition(Vector3 targetPos, float turnSpeed = 10f)
        {
            Vector3 dir = targetPos - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
            }
        }

        public void ApplyKnockbackAndStagger(Vector3 direction, float distance, float staggerDuration)
        {
            if (isUnstoppable) return;

            staggerUntilTime = Mathf.Max(staggerUntilTime, Time.time + staggerDuration);
            StopMoving();

            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
            {
                Vector3 targetPos = transform.position + direction.normalized * distance;
                Vector3 safePos = ClampPositionToRoom(targetPos);
                if (agent != null && agent.isOnNavMesh)
                {
                    agent.Warp(safePos);
                }
                else
                {
                    transform.position = safePos;
                }
            }
        }

        public void WarpTo(Vector3 position, Quaternion rotation)
        {
            staggerUntilTime = 0f;
            if (agent != null && agent.isOnNavMesh)
            {
                agent.Warp(position);
            }
            else
            {
                transform.position = position;
            }
            transform.rotation = rotation;
        }

        private void Update()
        {
            if (animator != null && agent != null)
            {
                float vel = agent.isOnNavMesh ? agent.velocity.magnitude : 0f;
                animator.SetFloat(HashSpeed, vel);
                animator.SetBool(HashIsMoving, vel > 0.15f && !agent.isStopped);
            }
        }
    }
}
