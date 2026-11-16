using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SkyGate.Core;
using SkyGate.World;

namespace SkyGate.UI
{
    /// <summary>
    /// Implements GDD Section 10.1 In-Game AI & DDA Debug Overlay (toggled via F1 / GameInputReader.OnToggleDebugPressed).
    /// Displays: Active Seed, Depth, Elevation, SmoothedSkill, Playstyle Label,
    /// DifficultyMultiplier, Adaptation Reason, and buttons for Regenerate Seed / Toggle DDA Mode.
    /// </summary>
    public class AIDebugOverlay : MonoBehaviour
    {
        [Header("Debug Panel References")]
        [SerializeField] private GameObject debugPanelRoot;
        [SerializeField] private TMP_Text debugContentText;
        [SerializeField] private Button toggleDdaModeButton;
        [SerializeField] private Button newRandomSeedButton;
        [SerializeField] private bool visibleOnStart = true;

        private GameManager gameManager;
        private GameInputReader inputReader;
        private int currentSeed;
        private bool isAdaptive = true;
        private int currentDepth;
        private string currentEncounter = "StartingSkirmish";
        private DDADecisionRecord latestRecord;

        private void Awake()
        {
            gameManager = FindAnyObjectByType<GameManager>();
            inputReader = FindAnyObjectByType<GameInputReader>();

            latestRecord = new DDADecisionRecord
            {
                depth = 0,
                skillScore = 0.55f,
                smoothedSkill = 0.55f,
                confidence = 0.25f,
                difficultyMultiplier = 1.00f,
                playstyleLabel = "Balanced",
                adaptationReason = "Initial Start (1.00x)"
            };

            if (debugPanelRoot != null)
                debugPanelRoot.SetActive(visibleOnStart);

            if (toggleDdaModeButton != null)
                toggleDdaModeButton.onClick.AddListener(OnToggleDdaClicked);
            if (newRandomSeedButton != null)
                newRandomSeedButton.onClick.AddListener(OnNewSeedClicked);
        }

        private void OnEnable()
        {
            if (inputReader == null)
                inputReader = FindAnyObjectByType<GameInputReader>();
            if (inputReader != null)
                inputReader.OnToggleDebugPressed += ToggleOverlay;

            GameEvents.OnRunStarted += HandleRunStarted;
            GameEvents.OnRoomEntered += HandleRoomEntered;
            GameEvents.OnDDAUpdated += HandleDDAUpdated;
        }

        private void OnDisable()
        {
            if (inputReader != null)
                inputReader.OnToggleDebugPressed -= ToggleOverlay;

            GameEvents.OnRunStarted -= HandleRunStarted;
            GameEvents.OnRoomEntered -= HandleRoomEntered;
            GameEvents.OnDDAUpdated -= HandleDDAUpdated;
        }

        public void ToggleOverlay()
        {
            if (debugPanelRoot == null) return;
            debugPanelRoot.SetActive(!debugPanelRoot.activeSelf);
            RefreshText();
        }

        private void HandleRunStarted(int seed, bool adaptive)
        {
            currentSeed = seed;
            isAdaptive = adaptive;
            RefreshText();
        }

        private void HandleRoomEntered(IslandNode node)
        {
            if (node != null)
            {
                currentDepth = node.Depth;
                currentEncounter = node.EncounterType.ToString();
            }
            RefreshText();
        }

        private void HandleDDAUpdated(DDADecisionRecord record)
        {
            latestRecord = record;
            if (gameManager != null && gameManager.DDAManager != null)
                isAdaptive = gameManager.DDAManager.IsAdaptiveEnabled;
            RefreshText();
        }

        private void OnToggleDdaClicked()
        {
            if (gameManager != null && gameManager.DDAManager != null)
            {
                gameManager.DDAManager.ToggleAdaptiveMode();
                isAdaptive = gameManager.DDAManager.IsAdaptiveEnabled;
                RefreshText();
            }
        }

        private void OnNewSeedClicked()
        {
            if (gameManager != null)
            {
                gameManager.StartNewRandomRun();
            }
        }

        private void RefreshText()
        {
            if (debugContentText == null) return;

            float elev = currentDepth * 2.5f;
            string modeLabel = isAdaptive ? "<color=#57CC68>ADAPTIVE DDA</color>" : "<color=#FFB703>STATIC 1.00x</color>";

            debugContentText.text =
                $"<b>[F1] AI & DDA TELEMETRY OVERLAY</b>\n" +
                $"Run Seed: <b>{currentSeed}</b>  |  Mode: {modeLabel}\n" +
                $"Island Depth: <b>{currentDepth}/4 (+{elev:F1}m)</b> [{currentEncounter}]\n" +
                $"Instant SkillScore: <b>{latestRecord.skillScore:F2}</b>  |  EMA SmoothedSkill: <b>{latestRecord.smoothedSkill:F2}</b>\n" +
                $"Playstyle Model: <b>{latestRecord.playstyleLabel}</b>  (Conf: {latestRecord.confidence:F2})\n" +
                $"Difficulty Multiplier: <b>{latestRecord.difficultyMultiplier:F2}x</b>  [Clamp 0.75..1.50]\n" +
                $"Last Policy Action: <i>{latestRecord.adaptationReason}</i>\n" +
                $"Controls: WASD Move | LMB 3-Hit Combo | Space Dodge | E Interact | V Camera | F1 Overlay";
        }
    }
}
