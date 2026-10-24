using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using CaughtGame.Navigation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class HuntingGroundRespawnTests
{
    private GameObject gridObject;
    private NavigationGrid grid;
    private GameObject regionObject;
    private HuntingGroundRegion region;
    private GameObject areaObject;
    private Component area;
    private GameObject floorObject;
    private GameObject generatedRoot;
    private readonly List<GameObject> obstacles = new List<GameObject>();
    private GameObject boidObject;
    private CapsuleCollider capsule;
    private Type areaType;
    private Type respawnType;
    private Type lifeType;
    private Type agentType;
    private Type containmentType;

    [SetUp]
    public void SetUp()
    {
        gridObject = new GameObject("Respawn Grid Test");
        grid = gridObject.AddComponent<NavigationGrid>();
        Assert.That(grid.TryApplyGridSize(3, 3, 4f, true, out _), Is.True);

        regionObject = new GameObject("Respawn Region Test");
        region = regionObject.AddComponent<HuntingGroundRegion>();
        SetPrivateField(region, "navigationGrid", grid);
        SetCell(1, 1, NavigationGridZone.HuntingGround, true);

        areaType = FindProjectType("BoidRespawnArea");
        respawnType = FindProjectType("BoidRespawn");
        lifeType = FindProjectType("BoidLife");
        agentType = FindProjectType("BoidAgent");
        containmentType = FindProjectType("PlayAreaContainmentBehaviour");
        areaObject = new GameObject("Respawn Area Test");
        areaObject.AddComponent<BoxCollider>().isTrigger = true;
        area = areaObject.AddComponent(areaType);
        SetPrivateField(area, "huntingGroundRegion", region);
        SetPrivateField(area, "maximumPlacementAttempts", 4);
        SetPrivateField(area, "groundLayerMask", (LayerMask)(1 << 8));
        SetPrivateField(area, "groundOffset", 0.05f);

        boidObject = new GameObject("Respawn Boid Test");
        boidObject.transform.position = new Vector3(6f, 0f, 6f);
        Rigidbody body = boidObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        capsule = boidObject.AddComponent<CapsuleCollider>();
        capsule.radius = 0.25f;
        capsule.height = 1.8f;
        Component containment = boidObject.AddComponent(containmentType);
        Component agent = boidObject.GetComponent(agentType);
        SetPrivateField(agent, "areaPolicy", Enum.Parse(FindProjectType("BoidAreaPolicy"), "RequireHuntingGround"));
        SetPrivateField(agent, "body", body);
        SetPrivateField(agent, "capsule", capsule);
        SetPrivateField(agent, "containment", containment);
        SetPrivateField(containment, "owner", agent);
        SetPrivateField(containment, "huntingGroundRegion", region);
        MakeGroundAt(grid.GridToWorldPosition(1, 1), false);
        Physics.SyncTransforms();
    }

    [TearDown]
    public void TearDown()
    {
        DestroyIfPresent(boidObject);
        DestroyIfPresent(floorObject);
        DestroyIfPresent(generatedRoot);
        for (int i = 0; i < obstacles.Count; i++) DestroyIfPresent(obstacles[i]);
        obstacles.Clear();
        DestroyIfPresent(areaObject);
        DestroyIfPresent(regionObject);
        DestroyIfPresent(gridObject);
        Physics.SyncTransforms();
    }

    [Test]
    public void RespawnArea_SelectsWalkableHuntingGroundCell()
    {
        Assert.That(TrySelect(out Vector3 position), Is.True);
        Assert.That(region.ContainsWorldPosition(position), Is.True);
        Assert.That(HuntingGroundFootprint.Contains(region, position, 0.25f, 0.1f), Is.True);
    }

    [Test]
    public void RespawnArea_RejectsBlockedHuntingGroundCells()
    {
        SetCell(1, 1, NavigationGridZone.HuntingGround, false);
        Assert.That(TrySelect(out _), Is.False);
    }

    [TestCase(NavigationGridZone.SafeZone)]
    [TestCase(NavigationGridZone.Spawn)]
    [TestCase(NavigationGridZone.Exit)]
    [TestCase(NavigationGridZone.Normal)]
    public void RespawnArea_RejectsNonHuntingZones(NavigationGridZone zone)
    {
        SetCell(1, 1, zone, true);
        Assert.That(TrySelect(out _), Is.False);
    }

    [Test]
    public void RespawnArea_EmptyRegionReturnsFailure()
    {
        SetCell(1, 1, NavigationGridZone.Normal, false);
        Assert.That(TrySelect(out _), Is.False);
    }

    [Test]
    public void RespawnArea_RegionModeRejectsAnInvalidAgentCapsule()
    {
        capsule.direction = 0;
        Assert.That(TrySelect(out _), Is.False);
    }

    [Test]
    public void RespawnArea_SupportsNonRectangularRegionWithoutUsingOtherZones()
    {
        SetCell(0, 1, NavigationGridZone.HuntingGround, true);
        SetCell(1, 0, NavigationGridZone.HuntingGround, true);
        SetCell(1, 2, NavigationGridZone.HuntingGround, true);
        SetCell(2, 1, NavigationGridZone.HuntingGround, true);

        for (int i = 0; i < 8; i++)
        {
            Assert.That(TrySelect(out Vector3 position), Is.True);
            Assert.That(region.ContainsWorldPosition(position), Is.True);
        }
    }

    [Test]
    public void RespawnArea_RespectsTranslatedAndRotatedGrid()
    {
        grid.transform.SetPositionAndRotation(new Vector3(20f, 3f, -12f), Quaternion.Euler(0f, 37f, 0f));
        floorObject.transform.position = grid.GridToWorldPosition(1, 1) + Vector3.down * 0.5f;
        Physics.SyncTransforms();

        Assert.That(TrySelect(out Vector3 position), Is.True);
        Assert.That(region.ContainsWorldPosition(position), Is.True);
    }

    [Test]
    public void RespawnArea_UsesCapsuleRadiusAndSafetyMargin()
    {
        SetPrivateField(area, "capsuleClearanceMargin", 2.1f);
        SetCell(1, 2, NavigationGridZone.Normal, false);
        Assert.That(TrySelect(out _), Is.False);
    }

    [Test]
    public void RespawnArea_UsesCapsuleHeightToPlaceFeetAboveGround()
    {
        capsule.height = 2.4f;
        Assert.That(TrySelect(out Vector3 position), Is.True);
        Assert.That(position.y, Is.EqualTo(1.25f).Within(0.001f));
    }

    [Test]
    public void RespawnArea_AccountsForCapsuleTransformScale()
    {
        boidObject.transform.localScale = new Vector3(2f, 2f, 2f);
        capsule.radius = 0.2f;
        capsule.height = 1f;
        capsule.center = Vector3.zero;
        SetPrivateField(area, "capsuleClearanceMargin", 0.05f);

        Assert.That(TrySelect(out Vector3 position), Is.True);
        Assert.That(position.y, Is.EqualTo(1.05f).Within(0.001f));
    }

    [Test]
    public void RespawnArea_RejectsCandidateTooCloseToWall()
    {
        CreateWall(grid.GridToWorldPosition(1, 1) + Vector3.right * 0.3f, new Vector3(0.2f, 2f, 2f));
        Assert.That(TrySelect(out _), Is.False);
    }

    [Test]
    public void RespawnArea_RejectsPhysicalObstacleOverlap()
    {
        CreateWall(grid.GridToWorldPosition(1, 1), new Vector3(1f, 2f, 1f));
        Assert.That(TrySelect(out _), Is.False);
    }

    [Test]
    public void RespawnArea_AcceptsGroundLayerSurface()
    {
        Assert.That(TrySelect(out _), Is.True);
    }

    [Test]
    public void RespawnArea_IgnoresTriggerVolumesDuringGroundAndClearanceChecks()
    {
        GameObject trigger = new GameObject("Respawn Trigger Test");
        obstacles.Add(trigger);
        trigger.transform.position = grid.GridToWorldPosition(1, 1);
        BoxCollider collider = trigger.AddComponent<BoxCollider>();
        collider.size = Vector3.one * 2f;
        collider.isTrigger = true;
        Physics.SyncTransforms();

        Assert.That(TrySelect(out _), Is.True);
    }

    [Test]
    public void RespawnArea_RejectsMissingGround()
    {
        UnityEngine.Object.DestroyImmediate(floorObject);
        floorObject = null;
        Assert.That(TrySelect(out _), Is.False);
    }

    [Test]
    public void RespawnArea_RejectsFootprintAtHuntingGroundCorner()
    {
        SetCell(1, 2, NavigationGridZone.Normal, false);
        Vector3 edge = grid.transform.TransformPoint(new Vector3(7.95f, 0f, 6f));
        Assert.That(HuntingGroundFootprint.Contains(region, edge, 0.25f, 0.1f), Is.False);
    }

    [Test]
    public void Respawn_ValidPlacementRestoresLifeAndMovesRigidbody()
    {
        Component respawn = CreateRespawnComponent(out Component life, out Rigidbody body);
        SetLifeState(life, "Respawning");

        bool succeeded = InvokeTryRespawnNow(respawn);

        Assert.That(succeeded, Is.True);
        Assert.That((bool)lifeType.GetProperty("IsAlive").GetValue(life), Is.True);
        Assert.That(region.ContainsWorldPosition(body.position), Is.True);
    }

    [Test]
    public void Respawn_FailureDoesNotReactivateBoid()
    {
        Component respawn = CreateRespawnComponent(out Component life, out _);
        SetCell(1, 1, NavigationGridZone.SafeZone, true);
        SetLifeState(life, "Respawning");

        bool succeeded = InvokeTryRespawnNow(respawn);

        Assert.That(succeeded, Is.False);
        Assert.That(lifeType.GetProperty("IsAlive").GetValue(life).Equals(true), Is.False);
    }

    [Test]
    public void Respawn_RetryCoroutineIsBoundedAfterRepeatedFailure()
    {
        Component respawn = CreateRespawnComponent(out Component life, out _);
        SetCell(1, 1, NavigationGridZone.SafeZone, true);
        SetLifeState(life, "Respawning");
        SetPrivateField(respawn, "maximumRespawnRetries", 2);

        IEnumerator routine = (IEnumerator)respawnType.GetMethod("RespawnAfterDelay", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(respawn, null);
        int yields = 0;
        while (routine.MoveNext()) yields++;

        Assert.That(yields, Is.EqualTo(3));
        Assert.That(lifeType.GetProperty("IsAlive").GetValue(life).Equals(true), Is.False);
    }

    [Test]
    public void RespawnWithoutRegion_PreservesRectangularLegacyPlacement()
    {
        SetPrivateField(area, "huntingGroundRegion", null);
        Component agent = boidObject.GetComponent(agentType);
        SetPrivateField(agent, "areaPolicy", Enum.Parse(FindProjectType("BoidAreaPolicy"), "LegacyRectangle"));
        Assert.That(TrySelect(out Vector3 position), Is.True);
        Assert.That(position.y, Is.EqualTo(0.05f).Within(0.001f));
    }

    [Test]
    public void RespawnArea_OneArgumentOverloadPreservesLegacyPlacement()
    {
        SetPrivateField(area, "huntingGroundRegion", region);
        Assert.That((bool)areaType.GetMethod("TryGetRandomPosition", new[] { typeof(Vector3).MakeByRefType() })
            .Invoke(area, new object[] { default(Vector3) }), Is.True);
    }

    [Test]
    public void RespawnArea_AcceptsMarkedGrayboxGroundOnDefaultLayer()
    {
        UnityEngine.Object.DestroyImmediate(floorObject);
        floorObject = null;
        MakeGroundAt(grid.GridToWorldPosition(1, 1), true);

        Assert.That(TrySelect(out _), Is.True);
    }

    private bool TrySelect(out Vector3 position)
    {
        object[] arguments = { capsule, default(Vector3) };
        bool result = (bool)areaType.GetMethod("TryGetRandomPosition", new[] { typeof(CapsuleCollider), typeof(Vector3).MakeByRefType() })
            .Invoke(area, arguments);
        position = (Vector3)arguments[1];
        return result;
    }

    private Component CreateRespawnComponent(out Component life, out Rigidbody body)
    {
        life = (Component)boidObject.AddComponent(lifeType);
        body = boidObject.GetComponent<Rigidbody>();
        Component respawn = boidObject.AddComponent(respawnType);
        SetPrivateField(respawn, "life", life);
        SetPrivateField(respawn, "body", body);
        SetPrivateField(respawn, "initialPosition", new Vector3(-100f, 0f, -100f));
        SetPrivateField(respawn, "respawnArea", area);
        return respawn;
    }

    private bool InvokeTryRespawnNow(Component respawn) =>
        (bool)respawnType.GetMethod("TryRespawnNow", BindingFlags.Instance | BindingFlags.Public).Invoke(respawn, null);

    private void SetLifeState(Component life, string stateName)
    {
        Type stateType = FindProjectType("BoidLifeState");
        SetPrivateField(life, "state", Enum.Parse(stateType, stateName));
    }

    private void MakeGroundAt(Vector3 cellCenter, bool generatedDefaultGround)
    {
        floorObject = new GameObject("Test Ground");
        floorObject.transform.position = cellCenter + Vector3.down * 0.5f;
        BoxCollider floor = floorObject.AddComponent<BoxCollider>();
        floor.size = new Vector3(20f, 1f, 20f);
        if (!generatedDefaultGround)
        {
            floorObject.layer = 8;
            return;
        }

        generatedRoot = new GameObject("Generated Geometry Test");
        generatedRoot.transform.position = floorObject.transform.position;
        GeneratedGeometryMarker owner = generatedRoot.AddComponent<GeneratedGeometryMarker>();
        owner.Initialize(grid, 1);
        GeneratedGeometryPartMarker part = floorObject.AddComponent<GeneratedGeometryPartMarker>();
        part.Initialize(owner);
        floorObject.name = "Ground";
    }

    private void CreateWall(Vector3 position, Vector3 size)
    {
        GameObject wall = new GameObject("Respawn Obstacle Test");
        obstacles.Add(wall);
        wall.transform.position = position;
        wall.AddComponent<BoxCollider>().size = size;
        Physics.SyncTransforms();
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

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Missing field " + fieldName + ".");
        field.SetValue(target, value);
    }

    private static void DestroyIfPresent(GameObject target)
    {
        if (target != null) UnityEngine.Object.DestroyImmediate(target);
    }
}
