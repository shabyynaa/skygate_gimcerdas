using UnityEngine;
using SkyGate.Core;
using SkyGate.World;

namespace SkyGate.DDA
{
    public class ActiveRoomTelemetry
    {
        public int islandId;
        public int depth;
        public string islandType;
        public float startTime;
        public float hpRatioStart = 1f;
        public float damageTaken;
        public float damageDealt;
        public int dodgeSuccesses;
        public int stunHits;
        public int deathsInRoom;
        public string gateChosen = "None";

        public void Reset(IslandNode node, float currentHpRatio)
        {
            islandId = node != null ? node.IslandId : 0;
            depth = node != null ? node.Depth : 0;
            islandType = node != null ? node.EncounterType.ToString() : "Skirmish";
            startTime = Time.time;
            hpRatioStart = Mathf.Clamp01(currentHpRatio);
            damageTaken = 0f;
            damageDealt = 0f;
            dodgeSuccesses = 0;
            stunHits = 0;
            deathsInRoom = 0;
        }

        public RoomTelemetrySummary ToSummary(float clearTime, float currentHpRatio)
        {
            return new RoomTelemetrySummary
            {
                islandId = islandId,
                depth = depth,
                islandType = islandType,
                clearTimeSeconds = Mathf.Max(1f, clearTime),
                hpRatioStart = hpRatioStart,
                hpRatioEnd = Mathf.Clamp01(currentHpRatio),
                damageTaken = damageTaken,
                damageDealt = damageDealt,
                dodgeSuccesses = dodgeSuccesses,
                stunHits = stunHits,
                deathsInRoom = deathsInRoom,
                gateChosen = gateChosen
            };
        }
    }
}
