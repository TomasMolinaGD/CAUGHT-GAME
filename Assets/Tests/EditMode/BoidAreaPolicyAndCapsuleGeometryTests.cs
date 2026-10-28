using System;
using System.Reflection;
using CaughtGame.Navigation;
using NUnit.Framework;
using UnityEngine;

public sealed class BoidAreaPolicyAndCapsuleGeometryTests
{
    private GameObject boidObject;
    private GameObject containmentObject;
    private GameObject wanderObject;
    private GameObject respawnAreaObject;
    private GameObject gridObject;
    private GameObject regionObject;
    private Type boidAgentType;
    private Type containmentType;
    private Type wanderType;
    private Type respawnAreaType;
    private Type areaPolicyType;
    private GameObject obstacleObject;
    private GameObject saturationObstaclesObject;

    [SetUp]
    public void SetUp()
    {
        boidAgentType = FindProjectType("BoidAgent");
        containmentType = FindProjectType("PlayAreaContainmentBehaviour");
        wanderType = FindProjectType("WanderBehaviour");
        respawnAreaType = FindProjectType("BoidRespawnArea");
        areaPolicyType = FindProjectType("BoidAreaPolicy");

        boidObject = new GameObject("B1 Policy Test Boid");
        boidObject.AddComponent<Rigidbody>();
        CapsuleCollider capsule = boidObject.AddComponent<CapsuleCollider>();
        capsule.height = 2f;
        boidObject.AddComponent(boidAgentType);

        containmentObject = boidObject;
        containmentObject.AddComponent(containmentType);
        wanderObject = boidObject;
        wanderObject.AddComponent(wanderType);
    }

    [TearDown]
    public void TearDown()
    {
        if (obstacleObject != null) UnityEngine.Object.DestroyImmediate(obstacleObject);
        if (saturationObstaclesObject != null) UnityEngine.Object.DestroyImmediate(saturationObstaclesObject);
        if (respawnAreaObject != null) UnityEngine.Object.DestroyImmediate(respawnAreaObject);
        if (regionObject != null) UnityEngine.Object.DestroyImmediate(regionObject);
        if (gridObject != null) UnityEngine.Object.DestroyImmediate(gridObject);
        if (boidObject != null) UnityEngine.Object.DestroyImmediate(boidObject);
    }

    [Test]
    public void AreaPolicy_DefaultsToLegacyRectangle()
    {
        object policy = boidAgentType.GetProperty("AreaPolicy").GetValue(
            boidObject.GetComponent(boidAgentType));

        Assert.That(policy.ToString(), Is.EqualTo("LegacyRectangle"));
        Assert.That(Enum.IsDefined(areaPolicyType, "RequireHuntingGround"), Is.True);
    }

    [Test]
    public void RequiredPolicy_MissingRegionStopsMovementInsteadOfUsingLegacyRectangle()
    {
        Component agent = boidObject.GetComponent(boidAgentType);
        SetPrivateField(agent, "body", boidObject.GetComponent<Rigidbody>());
        SetPrivateField(agent, "capsule", boidObject.GetComponent<CapsuleCollider>());
        SetPrivateField(agent, "containment", boidObject.GetComponent(containmentType));
        SetPolicy(agent, "RequireHuntingGround");
        boidObject.transform.position = new Vector3(10000f, 0f, -10000f);
        Rigidbody body = boidObject.GetComponent<Rigidbody>();
        body.position = boidObject.transform.position;

        InvokePrivate(agent, "FixedUpdate");

        Assert.That(body.position, Is.EqualTo(new Vector3(10000f, 0f, -10000f)));
        Assert.That((Vector3)boidAgentType.GetProperty("Velocity").GetValue(agent), Is.EqualTo(Vector3.zero));
        Assert.That((Vector3)boidAgentType.GetProperty("ActualVelocity").GetValue(agent), Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void RequiredPolicy_WanderDoesNotSelectLegacyRectangleTargetWhenRegionIsMissing()
    {
        Component agent = boidObject.GetComponent(boidAgentType);
        Component wander = boidObject.GetComponent(wanderType);
        SetPolicy(agent, "RequireHuntingGround");
        SetPrivateField(wander, "owner", agent);
        SetPrivateField(wander, "hasTarget", false);

        InvokePrivate(wander, "SelectLocalTarget");

        Assert.That(GetPrivateField<bool>(wander, "hasTarget"), Is.False);
    }

    [Test]
    public void RequiredPolicy_ContainmentDoesNotClampToLegacyRectangleWhenRegionIsMissing()
    {
        Component agent = boidObject.GetComponent(boidAgentType);
        Component containment = boidObject.GetComponent(containmentType);
        SetPolicy(agent, "RequireHuntingGround");
        SetPrivateField(containment, "owner", agent);
        Vector3 outsideLegacyRectangle = new Vector3(10000f, 0f, -10000f);
        object[] arguments = { outsideLegacyRectangle, false, false };

        Vector3 result = (Vector3)containmentType.GetMethod("ClampInsidePlayableArea")
            .Invoke(containment, arguments);

        Assert.That(result, Is.EqualTo(outsideLegacyRectangle));
        Assert.That(arguments[1], Is.EqualTo(false));
        Assert.That(arguments[2], Is.EqualTo(false));
    }

    [Test]
    public void RequiredPolicy_RespawnRejectsLegacyPlacementWhenRegionIsMissing()
    {
        respawnAreaObject = new GameObject("B1 Policy Test Respawn Area");
        respawnAreaObject.AddComponent<BoxCollider>().isTrigger = true;
        Component respawnArea = respawnAreaObject.AddComponent(respawnAreaType);
        Component agent = boidObject.GetComponent(boidAgentType);
        SetPolicy(agent, "RequireHuntingGround");

        object[] arguments = { boidObject.GetComponent<CapsuleCollider>(), default(Vector3) };
        bool selected = (bool)respawnAreaType.GetMethod(
            "TryGetRandomPosition", new[] { typeof(CapsuleCollider), typeof(Vector3).MakeByRefType() })
            .Invoke(respawnArea, arguments);

        Assert.That(selected, Is.False);
    }

    [Test]
    public void RequiredPolicy_RespawnDoesNotReuseInitialPositionWhenAreaIsMissing()
    {
        Component agent = boidObject.GetComponent(boidAgentType);
        SetPolicy(agent, "RequireHuntingGround");
        Component life = boidObject.AddComponent(FindProjectType("BoidLife"));
        Component respawn = boidObject.AddComponent(FindProjectType("BoidRespawn"));
        Vector3 initialPosition = new Vector3(25f, 0f, -10f);
        SetPrivateField(life, "state", Enum.Parse(FindProjectType("BoidLifeState"), "Respawning"));
        SetPrivateField(respawn, "life", life);
        SetPrivateField(respawn, "body", boidObject.GetComponent<Rigidbody>());
        SetPrivateField(respawn, "boidAgent", agent);
        SetPrivateField(respawn, "initialPosition", initialPosition);

        bool succeeded = (bool)respawn.GetType().GetMethod("TryRespawnNow").Invoke(respawn, null);

        Assert.That(succeeded, Is.False);
        Assert.That(boidObject.transform.position, Is.Not.EqualTo(initialPosition));
    }

    [Test]
    public void LegacyPolicy_WithRegionConfigured_ContainmentStillUsesLegacyRectangle()
    {
        ConfigureRequiredRegion();
        Component agent = boidObject.GetComponent(boidAgentType);
        Component containment = boidObject.GetComponent(containmentType);
        SetPolicy(agent, "LegacyRectangle");
        Vector3 outside = new Vector3(10000f, 0f, -10000f);

        object[] arguments = { outside, false, false };
        Vector3 result = (Vector3)containmentType.GetMethod("ClampInsidePlayableArea")
            .Invoke(containment, arguments);

        Assert.That((bool)containmentType.GetProperty("HasHuntingGroundRegion").GetValue(containment), Is.False);
        Assert.That(result, Is.Not.EqualTo(outside));
        Assert.That(arguments[1], Is.EqualTo(true));
        Assert.That(arguments[2], Is.EqualTo(true));
    }

    [Test]
    public void LegacyPolicy_WithRegionConfigured_WanderKeepsLegacyTargetSelection()
    {
        ConfigureRequiredRegion();
        Component agent = boidObject.GetComponent(boidAgentType);
        Component wander = boidObject.GetComponent(wanderType);
        SetPolicy(agent, "LegacyRectangle");
        SetPrivateField(wander, "hasTarget", false);

        InvokePrivate(wander, "SelectLocalTarget");

        Assert.That(GetPrivateField<bool>(wander, "hasTarget"), Is.True);
        Assert.That(GetPrivateField<bool>(wander, "hasUsableTarget"), Is.True);
    }

    [Test]
    public void LegacyPolicy_WithRegionConfigured_RespawnKeepsLegacyPlacement()
    {
        ConfigureRequiredRegion();
        respawnAreaObject = new GameObject("B1 Legacy Respawn Area");
        respawnAreaObject.AddComponent<BoxCollider>().isTrigger = true;
        Component respawnArea = respawnAreaObject.AddComponent(respawnAreaType);
        SetPrivateField(respawnArea, "huntingGroundRegion", regionObject.GetComponent(typeof(HuntingGroundRegion)));
        SetPolicy(boidObject.GetComponent(boidAgentType), "LegacyRectangle");

        object[] arguments = { boidObject.GetComponent<CapsuleCollider>(), default(Vector3) };
        bool selected = (bool)respawnAreaType.GetMethod(
            "TryGetRandomPosition", new[] { typeof(CapsuleCollider), typeof(Vector3).MakeByRefType() })
            .Invoke(respawnArea, arguments);

        Assert.That(selected, Is.True);
    }

    [Test]
    public void LegacyPolicy_WithUnsupportedPose_KeepsPhysicalCapsuleRadius()
    {
        Component agent = boidObject.GetComponent(boidAgentType);
        SetPrivateField(agent, "body", boidObject.GetComponent<Rigidbody>());
        SetPrivateField(agent, "capsule", boidObject.GetComponent<CapsuleCollider>());
        boidObject.transform.rotation = Quaternion.Euler(15f, 0f, 0f);
        boidObject.GetComponent<Rigidbody>().rotation = boidObject.transform.rotation;

        float radius = (float)boidAgentType.GetProperty("HorizontalCapsuleRadius").GetValue(agent);

        Assert.That(radius, Is.GreaterThan(0f));
    }

    [Test]
    public void RequiredPolicy_PhysicalRecertificationPreservesClearSegment()
    {
        Component agent = PreparePhysicalAgent();

        bool accepted = TryRecertify(agent, new Vector3(1f, 0f, 0f), out Vector3 approved);

        Assert.That(accepted, Is.True);
        Assert.That(approved.x, Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void RequiredPolicy_PhysicalRecertificationStopsBeforeObstacleWithoutSliding()
    {
        Component agent = PreparePhysicalAgent();
        obstacleObject = CreateObstacle(new Vector3(1.5f, 1f, 0f), new Vector3(0.2f, 2f, 2f));

        bool accepted = TryRecertify(agent, new Vector3(3f, 0f, 0f), out Vector3 approved);

        Assert.That(accepted, Is.True);
        Assert.That(approved.x, Is.GreaterThan(0f));
        Assert.That(approved.x, Is.LessThan(1f));
        Assert.That(approved.z, Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void RequiredPolicy_PhysicalRecertificationFailsClosedForInitialOverlap()
    {
        Component agent = PreparePhysicalAgent();
        obstacleObject = CreateObstacle(Vector3.zero, new Vector3(2f, 2f, 2f));

        bool accepted = TryRecertify(agent, new Vector3(1f, 0f, 0f), out Vector3 approved);

        Assert.That(accepted, Is.False);
        Assert.That(approved, Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void RequiredPolicy_PhysicalRecertificationIgnoresTriggerObstacles()
    {
        Component agent = PreparePhysicalAgent();
        obstacleObject = CreateObstacle(new Vector3(1.5f, 1f, 0f), new Vector3(0.2f, 2f, 2f));
        obstacleObject.GetComponent<BoxCollider>().isTrigger = true;

        bool accepted = TryRecertify(agent, new Vector3(3f, 0f, 0f), out Vector3 approved);

        Assert.That(accepted, Is.True);
        Assert.That(approved.x, Is.EqualTo(3f).Within(0.0001f));
    }

    [Test]
    public void LegacyPolicy_SaturatedOverlapBufferStillResolvesPenetration()
    {
        Component agent = PreparePhysicalAgent();
        SetPolicy(agent, "LegacyRectangle");
        saturationObstaclesObject = CreateSaturationObstacles();
        Vector3 resolvedPosition = Vector3.zero;

        Assert.That(CountPhysicalOverlaps(resolvedPosition), Is.GreaterThanOrEqualTo(16));
        bool resolved = ResolveEnvironmentOverlaps(agent, ref resolvedPosition);

        Assert.That(resolved, Is.True);
        Assert.That(CountPhysicalOverlaps(resolvedPosition), Is.EqualTo(0));
    }

    [Test]
    public void RequiredPolicy_SaturatedOverlapBufferRejectsPhysicalMovement()
    {
        Component agent = PreparePhysicalAgent();
        saturationObstaclesObject = CreateSaturationObstacles();
        Vector3 resolvedPosition = Vector3.zero;

        bool resolved = ResolveEnvironmentOverlaps(agent, ref resolvedPosition);
        bool recertified = TryRecertify(agent, new Vector3(1f, 0f, 0f), out Vector3 approved);

        Assert.That(resolved, Is.False);
        Assert.That(recertified, Is.False);
        Assert.That(approved, Is.EqualTo(Vector3.zero));
        Assert.That(CountPhysicalOverlaps(approved), Is.GreaterThanOrEqualTo(16));
    }

    [Test]
    public void CapsuleGeometry_NormalizesFiniteQuaternionBeforeBuildingCapsule()
    {
        MethodInfo method = FindCapsuleGeometryMethod();
        object[] arguments =
        {
            boidObject.GetComponent<Rigidbody>(), Vector3.zero, new Quaternion(0f, 0f, 0f, 2f), null
        };

        Assert.That((bool)method.Invoke(null, arguments), Is.True);
        Assert.That(GetStructProperty<Vector3>(arguments[3], "Point0").y, Is.EqualTo(0.5f).Within(0.0001f));
    }

    [Test]
    public void RequiredPolicy_RejectsTiltedGridTransform()
    {
        ConfigureRequiredRegion();
        gridObject.transform.rotation = Quaternion.Euler(15f, 0f, 0f);
        Component agent = boidObject.GetComponent(boidAgentType);

        object[] arguments = { null };
        bool configured = (bool)boidAgentType.GetMethod("TryGetRequiredHuntingGroundRegion")
            .Invoke(agent, arguments);

        Assert.That(configured, Is.False);
        Assert.That(arguments[0], Is.Null);
    }

    [Test]
    public void RequiredPolicy_AcceptsHorizontalGridWithoutRotation()
    {
        ConfigureRequiredRegion();
        Component agent = boidObject.GetComponent(boidAgentType);

        object[] arguments = { null };
        bool configured = (bool)boidAgentType.GetMethod("TryGetRequiredHuntingGroundRegion")
            .Invoke(agent, arguments);

        Assert.That(configured, Is.True);
        Assert.That(arguments[0], Is.Not.Null);
    }

    [Test]
    public void RequiredPolicy_RejectsSmallGridTilt()
    {
        ConfigureRequiredRegion();
        gridObject.transform.rotation = Quaternion.Euler(0.1f, 0f, 0f);
        Component agent = boidObject.GetComponent(boidAgentType);

        object[] arguments = { null };
        bool configured = (bool)boidAgentType.GetMethod("TryGetRequiredHuntingGroundRegion")
            .Invoke(agent, arguments);

        Assert.That(configured, Is.False);
        Assert.That(arguments[0], Is.Null);
    }

    [Test]
    public void RequiredPolicy_RejectsTiltNearFormerToleranceLimit()
    {
        ConfigureRequiredRegion();
        gridObject.transform.rotation = Quaternion.Euler(0.8f, 0f, 0f);
        Component agent = boidObject.GetComponent(boidAgentType);

        object[] arguments = { null };
        bool configured = (bool)boidAgentType.GetMethod("TryGetRequiredHuntingGroundRegion")
            .Invoke(agent, arguments);

        Assert.That(configured, Is.False);
        Assert.That(arguments[0], Is.Null);
    }

    [Test]
    public void RequiredPolicy_AcceptsNegligibleGridRotationNoise()
    {
        ConfigureRequiredRegion();
        gridObject.transform.rotation = Quaternion.Euler(0.00001f, 0f, 0f);
        Component agent = boidObject.GetComponent(boidAgentType);

        object[] arguments = { null };
        bool configured = (bool)boidAgentType.GetMethod("TryGetRequiredHuntingGroundRegion")
            .Invoke(agent, arguments);

        Assert.That(configured, Is.True);
        Assert.That(arguments[0], Is.Not.Null);
    }

    [Test]
    public void RequiredPolicy_AcceptsTranslatedYawedGridWithOrthogonalScale()
    {
        ConfigureRequiredRegion();
        gridObject.transform.SetPositionAndRotation(
            new Vector3(12f, 2f, -9f), Quaternion.Euler(0f, 37f, 0f));
        gridObject.transform.localScale = new Vector3(2f, 1f, 3f);
        Component agent = boidObject.GetComponent(boidAgentType);

        object[] arguments = { null };
        bool configured = (bool)boidAgentType.GetMethod("TryGetRequiredHuntingGroundRegion")
            .Invoke(agent, arguments);

        Assert.That(configured, Is.True);
        Assert.That(arguments[0], Is.SameAs(regionObject.GetComponent(typeof(HuntingGroundRegion))));
    }

    [Test]
    public void CapsuleGeometry_ReturnsWorldCapsuleAndConservativePivotFootprint()
    {
        CapsuleCollider capsule = boidObject.GetComponent<CapsuleCollider>();
        capsule.radius = 0.5f;
        capsule.height = 2f;
        capsule.center = new Vector3(1f, 0f, 0f);
        Rigidbody body = boidObject.GetComponent<Rigidbody>();
        Vector3 position = new Vector3(3f, 2f, 5f);
        Quaternion rotation = Quaternion.Euler(0f, 35f, 0f);
        MethodInfo method = FindCapsuleGeometryMethod();
        object[] arguments = { body, position, rotation, null };

        bool succeeded = (bool)method.Invoke(null, arguments);
        object worldCapsule = arguments[3];

        Assert.That(succeeded, Is.True);
        Vector3 expectedCenter = position + rotation * Vector3.right;
        Vector3 actualCenter = GetStructProperty<Vector3>(worldCapsule, "Center");
        Assert.That(actualCenter.x, Is.EqualTo(expectedCenter.x).Within(0.0001f));
        Assert.That(actualCenter.y, Is.EqualTo(expectedCenter.y).Within(0.0001f));
        Assert.That(actualCenter.z, Is.EqualTo(expectedCenter.z).Within(0.0001f));
        Assert.That(GetStructProperty<float>(worldCapsule, "Radius"), Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(GetStructProperty<float>(worldCapsule, "Height"), Is.EqualTo(2f).Within(0.0001f));
        Assert.That(GetStructProperty<float>(worldCapsule, "HorizontalFootprintRadius"),
            Is.EqualTo(1.5f).Within(0.0001f));
        Assert.That(GetStructProperty<Vector3>(worldCapsule, "Point0").y,
            Is.EqualTo(2.5f).Within(0.0001f));
        Assert.That(GetStructProperty<Vector3>(worldCapsule, "Point1").y,
            Is.EqualTo(1.5f).Within(0.0001f));
    }

    [Test]
    public void CapsuleGeometry_RejectsTiltedBodyPose()
    {
        MethodInfo method = FindCapsuleGeometryMethod();
        object[] arguments =
        {
            boidObject.GetComponent<Rigidbody>(), Vector3.zero, Quaternion.Euler(0.5f, 0f, 0f), null
        };

        Assert.That((bool)method.Invoke(null, arguments), Is.False);
    }

    [Test]
    public void CapsuleGeometry_ConservativelyIncludesNumericalTiltWithinTolerance()
    {
        MethodInfo method = FindCapsuleGeometryMethod();
        Quaternion rotation = Quaternion.Euler(0.05f, 0f, 0f);
        object[] arguments =
        {
            boidObject.GetComponent<Rigidbody>(), Vector3.zero, rotation, null
        };

        bool succeeded = (bool)method.Invoke(null, arguments);
        object worldCapsule = arguments[3];
        float expectedProjection = 0.5f * Mathf.Sin(0.05f * Mathf.Deg2Rad);

        Assert.That(succeeded, Is.True);
        Assert.That(GetStructProperty<float>(worldCapsule, "HorizontalFootprintRadius"),
            Is.EqualTo(0.5f + expectedProjection).Within(0.000001f));
        Assert.That(GetStructProperty<Vector3>(worldCapsule, "Point0").z,
            Is.EqualTo(expectedProjection).Within(0.000001f));
        Assert.That(GetStructProperty<Vector3>(worldCapsule, "Point1").z,
            Is.EqualTo(-expectedProjection).Within(0.000001f));
    }

    [Test]
    public void CapsuleGeometry_RejectsShearedTransform()
    {
        GameObject parent = new GameObject("Sheared B1 Test Parent");
        try
        {
            parent.transform.localScale = new Vector3(2f, 1f, 1f);
            boidObject.transform.SetParent(parent.transform, false);
            boidObject.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            MethodInfo method = FindCapsuleGeometryMethod();
            object[] arguments =
            {
                boidObject.GetComponent<Rigidbody>(), Vector3.zero,
                boidObject.transform.rotation, null
            };

            Assert.That((bool)method.Invoke(null, arguments), Is.False);
        }
        finally
        {
            boidObject.transform.SetParent(null, false);
            UnityEngine.Object.DestroyImmediate(parent);
        }
    }

    [Test]
    public void CapsuleGeometry_RejectsNonLocalYCollider()
    {
        boidObject.GetComponent<CapsuleCollider>().direction = 0;
        MethodInfo method = FindCapsuleGeometryMethod();
        object[] arguments =
        {
            boidObject.GetComponent<Rigidbody>(), Vector3.zero, Quaternion.identity, null
        };

        Assert.That((bool)method.Invoke(null, arguments), Is.False);
    }

    private void SetPolicy(Component agent, string value)
    {
        SetPrivateField(agent, "areaPolicy", Enum.Parse(areaPolicyType, value));
    }

    private void ConfigureRequiredRegion()
    {
        gridObject = new GameObject("B1 Policy Test Grid");
        NavigationGrid grid = gridObject.AddComponent<NavigationGrid>();
        Assert.That(grid.TryApplyGridSize(2, 2, 2f, true, out _), Is.True);
        Assert.That(grid.SetWalkable(0, 0, true), Is.True);
        Assert.That(grid.SetZone(0, 0, NavigationGridZone.HuntingGround), Is.True);
        regionObject = new GameObject("B1 Policy Test Region");
        Component region = regionObject.AddComponent(typeof(HuntingGroundRegion));
        SetPrivateField(region, "navigationGrid", grid);

        Component agent = boidObject.GetComponent(boidAgentType);
        Component containment = boidObject.GetComponent(containmentType);
        Component wander = boidObject.GetComponent(wanderType);
        SetPrivateField(agent, "body", boidObject.GetComponent<Rigidbody>());
        SetPrivateField(agent, "capsule", boidObject.GetComponent<CapsuleCollider>());
        SetPrivateField(agent, "containment", containment);
        SetPrivateField(agent, "wander", wander);
        SetPrivateField(containment, "owner", agent);
        SetPrivateField(containment, "huntingGroundRegion", region);
        SetPrivateField(wander, "owner", agent);
        SetPrivateField(wander, "huntingGroundRegion", region);
        SetPolicy(agent, "RequireHuntingGround");
    }

    private Component PreparePhysicalAgent()
    {
        Component agent = boidObject.GetComponent(boidAgentType);
        Rigidbody body = boidObject.GetComponent<Rigidbody>();
        CapsuleCollider capsule = boidObject.GetComponent<CapsuleCollider>();
        SetPrivateField(agent, "body", body);
        SetPrivateField(agent, "capsule", capsule);
        SetPrivateField(agent, "environmentLayers", (LayerMask)(1 << 12));
        SetPolicy(agent, "RequireHuntingGround");
        boidObject.transform.position = Vector3.zero;
        body.position = Vector3.zero;
        body.rotation = Quaternion.identity;
        Physics.SyncTransforms();
        return agent;
    }

    private bool TryRecertify(Component agent, Vector3 candidate, out Vector3 approved)
    {
        MethodInfo method = agent.GetType().GetMethod(
            "TryRecertifyPhysicalMovement",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        object[] arguments = { Vector3.zero, candidate, null };
        bool accepted = (bool)method.Invoke(agent, arguments);
        approved = (Vector3)arguments[2];
        return accepted;
    }

    private static bool ResolveEnvironmentOverlaps(Component agent, ref Vector3 position)
    {
        MethodInfo method = agent.GetType().GetMethod(
            "ResolveEnvironmentOverlaps",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        object[] arguments = { position };
        bool resolved = (bool)method.Invoke(agent, arguments);
        position = (Vector3)arguments[0];
        return resolved;
    }

    private static GameObject CreateSaturationObstacles()
    {
        GameObject root = new GameObject("B4 Saturation Test Obstacles");
        for (int index = 0; index < 16; index++)
        {
            GameObject obstacle = new GameObject($"Saturation Obstacle {index}");
            obstacle.layer = 12;
            obstacle.transform.SetParent(root.transform, false);
            BoxCollider collider = obstacle.AddComponent<BoxCollider>();
            collider.size = new Vector3(1.2f, 2f, 1.2f);
        }

        Physics.SyncTransforms();
        return root;
    }

    private static int CountPhysicalOverlaps(Vector3 position)
    {
        Collider[] overlaps = new Collider[32];
        return Physics.OverlapCapsuleNonAlloc(
            position + Vector3.up * 0.5f,
            position - Vector3.up * 0.5f,
            0.5f,
            overlaps,
            1 << 12,
            QueryTriggerInteraction.Ignore);
    }

    private static GameObject CreateObstacle(Vector3 position, Vector3 size)
    {
        GameObject obstacle = new GameObject("B4 Physical Test Obstacle");
        obstacle.layer = 12;
        obstacle.transform.position = position;
        BoxCollider collider = obstacle.AddComponent<BoxCollider>();
        collider.size = size;
        Physics.SyncTransforms();
        return obstacle;
    }

    private static MethodInfo FindCapsuleGeometryMethod()
    {
        Type geometryType = FindProjectType("BoidCapsuleGeometry");
        MethodInfo method = geometryType.GetMethod("TryGetWorldCapsule", BindingFlags.Public | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);
        return method;
    }

    private static T GetStructProperty<T>(object target, string propertyName) =>
        (T)target.GetType().GetProperty(propertyName).GetValue(target);

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

    private static void InvokePrivate(object target, string methodName) =>
        target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
}
