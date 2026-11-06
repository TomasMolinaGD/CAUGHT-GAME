using System.Collections.Generic;
using CaughtGame.Navigation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class NavigationGraphTests
{
    private readonly List<GameObject> createdObjects = new List<GameObject>();
    private NavigationGraph graph;

    [SetUp]
    public void SetUp()
    {
        graph = CreateObject("Navigation Graph").AddComponent<NavigationGraph>();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject createdObject in createdObjects)
        {
            if (createdObject != null)
            {
                Object.DestroyImmediate(createdObject);
            }
        }

        createdObjects.Clear();
    }

    [Test]
    public void EmptyGraph_HasNoNodesOrNeighbors()
    {
        Assert.That(graph.Nodes, Is.Empty);
        Assert.That(graph.Validate(), Is.Empty);
    }

    [Test]
    public void IsolatedNode_HasNoNeighbors()
    {
        NavigationNode node = CreateNode("Isolated");
        SetGraphNodes(node);

        Assert.That(graph.GetNeighbors(node, NavigationAgentType.Hunter), Is.Empty);
    }

    [Test]
    public void ValidBidirectionalConnection_IsTraversable()
    {
        NavigationNode first = CreateNode("First");
        NavigationNode second = CreateNode("Second");
        SetGraphNodes(first, second);

        Assert.That(first.TryConnectBidirectionally(second), Is.True);
        CollectionAssert.AreEqual(new[] { second }, graph.GetNeighbors(first, NavigationAgentType.Hunter));
        CollectionAssert.AreEqual(new[] { first }, graph.GetNeighbors(second, NavigationAgentType.Hunter));
        Assert.That(graph.Validate(), Is.Empty);
    }

    [Test]
    public void ConnectionApi_RejectsDuplicatesAndSelfReferences()
    {
        NavigationNode first = CreateNode("First");
        NavigationNode second = CreateNode("Second");

        Assert.That(first.TryConnectBidirectionally(second), Is.True);
        Assert.That(first.TryConnectBidirectionally(second), Is.False);
        Assert.That(first.TryConnectBidirectionally(first), Is.False);
        Assert.That(first.Connections, Has.Count.EqualTo(1));
        Assert.That(second.Connections, Has.Count.EqualTo(1));
    }

    [Test]
    public void PublicCollections_AreCachedReadOnlyViews()
    {
        NavigationNode first = CreateNode("First");
        NavigationNode second = CreateNode("Second");
        NavigationNode third = CreateNode("Third");
        SetGraphNodes(first, second, third);
        first.TryConnectBidirectionally(second);

        Assert.That(graph.Nodes, Is.SameAs(graph.Nodes));
        Assert.That(first.Connections, Is.SameAs(first.Connections));
        AssertReadOnlyCollection(graph.Nodes, third);
        AssertReadOnlyCollection(first.Connections, third);
    }

    [Test]
    public void Validation_ReportsAsymmetricConnectionWithoutRepairingIt()
    {
        NavigationNode first = CreateNode("First");
        NavigationNode second = CreateNode("Second");
        SetGraphNodes(first, second);
        SetConnections(first, second);

        Assert.That(graph.Validate(), Has.Some.Contains("asymmetric"));
        Assert.That(graph.GetNeighbors(first, NavigationAgentType.Hunter), Is.Empty);
        Assert.That(second.Connections, Is.Empty);
    }

    [Test]
    public void UnregisteredNodes_AreNotReturnedOrCosted()
    {
        NavigationNode registered = CreateNode("Registered");
        NavigationNode unregistered = CreateNode("Unregistered");
        SetGraphNodes(registered);
        registered.TryConnectBidirectionally(unregistered);

        Assert.That(graph.GetNeighbors(registered, NavigationAgentType.Hunter), Is.Empty);
        Assert.That(graph.GetNeighbors(unregistered, NavigationAgentType.Hunter), Is.Empty);
        Assert.That(graph.TryGetConnectionCost(registered, unregistered, NavigationAgentType.Hunter, out _), Is.False);
    }

    [Test]
    public void Validation_ReportsSerializedDuplicateConnectionsAndDoesNotTraverseThem()
    {
        NavigationNode first = CreateNode("First");
        NavigationNode second = CreateNode("Second");
        SetGraphNodes(first, second);
        SetConnections(first, second, second);
        SetConnections(second, first);

        Assert.That(graph.Validate(), Has.Some.Contains("duplicate connection"));
        Assert.That(graph.GetNeighbors(first, NavigationAgentType.Hunter), Is.Empty);
        Assert.That(graph.GetNeighbors(second, NavigationAgentType.Hunter), Is.Empty);
    }

    [Test]
    public void Validation_ReportsSerializedSelfConnectionsAndDoesNotTraverseThem()
    {
        NavigationNode node = CreateNode("Self Connected");
        SetGraphNodes(node);
        SetConnections(node, node);

        Assert.That(graph.Validate(), Has.Some.Contains("connects to itself"));
        Assert.That(graph.GetNeighbors(node, NavigationAgentType.Hunter), Is.Empty);
    }

    [Test]
    public void DisconnectedNodes_HaveNoPathEdgeBetweenThem()
    {
        NavigationNode first = CreateNode("First");
        NavigationNode second = CreateNode("Second");
        SetGraphNodes(first, second);

        Assert.That(graph.GetNeighbors(first, NavigationAgentType.Hunter), Is.Empty);
        Assert.That(graph.GetNeighbors(second, NavigationAgentType.Hunter), Is.Empty);
    }

    [Test]
    public void Hunter_CanEnterSafeZone()
    {
        NavigationNode normal = CreateNode("Normal");
        NavigationNode safe = CreateNode("Safe");
        SetZone(safe, NavigationZone.SafeZone);
        SetGraphNodes(normal, safe);
        normal.TryConnectBidirectionally(safe);

        CollectionAssert.AreEqual(new[] { safe }, graph.GetNeighbors(normal, NavigationAgentType.Hunter));
    }

    [Test]
    public void Wolf_CannotEnterOrTraverseSafeZone()
    {
        NavigationNode normal = CreateNode("Normal");
        NavigationNode safe = CreateNode("Safe");
        NavigationNode beyondSafe = CreateNode("Beyond Safe");
        SetZone(safe, NavigationZone.SafeZone);
        SetGraphNodes(normal, safe, beyondSafe);
        normal.TryConnectBidirectionally(safe);
        safe.TryConnectBidirectionally(beyondSafe);

        Assert.That(graph.GetNeighbors(normal, NavigationAgentType.Wolf), Is.Empty);
        Assert.That(graph.GetNeighbors(safe, NavigationAgentType.Wolf), Is.Empty);
        Assert.That(graph.GetNeighbors(beyondSafe, NavigationAgentType.Wolf), Is.Empty);
    }

    [Test]
    public void ConnectionCost_UsesVector3Distance()
    {
        NavigationNode first = CreateNode("First", new Vector3(1f, 2f, 3f));
        NavigationNode second = CreateNode("Second", new Vector3(4f, 6f, 3f));
        SetGraphNodes(first, second);
        first.TryConnectBidirectionally(second);

        Assert.That(graph.TryGetConnectionCost(first, second, NavigationAgentType.Hunter, out float cost), Is.True);
        Assert.That(cost, Is.EqualTo(Vector3.Distance(first.Position, second.Position)).Within(0.0001f));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ConnectionCost_RejectsNonTraversableOriginOrDestination(bool safeOrigin)
    {
        NavigationNode first = CreateNode("First");
        NavigationNode second = CreateNode("Second");
        SetZone(safeOrigin ? first : second, NavigationZone.SafeZone);
        SetGraphNodes(first, second);
        first.TryConnectBidirectionally(second);

        Assert.That(graph.TryGetConnectionCost(first, second, NavigationAgentType.Wolf, out _), Is.False);
    }

    private GameObject CreateObject(string objectName)
    {
        GameObject createdObject = new GameObject(objectName);
        createdObjects.Add(createdObject);
        return createdObject;
    }

    private NavigationNode CreateNode(string objectName, Vector3? position = null)
    {
        GameObject nodeObject = CreateObject(objectName);
        nodeObject.transform.position = position ?? Vector3.zero;
        return nodeObject.AddComponent<NavigationNode>();
    }

    private void SetGraphNodes(params NavigationNode[] nodes)
    {
        SerializedObject serializedGraph = new SerializedObject(graph);
        SerializedProperty nodeList = serializedGraph.FindProperty("nodes");
        nodeList.arraySize = nodes.Length;
        for (int index = 0; index < nodes.Length; index++)
        {
            nodeList.GetArrayElementAtIndex(index).objectReferenceValue = nodes[index];
        }

        serializedGraph.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetConnections(NavigationNode node, params NavigationNode[] connections)
    {
        SerializedObject serializedNode = new SerializedObject(node);
        SerializedProperty connectionList = serializedNode.FindProperty("connections");
        connectionList.arraySize = connections.Length;
        for (int index = 0; index < connections.Length; index++)
        {
            connectionList.GetArrayElementAtIndex(index).objectReferenceValue = connections[index];
        }

        serializedNode.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetZone(NavigationNode node, NavigationZone zone)
    {
        SerializedObject serializedNode = new SerializedObject(node);
        serializedNode.FindProperty("zone").enumValueIndex = (int)zone;
        serializedNode.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssertReadOnlyCollection<T>(IReadOnlyList<T> values, T valueToAdd)
    {
        Assert.That(values, Is.Not.InstanceOf<List<T>>());
        Assert.Throws<System.InvalidCastException>(() => CastToList(values));

        ICollection<T> collection = (ICollection<T>)values;
        Assert.That(collection.IsReadOnly, Is.True);
        Assert.Throws<System.NotSupportedException>(() => collection.Add(valueToAdd));

        IList<T> list = (IList<T>)values;
        Assert.That(list.IsReadOnly, Is.True);
        Assert.Throws<System.NotSupportedException>(() => list.Add(valueToAdd));
    }

    private static List<T> CastToList<T>(IReadOnlyList<T> values)
    {
        return (List<T>)values;
    }
}
