using System;
using System.Collections;
using UnityEngine;
using SkyGate.Core;
using SkyGate.Combat;

namespace SkyGate.World.Interactables
{
    [RequireComponent(typeof(HealthComponent))]
    public class StormCrystal : MonoBehaviour
    {
        [Header("Storm Crystal Settings")]
        [SerializeField] private float crystalMaxHP = 50f;
        [SerializeField] private float lightningInterval = 5.5f;
        [SerializeField] private float lightningWindup = 1.15f;
        [SerializeField] private float lightningRadius = 2.4f;
        [SerializeField] private float lightningDamage = 14f;
        [SerializeField] private AttackTelegraph telegraph;
        [SerializeField] private GameObject intactVisual;

        private HealthComponent health;
        private Transform playerTarget;
        private float strikeTimer;
        private bool roomActive;

        public event Action<StormCrystal> OnCrystalDestroyed;
        public bool IsDestroyed => health != null && !health.IsAlive;
        public HealthComponent Health => health;

        private void Awake()
        {
            health = GetComponent<HealthComponent>();
            health.Initialize(crystalMaxHP, 0f, 0f);
            if (telegraph == null)
                telegraph = GetComponentInChildren<AttackTelegraph>(true);
            strikeTimer = lightningInterval;
        }

        private void OnEnable()
        {
            if (health != null)
                health.OnDied += HandleDied;
        }

        private void OnDisable()
        {
            if (health != null)
                health.OnDied -= HandleDied;
        }

        public void SetRoomActive(bool active, Transform player)
        {
            roomActive = active;
            playerTarget = player;
            strikeTimer = lightningInterval * 0.75f;
        }

        public void ResetCrystal()
        {
            StopAllCoroutines();
            gameObject.SetActive(true);
            if (intactVisual != null) intactVisual.SetActive(true);
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = true;
            if (telegraph != null) telegraph.HideImmediate();
            health.Initialize(crystalMaxHP, 0f, 0f);
            strikeTimer = lightningInterval;
        }

        private void Update()
        {
            if (!roomActive || IsDestroyed || playerTarget == null) return;

            strikeTimer -= Time.deltaTime;
            if (strikeTimer <= 0f)
            {
                strikeTimer = lightningInterval;
                StartCoroutine(LightningStrikeRoutine(playerTarget.position));
            }
        }

        private IEnumerator LightningStrikeRoutine(Vector3 strikePos)
        {
            if (telegraph != null)
            {
                telegraph.ShowCircle(strikePos, lightningRadius, lightningWindup);
            }

            float elapsed = 0f;
            while (elapsed < lightningWindup)
            {
                if (IsDestroyed || !roomActive)
                {
                    if (telegraph != null) telegraph.HideImmediate();
                    yield break;
                }
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (telegraph != null)
                telegraph.HideImmediate();

            GameEvents.RaiseCameraShake(0.10f, 0.14f);

            if (playerTarget != null)
            {
                float dist = Vector3.Distance(
                    new Vector3(strikePos.x, 0f, strikePos.z),
                    new Vector3(playerTarget.position.x, 0f, playerTarget.position.z));

                var playerHealth = playerTarget.GetComponent<HealthComponent>();
                if (dist <= lightningRadius && playerHealth != null && playerHealth.IsAlive)
                {
                    if (playerHealth.IsInvulnerable)
                    {
                        GameEvents.RaisePlayerDodgeSuccess();
                    }
                    else
                    {
                        playerHealth.TakeDamage(new DamageInfo
                        {
                            amount = lightningDamage,
                            sourcePosition = strikePos,
                            knockbackDirection = Vector3.up,
                            knockbackForce = 3f,
                            isCritical = false,
                            instigator = gameObject
                        });
                    }
                }
                else if (dist <= lightningRadius + 2.0f)
                {
                    GameEvents.RaisePlayerDodgeSuccess();
                }
            }
        }

        private void HandleDied(DamageInfo info)
        {
            StopAllCoroutines();
            if (telegraph != null) telegraph.HideImmediate();
            if (intactVisual != null) intactVisual.SetActive(false);
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
            GameEvents.RaiseCameraShake(0.14f, 0.2f);
            OnCrystalDestroyed?.Invoke(this);
        }
    }
}
