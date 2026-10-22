using CaughtGame.Navigation;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(NavigationGridTestAgent))]
public sealed class NavigationGridTestAgentEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();

        NavigationGridTestAgent testAgent = (NavigationGridTestAgent)target;
        PathFollower follower = testAgent.Follower;
        if (follower == null)
        {
            EditorGUILayout.HelpBox("The required PathFollower component is missing.", MessageType.Warning);
            return;
        }

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Request Path To Configured Cell"))
            {
                NavigationPathResult result = testAgent.RequestPathToConfiguredCell();
                if (result == null || result.Status != NavigationPathStatus.Success)
                {
                    Debug.LogWarning($"{testAgent.name}: path request failed ({result?.Status.ToString() ?? "No grid assigned"}).", testAgent);
                }
                else
                {
                    Debug.Log($"{testAgent.name}: path started; cost {result.TotalCost:F2}, {result.StepCount} steps.", testAgent);
                }
            }

            if (GUILayout.Button("Stop"))
            {
                follower.Stop();
            }
        }

        string state = follower.IsMoving ? "Moving" : follower.IsComplete ? "Complete" : "Stopped";
        EditorGUILayout.LabelField("Movement state", state);
        if (follower.HasPath)
        {
            EditorGUILayout.Vector3Field("Current destination", follower.CurrentDestination);
        }
    }
}
