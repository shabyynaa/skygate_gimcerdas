using System.Collections;
using UnityEngine;
using TMPro;
using SkyGate.Core;

namespace SkyGate.Combat
{
    public class FloatingDamageSpawner : MonoBehaviour
    {
        [Header("Floating Text Prefab (TMP_Text)")]
        [SerializeField] private TMP_Text floatingTextPrefab;

        [Header("Colors (GDD 4.3)")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color criticalOrStunColor = new Color(1f, 0.85f, 0.15f, 1f);
        [SerializeField] private Color shieldAbsorbColor = new Color(0.28f, 0.82f, 1f, 1f);
        [SerializeField] private float floatDuration = 0.75f;
        [SerializeField] private float floatHeight = 1.15f;

        private Transform mainCamTransform;

        private void OnEnable()
        {
            GameEvents.OnFloatingDamageRequested += SpawnFloatingDamage;
        }

        private void OnDisable()
        {
            GameEvents.OnFloatingDamageRequested -= SpawnFloatingDamage;
        }

        private void SpawnFloatingDamage(Vector3 worldPos, float amount, bool isCrit, bool isShield)
        {
            if (floatingTextPrefab == null) return;

            if (mainCamTransform == null && Camera.main != null)
                mainCamTransform = Camera.main.transform;

            Vector3 jitter = new Vector3(Random.Range(-0.25f, 0.25f), Random.Range(0f, 0.25f), Random.Range(-0.25f, 0.25f));
            TMP_Text instance = Instantiate(floatingTextPrefab, worldPos + jitter, Quaternion.identity, transform);
            instance.gameObject.SetActive(true);

            string label = Mathf.RoundToInt(amount).ToString();
            if (isCrit) label += "!";
            if (isShield) label = $"[{label}]";

            instance.text = label;
            instance.color = isShield ? shieldAbsorbColor : (isCrit ? criticalOrStunColor : normalColor);
            instance.transform.localScale = Vector3.one * (isCrit ? 1.25f : 1.0f);

            StartCoroutine(AnimatePopupRoutine(instance));
        }

        private IEnumerator AnimatePopupRoutine(TMP_Text popup)
        {
            if (popup == null) yield break;

            Vector3 startPos = popup.transform.position;
            Vector3 endPos = startPos + Vector3.up * floatHeight;
            Color baseCol = popup.color;
            float elapsed = 0f;

            while (elapsed < floatDuration && popup != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / floatDuration);

                popup.transform.position = Vector3.Lerp(startPos, endPos, 1f - Mathf.Pow(1f - t, 2f));
                if (mainCamTransform != null)
                {
                    popup.transform.rotation = mainCamTransform.rotation;
                }

                float alpha = t < 0.6f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.6f) / 0.4f);
                popup.color = new Color(baseCol.r, baseCol.g, baseCol.b, alpha);
                yield return null;
            }

            if (popup != null)
                Destroy(popup.gameObject);
        }
    }
}
