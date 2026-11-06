using System;
using System.Collections.Generic;
using UnityEngine;

namespace CaughtGame.Navigation
{
    /// <summary>Immutable summary of an explicit physical route-clearance check.</summary>
    public sealed class NavigationPathValidationResult
    {
        private static readonly IReadOnlyList<NavigationPathSegmentValidation> EmptySegments =
            Array.AsReadOnly(Array.Empty<NavigationPathSegmentValidation>());
        private readonly IReadOnlyList<NavigationPathSegmentValidation> segments;

        public NavigationPathValidationStatus Status { get; }
        public string Message { get; }
        public int FirstProblematicSegmentIndex { get; }
        public Collider FirstBlockingCollider { get; }
        public Vector3 FirstProblemPosition { get; }
        public IReadOnlyList<NavigationPathSegmentValidation> Segments => segments ?? EmptySegments;
        public bool IsClear => Status == NavigationPathValidationStatus.Clear;

        internal NavigationPathValidationResult(NavigationPathValidationStatus status, string message,
            int firstProblematicSegmentIndex = -1, Collider firstBlockingCollider = null,
            Vector3 firstProblemPosition = default, NavigationPathSegmentValidation[] segments = null)
        {
            Status = status;
            Message = message ?? string.Empty;
            FirstProblematicSegmentIndex = firstProblematicSegmentIndex;
            FirstBlockingCollider = firstBlockingCollider;
            FirstProblemPosition = firstProblemPosition;
            this.segments = segments == null ? EmptySegments : Array.AsReadOnly(segments);
        }
    }
}
