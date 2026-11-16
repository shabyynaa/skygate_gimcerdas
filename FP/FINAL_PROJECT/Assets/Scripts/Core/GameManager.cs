using UnityEngine;
using UnityEngine.SceneManagement;
using SkyGate.DDA;
using SkyGate.PCG;
using SkyGate.Player;
using SkyGate.World;

namespace SkyGate.Core
{
    public class GameManager : MonoBehaviour
    {
        // Static cross-scene handoff from MainMenu
        public static int RequestedSeed = 0;
        public static bool RequestedAdaptiveMode = true;
        public static bool UseRequestedConfig = false;

        [Header("Run Configuration")]
        [SerializeField] private int defaultSeed = 12345;
        [SerializeField] private bool startWithRandomSeed = false;
        [SerializeField] private bool adaptiveMode = true;
        [SerializeField] private bool autoGenerateOnStart = true;

        [Header("System References")]
        [SerializeField] private BranchingIslandGenerator islandGenerator;
        [SerializeField] private DDAManager ddaManager;
        [SerializeField] private CheckpointManager checkpointManager;
        [SerializeField] private RunLogExporter runLogExporter;
        [SerializeField] private PlayerController playerInstance;
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private ThirdPersonCamera mainCameraRig;

        private int activeRunSeed;
        private bool isRunActive;

        public int ActiveRunSeed => activeRunSeed;
        public bool IsRunActive => isRunActive;
        public DDAManager DDAManager => ddaManager;
        public BranchingIslandGenerator IslandGenerator => islandGenerator;
        public PlayerController PlayerInstance => playerInstance;
        public RunLogExporter Exporter => runLogExporter;

        private void Awake()
        {
            if (islandGenerator == null)
                islandGenerator = GetComponentInChildren<BranchingIslandGenerator>();
            if (ddaManager == null)
                ddaManager = GetComponentInChildren<DDAManager>();
            if (checkpointManager == null)
                checkpointManager = GetComponentInChildren<CheckpointManager>();
            if (runLogExporter == null)
                runLogExporter = GetComponentInChildren<RunLogExporter>();
            if (mainCameraRig == null && Camera.main != null)
                mainCameraRig = Camera.main.GetComponent<ThirdPersonCamera>();
        }

        private void OnEnable()
        {
            GameEvents.OnDDAUpdated += HandleDDAUpdated;
            GameEvents.OnRunEnded += HandleRunEnded;
        }

        private void OnDisable()
        {
            GameEvents.OnDDAUpdated -= HandleDDAUpdated;
            GameEvents.OnRunEnded -= HandleRunEnded;
        }

        private void Start()
        {
            if (!autoGenerateOnStart) return;

            int seedToUse;
            bool adaptiveToUse = adaptiveMode;

            if (UseRequestedConfig)
            {
                seedToUse = RequestedSeed != 0 ? RequestedSeed : Random.Range(10000, 99999);
                adaptiveToUse = RequestedAdaptiveMode;
                UseRequestedConfig = false;
            }
            else if (startWithRandomSeed)
            {
                seedToUse = Random.Range(10000, 99999);
            }
            else
            {
                seedToUse = defaultSeed;
            }

            StartRun(seedToUse, adaptiveToUse);
        }

        public void StartRun(int seed, bool enableAdaptiveDDA)
        {
            Time.timeScale = 1f;
            activeRunSeed = seed;
            adaptiveMode = enableAdaptiveDDA;
            isRunActive = true;

            EnsurePlayerSpawned();

            PlayerStats stats = playerInstance != null ? playerInstance.GetComponent<PlayerStats>() : null;
            HealthComponent health = playerInstance != null ? playerInstance.GetComponent<HealthComponent>() : null;

            if (stats != null)
            {
                stats.ResetForNewRun();
            }

            if (ddaManager != null)
            {
                ddaManager.InitializeRun(enableAdaptiveDDA, health);
            }

            DifficultyContext initialCtx = ddaManager != null
                ? ddaManager.GetCurrentDifficultyContext()
                : DifficultyContext.Default;

            IslandNode startIsland = null;
            if (islandGenerator != null)
            {
                startIsland = islandGenerator.GenerateArchipelago(activeRunSeed, initialCtx);
            }

            if (startIsland != null && playerInstance != null)
            {
                Transform spawnAnchor = startIsland.EntranceSpawnPoint;
                Vector3 startPos = spawnAnchor != null ? spawnAnchor.position : startIsland.transform.position;
                playerInstance.ReviveAndTeleport(startPos + Vector3.up * 0.15f, Quaternion.identity);
            }

            if (checkpointManager != null && playerInstance != null)
            {
                checkpointManager.Initialize(playerInstance, startIsland);
            }

            if (mainCameraRig != null && playerInstance != null)
            {
                mainCameraRig.SetFollowTarget(playerInstance.transform);
            }

            GameEvents.RaiseRunStarted(activeRunSeed, enableAdaptiveDDA);
        }

        public void RestartWithSameSeed()
        {
            StartRun(activeRunSeed, ddaManager != null ? ddaManager.IsAdaptiveEnabled : adaptiveMode);
        }

        public void StartNewRandomRun()
        {
            int newSeed = Random.Range(10000, 99999);
            StartRun(newSeed, ddaManager != null ? ddaManager.IsAdaptiveEnabled : adaptiveMode);
        }

        public void ReturnToMainMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("MainMenu");
        }

        private void EnsurePlayerSpawned()
        {
            if (playerInstance == null)
            {
                GameObject existing = GameObject.FindGameObjectWithTag("Player");
                if (existing != null)
                    playerInstance = existing.GetComponent<PlayerController>();
            }

            if (playerInstance == null && playerPrefab != null)
            {
                GameObject spawned = Instantiate(playerPrefab, new Vector3(0f, 0.2f, -6f), Quaternion.identity);
                playerInstance = spawned.GetComponent<PlayerController>();
            }
        }

        private void HandleDDAUpdated(DDADecisionRecord record)
        {
            if (islandGenerator != null && ddaManager != null)
            {
                islandGenerator.ApplyDifficultyToUnvisitedIslands(ddaManager.GetCurrentDifficultyContext());
            }

            if (runLogExporter != null && ddaManager != null && ddaManager.RoomSummaries.Count > 0)
            {
                runLogExporter.ExportRunLog(
                    activeRunSeed,
                    ddaManager.IsAdaptiveEnabled,
                    false,
                    ddaManager.RoomSummaries,
                    ddaManager.DecisionHistory
                );
            }
        }

        private void HandleRunEnded(bool victory)
        {
            if (!isRunActive) return;
            isRunActive = false;

            if (runLogExporter != null && ddaManager != null)
            {
                runLogExporter.ExportRunLog(
                    activeRunSeed,
                    ddaManager.IsAdaptiveEnabled,
                    victory,
                    ddaManager.RoomSummaries,
                    ddaManager.DecisionHistory
                );
            }
        }
    }
}
