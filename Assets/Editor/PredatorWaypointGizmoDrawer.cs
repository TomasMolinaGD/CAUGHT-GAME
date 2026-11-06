using UnityEditor;
using UnityEngine;

public static class PredatorWaypointGizmoDrawer
{
    private const float MarkerRadius = 0.65f;

    [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.Active)]
    private static void DrawWaypoint(Transform transform, GizmoType gizmoType)
    {
        if (!TryGetWaypointColor(transform.name, out Color color))
        {
            return;
        }

        Color previousColor = Gizmos.color;
        Gizmos.color = color;
        Gizmos.DrawWireSphere(transform.position, MarkerRadius);
        Gizmos.DrawSphere(transform.position, 0.12f);
        Gizmos.color = previousColor;

        GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = color }
        };
        Handles.Label(transform.position + Vector3.up * 0.85f, transform.name, labelStyle);
    }

    private static bool TryGetWaypointColor(string objectName, out Color color)
    {
        if (!objectName.StartsWith("Wolf_L") || !objectName.Contains("_Waypoint_"))
        {
            color = default;
            return false;
        }

        if (objectName.StartsWith("Wolf_L1_"))
            color = new Color(0.1f, 0.85f, 1f);
        else if (objectName.StartsWith("Wolf_L2_"))
            color = new Color(0.25f, 1f, 0.35f);
        else if (objectName.StartsWith("Wolf_L3_"))
            color = new Color(1f, 0.55f, 0.12f);
        else if (objectName.StartsWith("Wolf_L4_"))
            color = new Color(0.9f, 0.25f, 1f);
        else
        {
            color = default;
            return false;
        }

        return true;
    }
}
