using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class PredatorAlertBootstrapTests
{
    private readonly List<GameObject> objects = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        for (int i = objects.Count - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(objects[i]);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void SharedBootstrapAllowsEachWolfToEmitAndRecipientsSearchWithoutRebroadcasting(
        int emitterIndex)
    {
        Type fsmType = Type.GetType("PredatorFSM, Assembly-CSharp");
        Type alertSystemType = Type.GetType("PredatorAlertSystem, Assembly-CSharp");
        Type bootstrapType = Type.GetType("PredatorAlertBootstrap, Assembly-CSharp");
        Assert.That(fsmType, Is.Not.Null);
        Assert.That(alertSystemType, Is.Not.Null);
        Assert.That(bootstrapType, Is.Not.Null);

        GameObject target = CreateThreat("AlertHunter");
        var wolves = new Component[4];
        for (int i = 0; i < wolves.Length; i++)
        {
            GameObject root = CreateObject($"AlertWolf_{i}");
            wolves[i] = root.AddComponent(fsmType);
            InitializeFsm(wolves[i]);
            InvokePublic(wolves[i], "ConfigureTargets", new List<GameObject> { target });
        }

        GameObject serviceRoot = CreateObject("SharedAlertService");
        Component alertSystem = serviceRoot.AddComponent(alertSystemType);
        Component bootstrap = serviceRoot.AddComponent(bootstrapType);
        Array members = Array.CreateInstance(fsmType, wolves.Length);
        for (int i = 0; i < wolves.Length; i++)
            members.SetValue(wolves[i], i);

        InvokePublic(bootstrap, "Configure", alertSystem, members);
        InvokePublic(bootstrap, "Configure", alertSystem, members);
        FieldInfo alertRaisedField = alertSystemType.GetField("AlertRaised",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Delegate alertHandlers = (Delegate)alertRaisedField.GetValue(alertSystem);
        for (int i = 0; i < wolves.Length; i++)
        {
            int subscriptions = 0;
            foreach (Delegate handler in alertHandlers.GetInvocationList())
                if (handler.Target == wolves[i]) subscriptions++;
            Assert.That(subscriptions, Is.EqualTo(1),
                $"Wolf {i} must subscribe exactly once to the shared alert system.");
        }

        Vector3 lastKnownPosition = new Vector3(5f, 0f, 7f);
        Component sourcePerception = (Component)GetProperty<object>(wolves[emitterIndex], "Perception");
        sourcePerception.GetType().GetField("target", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(sourcePerception, target);
        SetBackingField(sourcePerception, "CanSeeTarget", true);
        SetBackingField(sourcePerception, "NewlyDetected", true);
        SetBackingField(sourcePerception, "LastSeenPosition", lastKnownPosition);
        Invoke(wolves[emitterIndex], "UpdateVisibleTarget");

        Assert.That(GetProperty<int>(wolves[emitterIndex], "AlertsSent"), Is.EqualTo(1));
        Assert.That(GetProperty<bool>(wolves[emitterIndex], "ReceivedAlert"), Is.False,
            "A Wolf must ignore its own broadcast.");
        Assert.That(GetProperty<object>(wolves[emitterIndex], "CurrentState").ToString(),
            Is.EqualTo("Patrol"), "The alert source must not enter Search from its own alert.");

        for (int i = 0; i < wolves.Length; i++)
        {
            if (i == emitterIndex) continue;

            Assert.That(GetProperty<bool>(wolves[i], "ReceivedAlert"), Is.True);
            Assert.That(GetProperty<object>(wolves[i], "CurrentState").ToString(), Is.EqualTo("Search"));
            Assert.That(GetProperty<int>(wolves[i], "AlertsSent"), Is.Zero,
                "Receiving an alert must not rebroadcast it.");
            Assert.That(GetProperty<Vector3>(wolves[i], "LastKnownPosition"),
                Is.EqualTo(lastKnownPosition));
            Assert.That(GetProperty<GameObject>(wolves[i], "CurrentTarget"), Is.SameAs(target));
        }
    }

    [Test]
    public void DuplicateWolfConfigurationIsRejectedBeforeAnySubscriptionIsAdded()
    {
        Type fsmType = Type.GetType("PredatorFSM, Assembly-CSharp");
        Type alertSystemType = Type.GetType("PredatorAlertSystem, Assembly-CSharp");
        Type bootstrapType = Type.GetType("PredatorAlertBootstrap, Assembly-CSharp");
        Assert.That(fsmType, Is.Not.Null);
        Assert.That(alertSystemType, Is.Not.Null);
        Assert.That(bootstrapType, Is.Not.Null);
        Component firstWolf = CreateObject("DuplicateWolf_A").AddComponent(fsmType);
        Component secondWolf = CreateObject("DuplicateWolf_B").AddComponent(fsmType);
        Component alertSystem = CreateObject("DuplicateAlertSystem").AddComponent(alertSystemType);
        Component bootstrap = alertSystem.gameObject.AddComponent(bootstrapType);
        Array members = Array.CreateInstance(fsmType, 3);
        members.SetValue(firstWolf, 0);
        members.SetValue(secondWolf, 1);
        members.SetValue(firstWolf, 2);

        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
            () => Invoke(bootstrap, "Configure", alertSystem, members));

        Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
        FieldInfo alertRaisedField = alertSystemType.GetField("AlertRaised",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(alertRaisedField.GetValue(alertSystem), Is.Null,
            "Invalid membership must not leave earlier Wolves subscribed.");
    }

    [Test]
    public void AlertSystemRejectsExpiredAndSafeZoneTargets()
    {
        Type alertSystemType = Type.GetType("PredatorAlertSystem, Assembly-CSharp");
        Type fsmType = Type.GetType("PredatorFSM, Assembly-CSharp");
        GameObject sourceRoot = CreateObject("AlertSource");
        Component source = sourceRoot.AddComponent(fsmType);
        GameObject target = CreateThreat("AlertTarget");
        Component alertSystem = CreateObject("AlertSystem").AddComponent(alertSystemType);
        var alertType = Type.GetType("PredatorAlert, Assembly-CSharp");
        object expired = Activator.CreateInstance(alertType,
            source, target, Vector3.one, Time.time - 10f);

        Assert.That(Invoke<bool>(alertSystem, "IsRecent", expired), Is.False);

        GameObject zoneObject = CreateObject("AlertSafeZone");
        zoneObject.transform.position = target.transform.position;
        zoneObject.transform.localScale = Vector3.one * 4f;
        zoneObject.AddComponent<BoxCollider>().isTrigger = true;
        Component safeZone = zoneObject.AddComponent(Type.GetType("SafeZoneVolume, Assembly-CSharp"));
        Invoke(safeZone, "OnEnable");
        Physics.SyncTransforms();
        Invoke(alertSystem, "Broadcast", source, target, target.transform.position);
        object[] recentAlertArguments = { null };
        bool hasRecentAlert = (bool)alertSystem.GetType()
            .GetMethod("TryGetRecentAlert").Invoke(alertSystem, recentAlertArguments);
        Assert.That(hasRecentAlert, Is.False);
    }

    [Test]
    public void DirectVisualDetectionTakesPriorityOverReceivedAlert()
    {
        Type fsmType = Type.GetType("PredatorFSM, Assembly-CSharp");
        GameObject target = CreateThreat("VisibleAlertHunter");
        Component source = CreateObject("VisibleAlertSource").AddComponent(fsmType);
        Component receiver = CreateObject("VisibleAlertReceiver").AddComponent(fsmType);
        InitializeFsm(source);
        InitializeFsm(receiver);
        Invoke(receiver, "ConfigureTargets", new List<GameObject> { target });
        Component perception = (Component)GetProperty<object>(receiver, "Perception");
        perception.GetType().GetField("<CanSeeTarget>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic).SetValue(perception, true);
        Component system = CreateObject("VisibleAlertSystem")
            .AddComponent(Type.GetType("PredatorAlertSystem, Assembly-CSharp"));
        Invoke(receiver, "ConfigureAlertSystem", system);

        Invoke(system, "Broadcast", source, target, target.transform.position);

        Assert.That(GetProperty<bool>(receiver, "ReceivedAlert"), Is.False);
        Assert.That(GetProperty<object>(receiver, "CurrentState").ToString(), Is.EqualTo("Patrol"));
    }

    private GameObject CreateThreat(string name)
    {
        GameObject threat = CreateObject(name);
        threat.AddComponent(Type.GetType("BoidThreat, Assembly-CSharp"));
        threat.AddComponent<CapsuleCollider>().isTrigger = true;
        return threat;
    }

    private GameObject CreateObject(string name)
    {
        var result = new GameObject(name);
        objects.Add(result);
        return result;
    }

    private static void Invoke(Component component, string method, params object[] arguments)
    {
        component.GetType().GetMethod(method,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Invoke(component, arguments);
    }

    private static void InvokePublic(Component component, string method, params object[] arguments)
    {
        component.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public)
            .Invoke(component, arguments);
    }

    private static void InitializeFsm(Component fsm)
    {
        Invoke(fsm, "CacheDependencies");
        Invoke(fsm, "InitializeStateMachine");
    }

    private static T Invoke<T>(Component component, string method, params object[] arguments)
    {
        return (T)component.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public)
            .Invoke(component, arguments);
    }

    private static T GetProperty<T>(Component component, string property)
    {
        return (T)component.GetType().GetProperty(property).GetValue(component);
    }

    private static void SetBackingField(Component component, string property, object value)
    {
        component.GetType().GetField($"<{property}>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic).SetValue(component, value);
    }

}
