using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SkyGate.Core;

namespace SkyGate.UI
{
    public class ModalController : MonoBehaviour
    {
        [Header("Modal Root & Elements")]
        [SerializeField] private GameObject modalPanelRoot;
        [SerializeField] private TMP_Text modalTitleText;
        [SerializeField] private TMP_Text modalSummaryText;
        [SerializeField] private Button retrySameSeedButton;
        [SerializeField] private Button newRandomSeedButton;
        [SerializeField] private Button mainMenuButton;

        private GameManager gameManager;

        private void Awake()
        {
            gameManager = FindAnyObjectByType<GameManager>();
            if (modalPanelRoot != null)
                modalPanelRoot.SetActive(false);

            if (retrySameSeedButton != null)
                retrySameSeedButton.onClick.AddListener(OnRetrySameSeedClicked);
            if (newRandomSeedButton != null)
                newRandomSeedButton.onClick.AddListener(OnNewRandomSeedClicked);
            if (mainMenuButton != null)
                mainMenuButton.onClick.AddListener(OnMainMenuClicked);
        }

        private void OnEnable()
        {
            GameEvents.OnRunStarted += HandleRunStarted;
            GameEvents.OnRunEnded += HandleRunEnded;
        }

        private void OnDisable()
        {
            GameEvents.OnRunStarted -= HandleRunStarted;
            GameEvents.OnRunEnded -= HandleRunEnded;
        }

        private void HandleRunStarted(int seed, bool isAdaptive)
        {
            if (modalPanelRoot != null)
                modalPanelRoot.SetActive(false);
        }

        private void HandleRunEnded(bool victory)
        {
            if (modalPanelRoot != null)
                modalPanelRoot.SetActive(true);

            if (modalTitleText != null)
            {
                modalTitleText.text = victory ? "VICTORY — SKY SANCTUM RECLAIMED!" : "RUN DEFEATED — OUT OF LIVES";
                modalTitleText.color = victory
                    ? new Color(0.34f, 0.80f, 0.41f, 1f)
                    : new Color(0.90f, 0.22f, 0.27f, 1f);
            }

            if (modalSummaryText != null)
            {
                if (gameManager != null && gameManager.DDAManager != null)
                {
                    var dda = gameManager.DDAManager;
                    var model = dda.CurrentPlayerModel;
                    string csvPath = gameManager.Exporter != null ? gameManager.Exporter.LastExportedFilePath : "TelemetryLogs/";
                    modalSummaryText.text =
                        $"Run Seed: {gameManager.ActiveRunSeed}  |  Mode: {(dda.IsAdaptiveEnabled ? "Adaptive DDA" : "Static 1.0x")}\n" +
                        $"Rooms Cleared: {dda.RoomSummaries.Count}  |  Playstyle: {(model != null ? model.playstyleLabel : "Balanced")}\n" +
                        $"Final Smoothed Skill: {(model != null ? model.smoothedSkill : 0.55f):F2}  |  Final Multiplier: {dda.CurrentMultiplier:F2}x\n" +
                        $"Telemetry Exported: {csvPath}";
                }
                else
                {
                    modalSummaryText.text = victory
                        ? "You conquered the Zephyr Guardian atop the Step-Terrace Archipelago!"
                        : "All 3 lives were consumed. Try again with the same seed or generate a new archipelago.";
                }
            }
        }

        private void OnRetrySameSeedClicked()
        {
            if (modalPanelRoot != null) modalPanelRoot.SetActive(false);
            if (gameManager != null) gameManager.RestartWithSameSeed();
        }

        private void OnNewRandomSeedClicked()
        {
            if (modalPanelRoot != null) modalPanelRoot.SetActive(false);
            if (gameManager != null) gameManager.StartNewRandomRun();
        }

        private void OnMainMenuClicked()
        {
            if (gameManager != null) gameManager.ReturnToMainMenu();
        }
    }
}
