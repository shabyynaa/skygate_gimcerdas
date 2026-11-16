using System.Collections.Generic;
using UnityEngine;
using SkyGate.EnemyAI;

namespace SkyGate.PCG
{
    [CreateAssetMenu(fileName = "PCGConfig", menuName = "SkyGate/PCG Config")]
    public class PCGConfigSO : ScriptableObject
    {
        [Header("Step-Terrace Topology (GDD Section 2.3 & 8.1)")]
        public float terraceElevationStep = 2.5f;
        public float depthForwardSpacing = 32.0f;
        public float branchLateralOffset = 14.5f;
        public Vector2 normalArenaSize = new Vector2(22f, 22f);
        public Vector2 bossArenaSize = new Vector2(26f, 26f);

        [Header("Validation Rules (GDD Section 8.3)")]
        public float safeZoneRadius = 5.0f;
        public float pillarMinSpacing = 3.5f;
        public float objectiveMinSpacing = 4.5f;
        public float enemyMinSpacing = 2.5f;
        public int maxAttemptsPerObject = 30;

        [Header("World & Gameplay Prefabs")]
        public GameObject islandPlatformPrefab;
        public GameObject archedBridgePrefab;
        public GameObject lockdownBarrierPrefab;
        public GameObject mathGateDoorPrefab;
        public GameObject stunPillarPrefab;
        public GameObject sunBeaconPrefab;
        public GameObject runeAnchorPrefab;
        public GameObject stormCrystalPrefab;
        public GameObject healOrbPrefab;
        public GameObject victoryChestPrefab;

        [Header("Perimeter Backdrop Prefabs (FantasyTown & Nature)")]
        public List<GameObject> backdropPrefabs = new List<GameObject>();

        [Header("Enemy Prefabs & Archetypes")]
        public GameObject breezeImpPrefab;
        public EnemyArchetypeSO breezeImpArchetype;

        public GameObject skySpritePrefab;
        public EnemyArchetypeSO skySpriteArchetype;

        public GameObject duskWispPrefab;
        public EnemyArchetypeSO duskWispArchetype;

        public GameObject crystalGolemPrefab;
        public EnemyArchetypeSO crystalGolemArchetype;

        public GameObject zephyrGuardianPrefab;
        public EnemyArchetypeSO zephyrGuardianArchetype;
    }
}
