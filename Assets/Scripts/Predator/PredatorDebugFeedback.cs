using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(PredatorFSM), typeof(PredatorPerception))]
public sealed class PredatorDebugFeedback : MonoBehaviour
{
    [SerializeField] private bool visible = true;
    private PredatorFSM agent;
    private PredatorPerception perception;
    private GUIStyle boxStyle;
    private GUIStyle titleStyle;
    private GUIStyle textStyle;

    private void Awake()
    {
        agent = GetComponent<PredatorFSM>();
        perception = GetComponent<PredatorPerception>();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
            visible = !visible;
    }

    private void OnGUI()
    {
        if (!visible || agent == null || perception == null) return;
        EnsureStyles();
        GUI.Box(new Rect(16f, 16f, 410f, 160f), GUIContent.none, boxStyle);
        GUILayout.BeginArea(new Rect(28f, 22f, 386f, 148f));
        GUILayout.Label("PREDATOR FSM", titleStyle);
        GUILayout.Label($"Estado: {agent.CurrentState}", textStyle);
        GUILayout.Label($"Percepción: {(perception.CanSeeTarget ? "VISIBLE" : "SIN CONTACTO VISUAL")}", textStyle);
        GUILayout.Label($"Alertas emitidas: {agent.AlertsSent}", textStyle);
        GUILayout.Label($"Alerta recibida: {(agent.ReceivedAlert ? "SÍ" : "NO")}", textStyle);
        GUILayout.Label(agent.HasLastKnownPosition
            ? $"Última posición: {agent.LastKnownPosition}"
            : "Última posición: ninguna", textStyle);
        GUILayout.Label(agent.CurrentTarget != null
            ? $"Objetivo: {agent.CurrentTarget.name}"
            : "Objetivo: ninguno", textStyle);
        GUILayout.EndArea();
    }

    private void EnsureStyles()
    {
        if (boxStyle != null) return;
        boxStyle = new GUIStyle(GUI.skin.box);
        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
        textStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            normal = { textColor = new Color(0.85f, 0.95f, 1f) }
        };
    }
}
