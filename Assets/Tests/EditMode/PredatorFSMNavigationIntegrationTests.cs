using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class PredatorFSMNavigationIntegrationTests
{
    private readonly List<GameObject> objects = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        for (int i = objects.Count - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(objects[i]);
    }

    [Test]
    public void FsmResolvesNavigationControllerWhenConfigured()
    {
        GameObject root = CreateRoot(true);
        Component fsm = root.GetComponent(Type.GetType("PredatorFSM, Assembly-CSharp"));

        PropertyInfo property = fsm.GetType().GetProperty("NavigationController");
        Assert.That(property.GetValue(fsm), Is.Not.Null);
        Assert.IsTrue((bool)fsm.GetType().GetProperty("HasNavigationController").GetValue(fsm));
    }

    [Test]
    public void FsmRemainsLegacyCompatibleWithoutNavigationController()
    {
        GameObject root = CreateRoot(false);
        Component fsm = root.GetComponent(Type.GetType("PredatorFSM, Assembly-CSharp"));

        Assert.IsNull(fsm.GetType().GetProperty("NavigationController").GetValue(fsm));
        Invoke(fsm, "RequestNavigationDestination", new Vector3(2f, 0f, 0f));
        Invoke(fsm, "CancelNavigation");
    }

    [Test]
    public void FsmCancellationClearsControllerDestination()
    {
        GameObject root = CreateRoot(true);
        Component fsm = root.GetComponent(Type.GetType("PredatorFSM, Assembly-CSharp"));
        Component controller = root.GetComponent(Type.GetType("PredatorNavigationMovementController, Assembly-CSharp"));

        Invoke(fsm, "RequestNavigationDestination", new Vector3(2f, 0f, 0f));
        Assert.IsTrue((bool)controller.GetType().GetProperty("HasDestination").GetValue(controller));

        Invoke(fsm, "CancelNavigation");

        Assert.IsFalse((bool)controller.GetType().GetProperty("HasDestination").GetValue(controller));
    }

    [Test]
    public void FsmDoesNotTickNavigationASecondTime()
    {
        GameObject root = CreateRoot(true);
        Component fsm = root.GetComponent(Type.GetType("PredatorFSM, Assembly-CSharp"));
        Component controller = root.GetComponent(Type.GetType("PredatorNavigationMovementController, Assembly-CSharp"));
        MethodInfo tick = controller.GetType().GetMethod("TickNavigation");
        MethodInfo update = fsm.GetType().GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);

        tick.Invoke(controller, null);
        int tickCountBefore = (int)controller.GetType().GetProperty("TickCount").GetValue(controller);
        update.Invoke(fsm, null);
        int tickCountAfter = (int)controller.GetType().GetProperty("TickCount").GetValue(controller);

        Assert.That(tickCountAfter, Is.EqualTo(tickCountBefore));
    }

    private GameObject CreateRoot(bool withNavigationController)
    {
        GameObject root = new GameObject("PredatorFSMNavigationIntegrationTests_Root");
        objects.Add(root);

        Type fsmType = Type.GetType("PredatorFSM, Assembly-CSharp");
        Assert.IsNotNull(fsmType);
        if (withNavigationController)
        {
            Type controllerType = Type.GetType("PredatorNavigationMovementController, Assembly-CSharp");
            Assert.IsNotNull(controllerType);
            root.AddComponent(controllerType);
        }
        root.AddComponent(fsmType);
        return root;
    }

    private static void Invoke(Component component, string method, params object[] args)
    {
        component.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public)
            .Invoke(component, args);
    }
}
