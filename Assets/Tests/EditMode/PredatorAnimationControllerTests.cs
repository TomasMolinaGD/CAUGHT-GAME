using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public sealed class PredatorAnimationControllerTests
{
    private const string LegacyControllerPath = "Assets/Animations/Predator/Predator.controller";
    private const string WolfControllerPath =
        "Assets/Animations/Predator/Predator_WolfIntegration.controller";

    private readonly List<GameObject> objects = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        for (int i = objects.Count - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(objects[i]);
    }

    [Test]
    public void ZeroVelocitySelectsIdleWithoutMovingTransform()
    {
        Component controller = CreateController(WolfControllerPath, out Animator animator);
        Vector3 before = controller.transform.position;

        InvokeSetMovementVelocity(controller, Vector3.zero);

        Assert.That(animator.GetFloat("Y"), Is.Zero);
        Assert.That(animator.GetFloat("LocomotionPlayback"), Is.EqualTo(1f).Within(0.001f));
        Assert.That(controller.transform.position, Is.EqualTo(before));
        Assert.IsFalse(animator.applyRootMotion);
    }

    [Test]
    public void PatrolPlaybackIsLowerThanChasePlayback()
    {
        Component controller = CreateController(WolfControllerPath, out Animator animator);
        Vector3 before = controller.transform.position;

        InvokeSetMovementVelocity(controller, Vector3.right * 2.85f);
        float patrolPlayback = animator.GetFloat("LocomotionPlayback");
        InvokeSetMovementVelocity(controller, Vector3.right * 5.4f);
        float chasePlayback = animator.GetFloat("LocomotionPlayback");

        Assert.That(animator.GetFloat("Y"), Is.EqualTo(1f).Within(0.001f));
        Assert.That(patrolPlayback, Is.EqualTo(2.85f / 5.4f).Within(0.001f));
        Assert.That(chasePlayback, Is.EqualTo(1f).Within(0.001f));
        Assert.That(patrolPlayback, Is.LessThan(chasePlayback));
        Assert.That(controller.transform.position, Is.EqualTo(before));
    }

    [Test]
    public void MovingPlaybackUsesConfiguredMinimum()
    {
        Component controller = CreateController(WolfControllerPath, out Animator animator);

        InvokeSetMovementVelocity(controller, Vector3.right * 0.05f);

        Assert.That(animator.GetFloat("Y"), Is.EqualTo(1f).Within(0.001f));
        Assert.That(animator.GetFloat("LocomotionPlayback"), Is.EqualTo(0.35f).Within(0.001f));
    }

    [Test]
    public void LegacyControllerWithoutPlaybackParameterRemainsSupported()
    {
        Component controller = CreateController(LegacyControllerPath, out Animator animator);

        Assert.DoesNotThrow(() => InvokeSetMovementVelocity(controller, Vector3.right * 2.85f));

        Assert.That(animator.GetFloat("Y"), Is.EqualTo(1f).Within(0.001f));
        Assert.IsFalse(HasParameter(animator, "LocomotionPlayback"));
        Assert.IsFalse(animator.applyRootMotion);
    }

    [Test]
    public void PlaybackParameterAffectsLocomotionStateOnly()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(WolfControllerPath);
        Assert.IsNotNull(controller);
        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState locomotion = FindState(stateMachine, "Blend Tree");
        AnimatorState attack = FindState(stateMachine, "Attack");

        Assert.IsTrue(locomotion.speedParameterActive);
        Assert.That(locomotion.speedParameter, Is.EqualTo("LocomotionPlayback"));
        Assert.IsFalse(attack.speedParameterActive);
        Assert.That(attack.speed, Is.EqualTo(1f));
    }

    [Test]
    public void AttackIgnoresLocomotionPlaybackUpdates()
    {
        Component controller = CreateController(WolfControllerPath, out Animator animator);

        InvokeSetAttacking(controller, true);
        InvokeSetMovementVelocity(controller, Vector3.right * 2.85f);

        Assert.IsTrue(animator.GetBool("pAttack"));
        Assert.That(animator.GetFloat("LocomotionPlayback"), Is.EqualTo(1f).Within(0.001f));
    }

    private Component CreateController(string controllerPath, out Animator animator)
    {
        RuntimeAnimatorController runtimeController =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
        Assert.IsNotNull(runtimeController, controllerPath);

        GameObject root = new GameObject("PredatorAnimationControllerTests_Root");
        objects.Add(root);
        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform);
        animator = visual.AddComponent<Animator>();
        animator.runtimeAnimatorController = runtimeController;

        Type controllerType = Type.GetType("PredatorAnimationController, Assembly-CSharp");
        Assert.IsNotNull(controllerType);
        Component controller = root.AddComponent(controllerType);
        SetPrivateField(controller, "locomotionPlaybackDamping", 0f);
        controllerType.GetMethod("ConfigureAnimator", BindingFlags.Public | BindingFlags.Instance)
            .Invoke(controller, new object[] { animator });
        return controller;
    }

    private static void InvokeSetMovementVelocity(Component controller, Vector3 velocity)
    {
        MethodInfo method = controller.GetType().GetMethod(
            "SetMovementVelocity", BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(method, "PredatorAnimationController must expose SetMovementVelocity.");
        method.Invoke(controller, new object[] { velocity });
    }

    private static void InvokeSetAttacking(Component controller, bool attacking)
    {
        MethodInfo method = controller.GetType().GetMethod(
            "SetAttacking", BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(method, "PredatorAnimationController must expose SetAttacking.");
        method.Invoke(controller, new object[] { attacking });
    }

    private static AnimatorState FindState(AnimatorStateMachine stateMachine, string name)
    {
        foreach (ChildAnimatorState childState in stateMachine.states)
            if (childState.state.name == name) return childState.state;
        Assert.Fail($"Animator state not found: {name}");
        return null;
    }

    private static bool HasParameter(Animator animator, string name)
    {
        foreach (AnimatorControllerParameter parameter in animator.parameters)
            if (parameter.name == name) return true;
        return false;
    }

    private static void SetPrivateField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, name);
        field.SetValue(target, value);
    }
}
