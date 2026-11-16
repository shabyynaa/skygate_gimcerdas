using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SkyGate.Core;

namespace SkyGate.UI
{
    public class BossBarUI : MonoBehaviour
    {
        [SerializeField] private GameObject bossPanelRoot;
        [SerializeField] private TMP_Text bossTitleText;
        [SerializeField] private TMP_Text bossPhaseText;
        [SerializeField] private Image bossHpFillBar;

        private void Awake()
        {
            if (bossPanelRoot != null)
                bossPanelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            GameEvents.OnBossStateUpdated += HandleBossStateUpdated;
            GameEvents.OnRunStarted += HandleRunStarted;
        }

        private void OnDisable()
        {
            GameEvents.OnBossStateUpdated -= HandleBossStateUpdated;
            GameEvents.OnRunStarted -= HandleRunStarted;
        }

        private void HandleRunStarted(int seed, bool isAdaptive)
        {
            if (bossPanelRoot != null)
                bossPanelRoot.SetActive(false);
        }

        private void HandleBossStateUpdated(string bossName, float currentHP, float maxHP, int phase)
        {
            bool alive = currentHP > 0.1f && maxHP > 0.1f;
            if (bossPanelRoot != null)
                bossPanelRoot.SetActive(alive);

            if (!alive) return;

            if (bossTitleText != null)
                bossTitleText.text = $"{bossName} ({Mathf.CeilToInt(currentHP)}/{Mathf.RoundToInt(maxHP)})";

            if (bossPhaseText != null)
            {
                bossPhaseText.text = phase >= 2 ? "PHASE II: SOLAR OVERDRIVE" : "PHASE I: SKY SANCTUM WARDEN";
                bossPhaseText.color = phase >= 2
                    ? new Color(0.90f, 0.22f, 0.27f, 1f)
                    : new Color(0.98f, 0.78f, 0.22f, 1f);
            }

            if (bossHpFillBar != null)
            {
                float ratio = Mathf.Clamp01(currentHP / maxHP);
                bossHpFillBar.fillAmount = ratio;
                bossHpFillBar.transform.localScale = new Vector3(ratio, 1f, 1f);
            }
        }
    }
}
