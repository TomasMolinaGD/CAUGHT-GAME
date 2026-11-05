using System;
using System.Collections.Generic;
using System.Reflection;
using CaughtGame.Navigation;
using NUnit.Framework;
using UnityEngine;

public sealed class PredatorAttackStateTests
{
    private readonly List<GameObject> objects = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        for (int i = objects.Count - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(objects[i]);
    }

    [Test]
    public void ModerateSeparationDoesNotCancelAttackBeforeAnimationCompletes()
    {
        AttackFixture fixture = CreateFixture(0.5f);
        fixture.Target.transform.position = Vector3.forward * 1.2f;
        Invoke(fixture.Fsm.GetComponent(Type.GetType(
            "PredatorAnimationController, Assembly-CSharp")), "ConfigureAttackDuration", 10f);

        TickStateMachine(fixture.Fsm);

        Assert.That(GetState(fixture.Fsm), Is.EqualTo("Attack"));
        Assert.That(GetProperty<int>(fixture.Combat, "CaptureAttemptCount"), Is.Zero);
    }

    [Test]
    public void CompletedAttackOutsideRangeFailsAndReturnsToPursuit()
    {
        AttackFixture fixture = CreateFixture(0.5f);
        fixture.Target.transform.position = Vector3.forward * 1.2f;

        CompleteAttack(fixture);

        Assert.That(GetState(fixture.Fsm), Is.EqualTo("Pursuit"));
        Assert.That(fixture.Target.activeSelf, Is.True);
        Assert.That(GetProperty<object>(fixture.Combat, "LastCaptureAttemptResult").ToString(),
            Is.EqualTo("Failed"));
        Assert.That(GetProperty<float>(fixture.Combat, "CooldownRemaining"), Is.EqualTo(1f));
    }

    [Test]
    public void CompletedAttackInsideRangeCapturesCurrentTarget()
    {
        AttackFixture fixture = CreateFixture(0.5f);

        CompleteAttack(fixture);

        Assert.That(GetState(fixture.Fsm), Is.EqualTo("Patrol"));
        Assert.That(fixture.Target.activeSelf, Is.False);
        Assert.That(GetProperty<object>(fixture.Combat, "LastCaptureAttemptResult").ToString(),
            Is.EqualTo("Succeeded"));
        Assert.That(GetProperty<int>(fixture.Combat, "CaptureAttemptCount"), Is.EqualTo(1));
    }

    [Test]
    public void ValidImpactCapturesOnceAndWaitsForAnimationCompletion()
    {
        AttackFixture fixture = CreateFixture(0.5f);

        ReachAttackImpact(fixture);

        Assert.That(GetState(fixture.Fsm), Is.EqualTo("Attack"));
        Assert.That(fixture.Target.activeSelf, Is.False);
        Assert.That(GetProperty<int>(fixture.Combat, "CaptureAttemptCount"), Is.EqualTo(1));
        Assert.That(InvokePrivate<bool>(fixture.Fsm, "DiscardUnavailableTarget"), Is.False);
        Assert.That(GetState(fixture.Fsm), Is.EqualTo("Attack"));

        TickAttackState(fixture, 0f);
        Assert.That(GetProperty<int>(fixture.Combat, "CaptureAttemptCount"), Is.EqualTo(1));

        FinishAttackAnimation(fixture);

        Assert.That(GetState(fixture.Fsm), Is.EqualTo("Patrol"));
    }

    [Test]
    public void TargetEscapingBeforeImpactFailsAtCurrentDistance()
    {
        AttackFixture fixture = CreateFixture(0.5f);
        fixture.Target.transform.position = Vector3.forward * 1.2f;

        ReachAttackImpact(fixture);

        Assert.That(GetState(fixture.Fsm), Is.EqualTo("Attack"));
        Assert.That(fixture.Target.activeSelf, Is.True);
        Assert.That(GetProperty<object>(fixture.Combat, "LastCaptureAttemptResult").ToString(),
            Is.EqualTo("Failed"));
        Assert.That(GetProperty<int>(fixture.Combat, "CaptureAttemptCount"), Is.EqualTo(1));

        FinishAttackAnimation(fixture);

        Assert.That(GetState(fixture.Fsm), Is.EqualTo("Pursuit"));
    }

    [Test]
    public void FailedImpactIsNotRetriedDuringSameAnimation()
    {
        AttackFixture fixture = CreateFixture(0.5f);
        fixture.Target.transform.position = Vector3.forward * 1.2f;

        ReachAttackImpact(fixture);
        TickAttackState(fixture, 0f);

        Assert.That(GetState(fixture.Fsm), Is.EqualTo("Attack"));
        Assert.That(GetProperty<int>(fixture.Combat, "CaptureAttemptCount"), Is.EqualTo(1));
    }

    [Test]
    public void WindupApproachesTargetWithoutCreatingNavigationRequest()
    {
        AttackFixture fixture = CreateFixture(0.8f, null, true);
        Invoke(fixture.Fsm.GetComponent(Type.GetType(
            "PredatorAnimationController, Assembly-CSharp")), "ConfigureAttackDuration", 10f);
        float initialZ = fixture.Fsm.transform.position.z;

        TickAttackState(fixture, 0.05f);

        Assert.That(GetState(fixture.Fsm), Is.EqualTo("Attack"));
        Assert.That(fixture.Fsm.transform.position.z, Is.GreaterThan(initialZ));
        Assert.That(GetProperty<bool>(fixture.NavigationController, "HasDestination"), Is.False);
        Assert.That(GetProperty<int>(fixture.Combat, "CaptureAttemptCount"), Is.Zero);
    }

    [Test]
    public void BlockedWindupCancelsAttackWithoutCapture()
    {
        AttackFixture fixture = CreateFixture(0.8f);
        Invoke(fixture.Fsm.GetComponent(Type.GetType(
            "PredatorAnimationController, Assembly-CSharp")), "ConfigureAttackDuration", 10f);
        GameObject obstacle = CreateObject("AttackImpactObstacle",
            Vector3.right * 0.38f + Vector3.forward * 0.18f + Vector3.up * 1.5f);
        obstacle.layer = 12;
        obstacle.transform.localScale = new Vector3(0.1f, 3f, 0.1f);
        obstacle.AddComponent<BoxCollider>();
        Physics.SyncTransforms();

        TickAttackState(fixture, 0.05f);

        Assert.That(GetState(fixture.Fsm), Is.EqualTo("Pursuit"));
        Assert.That(fixture.Target.activeSelf, Is.True);
        Assert.That(GetProperty<int>(fixture.Combat, "CaptureAttemptCount"), Is.Zero);
    }

    [Test]
    public void SafeZoneCrossingDuringWindupCancelsAttackWithoutMovement()
    {
        AttackFixture fixture = CreateFixture(0.8f);
        Invoke(fixture.Fsm.GetComponent(Type.GetType(
            "PredatorAnimationController, Assembly-CSharp")), "ConfigureAttackDuration", 10f);
        GameObject safeZone = CreateObject("AttackWindupSafeZone",
            Vector3.forward * 0.125f + Vector3.up);
        safeZone.transform.localScale = new Vector3(2f, 2f, 0.04f);
        safeZone.AddComponent<BoxCollider>();
        Component safeZoneVolume = Add(safeZone, "SafeZoneVolume");
        InvokePrivate(safeZoneVolume, "OnEnable");
        Physics.SyncTransforms();

        TickAttackState(fixture, 0.05f);

        Assert.That(GetState(fixture.Fsm), Is.EqualTo("Pursuit"));
        Assert.That(fixture.Fsm.transform.position, Is.EqualTo(Vector3.zero));
        Assert.That(GetProperty<int>(fixture.Combat, "CaptureAttemptCount"), Is.Zero);
    }

    [Test]
    public void ObstacleBlockingLineOfSightDuringWindupTransitionsToSearch()
    {
        AttackFixture fixture = CreateFixture(0.8f);
        GameObject obstacle = CreateObject("AttackWindupLosObstacle",
            Vector3.forward * 0.4f + Vector3.up * 1.5f);
        obstacle.layer = 12;
        obstacle.transform.localScale = new Vector3(1f, 3f, 0.1f);
        obstacle.AddComponent<BoxCollider>();
        Physics.SyncTransforms();

        TickAttackState(fixture, 0f);

        Assert.That(GetState(fixture.Fsm), Is.EqualTo("Search"));
        Assert.That(GetProperty<int>(fixture.Combat, "CaptureAttemptCount"), Is.Zero);
    }

    [Test]
    public void TargetEnteringSafeZoneCancelsWithoutCapture()
    {
        AttackFixture fixture = CreateFixture(0.5f);
        GameObject safeZone = CreateObject("AttackSafeZone", fixture.Target.transform.position);
        safeZone.transform.localScale = Vector3.one * 2f;
        safeZone.AddComponent<BoxCollider>();
        Component safeZoneVolume = Add(safeZone, "SafeZoneVolume");
        InvokePrivate(safeZoneVolume, "OnEnable");
        Physics.SyncTransforms();
        Assert.That(Invoke<bool>(safeZoneVolume, "Contains", fixture.Target.transform.position), Is.True);

        TickStateMachine(fixture.Fsm);

        Assert.That(GetState(fixture.Fsm), Is.EqualTo("Patrol"));
        Assert.That(fixture.Target.activeSelf, Is.True);
        Assert.That(GetProperty<int>(fixture.Combat, "CaptureAttemptCount"), Is.Zero);
    }

    [Test]
    public void TargetEnteringNavigationGridSafeZoneCancelsWithoutCapture()
    {
        NavigationGrid grid = CreateGridWithSafeZone();
        AttackFixture fixture = CreateFixture(0.5f, grid);
        fixture.Target.transform.position = grid.GridToWorldPosition(0, 1);

        TickStateMachine(fixture.Fsm);

        Assert.That(GetState(fixture.Fsm), Is.EqualTo("Patrol"));
        Assert.That(fixture.Target.activeSelf, Is.True);
        Assert.That(GetProperty<int>(fixture.Combat, "CaptureAttemptCount"), Is.Zero);
    }

    [Test]
    public void DisabledTargetCancelsWithoutCapture()
    {
        AttackFixture fixture = CreateFixture(0.5f);
        fixture.Target.SetActive(false);

        TickStateMachine(fixture.Fsm);

        Assert.That(GetState(fixture.Fsm), Is.EqualTo("Patrol"));
        Assert.That(GetProperty<int>(fixture.Combat, "CaptureAttemptCount"), Is.Zero);
    }

    [Test]
    public void LostPerceptionCancelsAttackToSearch()
    {
        AttackFixture fixture = CreateFixture(0.5f);
        SetBackingField(fixture.Perception, "CanSeeTarget", false);

        TickStateMachine(fixture.Fsm);

        Assert.That(GetState(fixture.Fsm), Is.EqualTo("Search"));
        Assert.That(fixture.Target.activeSelf, Is.True);
        Assert.That(GetProperty<int>(fixture.Combat, "CaptureAttemptCount"), Is.Zero);
    }

    [Test]
    public void FailedCompletedAttackConsumesCooldownOnlyOnce()
    {
        AttackFixture fixture = CreateFixture(0.5f);
        fixture.Target.transform.position = Vector3.forward * 1.2f;
        CompleteAttack(fixture);
        float cooldownAfterFailure = GetProperty<float>(fixture.Combat, "CooldownRemaining");

        bool secondResult = Invoke<bool>(fixture.Combat, "TryCapture", fixture.Target,
            0.9f, 1.2f, true);

        Assert.That(secondResult, Is.False);
        Assert.That(GetProperty<int>(fixture.Combat, "CaptureAttemptCount"), Is.EqualTo(1));
        Assert.That(GetProperty<float>(fixture.Combat, "CooldownRemaining"),
            Is.EqualTo(cooldownAfterFailure));
    }

    [Test]
    public void DirectCaptureFailsInsideNavigationGridSafeZone()
    {
        NavigationGrid grid = CreateGridWithSafeZone();
        AttackFixture fixture = CreateFixture(0.5f, grid);
        fixture.Target.transform.position = grid.GridToWorldPosition(0, 1);

        bool captured = Invoke<bool>(fixture.Combat, "TryCapture", fixture.Target,
            0.9f, 0.5f, true);

        Assert.That(captured, Is.False);
        Assert.That(fixture.Target.activeSelf, Is.True);
        Assert.That(GetProperty<object>(fixture.Combat, "LastCaptureAttemptResult").ToString(),
            Is.EqualTo("Failed"));
    }

    [Test]
    public void DirectCaptureOutsideNavigationGridSafeZoneUsesNormalRules()
    {
        NavigationGrid grid = CreateGridWithSafeZone();
        AttackFixture fixture = CreateFixture(0.5f, grid);
        fixture.Target.transform.position = grid.GridToWorldPosition(0, 0);

        bool captured = Invoke<bool>(fixture.Combat, "TryCapture", fixture.Target,
            0.9f, 0.5f, true);

        Assert.That(captured, Is.True);
        Assert.That(fixture.Target.activeSelf, Is.False);
        Assert.That(GetProperty<object>(fixture.Combat, "LastCaptureAttemptResult").ToString(),
            Is.EqualTo("Succeeded"));
    }

    [Test]
    public void DirectCaptureStillFailsInsideSafeZoneVolume()
    {
        AttackFixture fixture = CreateFixture(0.5f);
        GameObject safeZone = CreateObject("DirectCaptureSafeZone", fixture.Target.transform.position);
        safeZone.transform.localScale = Vector3.one * 2f;
        safeZone.AddComponent<BoxCollider>();
        Component safeZoneVolume = Add(safeZone, "SafeZoneVolume");
        InvokePrivate(safeZoneVolume, "OnEnable");
        Physics.SyncTransforms();

        bool captured = Invoke<bool>(fixture.Combat, "TryCapture", fixture.Target,
            0.9f, 0.5f, true);

        Assert.That(captured, Is.False);
        Assert.That(fixture.Target.activeSelf, Is.True);
    }

    [Test]
    public void DirectCaptureWithoutNavigationProviderKeepsLegacyBehavior()
    {
        AttackFixture fixture = CreateFixture(0.5f);

        bool captured = Invoke<bool>(fixture.Combat, "TryCapture", fixture.Target,
            0.9f, 0.5f, true);

        Assert.That(captured, Is.True);
        Assert.That(fixture.Target.activeSelf, Is.False);
    }

    private AttackFixture CreateFixture(float targetDistance, NavigationGrid grid = null,
        bool includeNavigationController = false)
    {
        GameObject root = CreateObject("PredatorAttackStateTests_Wolf", Vector3.zero);
        Component controller = null;
        if (grid != null || includeNavigationController)
            controller = Add(root, "PredatorNavigationMovementController");
        Component fsm = Add(root, "PredatorFSM");
        Component perception = root.GetComponent(Type.GetType("PredatorPerception, Assembly-CSharp"));
        Component combat = root.GetComponent(Type.GetType("PredatorCombat, Assembly-CSharp"));
        Component animation = root.GetComponent(Type.GetType("PredatorAnimationController, Assembly-CSharp"));
        InvokePrivate(fsm, "CacheDependencies");
        InvokePrivate(fsm, "InitializeStateMachine");
        Invoke(animation, "ConfigureAttackDuration", 0.1f);
        Invoke(animation, "ConfigureAttackImpactNormalizedTime", 0.5f);

        GameObject target = CreateObject("PredatorAttackStateTests_Target",
            Vector3.forward * targetDistance);
        if (grid != null)
        {
            GameObject providerObject = CreateObject("PredatorAttackStateTests_Provider", Vector3.zero);
            Component provider = providerObject.AddComponent(Type.GetType(
                "CaughtGame.Navigation.NavigationGridGraphProvider, Assembly-CSharp"));
            Invoke(provider, "Configure", grid, (LayerMask)0);
            Invoke(controller, "ConfigureGraphProvider", provider);
        }
        Add(target, "BoidThreat");
        Invoke(fsm, "ConfigureTargets", new List<GameObject> { target });
        Invoke(fsm, "SetTarget", target);
        SetPrivateField(perception, "target", target);
        SetBackingField(perception, "CanSeeTarget", true);
        SetBackingField(perception, "LastSeenPosition", target.transform.position);
        SetBackingField(perception, "HasLastKnownPosition", true);
        ChangeState(fsm, "Attack");

        return new AttackFixture(fsm, perception, combat, controller, target);
    }

    private NavigationGrid CreateGridWithSafeZone()
    {
        GameObject root = CreateObject("PredatorAttackStateTests_Grid", Vector3.zero);
        NavigationGrid grid = root.AddComponent<NavigationGrid>();
        SetPrivateField(grid, "rows", 1);
        SetPrivateField(grid, "columns", 2);
        SetPrivateField(grid, "cellSize", 1f);
        SetPrivateField(grid, "cells", new List<NavigationGridCell>
        {
            new NavigationGridCell(0, 0, true, NavigationGridZone.Normal),
            new NavigationGridCell(0, 1, true, NavigationGridZone.SafeZone)
        });
        return grid;
    }

    private void CompleteAttack(AttackFixture fixture)
    {
        ReachAttackImpact(fixture);
        if (GetState(fixture.Fsm) == "Attack")
            FinishAttackAnimation(fixture);
    }

    private void ReachAttackImpact(AttackFixture fixture)
    {
        object attackState = GetCurrentState(fixture.Fsm);
        SetPrivateField(attackState, "attackElapsedTime", 0.05f);
        TickAttackState(fixture, 0f);
    }

    private void FinishAttackAnimation(AttackFixture fixture)
    {
        object attackState = GetCurrentState(fixture.Fsm);
        SetPrivateField(attackState, "attackElapsedTime", 0.1f);
        TickAttackState(fixture, 0f);
    }

    private static void TickAttackState(AttackFixture fixture, float deltaTime)
    {
        InvokePrivate(GetCurrentState(fixture.Fsm), "Tick", deltaTime);
    }

    private static object GetCurrentState(Component fsm)
    {
        object stateMachine = GetStateMachine(fsm);
        return stateMachine.GetType().GetProperty("CurrentState").GetValue(stateMachine);
    }

    private GameObject CreateObject(string name, Vector3 position)
    {
        var created = new GameObject(name);
        created.transform.position = position;
        objects.Add(created);
        return created;
    }

    private static Component Add(GameObject root, string typeName)
    {
        Type type = Type.GetType(typeName + ", Assembly-CSharp");
        Assert.That(type, Is.Not.Null, typeName);
        return root.AddComponent(type);
    }

    private static void ChangeState(Component fsm, string stateName)
    {
        object stateMachine = GetStateMachine(fsm);
        Type stateType = fsm.GetType().GetProperty("CurrentState").PropertyType;
        object state = Enum.Parse(stateType, stateName);
        stateMachine.GetType().GetMethod("ChangeState").Invoke(stateMachine, new[] { state });
    }

    private static void TickStateMachine(Component fsm)
    {
        object stateMachine = GetStateMachine(fsm);
        stateMachine.GetType().GetMethod("Update").Invoke(stateMachine, null);
    }

    private static object GetStateMachine(Component fsm)
    {
        return GetPrivateField<object>(fsm, "stateMachine");
    }

    private static string GetState(Component fsm)
    {
        return GetProperty<object>(fsm, "CurrentState").ToString();
    }

    private static void Invoke(Component component, string method, params object[] arguments)
    {
        component.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public)
            .Invoke(component, arguments);
    }

    private static T Invoke<T>(Component component, string method, params object[] arguments)
    {
        return (T)component.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public)
            .Invoke(component, arguments);
    }

    private static void InvokePrivate(Component component, string method)
    {
        component.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(component, null);
    }

    private static T InvokePrivate<T>(Component component, string method)
    {
        return (T)component.GetType().GetMethod(method,
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(component, null);
    }

    private static void InvokePrivate(object target, string method, params object[] arguments)
    {
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(target, arguments);
    }

    private static T GetProperty<T>(Component component, string name)
    {
        return (T)component.GetType().GetProperty(name).GetValue(component);
    }

    private static T GetPrivateField<T>(object target, string name)
    {
        return (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(target);
    }

    private static void SetPrivateField(object target, string name, object value)
    {
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(target, value);
    }

    private static void SetBackingField(object target, string propertyName, object value)
    {
        SetPrivateField(target, $"<{propertyName}>k__BackingField", value);
    }

    private readonly struct AttackFixture
    {
        public AttackFixture(Component fsm, Component perception, Component combat,
            Component navigationController, GameObject target)
        {
            Fsm = fsm;
            Perception = perception;
            Combat = combat;
            NavigationController = navigationController;
            Target = target;
        }

        public Component Fsm { get; }
        public Component Perception { get; }
        public Component Combat { get; }
        public Component NavigationController { get; }
        public GameObject Target { get; }
    }
}
