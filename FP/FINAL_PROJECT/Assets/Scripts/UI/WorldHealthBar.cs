using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SkyGate.Core;
using SkyGate.EnemyAI;

namespace SkyGate.UI
{
    public class WorldHealthBar : MonoBehaviour
    {
        [SerializeField] private HealthComponent targetHealth;
        [SerializeField] private EnemyBrain targetBrain;
        [SerializeField] private Image fillImage;
        [SerializeField] private TMP_Text stateBadgeText;

        private Transform mainCamTransform;

        private void Awake()
        {
            if (targetHealth == null)
                targetHealth = GetComponentInParent<HealthComponent>();
            if (targetBrain == null)
                targetBrain = GetComponentInParent<EnemyBrain>();
            if (Camera.main != null)
                mainCamTransform = Camera.main.transform;
        }

        private void OnEnable()
        {
            if (targetHealth != null)
                targetHealth.OnHealthChanged += HandleHealthChanged;
        }

        private void OnDisable()
        {
            if (targetHealth != null)
                targetHealth.OnHealthChanged -= HandleHealthChanged;
        }

        private void LateUpdate()
        {
            if (mainCamTransform == null && Camera.main != null)
                mainCamTransform = Camera.main.transform;

            if (mainCamTransform != null)
            {
                transform.rotation = mainCamTransform.rotation;
            }

            if (fillImage != null && targetHealth != null)
            {
                float ratio = targetHealth.IsAlive ? targetHealth.HPRatio : 0f;
                fillImage.fillAmount = ratio;
                fillImage.transform.localScale = new Vector3(ratio, 1f, 1f);
            }

            if (stateBadgeText != null && targetBrain != null)
            {
                if (targetBrain.CurrentState == EnemyState.Stunned)
                {
                    stateBadgeText.text = "STUNNED (1.5x DMG)";
                    stateBadgeText.color = new Color(0.98f, 0.82f, 0.20f, 1f);
                }
                else
                {
                    stateBadgeText.text = targetBrain.CurrentState.ToString().ToUpper();
                    stateBadgeText.color = Color.white;
                }
            }
        }

        private void HandleHealthChanged(float currentHP, float maxHP, float shield)
        {
            if (fillImage != null && maxHP > 0f)
            {
                fillImage.fillAmount = Mathf.Clamp01(currentHP / maxHP);
            }
        }
    }
}
