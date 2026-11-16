using UnityEngine;
using SkyGate.Core;

namespace SkyGate.World.Interactables
{
    public class VictoryChest : MonoBehaviour, IInteractable
    {
        [SerializeField] private bool isUnlocked = false;
        [SerializeField] private GameObject glowEffect;
        [SerializeField] private Transform lidTransform;

        private bool isOpened;

        public bool CanInteract => isUnlocked && !isOpened;
        public string InteractionPrompt => GetInteractionPrompt();

        private void Awake()
        {
            RefreshVisuals();
        }

        public void SetUnlocked(bool unlocked)
        {
            isUnlocked = unlocked;
            RefreshVisuals();
        }

        public string GetInteractionPrompt()
        {
            if (isOpened) return "Sky Sanctum Relic Claimed!";
            return isUnlocked ? "[E] Open Victory Chest" : "Defeat the Zephyr Guardian to Unlock";
        }

        public void Interact(GameObject interactor)
        {
            if (!isUnlocked || isOpened) return;
            isOpened = true;
            if (lidTransform != null)
                lidTransform.localRotation = Quaternion.Euler(-55f, 0f, 0f);

            GameEvents.RaiseRunEnded(true);
        }

        private void RefreshVisuals()
        {
            if (glowEffect != null)
                glowEffect.SetActive(isUnlocked);
        }
    }
}
