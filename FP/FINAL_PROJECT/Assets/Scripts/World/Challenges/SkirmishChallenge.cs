using UnityEngine;
using SkyGate.Core;

namespace SkyGate.World.Challenges
{
    public class SkirmishChallenge : MonoBehaviour, IRoomChallenge
    {
        private IslandNode island;
        private int totalEnemies;
        private int lastAliveCount = -1;
        private bool completed;

        public string ObjectiveDescription => "DEFEAT ALL SKY RAIDERS";
        public bool IsCompleted => completed;

        public void InitializeChallenge(IslandNode node, RoomController controller)
        {
            island = node;
            totalEnemies = island != null ? Mathf.Max(1, island.SpawnedEnemies.Count) : 1;
            lastAliveCount = -1;
            completed = false;
        }

        public void TickChallenge()
        {
            if (completed || island == null) return;

            totalEnemies = Mathf.Max(totalEnemies, island.SpawnedEnemies.Count);
            int alive = island.GetAliveEnemyCount();

            if (alive != lastAliveCount)
            {
                lastAliveCount = alive;
                int defeated = Mathf.Clamp(totalEnemies - alive, 0, totalEnemies);
                GameEvents.RaiseObjectiveProgressUpdated(
                    $"DEFEAT SKY RAIDERS ({defeated}/{totalEnemies})",
                    defeated,
                    totalEnemies
                );
            }

            if (alive == 0)
            {
                completed = true;
            }
        }

        public void ResetChallenge()
        {
            completed = false;
            lastAliveCount = -1;
        }
    }
}
