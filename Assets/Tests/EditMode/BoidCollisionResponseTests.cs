using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class BoidCollisionResponseTests
{
    private GameObject boidObject;
    private GameObject wallObject;
    private Component agent;
    private Rigidbody body;
    private SimulationMode originalSimulationMode;

    [SetUp]
    public void SetUp()
    {
        originalSimulationMode = Physics.simulationMode;
        Physics.simulationMode = SimulationMode.Script;
        boidObject = new GameObject("Collision Response Boid");
        body = boidObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        CapsuleCollider capsule = boidObject.AddComponent<CapsuleCollider>();
        capsule.radius = 0.5f;
        capsule.height = 2f;
        agent = boidObject.AddComponent(FindProjectType("BoidAgent"));
        SetPrivateField(agent, "body", body);
        SetPrivateField(agent, "capsule", capsule);
        SetPrivateField(agent, "environmentLayers", (LayerMask)(1 << 12));
        SetPrivateField(agent, "collisionSkin", 0.04f);
        SetPrivateField(agent, "maximumSlideIterations", 3);
        SetVelocity(Vector3.zero);
    }

    [TearDown]
    public void TearDown()
    {
        if (wallObject != null) UnityEngine.Object.DestroyImmediate(wallObject);
        if (boidObject != null) UnityEngine.Object.DestroyImmediate(boidObject);
        Physics.simulationMode = originalSimulationMode;
    }

    [Test]
    public void FreeMovement_PreservesVelocityAndDisplacement()
    {
        SetVelocity(new Vector3(1f, 0f, 0f));

        InvokeMove(new Vector3(1f, 0f, 0f));
        Physics.SyncTransforms();
        Physics.Simulate(Time.fixedDeltaTime);

        Assert.That(body.position.x, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(GetVelocity().x, Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void FrontalCollisionRemovesPenetratingVelocity()
    {
        CreateWall(new Vector3(1.5f, 1f, 0f), new Vector3(0.2f, 2f, 4f));
        SetVelocity(new Vector3(2f, 0f, 0f));

        InvokeMove(new Vector3(2f, 0f, 0f));

        Assert.That(GetVelocity().x, Is.GreaterThanOrEqualTo(0f));
        Assert.That(body.position.x, Is.LessThan(1.01f));
    }

    [Test]
    public void DiagonalCollisionPreservesTangentialVelocity()
    {
        CreateWall(new Vector3(1.5f, 1f, 0f), new Vector3(0.2f, 2f, 4f));
        SetVelocity(new Vector3(2f, 0f, 1f));

        InvokeMove(new Vector3(2f, 0f, 1f));

        Assert.That(GetVelocity().x, Is.GreaterThanOrEqualTo(0f));
        Assert.That(GetVelocity().z, Is.GreaterThan(0f));
    }

    [Test]
    public void CornerCollisionDoesNotProduceInvalidVelocity()
    {
        CreateWall(new Vector3(1.5f, 1f, 0f), new Vector3(0.2f, 2f, 4f));
        GameObject secondWall = new GameObject("Collision Response Corner");
        secondWall.layer = 12;
        secondWall.transform.position = new Vector3(0f, 1f, 1.5f);
        BoxCollider collider = secondWall.AddComponent<BoxCollider>();
        collider.size = new Vector3(4f, 2f, 0.2f);
        Physics.SyncTransforms();

        SetVelocity(new Vector3(2f, 0f, 2f));
        InvokeMove(new Vector3(2f, 0f, 2f));

        Vector3 velocity = GetVelocity();
        Assert.That(float.IsNaN(velocity.x) || float.IsInfinity(velocity.x), Is.False);
        Assert.That(float.IsNaN(velocity.z) || float.IsInfinity(velocity.z), Is.False);
        UnityEngine.Object.DestroyImmediate(secondWall);
    }

    private void CreateWall(Vector3 position, Vector3 size)
    {
        wallObject = new GameObject("Collision Response Wall");
        wallObject.layer = 12;
        wallObject.transform.position = position;
        BoxCollider wall = wallObject.AddComponent<BoxCollider>();
        wall.size = size;
        Physics.SyncTransforms();
    }

    private void InvokeMove(Vector3 displacement)
    {
        MethodInfo method = agent.GetType().GetMethod(
            "MoveWithEnvironmentCollision", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(agent, new object[] { displacement });
    }

    private Vector3 GetVelocity() => (Vector3)agent.GetType().GetProperty("Velocity").GetValue(agent);

    private void SetVelocity(Vector3 value)
    {
        SetPrivateField(agent, "<Velocity>k__BackingField", value);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
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
}
