using UnityEngine;
using SkyGate.Core;

namespace SkyGate.World.Challenges
{
    public class BossChallenge : MonoBehaviour, IRoomChallenge
    {
        private IslandNode island;
        private bool completed;
        private int lastAliveCount = -1;

        public string ObjectiveDescription => "DEFEAT THE ZEPHYR GUARDIAN";
        public bool IsCompleted => completed;

        public void InitializeChallenge(IslandNode node, RoomController controller)
        {
            island = node;
            completed = false;
            lastAliveCount = -1;
        }

        public void TickChallenge()
        {
            if (completed || island == null) return;

            int alive = island.GetAliveEnemyCount();
            if (alive != lastAliveCount)
            {
                lastAliveCount = alive;
                GameEvents.RaiseObjectiveProgressUpdated(
                    alive > 0 ? "SKY SANCTUM: DEFEAT THE ZEPHYR GUARDIAN" : "CLAIM THE SKY SANCTUM RELIC [E]",
                    alive == 0 ? 1 : 0,
                    1
                );
            }

            if (alive == 0)
            {
                completed = true;
                if (island.VictoryChest == null)
                {
                    // If no chest placed in arena, trigger victory directly
                    GameEvents.RaiseRunEnded(true);
                }
            }
        }

        public void ResetChallenge()
        {
            completed = false;
            lastAliveCount = -1;
        }
    }
}
