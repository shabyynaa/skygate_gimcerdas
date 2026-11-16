using UnityEngine;
using TMPro;
using SkyGate.Core;
using SkyGate.Player;

namespace SkyGate.MathGate
{
    [RequireComponent(typeof(Collider))]
    public class MathGateDoor : MonoBehaviour
    {
        [Header("Gate Configuration")]
        [SerializeField] private MathGateOptionData optionData;
        [SerializeField] private MathGateDoor partnerDoor;
        [SerializeField] private bool isUnlocked = false;

        [Header("World-Space UI References")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text positiveText;
        [SerializeField] private TMP_Text negativeText;
        [SerializeField] private Renderer portalRenderer;
        [SerializeField] private Renderer crystalRenderer;
        [SerializeField] private Light gateLight;
        [SerializeField] private GameObject lockedBarrierVisual;

        private bool isConsumed;
        private MaterialPropertyBlock propBlock;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        public MathGateOptionData OptionData => optionData;
        public bool IsRiskyGate => optionData.isHighRisk;
        public int TargetIslandId => optionData.targetIslandId;
        public bool IsUnlocked => isUnlocked;
        public bool IsConsumed => isConsumed;

        private void Awake()
        {
            propBlock = new MaterialPropertyBlock();
            Collider col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
            RefreshVisuals();
        }

        public void ConfigureGate(MathGateOptionData data, MathGateDoor partner, bool unlockedInitially = false)
        {
            optionData = data;
            partnerDoor = partner;
            isUnlocked = unlockedInitially;
            isConsumed = false;
            RefreshVisuals();
        }

        public void SetPartnerDoor(MathGateDoor partner)
        {
            partnerDoor = partner;
        }

        public void SetUnlocked(bool unlocked)
        {
            if (isConsumed) return;
            isUnlocked = unlocked;
            RefreshVisuals();
        }

        public void LockAsUnchosen()
        {
            isConsumed = true;
            isUnlocked = false;

            if (titleText != null) titleText.text = "GATE CLOSED";
            if (positiveText != null) positiveText.text = "LOCKED";
            if (negativeText != null) negativeText.text = "";
            if (lockedBarrierVisual != null) lockedBarrierVisual.SetActive(true);
            SetPortalTint(new Color(0.35f, 0.38f, 0.42f, 0.45f));
        }

        private void RefreshVisuals()
        {
            if (isConsumed) return;

            if (titleText != null)
            {
                string lockPrefix = isUnlocked ? "" : "[LOCKED] ";
                titleText.text = lockPrefix + (string.IsNullOrEmpty(optionData.gateTitle)
                    ? (optionData.isHighRisk ? "GATE B (RISK)" : "GATE A (SAFE)")
                    : optionData.gateTitle);
            }

            if (positiveText != null)
            {
                positiveText.text = optionData.positiveModifier.GetFormattedLabel();
                positiveText.color = new Color(0.34f, 0.80f, 0.41f, 1f); // Emerald Green #57CC68
            }

            if (negativeText != null)
            {
                negativeText.text = optionData.negativeModifier.GetFormattedLabel();
                negativeText.color = new Color(0.90f, 0.22f, 0.27f, 1f); // Crimson Red #E63946
            }

            if (lockedBarrierVisual != null)
            {
                lockedBarrierVisual.SetActive(!isUnlocked);
            }

            Color tint = !isUnlocked
                ? new Color(0.45f, 0.50f, 0.55f, 0.45f)
                : (optionData.isHighRisk
                    ? new Color(0.95f, 0.65f, 0.15f, 0.72f)  // Gold/Amber for High-Risk
                    : new Color(0.28f, 0.79f, 0.89f, 0.72f)); // Cyan for Safe
            SetPortalTint(tint);
        }

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private void SetPortalTint(Color c)
        {
            if (portalRenderer != null)
            {
                portalRenderer.GetPropertyBlock(propBlock);
                propBlock.SetColor(BaseColorId, c);
                propBlock.SetColor(ColorId, c);
                propBlock.SetColor(EmissionColorId, new Color(c.r, c.g, c.b, 1f) * 0.45f);
                portalRenderer.SetPropertyBlock(propBlock);
            }

            if (crystalRenderer != null)
            {
                Color opaque = new Color(c.r, c.g, c.b, 1f);
                crystalRenderer.GetPropertyBlock(propBlock);
                propBlock.SetColor(BaseColorId, opaque);
                propBlock.SetColor(ColorId, opaque);
                propBlock.SetColor(EmissionColorId, opaque * 0.85f);
                crystalRenderer.SetPropertyBlock(propBlock);
            }

            if (gateLight != null)
            {
                gateLight.color = new Color(c.r, c.g, c.b, 1f);
                gateLight.intensity = isUnlocked ? 2.4f : 0.7f;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!isUnlocked || isConsumed) return;
            if (!other.CompareTag("Player")) return;

            PlayerStats stats = other.GetComponent<PlayerStats>();
            if (stats == null)
                stats = other.GetComponentInParent<PlayerStats>();

            if (stats == null) return;

            isConsumed = true;
            isUnlocked = false;

            // Apply both positive and negative modifiers honestly (GDD Section 5)
            stats.ApplyMathGateChoice(optionData.positiveModifier, optionData.negativeModifier);

            if (titleText != null) titleText.text = "CHOSEN!";
            if (positiveText != null) positiveText.text = optionData.positiveModifier.GetFormattedLabel();
            if (negativeText != null) negativeText.text = "";
            SetPortalTint(new Color(0.34f, 0.80f, 0.41f, 0.35f));

            // Immediately lock partner door so player cannot double-dip (GDD Section 5.1)
            if (partnerDoor != null && !partnerDoor.IsConsumed)
            {
                partnerDoor.LockAsUnchosen();
            }

            GameEvents.RaiseMathGateChosen(this, optionData.isHighRisk);
        }
    }
}
