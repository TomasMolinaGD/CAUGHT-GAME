using System;
using System.Collections.Generic;
using System.Reflection;
using CaughtGame.Navigation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class HuntingGroundContainmentWanderTests
{
    private GameObject gridObject;
    private NavigationGrid grid;
    private GameObject regionObject;
    private HuntingGroundRegion region;
    private GameObject boidObject;
    private Component boidAgent;
    private Component containment;
    private Component wander;
    private Type agentType;
    private Type containmentType;
    private Type wanderType;

    [SetUp]
    public void SetUp()
    {
        gridObject = new GameObject("Hunting Ground Integration Grid Test");
        grid = gridObject.AddComponent<NavigationGrid>();
        Assert.That(grid.TryApplyGridSize(6, 6, 2f, true, out _), Is.True);

        regionObject = new GameObject("Hunting Ground Integration Region Test");
        region = regionObject.AddComponent<HuntingGroundRegion>();
        SetSerializedObjectReference(region, "navigationGrid", grid);

        agentType = FindProjectType("BoidAgent");
        containmentType = FindProjectType("PlayAreaContainmentBehaviour");
        wanderType = FindProjectType("WanderBehaviour");
        boidObject = new GameObject("Boid Behaviour Test");
        boidObject.AddComponent<Rigidbody>();
        boidObject.AddComponent<CapsuleCollider>();
        boidAgent = boidObject.AddComponent(agentType);
        containment = boidObject.AddComponent(containmentType);
        wander = boidObject.AddComponent(wanderType);
        SetPrivateField(containment, "owner", boidAgent);
        SetPrivateField(wander, "owner", boidAgent);
        SetPrivateField(wander, "desiredDirection", Vector3.forward);
    }

    [TearDown]
    public void TearDown()
    {
        if (boidObject != null) UnityEngine.Object.DestroyImmediate(boidObject);
        if (regionObject != null) UnityEngine.Object.DestroyImmediate(regionObject);
        if (gridObject != null) UnityEngine.Object.DestroyImmediate(gridObject);
    }

    [Test]
    public void Footprint_RequiresCenterAndCapsuleSamplesToBeInsideHuntingGround()
    {
        SetAllCells(NavigationGridZone.HuntingGround, true);
        Vector3 center = grid.GridToWorldPosition(2, 2);

        Assert.That(HuntingGroundFootprint.Contains(region, center, 0.5f, 0.1f), Is.True);
    }

    [Test]
    public void Footprint_RejectsBlockedAdjacentCellAndRespectsMargin()
    {
        SetAllCells(NavigationGridZone.HuntingGround, true);
        SetCell(2, 3, NavigationGridZone.HuntingGround, false);
        Vector3 nearBlockedCell = grid.transform.TransformPoint(new Vector3(5.7f, 0f, 5f));
        Vector3 nearMarginEdge = grid.transform.TransformPoint(new Vector3(5.4f, 0f, 5f));

        Assert.That(HuntingGroundFootprint.Contains(region, nearBlockedCell, 0.5f, 0f), Is.False);
        Assert.That(HuntingGroundFootprint.Contains(region, nearMarginEdge, 0.3f, 0.4f), Is.False);
        Assert.That(HuntingGroundFootprint.Contains(region, nearMarginEdge, 0.3f, 0f), Is.True);
    }

    [Test]
    public void Footprint_RejectsDiagonalCornerThatIsNotHuntingGround()
    {
        SetCell(0, 0, NavigationGridZone.HuntingGround, true);
        SetCell(0, 1, NavigationGridZone.HuntingGround, true);
        SetCell(1, 0, NavigationGridZone.HuntingGround, true);
        SetCell(1, 1, NavigationGridZone.HuntingGround, false);
        Vector3 nearSharedCorner = grid.transform.TransformPoint(new Vector3(1.6f, 0f, 1.6f));

        Assert.That(region.ContainsWorldPosition(nearSharedCorner), Is.True);
        Assert.That(HuntingGroundFootprint.Contains(region, nearSharedCorner, 0.6f, 0f), Is.False);
    }

    [Test]
    public void ConstrainMovement_StopsAtLastSafePointAndReportsOutwardDirection()
    {
        SetAllCells(NavigationGridZone.HuntingGround, true);
        SetCell(2, 3, NavigationGridZone.Normal, true);
        Vector3 start = grid.GridToWorldPosition(2, 2);
        Vector3 proposed = start + Vector3.right;

        Vector3 constrained = HuntingGroundFootprint.ConstrainMovement(
            region, start, proposed, 0.3f, 0.2f, false, default, out Vector3 outwardNormal);

        Assert.That(HuntingGroundFootprint.Contains(region, constrained, 0.3f, 0.2f), Is.True);
        Assert.That(constrained.x, Is.LessThan(proposed.x));
        Assert.That(outwardNormal.x, Is.GreaterThan(0.5f));
    }

    [Test]
    public void ConstrainMovement_DoesNotTeleportOrOscillateAtBoundary()
    {
        SetAllCells(NavigationGridZone.HuntingGround, true);
        SetCell(2, 3, NavigationGridZone.Normal, true);
        Vector3 position = grid.transform.TransformPoint(new Vector3(5.2f, 0f, 5f));
        Vector3 proposed = position + Vector3.right * 0.8f;
        float attemptedDistance = Vector3.Distance(position, proposed);

        Vector3 constrained = HuntingGroundFootprint.ConstrainMovement(
            region, position, proposed, 0.3f, 0.2f, false, default, out _);
        Vector3 repeated = HuntingGroundFootprint.ConstrainMovement(
            region, constrained, constrained + Vector3.right * 0.1f, 0.3f, 0.2f, false, default, out _);

        Assert.That(Vector3.Distance(position, constrained), Is.LessThanOrEqualTo(attemptedDistance));
        Assert.That(HuntingGroundFootprint.Contains(region, constrained, 0.3f, 0.2f), Is.True);
        Assert.That(Vector3.Distance(repeated, constrained), Is.LessThan(0.02f));
    }

    [Test]
    public void Footprint_RespectsTranslatedAndRotatedGridTransform()
    {
        SetAllCells(NavigationGridZone.HuntingGround, true);
        grid.transform.SetPositionAndRotation(new Vector3(12f, 2f, -9f), Quaternion.Euler(0f, 37f, 0f));
        Vector3 center = grid.GridToWorldPosition(2, 2);

        Assert.That(HuntingGroundFootprint.Contains(region, center, 0.5f, 0.1f), Is.True);
        Assert.That(region.ContainsWorldPosition(center + Vector3.right * 100f), Is.False);
    }

    [Test]
    public void ContainmentSteering_PointsTowardKnownInteriorPositionFromBorder()
    {
        SetAllCells(NavigationGridZone.HuntingGround, true);
        SetRequiredPolicy();
        Vector3 position = grid.transform.TransformPoint(new Vector3(8.7f, 0f, 5f));
        boidObject.transform.position = position;
        SetSerializedObjectReference(containment, "huntingGroundRegion", region);
        SetPrivateField(containment, "lastSafePosition", grid.GridToWorldPosition(2, 2));
        SetPrivateField(containment, "hasLastSafePosition", true);

        Vector3 steering = (Vector3)containmentType.GetMethod("CalculateSteering").Invoke(containment, null);

        Assert.That(steering.sqrMagnitude, Is.GreaterThan(0f));
        Assert.That(steering.x, Is.LessThan(0f));
    }

    [Test]
    public void ContainmentWithoutRegion_PreservesRectangularFallback()
    {
        MethodInfo clamp = containmentType.GetMethod("ClampInsidePlayableArea");
        object[] arguments = { new Vector3(600f, 0f, 700f), false, false };

        Vector3 result = (Vector3)clamp.Invoke(containment, arguments);

        Assert.That(result.x, Is.EqualTo(532.5f).Within(0.001f));
        Assert.That(result.z, Is.EqualTo(655.5f).Within(0.001f));
        Assert.That(arguments[1], Is.EqualTo(true));
        Assert.That(arguments[2], Is.EqualTo(true));
    }

    [Test]
    public void WanderWithRegion_SelectsTargetsInsideCotoWithCapsuleClearance()
    {
        SetAllCells(NavigationGridZone.HuntingGround, true);
        SetRequiredPolicy();
        boidObject.transform.position = grid.GridToWorldPosition(2, 2);
        SetSerializedObjectReference(wander, "huntingGroundRegion", region);
        SetSerializedFloat(wander, "minimumTravelDistance", 1f);
        SetSerializedFloat(wander, "maximumTravelDistance", 4f);
        SetSerializedFloat(wander, "huntingGroundMargin", 0.1f);
        SelectWanderTarget();
        Vector3 target = GetPrivateField<Vector3>(wander, "targetPosition");

        Assert.That(HuntingGroundFootprint.Contains(region, target, 0.5f, 0.1f), Is.True);
    }

    [Test]
    public void WanderWithoutRegion_PreservesConfiguredRectangleLogic()
    {
        SetSerializedVector2(wander, "areaCenter", Vector2.zero);
        SetSerializedVector2(wander, "areaHalfExtents", new Vector2(2f, 3f));
        SetSerializedFloat(wander, "minimumTravelDistance", 1f);
        SetSerializedFloat(wander, "maximumTravelDistance", 4f);
        boidObject.transform.position = Vector3.zero;

        SelectWanderTarget();
        Vector3 target = GetPrivateField<Vector3>(wander, "targetPosition");

        Assert.That(target.x, Is.InRange(-2f, 2f));
        Assert.That(target.z, Is.InRange(-3f, 3f));
    }

    private void SelectWanderTarget()
    {
        wanderType.GetMethod("SelectLocalTarget", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(wander, null);
    }

    private void SetRequiredPolicy()
    {
        Type policyType = FindProjectType("BoidAreaPolicy");
        SetPrivateField(boidAgent, "areaPolicy", Enum.Parse(policyType, "RequireHuntingGround"));
    }

    private void SetAllCells(NavigationGridZone zone, bool walkable)
    {
        for (int row = 0; row < grid.Rows; row++)
        {
            for (int column = 0; column < grid.Columns; column++)
            {
                SetCell(row, column, zone, walkable);
            }
        }
    }

    private void SetCell(int row, int column, NavigationGridZone zone, bool walkable)
    {
        Assert.That(grid.SetWalkable(row, column, walkable), Is.True);
        Assert.That(grid.SetZone(row, column, zone), Is.True);
    }

    private static Type FindProjectType(string typeName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(typeName, false);
            if (type != null) return type;
        }

        Assert.Fail("Could not locate project type " + typeName + ".");
        return null;
    }

    private static void SetSerializedObjectReference(Component target, string propertyName, UnityEngine.Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(propertyName).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetSerializedFloat(Component target, string propertyName, float value)
    {
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(propertyName).floatValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetSerializedVector2(Component target, string propertyName, Vector2 value)
    {
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(propertyName).vector2Value = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Missing field " + fieldName + ".");
        field.SetValue(target, value);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Missing field " + fieldName + ".");
        return (T)field.GetValue(target);
    }
}

public sealed class HuntingGroundFootprintExactTests
{
    private const float ReferenceGeometryEpsilon = 0.00001f;
    private GameObject gridObject;
    private GameObject regionObject;
    private NavigationGrid grid;
    private HuntingGroundRegion region;
    private readonly List<Vector2Int> invalidCells = new List<Vector2Int>();

    [SetUp]
    public void SetUp()
    {
        gridObject = new GameObject("Exact Footprint Grid Test");
        grid = gridObject.AddComponent<NavigationGrid>();
        Assert.That(grid.TryApplyGridSize(7, 7, 2f, true, out _), Is.True);

        regionObject = new GameObject("Exact Footprint Region Test");
        region = regionObject.AddComponent<HuntingGroundRegion>();
        SetPrivateField(region, "navigationGrid", grid);
        SetAllCells(NavigationGridZone.HuntingGround, true);
    }

    [TearDown]
    public void TearDown()
    {
        invalidCells.Clear();
        UnityEngine.Object.DestroyImmediate(regionObject);
        UnityEngine.Object.DestroyImmediate(gridObject);
    }

    [Test]
    public void Query_ClearForCircleInsideValidCells()
    {
        Assert.That(HuntingGroundFootprint.Query(region, grid.GridToWorldPosition(2, 2), 0f, 0f),
            Is.EqualTo(RegionQueryStatus.Clear));
        Assert.That(HuntingGroundFootprint.Query(region, grid.GridToWorldPosition(2, 2), 0.25f, 0f),
            Is.EqualTo(RegionQueryStatus.Clear));
    }

    [Test]
    public void Query_BlocksExactOuterBoundaryContactWithZeroOrPositiveRadius()
    {
        Vector3 boundary = grid.transform.TransformPoint(new Vector3(0f, 0f, 5f));
        Assert.That(HuntingGroundFootprint.Query(region, boundary, 0f, 0f),
            Is.EqualTo(RegionQueryStatus.Blocked));
        Assert.That(HuntingGroundFootprint.Query(region, boundary, 0.01f, 0f),
            Is.EqualTo(RegionQueryStatus.Blocked));
    }

    [Test]
    public void Query_BlocksTangencyAndMinimalInvasionIntoInvalidCell()
    {
        SetCell(2, 3, NavigationGridZone.Normal, true);
        Vector3 center = grid.GridToWorldPosition(2, 2);
        Assert.That(HuntingGroundFootprint.Query(region, center, 1f, 0f),
            Is.EqualTo(RegionQueryStatus.Blocked));

        Vector3 nearEdge = grid.transform.TransformPoint(new Vector3(5.99f, 0f, 5f));
        Assert.That(HuntingGroundFootprint.Query(region, nearEdge, 0.02f, 0f),
            Is.EqualTo(RegionQueryStatus.Blocked));
    }

    [Test]
    public void Query_BlocksDiagonalCornerAndEnclosedInvalidCell()
    {
        SetCell(3, 3, NavigationGridZone.Normal, true);
        Vector3 diagonal = grid.transform.TransformPoint(new Vector3(5.8f, 0f, 5.8f));
        Assert.That(HuntingGroundFootprint.Query(region, diagonal, 0.3f, 0f),
            Is.EqualTo(RegionQueryStatus.Blocked));

        SetCell(2, 2, NavigationGridZone.Normal, true);
        Vector3 adjacent = grid.GridToWorldPosition(2, 1);
        Assert.That(HuntingGroundFootprint.Query(region, adjacent, 1.5f, 0f),
            Is.EqualTo(RegionQueryStatus.Blocked));
    }

    [Test]
    public void Query_BlocksRadiusLargerThanCellAndHonorsMargin()
    {
        Vector3 center = grid.GridToWorldPosition(2, 2);
        SetCell(2, 3, NavigationGridZone.Normal, true);
        Assert.That(HuntingGroundFootprint.Query(region, center, 3f, 0f),
            Is.EqualTo(RegionQueryStatus.Blocked));
        Assert.That(HuntingGroundFootprint.Query(region, center, 0.5f, 0.6f),
            Is.EqualTo(RegionQueryStatus.Blocked));
    }

    [Test]
    public void Query_SupportsTranslatedYawedAndNonUniformScaledGrid()
    {
        grid.transform.SetPositionAndRotation(new Vector3(12f, 2f, -9f), Quaternion.Euler(0f, 37f, 0f));
        grid.transform.localScale = new Vector3(2f, 1f, 3f);
        Assert.That(HuntingGroundFootprint.Query(region, grid.GridToWorldPosition(2, 2), 0.25f, 0f),
            Is.EqualTo(RegionQueryStatus.Clear));
    }

    [Test]
    public void Query_RejectsInvalidConfigurationWithoutClampingInputs()
    {
        Vector3 center = grid.GridToWorldPosition(2, 2);
        Assert.That(HuntingGroundFootprint.Query(region, center, -0.1f, 0f),
            Is.EqualTo(RegionQueryStatus.InvalidConfiguration));
        Assert.That(HuntingGroundFootprint.Query(region, center, float.NaN, 0f),
            Is.EqualTo(RegionQueryStatus.InvalidConfiguration));
        Assert.That(HuntingGroundFootprint.Query(region, center, 0f, float.PositiveInfinity),
            Is.EqualTo(RegionQueryStatus.InvalidConfiguration));
        Assert.That(HuntingGroundFootprint.Query(region, new Vector3(float.NaN, 0f, 0f), 0f, 0f),
            Is.EqualTo(RegionQueryStatus.InvalidConfiguration));
    }

    [Test]
    public void Query_RejectsTiltedGridAndEmptyOrDisconnectedRegions()
    {
        grid.transform.rotation = Quaternion.Euler(0.1f, 0f, 0f);
        Assert.That(HuntingGroundFootprint.Query(region, grid.GridToWorldPosition(2, 2), 0f, 0f),
            Is.EqualTo(RegionQueryStatus.InvalidConfiguration));

        grid.transform.rotation = Quaternion.identity;
        SetCell(2, 2, NavigationGridZone.Normal, true);
        Assert.That(HuntingGroundFootprint.Query(region, grid.GridToWorldPosition(2, 2), 0f, 0f),
            Is.EqualTo(RegionQueryStatus.Blocked));

        SetAllCells(NavigationGridZone.Normal, false);
        SetCell(0, 0, NavigationGridZone.HuntingGround, true);
        Assert.That(HuntingGroundFootprint.Query(region, grid.GridToWorldPosition(0, 1), 0f, 0f),
            Is.EqualTo(RegionQueryStatus.Blocked));
    }

    [Test]
    public void Query_ContainsConservativeOffsetAndDetectsDiagonalRegression()
    {
        SetCell(3, 3, NavigationGridZone.Normal, true);
        Vector3 center = grid.GridToWorldPosition(2, 2);
        Assert.That(HuntingGroundFootprint.Contains(region, center, 0.5f, 0.5f), Is.True);
        Vector3 diagonal = grid.transform.TransformPoint(new Vector3(5.8f, 0f, 5.8f));
        Assert.That(HuntingGroundFootprint.Contains(region, diagonal, 0.3f, 0f), Is.False);
    }

    [Test]
    public void Query_InvalidatesDegenerateGridScale()
    {
        grid.transform.localScale = new Vector3(0f, 1f, 1f);
        Assert.That(HuntingGroundFootprint.Query(region, Vector3.zero, 0f, 0f),
            Is.EqualTo(RegionQueryStatus.InvalidConfiguration));
    }

    [Test]
    public void Query_RejectsWorldWidthProductThatOverflowsFloat()
    {
        invalidCells.Clear();
        Assert.That(grid.TryApplyGridSize(20, 2, 2f, true, out _), Is.True);
        grid.transform.localScale = new Vector3(1e37f, 1f, 1f);

        Vector3 position = grid.transform.TransformPoint(new Vector3(1f, 0f, 1f));
        Assert.That(HuntingGroundFootprint.Query(region, position, 0f, 0f),
            Is.EqualTo(RegionQueryStatus.InvalidConfiguration));
    }

    [Test]
    public void Query_RejectsWorldDepthProductThatOverflowsFloat()
    {
        invalidCells.Clear();
        Assert.That(grid.TryApplyGridSize(2, 20, 2f, true, out _), Is.True);
        grid.transform.localScale = new Vector3(1f, 1f, 1e37f);

        Vector3 position = grid.transform.TransformPoint(new Vector3(1f, 1f, 1f));
        Assert.That(HuntingGroundFootprint.Query(region, position, 0f, 0f),
            Is.EqualTo(RegionQueryStatus.InvalidConfiguration));
    }

    [Test]
    public void Query_BlocksTangencyWithNonUniformlyScaledCell()
    {
        grid.transform.localScale = new Vector3(2f, 1f, 3f);
        SetCell(2, 3, NavigationGridZone.Normal, true);
        Vector3 center = grid.GridToWorldPosition(2, 2);

        Assert.That(HuntingGroundFootprint.Query(region, center, 2f, 0f),
            Is.EqualTo(RegionQueryStatus.Blocked));
    }

    [Test]
    public void Query_BlocksEpsilonExpandedTangencyWithFollowingCellInvalid()
    {
        SetCell(2, 3, NavigationGridZone.Normal, true);
        Vector3 center = grid.GridToWorldPosition(2, 2);
        // The geometric gap is 0.000005m, which is inside the 0.00001m epsilon.
        float radius = 0.999995f;

        Assert.That(ReferenceQuery(center, radius, 0f), Is.EqualTo(RegionQueryStatus.Blocked));
        Assert.That(HuntingGroundFootprint.Query(region, center, radius, 0f),
            Is.EqualTo(RegionQueryStatus.Blocked));
    }

    [Test]
    public void Query_BlocksTangencyWithFollowingCellUnderYawAndNonUniformScale()
    {
        grid.transform.SetPositionAndRotation(new Vector3(12f, 2f, -9f), Quaternion.Euler(0f, 37f, 0f));
        grid.transform.localScale = new Vector3(2f, 1f, 3f);
        SetCell(2, 3, NavigationGridZone.Normal, true);
        Vector3 center = grid.GridToWorldPosition(2, 2);

        Assert.That(ReferenceQuery(center, 2f, 0f), Is.EqualTo(RegionQueryStatus.Blocked));
        Assert.That(HuntingGroundFootprint.Query(region, center, 2f, 0f),
            Is.EqualTo(RegionQueryStatus.Blocked));
    }

    [Test]
    public void Query_RejectsWorldCellsSmallerThanGeometryEpsilonAsDegenerate()
    {
        grid.transform.localScale = new Vector3(0.000001f, 1f, 0.000001f);
        Vector3 center = grid.transform.TransformPoint(new Vector3(5f, 0f, 5f));

        Assert.That(HuntingGroundFootprint.Query(region, center, 0f, 0f),
            Is.EqualTo(RegionQueryStatus.InvalidConfiguration));
    }

    [Test]
    public void LastIntersectingCell_IncludesFollowingCellAtExactBoundary()
    {
        MethodInfo method = typeof(HuntingGroundFootprint).GetMethod(
            "LastIntersectingCell", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);

        int previousFormula = (int)(Math.Ceiling(6d / 2d) - 1d);
        int actual = (int)method.Invoke(null, new object[] { 6d, 2f, 7 });

        Assert.That(previousFormula, Is.EqualTo(2));
        Assert.That(actual, Is.EqualTo(3));
    }

    [Test]
    public void Query_BlocksTangencyAtTranslatedYawedOuterBoundary()
    {
        grid.transform.SetPositionAndRotation(new Vector3(12f, 2f, -9f), Quaternion.Euler(0f, 37f, 0f));
        Vector3 boundary = grid.transform.TransformPoint(new Vector3(0f, 0f, 5f));

        Assert.That(HuntingGroundFootprint.Query(region, boundary, 0f, 0f),
            Is.EqualTo(RegionQueryStatus.Blocked));
    }

    [Test]
    public void Query_DistinguishesSeparationOutsideAndInsideGeometryEpsilon()
    {
        SetCell(2, 3, NavigationGridZone.Normal, true);
        Vector3 center = grid.GridToWorldPosition(2, 2);

        Assert.That(HuntingGroundFootprint.Query(region, center, 0.99998f, 0f),
            Is.EqualTo(RegionQueryStatus.Clear));
        Assert.That(HuntingGroundFootprint.Query(region, center, 0.999995f, 0f),
            Is.EqualTo(RegionQueryStatus.Blocked));
    }

    [Test]
    public void Query_BlocksCompletelyEnclosedInvalidCellOutsideTheCenterCell()
    {
        SetCell(3, 3, NavigationGridZone.Normal, true);
        Vector3 center = grid.transform.TransformPoint(new Vector3(5f, 0f, 7f));
        float radius = 3.2f;

        Assert.That(region.ContainsWorldPosition(center), Is.True);
        Assert.That(IsRectangleInsideReferenceDisk(3, 3, center, radius), Is.True);
        Assert.That(IsPointInsideCell(center, 3, 3), Is.False);
        Assert.That(HuntingGroundFootprint.Query(region, center, radius, 0f),
            Is.EqualTo(RegionQueryStatus.Blocked));
    }

    [Test]
    public void Query_RejectsFalseClearThatSixteenPerimeterSamplesMiss()
    {
        SetCell(3, 3, NavigationGridZone.Normal, true);
        Vector3 center = grid.transform.TransformPoint(new Vector3(5.9f, 0f, 5f));
        const float radius = 1.01f;

        Assert.That(region.ContainsWorldPosition(center), Is.True);
        Assert.That(OldSixteenSampleResult(center, radius, 0f), Is.True);
        Assert.That(ReferenceQuery(center, radius, 0f), Is.EqualTo(RegionQueryStatus.Blocked));
        Assert.That(HuntingGroundFootprint.Query(region, center, radius, 0f),
            Is.EqualTo(RegionQueryStatus.Blocked));
    }

    [Test]
    public void Query_MatchesIndependentReferenceAcrossCentersAndRadii()
    {
        SetCell(2, 3, NavigationGridZone.Normal, true);
        SetCell(4, 4, NavigationGridZone.Normal, true);
        Vector3[] centers =
        {
            grid.GridToWorldPosition(2, 2),
            grid.transform.TransformPoint(new Vector3(5.35f, 0f, 5.2f)),
            grid.transform.TransformPoint(new Vector3(8.2f, 0f, 7.6f))
        };
        float[] radii = { 0f, 0.45f, 1.2f };

        for (int i = 0; i < centers.Length; i++)
        {
            RegionQueryStatus expected = ReferenceQuery(centers[i], radii[i], 0.05f);
            RegionQueryStatus actual = HuntingGroundFootprint.Query(region, centers[i], radii[i], 0.05f);
            Assert.That(actual, Is.EqualTo(expected), "Scenario " + i + " diverged from reference.");
        }
    }

    private void SetAllCells(NavigationGridZone zone, bool walkable)
    {
        for (int row = 0; row < grid.Rows; row++)
        {
            for (int column = 0; column < grid.Columns; column++) SetCell(row, column, zone, walkable);
        }
    }

    private void SetCell(int row, int column, NavigationGridZone zone, bool walkable)
    {
        Assert.That(grid.SetWalkable(row, column, walkable), Is.True);
        Assert.That(grid.SetZone(row, column, zone), Is.True);
        Vector2Int coordinate = new Vector2Int(row, column);
        if (!walkable || zone != NavigationGridZone.HuntingGround)
        {
            if (!invalidCells.Contains(coordinate)) invalidCells.Add(coordinate);
        }
        else
        {
            invalidCells.Remove(coordinate);
        }
    }

    private RegionQueryStatus ReferenceQuery(Vector3 worldPosition, float radius, float margin)
    {
        if (!IsFinite(worldPosition) || !IsFinite(radius) || !IsFinite(margin) || radius < 0f || margin < 0f)
        {
            return RegionQueryStatus.InvalidConfiguration;
        }

        Vector3 axisX = grid.transform.TransformVector(Vector3.right);
        Vector3 axisZ = grid.transform.TransformVector(Vector3.forward);
        float width = axisX.magnitude * grid.CellSize;
        float depth = axisZ.magnitude * grid.CellSize;
        axisX.Normalize();
        axisZ.Normalize();
        Vector3 origin = grid.transform.TransformPoint(Vector3.zero);
        float radiusWithMargin = radius + margin;
        float radiusWithEpsilon = radiusWithMargin + ReferenceGeometryEpsilon;
        Vector3 offset = worldPosition - origin;
        float u = Vector3.Dot(offset, axisX);
        float v = Vector3.Dot(offset, axisZ);
        if (u - radiusWithMargin <= ReferenceGeometryEpsilon ||
            u + radiusWithMargin >= width * grid.Columns - ReferenceGeometryEpsilon ||
            v - radiusWithMargin <= ReferenceGeometryEpsilon ||
            v + radiusWithMargin >= depth * grid.Rows - ReferenceGeometryEpsilon)
        {
            return RegionQueryStatus.Blocked;
        }

        float radiusSquared = radiusWithEpsilon * radiusWithEpsilon;
        for (int i = 0; i < invalidCells.Count; i++)
        {
            Vector2Int cell = invalidCells[i];
            Vector3 cellCenter = grid.transform.TransformPoint(new Vector3(
                (cell.y + 0.5f) * grid.CellSize, 0f, (cell.x + 0.5f) * grid.CellSize));
            Vector3 cellOffset = worldPosition - cellCenter;
            float localX = Vector3.Dot(cellOffset, axisX);
            float localZ = Vector3.Dot(cellOffset, axisZ);
            float halfWidth = width * 0.5f;
            float halfDepth = depth * 0.5f;
            float closestX = Mathf.Clamp(localX, -halfWidth, halfWidth);
            float closestZ = Mathf.Clamp(localZ, -halfDepth, halfDepth);
            float dx = localX - closestX;
            float dz = localZ - closestZ;
            if (dx * dx + dz * dz <= radiusSquared) return RegionQueryStatus.Blocked;
        }

        return RegionQueryStatus.Clear;
    }

    private bool OldSixteenSampleResult(Vector3 center, float radius, float margin)
    {
        Vector3[] directions =
        {
            Vector3.right,
            new Vector3(0.9238795f, 0f, 0.3826834f),
            new Vector3(0.7071068f, 0f, 0.7071068f),
            new Vector3(0.3826834f, 0f, 0.9238795f),
            Vector3.forward,
            new Vector3(-0.3826834f, 0f, 0.9238795f),
            new Vector3(-0.7071068f, 0f, 0.7071068f),
            new Vector3(-0.9238795f, 0f, 0.3826834f),
            Vector3.left,
            new Vector3(-0.9238795f, 0f, -0.3826834f),
            new Vector3(-0.7071068f, 0f, -0.7071068f),
            new Vector3(-0.3826834f, 0f, -0.9238795f),
            Vector3.back,
            new Vector3(0.3826834f, 0f, -0.9238795f),
            new Vector3(0.7071068f, 0f, -0.7071068f),
            new Vector3(0.9238795f, 0f, -0.3826834f)
        };
        float clearance = Mathf.Max(0f, radius) + Mathf.Max(0f, margin);
        if (!region.ContainsWorldPosition(center)) return false;
        for (int i = 0; i < directions.Length; i++)
        {
            if (!region.ContainsWorldPosition(center + directions[i] * clearance)) return false;
        }
        return true;
    }

    private bool IsRectangleInsideReferenceDisk(int row, int column, Vector3 center, float radius)
    {
        Vector3 axisX = grid.transform.TransformVector(Vector3.right).normalized;
        Vector3 axisZ = grid.transform.TransformVector(Vector3.forward).normalized;
        Vector3 cellCenter = grid.transform.TransformPoint(new Vector3(
            (column + 0.5f) * grid.CellSize, 0f, (row + 0.5f) * grid.CellSize));
        float halfWidth = grid.transform.TransformVector(Vector3.right).magnitude * grid.CellSize * 0.5f;
        float halfDepth = grid.transform.TransformVector(Vector3.forward).magnitude * grid.CellSize * 0.5f;
        float localX = Vector3.Dot(cellCenter - center, axisX);
        float localZ = Vector3.Dot(cellCenter - center, axisZ);
        float farthestX = Mathf.Abs(localX) + halfWidth;
        float farthestZ = Mathf.Abs(localZ) + halfDepth;
        return farthestX * farthestX + farthestZ * farthestZ < radius * radius;
    }

    private bool IsPointInsideCell(Vector3 point, int row, int column)
    {
        return grid.TryWorldToGrid(point, out int actualRow, out int actualColumn) &&
               actualRow == row && actualColumn == column;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Missing field " + fieldName + ".");
        field.SetValue(target, value);
    }
}

public sealed class HuntingGroundContinuousMovementTests
{
    private GameObject gridObject;
    private NavigationGrid grid;
    private GameObject regionObject;
    private HuntingGroundRegion region;

    [SetUp]
    public void SetUp()
    {
        gridObject = new GameObject("Continuous Movement Grid Test");
        grid = gridObject.AddComponent<NavigationGrid>();
        Assert.That(grid.TryApplyGridSize(8, 3, 2f, true, out _), Is.True);

        regionObject = new GameObject("Continuous Movement Region Test");
        region = regionObject.AddComponent<HuntingGroundRegion>();
        SerializedObject serialized = new SerializedObject(region);
        serialized.FindProperty("navigationGrid").objectReferenceValue = grid;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        for (int row = 0; row < grid.Rows; row++)
        {
            for (int column = 0; column < grid.Columns; column++)
            {
                Assert.That(grid.SetWalkable(row, column, true), Is.True);
                Assert.That(grid.SetZone(row, column, NavigationGridZone.HuntingGround), Is.True);
            }
        }
    }

    [TearDown]
    public void TearDown()
    {
        if (regionObject != null) UnityEngine.Object.DestroyImmediate(regionObject);
        if (gridObject != null) UnityEngine.Object.DestroyImmediate(gridObject);
    }

    [Test]
    public void Sweep_AllowsFreeMulticellMovement()
    {
        Vector3 start = grid.GridToWorldPosition(1, 1);
        Vector3 end = grid.GridToWorldPosition(1, 5);

        RegionSweepResult result = HuntingGroundFootprint.Sweep(region, start, end, 0.25f, 0f);

        Assert.That(result.Status, Is.EqualTo(RegionSweepStatus.Clear));
        Assert.That(result.SafeFraction, Is.EqualTo(1f));
    }

    [Test]
    public void SweepStopsAtFirstInvalidCellAndReportsSafePrefix()
    {
        Assert.That(grid.SetZone(1, 3, NavigationGridZone.Normal), Is.True);
        Vector3 start = grid.GridToWorldPosition(1, 1);
        Vector3 end = grid.GridToWorldPosition(1, 5);

        RegionSweepResult result = HuntingGroundFootprint.Sweep(region, start, end, 0.25f, 0f);

        Assert.That(result.Status, Is.EqualTo(RegionSweepStatus.Blocked));
        Assert.That(result.SafeFraction, Is.LessThan(result.ContactFraction.Value));
        Assert.That(result.Column, Is.EqualTo(3));
        Assert.That(result.Row, Is.EqualTo(1));
    }

    [Test]
    public void SweepRejectsInvalidStartAndZeroLengthInvalidMovement()
    {
        Assert.That(grid.SetZone(1, 1, NavigationGridZone.Normal), Is.True);
        Vector3 invalid = grid.GridToWorldPosition(1, 1);

        RegionSweepResult result = HuntingGroundFootprint.Sweep(region, invalid, invalid, 0.25f, 0f);

        Assert.That(result.Status, Is.EqualTo(RegionSweepStatus.InvalidStart));
    }

    [Test]
    public void SweepRejectsCandidateBudgetWithoutTruncatingTheSearch()
    {
        Vector3 start = grid.GridToWorldPosition(1, 0);
        Vector3 end = grid.GridToWorldPosition(1, 7);

        RegionSweepResult result = HuntingGroundFootprint.Sweep(region, start, end, 0.25f, 0f, 1);

        Assert.That(result.Status, Is.EqualTo(RegionSweepStatus.BudgetExceeded));
        Assert.That(result.SafeFraction, Is.EqualTo(0f));
    }

    [Test]
    public void SweepBlocksOuterBoundaryContact()
    {
        Vector3 start = grid.GridToWorldPosition(1, 1);
        Vector3 end = grid.transform.TransformPoint(new Vector3(-0.25f, 0f, 3f));

        RegionSweepResult result = HuntingGroundFootprint.Sweep(region, start, end, 0.25f, 0f);

        Assert.That(result.Status, Is.EqualTo(RegionSweepStatus.Blocked));
        Assert.That(result.Normal.x, Is.LessThan(0f));
    }

    [Test]
    public void SweepSupportsYawAndNonUniformHorizontalScale()
    {
        grid.transform.SetPositionAndRotation(new Vector3(10f, 0f, -4f), Quaternion.Euler(0f, 35f, 0f));
        grid.transform.localScale = new Vector3(1.5f, 1f, 0.75f);
        Vector3 start = grid.GridToWorldPosition(1, 1);
        Vector3 end = grid.GridToWorldPosition(1, 5);

        RegionSweepResult result = HuntingGroundFootprint.Sweep(region, start, end, 0.25f, 0.1f);

        Assert.That(result.Status, Is.EqualTo(RegionSweepStatus.Clear));
    }

    [Test]
    public void SweepDetectsDiagonalCornerContactWithDoublePrecision()
    {
        Assert.That(grid.SetZone(1, 3, NavigationGridZone.Normal), Is.True);
        Vector3 start = grid.transform.TransformPoint(new Vector3(4f, 0f, 1f));
        Vector3 end = grid.transform.TransformPoint(new Vector3(6.2f, 0f, 1.05f));

        RegionSweepResult result = HuntingGroundFootprint.Sweep(region, start, end, 0.96f, 0f);

        Assert.That(result.Status, Is.EqualTo(RegionSweepStatus.Blocked));
        Assert.That(result.Column, Is.EqualTo(3));
    }

    [Test]
    public void SweepLeavesAReallyFreeNearbyCornerUnblocked()
    {
        Assert.That(grid.SetZone(1, 3, NavigationGridZone.Normal), Is.True);
        Vector3 start = grid.transform.TransformPoint(new Vector3(4f, 0f, 1f));
        Vector3 end = grid.transform.TransformPoint(new Vector3(6.2f, 0f, 1.05f));

        RegionSweepResult result = HuntingGroundFootprint.Sweep(region, start, end, 0.94f, 0f);

        Assert.That(result.Status, Is.EqualTo(RegionSweepStatus.Clear));
    }

    [Test]
    public void SweepNormalsPointTowardTheBlockingSurfaceForCellsAndOuterBoundary()
    {
        Assert.That(grid.SetZone(1, 3, NavigationGridZone.Normal), Is.True);
        Vector3 internalStart = grid.GridToWorldPosition(1, 1);
        Vector3 internalEnd = grid.GridToWorldPosition(1, 5);
        RegionSweepResult internalResult = HuntingGroundFootprint.Sweep(
            region, internalStart, internalEnd, 0.25f, 0f);

        Vector3 outerEnd = grid.transform.TransformPoint(new Vector3(-0.25f, 0f, 3f));
        RegionSweepResult outerResult = HuntingGroundFootprint.Sweep(
            region, internalStart, outerEnd, 0.25f, 0f);

        Assert.That(internalResult.Status, Is.EqualTo(RegionSweepStatus.Blocked));
        Assert.That(internalResult.Normal.x, Is.GreaterThan(0.5f));
        Assert.That(outerResult.Status, Is.EqualTo(RegionSweepStatus.Blocked));
        Assert.That(outerResult.Normal.x, Is.LessThan(-0.5f));
    }
}
