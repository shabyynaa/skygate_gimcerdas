using System.Collections.Generic;
using UnityEngine;
using SkyGate.Core;
using SkyGate.MathGate;
using SkyGate.World;
using SkyGate.World.Challenges;

namespace SkyGate.PCG
{
    [RequireComponent(typeof(IslandContentSpawner))]
    [RequireComponent(typeof(RuntimeNavMeshBuilder))]
    public class BranchingIslandGenerator : MonoBehaviour
    {
        [Header("PCG Configuration")]
        [SerializeField] private PCGConfigSO config;
        [SerializeField] private Transform worldRoot;

        private IslandContentSpawner contentSpawner;
        private RuntimeNavMeshBuilder navMeshBuilder;
        private readonly List<IslandGraphNode> graphNodes = new List<IslandGraphNode>();
        private readonly List<IslandNode> spawnedIslands = new List<IslandNode>();
        private readonly List<GameObject> spawnedBridges = new List<GameObject>();

        public PCGConfigSO Config => config;
        public IReadOnlyList<IslandGraphNode> GraphNodes => graphNodes;
        public IReadOnlyList<IslandNode> SpawnedIslands => spawnedIslands;

        private void Awake()
        {
            contentSpawner = GetComponent<IslandContentSpawner>();
            navMeshBuilder = GetComponent<RuntimeNavMeshBuilder>();
            if (worldRoot == null)
            {
                GameObject rootObj = new GameObject("GeneratedArchipelago");
                rootObj.transform.SetParent(transform, false);
                worldRoot = rootObj.transform;
            }
        }

        public void SetConfig(PCGConfigSO pcgConfig)
        {
            config = pcgConfig;
        }

        public void ClearGeneratedWorld()
        {
            graphNodes.Clear();
            spawnedIslands.Clear();
            spawnedBridges.Clear();

            if (worldRoot != null)
            {
                for (int i = worldRoot.childCount - 1; i >= 0; i--)
                {
                    Destroy(worldRoot.GetChild(i).gameObject);
                }
            }
        }

        /// <summary>
        /// Generates the 5-Depth Step-Terrace Archipelago (Depth 0 -> Depth 4, +2.5m per step)
        /// from a deterministic integer seed and validates 100% reachability via BFS.
        /// </summary>
        public IslandNode GenerateArchipelago(int runSeed, DifficultyContext initialDdaContext)
        {
            ClearGeneratedWorld();
            if (config == null)
            {
                Debug.LogError("[BranchingIslandGenerator] Missing PCGConfigSO!");
                return null;
            }

            System.Random rng = new System.Random(runSeed);

            // 1. Build Step-Terrace Branching Graph (Depths 0..4)
            BuildIslandGraph(rng);

            // 2. Validate Graph Connectivity via BFS (Depth 0 -> Depth 4 Boss Sanctum)
            bool reachable = IslandGraphNode.ValidatePathExistsBFS(graphNodes, 0, 7);
            if (!reachable)
            {
                Debug.LogWarning("[BranchingIslandGenerator] BFS detected disconnected graph; repairing edges.");
                graphNodes[0].nextNodeIds.Add(1);
                graphNodes[1].nextNodeIds.Add(3);
                graphNodes[3].nextNodeIds.Add(5);
                graphNodes[5].nextNodeIds.Add(7);
            }

            // 3. Instantiate Island Platforms & RoomControllers
            Dictionary<int, IslandGraphNode> lookup = new Dictionary<int, IslandGraphNode>();
            for (int i = 0; i < graphNodes.Count; i++)
            {
                IslandGraphNode gNode = graphNodes[i];
                lookup[gNode.id] = gNode;

                IslandNode island = SpawnIslandPlatform(gNode);
                gNode.spawnedIsland = island;
                spawnedIslands.Add(island);
            }

            // 4. Instantiate Connecting Arched Bridges and Paired MathGate Doors
            for (int i = 0; i < graphNodes.Count; i++)
            {
                IslandGraphNode fromNode = graphNodes[i];
                if (fromNode.nextNodeIds.Count == 0) continue;

                SpawnBridgesAndGatesForIsland(fromNode, lookup, rng, initialDdaContext.difficultyMultiplier);
            }

            // 5. Initial Runtime NavMesh Bake so NavMeshAgents have valid surfaces upon instantiation
            if (navMeshBuilder != null)
            {
                navMeshBuilder.RebuildNavMesh();
            }

            // 6. Populate Islands (Pillars, Objectives, Backdrops, Enemies) & Refresh NavMesh with Obstacles
            for (int i = 0; i < graphNodes.Count; i++)
            {
                IslandGraphNode gNode = graphNodes[i];
                System.Random islandRng = new System.Random(runSeed + gNode.id * 997);
                contentSpawner.PopulateIsland(gNode.spawnedIsland, config, islandRng, initialDdaContext);
            }

            if (navMeshBuilder != null)
            {
                navMeshBuilder.RebuildNavMesh();
            }

            return spawnedIslands.Count > 0 ? spawnedIslands[0] : null;
        }

        public void ApplyDifficultyToUnvisitedIslands(DifficultyContext ctx)
        {
            for (int i = 0; i < spawnedIslands.Count; i++)
            {
                IslandNode island = spawnedIslands[i];
                if (island == null) continue;

                RoomController rc = island.GetComponentInChildren<RoomController>();
                if (rc != null && (rc.HasEntered || rc.IsCleared))
                    continue;

                for (int e = 0; e < island.SpawnedEnemies.Count; e++)
                {
                    if (island.SpawnedEnemies[e] != null)
                    {
                        island.SpawnedEnemies[e].ApplyDifficulty(ctx);
                    }
                }
            }
        }

        private void BuildIslandGraph(System.Random rng)
        {
            float zStep = config.depthForwardSpacing;
            float yStep = config.terraceElevationStep;
            float xBranch = config.branchLateralOffset;

            IslandEncounterType[] midPool = new IslandEncounterType[]
            {
                IslandEncounterType.Skirmish,
                IslandEncounterType.SunBeaconSanctuary,
                IslandEncounterType.ColossusArena,
                IslandEncounterType.StormSurgeTower
            };

            // Depth 0: Starting Island (ID 0)
            var d0 = new IslandGraphNode
            {
                id = 0,
                depth = 0,
                branchIndex = 0,
                encounterType = IslandEncounterType.StartingSkirmish,
                worldPosition = new Vector3(0f, 0f, 0f),
                arenaSize = config.normalArenaSize
            };
            d0.nextNodeIds.Add(1);
            d0.nextNodeIds.Add(2);
            graphNodes.Add(d0);

            // Depth 1: Left (ID 1) & Right (ID 2) at Y = +2.5m
            var d1Left = new IslandGraphNode
            {
                id = 1,
                depth = 1,
                branchIndex = -1,
                encounterType = midPool[rng.Next(0, 2)],
                worldPosition = new Vector3(-xBranch, yStep * 1f, zStep * 1f),
                arenaSize = config.normalArenaSize
            };
            d1Left.nextNodeIds.Add(3);

            var d1Right = new IslandGraphNode
            {
                id = 2,
                depth = 1,
                branchIndex = 1,
                encounterType = IslandEncounterType.SunBeaconSanctuary,
                worldPosition = new Vector3(xBranch, yStep * 1f, zStep * 1f),
                arenaSize = config.normalArenaSize
            };
            d1Right.nextNodeIds.Add(4);
            graphNodes.Add(d1Left);
            graphNodes.Add(d1Right);

            // Depth 2: Left (ID 3) & Right (ID 4) at Y = +5.0m
            var d2Left = new IslandGraphNode
            {
                id = 3,
                depth = 2,
                branchIndex = -1,
                encounterType = IslandEncounterType.ColossusArena,
                worldPosition = new Vector3(-xBranch, yStep * 2f, zStep * 2f),
                arenaSize = config.normalArenaSize
            };
            d2Left.nextNodeIds.Add(5);

            var d2Right = new IslandGraphNode
            {
                id = 4,
                depth = 2,
                branchIndex = 1,
                encounterType = IslandEncounterType.StormSurgeTower,
                worldPosition = new Vector3(xBranch, yStep * 2f, zStep * 2f),
                arenaSize = config.normalArenaSize
            };
            d2Right.nextNodeIds.Add(6);
            graphNodes.Add(d2Left);
            graphNodes.Add(d2Right);

            // Depth 3: Left (ID 5) & Right (ID 6) at Y = +7.5m
            var d3Left = new IslandGraphNode
            {
                id = 5,
                depth = 3,
                branchIndex = -1,
                encounterType = midPool[rng.Next(0, midPool.Length)],
                worldPosition = new Vector3(-xBranch * 0.75f, yStep * 3f, zStep * 3f),
                arenaSize = config.normalArenaSize
            };
            d3Left.nextNodeIds.Add(7);

            var d3Right = new IslandGraphNode
            {
                id = 6,
                depth = 3,
                branchIndex = 1,
                encounterType = IslandEncounterType.ColossusArena,
                worldPosition = new Vector3(xBranch * 0.75f, yStep * 3f, zStep * 3f),
                arenaSize = config.normalArenaSize
            };
            d3Right.nextNodeIds.Add(7);
            graphNodes.Add(d3Left);
            graphNodes.Add(d3Right);

            // Depth 4: Sky Sanctum Boss Arena (ID 7) at Y = +10.0m
            var d4Boss = new IslandGraphNode
            {
                id = 7,
                depth = 4,
                branchIndex = 0,
                encounterType = IslandEncounterType.BossSanctum,
                worldPosition = new Vector3(0f, yStep * 4f, zStep * 4f),
                arenaSize = config.bossArenaSize
            };
            graphNodes.Add(d4Boss);
        }

        private IslandNode SpawnIslandPlatform(IslandGraphNode gNode)
        {
            GameObject islandObj;
            if (config.islandPlatformPrefab != null)
            {
                islandObj = Instantiate(config.islandPlatformPrefab, gNode.worldPosition, Quaternion.identity, worldRoot);
            }
            else
            {
                islandObj = new GameObject();
                islandObj.transform.SetParent(worldRoot, false);
                islandObj.transform.position = gNode.worldPosition;
            }

            islandObj.name = $"Island_D{gNode.depth}_ID{gNode.id}_{gNode.encounterType}";

            Transform slab = islandObj.transform.Find("PlatformSlab");
            if (slab != null)
            {
                slab.localScale = new Vector3(gNode.arenaSize.x, 3.0f, gNode.arenaSize.y);
            }
            Transform visualArch = islandObj.transform.Find("IslandVisualArch");
            if (visualArch != null)
            {
                visualArch.localScale = new Vector3(gNode.arenaSize.x / 22f, 1f, gNode.arenaSize.y / 22f);
            }

            GameObject entranceObj = new GameObject("EntranceSpawnPoint");
            entranceObj.transform.SetParent(islandObj.transform, false);
            entranceObj.transform.localPosition = new Vector3(0f, 0.1f, -gNode.arenaSize.y * 0.38f);

            IslandNode node = islandObj.GetComponent<IslandNode>();
            if (node == null) node = islandObj.AddComponent<IslandNode>();
            node.ConfigureIsland(gNode.id, gNode.depth, gNode.encounterType, gNode.arenaSize, entranceObj.transform);

            if (config.lockdownBarrierPrefab != null)
            {
                Vector3 northBarrierPos = gNode.worldPosition + new Vector3(0f, 0.9f, gNode.arenaSize.y * 0.48f);
                GameObject northBarrier = Instantiate(config.lockdownBarrierPrefab, northBarrierPos, Quaternion.identity, islandObj.transform);
                northBarrier.SetActive(false);
                node.RegisterLockdownBarrier(northBarrier);
            }

            GameObject zoneObj = new GameObject("RoomZoneTrigger");
            zoneObj.tag = "RoomZone";
            int triggerLayer = LayerMask.NameToLayer("TriggerZone");
            if (triggerLayer >= 0) zoneObj.layer = triggerLayer;
            zoneObj.transform.SetParent(islandObj.transform, false);
            zoneObj.transform.localPosition = new Vector3(0f, 1.5f, 0f);

            BoxCollider box = zoneObj.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(gNode.arenaSize.x * 0.86f, 4.0f, gNode.arenaSize.y * 0.86f);

            RoomController rc = zoneObj.AddComponent<RoomController>();
            IRoomChallenge challenge = AttachChallengeForEncounter(zoneObj, gNode.encounterType);
            rc.InitializeRoom(node, challenge);

            return node;
        }

        private IRoomChallenge AttachChallengeForEncounter(GameObject zoneObj, IslandEncounterType type)
        {
            switch (type)
            {
                case IslandEncounterType.SunBeaconSanctuary:
                    return zoneObj.AddComponent<SunBeaconChallenge>();
                case IslandEncounterType.ColossusArena:
                    return zoneObj.AddComponent<ColossusChallenge>();
                case IslandEncounterType.StormSurgeTower:
                    return zoneObj.AddComponent<StormSurgeChallenge>();
                case IslandEncounterType.BossSanctum:
                    return zoneObj.AddComponent<BossChallenge>();
                default:
                    return zoneObj.AddComponent<SkirmishChallenge>();
            }
        }

        private void SpawnBridgesAndGatesForIsland(
            IslandGraphNode fromNode,
            Dictionary<int, IslandGraphNode> lookup,
            System.Random rng,
            float ddaMult)
        {
            int nextCount = fromNode.nextNodeIds.Count;
            if (nextCount == 0) return;

            // Jika menuju Boss (Depth 3 ke Depth 4), kita paksa agar tetap membuat 2 jalur (Kiri & Kanan) 
            // meskipun target ID-nya sama (ID 7), supaya jembatan dan gerbangnya jadi sepasang (kembar).
            bool isBossTransition = (fromNode.depth == 3 && nextCount == 1);
            int effectiveCount = isBossTransition ? 2 : nextCount;

            int targetA = fromNode.nextNodeIds[0];
            int targetB = effectiveCount > 1 ? (isBossTransition ? targetA : fromNode.nextNodeIds[1]) : targetA;

            MathGateSelector.GenerateGatePair(
                rng,
                targetA,
                targetB,
                ddaMult,
                out MathGateOptionData safeData,
                out MathGateOptionData riskyData
            );

            MathGateDoor doorA = null;
            MathGateDoor doorB = null;

            for (int i = 0; i < effectiveCount; i++)
            {
                int toId = isBossTransition ? fromNode.nextNodeIds[0] : fromNode.nextNodeIds[i];
                if (!lookup.TryGetValue(toId, out IslandGraphNode toNode)) continue;

                // Beri jarak offset X agar kedua jembatan terpisah ke kiri dan kanan dengan rapi
                float xOffset = (effectiveCount > 1) ? (i == 0 ? -3.5f : 3.5f) : 0f;

                Vector3 startEdge = fromNode.worldPosition + new Vector3(xOffset, 0f, fromNode.arenaSize.y * 0.48f);
                Vector3 endEdge = toNode.worldPosition + new Vector3(xOffset * 0.3f, 0f, -toNode.arenaSize.y * 0.48f);

                // Buat segmen jembatan fisik untuk masing-masing sisi
                SpawnBridgeSegment(startEdge, endEdge);

                if (config.mathGateDoorPrefab != null)
                {
                    Vector3 gateDir = (endEdge - startEdge);
                    gateDir.y = 0f;
                    Quaternion gateRot = gateDir.sqrMagnitude > 0.01f
                        ? Quaternion.LookRotation(gateDir.normalized, Vector3.up)
                        : Quaternion.identity;

                    Vector3 gatePos = startEdge + gateDir.normalized * 1.1f;
                    GameObject gateObj = Instantiate(config.mathGateDoorPrefab, gatePos, gateRot, fromNode.spawnedIsland.transform);
                    MathGateDoor door = gateObj.GetComponent<MathGateDoor>();
                    if (door != null)
                    {
                        bool isRiskSlot = (i == 1);
                        door.ConfigureGate(isRiskSlot ? riskyData : safeData, null, false);
                        fromNode.spawnedIsland.RegisterMathGateDoor(door);

                        if (i == 0) doorA = door;
                        else if (i == 1) doorB = door;
                    }
                }
            }

            if (doorA != null && doorB != null)
            {
                doorA.SetPartnerDoor(doorB);
                doorB.SetPartnerDoor(doorA);
            }
        }

        private void SpawnBridgeSegment(Vector3 startEdge, Vector3 endEdge)
        {
            Vector3 delta = endEdge - startEdge;
            float length = delta.magnitude;
            if (length < 0.5f) return;

            Vector3 midPoint = (startEdge + endEdge) * 0.5f;
            Quaternion rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);

            GameObject bridgeObj;
            if (config.archedBridgePrefab != null)
            {
                bridgeObj = Instantiate(config.archedBridgePrefab, midPoint, rotation, worldRoot);
            }
            else
            {
                bridgeObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bridgeObj.transform.SetParent(worldRoot, false);
                bridgeObj.transform.SetPositionAndRotation(midPoint, rotation);
            }

            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer >= 0) bridgeObj.layer = groundLayer;

            Transform deckCollider = bridgeObj.transform.Find("BridgeDeckCollider");
            Transform tileTemplate = bridgeObj.transform.Find("BridgeTileTemplate");
            if (deckCollider != null)
            {
                deckCollider.localScale = new Vector3(4.2f, 0.45f, length + 1.4f);

                if (tileTemplate != null)
                {
                    float spanStep = 3.8f;
                    int spanCount = Mathf.Max(2, Mathf.CeilToInt((length + 0.8f) / spanStep));
                    float actualStep = (length + 0.8f) / spanCount;
                    float startZ = -actualStep * (spanCount - 1) * 0.5f;

                    for (int s = 0; s < spanCount; s++)
                    {
                        GameObject span = Instantiate(tileTemplate.gameObject, bridgeObj.transform, false);
                        span.name = $"BridgeSpan_{s}";
                        span.SetActive(true);
                        span.transform.localPosition = new Vector3(0f, 0f, startZ + s * actualStep);
                        span.transform.localRotation = Quaternion.identity;
                        span.transform.localScale = new Vector3(1f, 1f, actualStep / 3.9f);
                    }
                    tileTemplate.gameObject.SetActive(false);
                }
            }
            else
            {
                bridgeObj.transform.localScale = new Vector3(4.2f, 0.45f, length + 1.2f);
            }

            spawnedBridges.Add(bridgeObj);
        }
    }
}