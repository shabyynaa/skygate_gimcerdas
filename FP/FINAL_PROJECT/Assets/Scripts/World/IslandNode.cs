using System.Collections.Generic;
using UnityEngine;
using SkyGate.EnemyAI;
using SkyGate.MathGate;
using SkyGate.World.Interactables;

namespace SkyGate.World
{
    public enum IslandEncounterType
    {
        StartingSkirmish = 0,
        Skirmish = 1,
        SunBeaconSanctuary = 2,
        ColossusArena = 3,
        StormSurgeTower = 4,
        BossSanctum = 5
    }

    public class IslandNode : MonoBehaviour
    {
        [Header("Island Identity")]
        [SerializeField] private int islandId;
        [SerializeField] private int depth;
        [SerializeField] private IslandEncounterType encounterType;
        [SerializeField] private Vector2 arenaSize = new Vector2(22f, 22f);

        [Header("Anchors & Zones")]
        [SerializeField] private Transform entranceSpawnPoint;
        [SerializeField] private List<Transform> exitBridgeAnchors = new List<Transform>();
        [SerializeField] private List<GameObject> lockdownBarriers = new List<GameObject>();
        [SerializeField] private List<MathGateDoor> mathGateDoors = new List<MathGateDoor>();

        [Header("Spawned Content")]
        [SerializeField] private List<EnemyBrain> spawnedEnemies = new List<EnemyBrain>();
        [SerializeField] private List<SunBeacon> sunBeacons = new List<SunBeacon>();
        [SerializeField] private List<RuneAnchor> runeAnchors = new List<RuneAnchor>();
        [SerializeField] private List<StormCrystal> stormCrystals = new List<StormCrystal>();
        [SerializeField] private List<HealOrb> healOrbs = new List<HealOrb>();
        [SerializeField] private VictoryChest victoryChest;

        public int IslandId => islandId;
        public int Depth => depth;
        public IslandEncounterType EncounterType => encounterType;
        public Vector2 ArenaSize => arenaSize;

        public Bounds RoomBounds => new Bounds(
            transform.position + new Vector3(0f, 2f, 0f),
            new Vector3(arenaSize.x, 8f, arenaSize.y)
        );

        public Transform EntranceSpawnPoint => entranceSpawnPoint != null ? entranceSpawnPoint : transform;
        public List<Transform> ExitBridgeAnchors => exitBridgeAnchors;
        public List<GameObject> LockdownBarriers => lockdownBarriers;
        public List<MathGateDoor> MathGateDoors => mathGateDoors;

        public List<EnemyBrain> SpawnedEnemies => spawnedEnemies;
        public List<SunBeacon> SunBeacons => sunBeacons;
        public List<RuneAnchor> RuneAnchors => runeAnchors;
        public List<StormCrystal> StormCrystals => stormCrystals;
        public List<HealOrb> HealOrbs => healOrbs;
        public VictoryChest VictoryChest => victoryChest;

        public void ConfigureIsland(int id, int islandDepth, IslandEncounterType type, Vector2 size, Transform entrance)
        {
            islandId = id;
            depth = islandDepth;
            encounterType = type;
            arenaSize = size;
            entranceSpawnPoint = entrance;
        }

        public void RegisterSpawnedEnemy(EnemyBrain enemy)
        {
            if (enemy != null && !spawnedEnemies.Contains(enemy))
                spawnedEnemies.Add(enemy);
        }

        public void RegisterSunBeacon(SunBeacon beacon)
        {
            if (beacon != null && !sunBeacons.Contains(beacon))
                sunBeacons.Add(beacon);
        }

        public void RegisterRuneAnchor(RuneAnchor anchor)
        {
            if (anchor != null && !runeAnchors.Contains(anchor))
                runeAnchors.Add(anchor);
        }

        public void RegisterStormCrystal(StormCrystal crystal)
        {
            if (crystal != null && !stormCrystals.Contains(crystal))
                stormCrystals.Add(crystal);
        }

        public void RegisterHealOrb(HealOrb orb)
        {
            if (orb != null && !healOrbs.Contains(orb))
                healOrbs.Add(orb);
        }

        public void RegisterLockdownBarrier(GameObject barrier)
        {
            if (barrier != null && !lockdownBarriers.Contains(barrier))
                lockdownBarriers.Add(barrier);
        }

        public void RegisterMathGateDoor(MathGateDoor door)
        {
            if (door != null && !mathGateDoors.Contains(door))
                mathGateDoors.Add(door);
        }

        public void SetVictoryChest(VictoryChest chest)
        {
            victoryChest = chest;
        }

        public void SetLockdownBarriersActive(bool active)
        {
            for (int i = 0; i < lockdownBarriers.Count; i++)
            {
                if (lockdownBarriers[i] != null)
                    lockdownBarriers[i].SetActive(active);
            }
        }

        public void SetExitGatesUnlocked(bool unlocked)
        {
            for (int i = 0; i < mathGateDoors.Count; i++)
            {
                if (mathGateDoors[i] != null)
                    mathGateDoors[i].SetUnlocked(unlocked);
            }
        }

        public int GetAliveEnemyCount()
        {
            int count = 0;
            for (int i = 0; i < spawnedEnemies.Count; i++)
            {
                EnemyBrain b = spawnedEnemies[i];
                if (b != null && b.Health != null && b.Health.IsAlive && b.CurrentState != EnemyState.Dead)
                {
                    count++;
                }
            }
            return count;
        }
    }
}
