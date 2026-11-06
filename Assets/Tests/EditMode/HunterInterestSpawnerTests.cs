using System;
using System.Collections.Generic;
using System.Reflection;
using CaughtGame.Navigation;
using NUnit.Framework;
using UnityEngine;

public sealed class HunterInterestSpawnerTests
{
    private readonly List<GameObject> createdObjects = new List<GameObject>();
    private NavigationGrid grid;
    private Component spawner;

    [SetUp]
    public void SetUp()
    {
        GameObject gridObject = CreateObject("HunterInterestSpawnerTests.Grid");
        grid = gridObject.AddComponent<NavigationGrid>();
        Assert.That(grid.TryApplyGridSize(3, 3, 4f, true, out _), Is.True);

        GameObject spawnerObject = CreateObject("HunterInterestSpawnerTests.Spawner");
        spawnerObject.transform.position = new Vector3(2f, 0f, 2f);
        spawner = spawnerObject.AddComponent(FindProjectType("HunterInterestSpawner"));
        SetField("navigationGrid", grid);
        SetField("areaCenter", new Vector2(6f, 2f));
        SetField("areaHalfExtents", Vector2.one);
        SetField("spawnHeight", 1.05f);
        SetField("clearanceRadius", 0.25f);
        SetField("placementAttempts", 4);
        SetField("blockedLayers", (LayerMask)(1 << 12));
    }

    [TearDown]
    public void TearDown()
    {
        for (int index = createdObjects.Count - 1; index >= 0; index--)
        {
            UnityEngine.Object.DestroyImmediate(createdObjects[index]);
        }

        createdObjects.Clear();
    }

    [Test]
    public void CandidateInsideReachableWalkableCell_IsAccepted()
    {
        Assert.That(TryFindSpawnPosition(out Vector3 position), Is.True);
        Assert.That(grid.TryWorldToGrid(position, out int row, out int column), Is.True);
        Assert.That(grid.TryGetCell(row, column, out NavigationGridCell cell), Is.True);
        Assert.That(cell.Walkable, Is.True);
    }

    [Test]
    public void CandidateOutsideGrid_IsRejected()
    {
        SetField("areaCenter", new Vector2(20f, 20f));

        Assert.That(TryFindSpawnPosition(out _), Is.False);
    }

    [Test]
    public void CandidateInsideBlockedPhysicsVolume_IsRejected()
    {
        GameObject obstacle = CreateObject("HunterInterestSpawnerTests.Obstacle");
        obstacle.layer = 12;
        obstacle.transform.position = new Vector3(6f, 1.05f, 2f);
        BoxCollider collider = obstacle.AddComponent<BoxCollider>();
        collider.size = new Vector3(4f, 4f, 4f);
        Physics.SyncTransforms();

        Assert.That(TryFindSpawnPosition(out _), Is.False);
    }

    [Test]
    public void CandidateInDisconnectedWalkableCell_IsRejected()
    {
        Assert.That(grid.TryApplyGridSize(3, 1, 4f, true, out _), Is.True);
        Assert.That(grid.SetWalkable(0, 1, false), Is.True);
        SetField("areaCenter", new Vector2(10f, 2f));

        Assert.That(TryFindSpawnPosition(out _), Is.False);
    }

    [Test]
    public void MissingGrid_PreservesLegacyRectangleAndPhysicsValidation()
    {
        SetField("navigationGrid", null);

        Assert.That(TryFindSpawnPosition(out Vector3 position), Is.True);
        Assert.That(position.x, Is.InRange(5f, 7f));
        Assert.That(position.z, Is.InRange(1f, 3f));
    }

    private bool TryFindSpawnPosition(out Vector3 position)
    {
        MethodInfo method = spawner.GetType().GetMethod(
            "TryFindSpawnPosition",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        object[] arguments = { Vector3.zero };
        bool result = (bool)method.Invoke(spawner, arguments);
        position = (Vector3)arguments[0];
        return result;
    }

    private void SetField(string fieldName, object value)
    {
        FieldInfo field = spawner.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing field {fieldName}");
        field.SetValue(spawner, value);
    }

    private GameObject CreateObject(string name)
    {
        var gameObject = new GameObject(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private static Type FindProjectType(string fullName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(fullName);
            if (type != null)
            {
                return type;
            }
        }

        throw new InvalidOperationException($"Could not find project type {fullName}.");
    }
}
