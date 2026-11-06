using CaughtGame.Navigation;
using NUnit.Framework;
using UnityEngine;

public sealed class PathFollowerTests
{
    private GameObject gridObject;
    private GameObject followerObject;
    private NavigationGrid grid;
    private PathFollower follower;

    [SetUp]
    public void SetUp()
    {
        gridObject = new GameObject("Path Follower Test Grid");
        grid = gridObject.AddComponent<NavigationGrid>();
        Assert.That(grid.TryApplyGridSize(3, 1, 2f, true, out _), Is.True);

        followerObject = new GameObject("Path Follower Test Agent");
        followerObject.transform.position = grid.GridToWorldPosition(0, 0) + Vector3.up;
        follower = followerObject.AddComponent<PathFollower>();
        follower.Speed = 4f;
        follower.ArrivalTolerance = 0.05f;
    }

    [TearDown]
    public void TearDown()
    {
        if (followerObject != null) Object.DestroyImmediate(followerObject);
        if (gridObject != null) Object.DestroyImmediate(gridObject);
    }

    [Test]
    public void FollowPath_AdvancesWaypointsInOrderAndStopsAtDestination()
    {
        NavigationPathResult path = NavigationGridPathfinder.FindPath(
            grid, 0, 0, 0, 2, NavigationAgentType.Hunter);

        Assert.That(follower.FollowPath(path), Is.True);
        Assert.That(follower.IsMoving, Is.True);
        Assert.That(follower.IsComplete, Is.False);
        Assert.That(follower.CurrentDestination.x, Is.EqualTo(1f).Within(0.0001f));

        follower.Advance(0f);
        Assert.That(follower.CurrentWaypointIndex, Is.EqualTo(1));
        Assert.That(follower.CurrentDestination.x, Is.EqualTo(3f).Within(0.0001f));

        follower.Advance(0.5f);
        Assert.That(follower.CurrentWaypointIndex, Is.EqualTo(2));
        Assert.That(follower.transform.position.x, Is.EqualTo(3f).Within(0.0001f));

        follower.Advance(0.5f);
        Assert.That(follower.IsMoving, Is.False);
        Assert.That(follower.IsComplete, Is.True);
        Assert.That(follower.transform.position.x, Is.EqualTo(5f).Within(0.0001f));
        Assert.That(follower.transform.position.y, Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void FollowPath_RejectsFailedPathAndDoesNotMove()
    {
        Assert.That(grid.SetWalkable(0, 0, false), Is.True);
        NavigationPathResult failedPath = NavigationGridPathfinder.FindPath(
            grid, 0, 0, 0, 2, NavigationAgentType.Hunter);
        Vector3 initialPosition = follower.transform.position;

        Assert.That(follower.FollowPath(failedPath), Is.False);
        follower.Advance(1f);

        Assert.That(follower.IsMoving, Is.False);
        Assert.That(follower.IsComplete, Is.False);
        Assert.That(follower.transform.position, Is.EqualTo(initialPosition));
    }
}
