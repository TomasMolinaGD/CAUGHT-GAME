using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace CaughtGame.Navigation
{
    public sealed class NavigationNode : MonoBehaviour, ISerializationCallbackReceiver
    {
        private static readonly IReadOnlyList<NavigationNode> EmptyConnections = Array.AsReadOnly(Array.Empty<NavigationNode>());

        [SerializeField] private List<NavigationNode> connections = new List<NavigationNode>();
        [SerializeField] private NavigationZone zone = NavigationZone.Normal;
        [NonSerialized] private ReadOnlyCollection<NavigationNode> connectionsView;

        public Vector3 Position => transform.position;
        public NavigationZone Zone => zone;
        public IReadOnlyList<NavigationNode> Connections
        {
            get
            {
                if (connections == null)
                {
                    return EmptyConnections;
                }

                return connectionsView ?? (connectionsView = connections.AsReadOnly());
            }
        }

        /// <summary>
        /// Adds a reciprocal connection only when neither side already contains the link.
        /// Existing one-sided or duplicate serialized links are left untouched for validation.
        /// </summary>
        public bool TryConnectBidirectionally(NavigationNode other)
        {
            if (other == null || other == this || connections == null || other.connections == null)
            {
                return false;
            }

            if (connections.Contains(other) || other.connections.Contains(this))
            {
                return false;
            }

            connections.Add(other);
            other.connections.Add(this);
            return true;
        }

        internal int ConnectionCountTo(NavigationNode other)
        {
            if (connections == null)
            {
                return 0;
            }

            int count = 0;
            for (int index = 0; index < connections.Count; index++)
            {
                if (connections[index] == other)
                {
                    count++;
                }
            }

            return count;
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            connectionsView = null;
        }
    }
}
