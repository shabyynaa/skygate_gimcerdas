using System;
using UnityEngine;

namespace SkyGate.Core
{
    public class HealthComponent : MonoBehaviour, IDamageable
    {
        [Header("Base Health Settings")]
        [SerializeField] private bool isPlayer;
        [SerializeField] private float maxHP = 100f;
        [SerializeField] private float currentHP = 100f;
        [SerializeField] private float currentShield = 0f;
        [SerializeField] private float maxShield = 50f;

        [Header("Defense & Multipliers")]
        [SerializeField] private float damageReduction = 0f; // -0.25 to +0.45 for player, or 0.35 for Crystal Golem armor
        [SerializeField] private float incomingDamageMultiplier = 1f; // e.g., 1.5x when stunned

        public float MaxHP => maxHP;
        public float CurrentHP => currentHP;
        public float CurrentShield => currentShield;
        public float MaxShield => maxShield;
        public float HPRatio => maxHP > 0f ? Mathf.Clamp01(currentHP / maxHP) : 0f;
        public float DamageReduction => damageReduction;
        public bool IsInvulnerable => Time.time < invulnerableUntil;
        public bool IsAlive => !isDead && currentHP > 0f;
        public bool IsPlayer => isPlayer;

        public event Action<float, float, float> OnHealthUpdated; // currentHP, maxHP, shield
        public event Action<float, float, float> OnHealthChanged
        {
            add => OnHealthUpdated += value;
            remove => OnHealthUpdated -= value;
        }
        public event Action<DamageInfo, float, bool> OnDamaged;   // info, finalDamage, absorbedByShield
        public event Action<float, float> OnHealed;               // hpAdded, shieldAdded
        public event Action<DamageInfo> OnDied;

        private float invulnerableUntil;
        private bool isDead;

        private void Awake()
        {
            currentHP = Mathf.Clamp(currentHP, 1f, maxHP);
        }

        public void Initialize(float initialMaxHP, float initialShield = 0f, float initialDamageReduction = 0f)
        {
            maxHP = Mathf.Max(1f, initialMaxHP);
            currentHP = maxHP;
            currentShield = Mathf.Clamp(initialShield, 0f, maxShield);
            damageReduction = initialDamageReduction;
            incomingDamageMultiplier = 1f;
            isDead = false;
            invulnerableUntil = 0f;
            NotifyHealthChanged();
        }

        public void SetDamageReduction(float reduction)
        {
            damageReduction = Mathf.Clamp(reduction, -0.50f, 0.80f);
        }

        public void SetIncomingDamageMultiplier(float mult)
        {
            incomingDamageMultiplier = Mathf.Max(0.1f, mult);
        }

        public void GrantInvulnerability(float duration)
        {
            invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + duration);
        }

        public void TakeDamage(DamageInfo info)
        {
            if (isDead || currentHP <= 0f) return;

            if (IsInvulnerable)
            {
                if (isPlayer)
                {
                    GameEvents.RaisePlayerDodgeSuccess();
                }
                return;
            }

            float raw = Mathf.Max(1f, info.amount);
            float effectiveReduction = info.bypassArmor ? Mathf.Min(0f, damageReduction) : damageReduction;
            float reduced = raw * (1f - effectiveReduction) * incomingDamageMultiplier;
            float finalDamage = Mathf.Max(1f, Mathf.Round(reduced));

            bool absorbedByShield = false;
            float remainingDamage = finalDamage;

            if (currentShield > 0f)
            {
                absorbedByShield = true;
                if (currentShield >= remainingDamage)
                {
                    currentShield -= remainingDamage;
                    remainingDamage = 0f;
                }
                else
                {
                    remainingDamage -= currentShield;
                    currentShield = 0f;
                }
            }

            if (remainingDamage > 0f)
            {
                currentHP = Mathf.Max(0f, currentHP - remainingDamage);
            }

            OnDamaged?.Invoke(info, finalDamage, absorbedByShield && remainingDamage <= 0f);
            GameEvents.RaiseFloatingDamage(
                transform.position + Vector3.up * 1.6f,
                finalDamage,
                info.isCritical || incomingDamageMultiplier > 1.1f,
                absorbedByShield && remainingDamage <= 0f
            );

            if (isPlayer)
            {
                GameEvents.RaisePlayerDamaged(finalDamage, currentHP, currentShield);
            }

            NotifyHealthChanged();

            if (currentHP <= 0f && !isDead)
            {
                isDead = true;
                OnDied?.Invoke(info);
            }
        }

        /// <summary>
        /// Heals HP; if overflowToShield is true (MathGate HealPercent rule), excess healing converts to Shield up to maxShield (50).
        /// </summary>
        public void Heal(float amount, bool overflowToShield = true)
        {
            if (isDead || amount <= 0f) return;

            float missingHP = maxHP - currentHP;
            float hpAdded = Mathf.Min(amount, missingHP);
            currentHP += hpAdded;

            float excess = amount - hpAdded;
            float shieldAdded = 0f;
            if (overflowToShield && excess > 0f)
            {
                float missingShield = maxShield - currentShield;
                shieldAdded = Mathf.Min(excess, missingShield);
                currentShield += shieldAdded;
            }

            OnHealed?.Invoke(hpAdded, shieldAdded);
            if (isPlayer)
            {
                GameEvents.RaisePlayerHealed(hpAdded, shieldAdded);
            }
            NotifyHealthChanged();
        }

        /// <summary>
        /// Non-lethal direct HP reduction used by MathGate DamageCurrentHP (leaves at least minRemainingHP = 5).
        /// </summary>
        public void ApplyNonLethalGateDamage(float amount, float minRemainingHP = 5f)
        {
            if (isDead || amount <= 0f) return;
            currentHP = Mathf.Max(minRemainingHP, currentHP - amount);
            NotifyHealthChanged();
        }

        /// <summary>
        /// Modifies MaxHP within [50, 250] as specified in GDD Section 4.2 & 5.2.
        /// </summary>
        public void ModifyMaxHP(float delta, float minCap = 50f, float maxCap = 250f)
        {
            float oldMax = maxHP;
            maxHP = Mathf.Clamp(maxHP + delta, minCap, maxCap);
            if (delta > 0f)
            {
                currentHP = Mathf.Min(maxHP, currentHP + (maxHP - oldMax));
            }
            else
            {
                currentHP = Mathf.Min(currentHP, maxHP);
                currentHP = Mathf.Max(5f, currentHP);
            }
            NotifyHealthChanged();
        }

        public void FullRestore()
        {
            isDead = false;
            currentHP = maxHP;
            invulnerableUntil = Time.time + 0.75f;
            NotifyHealthChanged();
        }

        private void NotifyHealthChanged()
        {
            OnHealthUpdated?.Invoke(currentHP, maxHP, currentShield);
            if (isPlayer)
            {
                GameEvents.RaisePlayerHealthChanged(currentHP, maxHP, currentShield);
            }
        }
    }
}
