using System;
using System.Collections.Generic;
using System.Reflection;
using CaughtGame.Navigation;
using NUnit.Framework;
using UnityEngine;

public sealed class HunterFleeDetectionTests
{
    private readonly List<GameObject> objects = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject instance in objects)
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }

        objects.Clear();
    }

    [Test]
    public void DetectsClosestWolfWithinRangeAndLineOfSight()
    {
        Component perception = CreatePerception(5f);
        Component fartherWolf = CreateWolf(new Vector3(4f, 0f, 0f));
        Component closerWolf = CreateWolf(new Vector3(2f, 0f, 0f));

        Physics.SyncTransforms();
        Invoke(perception, "ForceRefresh");

        Assert.That(GetProperty<object>(perception, "ClosestWolf"), Is.SameAs(closerWolf));
        Assert.That(GetProperty<object>(perception, "ClosestWolf"), Is.Not.SameAs(fartherWolf));
    }

    [Test]
    public void TickBeforeRefreshIntervalKeepsCachedWolfUntilExplicitRepositionRefresh()
    {
        bool previousAutoSyncTransforms = Physics.autoSyncTransforms;
        Physics.autoSyncTransforms = false;
        try
        {
            Component perception = CreatePerception(5f);
            Component wolf = CreateWolf(new Vector3(2f, 0f, 0f));
            Invoke(perception, "ForceRefresh");
            Assert.That(GetProperty<object>(perception, "ClosestWolf"), Is.SameAs(wolf));

            wolf.transform.position = new Vector3(8f, 0f, 0f);
            Invoke(perception, "Tick", 0f);

            Assert.That(GetProperty<object>(perception, "ClosestWolf"), Is.SameAs(wolf),
                "Tick(0) must preserve the cached result until the normal interval expires.");

            Invoke(perception, "ForceRefresh");

            Assert.That(GetProperty<object>(perception, "ClosestWolf"), Is.Null,
                "An explicit refresh after repositioning must synchronize physics and discard stale range data.");
        }
        finally
        {
            Physics.autoSyncTransforms = previousAutoSyncTransforms;
            Physics.SyncTransforms();
        }
    }

    [Test]
    public void ExplicitRepositionRefreshDetectsNewLineOfSightBlockerBeforeIntervalExpires()
    {
        bool previousAutoSyncTransforms = Physics.autoSyncTransforms;
        Physics.autoSyncTransforms = false;
        try
        {
            Component perception = CreatePerception(5f);
            Component wolf = CreateWolf(new Vector3(4f, 0f, 0f));
            Invoke(perception, "ForceRefresh");
            Assert.That(GetProperty<object>(perception, "ClosestWolf"), Is.SameAs(wolf));

            CreateObstacleBetweenHunterAndWolf();
            Invoke(perception, "Tick", 0f);

            Assert.That(GetProperty<object>(perception, "ClosestWolf"), Is.SameAs(wolf));

            Invoke(perception, "ForceRefresh");

            Assert.That(GetProperty<object>(perception, "ClosestWolf"), Is.Null,
                "The explicit reposition refresh must synchronize the new blocker before evaluating LOS.");
        }
        finally
        {
            Physics.autoSyncTransforms = previousAutoSyncTransforms;
            Physics.SyncTransforms();
        }
    }

    [Test]
    public void WolfOutsideDetectionRadiusIsNotDetected()
    {
        Component perception = CreatePerception(3f);
        CreateWolf(new Vector3(4f, 0f, 0f));

        Physics.SyncTransforms();
        Invoke(perception, "ForceRefresh");

        Assert.That(GetProperty<object>(perception, "ClosestWolf"), Is.Null);
    }

    [Test]
    public void ObstacleBlocksWolfLineOfSight()
    {
        Component perception = CreatePerception(5f);
        CreateWolf(new Vector3(4f, 0f, 0f));
        GameObject obstacle = CreateObject(
            "HunterFleeDetectionTests.Obstacle", new Vector3(2f, 1f, 0f));
        obstacle.layer = 12;
        obstacle.AddComponent<BoxCollider>().size = new Vector3(1f, 3f, 1f);

        Physics.SyncTransforms();
        Invoke(perception, "ForceRefresh");

        Assert.That(GetProperty<object>(perception, "ClosestWolf"), Is.Null);
    }

    [Test]
    public void BlockedWolfDoesNotHideDifferentVisibleWolf()
    {
        Component perception = CreatePerception(6f);
        CreateWolf(new Vector3(4f, 0f, 0f));
        Component visibleWolf = CreateWolf(new Vector3(0f, 0f, 4f));
        GameObject obstacle = CreateObject(
            "HunterFleeDetectionTests.Obstacle", new Vector3(2f, 1f, 0f));
        obstacle.layer = 12;
        obstacle.AddComponent<BoxCollider>().size = new Vector3(1f, 3f, 1f);

        Physics.SyncTransforms();
        Invoke(perception, "ForceRefresh");

        Assert.That(GetProperty<object>(perception, "ClosestWolf"), Is.SameAs(visibleWolf));
    }

    [Test]
    public void WolfDetectionDoesNotReplaceBoidDetection()
    {
        Component perception = CreatePerception(5f);
        CreateWolf(new Vector3(2f, 0f, 0f));
        Component boid = CreateLivingBoid(new Vector3(0f, 0f, 2f));

        Physics.SyncTransforms();
        Invoke(perception, "ForceRefresh");

        Assert.That(GetProperty<object>(perception, "ClosestWolf"), Is.Not.Null);
        Assert.That(GetProperty<object>(perception, "ClosestLivingBoid"), Is.SameAs(boid));
    }

    [Test]
    public void DetectedWolfInterruptsAttackAndEntersFleeOnlyOnce()
    {
        Component hunter = CreateHunterAgent();
        CreateWolf(new Vector3(2f, 0f, 0f));
        object machine = GetPrivateField(hunter, "stateMachine");
        Type policeState = FindProjectType("PoliceState");
        Assert.That((bool)Invoke(machine, "ChangeState", Enum.Parse(policeState, "Attack")), Is.True);

        Physics.SyncTransforms();
        Component perception = (Component)GetProperty<object>(hunter, "Perception");
        Invoke(perception, "ForceRefresh");
        InvokePrivate(hunter, "Update");
        object fleeState = GetProperty<object>(machine, "CurrentState");
        InvokePrivate(hunter, "Update");

        Assert.That(GetProperty<Enum>(hunter, "CurrentState").ToString(), Is.EqualTo("Flee"));
        Assert.That(GetProperty<object>(machine, "CurrentState"), Is.SameAs(fleeState));
    }

    [Test]
    public void DetectedWolfDoesNotInterruptDeadState()
    {
        Component hunter = CreateHunterAgent();
        CreateWolf(new Vector3(2f, 0f, 0f));
        object machine = GetPrivateField(hunter, "stateMachine");
        Type policeState = FindProjectType("PoliceState");

        object patrolState = GetProperty<object>(machine, "CurrentState");
        Assert.That((bool)Invoke(machine, "ChangeState", Enum.Parse(policeState, "Attack")), Is.True);
        Invoke(machine, "RegisterState", Enum.Parse(policeState, "Dead"), patrolState);
        Assert.That((bool)Invoke(machine, "ChangeState", Enum.Parse(policeState, "Dead")), Is.True);

        Physics.SyncTransforms();
        Component perception = (Component)GetProperty<object>(hunter, "Perception");
        Invoke(perception, "ForceRefresh");
        InvokePrivate(hunter, "Update");

        Assert.That(GetProperty<Enum>(hunter, "CurrentState").ToString(), Is.EqualTo("Dead"));
    }

    [Test]
    public void WallPreventsSearchFromEnteringFlee()
    {
        Component hunter = CreateHunterAgent(true);
        CreateWolf(new Vector3(4f, 0f, 0f));
        CreateObstacleBetweenHunterAndWolf();

        Physics.SyncTransforms();
        Component perception = (Component)GetProperty<object>(hunter, "Perception");
        Invoke(perception, "ForceRefresh");
        InvokePrivate(hunter, "Update");

        Assert.That(GetProperty<object>(perception, "ClosestWolf"), Is.Null);
        Assert.That(GetProperty<bool>(perception, "HasRecentWolfThreat"), Is.False);
        Assert.That(GetProperty<Enum>(hunter, "CurrentState").ToString(), Is.EqualTo("Search"));
    }

    [Test]
    public void BriefLineOfSightLossKeepsCurrentFleeStateWithoutVisibleWolf()
    {
        Component hunter = CreateHunterAgent(true, 0.5f);
        CreateWolf(new Vector3(4f, 0f, 0f));
        Component perception = (Component)GetProperty<object>(hunter, "Perception");

        Physics.SyncTransforms();
        Invoke(perception, "ForceRefresh");
        InvokePrivate(hunter, "Update");
        CreateObstacleBetweenHunterAndWolf();
        Physics.SyncTransforms();
        Invoke(perception, "ForceRefresh");
        Invoke(perception, "Tick", 0.1f);
        InvokePrivate(hunter, "Update");

        Assert.That(GetProperty<object>(perception, "ClosestWolf"), Is.Null);
        Assert.That(GetProperty<bool>(perception, "HasRecentWolfThreat"), Is.True);
        Assert.That(GetProperty<Enum>(hunter, "CurrentState").ToString(), Is.EqualTo("Flee"));
    }

    [Test]
    public void SustainedLineOfSightLossDoesNotCancelActiveFlee()
    {
        Component hunter = CreateHunterAgent(true, 0.5f);
        CreateWolf(new Vector3(4f, 0f, 0f));
        Component perception = (Component)GetProperty<object>(hunter, "Perception");

        Physics.SyncTransforms();
        Invoke(perception, "ForceRefresh");
        InvokePrivate(hunter, "Update");
        CreateObstacleBetweenHunterAndWolf();
        Physics.SyncTransforms();
        Invoke(perception, "ForceRefresh");
        Invoke(perception, "Tick", 0.6f);
        InvokePrivate(hunter, "Update");

        Assert.That(GetProperty<object>(perception, "ClosestWolf"), Is.Null);
        Assert.That(GetProperty<bool>(perception, "HasRecentWolfThreat"), Is.False);
        Assert.That(GetProperty<Enum>(hunter, "CurrentState").ToString(), Is.EqualTo("Flee"));
    }

    [Test]
    public void RecoveredLineOfSightRefreshesFleeGracePeriod()
    {
        Component hunter = CreateHunterAgent(true, 0.5f);
        CreateWolf(new Vector3(4f, 0f, 0f));
        Component perception = (Component)GetProperty<object>(hunter, "Perception");

        Physics.SyncTransforms();
        Invoke(perception, "ForceRefresh");
        InvokePrivate(hunter, "Update");
        GameObject obstacle = CreateObstacleBetweenHunterAndWolf();
        Physics.SyncTransforms();
        Invoke(perception, "ForceRefresh");
        Invoke(perception, "Tick", 0.3f);

        obstacle.SetActive(false);
        Physics.SyncTransforms();
        Invoke(perception, "ForceRefresh");
        Invoke(perception, "Tick", 0.3f);
        InvokePrivate(hunter, "Update");

        Assert.That(GetProperty<object>(perception, "ClosestWolf"), Is.Not.Null);
        Assert.That(GetProperty<bool>(perception, "HasRecentWolfThreat"), Is.True);
        Assert.That(GetProperty<float>(perception, "WolfLossGraceRemaining"), Is.EqualTo(0.5f));
        Assert.That(GetProperty<Enum>(hunter, "CurrentState").ToString(), Is.EqualTo("Flee"));
    }

    [Test]
    public void DisabledLossGraceDoesNotCancelActiveFlee()
    {
        Component hunter = CreateHunterAgent(true, 0f);
        CreateWolf(new Vector3(4f, 0f, 0f));
        Component perception = (Component)GetProperty<object>(hunter, "Perception");

        Physics.SyncTransforms();
        Invoke(perception, "ForceRefresh");
        InvokePrivate(hunter, "Update");
        Assert.That(GetProperty<Enum>(hunter, "CurrentState").ToString(), Is.EqualTo("Flee"));

        CreateObstacleBetweenHunterAndWolf();
        Physics.SyncTransforms();
        Invoke(perception, "ForceRefresh");
        InvokePrivate(hunter, "Update");

        Assert.That(GetProperty<object>(perception, "ClosestWolf"), Is.Null);
        Assert.That(GetProperty<Enum>(hunter, "CurrentState").ToString(), Is.EqualTo("Flee"));
    }

    [Test]
    public void SearchToFleeCancelsPreviousRouteAndChoosesLowestCostSafeZone()
    {
        NavigationGrid grid = CreateGrid();
        SetSafeZone(grid, 0, 4, "SafeZone_A");
        SetSafeZone(grid, 2, 2, "SafeZone_B");
        Assert.That(grid.SetWalkable(2, 1, false), Is.True);
        Assert.That(grid.SetWalkable(1, 2, false), Is.True);
        Assert.That(grid.SetWalkable(3, 2, false), Is.True);
        Component hunter = CreateHunterAgent(true, 0.3f, grid, grid.GridToWorldPosition(2, 0));

        ulong searchRequestId = (ulong)Invoke(
            hunter, "RequestNavigation", grid.GridToWorldPosition(4, 4), 0.25f, 1f, null);
        ChangeState(hunter, "Flee");
        object request = GetActiveNavigationRequest(hunter);

        Assert.That(GetProperty<ulong>(request, "Id"), Is.Not.EqualTo(searchRequestId));
        Assert.That(GetProperty<Vector3>(request, "Destination"),
            Is.EqualTo(grid.GridToWorldPosition(0, 4)));
    }

    [Test]
    public void FleeKeepsOneRequestAfterLosingLineOfSight()
    {
        NavigationGrid grid = CreateGrid();
        SetSafeZone(grid, 2, 4, "SafeZone_A");
        Component hunter = CreateHunterAgent(true, 0f, grid, grid.GridToWorldPosition(2, 0));
        ChangeState(hunter, "Flee");
        ulong requestId = GetProperty<ulong>(GetActiveNavigationRequest(hunter), "Id");

        InvokePrivate(hunter, "Update");
        int calculationCount = GetProperty<int>(
            GetProperty<object>(hunter, "NavigationController"), "PathCalculationCount");
        InvokePrivate(hunter, "Update");

        Assert.That(GetProperty<Enum>(hunter, "CurrentState").ToString(), Is.EqualTo("Flee"));
        Assert.That(GetProperty<ulong>(GetActiveNavigationRequest(hunter), "Id"), Is.EqualTo(requestId));
        Assert.That(GetProperty<int>(
            GetProperty<object>(hunter, "NavigationController"), "PathCalculationCount"),
            Is.EqualTo(calculationCount));
    }

    [Test]
    public void FleeControllerExecutesExactThreatScoredPath()
    {
        NavigationGrid grid = CreateGrid();
        SetSafeZone(grid, 2, 4, "SafeZone_A");
        Component hunter = CreateHunterAgent(true, 0f, grid, grid.GridToWorldPosition(2, 0));
        CreateWolf(hunter.transform.position + Vector3.right * 2f);
        Component perception = (Component)GetProperty<object>(hunter, "Perception");
        Physics.SyncTransforms();
        Invoke(perception, "ForceRefresh");

        ChangeState(hunter, "Flee");
        object request = GetActiveNavigationRequest(hunter);
        object plannedPath = GetProperty<object>(request, "PlannedPath");
        InvokePrivate(hunter, "Update");
        object controller = GetProperty<object>(hunter, "NavigationController");

        Assert.That(plannedPath, Is.Not.Null);
        Assert.That(GetProperty<object>(controller, "CurrentPath"), Is.SameAs(plannedPath));
        Assert.That(GetProperty<int>(controller, "PathCalculationCount"), Is.EqualTo(1));
    }

    [Test]
    public void StableThreatDoesNotReplaceFleeRequestOrReplanEveryFrame()
    {
        NavigationGrid grid = CreateGrid();
        SetSafeZone(grid, 0, 4, "SafeZone_A");
        SetSafeZone(grid, 4, 4, "SafeZone_B");
        Component hunter = CreateHunterAgent(true, 0f, grid, grid.GridToWorldPosition(2, 0));
        CreateWolf(hunter.transform.position + Vector3.right * 2f);
        Component perception = (Component)GetProperty<object>(hunter, "Perception");
        Physics.SyncTransforms();
        Invoke(perception, "ForceRefresh");
        ChangeState(hunter, "Flee");
        ulong requestId = GetProperty<ulong>(GetActiveNavigationRequest(hunter), "Id");
        object fleeState = GetProperty<object>(GetPrivateField(hunter, "stateMachine"),
            "CurrentState");
        InvokePrivate(hunter, "Update");
        object controller = GetProperty<object>(hunter, "NavigationController");
        int calculationCount = GetProperty<int>(controller, "PathCalculationCount");

        InvokePrivate(fleeState, "Tick", 1f);
        InvokePrivate(fleeState, "Tick", 1f);

        Assert.That(GetProperty<ulong>(GetActiveNavigationRequest(hunter), "Id"),
            Is.EqualTo(requestId));
        Assert.That(GetProperty<int>(controller, "PathCalculationCount"),
            Is.EqualTo(calculationCount));
    }

    [TestCase("SafeZone_East", "SafeZone_West")]
    [TestCase("SafeZone_A", "SafeZone_A")]
    public void SignificantThreatMovementReplansAfterCooldownWithoutOscillatingBetweenTiedDestinations(
        string eastZone, string westZone)
    {
        NavigationGrid grid = CreateGrid();
        SetSafeZone(grid, 2, 4, eastZone);
        SetSafeZone(grid, 2, 0, westZone);
        Vector3 hunterStart = grid.GridToWorldPosition(2, 2);
        Component hunter = CreateHunterAgent(true, 0f, grid, hunterStart);
        Vector3 eastRouteThreat = Vector3.Lerp(
            hunterStart, grid.GridToWorldPosition(2, 3), 0.5f);
        Vector3 westRouteThreat = Vector3.Lerp(
            hunterStart, grid.GridToWorldPosition(2, 1), 0.5f);
        Component wolf = CreateWolf(eastRouteThreat);
        Component perception = (Component)GetProperty<object>(hunter, "Perception");
        SetPrivateField(perception, "<ClosestWolf>k__BackingField", wolf);
        ChangeState(hunter, "Flee");
        object initialRequest = GetActiveNavigationRequest(hunter);
        ulong initialRequestId = GetProperty<ulong>(initialRequest, "Id");
        Assert.That(GetProperty<Vector3>(initialRequest, "Destination"),
            Is.EqualTo(grid.GridToWorldPosition(2, 0)));
        object fleeState = GetProperty<object>(GetPrivateField(hunter, "stateMachine"),
            "CurrentState");

        wolf.transform.position = westRouteThreat;
        InvokePrivate(fleeState, "Tick", 0.5f);
        Assert.That(GetProperty<ulong>(GetActiveNavigationRequest(hunter), "Id"),
            Is.EqualTo(initialRequestId));

        InvokePrivate(fleeState, "Tick", 0.3f);
        object replacementRequest = GetActiveNavigationRequest(hunter);

        Assert.That(GetProperty<ulong>(replacementRequest, "Id"),
            Is.Not.EqualTo(initialRequestId));
        Assert.That(GetProperty<Vector3>(replacementRequest, "Destination"),
            Is.EqualTo(grid.GridToWorldPosition(2, 0)));
        wolf.transform.position = eastRouteThreat;
        InvokePrivate(fleeState, "Tick", 1f);
        Assert.That(GetProperty<Vector3>(GetActiveNavigationRequest(hunter), "Destination"),
            Is.EqualTo(grid.GridToWorldPosition(2, 0)));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void FleeRouteDiagnosticsOnlyEmitsDecisionsAndDoesNotChangeRequests(bool trace)
    {
        NavigationGrid grid = CreateGrid();
        SetSafeZone(grid, 2, 4, "East");
        SetSafeZone(grid, 2, 0, "West");
        Vector3 start = grid.GridToWorldPosition(2, 2);
        Component hunter = CreateHunterAgent(true, 0f, grid, start);
        if (trace) SetPrivateField(hunter, "logFleeRouteDecisions", true);
        Component wolf = CreateWolf(start + Vector3.right * 2f);
        SetPrivateField(GetProperty<object>(hunter, "Perception"), "<ClosestWolf>k__BackingField", wolf);
        var lines = new List<string>();
        void Collect(string line, string stack, LogType type)
        {
            if (line.StartsWith("[FleeRoute]")) lines.Add(line);
        }
        Application.logMessageReceived += Collect;
        try
        {
            ChangeState(hunter, "Flee");
            ulong initialId = GetProperty<ulong>(GetActiveNavigationRequest(hunter), "Id");
            object flee = GetProperty<object>(GetPrivateField(hunter, "stateMachine"), "CurrentState");
            object controller = GetProperty<object>(hunter, "NavigationController");
            Invoke(controller, "SetEnabled", true);
            Invoke(controller, "Tick");
            InvokePrivate(flee, "Tick", 1f); // Stable Wolf: no decision.
            wolf.transform.position = start + Vector3.right * 4f;
            InvokePrivate(flee, "Tick", 1f); // West still wins.
            Assert.That(GetProperty<ulong>(GetActiveNavigationRequest(hunter), "Id"), Is.EqualTo(initialId));
            wolf.transform.position = start + Vector3.left * 2f;
            InvokePrivate(flee, "Tick", 1f);
            ulong newId = GetProperty<ulong>(GetActiveNavigationRequest(hunter), "Id");
            Assert.That(newId, Is.Not.EqualTo(initialId));
            Assert.That(lines.Count, Is.EqualTo(trace ? 3 : 0));
            if (trace)
            {
                Assert.That(lines[0], Does.Contain("event=initial").And.Contain("active=[none]")
                    .And.Contain("candidate=[zone=West").And.Contain("InitialApproach="));
                Assert.That(lines[1], Does.Contain("event=replace").And.Contain("decision=keep/same-destination")
                    .And.Contain($"requestAfter={initialId}"));
                Assert.That(lines[2], Does.Contain("decision=replace-accepted").And.Contain("active=[zone=West")
                    .And.Contain("candidate=[zone=West").And.Contain($"requestAfter={newId}"));
            }
        }
        finally { Application.logMessageReceived -= Collect; }
    }

    [Test]
    public void ReplanFromNewCellAdoptsShorterReachablePathEvenWhenItIsRiskier()
    {
        NavigationGrid grid = CreateGrid();
        SetSafeZone(grid, 2, 0, "West");
        SetSafeZone(grid, 2, 4, "East");
        Vector3 start = grid.GridToWorldPosition(2, 2);
        Component hunter = CreateHunterAgent(true, 0f, grid, start);
        Component wolf = CreateWolf(start + Vector3.right * 2f);
        SetPrivateField(GetProperty<object>(hunter, "Perception"), "<ClosestWolf>k__BackingField", wolf);
        ChangeState(hunter, "Flee");
        object initial = GetActiveNavigationRequest(hunter);
        Assert.That(GetProperty<Vector3>(initial, "Destination"), Is.EqualTo(grid.GridToWorldPosition(2, 0)));
        object controller = GetProperty<object>(hunter, "NavigationController");
        Invoke(controller, "Tick");

        // Controlled change of origin: East now costs 4, West costs 12.
        hunter.transform.position = grid.GridToWorldPosition(2, 3);
        wolf.transform.position = hunter.transform.position + Vector3.right * 2f;
        object flee = GetProperty<object>(GetPrivateField(hunter, "stateMachine"), "CurrentState");
        InvokePrivate(flee, "Tick", 1f);
        object replacement = GetActiveNavigationRequest(hunter);
        Assert.That(GetProperty<ulong>(replacement, "Id"), Is.Not.EqualTo(GetProperty<ulong>(initial, "Id")));
        Assert.That(GetProperty<Vector3>(replacement, "Destination"), Is.EqualTo(grid.GridToWorldPosition(2, 4)));
        var planned = GetProperty<NavigationPathResult>(replacement, "PlannedPath");
        Assert.That(planned.TotalCost, Is.GreaterThan(4f), "The nearest destination admits a detour around the Wolf.");
        Assert.That(SafeZonePathSelector.EvaluateThreat(planned, hunter.transform.position,
            wolf.transform.position, 0.9f, 0.35f, 1).CrossesCaptureRadius, Is.False);
        Invoke(controller, "Tick");
        Assert.That(GetProperty<object>(controller, "CurrentPath"), Is.SameAs(planned));
    }

    [Test]
    public void DefaultFleeRequestUsesSixMetersPerSecondAtHunterBaseSpeed()
    {
        NavigationGrid grid = CreateGrid();
        SetSafeZone(grid, 2, 4, "East");
        Component hunter = CreateHunterAgent(true, 0f, grid, grid.GridToWorldPosition(2, 0));
        SetPrivateField(hunter, "pursuitSpeedMultiplier", 1.25f);
        ChangeState(hunter, "Flee");

        float multiplier = GetProperty<float>(GetActiveNavigationRequest(hunter), "SpeedMultiplier");
        float baseSpeed = (float)GetPrivateField(GetProperty<object>(hunter, "Movement"), "speed");
        Assert.That(baseSpeed, Is.EqualTo(3f));
        Assert.That(multiplier, Is.EqualTo(2f));
        Assert.That(baseSpeed * multiplier, Is.EqualTo(6f));
        Assert.That(GetProperty<float>(hunter, "PursuitSpeedMultiplier"), Is.EqualTo(1.25f));
    }

    [Test]
    public void MovingWolfCanReplaceDangerousPathToSameCellOnlyAfterCooldown()
    {
        NavigationGrid grid = CreateGrid();
        SetSafeZone(grid, 2, 4, "East");
        Vector3 start = grid.GridToWorldPosition(2, 2);
        Component hunter = CreateHunterAgent(true, 0f, grid, start);
        const float configuredFleeMultiplier = 1.9f;
        SetPrivateField(hunter, "fleeSpeedMultiplier", configuredFleeMultiplier);
        Component wolf = CreateWolf(start + Vector3.left * 2f);
        SetPrivateField(GetProperty<object>(hunter, "Perception"), "<ClosestWolf>k__BackingField", wolf);
        ChangeState(hunter, "Flee");
        object original = GetActiveNavigationRequest(hunter);
        Assert.That(GetProperty<float>(original, "SpeedMultiplier"), Is.EqualTo(configuredFleeMultiplier));
        ulong id = GetProperty<ulong>(original, "Id");
        object controller = GetProperty<object>(hunter, "NavigationController");
        object flee = GetProperty<object>(GetPrivateField(hunter, "stateMachine"), "CurrentState");
        Invoke(controller, "Tick");
        wolf.transform.position = start + Vector3.right * 2f;
        InvokePrivate(flee, "Tick", 0.5f);
        Assert.That(GetProperty<ulong>(GetActiveNavigationRequest(hunter), "Id"), Is.EqualTo(id));
        InvokePrivate(flee, "Tick", 0.3f);
        object replacement = GetActiveNavigationRequest(hunter);
        Assert.That(GetProperty<float>(replacement, "SpeedMultiplier"), Is.EqualTo(configuredFleeMultiplier));
        ulong newId = GetProperty<ulong>(replacement, "Id");
        Assert.That(newId, Is.Not.EqualTo(id));
        Assert.That(GetProperty<Vector3>(replacement, "Destination"), Is.EqualTo(GetProperty<Vector3>(original, "Destination")));
        var route = GetProperty<NavigationPathResult>(replacement, "PlannedPath");
        Assert.That(route.Cells, Is.Not.EqualTo(GetProperty<NavigationPathResult>(original, "PlannedPath").Cells));
        Assert.That(SafeZonePathSelector.EvaluateThreat(route, start, wolf.transform.position, 0.9f, 0.35f, 1)
            .CrossesCaptureRadius, Is.False);
        Invoke(controller, "Tick");
        Assert.That(GetProperty<object>(controller, "CurrentPath"), Is.SameAs(route));
        InvokePrivate(flee, "Tick", 1f);
        Assert.That(GetProperty<ulong>(GetActiveNavigationRequest(hunter), "Id"), Is.EqualTo(newId));
    }

    [Test]
    public void FleeWithoutDetectedWolfPreservesLeastCostSelection()
    {
        NavigationGrid grid = CreateGrid();
        SetSafeZone(grid, 0, 4, "SafeZone_A");
        SetSafeZone(grid, 2, 2, "SafeZone_B");
        Assert.That(grid.SetWalkable(2, 1, false), Is.True);
        Assert.That(grid.SetWalkable(1, 2, false), Is.True);
        Assert.That(grid.SetWalkable(3, 2, false), Is.True);
        Component hunter = CreateHunterAgent(true, 0f, grid, grid.GridToWorldPosition(2, 0));

        ChangeState(hunter, "Flee");
        object request = GetActiveNavigationRequest(hunter);

        Assert.That(GetProperty<Vector3>(request, "Destination"),
            Is.EqualTo(grid.GridToWorldPosition(0, 4)));
        Assert.That(GetProperty<object>(request, "PlannedPath"), Is.Not.Null);
    }

    [Test]
    public void FleeArrivalStopsInsideSafeZoneAndKeepsProtectedState()
    {
        NavigationGrid grid = CreateGrid();
        SetSafeZone(grid, 2, 4, "SafeZone_A");
        Component hunter = CreateHunterAgent(true, 0.3f, grid, grid.GridToWorldPosition(2, 0));
        ChangeState(hunter, "Flee");
        Vector3 destination = GetProperty<Vector3>(GetActiveNavigationRequest(hunter), "Destination");

        InvokePrivate(hunter, "Update");
        hunter.transform.position = destination;
        Physics.SyncTransforms();
        InvokePrivate(hunter, "Update");
        InvokePrivate(hunter, "Update");

        Assert.That(GetProperty<Enum>(hunter, "CurrentState").ToString(), Is.EqualTo("Flee"));
        Assert.That(GetProperty<bool>(GetProperty<object>(hunter, "NavigationIntent"),
            "HasActiveRequest"), Is.False);
        Assert.That(grid.TryWorldToGrid(hunter.transform.position, out int row, out int column), Is.True);
        Assert.That(grid.TryGetCell(row, column, out NavigationGridCell cell), Is.True);
        Assert.That(cell.Zone, Is.EqualTo(NavigationGridZone.SafeZone));
        Assert.That(GetProperty<Vector3>(GetProperty<object>(hunter, "Movement"), "Velocity"),
            Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void FleeArrivalStartsConfiguredSafeZoneWait()
    {
        NavigationGrid grid = CreateGrid();
        SetSafeZone(grid, 2, 4, "SafeZone_A");
        Component hunter = CreateHunterAgent(
            true, 0.3f, grid, grid.GridToWorldPosition(2, 0), 5f);

        object fleeState = ArriveInSafeZone(hunter);

        Assert.That(GetPrivateField(fleeState, "isWaitingInSafeZone"), Is.True);
        Assert.That(GetPrivateField(fleeState, "safeZoneWaitRemaining"), Is.EqualTo(5f));
        Assert.That(GetProperty<Enum>(hunter, "CurrentState").ToString(), Is.EqualTo("Flee"));
        Assert.That(GetProperty<Vector3>(GetProperty<object>(hunter, "Movement"), "Velocity"),
            Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void FleeRemainsProtectedUntilConfiguredWaitElapses()
    {
        NavigationGrid grid = CreateGrid();
        SetSafeZone(grid, 2, 4, "SafeZone_A");
        Component hunter = CreateHunterAgent(
            true, 0.3f, grid, grid.GridToWorldPosition(2, 0), 5f);
        object fleeState = ArriveInSafeZone(hunter);

        InvokePrivate(fleeState, "UpdateSafeZoneWait", 4f);

        Assert.That(GetProperty<Enum>(hunter, "CurrentState").ToString(), Is.EqualTo("Flee"));
        Assert.That(GetPrivateField(fleeState, "safeZoneWaitRemaining"), Is.EqualTo(1f));
        Assert.That(GetProperty<Vector3>(GetProperty<object>(hunter, "Movement"), "Velocity"),
            Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void CompletedSafeZoneWaitWithPartialGatherProgressReturnsToSearch()
    {
        NavigationGrid grid = CreateGrid();
        SetSafeZone(grid, 2, 4, "SafeZone_A");
        Component hunter = CreateHunterAgent(
            true, 0.3f, grid, grid.GridToWorldPosition(2, 0), 5f);
        SetPrivateField(hunter, "requiredGathers", 2);
        InvokePrivate(hunter, "RegisterGatherCompletion");
        object fleeState = ArriveInSafeZone(hunter);
        Assert.That(GetProperty<bool>(GetProperty<object>(hunter, "NavigationIntent"), "HasActiveRequest"), Is.False);

        InvokePrivate(fleeState, "UpdateSafeZoneWait", 5f);
        InvokePrivate(hunter, "Update");

        Assert.That(GetProperty<Enum>(hunter, "CurrentState").ToString(), Is.EqualTo("Search"));
        Assert.That(GetProperty<int>(hunter, "CompletedGathers"), Is.EqualTo(1));
    }

    [Test]
    public void VisibleWolfDoesNotReenterFleeWhileHunterRemainsInSafeZone()
    {
        NavigationGrid grid = CreateGrid();
        SetSafeZone(grid, 2, 4, "SafeZone_A");
        Component hunter = CreateHunterAgent(
            true, 0.3f, grid, grid.GridToWorldPosition(2, 0), 5f);
        CreateWolf(grid.GridToWorldPosition(2, 4) + Vector3.left * 2f);
        Component perception = (Component)GetProperty<object>(hunter, "Perception");
        Physics.SyncTransforms();
        Invoke(perception, "ForceRefresh");
        object fleeState = ArriveInSafeZone(hunter);
        Physics.SyncTransforms();
        Invoke(perception, "ForceRefresh");

        InvokePrivate(fleeState, "UpdateSafeZoneWait", 5f);
        InvokePrivate(hunter, "Update");

        Assert.That(GetProperty<object>(perception, "ClosestWolf"), Is.Not.Null);
        Assert.That(GetProperty<Enum>(hunter, "CurrentState").ToString(), Is.EqualTo("Search"));
    }

    [Test]
    public void VisibleWolfCanTriggerFleeAgainAfterHunterLeavesSafeZone()
    {
        NavigationGrid grid = CreateGrid();
        SetSafeZone(grid, 2, 4, "SafeZone_A");
        Component hunter = CreateHunterAgent(
            true, 0.3f, grid, grid.GridToWorldPosition(2, 0), 5f);
        CreateWolf(grid.GridToWorldPosition(2, 4) + Vector3.left * 2f);
        Component perception = (Component)GetProperty<object>(hunter, "Perception");
        Physics.SyncTransforms();
        Invoke(perception, "ForceRefresh");
        object fleeState = ArriveInSafeZone(hunter);
        InvokePrivate(fleeState, "UpdateSafeZoneWait", 5f);
        hunter.transform.position = grid.GridToWorldPosition(2, 3);
        Physics.SyncTransforms();
        Invoke(perception, "ForceRefresh");

        InvokePrivate(hunter, "Update");

        Assert.That(GetProperty<object>(perception, "ClosestWolf"), Is.Not.Null);
        Assert.That(GetProperty<Enum>(hunter, "CurrentState").ToString(), Is.EqualTo("Flee"));
    }

    [Test]
    public void CompletedSafeZoneWaitUsesExistingExitCondition()
    {
        NavigationGrid grid = CreateGrid();
        SetSafeZone(grid, 2, 4, "SafeZone_A");
        Component hunter = CreateHunterAgent(
            true, 0.3f, grid, grid.GridToWorldPosition(2, 0), 5f);
        SetPrivateField(hunter, "requiredGathers", 1);
        InvokePrivate(hunter, "RegisterGatherCompletion");
        object fleeState = ArriveInSafeZone(hunter);

        InvokePrivate(fleeState, "UpdateSafeZoneWait", 4f);
        Assert.That(GetProperty<Enum>(hunter, "CurrentState").ToString(), Is.EqualTo("Flee"));
        Assert.That(GetProperty<int>(hunter, "CompletedGathers"), Is.EqualTo(1));
        InvokePrivate(fleeState, "UpdateSafeZoneWait", 1f);

        Assert.That(GetProperty<Enum>(hunter, "CurrentState").ToString(), Is.EqualTo("Exit"));
    }

    [Test]
    public void LeavingSafeZoneRestartsEscapeWithoutExitingFlee()
    {
        NavigationGrid grid = CreateGrid();
        SetSafeZone(grid, 2, 4, "SafeZone_A");
        Component hunter = CreateHunterAgent(
            true, 0.3f, grid, grid.GridToWorldPosition(2, 0), 5f);
        object fleeState = ArriveInSafeZone(hunter);
        SetPrivateField(fleeState, "safeZoneWaitRemaining", 0f);
        hunter.transform.position = grid.GridToWorldPosition(2, 3);
        Physics.SyncTransforms();

        InvokePrivate(hunter, "Update");

        Assert.That(GetProperty<Enum>(hunter, "CurrentState").ToString(), Is.EqualTo("Flee"));
        Assert.That(GetPrivateField(fleeState, "isWaitingInSafeZone"), Is.False);
        Assert.That(GetPrivateField(fleeState, "safeZoneWaitRemaining"), Is.EqualTo(0f));
        Assert.That(GetProperty<bool>(GetProperty<object>(hunter, "NavigationIntent"),
            "HasActiveRequest"), Is.True);
        Assert.That(GetProperty<Vector3>(GetProperty<object>(hunter, "Movement"), "Velocity"),
            Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void EnteringFleeAgainResetsSafeZoneWaitState()
    {
        NavigationGrid grid = CreateGrid();
        SetSafeZone(grid, 2, 4, "SafeZone_A");
        Component hunter = CreateHunterAgent(
            true, 0.3f, grid, grid.GridToWorldPosition(2, 0), 5f);
        object fleeState = ArriveInSafeZone(hunter);
        SetPrivateField(fleeState, "safeZoneWaitRemaining", 1f);

        ChangeState(hunter, "Search");
        ChangeState(hunter, "Flee");

        Assert.That(GetPrivateField(fleeState, "isWaitingInSafeZone"), Is.False);
        Assert.That(GetPrivateField(fleeState, "safeZoneWaitRemaining"), Is.EqualTo(0f));

        // The Hunter is already inside the selected Safe Zone. One update reports
        // arrival and starts the new wait; no subsequent frame time is consumed.
        InvokePrivate(hunter, "Update");
        InvokePrivate(fleeState, "UpdateSafeZoneWait", 0f);

        Assert.That(GetPrivateField(fleeState, "isWaitingInSafeZone"), Is.True);
        Assert.That(GetPrivateField(fleeState, "safeZoneWaitRemaining"), Is.EqualTo(5f));
    }

    [Test]
    public void DeadInterruptsAndClearsActiveSafeZoneWait()
    {
        NavigationGrid grid = CreateGrid();
        SetSafeZone(grid, 2, 4, "SafeZone_A");
        Component hunter = CreateHunterAgent(
            true, 0.3f, grid, grid.GridToWorldPosition(2, 0), 5f);
        object machine = GetPrivateField(hunter, "stateMachine");
        object replacementDeadState = GetProperty<object>(machine, "CurrentState");
        object fleeState = ArriveInSafeZone(hunter);
        Type policeState = FindProjectType("PoliceState");
        Invoke(machine, "RegisterState", Enum.Parse(policeState, "Dead"), replacementDeadState);

        Assert.That((bool)Invoke(machine, "ChangeState", Enum.Parse(policeState, "Dead")), Is.True);

        Assert.That(GetProperty<Enum>(hunter, "CurrentState").ToString(), Is.EqualTo("Dead"));
        Assert.That(GetPrivateField(fleeState, "isWaitingInSafeZone"), Is.False);
        Assert.That(GetPrivateField(fleeState, "safeZoneWaitRemaining"), Is.EqualTo(0f));
    }

    [Test]
    public void BlockedActiveCorridorSwitchesToReachableAlternativeWithoutAWolfRefresh()
    {
        NavigationGrid grid = CreateGrid();
        // A costs 8 along the row. B costs 12 via the west branch.
        for (int r = 0; r < 5; r++)
            for (int c = 0; c < 5; c++)
                grid.SetWalkable(r, c, r == 2 || (c == 1 && r >= 2));
        SetSafeZone(grid, 2, 4, "A");
        SetSafeZone(grid, 4, 1, "B");
        Component hunter = CreateHunterAgent(true, 0f, grid, grid.GridToWorldPosition(2, 2));
        ChangeState(hunter, "Flee");
        object controller = GetProperty<object>(hunter, "NavigationController");
        Invoke(controller, "Tick");
        Assert.That(GetProperty<object>(controller, "CurrentPath"), Is.Not.Null);
        Assert.That(GetProperty<Vector3>(GetActiveNavigationRequest(hunter), "Destination"),
            Is.EqualTo(grid.GridToWorldPosition(2, 4)));

        grid.SetWalkable(2, 3, false); // Block the next step after A's path was adopted.
        Vector3 before = hunter.transform.position;
        InvokePrivate(hunter, "Update");
        Assert.That(hunter.transform.position, Is.EqualTo(before));
        object alternative = GetActiveNavigationRequest(hunter);
        Assert.That(GetProperty<Vector3>(alternative, "Destination"), Is.EqualTo(grid.GridToWorldPosition(4, 1)));
        Invoke(controller, "Tick");
        Assert.That(GetProperty<object>(controller, "CurrentPath"),
            Is.SameAs(GetProperty<object>(alternative, "PlannedPath")));

        // B is now in progress; a later, independent obstruction must not exhaust
        // the retry budget that applied to the earlier failed route.
        object flee = GetProperty<object>(GetPrivateField(hunter, "stateMachine"), "CurrentState");
        InvokePrivate(flee, "Tick", 0f);
        grid.SetWalkable(2, 3, true);
        grid.SetWalkable(2, 1, false);
        InvokePrivate(hunter, "Update");
        Assert.That(GetProperty<Vector3>(GetActiveNavigationRequest(hunter), "Destination"),
            Is.EqualTo(grid.GridToWorldPosition(2, 4)));
    }

    [Test]
    public void FleeNoPathCancelsOnceWithoutRetrying()
    {
        NavigationGrid grid = CreateGrid();
        SetSafeZone(grid, 2, 4, "SafeZone_A");
        Component hunter = CreateHunterAgent(true, 0.3f, grid, grid.GridToWorldPosition(2, 0));
        ChangeState(hunter, "Flee");
        for (int row = 0; row < grid.Rows; row++)
        {
            Assert.That(grid.SetWalkable(row, 2, false), Is.True);
        }

        InvokePrivate(hunter, "Update");
        object controller = GetProperty<object>(hunter, "NavigationController");
        int calculationCount = GetProperty<int>(controller, "PathCalculationCount");
        InvokePrivate(hunter, "Update");

        Assert.That(GetProperty<bool>(GetProperty<object>(hunter, "NavigationIntent"),
            "HasActiveRequest"), Is.False);
        Assert.That(calculationCount, Is.EqualTo(1));
        Assert.That(GetProperty<int>(controller, "PathCalculationCount"), Is.EqualTo(calculationCount));
        Assert.That(GetProperty<Enum>(hunter, "CurrentState").ToString(), Is.EqualTo("Flee"));
    }

    [Test]
    public void FleeWithoutGridStopsWithoutCreatingRequests()
    {
        Component hunter = CreateHunterAgent(true);
        ChangeState(hunter, "Flee");

        InvokePrivate(hunter, "Update");
        InvokePrivate(hunter, "Update");

        Assert.That(GetProperty<bool>(GetProperty<object>(hunter, "NavigationIntent"),
            "HasActiveRequest"), Is.False);
        Assert.That(GetProperty<int>(GetProperty<object>(hunter, "NavigationController"),
            "PathCalculationCount"), Is.EqualTo(0));
        Assert.That(GetProperty<Enum>(hunter, "CurrentState").ToString(), Is.EqualTo("Flee"));
    }

    [Test]
    public void DeadInterruptsFleeAndCancelsOwnedNavigation()
    {
        NavigationGrid grid = CreateGrid();
        SetSafeZone(grid, 2, 4, "SafeZone_A");
        Component hunter = CreateHunterAgent(true, 0.3f, grid, grid.GridToWorldPosition(2, 0));
        object machine = GetPrivateField(hunter, "stateMachine");
        Type policeState = FindProjectType("PoliceState");
        object patrolState = GetProperty<object>(machine, "CurrentState");
        ChangeState(hunter, "Flee");
        Invoke(machine, "RegisterState", Enum.Parse(policeState, "Dead"), patrolState);

        Assert.That((bool)Invoke(machine, "ChangeState", Enum.Parse(policeState, "Dead")), Is.True);

        Assert.That(GetProperty<Enum>(hunter, "CurrentState").ToString(), Is.EqualTo("Dead"));
        Assert.That(GetProperty<bool>(GetProperty<object>(hunter, "NavigationIntent"),
            "HasActiveRequest"), Is.False);
    }

    private Component CreatePerception(float wolfRadius)
    {
        GameObject hunter = CreateObject("HunterFleeDetectionTests.Perception", Vector3.zero);
        Component perception = (Component)hunter.AddComponent(FindProjectType("HunterPerception"));
        SetPrivateField(perception, "wolfDetectionRadius", wolfRadius);
        SetPrivateField(perception, "wolfLayerMask", (LayerMask)~0);
        return perception;
    }

    private Component CreateHunterAgent(
        bool gridNavigation = false,
        float lossGraceDuration = 0.3f,
        NavigationGrid grid = null,
        Vector3? position = null,
        float safeZoneWaitTime = 3f)
    {
        GameObject hunter = CreateObject(
            "HunterFleeDetectionTests.Agent", position ?? Vector3.zero);
        hunter.SetActive(false);
        Component agent = (Component)hunter.AddComponent(FindProjectType("FSMAgent"));
        SetPrivateField(agent, "enableGridNavigation", gridNavigation);
        SetPrivateField(agent, "navigationGrid", grid);
        SetPrivateField(agent, "safeZoneWaitTime", safeZoneWaitTime);
        InvokePrivate(agent, "Awake");
        Component perception = (Component)GetProperty<object>(agent, "Perception");
        SetPrivateField(perception, "wolfDetectionRadius", 5f);
        SetPrivateField(perception, "wolfLossGraceDuration", lossGraceDuration);
        SetPrivateField(perception, "wolfLayerMask", (LayerMask)~0);
        return agent;
    }

    private static object ArriveInSafeZone(Component hunter)
    {
        ChangeState(hunter, "Flee");
        CompleteFleeNavigationToSafeZone(hunter);
        return GetProperty<object>(GetPrivateField(hunter, "stateMachine"), "CurrentState");
    }

    private static void CompleteFleeNavigationToSafeZone(Component hunter)
    {
        Vector3 destination = GetProperty<Vector3>(
            GetActiveNavigationRequest(hunter), "Destination");
        InvokePrivate(hunter, "Update");
        hunter.transform.position = destination;
        Physics.SyncTransforms();
        InvokePrivate(hunter, "Update");
    }

    private NavigationGrid CreateGrid()
    {
        GameObject gridObject = CreateObject("HunterFleeDetectionTests.Grid", Vector3.zero);
        NavigationGrid grid = gridObject.AddComponent<NavigationGrid>();
        Assert.That(grid.TryApplyGridSize(5, 5, 4f, true, out _), Is.True);
        return grid;
    }

    private static void SetSafeZone(
        NavigationGrid grid, int row, int column, string zoneId)
    {
        Assert.That(grid.SetWalkable(row, column, true), Is.True);
        Assert.That(grid.SetZone(row, column, NavigationGridZone.SafeZone, zoneId), Is.True);
    }

    private static void ChangeState(Component hunter, string stateName)
    {
        object machine = GetPrivateField(hunter, "stateMachine");
        Type policeState = FindProjectType("PoliceState");
        Assert.That((bool)Invoke(machine, "ChangeState", Enum.Parse(policeState, stateName)), Is.True);
    }

    private static object GetActiveNavigationRequest(Component hunter)
    {
        object intent = GetProperty<object>(hunter, "NavigationIntent");
        Type requestType = FindProjectType("HunterNavigationRequest");
        object[] arguments = { Activator.CreateInstance(requestType) };
        Assert.That((bool)Invoke(intent, "TryGetCurrent", arguments), Is.True);
        return arguments[0];
    }

    private Component CreateWolf(Vector3 position)
    {
        GameObject wolf = CreateObject("HunterFleeDetectionTests.Wolf", position);
        wolf.SetActive(false);
        wolf.AddComponent<SphereCollider>().radius = 0.5f;
        Component fsm = (Component)wolf.AddComponent(FindProjectType("PredatorFSM"));
        wolf.SetActive(true);
        return fsm;
    }

    private Component CreateLivingBoid(Vector3 position)
    {
        GameObject boid = CreateObject("HunterFleeDetectionTests.Boid", position);
        boid.layer = 9;
        boid.AddComponent<SphereCollider>().radius = 0.5f;
        return (Component)boid.AddComponent(FindProjectType("BoidLife"));
    }

    private GameObject CreateObstacleBetweenHunterAndWolf()
    {
        GameObject obstacle = CreateObject(
            "HunterFleeDetectionTests.Wall", new Vector3(2f, 1f, 0f));
        obstacle.layer = 12;
        obstacle.AddComponent<BoxCollider>().size = new Vector3(1f, 3f, 1f);
        return obstacle;
    }

    private GameObject CreateObject(string name, Vector3 position)
    {
        GameObject instance = new GameObject(name);
        instance.transform.position = position;
        objects.Add(instance);
        return instance;
    }

    private static object Invoke(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName);
        Assert.That(method, Is.Not.Null, "Missing method " + methodName + ".");
        return method.Invoke(target, arguments);
    }

    private static object InvokePrivate(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, "Missing method " + methodName + ".");
        return method.Invoke(target, arguments);
    }

    private static T GetProperty<T>(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName);
        Assert.That(property, Is.Not.Null, "Missing property " + propertyName + ".");
        return (T)property.GetValue(target);
    }

    private static object GetPrivateField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Missing field " + fieldName + ".");
        return field.GetValue(target);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Missing field " + fieldName + ".");
        field.SetValue(target, value);
    }

    private static Type FindProjectType(string typeName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(typeName, false);
            if (type != null)
            {
                return type;
            }
        }

        Assert.Fail("Could not locate project type " + typeName + ".");
        return null;
    }
}
