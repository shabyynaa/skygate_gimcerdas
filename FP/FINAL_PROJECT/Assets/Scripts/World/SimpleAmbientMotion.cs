using UnityEngine;

namespace SkyGate.World
{
    /// <summary>
    /// Applies lightweight visual rotation and vertical bobbing to decorative props,
    /// floating sanctuary crystals, and backdrop windmill sails without affecting physics colliders.
    /// </summary>
    public class SimpleAmbientMotion : MonoBehaviour
    {
        [SerializeField] private Vector3 rotateAnglesPerSecond = new Vector3(0f, 45f, 0f);
        [SerializeField] private float bobAmplitude = 0f;
        [SerializeField] private float bobFrequency = 2.0f;

        private Vector3 startLocalPos;
        private float phaseOffset;

        private void Awake()
        {
            startLocalPos = transform.localPosition;
            phaseOffset = (transform.position.x + transform.position.z) * 0.37f;
        }

        private void Update()
        {
            if (rotateAnglesPerSecond.sqrMagnitude > 0.001f)
            {
                transform.Rotate(rotateAnglesPerSecond * Time.deltaTime, Space.Self);
            }

            if (bobAmplitude > 0.001f)
            {
                float yOffset = Mathf.Sin((Time.time + phaseOffset) * bobFrequency) * bobAmplitude;
                transform.localPosition = startLocalPos + Vector3.up * yOffset;
            }
        }

        public void Configure(Vector3 rotationPerSec, float amplitude = 0f, float frequency = 2.0f)
        {
            rotateAnglesPerSecond = rotationPerSec;
            bobAmplitude = amplitude;
            bobFrequency = frequency;
        }
    }
}
