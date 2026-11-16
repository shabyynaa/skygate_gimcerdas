using UnityEngine;
using SkyGate.Core;

namespace SkyGate.Player
{
    public class ThirdPersonCamera : MonoBehaviour
    {
        [Header("Target & Framing (GDD 4.1)")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 lookOffset = new Vector3(0f, 2.35f, 1.8f);
        [SerializeField] private float defaultDistance = 9.8f;
        [SerializeField] private float tacticalDistance = 13.8f;
        [SerializeField] private float defaultPitch = 21f;
        [SerializeField] private float tacticalPitch = 34f;
        [SerializeField] private float minPitch = 10f;
        [SerializeField] private float maxPitch = 65f;
        [SerializeField] private float mouseSensitivity = 0.14f;
        [SerializeField] private float followSmoothTime = 0.06f;

        private float yaw;
        private float pitch;
        private bool isTacticalView;
        private Vector3 smoothVelocity;
        private Vector3 currentAnchorPos;

        private float shakeTimer;
        private float shakeAmplitude;

        public bool IsTacticalView => isTacticalView;

        private void Start()
        {
            if (target == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null) target = playerObj.transform;
            }

            yaw = target != null ? target.eulerAngles.y : 0f;
            pitch = defaultPitch;
            if (target != null)
                currentAnchorPos = target.position + lookOffset;

            if (GameInputReader.Instance != null)
            {
                GameInputReader.Instance.OnCameraTogglePressed += ToggleViewMode;
            }
            GameEvents.OnCameraShakeRequested += TriggerShake;
        }

        private void OnDestroy()
        {
            if (GameInputReader.HasInstance)
            {
                GameInputReader.PeekInstance.OnCameraTogglePressed -= ToggleViewMode;
            }
            GameEvents.OnCameraShakeRequested -= TriggerShake;
        }

        public void SetFollowTarget(Transform newTarget)
        {
            SetTarget(newTarget);
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            if (target != null)
            {
                currentAnchorPos = target.position + lookOffset;
                yaw = target.eulerAngles.y;
            }
        }

        private void ToggleViewMode()
        {
            isTacticalView = !isTacticalView;
            pitch = isTacticalView ? tacticalPitch : defaultPitch;
        }

        private void TriggerShake(float amplitude, float duration)
        {
            shakeAmplitude = Mathf.Max(shakeAmplitude, amplitude);
            shakeTimer = Mathf.Max(shakeTimer, duration);
        }

        private void LateUpdate()
        {
            if (target == null) return;

            if (GameInputReader.Instance != null && GameInputReader.Instance.IsGameplayEnabled)
            {
                Vector2 look = GameInputReader.Instance.LookInput;
                yaw += look.x * mouseSensitivity;
                pitch = Mathf.Clamp(pitch - look.y * mouseSensitivity, minPitch, maxPitch);
            }

            float activeDist = isTacticalView ? tacticalDistance : defaultDistance;
            Quaternion orbitRot = Quaternion.Euler(pitch, yaw, 0f);

            Vector3 desiredAnchor = target.position + lookOffset;
            currentAnchorPos = Vector3.SmoothDamp(currentAnchorPos, desiredAnchor, ref smoothVelocity, followSmoothTime);

            Vector3 desiredCamPos = currentAnchorPos - (orbitRot * Vector3.forward * activeDist);

            if (shakeTimer > 0f)
            {
                shakeTimer -= Time.unscaledDeltaTime;
                desiredCamPos += Random.insideUnitSphere * shakeAmplitude;
                if (shakeTimer <= 0f) shakeAmplitude = 0f;
            }

            transform.SetPositionAndRotation(desiredCamPos, orbitRot);
        }
    }
}
