using System;
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
