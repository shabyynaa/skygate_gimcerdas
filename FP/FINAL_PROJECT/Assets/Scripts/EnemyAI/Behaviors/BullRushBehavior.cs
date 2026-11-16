using UnityEngine;
using SkyGate.Core;
using SkyGate.Combat;

namespace SkyGate.EnemyAI.Behaviors
{
    public class BullRushBehavior : MonoBehaviour
    {
        private static readonly int HashIsCharging = Animator.StringToHash("IsCharging");
        private static readonly int HashWindup = Animator.StringToHash("Windup");

        private enum RushSubState
        {
            Idle,
            Windup,
            Charging
        }

        [SerializeField] private Animator animator;
        [SerializeField] private AttackTelegraph telegraph;

        private EnemyBrain brain;
        private RushSubState rushState = RushSubState.Idle;
        private float stateTimer;
        private float nextRushAllowedTime;
        private Vector3 lockedChargeDir;
        private Transform cachedTarget;
        private int pillarAndObstacleMask;

        public bool IsBusy => rushState != RushSubState.Idle;

        private void Awake()
        {
            brain = GetComponent<EnemyBrain>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            if (telegraph == null)
                telegraph = GetComponentInChildren<AttackTelegraph>(true);

            int wallLayer = LayerMask.NameToLayer("Wall");
            int obsLayer = LayerMask.NameToLayer("Obstacle");
            pillarAndObstacleMask = 0;
            if (wallLayer >= 0) pillarAndObstacleMask |= (1 << wallLayer);
            if (obsLayer >= 0) pillarAndObstacleMask |= (1 << obsLayer);
            if (pillarAndObstacleMask == 0) pillarAndObstacleMask = ~0;
        }

        public void Initialize(EnemyBrain owner)
        {
            brain = owner;
            nextRushAllowedTime = Time.time + 2.0f;
        }

        public void ResetState()
        {
            rushState = RushSubState.Idle;
            if (brain != null && brain.Motor != null)
                brain.Motor.IsUnstoppable = false;
            if (animator != null)
                animator.SetBool(HashIsCharging, false);
            if (telegraph != null)
                telegraph.HideImmediate();
        }

        public bool TryStartBullRush(Transform target)
        {
            if (brain == null || target == null || brain.Archetype == null) return false;
            if (!brain.Archetype.canBullRush || rushState != RushSubState.Idle) return false;
            if (Time.time < nextRushAllowedTime) return false;

            float dist = brain.Perception.DistanceToTarget;
            if (dist < 4.5f || dist > 12.5f) return false;

            cachedTarget = target;
            lockedChargeDir = (target.position - transform.position);
            lockedChargeDir.y = 0f;
            if (lockedChargeDir.sqrMagnitude < 0.01f) lockedChargeDir = transform.forward;
            lockedChargeDir.Normalize();

            transform.rotation = Quaternion.LookRotation(lockedChargeDir, Vector3.up);
            brain.Combat.CancelCurrentAttack();
            brain.Motor.StopMoving();

            rushState = RushSubState.Windup;
            stateTimer = 0.8f; // GDD Section 6.2: 0.8s ChargeWindup
            nextRushAllowedTime = Time.time + brain.Archetype.chargeCooldown;

            if (animator != null)
                animator.SetTrigger(HashWindup);

            if (telegraph != null)
                telegraph.ShowLine(transform.position, lockedChargeDir, 10.5f, 1.1f, stateTimer);

            return true;
        }

        public void TickBehavior()
        {
            if (brain == null || brain.Archetype == null)
            {
                ResetState();
                return;
            }

            if (rushState == RushSubState.Windup)
            {
                brain.Motor.StopMoving();
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                {
                    if (telegraph != null)
                        telegraph.HideImmediate();

                    rushState = RushSubState.Charging;
                    stateTimer = 1.25f; // Up to ~11m charge at 9 m/s
                    brain.Motor.IsUnstoppable = true;
                    if (animator != null)
                        animator.SetBool(HashIsCharging, true);
                }
                return;
            }

            if (rushState == RushSubState.Charging)
            {
                float chargeSpeed = brain.Archetype.chargeSpeed;
                float stepDist = chargeSpeed * Time.deltaTime;

                // SphereCast 1.0m ahead for StunPillar or Obstacle (GDD Section 6.2)
                Vector3 castOrigin = transform.position + Vector3.up * 0.9f;
                if (Physics.SphereCast(castOrigin, 0.55f, lockedChargeDir, out RaycastHit hit, 1.0f, pillarAndObstacleMask, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider != null && hit.collider.CompareTag("StunPillar"))
                    {
                        ResetState();
                        brain.TriggerStun(brain.Archetype.stunDuration);
                        return;
                    }
                    else if (hit.collider != null)
                    {
                        ResetState();
                        GameEvents.RaiseCameraShake(0.08f, 0.12f);
                        return;
                    }
                }

                // Move forward along locked charge vector
                Vector3 nextPos = brain.Motor.ClampPositionToRoom(transform.position + lockedChargeDir * stepDist);
                if (brain.Motor.Agent != null && brain.Motor.Agent.isOnNavMesh)
                {
                    brain.Motor.Agent.Move(nextPos - transform.position);
                }
                else
                {
                    transform.position = nextPos;
                }
                transform.rotation = Quaternion.LookRotation(lockedChargeDir, Vector3.up);

                // Check Player impact
                if (cachedTarget != null)
                {
                    float distToPlayer = Vector3.Distance(
                        new Vector3(transform.position.x, 0f, transform.position.z),
                        new Vector3(cachedTarget.position.x, 0f, cachedTarget.position.z));

                    if (distToPlayer <= 1.35f)
                    {
                        var playerHealth = cachedTarget.GetComponent<HealthComponent>();
                        if (playerHealth != null && playerHealth.IsAlive)
                        {
                            if (playerHealth.IsInvulnerable)
                            {
                                GameEvents.RaisePlayerDodgeSuccess();
                            }
                            else
                            {
                                playerHealth.TakeDamage(new DamageInfo
                                {
                                    amount = brain.Archetype.chargeDamage,
                                    sourcePosition = transform.position,
                                    knockbackDirection = lockedChargeDir,
                                    knockbackForce = 8.0f,
                                    isCritical = true,
                                    instigator = gameObject
                                });
                            }
                        }
                        ResetState();
                        return;
                    }
                }

                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                {
                    ResetState();
                }
            }
        }
    }
}
