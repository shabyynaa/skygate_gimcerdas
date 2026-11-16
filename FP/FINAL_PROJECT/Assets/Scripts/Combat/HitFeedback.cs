using System.Collections;
using UnityEngine;
using SkyGate.Core;

namespace SkyGate.Combat
{
    [RequireComponent(typeof(HealthComponent))]
    public class HitFeedback : MonoBehaviour
    {
        private static readonly int PropBaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int PropColor = Shader.PropertyToID("_Color");
        private static readonly int PropEmissionColor = Shader.PropertyToID("_EmissionColor");

        [Header("Hit Flash Settings (GDD 4.3 & ASSET_CONTRACT 1.2)")]
        [SerializeField] private Color flashColor = new Color(1f, 0.92f, 0.92f, 1f);
        [SerializeField] private Color flashEmission = new Color(0.85f, 0.25f, 0.25f, 1f);
        [SerializeField] private float flashDuration = 0.10f;
        [SerializeField] private ParticleSystem hitSparkVFX;

        private HealthComponent health;
        private Renderer[] renderers;
        private Color[] originalColors;
        private MaterialPropertyBlock propBlock;
        private Coroutine flashRoutine;

        private static bool isInHitstop;

        private void Awake()
        {
            health = GetComponent<HealthComponent>();
            CacheRenderers();
            propBlock = new MaterialPropertyBlock();
        }

        public void CacheRenderers()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            originalColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                var mat = renderers[i] != null ? renderers[i].sharedMaterial : null;
                if (mat != null)
                {
                    if (mat.HasProperty(PropBaseColor))
                        originalColors[i] = mat.GetColor(PropBaseColor);
                    else if (mat.HasProperty(PropColor))
                        originalColors[i] = mat.GetColor(PropColor);
                    else
                        originalColors[i] = Color.white;
                }
                else
                {
                    originalColors[i] = Color.white;
                }
            }
        }

        private void OnEnable()
        {
            if (health != null)
                health.OnDamaged += HandleDamaged;

            if (health != null && health.IsPlayer)
                GameEvents.OnHitstopRequested += HandleHitstopRequested;
        }

        private void OnDisable()
        {
            if (health != null)
                health.OnDamaged -= HandleDamaged;

            if (health != null && health.IsPlayer)
                GameEvents.OnHitstopRequested -= HandleHitstopRequested;
        }

        private void HandleDamaged(DamageInfo info, float finalDamage, bool absorbedByShield)
        {
            if (hitSparkVFX != null)
            {
                hitSparkVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                hitSparkVFX.Play();
            }

            if (flashRoutine != null)
                StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(FlashRoutine(absorbedByShield));
        }

        private IEnumerator FlashRoutine(bool shieldFlash)
        {
            Color targetBase = shieldFlash ? new Color(0.35f, 0.85f, 1f, 1f) : flashColor;
            Color targetEmission = shieldFlash ? new Color(0.2f, 0.6f, 1f, 1f) : flashEmission;

            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || r is ParticleSystemRenderer) continue;
                r.GetPropertyBlock(propBlock);
                propBlock.SetColor(PropBaseColor, targetBase);
                propBlock.SetColor(PropColor, targetBase);
                propBlock.SetColor(PropEmissionColor, targetEmission);
                r.SetPropertyBlock(propBlock);
            }

            yield return new WaitForSecondsRealtime(flashDuration);

            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || r is ParticleSystemRenderer) continue;
                r.GetPropertyBlock(propBlock);
                propBlock.Clear();
                r.SetPropertyBlock(propBlock);
            }
            flashRoutine = null;
        }

        private void HandleHitstopRequested(float duration)
        {
            if (!isInHitstop && gameObject.activeInHierarchy)
            {
                StartCoroutine(HitstopRoutine(duration));
            }
        }

        private IEnumerator HitstopRoutine(float duration)
        {
            isInHitstop = true;
            float prevScale = Time.timeScale;
            if (prevScale > 0.05f)
            {
                Time.timeScale = 0.08f;
                yield return new WaitForSecondsRealtime(Mathf.Clamp(duration, 0.02f, 0.08f));
                if (Time.timeScale < 0.5f)
                    Time.timeScale = 1f;
            }
            isInHitstop = false;
        }
    }
}
