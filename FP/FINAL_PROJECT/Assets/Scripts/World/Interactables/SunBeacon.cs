using System;
using System.Collections.Generic;
using UnityEngine;
using SkyGate.Core;

namespace SkyGate.World.Interactables
{
    public class SunBeacon : MonoBehaviour, IInteractable
    {
        public static readonly List<SunBeacon> ActiveBeacons = new List<SunBeacon>();

        [Header("Beacon Settings")]
        [SerializeField] private float safeRadius = 4.5f;
        [SerializeField] private Light beaconLight;
        [SerializeField] private GameObject flameVisual;
        [SerializeField] private Renderer ringRenderer;

        private bool isLit;
        public event Action<SunBeacon> OnBeaconLit;

        public bool IsLit => isLit;
        public float SafeRadius => safeRadius;
        public bool CanInteract => !isLit;
        public string InteractionPrompt => GetInteractionPrompt();

        private void OnEnable()
        {
            if (!ActiveBeacons.Contains(this))
                ActiveBeacons.Add(this);
            RefreshVisuals();
        }

        private void OnDisable()
        {
            ActiveBeacons.Remove(this);
        }

        public string GetInteractionPrompt()
        {
            return isLit ? "Sun Beacon Lit" : "[E] Ignite Sun Beacon";
        }

        public void Interact(GameObject interactor)
        {
            if (isLit) return;
            isLit = true;
            RefreshVisuals();
            OnBeaconLit?.Invoke(this);
        }

        public void ResetBeacon()
        {
            isLit = false;
            RefreshVisuals();
        }

        private void RefreshVisuals()
        {
            if (beaconLight != null)
            {
                beaconLight.enabled = isLit;
                beaconLight.range = safeRadius * 1.6f;
            }
            if (flameVisual != null)
            {
                flameVisual.SetActive(isLit);
            }
            if (ringRenderer != null)
            {
                ringRenderer.enabled = isLit;
                ringRenderer.transform.localScale = new Vector3(safeRadius * 2f, 0.04f, safeRadius * 2f);
            }
        }
    }
}
