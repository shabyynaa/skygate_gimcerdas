using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SkyGate.Core;
using SkyGate.World;

namespace SkyGate.UI
{
    public class HUDController : MonoBehaviour
    {
        [Header("Top-Left Vital & Stats Panel")]
        [SerializeField] private Image hpFillBar;
        [SerializeField] private Image shieldFillBar;
        [SerializeField] private TMP_Text hpValueText;
        [SerializeField] private TMP_Text livesValueText;
        [SerializeField] private TMP_Text statsLineText;

        [Header("Top-Center Room Progress Banner")]
        [SerializeField] private TMP_Text islandDepthText;
        [SerializeField] private TMP_Text objectiveBannerText;

        [Header("Bottom-Center Interaction Prompt")]
        [SerializeField] private GameObject interactionPromptRoot;
        [SerializeField] private TMP_Text interactionPromptText;

        private void OnEnable()
        {
            GameEvents.OnPlayerStatsDetailed += HandlePlayerStatsChanged;
            GameEvents.OnRoomEntered += HandleRoomEntered;
            GameEvents.OnObjectiveProgressUpdated += HandleObjectiveUpdated;
            GameEvents.OnInteractionPromptChanged += HandleInteractionPromptChanged;
        }

        private void OnDisable()
        {
            GameEvents.OnPlayerStatsDetailed -= HandlePlayerStatsChanged;
            GameEvents.OnRoomEntered -= HandleRoomEntered;
            GameEvents.OnObjectiveProgressUpdated -= HandleObjectiveUpdated;
            GameEvents.OnInteractionPromptChanged -= HandleInteractionPromptChanged;
        }

        private void Start()
        {
            if (interactionPromptRoot != null)
                interactionPromptRoot.SetActive(false);
        }

        private void HandlePlayerStatsChanged(
            float currentHP,
            float maxHP,
            float currentShield,
            int remainingLives,
            float attackDamage,
            float moveSpeed,
            float attackSpeedMult)
        {
            float hpRatio = maxHP > 0f ? Mathf.Clamp01(currentHP / maxHP) : 0f;
            float shieldRatio = maxHP > 0f ? Mathf.Clamp01(currentShield / (maxHP * 0.5f)) : 0f;

            if (hpFillBar != null)
            {
                hpFillBar.fillAmount = hpRatio;
                hpFillBar.transform.localScale = new Vector3(hpRatio, 1f, 1f);
            }
            if (shieldFillBar != null)
            {
                shieldFillBar.fillAmount = shieldRatio;
                shieldFillBar.transform.localScale = new Vector3(shieldRatio, 1f, 1f);
            }

            if (hpValueText != null)
            {
                string shieldSuffix = currentShield > 0.5f ? $" (+{Mathf.RoundToInt(currentShield)} SHD)" : "";
                hpValueText.text = $"HP: {Mathf.CeilToInt(currentHP)} / {Mathf.RoundToInt(maxHP)}{shieldSuffix}";
            }

            if (livesValueText != null)
            {
                string hearts = new string('*', Mathf.Clamp(remainingLives, 0, 5));
                livesValueText.text = $"LIVES: {remainingLives} [{hearts}]";
            }

            if (statsLineText != null)
            {
                statsLineText.text = $"ATK: {Mathf.RoundToInt(attackDamage)}  |  SPD: {moveSpeed:F1}  |  ASPD: {Mathf.RoundToInt(attackSpeedMult * 100f)}%";
            }
        }

        private void HandleRoomEntered(IslandNode node)
        {
            if (node == null) return;
            if (islandDepthText != null)
            {
                float elev = node.Depth * 2.5f;
                islandDepthText.text = $"DEPTH {node.Depth} / 4  (ELEVATION +{elev:F1}m)  —  {node.EncounterType.ToString().ToUpper()}";
            }
        }

        private void HandleObjectiveUpdated(string description, int currentCount, int requiredCount)
        {
            if (objectiveBannerText != null)
            {
                objectiveBannerText.text = description;
            }
        }

        private void HandleInteractionPromptChanged(string prompt, bool visible)
        {
            if (interactionPromptRoot != null)
                interactionPromptRoot.SetActive(visible && !string.IsNullOrEmpty(prompt));
            if (interactionPromptText != null && visible)
                interactionPromptText.text = prompt;
        }
    }
}
