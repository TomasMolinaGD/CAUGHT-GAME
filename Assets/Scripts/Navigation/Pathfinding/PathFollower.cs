using System.Collections.Generic;
using UnityEngine;

namespace CaughtGame.Navigation
{
    /// <summary>
    /// Moves this Transform over an already calculated path on the XZ plane.
    /// This component does not perform pathfinding or collision avoidance.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PathFollower : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float speed = 3f;
        [SerializeField, Min(0f)] private float arrivalTolerance = 0.1f;
        [SerializeField, Min(0f)] private float rotationSpeed = 360f;

        private IReadOnlyList<Vector3> waypoints;
        private int currentWaypointIndex;
        private bool isMoving;

        public float Speed
        {
            get => speed;
            set => speed = Mathf.Max(0.1f, value);
        }

        public float ArrivalTolerance
        {
            get => arrivalTolerance;
            set => arrivalTolerance = Mathf.Max(0f, value);
        }

        public float RotationSpeed
        {
            get => rotationSpeed;
            set => rotationSpeed = Mathf.Max(0f, value);
        }

        public bool HasPath => waypoints != null && waypoints.Count > 0;
        public bool IsMoving => isMoving;
        public bool IsComplete { get; private set; }
        public int CurrentWaypointIndex => currentWaypointIndex;

        public Vector3 CurrentDestination
        {
            get
            {
                if (!HasPath)
                {
                    return transform.position;
                }

                int index = Mathf.Min(currentWaypointIndex, waypoints.Count - 1);
                Vector3 destination = waypoints[index];
                destination.y = transform.position.y;
                return destination;
            }
        }

        private void Update()
        {
            Advance(Time.deltaTime);
        }

        /// <summary>
        /// Starts following a successful path without recalculating it.
        /// </summary>
        public bool FollowPath(NavigationPathResult path)
        {
            Stop();
            if (path == null || path.Status != NavigationPathStatus.Success || path.Waypoints.Count == 0)
            {
                return false;
            }

            waypoints = path.Waypoints;
            currentWaypointIndex = 0;
            isMoving = true;
            return true;
        }

        public void Stop()
        {
            waypoints = null;
            currentWaypointIndex = 0;
            isMoving = false;
            IsComplete = false;
        }

        /// <summary>
        /// Advances movement by a supplied duration; also supports deterministic Edit Mode tests.
        /// </summary>
        public void Advance(float deltaTime)
        {
            if (!isMoving || !IsFinite(deltaTime) || deltaTime < 0f || currentWaypointIndex >= waypoints.Count)
            {
                return;
            }

            Vector3 position = transform.position;
            Vector3 destination = waypoints[currentWaypointIndex];
            destination.y = position.y;
            Vector3 direction = destination - position;
            float distance = direction.magnitude;
            if (distance <= arrivalTolerance)
            {
                ArriveAtWaypoint(destination);
                return;
            }

            if (deltaTime <= 0f)
            {
                return;
            }

            Vector3 nextPosition = Vector3.MoveTowards(position, destination, speed * deltaTime);
            transform.position = new Vector3(nextPosition.x, position.y, nextPosition.z);

            if (rotationSpeed > 0f && direction.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation,
                    rotationSpeed * deltaTime);
            }

            if (Vector3.Distance(transform.position, destination) <= arrivalTolerance)
            {
                ArriveAtWaypoint(destination);
            }
        }

        private void ArriveAtWaypoint(Vector3 destination)
        {
            Vector3 position = transform.position;
            transform.position = new Vector3(destination.x, position.y, destination.z);
            currentWaypointIndex++;
            if (currentWaypointIndex >= waypoints.Count)
            {
                isMoving = false;
                IsComplete = true;
            }
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
