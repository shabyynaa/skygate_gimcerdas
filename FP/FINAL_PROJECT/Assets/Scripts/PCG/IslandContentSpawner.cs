using System.Collections.Generic;
using UnityEngine;
using SkyGate.Core;
using SkyGate.EnemyAI;
using SkyGate.EnemyAI.Behaviors;
using SkyGate.World;
using SkyGate.World.Interactables;

namespace SkyGate.PCG
{
    public class IslandContentSpawner : MonoBehaviour
    {
        private int wallAndObstacleMask;

        private void Awake()
        {
            int wallLayer = LayerMask.NameToLayer("Wall");
            int obsLayer = LayerMask.NameToLayer("Obstacle");
            wallAndObstacleMask = 0;
            if (wallLayer >= 0) wallAndObstacleMask |= (1 << wallLayer);
            if (obsLayer >= 0) wallAndObstacleMask |= (1 << obsLayer);
        }

        /// <summary>
        /// Populates an IslandNode using deterministic System.Random and enforces
        /// the 3 Mandatory PCG Validation Rules (GDD Section 8.3 & AI_RULES Section 2.3).
        /// </summary>
        public void PopulateIsland(
            IslandNode island,
            PCGConfigSO config,
            System.Random rng,
            DifficultyContext ddaContext)
        {
            if (island == null || config == null) return;

            Vector3 center = island.transform.position;
            Vector3 entrancePos = island.EntranceSpawnPoint != null
                ? island.EntranceSpawnPoint.position
                : center + new Vector3(0f, 0f, -island.ArenaSize.y * 0.38f);

            List<Vector3> placedPillars = new List<Vector3>();
            List<Vector3> placedObjectives = new List<Vector3>();
            List<Vector3> placedEnemies = new List<Vector3>();

            // 1. Place Stun Pillars (2-3 per island, 3 in Colossus Arena & Boss Sanctum)
            int pillarCount = (island.EncounterType == IslandEncounterType.ColossusArena || island.EncounterType == IslandEncounterType.BossSanctum)
                ? 3
                : rng.Next(2, 4);

            for (int i = 0; i < pillarCount; i++)
            {
                if (config.stunPillarPrefab == null) break;
                Vector3 pos = FindValidPosition(
                    rng,
                    center,
                    island.ArenaSize,
                    entrancePos,
                    config.safeZoneRadius * 0.75f,
                    config.pillarMinSpacing,
                    placedPillars,
                    config.maxAttemptsPerObject,
                    i
                );
                placedPillars.Add(pos);
                GameObject pillar = Instantiate(config.stunPillarPrefab, pos, Quaternion.Euler(0f, rng.Next(0, 4) * 90f, 0f), island.transform);
                pillar.tag = "StunPillar";
            }

            // 2. Place Encounter-Specific Tactical Objectives
            switch (island.EncounterType)
            {
                case IslandEncounterType.SunBeaconSanctuary:
                    for (int i = 0; i < 2; i++)
                    {
                        if (config.sunBeaconPrefab == null) break;
                        Vector3 pos = FindValidPosition(
                            rng, center, island.ArenaSize, entrancePos,
                            config.safeZoneRadius * 0.7f, config.objectiveMinSpacing,
                            placedObjectives, config.maxAttemptsPerObject, i
                        );
                        placedObjectives.Add(pos);
                        placedPillars.Add(pos);
                        GameObject obj = Instantiate(config.sunBeaconPrefab, pos, Quaternion.identity, island.transform);
                        SunBeacon beacon = obj.GetComponent<SunBeacon>();
                        if (beacon != null) island.RegisterSunBeacon(beacon);
                    }
                    break;

                case IslandEncounterType.ColossusArena:
                    for (int i = 0; i < 2; i++)
                    {
                        if (config.runeAnchorPrefab == null) break;
                        Vector3 pos = FindValidPosition(
                            rng, center, island.ArenaSize, entrancePos,
                            config.safeZoneRadius * 0.7f, config.objectiveMinSpacing,
                            placedObjectives, config.maxAttemptsPerObject, i
                        );
                        placedObjectives.Add(pos);
                        placedPillars.Add(pos);
                        GameObject obj = Instantiate(config.runeAnchorPrefab, pos, Quaternion.identity, island.transform);
                        RuneAnchor anchor = obj.GetComponent<RuneAnchor>();
                        if (anchor != null) island.RegisterRuneAnchor(anchor);
                    }
                    break;

                case IslandEncounterType.StormSurgeTower:
                    for (int i = 0; i < 2; i++)
                    {
                        if (config.stormCrystalPrefab == null) break;
                        Vector3 pos = FindValidPosition(
                            rng, center, island.ArenaSize, entrancePos,
                            config.safeZoneRadius, config.objectiveMinSpacing,
                            placedObjectives, config.maxAttemptsPerObject, i
                        );
                        placedObjectives.Add(pos);
                        placedPillars.Add(pos);
                        GameObject obj = Instantiate(config.stormCrystalPrefab, pos, Quaternion.identity, island.transform);
                        StormCrystal crystal = obj.GetComponent<StormCrystal>();
                        if (crystal != null) island.RegisterStormCrystal(crystal);
                    }
                    break;

                case IslandEncounterType.BossSanctum:
                    if (config.victoryChestPrefab != null)
                    {
                        Vector3 chestPos = center + new Vector3(0f, 0f, island.ArenaSize.y * 0.32f);
                        GameObject chestObj = Instantiate(config.victoryChestPrefab, chestPos, Quaternion.Euler(0f, 180f, 0f), island.transform);
                        VictoryChest chest = chestObj.GetComponent<VictoryChest>();
                        if (chest != null)
                        {
                            chest.SetUnlocked(false);
                            island.SetVictoryChest(chest);
                        }
                    }
                    break;
            }

            // 3. Place Heal Orbs (1 if normal, 2 if player struggling in DDA)
            int healCount = ddaContext.difficultyMultiplier < 0.92f ? 2 : (island.Depth >= 2 ? 1 : 0);
            for (int i = 0; i < healCount; i++)
            {
                if (config.healOrbPrefab == null) break;
                Vector3 pos = FindValidPosition(
                    rng, center, island.ArenaSize, entrancePos,
                    3.0f, 3.0f, placedObjectives, config.maxAttemptsPerObject, i + 2
                );
                placedObjectives.Add(pos);
                GameObject orbObj = Instantiate(config.healOrbPrefab, pos + Vector3.up * 0.7f, Quaternion.identity, island.transform);
                HealOrb orb = orbObj.GetComponent<HealOrb>();
                if (orb != null) island.RegisterHealOrb(orb);
            }

            // 4. Spawn Enemies (Rule 1: >= 5.0m from Player Entrance Spawn Point)
            SpawnEncounterEnemies(island, config, rng, ddaContext, center, entrancePos, placedEnemies);

            // 5. Place Perimeter Scenic Backdrops (North, East, West edges only; South kept open for camera)
            SpawnPerimeterBackdrops(island, config, rng, center);
        }

        private void SpawnEncounterEnemies(
            IslandNode island,
            PCGConfigSO config,
            System.Random rng,
            DifficultyContext ddaContext,
            Vector3 center,
            Vector3 entrancePos,
            List<Vector3> placedEnemies)
        {
            switch (island.EncounterType)
            {
                case IslandEncounterType.StartingSkirmish:
                    SpawnSingleEnemy(island, config.breezeImpPrefab, config.breezeImpArchetype, rng, ddaContext, center, entrancePos, config, placedEnemies, 0);
                    SpawnSingleEnemy(island, config.breezeImpPrefab, config.breezeImpArchetype, rng, ddaContext, center, entrancePos, config, placedEnemies, 1);
                    SpawnSingleEnemy(island, config.skySpritePrefab, config.skySpriteArchetype, rng, ddaContext, center, entrancePos, config, placedEnemies, 2);
                    break;

                case IslandEncounterType.Skirmish:
                    SpawnSingleEnemy(island, config.breezeImpPrefab, config.breezeImpArchetype, rng, ddaContext, center, entrancePos, config, placedEnemies, 0);
                    SpawnSingleEnemy(island, config.breezeImpPrefab, config.breezeImpArchetype, rng, ddaContext, center, entrancePos, config, placedEnemies, 1);
                    SpawnSingleEnemy(island, config.skySpritePrefab, config.skySpriteArchetype, rng, ddaContext, center, entrancePos, config, placedEnemies, 2);
                    if (ddaContext.difficultyMultiplier >= 1.05f || island.Depth >= 2)
                    {
                        SpawnSingleEnemy(island, config.skySpritePrefab, config.skySpriteArchetype, rng, ddaContext, center, entrancePos, config, placedEnemies, 3);
                    }
                    break;

                case IslandEncounterType.SunBeaconSanctuary:
                    SpawnSingleEnemy(island, config.duskWispPrefab, config.duskWispArchetype, rng, ddaContext, center, entrancePos, config, placedEnemies, 0);
                    SpawnSingleEnemy(island, config.duskWispPrefab, config.duskWispArchetype, rng, ddaContext, center, entrancePos, config, placedEnemies, 1);
                    SpawnSingleEnemy(island, config.breezeImpPrefab, config.breezeImpArchetype, rng, ddaContext, center, entrancePos, config, placedEnemies, 2);
                    break;

                case IslandEncounterType.ColossusArena:
                    SpawnSingleEnemy(island, config.crystalGolemPrefab, config.crystalGolemArchetype, rng, ddaContext, center, entrancePos, config, placedEnemies, 0);
                    SpawnSingleEnemy(island, config.skySpritePrefab, config.skySpriteArchetype, rng, ddaContext, center, entrancePos, config, placedEnemies, 1);
                    if (ddaContext.difficultyMultiplier >= 1.08f)
                    {
                        SpawnSingleEnemy(island, config.breezeImpPrefab, config.breezeImpArchetype, rng, ddaContext, center, entrancePos, config, placedEnemies, 2);
                    }
                    break;

                case IslandEncounterType.StormSurgeTower:
                    SpawnSingleEnemy(island, config.skySpritePrefab, config.skySpriteArchetype, rng, ddaContext, center, entrancePos, config, placedEnemies, 0);
                    SpawnSingleEnemy(island, config.duskWispPrefab, config.duskWispArchetype, rng, ddaContext, center, entrancePos, config, placedEnemies, 1);
                    SpawnSingleEnemy(island, config.breezeImpPrefab, config.breezeImpArchetype, rng, ddaContext, center, entrancePos, config, placedEnemies, 2);
                    break;

                case IslandEncounterType.BossSanctum:
                    Vector3 bossPos = center + new Vector3(0f, 0f, island.ArenaSize.y * 0.14f);
                    placedEnemies.Add(bossPos);
                    if (config.zephyrGuardianPrefab != null)
                    {
                        GameObject bossObj = Instantiate(config.zephyrGuardianPrefab, bossPos, Quaternion.Euler(0f, 180f, 0f), island.transform);
                        EnemyBrain bossBrain = bossObj.GetComponent<EnemyBrain>();
                        if (bossBrain != null)
                        {
                            bossBrain.SetupEnemy(config.zephyrGuardianArchetype, island, ddaContext);
                            BossPhaseBehavior bossPhase = bossObj.GetComponent<BossPhaseBehavior>();
                            if (bossPhase != null)
                            {
                                bossPhase.SetMinionTemplate(config.breezeImpPrefab, config.breezeImpArchetype);
                            }
                            island.RegisterSpawnedEnemy(bossBrain);
                        }
                    }
                    break;
            }
        }

        private void SpawnSingleEnemy(
            IslandNode island,
            GameObject prefab,
            EnemyArchetypeSO archetype,
            System.Random rng,
            DifficultyContext ddaContext,
            Vector3 center,
            Vector3 entrancePos,
            PCGConfigSO config,
            List<Vector3> placedEnemies,
            int fallbackIndex)
        {
            if (prefab == null) return;

            Vector3 spawnPos = FindValidPosition(
                rng,
                center,
                island.ArenaSize,
                entrancePos,
                config.safeZoneRadius,
                config.enemyMinSpacing,
                placedEnemies,
                config.maxAttemptsPerObject,
                fallbackIndex
            );
            placedEnemies.Add(spawnPos);

            GameObject enemyObj = Instantiate(prefab, spawnPos, Quaternion.Euler(0f, 180f, 0f), island.transform);
            EnemyBrain brain = enemyObj.GetComponent<EnemyBrain>();
            if (brain != null)
            {
                brain.SetupEnemy(archetype, island, ddaContext);
                island.RegisterSpawnedEnemy(brain);
            }
        }

        private Vector3 FindValidPosition(
            System.Random rng,
            Vector3 islandCenter,
            Vector2 arenaSize,
            Vector3 entrancePos,
            float minSafeZoneDist,
            float minSpacing,
            List<Vector3> existingPositions,
            int maxAttempts,
            int fallbackQuadrant)
        {
            float halfW = Mathf.Max(4f, arenaSize.x * 0.5f - 3.2f);
            float halfL = Mathf.Max(4f, arenaSize.y * 0.5f - 3.2f);

            int attempts = Mathf.Max(5, maxAttempts);
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                float rx = (float)(rng.NextDouble() * 2.0 - 1.0) * halfW;
                float rz = (float)(rng.NextDouble() * 2.0 - 1.0) * halfL;
                Vector3 candidate = new Vector3(islandCenter.x + rx, islandCenter.y, islandCenter.z + rz);

                // Rule 1: Player Spawn Safe Zone (>= 5.0m from island entrance)
                float distToEntrance = Vector2.Distance(
                    new Vector2(candidate.x, candidate.z),
                    new Vector2(entrancePos.x, entrancePos.z));
                if (distToEntrance < minSafeZoneDist)
                    continue;

                // Rule 2: Obstacle & Wall Clearance via Physics.CheckSphere
                if (wallAndObstacleMask != 0 && Physics.CheckSphere(candidate + Vector3.up * 0.9f, 0.85f, wallAndObstacleMask, QueryTriggerInteraction.Ignore))
                    continue;

                // Rule 3: Minimum Inter-Object Spacing
                bool tooClose = false;
                for (int i = 0; i < existingPositions.Count; i++)
                {
                    float d = Vector2.Distance(
                        new Vector2(candidate.x, candidate.z),
                        new Vector2(existingPositions[i].x, existingPositions[i].z));
                    if (d < minSpacing)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) continue;

                return candidate;
            }

            // Pre-validated safe quadrant fallback (guaranteed > 5m from South entrance)
            Vector3[] fallbackOffsets = new Vector3[]
            {
                new Vector3(-halfW * 0.55f, 0f, halfL * 0.35f),
                new Vector3( halfW * 0.55f, 0f, halfL * 0.35f),
                new Vector3(-halfW * 0.45f, 0f, 0.5f),
                new Vector3( halfW * 0.45f, 0f, 0.5f)
            };
            Vector3 offset = fallbackOffsets[Mathf.Abs(fallbackQuadrant) % fallbackOffsets.Length];
            return islandCenter + offset;
        }

        private void SpawnPerimeterBackdrops(
            IslandNode island,
            PCGConfigSO config,
            System.Random rng,
            Vector3 center)
        {
            if (config.backdropPrefabs == null || config.backdropPrefabs.Count == 0)
                return;

            float halfW = island.ArenaSize.x * 0.5f;
            float halfL = island.ArenaSize.y * 0.5f;
            float outerOffsetX = halfW + 10.2f;

            // Keep sibling branch gap (-14.5 vs +14.5) clear: place side dioramas on open sky flanks
            var backdropSlots = new List<(Vector3 pos, float yRot)>();
            if (center.x <= 1.0f)
            {
                backdropSlots.Add((center + new Vector3(-outerOffsetX, -0.8f, halfL * 0.42f), 90f));
                backdropSlots.Add((center + new Vector3(-outerOffsetX - 2.4f, -2.6f, -halfL * 0.35f), 65f));
            }
            if (center.x >= -1.0f)
            {
                backdropSlots.Add((center + new Vector3(outerOffsetX, -0.5f, halfL * 0.42f), -90f));
                backdropSlots.Add((center + new Vector3(outerOffsetX + 2.4f, -2.2f, -halfL * 0.35f), -65f));
            }

            int backLayer = LayerMask.NameToLayer("BackgroundProp");
            for (int i = 0; i < backdropSlots.Count; i++)
            {
                int idx = rng.Next(0, config.backdropPrefabs.Count);
                GameObject prefab = config.backdropPrefabs[idx];
                if (prefab == null) continue;

                GameObject prop = Instantiate(
                    prefab,
                    backdropSlots[i].pos,
                    Quaternion.Euler(0f, backdropSlots[i].yRot, 0f),
                    island.transform);
                if (backLayer >= 0)
                    SetLayerRecursively(prop, backLayer);
            }
        }

        private void SetLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
    }
}
