using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace CaughtGame.Navigation
{
    public sealed class NavigationGraph : MonoBehaviour, ISerializationCallbackReceiver
    {
        private static readonly IReadOnlyList<NavigationNode> EmptyNodes = Array.AsReadOnly(Array.Empty<NavigationNode>());

        [SerializeField] private List<NavigationNode> nodes = new List<NavigationNode>();
        [SerializeField, Min(0.05f)] private float gizmoNodeRadius = 0.5f;
        [NonSerialized] private ReadOnlyCollection<NavigationNode> nodesView;

        public IReadOnlyList<NavigationNode> Nodes
        {
            get
            {
                if (nodes == null)
                {
                    return EmptyNodes;
                }

                return nodesView ?? (nodesView = nodes.AsReadOnly());
            }
        }

        /// <summary>
        /// Returns only structurally valid, reciprocal connections allowed for the agent.
        /// </summary>
        public IReadOnlyList<NavigationNode> GetNeighbors(NavigationNode node, NavigationAgentType agentType)
        {
            List<NavigationNode> neighbors = new List<NavigationNode>();
            if (!IsRegisteredExactlyOnce(node) || !CanTraverse(node, agentType) || node.Connections == null)
            {
                return neighbors;
            }

            for (int index = 0; index < node.Connections.Count; index++)
            {
                NavigationNode neighbor = node.Connections[index];
                if (IsValidReciprocalConnection(node, neighbor) && CanTraverse(neighbor, agentType))
                {
                    neighbors.Add(neighbor);
                }
            }

            return neighbors;
        }

        /// <summary>
        /// Calculates the Euclidean cost for a valid connection without storing a cached value.
        /// </summary>
        public bool TryGetConnectionCost(
            NavigationNode from,
            NavigationNode to,
            NavigationAgentType agentType,
            out float cost)
        {
            cost = 0f;
            if (!IsRegisteredExactlyOnce(from)
                || !IsRegisteredExactlyOnce(to)
                || !CanTraverse(from, agentType)
                || !CanTraverse(to, agentType)
                || !IsValidReciprocalConnection(from, to))
            {
                return false;
            }

            cost = Vector3.Distance(from.Position, to.Position);
            return true;
        }

        /// <summary>
        /// Reports invalid serialized graph data without changing it.
        /// </summary>
        public IReadOnlyList<string> Validate()
        {
            List<string> issues = new List<string>();
            if (nodes == null)
            {
                issues.Add("The graph node collection is null.");
                return issues;
            }

            for (int nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
            {
                NavigationNode node = nodes[nodeIndex];
                if (node == null)
                {
                    issues.Add("The graph contains a null node reference.");
                    continue;
                }

                if (CountNodes(node) > 1)
                {
                    issues.Add($"Node '{node.name}' appears more than once in the graph.");
                }

                IReadOnlyList<NavigationNode> connections = node.Connections;
                if (connections == null)
                {
                    issues.Add($"Node '{node.name}' has a null connection collection.");
                    continue;
                }

                for (int connectionIndex = 0; connectionIndex < connections.Count; connectionIndex++)
                {
                    NavigationNode neighbor = connections[connectionIndex];
                    if (neighbor == null)
                    {
                        issues.Add($"Node '{node.name}' has a null connection.");
                        continue;
                    }

                    if (neighbor == node)
                    {
                        issues.Add($"Node '{node.name}' connects to itself.");
                        continue;
                    }

                    if (node.ConnectionCountTo(neighbor) > 1)
                    {
                        issues.Add($"Node '{node.name}' has a duplicate connection to '{neighbor.name}'.");
                    }

                    if (!IsRegisteredExactlyOnce(neighbor))
                    {
                        issues.Add($"Node '{node.name}' connects to a node that is not registered exactly once.");
                    }

                    if (neighbor.ConnectionCountTo(node) != 1)
                    {
                        issues.Add($"Connection from '{node.name}' to '{neighbor.name}' is asymmetric.");
                    }
                }
            }

            return issues;
        }

        private bool IsValidReciprocalConnection(NavigationNode from, NavigationNode to)
        {
            return to != null
                && to != from
                && IsRegisteredExactlyOnce(to)
                && from.ConnectionCountTo(to) == 1
                && to.ConnectionCountTo(from) == 1;
        }

        private bool IsRegisteredExactlyOnce(NavigationNode node)
        {
            return node != null && CountNodes(node) == 1;
        }

        private int CountNodes(NavigationNode node)
        {
            if (nodes == null || node == null)
            {
                return 0;
            }

            int count = 0;
            for (int index = 0; index < nodes.Count; index++)
            {
                if (nodes[index] == node)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool CanTraverse(NavigationNode node, NavigationAgentType agentType)
        {
            if (node == null)
            {
                return false;
            }

            switch (agentType)
            {
                case NavigationAgentType.Hunter:
                    return true;
                case NavigationAgentType.Wolf:
                    return node.Zone != NavigationZone.SafeZone;
                default:
                    return false;
            }
        }

        private void OnDrawGizmos()
        {
            if (nodes == null)
            {
                return;
            }

            HashSet<long> drawnEdges = new HashSet<long>();
            for (int index = 0; index < nodes.Count; index++)
            {
                NavigationNode node = nodes[index];
                if (node == null)
                {
                    continue;
                }

                Gizmos.color = GetZoneColor(node.Zone);
                Gizmos.DrawSphere(node.Position, gizmoNodeRadius);

                IReadOnlyList<NavigationNode> connections = node.Connections;
                if (connections == null)
                {
                    continue;
                }

                for (int connectionIndex = 0; connectionIndex < connections.Count; connectionIndex++)
                {
                    NavigationNode neighbor = connections[connectionIndex];
                    if (neighbor == null)
                    {
                        continue;
                    }

                    long edgeKey = GetUndirectedEdgeKey(node.GetInstanceID(), neighbor.GetInstanceID());
                    if (!drawnEdges.Add(edgeKey))
                    {
                        continue;
                    }

                    Gizmos.color = IsValidReciprocalConnection(node, neighbor) ? Color.white : Color.red;
                    Gizmos.DrawLine(node.Position, neighbor.Position);
                }
            }
        }

        private static long GetUndirectedEdgeKey(int firstId, int secondId)
        {
            int lower = firstId < secondId ? firstId : secondId;
            int upper = firstId < secondId ? secondId : firstId;
            return ((long)lower << 32) ^ (uint)upper;
        }

        private static Color GetZoneColor(NavigationZone zone)
        {
            switch (zone)
            {
                case NavigationZone.HuntingGround:
                    return new Color(1f, 0.55f, 0.1f);
                case NavigationZone.SafeZone:
                    return Color.cyan;
                case NavigationZone.Spawn:
                    return Color.green;
                case NavigationZone.Exit:
                    return Color.magenta;
                default:
                    return Color.white;
            }
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            nodesView = null;
        }
    }
}
