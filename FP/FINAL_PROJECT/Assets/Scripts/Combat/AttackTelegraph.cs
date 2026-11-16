using UnityEngine;

namespace SkyGate.Combat
{
    public class AttackTelegraph : MonoBehaviour
    {
        [Header("Telegraph Visuals (GDD 4.3 & 7.2)")]
        [SerializeField] private Transform lineTransform;
        [SerializeField] private Transform circleTransform;

        private float hideAtTime;
        private bool isShowing;

        private void Awake()
        {
            HideImmediate();
        }

        public void ShowLine(Vector3 origin, Vector3 direction, float length, float width, float duration)
        {
            HideImmediate();
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f) direction = transform.forward;
            direction.Normalize();

            if (lineTransform != null)
            {
                lineTransform.gameObject.SetActive(true);
                lineTransform.position = origin + Vector3.up * 0.06f + direction * (length * 0.5f);
                lineTransform.rotation = Quaternion.LookRotation(direction, Vector3.up);
                lineTransform.localScale = new Vector3(width, 0.04f, length);
            }

            hideAtTime = Time.time + duration;
            isShowing = true;
        }

        public void ShowCircle(Vector3 center, float radius, float duration)
        {
            HideImmediate();
            if (circleTransform != null)
            {
                circleTransform.gameObject.SetActive(true);
                circleTransform.position = new Vector3(center.x, transform.position.y + 0.06f, center.z);
                circleTransform.localScale = new Vector3(radius * 2f, 0.04f, radius * 2f);
            }

            hideAtTime = Time.time + duration;
            isShowing = true;
        }

        public void HideImmediate()
        {
            isShowing = false;
            if (lineTransform != null) lineTransform.gameObject.SetActive(false);
            if (circleTransform != null) circleTransform.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (isShowing && Time.time >= hideAtTime)
            {
                HideImmediate();
            }
        }
    }
}
