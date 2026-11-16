using UnityEngine;

namespace SkyGate.EnemyAI.Behaviors
{
    public class RangedKiteBehavior : MonoBehaviour
    {
        private EnemyBrain brain;

        private void Awake()
        {
            brain = GetComponent<EnemyBrain>();
        }

        public void Initialize(EnemyBrain owner)
        {
            brain = owner;
        }

        public void TickRangedKite(Transform target, bool seesPlayer)
        {
            if (brain == null || target == null || brain.Archetype == null) return;

            var arch = brain.Archetype;
            float dist = brain.Perception.DistanceToTarget;

            // Retreat if Player closes within 4.0m (GDD Section 6.2)
            if (dist < arch.retreatDistance)
            {
                Vector3 awayDir = (transform.position - target.position);
                awayDir.y = 0f;
                if (awayDir.sqrMagnitude < 0.01f)
                    awayDir = -transform.forward;
                awayDir.Normalize();

                Vector3 retreatPos = brain.Motor.ClampPositionToRoom(transform.position + awayDir * 4.5f);
                brain.Motor.MoveTo(retreatPos, brain.EffectiveChaseSpeed * 1.05f);
                return;
            }

            // Close gap if beyond maxOptimalDistance (10.0m) or lacking LoS
            if (!seesPlayer || dist > arch.maxOptimalDistance)
            {
                Vector3 dest = seesPlayer ? target.position : brain.Perception.LastSeenPosition;
                brain.Motor.MoveTo(dest, brain.EffectiveChaseSpeed);
                return;
            }

            // Inside optimal range [6.0m - 10.0m] -> strafe slightly or fire projectile
            brain.Motor.StopMoving();
            brain.Motor.FacePosition(target.position, 10f);

            if (brain.Combat.IsCooldownReady && !brain.Combat.IsAttacking)
            {
                brain.Combat.StartRangedAttack(
                    target,
                    brain.EffectiveDamage,
                    arch.projectileSpeed,
                    arch.windupDuration,
                    brain.EffectiveCooldown
                );
            }
        }
    }
}
