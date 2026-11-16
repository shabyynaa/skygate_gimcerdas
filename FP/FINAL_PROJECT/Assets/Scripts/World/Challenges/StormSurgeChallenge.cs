using UnityEngine;
using SkyGate.Core;

namespace SkyGate.World.Challenges
{
    public class StormSurgeChallenge : MonoBehaviour, IRoomChallenge
    {
        private IslandNode island;
        private RoomController controller;
        private bool completed;
        private bool activatedCrystals;
        private int lastDestroyedCrystals = -1;
        private int lastAliveEnemies = -1;

        public string ObjectiveDescription => "SHATTER STORM CRYSTALS & DEFEAT DEFENDERS";
        public bool IsCompleted => completed;

        public void InitializeChallenge(IslandNode node, RoomController roomController)
        {
            island = node;
            controller = roomController;
            completed = false;
            activatedCrystals = false;
            lastDestroyedCrystals = -1;
            lastAliveEnemies = -1;
        }

        public void TickChallenge()
        {
            if (completed || island == null) return;

            if (!activatedCrystals)
            {
                activatedCrystals = true;
                Transform player = controller != null ? controller.CachedPlayer : null;
                for (int i = 0; i < island.StormCrystals.Count; i++)
                {
                    if (island.StormCrystals[i] != null)
                        island.StormCrystals[i].SetRoomActive(true, player);
                }
            }

            int totalCrystals = island.StormCrystals.Count;
            int destroyedCrystals = 0;
            for (int i = 0; i < totalCrystals; i++)
            {
                if (island.StormCrystals[i] == null || island.StormCrystals[i].IsDestroyed)
                    destroyedCrystals++;
            }

            int totalEnemies = Mathf.Max(1, island.SpawnedEnemies.Count);
            int aliveEnemies = island.GetAliveEnemyCount();
            int defeated = Mathf.Clamp(totalEnemies - aliveEnemies, 0, totalEnemies);

            if (destroyedCrystals != lastDestroyedCrystals || aliveEnemies != lastAliveEnemies)
            {
                lastDestroyedCrystals = destroyedCrystals;
                lastAliveEnemies = aliveEnemies;
                GameEvents.RaiseObjectiveProgressUpdated(
                    $"STORM CRYSTALS ({destroyedCrystals}/{totalCrystals}) | DEFENDERS ({defeated}/{totalEnemies})",
                    destroyedCrystals + defeated,
                    totalCrystals + totalEnemies
                );
            }

            if (destroyedCrystals >= totalCrystals && aliveEnemies == 0)
            {
                completed = true;
                for (int i = 0; i < totalCrystals; i++)
                {
                    if (island.StormCrystals[i] != null)
                        island.StormCrystals[i].SetRoomActive(false, null);
                }
            }
        }

        public void ResetChallenge()
        {
            completed = false;
            activatedCrystals = false;
            lastDestroyedCrystals = -1;
            lastAliveEnemies = -1;

            if (island != null)
            {
                for (int i = 0; i < island.StormCrystals.Count; i++)
                {
                    if (island.StormCrystals[i] != null)
                    {
                        island.StormCrystals[i].SetRoomActive(false, null);
                        island.StormCrystals[i].ResetCrystal();
                    }
                }
            }
        }
    }
}
