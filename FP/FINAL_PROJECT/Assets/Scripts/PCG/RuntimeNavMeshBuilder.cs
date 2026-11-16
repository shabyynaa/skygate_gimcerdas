using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

namespace SkyGate.PCG
{
    [RequireComponent(typeof(NavMeshSurface))]
    public class RuntimeNavMeshBuilder : MonoBehaviour
    {
        private NavMeshSurface navMeshSurface;

        private void Awake()
        {
            EnsureSurfaceConfigured();
        }

        private void EnsureSurfaceConfigured()
        {
            if (navMeshSurface == null)
                navMeshSurface = GetComponent<NavMeshSurface>();

            if (navMeshSurface == null)
                navMeshSurface = gameObject.AddComponent<NavMeshSurface>();

            navMeshSurface.collectObjects = CollectObjects.All;
            navMeshSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

            int groundLayer = LayerMask.NameToLayer("Ground");
            int wallLayer = LayerMask.NameToLayer("Wall");
            int obstacleLayer = LayerMask.NameToLayer("Obstacle");

            int mask = 0;
            if (groundLayer >= 0) mask |= (1 << groundLayer);
            if (wallLayer >= 0) mask |= (1 << wallLayer);
            if (obstacleLayer >= 0) mask |= (1 << obstacleLayer);
            if (mask == 0) mask = ~0;

            navMeshSurface.layerMask = mask;
        }

        public void RebuildNavMesh()
        {
            EnsureSurfaceConfigured();
            Physics.SyncTransforms();
            navMeshSurface.BuildNavMesh();
        }
    }
}
