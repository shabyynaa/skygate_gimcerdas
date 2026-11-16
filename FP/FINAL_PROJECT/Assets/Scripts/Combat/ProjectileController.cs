using UnityEngine;
using SkyGate.Core;

namespace SkyGate.Combat
{
    [RequireComponent(typeof(SphereCollider))]
    [RequireComponent(typeof(Rigidbody))]
    public class ProjectileController : MonoBehaviour
    {
        [Header("Projectile Settings (GDD 7.2 & ASSET_CONTRACT 4.2)")]
        [SerializeField] private float speed = 11f;
        [SerializeField] private float damage = 12f;
        [SerializeField] private float maxLifetime = 4.0f;
        [SerializeField] private float knockbackForce = 3.0f;

        private Vector3 moveDirection = Vector3.forward;
        private GameObject instigator;
        private float spawnTime;
        private bool hasHit;

        private int playerLayer;
        private int wallLayer;
        private int obstacleLayer;

        private void Awake()
        {
            playerLayer = LayerMask.NameToLayer("Player");
            wallLayer = LayerMask.NameToLayer("Wall");
            obstacleLayer = LayerMask.NameToLayer("Obstacle");

            var rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var col = GetComponent<SphereCollider>();
            col.isTrigger = true;
        }

        public void Initialize(Vector3 direction, float dmg, float projSpeed, GameObject owner)
        {
            direction.y = 0f;
            moveDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward;
            damage = dmg;
            speed = projSpeed > 0f ? projSpeed : 11f;
            instigator = owner;
            spawnTime = Time.time;
            hasHit = false;
            transform.rotation = Quaternion.LookRotation(moveDirection, Vector3.up);
        }

        private void Update()
        {
            if (hasHit) return;

            transform.position += moveDirection * (speed * Time.deltaTime);

            if (Time.time - spawnTime >= maxLifetime)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (hasHit || other == null) return;
            if (instigator != null && (other.gameObject == instigator || other.transform.IsChildOf(instigator.transform)))
                return;

            int otherLayer = other.gameObject.layer;

            // Ignore TriggerZone and Enemy layers per ASSET_CONTRACT.md Section 4.2
            if (other.isTrigger) return;
            if (other.CompareTag("Enemy")) return;

            if (other.CompareTag("Player") || otherLayer == playerLayer)
            {
                IDamageable damageable = other.GetComponentInParent<IDamageable>();
                if (damageable != null)
                {
                    hasHit = true;
                    damageable.TakeDamage(new DamageInfo
                    {
                        amount = damage,
                        sourcePosition = transform.position,
                        knockbackDirection = moveDirection,
                        knockbackForce = knockbackForce,
                        isCritical = false,
                        bypassArmor = false,
                        instigator = instigator
                    });
                    Destroy(gameObject);
                    return;
                }
            }

            if (otherLayer == wallLayer || otherLayer == obstacleLayer || other.CompareTag("StunPillar") || other.CompareTag("Obstacle"))
            {
                hasHit = true;
                Destroy(gameObject);
            }
        }
    }
}
