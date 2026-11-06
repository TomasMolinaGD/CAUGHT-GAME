using UnityEngine;

namespace CaughtGame.Navigation
{
    /// <summary>Observes an existing PathFollower to capture elapsed time without changing movement.</summary>
    [DisallowMultipleComponent]
    public sealed class NavigationRouteMetricsRecorder : MonoBehaviour
    {
        private NavigationRouteMetrics currentMetrics;
        private PathFollower observedFollower;
        private float startTime;

        public NavigationRouteMetrics CurrentMetrics => currentMetrics;
        public bool IsRecording => observedFollower != null && currentMetrics != null && !currentMetrics.HasActualTime;

        public bool Begin(NavigationRouteMetrics metrics, PathFollower follower)
        {
            if (metrics == null || metrics.PathStatus != NavigationPathStatus.Success ||
                follower == null || !follower.IsMoving)
            {
                return false;
            }

            currentMetrics = metrics.WithoutActualMovementTime();
            observedFollower = follower;
            startTime = Time.unscaledTime;
            return true;
        }

        public void Clear()
        {
            observedFollower = null;
            currentMetrics = null;
            startTime = 0f;
        }

        private void Update()
        {
            RecordCompletionIfReady();
        }

        /// <summary>Records completion when the observed follower has reached its final waypoint.</summary>
        public bool RecordCompletionIfReady()
        {
            if (observedFollower == null || currentMetrics == null || !observedFollower.IsComplete) return false;

            float elapsed = Time.unscaledTime - startTime;
            currentMetrics = currentMetrics.WithActualMovementTime(elapsed);
            observedFollower = null;
            return true;
        }
    }
}
