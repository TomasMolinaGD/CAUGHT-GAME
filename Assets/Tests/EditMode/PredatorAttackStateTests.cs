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

    private AttackFixture CreateFixture(float targetDistance, NavigationGrid grid = null)
    {
        GameObject root = CreateObject("PredatorAttackStateTests_Wolf", Vector3.zero);
        Component controller = null;
        if (grid != null)
            controller = Add(root, "PredatorNavigationMovementController");
        Component fsm = Add(root, "PredatorFSM");
        Component perception = root.GetComponent(Type.GetType("PredatorPerception, Assembly-CSharp"));
        Component combat = root.GetComponent(Type.GetType("PredatorCombat, Assembly-CSharp"));
        Component animation = root.GetComponent(Type.GetType("PredatorAnimationController, Assembly-CSharp"));
        InvokePrivate(fsm, "CacheDependencies");
        InvokePrivate(fsm, "InitializeStateMachine");
        Invoke(animation, "ConfigureAttackDuration", 0.1f);

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

        return new AttackFixture(fsm, perception, combat, target);
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
        object attackState = GetStateMachine(fixture.Fsm).GetType()
            .GetProperty("CurrentState").GetValue(GetStateMachine(fixture.Fsm));
        SetPrivateField(attackState, "attackElapsedTime", 1f);
        TickStateMachine(fixture.Fsm);
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
        public AttackFixture(Component fsm, Component perception, Component combat, GameObject target)
        {
            Fsm = fsm;
            Perception = perception;
            Combat = combat;
            Target = target;
        }

        public Component Fsm { get; }
        public Component Perception { get; }
        public Component Combat { get; }
        public GameObject Target { get; }
    }
}
