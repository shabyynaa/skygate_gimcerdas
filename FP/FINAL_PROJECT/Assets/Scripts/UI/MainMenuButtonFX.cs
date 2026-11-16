using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SkyGate.UI
{
    /// <summary>
    /// Smooth hover, press, and playful wooden-signpost tilt/scale animations using unscaled time.
    /// Zero allocations per frame.
    /// </summary>
    public class MainMenuButtonFX : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Target Graphics")]
        [SerializeField] private Graphic tintTarget;
        [SerializeField] private Color normalTint = Color.white;
        [SerializeField] private Color hoverTint = new Color(1.0f, 0.96f, 0.80f, 1.0f);
        [SerializeField] private Color pressTint = new Color(0.84f, 0.78f, 0.66f, 1.0f);

        [Header("Motion")]
        [SerializeField] private float hoverScale = 1.08f;
        [SerializeField] private float pressScale = 0.94f;
        [SerializeField] private float hoverTiltBoostDeg = 2.5f;
        [SerializeField] private float idleSwayAmpDeg = 0.7f;
        [SerializeField] private float idleSwayFreq = 1.25f;
        [SerializeField] private float smoothSpeed = 14f;

        [Header("Optional Audio")]
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioClip hoverClip;
        [SerializeField] private AudioClip clickClip;

        private RectTransform rectTransform;
        private Vector3 baseScale = Vector3.one;
        private float baseAngleZ;
        private float phaseOffset;
        private bool isHovered;
        private bool isPressed;
        private float currentScaleFactor = 1f;
        private float currentTiltOffset;

        private bool customTintConfigured;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            baseScale = rectTransform != null ? rectTransform.localScale : Vector3.one;
            if (baseScale.sqrMagnitude < 0.001f) baseScale = Vector3.one;
            baseAngleZ = rectTransform != null ? rectTransform.localEulerAngles.z : 0f;
            if (baseAngleZ > 180f) baseAngleZ -= 360f;

            if (tintTarget == null)
                tintTarget = GetComponent<Image>();

            if (!customTintConfigured && tintTarget != null)
            {
                normalTint = tintTarget.color;
                hoverTint = Color.Lerp(normalTint, Color.white, 0.20f);
                pressTint = new Color(normalTint.r * 0.84f, normalTint.g * 0.84f, normalTint.b * 0.84f, normalTint.a);
            }

            phaseOffset = transform.GetSiblingIndex() * 0.95f;
        }

        private void OnDisable()
        {
            isHovered = false;
            isPressed = false;
            currentScaleFactor = 1f;
            currentTiltOffset = 0f;
            if (rectTransform != null)
            {
                rectTransform.localScale = baseScale;
                rectTransform.localRotation = Quaternion.Euler(0f, 0f, baseAngleZ);
            }
            if (tintTarget != null)
                tintTarget.color = normalTint;
        }

        private void Update()
        {
            if (rectTransform == null) return;

            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float t = 1f - Mathf.Exp(-smoothSpeed * dt);

            float targetScale = isPressed ? pressScale : (isHovered ? hoverScale : 1f);
            float idleSway = idleSwayAmpDeg > 0.001f ? Mathf.Sin(Time.unscaledTime * idleSwayFreq + phaseOffset) * idleSwayAmpDeg : 0f;
            float targetTilt = (isHovered ? hoverTiltBoostDeg : 0f) + idleSway;
            Color targetColor = isPressed ? pressTint : (isHovered ? hoverTint : normalTint);

            currentScaleFactor = Mathf.Lerp(currentScaleFactor, targetScale, t);
            currentTiltOffset = Mathf.Lerp(currentTiltOffset, targetTilt, t);

            rectTransform.localScale = baseScale * currentScaleFactor;
            rectTransform.localRotation = Quaternion.Euler(0f, 0f, baseAngleZ + currentTiltOffset);

            if (tintTarget != null)
                tintTarget.color = Color.Lerp(tintTarget.color, targetColor, t);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            if (sfxSource != null && hoverClip != null)
                sfxSource.PlayOneShot(hoverClip, 0.55f);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            isPressed = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            isPressed = true;
            if (sfxSource != null && clickClip != null)
                sfxSource.PlayOneShot(clickClip, 0.78f);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPressed = false;
        }

        public void ConfigureAudio(AudioSource source, AudioClip hover, AudioClip click)
        {
            sfxSource = source;
            hoverClip = hover;
            clickClip = click;
        }

        public void ConfigureMotion(float hoverScl, float hoverTiltDeg, float swayAmp, float swayHz)
        {
            hoverScale = hoverScl;
            hoverTiltBoostDeg = hoverTiltDeg;
            idleSwayAmpDeg = swayAmp;
            idleSwayFreq = swayHz;
        }

        public void ConfigureTintTarget(Graphic target, Color normal, Color hover, Color press)
        {
            tintTarget = target;
            normalTint = normal;
            hoverTint = hover;
            pressTint = press;
            customTintConfigured = true;
            if (tintTarget != null)
                tintTarget.color = normalTint;
        }
    }
}
