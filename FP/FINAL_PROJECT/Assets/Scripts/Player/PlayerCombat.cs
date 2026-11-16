using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SkyGate.Core;

namespace SkyGate.Player
{
    [RequireComponent(typeof(PlayerStats))]
    [RequireComponent(typeof(HealthComponent))]
    public class PlayerCombat : MonoBehaviour
    {
        // Animator Parameter Hashes (ASSET_CONTRACT.md Section 5.1)
        private static readonly int HashAttack = Animator.StringToHash("Attack");
        private static readonly int HashComboStep = Animator.StringToHash("AttackStep");

        [Header("3-Hit Combo Settings (GDD 4.1 & 4.3)")]
        [SerializeField] private float hitWindowDelay = 0.15f;
        [SerializeField] private float attackRange = 1.65f;
        [SerializeField] private float attackRadius = 1.25f;
        [SerializeField] private float comboResetWindow = 1.15f;
        [SerializeField] private float finisherMultiplier = 1.4f;
        [SerializeField] private float normalKnockback = 4.0f;
        [SerializeField] private float finisherKnockback = 8.0f;
        [SerializeField] private LayerMask hitLayerMask;

        [Header("Visuals & References")]
        [SerializeField] private Animator animator;
        [SerializeField] private ParticleSystem slashVFX;

        private PlayerStats stats;
        private HealthComponent health;

        private int currentComboStep = 0;
        private float lastAttackTime = -10f;
        private float nextAllowedAttackTime = 0f;
        private Coroutine activeAttackRoutine;
        private readonly Collider[] hitBuffer = new Collider[24];
        private readonly HashSet<GameObject> damagedTargetsThisSwing = new HashSet<GameObject>();

        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
            health = GetComponent<HealthComponent>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            if (hitLayerMask.value == 0)
            {
                int enemyLayer = LayerMask.NameToLayer("Enemy");
                int obstacleLayer = LayerMask.NameToLayer("Obstacle");
                int interactableLayer = LayerMask.NameToLayer("Default");
                hitLayerMask = (enemyLayer >= 0 ? (1 << enemyLayer) : ~0) | (obstacleLayer >= 0 ? (1 << obstacleLayer) : 0) | (1 << interactableLayer);
            }
        }

        private void Start()
        {
            if (GameInputReader.Instance != null)
            {
                GameInputReader.Instance.OnAttackPressed += TryAttack;
            }
        }

        private void OnDestroy()
        {
            if (GameInputReader.HasInstance)
            {
                GameInputReader.PeekInstance.OnAttackPressed -= TryAttack;
            }
        }

        public void TryAttack()
        {
            if (!health.IsAlive || Time.time < nextAllowedAttackTime) return;

            if (Time.time - lastAttackTime > comboResetWindow)
            {
                currentComboStep = 1;
            }
            else
            {
                currentComboStep = (currentComboStep % 3) + 1;
            }

            lastAttackTime = Time.time;
            nextAllowedAttackTime = Time.time + stats.EffectiveAttackCooldown;

            if (animator != null)
            {
                animator.SetInteger(HashComboStep, currentComboStep);
                animator.SetTrigger(HashAttack);
            }

            if (slashVFX != null)
            {
                slashVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                slashVFX.Play();
            }

            GameEvents.RaisePlayerAttackSwung();

            if (activeAttackRoutine != null)
                StopCoroutine(activeAttackRoutine);
            activeAttackRoutine = StartCoroutine(ExecuteHitWindowRoutine(currentComboStep));
        }

        private IEnumerator ExecuteHitWindowRoutine(int step)
        {
            // GDD Section 4.3 & TDD Section 5.1: 0.15s delay before checking hitbox
            yield return new WaitForSeconds(hitWindowDelay);

            if (!health.IsAlive) yield break;

            bool isFinisher = (step == 3);
            float damageMultiplier = isFinisher ? finisherMultiplier : 1.0f;
            float dmgAmount = Mathf.Round(stats.ATK * damageMultiplier);
            float kbForce = isFinisher ? finisherKnockback : normalKnockback;

            Vector3 sphereCenter = transform.position + Vector3.up * 0.9f + transform.forward * (attackRange * 0.65f);
            int hitCount = Physics.OverlapSphereNonAlloc(sphereCenter, attackRadius, hitBuffer, hitLayerMask, QueryTriggerInteraction.Collide);

            damagedTargetsThisSwing.Clear();
            bool connectedAny = false;

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = hitBuffer[i];
                if (col == null || col.gameObject == gameObject) continue;

                IDamageable damageable = col.GetComponentInParent<IDamageable>();
                if (damageable == null || !damageable.IsAlive) continue;

                MonoBehaviour mb = damageable as MonoBehaviour;
                if (mb == null || mb.gameObject == gameObject) continue;
                if (damagedTargetsThisSwing.Contains(mb.gameObject)) continue;

                damagedTargetsThisSwing.Add(mb.gameObject);

                Vector3 kbDir = (mb.transform.position - transform.position);
                kbDir.y = 0f;
                if (kbDir.sqrMagnitude < 0.001f) kbDir = transform.forward;
                kbDir.Normalize();

                DamageInfo info = new DamageInfo
                {
                    amount = dmgAmount,
                    sourcePosition = transform.position,
                    knockbackDirection = kbDir,
                    knockbackForce = kbForce,
                    isCritical = isFinisher,
                    bypassArmor = false,
                    instigator = gameObject
                };

                damageable.TakeDamage(info);
                connectedAny = true;
            }

            if (connectedAny)
            {
                GameEvents.RaiseCameraShake(isFinisher ? 0.12f : 0.06f, 0.10f);
                if (isFinisher)
                {
                    GameEvents.RaiseHitstop(0.04f);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.4f);
            Vector3 sphereCenter = transform.position + Vector3.up * 0.9f + transform.forward * (attackRange * 0.65f);
            Gizmos.DrawWireSphere(sphereCenter, attackRadius);
        }
    }
}
