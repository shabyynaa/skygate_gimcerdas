using UnityEngine;
using SkyGate.Core;

namespace SkyGate.World.Challenges
{
    public class SunBeaconChallenge : MonoBehaviour, IRoomChallenge
    {
        private IslandNode island;
        private bool completed;
        private int lastLitCount = -1;
        private int lastAliveCount = -1;

        public string ObjectiveDescription => "IGNITE SUN BEACONS [E] & CLEAR DUSK WISPS";
        public bool IsCompleted => completed;

        public void InitializeChallenge(IslandNode node, RoomController controller)
        {
            island = node;
            completed = false;
            lastLitCount = -1;
            lastAliveCount = -1;
        }

        public void TickChallenge()
        {
            if (completed || island == null) return;

            int totalBeacons = island.SunBeacons.Count;
            int litBeacons = 0;
            for (int i = 0; i < totalBeacons; i++)
            {
                if (island.SunBeacons[i] != null && island.SunBeacons[i].IsLit)
                    litBeacons++;
            }

            int totalEnemies = Mathf.Max(1, island.SpawnedEnemies.Count);
            int aliveEnemies = island.GetAliveEnemyCount();
            int defeated = Mathf.Clamp(totalEnemies - aliveEnemies, 0, totalEnemies);

            if (litBeacons != lastLitCount || aliveEnemies != lastAliveCount)
            {
                lastLitCount = litBeacons;
                lastAliveCount = aliveEnemies;
                GameEvents.RaiseObjectiveProgressUpdated(
                    $"BEACONS LIT ({litBeacons}/{totalBeacons}) | ENEMIES ({defeated}/{totalEnemies})",
                    litBeacons + defeated,
                    totalBeacons + totalEnemies
                );
            }

            if (litBeacons >= totalBeacons && aliveEnemies == 0)
            {
                completed = true;
            }
        }

        public void ResetChallenge()
        {
            completed = false;
            lastLitCount = -1;
            lastAliveCount = -1;
            if (island != null)
            {
                for (int i = 0; i < island.SunBeacons.Count; i++)
                {
                    if (island.SunBeacons[i] != null)
                        island.SunBeacons[i].ResetBeacon();
                }
            }
        }
    }
}
