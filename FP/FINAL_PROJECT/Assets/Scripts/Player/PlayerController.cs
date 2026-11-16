using UnityEngine;
using UnityEngine.AI;
using SkyGate.Core;

namespace SkyGate.Player
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerStats))]
    [RequireComponent(typeof(HealthComponent))]
    public class PlayerController : MonoBehaviour
    {
        // Animator Parameter Hashes (ASSET_CONTRACT.md Section 5.1)
        private static readonly int HashSpeed = Animator.StringToHash("Speed");
        private static readonly int HashDodge = Animator.StringToHash("Dodge");
        private static readonly int HashHit = Animator.StringToHash("Hit");
        private static readonly int HashInteract = Animator.StringToHash("Interact");
        private static readonly int HashDie = Animator.StringToHash("Die");
        private static readonly int HashIsDead = Animator.StringToHash("IsDead");

        [Header("Movement & Dodge (GDD 4.1)")]
        [SerializeField] private float rotationSmoothTime = 0.08f;
        [SerializeField] private float dodgeDistance = 4.5f;
        [SerializeField] private float dodgeDuration = 0.25f;
        [SerializeField] private float dodgeCooldown = 1.2f;
        [SerializeField] private float gravity = -24f;

        [Header("Interaction")]
        [SerializeField] private float interactRadius = 2.8f;
        [SerializeField] private LayerMask enemyLayerMask;

        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private ParticleSystem dodgeTrailVFX;

        private CharacterController controller;
        private PlayerStats stats;
        private HealthComponent health;

        private float currentYawVelocity;
        private float verticalVelocity;
        private bool isDodging;
        private float dodgeTimer;
        private float nextDodgeTime;
        private Vector3 dodgeDirection;
        private Vector3 knockbackVelocity;

        private IInteractable nearestInteractable;
        private string lastPrompt = string.Empty;
        private readonly Collider[] overlapBuffer = new Collider[24];

        public bool IsDodging => isDodging;
        public Animator CharacterAnimator => animator;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            stats = GetComponent<PlayerStats>();
            health = GetComponent<HealthComponent>();

            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            if (animator != null)
                animator.applyRootMotion = false; // Mandatory per AI_RULES.md
        }

        private void Start()
        {
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;

            BindInput(true);
            health.OnDamaged += HandleDamaged;
            health.OnDied += HandleDied;
        }

        private void OnDestroy()
        {
            BindInput(false);
            if (health != null)
            {
                health.OnDamaged -= HandleDamaged;
                health.OnDied -= HandleDied;
            }
        }

        private void BindInput(bool subscribe)
        {
            var input = subscribe ? GameInputReader.Instance : GameInputReader.PeekInstance;
            if (input == null) return;

            if (subscribe)
            {
                input.OnDodgePressed += TryDodge;
                input.OnInteractPressed += TryInteract;
            }
            else
            {
                input.OnDodgePressed -= TryDodge;
                input.OnInteractPressed -= TryInteract;
            }
        }

        private void Update()
        {
            if (!health.IsAlive) return;

            UpdateMovement();
            ScanInteractables();
        }

        private void UpdateMovement()
        {
            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f;
            else
                verticalVelocity += gravity * Time.deltaTime;

            Vector3 horizontalMove = Vector3.zero;

            if (isDodging)
            {
                dodgeTimer -= Time.deltaTime;
                float dodgeSpeed = dodgeDistance / Mathf.Max(0.05f, dodgeDuration);
                horizontalMove = dodgeDirection * dodgeSpeed;

                if (dodgeTimer <= 0f)
                {
                    isDodging = false;
                }
            }
            else
            {
                Vector2 moveInput = GameInputReader.Instance != null ? GameInputReader.Instance.MoveInput : Vector2.zero;
                Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y).normalized;

                if (inputDir.sqrMagnitude >= 0.01f)
                {
                    float camYaw = cameraTransform != null ? cameraTransform.eulerAngles.y : 0f;
                    float targetAngle = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg + camYaw;
                    float smoothAngle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref currentYawVelocity, rotationSmoothTime);
                    transform.rotation = Quaternion.Euler(0f, smoothAngle, 0f);

                    Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
                    horizontalMove = moveDir.normalized * stats.EffectiveWalkSpeed;
                }

                if (animator != null)
                {
                    animator.SetFloat(HashSpeed, inputDir.magnitude, 0.08f, Time.deltaTime);
                }
            }

            if (knockbackVelocity.sqrMagnitude > 0.05f)
            {
                horizontalMove += knockbackVelocity;
                knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, 10f * Time.deltaTime);
            }

            Vector3 finalVelocity = horizontalMove + Vector3.up * verticalVelocity;
            controller.Move(finalVelocity * Time.deltaTime);

            // Prevent falling into the void if knocked off an edge
            if (transform.position.y < -12f)
            {
                DamageInfo fallDmg = new DamageInfo
                {
                    amount = 999f,
                    sourcePosition = transform.position,
                    bypassArmor = true
                };
                health.TakeDamage(fallDmg);
            }
        }

        private void TryDodge()
        {
            if (!health.IsAlive || isDodging || Time.time < nextDodgeTime) return;

            nextDodgeTime = Time.time + dodgeCooldown;
            isDodging = true;
            dodgeTimer = dodgeDuration;

            Vector2 moveInput = GameInputReader.Instance != null ? GameInputReader.Instance.MoveInput : Vector2.zero;
            if (moveInput.sqrMagnitude >= 0.01f && cameraTransform != null)
            {
                float targetAngle = Mathf.Atan2(moveInput.x, moveInput.y) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;
                dodgeDirection = (Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward).normalized;
                transform.rotation = Quaternion.Euler(0f, targetAngle, 0f);
            }
            else
            {
                dodgeDirection = transform.forward;
            }

            // Grant i-frames during dodge (GDD Section 4.1: 0.25s i-frame)
            health.GrantInvulnerability(dodgeDuration + 0.05f);
            GameEvents.RaisePlayerDodgeStarted();

            if (animator != null)
                animator.SetTrigger(HashDodge);

            if (dodgeTrailVFX != null)
                dodgeTrailVFX.Play();

            // Optional Static Dash perk (GDD Section 5.3)
            if (stats.HasStaticDash)
            {
                TriggerStaticDashShockwave();
            }
        }

        private void TriggerStaticDashShockwave()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, 3.0f, overlapBuffer, enemyLayerMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (overlapBuffer[i].TryGetComponent<IDamageable>(out var target) && overlapBuffer[i].gameObject != gameObject)
                {
                    Vector3 dir = (overlapBuffer[i].transform.position - transform.position).normalized;
                    dir.y = 0f;
                    target.TakeDamage(new DamageInfo
                    {
                        amount = Mathf.Round(stats.ATK * 0.6f),
                        sourcePosition = transform.position,
                        knockbackDirection = dir,
                        knockbackForce = 3f,
                        isCritical = false,
                        instigator = gameObject
                    });
                }
            }
        }

        private void ScanInteractables()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, interactRadius, overlapBuffer, ~0, QueryTriggerInteraction.Collide);
            IInteractable best = null;
            float bestDistSqr = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var col = overlapBuffer[i];
                if (col == null) continue;
                var interactable = col.GetComponentInParent<IInteractable>();
                if (interactable != null && interactable.CanInteract)
                {
                    float dSqr = (col.transform.position - transform.position).sqrMagnitude;
                    if (dSqr < bestDistSqr)
                    {
                        bestDistSqr = dSqr;
                        best = interactable;
                    }
                }
            }

            nearestInteractable = best;
            string prompt = nearestInteractable != null ? nearestInteractable.InteractionPrompt : string.Empty;
            if (prompt != lastPrompt)
            {
                lastPrompt = prompt;
                GameEvents.RaiseInteractPromptChanged(prompt);
            }
        }

        private void TryInteract()
        {
            if (!health.IsAlive) return;
            ScanInteractables();
            if (nearestInteractable != null && nearestInteractable.CanInteract)
            {
                if (animator != null)
                    animator.SetTrigger(HashInteract);

                nearestInteractable.Interact(gameObject);
                ScanInteractables();
            }
        }

        public void ReviveAndTeleport(Vector3 position, Quaternion rotation)
        {
            TeleportTo(position, rotation);
        }

        public void TeleportTo(Vector3 position, Quaternion rotation)
        {
            if (controller == null) controller = GetComponent<CharacterController>();
            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            verticalVelocity = 0f;
            knockbackVelocity = Vector3.zero;
            isDodging = false;
            controller.enabled = wasEnabled;

            if (animator != null)
            {
                animator.SetBool(HashIsDead, false);
                animator.SetFloat(HashSpeed, 0f);
            }
        }

        private void HandleDamaged(DamageInfo info, float finalDamage, bool absorbed)
        {
            if (!absorbed && animator != null && health.IsAlive)
            {
                animator.SetTrigger(HashHit);
            }

            if (info.knockbackForce > 0f && info.knockbackDirection.sqrMagnitude > 0.001f)
            {
                Vector3 candidatePos = transform.position + info.knockbackDirection.normalized * 0.6f;
                if (NavMesh.SamplePosition(candidatePos, out _, 1.5f, NavMesh.AllAreas))
                {
                    knockbackVelocity = info.knockbackDirection.normalized * Mathf.Min(info.knockbackForce, 6f);
                }
            }

            GameEvents.RaiseCameraShake(0.08f, 0.12f);
        }

        private void HandleDied(DamageInfo info)
        {
            isDodging = false;
            if (animator != null)
            {
                animator.SetBool(HashIsDead, true);
                animator.SetTrigger(HashDie);
            }
        }
    }
}
