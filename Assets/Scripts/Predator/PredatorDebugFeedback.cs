using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(PredatorFSM), typeof(PredatorPerception))]
public sealed class PredatorDebugFeedback : MonoBehaviour
{
    [SerializeField] private bool visible;
    [SerializeField, Min(0.5f)] private float alertDisplayDuration = 2.5f;

    private PredatorFSM agent;
    private PredatorPerception perception;
    private int previousAlertsSent;
    private bool previousReceivedAlert;
    private float alertDisplayRemaining;
    private string alertMessage;
    private GUIStyle boxStyle;
    private GUIStyle titleStyle;
    private GUIStyle textStyle;
    private GUIStyle alertStyle;

    private void Awake()
    {
        agent = GetComponent<PredatorFSM>();
        perception = GetComponent<PredatorPerception>();
        previousAlertsSent = agent.AlertsSent;
        previousReceivedAlert = agent.ReceivedAlert;
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame)
            visible = !visible;

        if (agent.AlertsSent > previousAlertsSent)
        {
            ShowAlert("AULLIDO EMITIDO");
        }
        else if (agent.ReceivedAlert && !previousReceivedAlert)
        {
            ShowAlert("ALERTA RECIBIDA");
        }

        previousAlertsSent = agent.AlertsSent;
        previousReceivedAlert = agent.ReceivedAlert;
        alertDisplayRemaining = Mathf.Max(0f, alertDisplayRemaining - Time.deltaTime);
    }

    private void OnGUI()
    {
        if (agent == null || perception == null) return;
        EnsureStyles();

        const float panelWidth = 286f;
        float panelHeight = visible ? 158f : 58f;
        float panelX = Mathf.Max(16f, Screen.width - panelWidth - 16f);
        float panelY = 16f + GetPanelIndex() * (panelHeight + 8f);
        Rect panelRect = new Rect(panelX, panelY, panelWidth, panelHeight);
        GUI.Box(panelRect, GUIContent.none, boxStyle);

        GUILayout.BeginArea(new Rect(
            panelRect.x + 10f,
            panelRect.y + 6f,
            panelRect.width - 20f,
            panelRect.height - 12f));
        GUILayout.Label($"{GetWolfLabel()} - {agent.CurrentState}", titleStyle);
        if (alertDisplayRemaining > 0f)
            GUILayout.Label(alertMessage, alertStyle);
        else
            GUILayout.Label(perception.CanSeeTarget ? "CONTACTO VISUAL" : "SIN CONTACTO", textStyle);

        if (!visible)
        {
            GUILayout.EndArea();
            return;
        }

        GUILayout.Label($"Percepción: {(perception.CanSeeTarget ? "VISIBLE" : "SIN CONTACTO VISUAL")}", textStyle);
        GUILayout.Label($"Alertas emitidas: {agent.AlertsSent}", textStyle);
        GUILayout.Label($"Alerta recibida: {(agent.ReceivedAlert ? "SÍ" : "NO")}", textStyle);
        GUILayout.Label(agent.HasLastKnownPosition
            ? $"Última posición: {agent.LastKnownPosition}"
            : "Última posición: ninguna", textStyle);
        GUILayout.Label(agent.CurrentTarget != null
            ? $"Objetivo: {agent.CurrentTarget.name}"
            : "Objetivo: ninguno", textStyle);
        GUILayout.Label("F2: mostrar/ocultar diagnostico", textStyle);
        GUILayout.EndArea();
    }

    private void ShowAlert(string message)
    {
        alertMessage = message;
        alertDisplayRemaining = alertDisplayDuration;
    }

    private int GetPanelIndex()
    {
        string objectName = gameObject.name;
        if (objectName.Contains("_L2_")) return 1;
        if (objectName.Contains("_L3_")) return 2;
        if (objectName.Contains("_L4_")) return 3;
        return 0;
    }

    private string GetWolfLabel()
    {
        string objectName = gameObject.name;
        if (objectName.Contains("_L1_")) return "WOLF L1";
        if (objectName.Contains("_L2_")) return "WOLF L2";
        if (objectName.Contains("_L3_")) return "WOLF L3";
        if (objectName.Contains("_L4_")) return "WOLF L4";
        return "WOLF";
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
        alertStyle = new GUIStyle(textStyle)
        {
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(1f, 0.78f, 0.18f) }
        };
    }

    private void OnValidate()
    {
        alertDisplayDuration = Mathf.Max(0.5f, alertDisplayDuration);
    }
}
