using UnityEngine;
using SkyGate.World.Interactables;

namespace SkyGate.EnemyAI.Behaviors
{
    public class PhotophobiaBehavior : MonoBehaviour
    {
        private EnemyBrain brain;
        private float stalkAngleOffset;

        private void Awake()
        {
            brain = GetComponent<EnemyBrain>();
            stalkAngleOffset = Random.Range(0f, 360f);
        }

        public void Initialize(EnemyBrain owner)
        {
            brain = owner;
        }

        /// <summary>
        /// GDD Section 6.2: Dusk Wisp avoids lit SunBeacons (4.5m radius).
        /// Returns true if photophobia overrode movement this frame.
        /// </summary>
        public bool TryHandlePhotophobia(Transform target, float moveSpeed)
        {
            if (brain == null || target == null) return false;

            var beacons = SunBeacon.ActiveBeacons;
            if (beacons == null || beacons.Count == 0) return false;

            // 1. Check if Dusk Wisp itself is inside any lit SunBeacon radius
            for (int i = 0; i < beacons.Count; i++)
            {
                SunBeacon beacon = beacons[i];
                if (beacon == null || !beacon.IsLit) continue;

                Vector3 toWisp = transform.position - beacon.transform.position;
                toWisp.y = 0f;
                float distToWisp = toWisp.magnitude;

                if (distToWisp < beacon.SafeRadius + 0.6f)
                {
                    if (toWisp.sqrMagnitude < 0.01f) toWisp = transform.forward;
                    Vector3 fleePos = beacon.transform.position + toWisp.normalized * (beacon.SafeRadius + 1.8f);
                    brain.Motor.MoveTo(brain.Motor.ClampPositionToRoom(fleePos), moveSpeed * 1.15f);
                    return true;
                }
            }

            // 2. Check if the Player is standing inside a lit SunBeacon safe zone
            for (int i = 0; i < beacons.Count; i++)
            {
                SunBeacon beacon = beacons[i];
                if (beacon == null || !beacon.IsLit) continue;

                Vector3 toPlayer = target.position - beacon.transform.position;
                toPlayer.y = 0f;
                if (toPlayer.magnitude <= beacon.SafeRadius)
                {
                    // Stalk around the perimeter outside the light zone
                    stalkAngleOffset += Time.deltaTime * 35f;
                    float rad = stalkAngleOffset * Mathf.Deg2Rad;
                    float orbitRadius = beacon.SafeRadius + 1.4f;
                    Vector3 orbitPos = beacon.transform.position + new Vector3(Mathf.Cos(rad) * orbitRadius, 0f, Mathf.Sin(rad) * orbitRadius);
                    brain.Motor.MoveTo(brain.Motor.ClampPositionToRoom(orbitPos), moveSpeed * 0.85f);
                    brain.Motor.FacePosition(target.position, 6f);
                    return true;
                }
            }

            return false;
        }

        public float GetEffectiveLifeSteal()
        {
            if (brain == null || brain.Archetype == null) return 0f;

            // Life-steal is disabled when all beacons in the room are lit (GDD Section 6.2)
            var beacons = SunBeacon.ActiveBeacons;
            if (beacons != null && beacons.Count > 0)
            {
                bool allLit = true;
                for (int i = 0; i < beacons.Count; i++)
                {
                    if (beacons[i] != null && !beacons[i].IsLit)
                    {
                        allLit = false;
                        break;
                    }
                }
                if (allLit) return 0f;
            }

            return brain.Archetype.lifeStealAmount;
        }
    }
}
