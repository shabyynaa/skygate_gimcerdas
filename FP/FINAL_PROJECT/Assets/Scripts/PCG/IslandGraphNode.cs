using System.Collections.Generic;
using UnityEngine;
using SkyGate.World;

namespace SkyGate.PCG
{
    public class IslandGraphNode
    {
        public int id;
        public int depth;
        public int branchIndex; // -1 = Left, 0 = Center, +1 = Right
        public IslandEncounterType encounterType;
        public Vector3 worldPosition;
        public Vector2 arenaSize;
        public readonly List<int> nextNodeIds = new List<int>();
        public IslandNode spawnedIsland;

        /// <summary>
        /// Verifies via Breadth-First Search (BFS) that a valid connected path exists
        /// from the start island (Depth 0) to the Boss Sanctum (Depth 4).
        /// </summary>
        public static bool ValidatePathExistsBFS(IReadOnlyList<IslandGraphNode> nodes, int startId, int targetBossId)
        {
            if (nodes == null || nodes.Count == 0) return false;

            Dictionary<int, IslandGraphNode> lookup = new Dictionary<int, IslandGraphNode>();
            for (int i = 0; i < nodes.Count; i++)
            {
                lookup[nodes[i].id] = nodes[i];
            }

            if (!lookup.ContainsKey(startId) || !lookup.ContainsKey(targetBossId))
                return false;

            Queue<int> queue = new Queue<int>();
            HashSet<int> visited = new HashSet<int>();

            queue.Enqueue(startId);
            visited.Add(startId);

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (current == targetBossId)
                    return true;

                if (lookup.TryGetValue(current, out IslandGraphNode node))
                {
                    for (int i = 0; i < node.nextNodeIds.Count; i++)
                    {
                        int nextId = node.nextNodeIds[i];
                        if (!visited.Contains(nextId))
                        {
                            visited.Add(nextId);
                            queue.Enqueue(nextId);
                        }
                    }
                }
            }

            return false;
        }
    }
}
