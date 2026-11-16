using System;
using UnityEngine;
using SkyGate.Core;

namespace SkyGate.World.Interactables
{
    public class RuneAnchor : MonoBehaviour, IInteractable
    {
        [SerializeField] private Light runeLight;
        [SerializeField] private GameObject activeAuraVisual;
        [SerializeField] private Renderer crystalRenderer;

        private bool isActivated;
        public event Action<RuneAnchor> OnRuneActivated;

        public bool IsActivated => isActivated;
        public bool CanInteract => !isActivated;
        public string InteractionPrompt => GetInteractionPrompt();

        private void Awake()
        {
            RefreshVisuals();
        }

        public string GetInteractionPrompt()
        {
            return isActivated ? "Rune Anchor Active" : "[E] Activate Rune Anchor";
        }

        public void Interact(GameObject interactor)
        {
            if (isActivated) return;
            isActivated = true;
            RefreshVisuals();
            OnRuneActivated?.Invoke(this);
        }

        public void ResetAnchor()
        {
            isActivated = false;
            RefreshVisuals();
        }

        private void RefreshVisuals()
        {
            if (runeLight != null) runeLight.enabled = isActivated;
            if (activeAuraVisual != null) activeAuraVisual.SetActive(isActivated);
            if (crystalRenderer != null)
            {
                var prop = new MaterialPropertyBlock();
                crystalRenderer.GetPropertyBlock(prop);
                Color c = isActivated ? new Color(0.28f, 0.79f, 0.89f, 1f) : new Color(0.45f, 0.50f, 0.58f, 1f);
                prop.SetColor("_BaseColor", c);
                prop.SetColor("_Color", c);
                crystalRenderer.SetPropertyBlock(prop);
            }
        }
    }
}
