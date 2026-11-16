using UnityEngine;

namespace SkyGate.Core
{
    [System.Serializable]
    public struct DamageInfo
    {
        public float amount;
        public Vector3 sourcePosition;
        public Vector3 knockbackDirection;
        public float knockbackForce;
        public bool isCritical;
        public bool bypassArmor;
        public GameObject instigator;
    }
}
