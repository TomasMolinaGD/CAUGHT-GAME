using System;
using UnityEngine;

namespace CaughtGame.Navigation
{
    [Serializable]
    public struct NavigationGridCell
    {
        [SerializeField] private int row;
        [SerializeField] private int column;
        [SerializeField] private bool walkable;
        [SerializeField] private NavigationGridZone zone;
        [SerializeField] private string zoneId;

        public int Row => row;
        public int Column => column;
        public bool Walkable => walkable;
        public NavigationGridZone Zone => zone;
        public string ZoneId => zoneId ?? string.Empty;

        public NavigationGridCell(int row, int column, bool walkable = true,
            NavigationGridZone zone = NavigationGridZone.Normal, string zoneId = null)
        {
            this.row = row;
            this.column = column;
            this.walkable = walkable;
            this.zone = zone;
            this.zoneId = zoneId ?? string.Empty;
        }

        internal NavigationGridCell WithWalkable(bool value)
        {
            walkable = value;
            return this;
        }

        internal NavigationGridCell WithZone(NavigationGridZone value, string id)
        {
            zone = value;
            zoneId = id ?? string.Empty;
            return this;
        }
    }
}
