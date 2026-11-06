using UnityEngine;

namespace CaughtGame.Navigation
{
    /// <summary>
    /// A temporary, manually controlled capsule that requests one path at a time.
    /// Transform movement and trigger colliders do not validate physical collision avoidance.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PathFollower))]
    public sealed class NavigationGridTestAgent : MonoBehaviour
    {
        [SerializeField] private NavigationGrid grid;
        [SerializeField] private NavigationAgentType agentType = NavigationAgentType.Hunter;
        [SerializeField, Min(0)] private int destinationRow;
        [SerializeField, Min(0)] private int destinationColumn;
        [SerializeField, Min(0.1f)] private float movementSpeed = 3f;
        [Tooltip("Prototype sizing only; the test-scene builder uses a trigger collider and does not validate wall collisions.")]
        [SerializeField, Min(0.1f)] private float referenceRadius = 0.5f;

        private PathFollower pathFollower;

        public NavigationGrid Grid => grid;
        public NavigationAgentType AgentType => agentType;
        public int DestinationRow => destinationRow;
        public int DestinationColumn => destinationColumn;
        public float MovementSpeed => movementSpeed;
        public float ReferenceRadius => referenceRadius;
        public NavigationPathResult LastPathResult { get; private set; }

        public PathFollower Follower
        {
            get
            {
                if (pathFollower == null)
                {
                    pathFollower = GetComponent<PathFollower>();
                }

                return pathFollower;
            }
        }

        private void Awake()
        {
            GetPathFollower();
        }

        public void Configure(NavigationGrid sourceGrid, NavigationAgentType type,
            int targetRow, int targetColumn, float speed, float radius)
        {
            grid = sourceGrid;
            agentType = type;
            destinationRow = Mathf.Max(0, targetRow);
            destinationColumn = Mathf.Max(0, targetColumn);
            movementSpeed = Mathf.Max(0.1f, speed);
            referenceRadius = Mathf.Max(0.1f, radius);
            GetPathFollower().Speed = movementSpeed;

            CapsuleCollider capsule = GetComponent<CapsuleCollider>();
            if (capsule != null)
            {
                capsule.direction = 1;
                capsule.radius = referenceRadius;
                capsule.height = Mathf.Max(capsule.height, referenceRadius * 2f);
                capsule.isTrigger = true;
            }
        }

        public NavigationPathResult RequestPathToConfiguredCell()
        {
            return RequestPathToCell(destinationRow, destinationColumn);
        }

        public NavigationPathResult RequestPathToCell(int row, int column)
        {
            if (grid == null)
            {
                GetPathFollower().Stop();
                LastPathResult = null;
                return null;
            }

            grid.TryWorldToGrid(transform.position, out int startRow, out int startColumn);
            LastPathResult = NavigationGridPathfinder.FindPath(grid, startRow, startColumn,
                row, column, agentType);
            if (!GetPathFollower().FollowPath(LastPathResult))
            {
                GetPathFollower().Stop();
            }

            return LastPathResult;
        }

        private PathFollower GetPathFollower()
        {
            if (pathFollower == null)
            {
                pathFollower = GetComponent<PathFollower>();
            }

            if (pathFollower == null)
            {
                pathFollower = gameObject.AddComponent<PathFollower>();
            }

            pathFollower.Speed = movementSpeed;
            return pathFollower;
        }

        private void OnValidate()
        {
            movementSpeed = Mathf.Max(0.1f, movementSpeed);
            referenceRadius = Mathf.Max(0.1f, referenceRadius);
        }
    }
}
