using UnityEngine;
using SkyGate.Core;

namespace SkyGate.World
{
    [RequireComponent(typeof(Collider))]
    public class RoomController : MonoBehaviour
    {
        [SerializeField] private IslandNode islandNode;

        private IRoomChallenge activeChallenge;
        private bool hasEntered;
        private bool isCleared;
        private float roomStartTime;
        private Transform cachedPlayer;

        public IslandNode Island => islandNode;
        public IRoomChallenge ActiveChallenge => activeChallenge;
        public bool HasEntered => hasEntered;
        public bool IsCleared => isCleared;
        public Transform CachedPlayer => cachedPlayer;

        private void Awake()
        {
            if (islandNode == null)
                islandNode = GetComponentInParent<IslandNode>();

            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        public void InitializeRoom(IslandNode node, IRoomChallenge challenge)
        {
            islandNode = node;
            activeChallenge = challenge;
            hasEntered = false;
            isCleared = false;

            if (islandNode != null)
            {
                islandNode.SetLockdownBarriersActive(false);
                islandNode.SetExitGatesUnlocked(false);
            }

            if (activeChallenge != null && islandNode != null)
            {
                activeChallenge.InitializeChallenge(islandNode, this);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isCleared || hasEntered) return;
            if (!other.CompareTag("Player")) return;

            cachedPlayer = other.transform;
            ActivateRoomEncounter();
        }

        public void ActivateRoomEncounter()
        {
            if (hasEntered || isCleared || islandNode == null) return;

            hasEntered = true;
            roomStartTime = Time.time;

            islandNode.SetLockdownBarriersActive(true);
            islandNode.SetExitGatesUnlocked(false);

            if (activeChallenge == null)
            {
                activeChallenge = GetComponent<IRoomChallenge>();
                if (activeChallenge != null)
                    activeChallenge.InitializeChallenge(islandNode, this);
            }

            GameEvents.RaiseRoomEntered(islandNode);

            if (activeChallenge != null)
            {
                GameEvents.RaiseObjectiveProgressUpdated(activeChallenge.ObjectiveDescription, 0, 1);
            }
        }

        private void Update()
        {
            if (!hasEntered || isCleared || activeChallenge == null) return;

            activeChallenge.TickChallenge();

            if (activeChallenge.IsCompleted)
            {
                CompleteRoomEncounter();
            }
        }

        public void CompleteRoomEncounter()
        {
            if (isCleared) return;
            isCleared = true;

            float clearTime = Mathf.Max(0.1f, Time.time - roomStartTime);

            if (islandNode != null)
            {
                islandNode.SetLockdownBarriersActive(false);
                islandNode.SetExitGatesUnlocked(true);

                if (islandNode.VictoryChest != null)
                {
                    islandNode.VictoryChest.SetUnlocked(true);
                }
            }

            GameEvents.RaiseObjectiveProgressUpdated("ISLAND CLEARED - CHOOSE EXIT GATE", 1, 1);
            GameEvents.RaiseRoomCleared(islandNode, clearTime);
        }

        public void ResetRoomForCheckpoint()
        {
            if (isCleared) return;

            hasEntered = false;
            if (islandNode != null)
            {
                islandNode.SetLockdownBarriersActive(false);
                islandNode.SetExitGatesUnlocked(false);

                for (int i = 0; i < islandNode.SpawnedEnemies.Count; i++)
                {
                    if (islandNode.SpawnedEnemies[i] != null)
                        islandNode.SpawnedEnemies[i].ResetToSpawn();
                }

                for (int i = 0; i < islandNode.HealOrbs.Count; i++)
                {
                    if (islandNode.HealOrbs[i] != null)
                        islandNode.HealOrbs[i].ResetOrb();
                }
            }

            if (activeChallenge != null)
            {
                activeChallenge.ResetChallenge();
            }
        }
    }
}
