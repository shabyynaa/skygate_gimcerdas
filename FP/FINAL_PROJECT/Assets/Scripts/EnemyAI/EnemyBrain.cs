using System.Collections.Generic;
using UnityEngine;
using SkyGate.Core;
using SkyGate.EnemyAI.Behaviors;
using SkyGate.World;

namespace SkyGate.EnemyAI
{
    public enum EnemyState
    {
        Patrol,
        Chase,
        Windup,
        Attack,
        Reposition,
        Stunned,
        Dead
    }

    [RequireComponent(typeof(HealthComponent))]
    [RequireComponent(typeof(EnemyPerception))]
    [RequireComponent(typeof(EnemyMotor))]
    [RequireComponent(typeof(EnemyCombat))]
    public class EnemyBrain : MonoBehaviour, IDifficultyScalable
    {
        // Animator Parameter Hashes (ASSET_CONTRACT.md Section 5.2)
        private static readonly int HashIsStunned = Animator.StringToHash("IsStunned");
        private static readonly int HashHit = Animator.StringToHash("Hit");
        private static readonly int HashDie = Animator.StringToHash("Die");
        private static readonly int HashIsDead = Animator.StringToHash("IsDead");

        [Header("Archetype Data")]
        [SerializeField] private EnemyArchetypeSO archetype;
        [SerializeField] private Animator animator;

        private HealthComponent health;
        private EnemyPerception perception;
        private EnemyMotor motor;
        private EnemyCombat combat;

        // Optional tactical behaviors
        private RangedKiteBehavior rangedKite;
        private PhotophobiaBehavior photophobia;
        private BullRushBehavior bullRush;
        private BossPhaseBehavior bossPhase;

        private EnemyState currentState = EnemyState.Patrol;
        private Vector3 initialSpawnPos;
        private Quaternion initialSpawnRot;
        private readonly List<Vector3> patrolWaypoints = new List<Vector3>();
        private int currentWaypointIndex;
        private float waypointWaitTimer;

        // Effective scaled stats (DDA)
        private float effectiveMaxHP;
        private float effectiveDamage;
        private float effectiveCooldown;
        private float effectiveChaseSpeed;

        // Memory & Reposition state
        private float losMemoryTimer;
        private bool hasFledOnce;
        private float fleeTimer;
        private Vector3 fleeCoverTarget;
        private float stunRemainingTimer;
        private IslandNode parentIsland;

        public EnemyArchetypeSO Archetype => archetype;
        public EnemyState CurrentState => currentState;
        public HealthComponent Health => health;
        public EnemyPerception Perception => perception;
        public EnemyMotor Motor => motor;
        public EnemyCombat Combat => combat;
        public IslandNode ParentIsland => parentIsland;

        public float EffectiveDamage => effectiveDamage;
        public float EffectiveCooldown => effectiveCooldown;
        public float EffectiveChaseSpeed => effectiveChaseSpeed;

        private void EnsureComponentsCached()
        {
            if (health == null) health = GetComponent<HealthComponent>();
            if (perception == null) perception = GetComponent<EnemyPerception>();
            if (motor == null) motor = GetComponent<EnemyMotor>();
            if (combat == null) combat = GetComponent<EnemyCombat>();

            if (rangedKite == null) rangedKite = GetComponent<RangedKiteBehavior>();
            if (photophobia == null) photophobia = GetComponent<PhotophobiaBehavior>();
            if (bullRush == null) bullRush = GetComponent<BullRushBehavior>();
            if (bossPhase == null) bossPhase = GetComponent<BossPhaseBehavior>();

            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            if (animator != null)
                animator.applyRootMotion = false;
        }

        private void Awake()
        {
            EnsureComponentsCached();

            initialSpawnPos = transform.position;
            initialSpawnRot = transform.rotation;

            if (archetype != null)
            {
                ApplyDifficulty(DifficultyContext.Default);
            }
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.OnDamaged += HandleDamaged;
                health.OnDied += HandleDied;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.OnDamaged -= HandleDamaged;
                health.OnDied -= HandleDied;
            }
        }

        public void SetupEnemy(EnemyArchetypeSO data, IslandNode island, DifficultyContext ddaCtx, List<Vector3> waypoints = null)
        {
            EnsureComponentsCached();
            archetype = data;
            parentIsland = island;
            initialSpawnPos = transform.position;
            initialSpawnRot = transform.rotation;

            if (island != null)
            {
                motor.SetRoomBounds(island.RoomBounds);
            }

            patrolWaypoints.Clear();
            if (waypoints != null && waypoints.Count > 0)
            {
                patrolWaypoints.AddRange(waypoints);
            }
            else
            {
                patrolWaypoints.Add(initialSpawnPos);
                patrolWaypoints.Add(motor.ClampPositionToRoom(initialSpawnPos + new Vector3(3.5f, 0f, 2.5f)));
                patrolWaypoints.Add(motor.ClampPositionToRoom(initialSpawnPos + new Vector3(-3.5f, 0f, -2.5f)));
            }

            ApplyDifficulty(ddaCtx);
            perception.Initialize(archetype, null);

            if (rangedKite != null) rangedKite.Initialize(this);
            if (photophobia != null) photophobia.Initialize(this);
            if (bullRush != null) bullRush.Initialize(this);
            if (bossPhase != null) bossPhase.Initialize(this);
        }

        public void ApplyDifficulty(DifficultyContext ctx)
        {
            EnsureComponentsCached();
            if (archetype == null) return;

            float hpMult = ctx.hpMultiplier > 0.1f ? ctx.hpMultiplier : 1.0f;
            float dmgMult = ctx.damageMultiplier > 0.1f ? ctx.damageMultiplier : 1.0f;
            float aggMult = ctx.aggressionMultiplier > 0.1f ? ctx.aggressionMultiplier : 1.0f;
            float spdMult = ctx.speedMultiplier > 0.1f ? ctx.speedMultiplier : 1.0f;

            effectiveMaxHP = Mathf.Round(archetype.maxHP * hpMult);
            effectiveDamage = Mathf.Round(archetype.baseDamage * dmgMult);
            effectiveCooldown = archetype.attackCooldown / aggMult;
            effectiveChaseSpeed = archetype.chaseSpeed * Mathf.Min(1.15f, spdMult);

            float armor = archetype.hasArmor ? archetype.armorReduction : 0f;
            health.Initialize(effectiveMaxHP, 0f, armor);
        }

        public void OverrideCombatStats(float newDamage, float newCooldown, float newChaseSpeed)
        {
            effectiveDamage = newDamage;
            effectiveCooldown = newCooldown;
            effectiveChaseSpeed = newChaseSpeed;
        }

        public void SetArmorActive(bool active)
        {
            if (archetype == null) return;
            health.SetDamageReduction(active && archetype.hasArmor ? archetype.armorReduction : 0f);
        }

        public void TriggerStun(float duration)
        {
            if (currentState == EnemyState.Dead) return;

            combat.CancelCurrentAttack();
            motor.StopMoving();
            currentState = EnemyState.Stunned;
            stunRemainingTimer = duration;
            health.SetIncomingDamageMultiplier(archetype != null ? archetype.stunDamageTakenMultiplier : 1.5f);

            if (animator != null)
                animator.SetBool(HashIsStunned, true);

            GameEvents.RaiseHitstop(0.05f);
            GameEvents.RaiseCameraShake(0.12f, 0.2f);
        }

        public void ResetToSpawn()
        {
            gameObject.SetActive(true);
            combat.CancelCurrentAttack();
            motor.WarpTo(initialSpawnPos, initialSpawnRot);
            currentState = EnemyState.Patrol;
            hasFledOnce = false;
            fleeTimer = 0f;
            stunRemainingTimer = 0f;
            losMemoryTimer = 0f;

            float armor = (archetype != null && archetype.hasArmor) ? archetype.armorReduction : 0f;
            health.Initialize(effectiveMaxHP > 0 ? effectiveMaxHP : (archetype != null ? archetype.maxHP : 70f), 0f, armor);
            health.SetIncomingDamageMultiplier(1f);

            if (animator != null)
            {
                animator.SetBool(HashIsDead, false);
                animator.SetBool(HashIsStunned, false);
            }

            if (bullRush != null) bullRush.ResetState();
            if (bossPhase != null) bossPhase.ResetState();
        }

        private void Update()
        {
            if (currentState == EnemyState.Dead || archetype == null || !health.IsAlive)
                return;

            // Stunned state check
            if (currentState == EnemyState.Stunned)
            {
                stunRemainingTimer -= Time.deltaTime;
                if (stunRemainingTimer <= 0f)
                {
                    health.SetIncomingDamageMultiplier(1f);
                    if (animator != null)
                        animator.SetBool(HashIsStunned, false);
                    currentState = EnemyState.Chase;
                }
                return;
            }

            // Bull Rush override for Crystal Golem
            if (bullRush != null && bullRush.IsBusy)
            {
                bullRush.TickBehavior();
                return;
            }

            // Boss Phase 2 update
            if (bossPhase != null)
            {
                bossPhase.TickBehavior();
            }

            bool isPursuing = (currentState == EnemyState.Chase || currentState == EnemyState.Attack || currentState == EnemyState.Windup || currentState == EnemyState.Reposition);
            bool seesPlayer = perception.EvaluatePerception(isPursuing);

            if (seesPlayer)
            {
                losMemoryTimer = archetype.losMemoryDuration;
                perception.SetAlertVisual(true);
            }
            else if (losMemoryTimer > 0f)
            {
                losMemoryTimer -= Time.deltaTime;
            }

            // Check Ambush Flanker Flee condition (Breeze Imp: HP <= 25%, max 2.5s reposition behind nearest pillar)
            if (archetype.canFlee && !hasFledOnce && health.HPRatio <= archetype.fleeThresholdRatio && currentState != EnemyState.Reposition)
            {
                hasFledOnce = true;
                fleeTimer = archetype.maxFleeDuration;
                fleeCoverTarget = FindNearestCoverPosition();
                combat.CancelCurrentAttack();
                currentState = EnemyState.Reposition;
            }

            switch (currentState)
            {
                case EnemyState.Patrol:
                    TickPatrol(seesPlayer);
                    break;

                case EnemyState.Chase:
                    TickChase(seesPlayer);
                    break;

                case EnemyState.Windup:
                case EnemyState.Attack:
                    TickAttackState();
                    break;

                case EnemyState.Reposition:
                    TickReposition();
                    break;
            }
        }

        private void TickPatrol(bool seesPlayer)
        {
            perception.SetAlertVisual(false);

            if (seesPlayer)
            {
                currentState = EnemyState.Chase;
                return;
            }

            if (patrolWaypoints.Count == 0) return;

            Vector3 wp = patrolWaypoints[currentWaypointIndex];
            float dist = Vector3.Distance(new Vector3(transform.position.x, 0f, transform.position.z), new Vector3(wp.x, 0f, wp.z));

            if (dist <= 1.1f)
            {
                waypointWaitTimer += Time.deltaTime;
                motor.StopMoving();
                if (waypointWaitTimer >= 1.2f)
                {
                    waypointWaitTimer = 0f;
                    currentWaypointIndex = (currentWaypointIndex + 1) % patrolWaypoints.Count;
                }
            }
            else
            {
                motor.MoveTo(wp, archetype.patrolSpeed);
            }
        }

        private void TickChase(bool seesPlayer)
        {
            if (!seesPlayer && losMemoryTimer <= 0f)
            {
                currentState = EnemyState.Patrol;
                return;
            }

            Transform target = perception.TargetTransform;
            if (target == null) return;

            // Photophobia behavior override (Dusk Wisp stalks outside SunBeacon light zone)
            if (photophobia != null && photophobia.TryHandlePhotophobia(target, effectiveChaseSpeed))
            {
                return;
            }

            // Bull Rush check for Crystal Golem
            if (bullRush != null && bullRush.TryStartBullRush(target))
            {
                return;
            }

            // Ranged Kite behavior override (Sky Sprite keeps 6m-10m distance and retreats if < 4m)
            if (rangedKite != null)
            {
                rangedKite.TickRangedKite(target, seesPlayer);
                return;
            }

            float dist = perception.DistanceToTarget;
            if (seesPlayer && dist <= archetype.attackRange)
            {
                motor.StopMoving();
                motor.FacePosition(target.position);
                if (combat.IsCooldownReady)
                {
                    currentState = EnemyState.Windup;
                    float lifeSteal = (photophobia != null) ? photophobia.GetEffectiveLifeSteal() : 0f;
                    combat.StartMeleeAttack(target, effectiveDamage, archetype.attackRange, archetype.windupDuration, effectiveCooldown, lifeSteal);
                }
            }
            else
            {
                Vector3 dest = seesPlayer ? target.position : perception.LastSeenPosition;
                motor.MoveTo(dest, effectiveChaseSpeed);
            }
        }

        private void TickAttackState()
        {
            motor.StopMoving();
            if (perception.TargetTransform != null)
            {
                motor.FacePosition(perception.TargetTransform.position, 6f);
            }

            if (!combat.IsAttacking)
            {
                currentState = EnemyState.Chase;
            }
        }

        private void TickReposition()
        {
            fleeTimer -= Time.deltaTime;
            motor.MoveTo(fleeCoverTarget, effectiveChaseSpeed * 1.15f);

            float dist = Vector3.Distance(new Vector3(transform.position.x, 0f, transform.position.z), new Vector3(fleeCoverTarget.x, 0f, fleeCoverTarget.z));
            if (fleeTimer <= 0f || dist <= 1.2f)
            {
                currentState = EnemyState.Chase;
            }
        }

        private Vector3 FindNearestCoverPosition()
        {
            Transform player = perception.TargetTransform;
            Vector3 awayDir = player != null ? (transform.position - player.position).normalized : -transform.forward;
            awayDir.y = 0f;
            if (awayDir.sqrMagnitude < 0.01f) awayDir = -transform.forward;

            Collider[] cols = Physics.OverlapSphere(transform.position, 9f);
            float bestDist = float.MaxValue;
            Vector3 bestCover = motor.ClampPositionToRoom(transform.position + awayDir * 5.0f);

            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null && (cols[i].CompareTag("StunPillar") || cols[i].CompareTag("Obstacle")))
                {
                    float d = (cols[i].transform.position - transform.position).sqrMagnitude;
                    if (d < bestDist)
                    {
                        bestDist = d;
                        Vector3 behindPillar = cols[i].transform.position + awayDir * 2.0f;
                        bestCover = motor.ClampPositionToRoom(behindPillar);
                    }
                }
            }
            return bestCover;
        }

        private void HandleDamaged(DamageInfo info, float finalDamage, bool absorbed)
        {
            if (!health.IsAlive) return;

            bool wasStunned = (currentState == EnemyState.Stunned);
            GameEvents.RaiseEnemyDamaged(this, info, wasStunned);

            if (currentState == EnemyState.Patrol)
            {
                losMemoryTimer = archetype != null ? archetype.losMemoryDuration : 2.0f;
                currentState = EnemyState.Chase;
            }

            if (currentState != EnemyState.Stunned && (bullRush == null || !bullRush.IsBusy) && archetype != null && archetype.roleType != EnemyRoleType.Boss)
            {
                if (animator != null)
                    animator.SetTrigger(HashHit);

                float kbDist = info.isCritical ? 0.8f : 0.4f; // GDD Section 4.3: 0.4m normal, 0.8m finisher
                motor.ApplyKnockbackAndStagger(info.knockbackDirection, kbDist, 0.18f);
            }
        }

        private void HandleDied(DamageInfo info)
        {
            currentState = EnemyState.Dead;
            combat.CancelCurrentAttack();
            motor.StopMoving();
            if (motor.Agent != null) motor.Agent.enabled = false;

            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            if (animator != null)
            {
                animator.SetBool(HashIsDead, true);
                animator.SetTrigger(HashDie);
            }

            if (bossPhase != null)
            {
                GameEvents.RaiseBossStateUpdated(archetype != null ? archetype.enemyName : "BOSS", 0f, effectiveMaxHP, bossPhase.CurrentPhase);
                GameEvents.RaiseBossDefeated();
            }

            GameEvents.RaiseEnemyKilled(this);
            Destroy(gameObject, 2.2f);
        }
    }
}
