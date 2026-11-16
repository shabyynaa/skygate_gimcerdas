using UnityEngine;

namespace SkyGate.EnemyAI
{
    public class EnemyPerception : MonoBehaviour
    {
        [Header("Perception Settings")]
        [SerializeField] private Light gazeLight;
        [SerializeField] private Color patrolGazeColor = new Color(1f, 0.88f, 0.25f, 1f);
        [SerializeField] private Color alertGazeColor = new Color(1f, 0.22f, 0.25f, 1f);
        [SerializeField] private LayerMask losBlockingMask;

        private Transform targetTransform;
        private EnemyArchetypeSO archetype;
        private float nextTargetSearchTime;

        public Transform TargetTransform => targetTransform;
        public bool HasLineOfSight { get; private set; }
        public bool IsTargetDetected { get; private set; }
        public float DistanceToTarget { get; private set; }
        public Vector3 LastSeenPosition { get; private set; }

        public static bool ShowDebugGizmos { get; set; }

        private void Awake()
        {
            if (losBlockingMask.value == 0)
            {
                int wallLayer = LayerMask.NameToLayer("Wall");
                int obstacleLayer = LayerMask.NameToLayer("Obstacle");
                losBlockingMask = (wallLayer >= 0 ? (1 << wallLayer) : 0) | (obstacleLayer >= 0 ? (1 << obstacleLayer) : 0);
            }
        }

        public void Initialize(EnemyArchetypeSO data, Transform playerTarget)
        {
            archetype = data;
            targetTransform = playerTarget;
            SetAlertVisual(false);
        }

        public void SetTarget(Transform playerTarget)
        {
            targetTransform = playerTarget;
        }

        public bool EvaluatePerception(bool isCurrentlyPursuing)
        {
            if (targetTransform == null && Time.time >= nextTargetSearchTime)
            {
                nextTargetSearchTime = Time.time + 1.0f;
                if (SkyGate.Core.GameInputReader.Instance != null)
                {
                    var pc = FindAnyObjectByType<SkyGate.Player.PlayerController>();
                    if (pc != null) targetTransform = pc.transform;
                }
            }

            if (targetTransform == null || archetype == null)
            {
                IsTargetDetected = false;
                HasLineOfSight = false;
                DistanceToTarget = float.MaxValue;
                return false;
            }

            Vector3 origin = transform.position + Vector3.up * 1.1f;
            Vector3 targetPos = targetTransform.position + Vector3.up * 1.0f;
            Vector3 toTarget = targetPos - origin;
            Vector3 flatToTarget = new Vector3(toTarget.x, 0f, toTarget.z);
            DistanceToTarget = flatToTarget.magnitude;

            float effectiveRadius = isCurrentlyPursuing
                ? archetype.detectRadius * archetype.pursuitPersistenceMultiplier
                : archetype.detectRadius;

            if (DistanceToTarget > effectiveRadius)
            {
                IsTargetDetected = false;
                HasLineOfSight = false;
                return false;
            }

            bool inCloseAlert = DistanceToTarget <= archetype.closeAlertRadius;
            float angle = Vector3.Angle(transform.forward, flatToTarget.normalized);
            bool inFov = inCloseAlert || isCurrentlyPursuing || (angle <= archetype.fovAngle * 0.5f);

            if (!inFov)
            {
                IsTargetDetected = false;
                HasLineOfSight = false;
                return false;
            }

            // Raycast LOS ignoring TriggerZone per ASSET_CONTRACT.md Section 4.2
            bool blocked = Physics.Raycast(
                origin,
                toTarget.normalized,
                toTarget.magnitude,
                losBlockingMask,
                QueryTriggerInteraction.Ignore
            );

            HasLineOfSight = !blocked;
            IsTargetDetected = HasLineOfSight;

            if (HasLineOfSight)
            {
                LastSeenPosition = targetTransform.position;
            }

            return IsTargetDetected;
        }

        public void SetAlertVisual(bool isAlert)
        {
            if (gazeLight != null)
            {
                gazeLight.color = isAlert ? alertGazeColor : patrolGazeColor;
                gazeLight.intensity = isAlert ? 3.2f : 2.0f;
            }
        }

        private void OnDrawGizmos()
        {
            if (!ShowDebugGizmos || archetype == null) return;

            Vector3 origin = transform.position + Vector3.up * 0.5f;
            Gizmos.color = IsTargetDetected ? new Color(1f, 0.2f, 0.2f, 0.45f) : new Color(1f, 0.9f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(origin, archetype.detectRadius);

            Vector3 leftRay = Quaternion.Euler(0f, -archetype.fovAngle * 0.5f, 0f) * transform.forward;
            Vector3 rightRay = Quaternion.Euler(0f, archetype.fovAngle * 0.5f, 0f) * transform.forward;
            Gizmos.DrawLine(origin, origin + leftRay * archetype.detectRadius);
            Gizmos.DrawLine(origin, origin + rightRay * archetype.detectRadius);

            if (targetTransform != null && DistanceToTarget <= archetype.detectRadius * 1.4f)
            {
                Gizmos.color = HasLineOfSight ? Color.red : Color.gray;
                Gizmos.DrawLine(origin + Vector3.up * 0.6f, targetTransform.position + Vector3.up * 1.0f);
            }
        }
    }
}
