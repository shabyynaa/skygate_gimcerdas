using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkyGate.World
{
    /// <summary>
    /// Drives the continuous, lightweight 3D animated background for the Main Menu:
    /// slow 360-degree camera orbit with screen-left composition framing, swaying foliage,
    /// drifting 3D cloud ring, slowly orbiting floating islands, and smooth Play zoom transition.
    /// </summary>
    public class MainMenuShowcaseDiorama : MonoBehaviour
    {
        [Serializable]
        public struct CloudOrbitItem
        {
            public Transform target;
            public float orbitDegreesPerSec;
            public float bobAmplitude;
            public float bobFrequency;
            public float phaseOffset;
            [HideInInspector] public Vector3 initialLocalOffset;
            [HideInInspector] public float currentAngle;
        }

        [Header("Camera Orbit")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Vector3 orbitPivot = new Vector3(0f, 1.35f, 4.0f);
        [SerializeField] private float orbitRadius = 22.8f;
        [SerializeField] private float orbitHeight = 8.2f;
        [SerializeField] private float orbitDegreesPerSec = 4.2f;
        [SerializeField] private float lookRightOffset = 2.45f;
        [SerializeField] private float cameraBobAmplitude = 0.55f;
        [SerializeField] private float cameraBobFrequency = 0.38f;
        [SerializeField] private float startAngleDeg = -18f;

        [Header("Foliage & World Motion")]
        [SerializeField] private List<Transform> swayTargets = new List<Transform>();
        [SerializeField] private float swayAngleDegrees = 2.4f;
        [SerializeField] private float swayFrequency = 1.45f;

        [SerializeField] private List<Transform> orbitingIslands = new List<Transform>();
        [SerializeField] private float islandOrbitDegPerSec = 0.85f;
        [SerializeField] private float islandBobAmplitude = 0.55f;

        [SerializeField] private List<CloudOrbitItem> driftingClouds = new List<CloudOrbitItem>();

        [Header("Hero / Showcase Island Subtle Float")]
        [SerializeField] private Transform mainIslandGroup;
        [SerializeField] private float mainIslandBobAmplitude = 0.16f;
        [SerializeField] private float mainIslandBobFrequency = 0.55f;

        private Quaternion[] swayBaseRotations;
        private float[] swayPhases;
        private Vector3[] islandBaseOffsets;
        private float[] islandAngles;
        private Vector3 mainIslandBasePos;

        private float currentOrbitAngle;
        private float orbitSpeedMultiplier = 1f;
        private float transitionProgress;
        private bool isZoomTransitioning;
        private float currentCameraHeight;
        private float currentCameraRadius;

        private void Awake()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;

            currentOrbitAngle = startAngleDeg;
            currentCameraRadius = orbitRadius;
            currentCameraHeight = orbitHeight;

            if (mainIslandGroup != null)
                mainIslandBasePos = mainIslandGroup.position;

            int swayCount = swayTargets != null ? swayTargets.Count : 0;
            swayBaseRotations = new Quaternion[swayCount];
            swayPhases = new float[swayCount];
            for (int i = 0; i < swayCount; i++)
            {
                if (swayTargets[i] != null)
                {
                    swayBaseRotations[i] = swayTargets[i].localRotation;
                    swayPhases[i] = (swayTargets[i].position.x * 0.41f) + (swayTargets[i].position.z * 0.67f);
                }
            }

            int islandCount = orbitingIslands != null ? orbitingIslands.Count : 0;
            islandBaseOffsets = new Vector3[islandCount];
            islandAngles = new float[islandCount];
            for (int i = 0; i < islandCount; i++)
            {
                if (orbitingIslands[i] != null)
                {
                    islandBaseOffsets[i] = orbitingIslands[i].position - orbitPivot;
                }
            }

            if (driftingClouds != null)
            {
                for (int i = 0; i < driftingClouds.Count; i++)
                {
                    CloudOrbitItem item = driftingClouds[i];
                    if (item.target != null)
                    {
                        item.initialLocalOffset = item.target.position - orbitPivot;
                        driftingClouds[i] = item;
                    }
                }
            }
        }

        private void Start()
        {
            UpdateCameraTransform(0f);
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float t = Time.unscaledTime;

            if (mainIslandGroup != null && mainIslandBobAmplitude > 0.001f)
            {
                float dy = Mathf.Sin(t * mainIslandBobFrequency) * mainIslandBobAmplitude;
                mainIslandGroup.position = mainIslandBasePos + Vector3.up * dy;
            }

            UpdateFoliageSway(t);
            UpdateOrbitingClouds(dt, t);
            UpdateOrbitingIslands(dt, t);
            UpdateCameraTransform(dt);
        }

        private void UpdateFoliageSway(float t)
        {
            if (swayTargets == null) return;
            for (int i = 0; i < swayTargets.Count; i++)
            {
                Transform tr = swayTargets[i];
                if (tr == null) continue;
                float phase = swayPhases[i];
                float swayZ = Mathf.Sin(t * swayFrequency + phase) * swayAngleDegrees;
                float swayX = Mathf.Cos(t * (swayFrequency * 0.72f) + phase) * (swayAngleDegrees * 0.55f);
                tr.localRotation = swayBaseRotations[i] * Quaternion.Euler(swayX, 0f, swayZ);
            }
        }

        private void UpdateOrbitingClouds(float dt, float t)
        {
            if (driftingClouds == null) return;
            for (int i = 0; i < driftingClouds.Count; i++)
            {
                CloudOrbitItem item = driftingClouds[i];
                if (item.target == null) continue;
                item.currentAngle = (item.currentAngle + item.orbitDegreesPerSec * dt) % 360f;
                Quaternion yaw = Quaternion.Euler(0f, item.currentAngle, 0f);
                Vector3 pos = orbitPivot + (yaw * item.initialLocalOffset);
                pos.y += Mathf.Sin(t * item.bobFrequency + item.phaseOffset) * item.bobAmplitude;
                item.target.position = pos;
                driftingClouds[i] = item;
            }
        }

        private void UpdateOrbitingIslands(float dt, float t)
        {
            if (orbitingIslands == null) return;
            for (int i = 0; i < orbitingIslands.Count; i++)
            {
                Transform tr = orbitingIslands[i];
                if (tr == null) continue;
                islandAngles[i] = (islandAngles[i] + islandOrbitDegPerSec * dt * orbitSpeedMultiplier) % 360f;
                Quaternion yaw = Quaternion.Euler(0f, islandAngles[i], 0f);
                Vector3 pos = orbitPivot + (yaw * islandBaseOffsets[i]);
                pos.y += Mathf.Sin(t * 0.65f + i * 1.57f) * islandBobAmplitude;
                tr.position = pos;
            }
        }

        private void UpdateCameraTransform(float dt)
        {
            if (targetCamera == null) return;

            if (isZoomTransitioning)
            {
                transitionProgress = Mathf.Clamp01(transitionProgress + dt / 0.58f);
            }

            float zoomEase = transitionProgress * transitionProgress * (3f - 2f * transitionProgress);
            currentOrbitAngle = (currentOrbitAngle + orbitDegreesPerSec * orbitSpeedMultiplier * dt) % 360f;

            float rad = currentOrbitAngle * Mathf.Deg2Rad;
            Vector3 radialDir = new Vector3(Mathf.Sin(rad), 0f, -Mathf.Cos(rad));
            Vector3 camRight = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));

            float breathY = Mathf.Sin(Time.unscaledTime * cameraBobFrequency) * cameraBobAmplitude;
            currentCameraRadius = Mathf.Lerp(orbitRadius, 10.5f, zoomEase);
            currentCameraHeight = Mathf.Lerp(orbitHeight + breathY, 3.8f, zoomEase);

            Vector3 camPos = orbitPivot + radialDir * currentCameraRadius + Vector3.up * currentCameraHeight;
            Vector3 lookTarget = orbitPivot + camRight * (lookRightOffset * (1f - zoomEase));

            Vector3 forward = lookTarget - camPos;
            if (forward.sqrMagnitude > 0.001f)
            {
                targetCamera.transform.SetPositionAndRotation(camPos, Quaternion.LookRotation(forward, Vector3.up));
            }
        }

        public void SetOrbitSpeedMultiplier(float multiplier)
        {
            orbitSpeedMultiplier = Mathf.Clamp(multiplier, 0.05f, 2.0f);
        }

        public void BeginPlayTransition()
        {
            isZoomTransitioning = true;
            transitionProgress = 0f;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            Camera cam,
            Transform mainGroup,
            List<Transform> foliage,
            List<Transform> islands,
            List<CloudOrbitItem> clouds)
        {
            targetCamera = cam;
            mainIslandGroup = mainGroup;
            swayTargets = foliage ?? new List<Transform>();
            orbitingIslands = islands ?? new List<Transform>();
            driftingClouds = clouds ?? new List<CloudOrbitItem>();
        }
#endif
    }
}
