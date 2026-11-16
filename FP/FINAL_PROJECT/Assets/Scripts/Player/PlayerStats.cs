using UnityEngine;
using SkyGate.Core;
using SkyGate.MathGate;

namespace SkyGate.Player
{
    [RequireComponent(typeof(HealthComponent))]
    public class PlayerStats : MonoBehaviour
    {
        [Header("Base Stats (GDD 4.2)")]
        [SerializeField] private float baseMaxHP = 100f;
        [SerializeField] private float baseATK = 18f;
        [SerializeField] private float baseWalkSpeed = 6.5f;
        [SerializeField] private float baseAttackCooldown = 0.55f;
        [SerializeField] private int maxLives = 3;

        [Header("Runtime Stats")]
        [SerializeField] private float currentATK = 18f;
        [SerializeField] private float damageReduction = 0f; // -0.25 to +0.45
        [SerializeField] private float moveSpeedMultiplier = 1.0f; // 0.75 to 1.60
        [SerializeField] private float attackCooldownMultiplier = 1.0f; // 0.55 to 1.40
        [SerializeField] private int currentLives = 3;

        [Header("Optional Perks (GDD 5.3)")]
        [SerializeField] private bool hasVampiricBlade;
        [SerializeField] private bool hasStaticDash;

        private HealthComponent health;

        public HealthComponent Health => health;
        public float ATK => currentATK;
        public float DamageReduction => damageReduction;
        public float MoveSpeedMultiplier => moveSpeedMultiplier;
        public float EffectiveWalkSpeed => baseWalkSpeed * moveSpeedMultiplier;
        public float AttackCooldownMultiplier => attackCooldownMultiplier;
        public float EffectiveAttackCooldown => baseAttackCooldown * attackCooldownMultiplier;
        public int CurrentLives => currentLives;
        public int RemainingLives => currentLives;
        public int MaxLives => maxLives;
        public bool HasVampiricBlade => hasVampiricBlade;
        public bool HasStaticDash => hasStaticDash;

        private void Awake()
        {
            health = GetComponent<HealthComponent>();
        }

        private void OnEnable()
        {
            GameEvents.OnEnemyKilled += HandleEnemyKilled;
            if (health == null) health = GetComponent<HealthComponent>();
            if (health != null)
            {
                health.OnHealthChanged += HandleHealthChanged;
            }
        }

        private void OnDisable()
        {
            GameEvents.OnEnemyKilled -= HandleEnemyKilled;
            if (health != null)
            {
                health.OnHealthChanged -= HandleHealthChanged;
            }
        }

        private void HandleHealthChanged(float cur, float max, float shield)
        {
            BroadcastFullStats();
        }

        public void ResetToInitial()
        {
            currentATK = baseATK;
            damageReduction = 0f;
            moveSpeedMultiplier = 1.0f;
            attackCooldownMultiplier = 1.0f;
            currentLives = maxLives;
            hasVampiricBlade = false;
            hasStaticDash = false;

            if (health == null) health = GetComponent<HealthComponent>();
            health.Initialize(baseMaxHP, 0f, damageReduction);

            GameEvents.RaisePlayerLivesChanged(currentLives);
            BroadcastFullStats();
        }

        public void ResetForNewRun()
        {
            ResetToInitial();
        }

        public void RestoreFullHPKeepModifiers()
        {
            if (health == null) health = GetComponent<HealthComponent>();
            health.Initialize(health.MaxHP > 0f ? health.MaxHP : baseMaxHP, health.CurrentShield, damageReduction);
            BroadcastFullStats();
        }

        public bool ConsumeLife()
        {
            currentLives = Mathf.Max(0, currentLives - 1);
            GameEvents.RaisePlayerLivesChanged(currentLives);
            BroadcastFullStats();
            return currentLives > 0;
        }

        public void ApplyMathGateChoice(MathGateModifier positiveMod, MathGateModifier negativeMod)
        {
            ApplyModifierStruct(positiveMod);
            ApplyModifierStruct(negativeMod);
            BroadcastFullStats();
        }

        private void ApplyModifierStruct(MathGateModifier mod)
        {
            if (mod.effectType == MathGateEffectType.None || Mathf.Abs(mod.value) < 0.001f)
                return;

            if (health == null) health = GetComponent<HealthComponent>();

            switch (mod.effectType)
            {
                case MathGateEffectType.HealHP:
                    health.Heal(mod.value, mod.allowOverhealShield);
                    break;

                case MathGateEffectType.TakeFlatDamage:
                    health.ApplyNonLethalGateDamage(Mathf.Abs(mod.value), 5f);
                    break;

                case MathGateEffectType.DamageFlat:
                    currentATK = Mathf.Clamp(currentATK + mod.value, 8f, 100f);
                    break;

                case MathGateEffectType.MaxHPFlat:
                    health.ModifyMaxHP(mod.value, 50f, 250f);
                    break;

                case MathGateEffectType.MoveSpeedPercent:
                    moveSpeedMultiplier = Mathf.Clamp(moveSpeedMultiplier + mod.value, 0.75f, 1.60f);
                    break;

                case MathGateEffectType.AttackSpeedPercent:
                    float factor = mod.value >= 0f ? (1f - mod.value) : (1f + Mathf.Abs(mod.value));
                    attackCooldownMultiplier = Mathf.Clamp(attackCooldownMultiplier * factor, 0.55f, 1.40f);
                    break;

                default:
                    ApplyMathModifier(mod.effectType, mod.value);
                    break;
            }
        }

        public void ApplyMathModifier(MathGateEffectType effectType, float value)
        {
            if (health == null) health = GetComponent<HealthComponent>();

            switch (effectType)
            {
                case MathGateEffectType.HealPercent:
                    float healAmt = health.MaxHP * (value / 100f);
                    health.Heal(healAmt, true);
                    break;

                case MathGateEffectType.DamageCurrentHP:
                    health.ApplyNonLethalGateDamage(Mathf.Abs(value), 5f);
                    break;

                case MathGateEffectType.ModifyMaxHP:
                    health.ModifyMaxHP(value, 50f, 250f);
                    break;

                case MathGateEffectType.MultiplyATK:
                    currentATK = Mathf.Clamp(Mathf.Round(currentATK * value), 8f, 100f);
                    break;

                case MathGateEffectType.DivideATK:
                    float divisor = Mathf.Max(0.1f, value);
                    currentATK = Mathf.Clamp(Mathf.Round(currentATK / divisor), 8f, 100f);
                    break;

                case MathGateEffectType.ModifySpeedPct:
                    moveSpeedMultiplier = Mathf.Clamp(moveSpeedMultiplier + (value / 100f), 0.75f, 1.60f);
                    break;

                case MathGateEffectType.ModifyCooldownPct:
                    float factor = value >= 0f ? (1f - (value / 100f)) : (1f + (Mathf.Abs(value) / 100f));
                    attackCooldownMultiplier = Mathf.Clamp(attackCooldownMultiplier * factor, 0.55f, 1.40f);
                    break;

                case MathGateEffectType.ModifyDEF:
                    damageReduction = Mathf.Clamp(damageReduction + (value / 100f), -0.25f, 0.45f);
                    health.SetDamageReduction(damageReduction);
                    break;
            }

            BroadcastFullStats();
        }

        public void GrantRandomPerk(System.Random rng)
        {
            if (!hasVampiricBlade && (hasStaticDash || (rng != null ? rng.NextDouble() < 0.5 : Random.value < 0.5f)))
            {
                hasVampiricBlade = true;
                GameEvents.RaisePerkAcquired("Vampiric Blade (+3 HP / Kill)");
            }
            else if (!hasStaticDash)
            {
                hasStaticDash = true;
                GameEvents.RaisePerkAcquired("Static Dash (Shockwave Dodge)");
            }
            else
            {
                health.Heal(20f, true);
                GameEvents.RaisePerkAcquired("Sky Aegis (+20 Shield)");
            }
            BroadcastFullStats();
        }

        private void BroadcastFullStats()
        {
            GameEvents.RaisePlayerStatsChanged();
            if (health != null)
            {
                float aspdMult = attackCooldownMultiplier > 0.01f ? (1f / attackCooldownMultiplier) : 1f;
                GameEvents.RaisePlayerStatsDetailed(
                    health.CurrentHP,
                    health.MaxHP,
                    health.CurrentShield,
                    currentLives,
                    currentATK,
                    EffectiveWalkSpeed,
                    aspdMult
                );
            }
        }

        private void HandleEnemyKilled(SkyGate.EnemyAI.EnemyBrain enemy)
        {
            if (hasVampiricBlade && health != null && health.IsAlive)
            {
                health.Heal(3f, true);
            }
        }
    }
}
