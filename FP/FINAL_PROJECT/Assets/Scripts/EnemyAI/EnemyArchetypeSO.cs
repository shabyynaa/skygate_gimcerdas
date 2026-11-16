using UnityEngine;

namespace SkyGate.EnemyAI
{
    public enum EnemyRoleType
    {
        MeleeRusher = 0,      // Breeze Imp
        RangedShooter = 1,    // Sky Sprite
        LightHunter = 2,      // Dusk Wisp
        HeavyBrute = 3,       // Crystal Golem
        Boss = 4,             // Zephyr Guardian

        // Alias values for GDD/TDD naming
        MeleeFlanker = 0,
        RangedKiter = 1,
        ShadowStalker = 2,
        HeavyBruiser = 3
    }

    [CreateAssetMenu(fileName = "EA_EnemyArchetype", menuName = "SkyGate/Enemy Archetype SO")]
    public class EnemyArchetypeSO : ScriptableObject
    {
        [Header("Identity")]
        public string archetypeName = "Breeze Imp";
        public EnemyRoleType roleType = EnemyRoleType.MeleeRusher;

        public string enemyName
        {
            get => archetypeName;
            set => archetypeName = value;
        }

        [Header("Core Stats (GDD 7.2)")]
        public float maxHP = 70f;
        public float patrolSpeed = 2.0f;
        public float chaseSpeed = 6.0f;
        public float baseDamage = 10f;
        public float attackRange = 2.2f;
        public float windupDuration = 0.35f;
        public float attackCooldown = 1.3f;

        [Header("Perception (GDD 7.1 & 7.2)")]
        public float detectRadius = 11f;
        public float fovAngle = 140f;
        public float closeAlertRadius = 2.2f;
        public float pursuitPersistenceMultiplier = 1.4f;
        public float losMemoryDuration = 2.0f;

        public float detectionRadius
        {
            get => detectRadius;
            set => detectRadius = value;
        }

        [Header("Armor & Flee (Imp & Golem)")]
        public bool hasArmor = false;
        public float armorReduction = 0.35f;
        public bool canFlee = true;
        public float fleeThresholdRatio = 0.25f;
        public float maxFleeDuration = 2.5f;

        [Header("Ranged Kite Settings (Sky Sprite)")]
        public bool isRanged = false;
        public float idealMinDistance = 6.0f;
        public float idealMaxDistance = 10.0f;
        public float kiteRetreatDistance = 4.0f;
        public float projectileSpeed = 11.0f;

        public float minOptimalDistance
        {
            get => idealMinDistance;
            set => idealMinDistance = value;
        }

        public float maxOptimalDistance
        {
            get => idealMaxDistance;
            set => idealMaxDistance = value;
        }

        public float retreatDistance
        {
            get => kiteRetreatDistance;
            set => kiteRetreatDistance = value;
        }

        [Header("Photophobia Settings (Dusk Wisp)")]
        public bool avoidsSunBeacon = false;
        public float lifeStealAmount = 5.0f;

        [Header("Bull Rush Settings (Crystal Golem)")]
        public bool canBullRush = false;
        public float chargeMinDistance = 5.0f;
        public float chargeMaxDistance = 12.0f;
        public float chargeCooldown = 7.0f;
        public float chargeWindupDuration = 0.8f;
        public float chargeSpeed = 9.0f;
        public float chargeDamage = 26f;
        public float stunDuration = 3.5f;
        public float stunDamageTakenMultiplier = 1.5f;

        [Header("Boss Phase 2 Settings (Zephyr Guardian)")]
        public float phase2Speed = 6.6f;
        public float phase2Damage = 20f;
        public float phase2Cooldown = 1.05f;
        public float phase2AoEWindup = 0.9f;
        public float phase2AoERadius = 4.2f;
    }
}
