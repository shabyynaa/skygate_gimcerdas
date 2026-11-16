using UnityEngine;
using SkyGate.Core;

namespace SkyGate.EnemyAI.Behaviors
{
    public class BossPhaseBehavior : MonoBehaviour
    {
        private static readonly int ShaderBaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ShaderColorId = Shader.PropertyToID("_Color");
        private static readonly int ShaderEmissionId = Shader.PropertyToID("_EmissionColor");

        [Header("Boss Phase Settings")]
        [SerializeField] private string bossTitle = "THE ZEPHYR GUARDIAN";
        [SerializeField] private float phase2ThresholdRatio = 0.50f;
        [SerializeField] private float aoeWaveInterval = 6.5f;
        [SerializeField] private float aoeWaveRadius = 4.6f;
        [SerializeField] private float aoeWaveWindup = 0.90f;
        [SerializeField] private GameObject minionPrefab;
        [SerializeField] private EnemyArchetypeSO minionArchetype;

        private EnemyBrain brain;
        private Renderer[] renderers;
        private MaterialPropertyBlock propBlock;
        private int currentPhase = 1;
        private bool summonedPhase2Adds;
        private float aoeTimer;

        public int CurrentPhase => currentPhase;

        private void Awake()
        {
            brain = GetComponent<EnemyBrain>();
            renderers = GetComponentsInChildren<Renderer>(true);
            propBlock = new MaterialPropertyBlock();
            aoeTimer = aoeWaveInterval;
        }

        public void Initialize(EnemyBrain owner)
        {
            brain = owner;
            currentPhase = 1;
            summonedPhase2Adds = false;
            aoeTimer = aoeWaveInterval;
        }

        public void SetMinionTemplate(GameObject prefab, EnemyArchetypeSO arch)
        {
            minionPrefab = prefab;
            minionArchetype = arch;
        }

        public void ResetState()
        {
            currentPhase = 1;
            summonedPhase2Adds = false;
            aoeTimer = aoeWaveInterval;
            ClearPhaseTint();
            GameEvents.RaiseBossStateUpdated(bossTitle, 0f, 0f, 1);
        }

        public void TickBehavior()
        {
            if (brain == null || brain.Health == null || !brain.Health.IsAlive) return;
            if (brain.CurrentState == EnemyState.Patrol) return;

            // Check Phase 2 transition (GDD Section 6.2: <= 50% HP -> Solar Overdrive)
            if (currentPhase == 1 && brain.Health.HPRatio <= phase2ThresholdRatio)
            {
                EnterPhase2();
            }

            // Periodic AoE Wind Wave (faster in Phase 2)
            aoeTimer -= Time.deltaTime;
            if (aoeTimer <= 0f && !brain.Combat.IsAttacking && brain.CurrentState != EnemyState.Stunned)
            {
                aoeTimer = currentPhase == 2 ? aoeWaveInterval * 0.75f : aoeWaveInterval;
                brain.Combat.StartAoEAttack(
                    transform.position,
                    aoeWaveRadius,
                    brain.EffectiveDamage * 1.15f,
                    aoeWaveWindup,
                    brain.EffectiveCooldown
                );
            }

            BroadcastState();
        }

        private void EnterPhase2()
        {
            currentPhase = 2;
            ApplyPhase2Tint();

            // Increase attack & speed in Solar Overdrive (GDD Section 6.2: speed 6.6 m/s, cooldown 1.05s)
            brain.OverrideCombatStats(
                Mathf.Round(brain.EffectiveDamage * 1.2f),
                1.05f,
                6.6f
            );

            GameEvents.RaiseCameraShake(0.22f, 0.35f);

            // Summon 2 Breeze Imps once at Phase 2 start (GDD Section 6.2)
            if (!summonedPhase2Adds && minionPrefab != null)
            {
                summonedPhase2Adds = true;
                for (int i = 0; i < 2; i++)
                {
                    Vector3 offset = (i == 0 ? Vector3.left : Vector3.right) * 3.2f;
                    Vector3 spawnPos = brain.Motor.ClampPositionToRoom(transform.position + offset);
                    GameObject addObj = Instantiate(minionPrefab, spawnPos, Quaternion.identity);
                    EnemyBrain addBrain = addObj.GetComponent<EnemyBrain>();
                    if (addBrain != null)
                    {
                        if (minionArchetype != null)
                            addBrain.SetupEnemy(minionArchetype, brain.ParentIsland, DifficultyContext.Default);
                        if (brain.ParentIsland != null)
                            brain.ParentIsland.RegisterSpawnedEnemy(addBrain);
                    }
                }
            }
        }

        private void ApplyPhase2Tint()
        {
            if (renderers == null) return;
            Color crimson = new Color(0.90f, 0.22f, 0.27f, 1f); // #E63946
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].GetPropertyBlock(propBlock);
                propBlock.SetColor(ShaderBaseColorId, crimson);
                propBlock.SetColor(ShaderColorId, crimson);
                propBlock.SetColor(ShaderEmissionId, crimson * 0.45f);
                renderers[i].SetPropertyBlock(propBlock);
            }
        }

        private void ClearPhaseTint()
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].SetPropertyBlock(null);
            }
        }

        private void BroadcastState()
        {
            if (brain != null && brain.Health != null)
            {
                GameEvents.RaiseBossStateUpdated(bossTitle, brain.Health.CurrentHP, brain.Health.MaxHP, currentPhase);
            }
        }
    }
}
