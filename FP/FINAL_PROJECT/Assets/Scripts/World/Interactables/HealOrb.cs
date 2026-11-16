using UnityEngine;
using SkyGate.Core;

namespace SkyGate.World.Interactables
{
    [RequireComponent(typeof(Collider))]
    public class HealOrb : MonoBehaviour, IInteractable
    {
        [SerializeField] private float healAmount = 25f;
        [SerializeField] private bool allowOverhealShield = false;
        [SerializeField] private float bobFrequency = 2.2f;
        [SerializeField] private float bobAmplitude = 0.18f;

        private Vector3 baseLocalPos;
        private bool consumed;

        public bool CanInteract => !consumed;
        public string InteractionPrompt => GetInteractionPrompt();

        private void Awake()
        {
            baseLocalPos = transform.localPosition;
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void Update()
        {
            if (consumed) return;
            transform.localPosition = baseLocalPos + Vector3.up * (Mathf.Sin(Time.time * bobFrequency) * bobAmplitude);
            transform.Rotate(0f, 65f * Time.deltaTime, 0f, Space.Self);
        }

        public string GetInteractionPrompt()
        {
            return consumed ? "" : $"[E] Absorb Sky Vital (+{Mathf.RoundToInt(healAmount)} HP)";
        }

        public void Interact(GameObject interactor)
        {
            TryConsume(interactor);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (consumed) return;
            if (other.CompareTag("Player"))
            {
                TryConsume(other.gameObject);
            }
        }

        private void TryConsume(GameObject playerObj)
        {
            if (consumed || playerObj == null) return;
            var health = playerObj.GetComponent<HealthComponent>();
            if (health == null) health = playerObj.GetComponentInParent<HealthComponent>();
            if (health == null || !health.IsAlive) return;

            consumed = true;
            health.Heal(healAmount, allowOverhealShield);
            gameObject.SetActive(false);
        }

        public void ResetOrb()
        {
            consumed = false;
            gameObject.SetActive(true);
        }
    }
}
