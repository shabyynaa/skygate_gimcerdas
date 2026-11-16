using UnityEngine;

namespace SkyGate.World
{
    public class UVWaterfallScroller : MonoBehaviour
    {
        [SerializeField] private Vector2 scrollSpeed = new Vector2(0f, -0.85f);
        private Renderer rend;
        private MaterialPropertyBlock propBlock;
        private Vector2 currentOffset;
        private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");

        private void Awake()
        {
            rend = GetComponent<Renderer>();
            propBlock = new MaterialPropertyBlock();
        }

        private void Update()
        {
            if (rend == null) rend = GetComponent<Renderer>();
            if (rend == null) return;
            if (propBlock == null) propBlock = new MaterialPropertyBlock();
            currentOffset += scrollSpeed * Time.deltaTime;
            currentOffset.x %= 1f;
            currentOffset.y %= 1f;

            rend.GetPropertyBlock(propBlock);
            propBlock.SetVector(BaseMapStId, new Vector4(1f, 1f, currentOffset.x, currentOffset.y));
            rend.SetPropertyBlock(propBlock);
        }
    }
}
