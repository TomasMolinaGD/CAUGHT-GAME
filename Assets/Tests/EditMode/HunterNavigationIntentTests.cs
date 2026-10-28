using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class HunterNavigationIntentTests
{
    private Type intentType;
    private Type requestType;
    private object intent;

    [SetUp]
    public void SetUp()
    {
        intentType = FindProjectType("HunterNavigationIntent");
        requestType = FindProjectType("HunterNavigationRequest");
        intent = Activator.CreateInstance(intentType);
    }

    [Test]
    public void RequestNavigationCreatesInspectableActiveRequest()
    {
        ulong id = (ulong)Invoke("RequestNavigation", new Vector3(4f, 0f, 8f), 0.5f, 2f);

        Assert.That(GetProperty<bool>("HasActiveRequest"), Is.True);
        object request = GetCurrentRequest(out bool available);
        Assert.That(available, Is.True);
        Assert.That(GetRequestProperty<ulong>(request, "Id"), Is.EqualTo(id));
        Assert.That(GetRequestProperty<Vector3>(request, "Destination"), Is.EqualTo(new Vector3(4f, 0f, 8f)));
        Assert.That(GetRequestProperty<float>(request, "StoppingDistance"), Is.EqualTo(0.5f));
        Assert.That(GetRequestProperty<float>(request, "SpeedMultiplier"), Is.EqualTo(2f));
    }

    [Test]
    public void ReplaceNavigationRequiresCurrentRequestId()
    {
        ulong firstId = (ulong)Invoke("RequestNavigation", Vector3.right, 0.5f, 1f);
        object[] arguments = { firstId, Vector3.forward, 1f, 1.5f, null };
        bool replaced = (bool)Invoke("ReplaceNavigation", arguments);

        Assert.That(replaced, Is.True);
        ulong secondId = (ulong)arguments[4];
        Assert.That(secondId, Is.Not.EqualTo(firstId));

        object[] staleArguments = { firstId, Vector3.left, 1f, 1f, null };
        Assert.That((bool)Invoke("ReplaceNavigation", staleArguments), Is.False);
        object current = GetCurrentRequest(out bool available);
        Assert.That(available, Is.True);
        Assert.That(GetRequestProperty<ulong>(current, "Id"), Is.EqualTo(secondId));
        Assert.That(GetRequestProperty<Vector3>(current, "Destination"), Is.EqualTo(Vector3.forward));
    }

    [Test]
    public void CancelNavigationCannotCancelAReplacedRequestWithAnOldId()
    {
        ulong firstId = (ulong)Invoke("RequestNavigation", Vector3.right, 0.5f, 1f);
        object[] replaceArguments = { firstId, Vector3.forward, 0.5f, 1f, null };
        Assert.That((bool)Invoke("ReplaceNavigation", replaceArguments), Is.True);
        ulong currentId = (ulong)replaceArguments[4];

        Assert.That((bool)Invoke("CancelNavigation", firstId), Is.False);
        Assert.That(GetProperty<bool>("HasActiveRequest"), Is.True);
        Assert.That((bool)Invoke("CancelNavigation", currentId), Is.True);
        Assert.That(GetProperty<bool>("HasActiveRequest"), Is.False);
    }

    [Test]
    public void CancelAllClearsTheCurrentIntent()
    {
        Invoke("RequestNavigation", Vector3.right, 0f, 1f);

        Invoke("CancelAll");

        Assert.That(GetProperty<bool>("HasActiveRequest"), Is.False);
        Assert.That(GetCurrentRequest(out bool available), Is.Null);
        Assert.That(available, Is.False);
    }

    private object GetCurrentRequest(out bool available)
    {
        MethodInfo method = intentType.GetMethod("TryGetCurrent");
        object[] arguments = { null };
        available = (bool)method.Invoke(intent, arguments);
        return available ? arguments[0] : null;
    }

    private object Invoke(string methodName, params object[] arguments)
    {
        MethodInfo method = intentType.GetMethod(methodName);
        Assert.That(method, Is.Not.Null, "Missing method " + methodName + ".");
        return method.Invoke(intent, arguments);
    }

    private T GetProperty<T>(string propertyName) =>
        (T)intentType.GetProperty(propertyName).GetValue(intent);

    private static T GetRequestProperty<T>(object request, string propertyName) =>
        (T)requestTypeCache.GetProperty(propertyName).GetValue(request);

    private static Type requestTypeCache;

    private static Type FindProjectType(string typeName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(typeName, false);
            if (type != null)
            {
                if (typeName == "HunterNavigationRequest") requestTypeCache = type;
                return type;
            }
        }

        Assert.Fail("Could not locate project type " + typeName + ".");
        return null;
    }
}
