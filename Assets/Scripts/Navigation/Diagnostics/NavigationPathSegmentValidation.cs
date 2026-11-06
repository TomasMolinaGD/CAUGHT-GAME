using UnityEngine;

namespace CaughtGame.Navigation
{
    /// <summary>Cached physical clearance result for one consecutive waypoint pair.</summary>
    public readonly struct NavigationPathSegmentValidation
    {
        public Vector3 Start { get; }
        public Vector3 End { get; }
        public bool IsClear { get; }
        public Collider BlockingCollider { get; }
        public Vector3 ProblemPosition { get; }

        public NavigationPathSegmentValidation(Vector3 start, Vector3 end, bool isClear,
            Collider blockingCollider, Vector3 problemPosition)
        {
            Start = start;
            End = end;
            IsClear = isClear;
            BlockingCollider = blockingCollider;
            ProblemPosition = problemPosition;
        }
    }
}
