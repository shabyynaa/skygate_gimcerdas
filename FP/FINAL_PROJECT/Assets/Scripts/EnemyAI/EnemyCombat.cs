using System.Collections;
using UnityEngine;
using SkyGate.Core;
using SkyGate.Combat;

namespace SkyGate.EnemyAI
{
    public class EnemyCombat : MonoBehaviour
    {
        // Animator Parameter Hashes (ASSET_CONTRACT.md Section 5.2)
        private static readonly int HashWindup = Animator.StringToHash("Windup");
        private static readonly int HashAttack = Animator.StringToHash("Attack");

        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private AttackTelegraph telegraph;
        [SerializeField] private ProjectileController projectilePrefab;
        [SerializeField] private Transform projectileSpawnPoint;

        private EnemyBrain brain;
        private float nextAttackAllowedTime;
        private Coroutine activeAttackRoutine;
        private bool isAttacking;

        public bool IsAttacking => isAttacking;
        public bool IsCooldownReady => Time.time >= nextAttackAllowedTime;

        private void Awake()
        {
            brain = GetComponent<EnemyBrain>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            if (telegraph == null)
                telegraph = GetComponentInChildren<AttackTelegraph>(true);
        }

        public void SetProjectilePrefab(ProjectileController prefab)
        {
            projectilePrefab = prefab;
        }

        public void CancelCurrentAttack()
        {
            if (activeAttackRoutine != null)
            {
                StopCoroutine(activeAttackRoutine);
                activeAttackRoutine = null;
            }
            isAttacking = false;
            if (telegraph != null)
                telegraph.HideImmediate();
        }

        public void StartMeleeAttack(Transform target, float damage, float range, float windupDuration, float cooldown, float lifeSteal = 0f)
        {
            if (isAttacking || Time.time < nextAttackAllowedTime) return;
            activeAttackRoutine = StartCoroutine(MeleeAttackRoutine(target, damage, range, windupDuration, cooldown, lifeSteal));
        }

        public void StartRangedAttack(Transform target, float damage, float projSpeed, float windupDuration, float cooldown)
        {
            if (isAttacking || Time.time < nextAttackAllowedTime) return;
            activeAttackRoutine = StartCoroutine(RangedAttackRoutine(target, damage, projSpeed, windupDuration, cooldown));
        }

        public void StartAoEAttack(Vector3 center, float radius, float damage, float windupDuration, float cooldown)
        {
            if (isAttacking || Time.time < nextAttackAllowedTime) return;
            activeAttackRoutine = StartCoroutine(AoEAttackRoutine(center, radius, damage, windupDuration, cooldown));
        }

        private IEnumerator MeleeAttackRoutine(Transform target, float damage, float range, float windupDuration, float cooldown, float lifeSteal)
        {
            isAttacking = true;
            nextAttackAllowedTime = Time.time + windupDuration + cooldown;

            if (animator != null)
                animator.SetTrigger(HashWindup);

            Vector3 strikePos = target != null ? target.position : transform.position + transform.forward * (range * 0.7f);
            if (telegraph != null)
            {
                telegraph.ShowCircle(strikePos, Mathf.Max(1.3f, range * 0.65f), windupDuration);
            }

            float elapsed = 0f;
            while (elapsed < windupDuration)
            {
                if (brain != null && (brain.CurrentState == EnemyState.Stunned || brain.CurrentState == EnemyState.Dead))
                {
                    CancelCurrentAttack();
                    yield break;
                }
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (telegraph != null)
                telegraph.HideImmediate();

            if (animator != null)
                animator.SetTrigger(HashAttack);

            if (target != null)
            {
                float dist = Vector3.Distance(new Vector3(transform.position.x, 0f, transform.position.z), new Vector3(target.position.x, 0f, target.position.z));
                var playerHealth = target.GetComponent<HealthComponent>();

                if (dist <= range + 0.55f && playerHealth != null && playerHealth.IsAlive)
                {
                    if (playerHealth.IsInvulnerable)
                    {
                        GameEvents.RaisePlayerDodgeSuccess();
                    }
                    else
                    {
                        Vector3 kbDir = (target.position - transform.position).normalized;
                        kbDir.y = 0f;
                        playerHealth.TakeDamage(new DamageInfo
                        {
                            amount = damage,
                            sourcePosition = transform.position,
                            knockbackDirection = kbDir,
                            knockbackForce = 4.0f,
                            isCritical = false,
                            instigator = gameObject
                        });

                        if (lifeSteal > 0f && brain != null && brain.Health != null)
                        {
                            brain.Health.Heal(lifeSteal, false);
                        }
                    }
                }
                else
                {
                    // Player stepped out of telegraph before strike landed -> successful dodge/evasion!
                    GameEvents.RaisePlayerDodgeSuccess();
                }
            }

            yield return new WaitForSeconds(0.25f);
            isAttacking = false;
            activeAttackRoutine = null;
        }

        private IEnumerator RangedAttackRoutine(Transform target, float damage, float projSpeed, float windupDuration, float cooldown)
        {
            isAttacking = true;
            nextAttackAllowedTime = Time.time + windupDuration + cooldown;

            if (animator != null)
                animator.SetTrigger(HashWindup);

            Vector3 spawnPos = projectileSpawnPoint != null ? projectileSpawnPoint.position : transform.position + Vector3.up * 1.1f + transform.forward * 0.6f;
            Vector3 aimDir = target != null ? (target.position - transform.position) : transform.forward;
            aimDir.y = 0f;
            if (aimDir.sqrMagnitude < 0.001f) aimDir = transform.forward;
            aimDir.Normalize();

            if (telegraph != null)
            {
                telegraph.ShowLine(transform.position, aimDir, 10f, 0.35f, windupDuration);
            }

            float elapsed = 0f;
            while (elapsed < windupDuration)
            {
                if (brain != null && (brain.CurrentState == EnemyState.Stunned || brain.CurrentState == EnemyState.Dead))
                {
                    CancelCurrentAttack();
                    yield break;
                }
                if (target != null && elapsed < windupDuration * 0.5f)
                {
                    aimDir = (target.position - transform.position);
                    aimDir.y = 0f;
                    if (aimDir.sqrMagnitude > 0.001f) aimDir.Normalize();
                    transform.rotation = Quaternion.LookRotation(aimDir, Vector3.up);
                }
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (telegraph != null)
                telegraph.HideImmediate();

            if (animator != null)
                animator.SetTrigger(HashAttack);

            if (projectilePrefab != null)
            {
                spawnPos = projectileSpawnPoint != null ? projectileSpawnPoint.position : transform.position + Vector3.up * 1.1f + aimDir * 0.7f;
                ProjectileController proj = Instantiate(projectilePrefab, spawnPos, Quaternion.LookRotation(aimDir, Vector3.up));
                proj.gameObject.SetActive(true);
                proj.Initialize(aimDir, damage, projSpeed, gameObject);
            }

            yield return new WaitForSeconds(0.25f);
            isAttacking = false;
            activeAttackRoutine = null;
        }

        private IEnumerator AoEAttackRoutine(Vector3 center, float radius, float damage, float windupDuration, float cooldown)
        {
            isAttacking = true;
            nextAttackAllowedTime = Time.time + windupDuration + cooldown;

            if (animator != null)
                animator.SetTrigger(HashWindup);

            if (telegraph != null)
            {
                telegraph.ShowCircle(center, radius, windupDuration);
            }

            float elapsed = 0f;
            while (elapsed < windupDuration)
            {
                if (brain != null && (brain.CurrentState == EnemyState.Stunned || brain.CurrentState == EnemyState.Dead))
                {
                    CancelCurrentAttack();
                    yield break;
                }
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (telegraph != null)
                telegraph.HideImmediate();

            if (animator != null)
                animator.SetTrigger(HashAttack);

            GameEvents.RaiseCameraShake(0.14f, 0.18f);

            Transform targetTransform = (brain != null && brain.Perception != null) ? brain.Perception.TargetTransform : null;
            if (targetTransform != null)
            {
                float dist = Vector3.Distance(new Vector3(center.x, 0f, center.z), new Vector3(targetTransform.position.x, 0f, targetTransform.position.z));
                var playerHealth = targetTransform.GetComponent<HealthComponent>();
                if (dist <= radius && playerHealth != null && playerHealth.IsAlive)
                {
                    if (playerHealth.IsInvulnerable)
                    {
                        GameEvents.RaisePlayerDodgeSuccess();
                    }
                    else
                    {
                        Vector3 kbDir = (targetTransform.position - center).normalized;
                        kbDir.y = 0f;
                        playerHealth.TakeDamage(new DamageInfo
                        {
                            amount = damage,
                            sourcePosition = center,
                            knockbackDirection = kbDir,
                            knockbackForce = 7.0f,
                            isCritical = true,
                            instigator = gameObject
                        });
                    }
                }
                else if (dist <= radius + 2.5f)
                {
                    GameEvents.RaisePlayerDodgeSuccess();
                }
            }

            yield return new WaitForSeconds(0.35f);
            isAttacking = false;
            activeAttackRoutine = null;
        }
    }
}
