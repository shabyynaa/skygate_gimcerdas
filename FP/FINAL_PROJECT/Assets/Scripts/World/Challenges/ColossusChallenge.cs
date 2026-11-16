using UnityEngine;
using SkyGate.Core;

namespace SkyGate.World.Challenges
{
    public class ColossusChallenge : MonoBehaviour, IRoomChallenge
    {
        private IslandNode island;
        private bool completed;
        private bool armorShattered;
        private int lastActivatedAnchors = -1;
        private int lastAliveEnemies = -1;

        public string ObjectiveDescription => "ACTIVATE RUNE ANCHORS [E] OR BAit PILLAR STUN TO DEFEAT GOLEM";
        public bool IsCompleted => completed;

        public void InitializeChallenge(IslandNode node, RoomController controller)
        {
            island = node;
            completed = false;
            armorShattered = false;
            lastActivatedAnchors = -1;
            lastAliveEnemies = -1;
        }

        public void TickChallenge()
        {
            if (completed || island == null) return;

            int totalAnchors = island.RuneAnchors.Count;
            int activeAnchors = 0;
            for (int i = 0; i < totalAnchors; i++)
            {
                if (island.RuneAnchors[i] != null && island.RuneAnchors[i].IsActivated)
                    activeAnchors++;
            }

            if (!armorShattered && totalAnchors > 0 && activeAnchors >= totalAnchors)
            {
                armorShattered = true;
                for (int i = 0; i < island.SpawnedEnemies.Count; i++)
                {
                    if (island.SpawnedEnemies[i] != null)
                        island.SpawnedEnemies[i].SetArmorActive(false);
                }
                GameEvents.RaiseCameraShake(0.15f, 0.22f);
            }

            int totalEnemies = Mathf.Max(1, island.SpawnedEnemies.Count);
            int alive = island.GetAliveEnemyCount();
            int defeated = Mathf.Clamp(totalEnemies - alive, 0, totalEnemies);

            if (activeAnchors != lastActivatedAnchors || alive != lastAliveEnemies)
            {
                lastActivatedAnchors = activeAnchors;
                lastAliveEnemies = alive;
                string armorTag = armorShattered ? "ARMOR SHATTERED!" : $"RUNES ({activeAnchors}/{totalAnchors})";
                GameEvents.RaiseObjectiveProgressUpdated(
                    $"COLOSSUS ARENA: {armorTag} | GUARDIANS ({defeated}/{totalEnemies})",
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
            armorShattered = false;
            lastActivatedAnchors = -1;
            lastAliveEnemies = -1;

            if (island != null)
            {
                for (int i = 0; i < island.RuneAnchors.Count; i++)
                {
                    if (island.RuneAnchors[i] != null)
                        island.RuneAnchors[i].ResetAnchor();
                }
                for (int i = 0; i < island.SpawnedEnemies.Count; i++)
                {
                    if (island.SpawnedEnemies[i] != null)
                        island.SpawnedEnemies[i].SetArmorActive(true);
                }
            }
        }
    }
}
