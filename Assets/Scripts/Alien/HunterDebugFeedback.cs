using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(FSMAgent), typeof(HunterPerception), typeof(HunterCombat))]
public sealed class HunterDebugFeedback : MonoBehaviour
{
    [SerializeField] private bool visible = true;
    [SerializeField, Min(240f)] private float panelWidth = 340f;

    private FSMAgent agent;
    private HunterPerception perception;
    private HunterCombat combat;
    private HunterInterestSpawner interestSpawner;
    private PoliceState previousState;
    private string latestTransition = "Inicio -> Patrol";
    private float transitionDisplayRemaining;
    private GUIStyle panelStyle;
    private GUIStyle titleStyle;
    private GUIStyle labelStyle;
    private GUIStyle accentStyle;

    private void Awake()
    {
        agent = GetComponent<FSMAgent>();
        perception = GetComponent<HunterPerception>();
        combat = GetComponent<HunterCombat>();
        interestSpawner = GetComponent<HunterInterestSpawner>();
        previousState = agent.CurrentState;
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
        {
            visible = !visible;
        }

        PoliceState currentState = agent.CurrentState;
        if (currentState != previousState)
        {
            latestTransition = $"{previousState} -> {currentState}";
            previousState = currentState;
            transitionDisplayRemaining = 2.5f;
        }

        transitionDisplayRemaining = Mathf.Max(
            0f,
            transitionDisplayRemaining - Time.deltaTime);
    }

    private void OnGUI()
    {
        if (!visible || agent == null)
        {
            return;
        }

        EnsureStyles();
        const float panelHeight = 224f;
        Rect panelRect = new Rect(16f, 16f, panelWidth, panelHeight);
        GUI.Box(panelRect, GUIContent.none, panelStyle);

        GUILayout.BeginArea(new Rect(
            panelRect.x + 14f,
            panelRect.y + 10f,
            panelRect.width - 28f,
            panelRect.height - 20f));
        GUILayout.Label("NPC CAZADOR - FSM", titleStyle);
        GUILayout.Label($"Estado: {GetStateLabel(agent.CurrentState)}", accentStyle);
        GUILayout.Label($"Accion: {GetActionLabel(agent.CurrentState)}", labelStyle);
        GUILayout.Label(GetTargetLabel(), labelStyle);
        GUILayout.Label(
            $"Detectados: {perception.LivingBoidCount} vivos / " +
            $"{perception.DeadBoidCount} eliminados",
            labelStyle);
        GUILayout.Label(
            $"TBA restante: {combat.CooldownRemaining:0.0}s | " +
            $"Intereses: {interestSpawner.ActiveInterestCount}/5",
            labelStyle);

        if (transitionDisplayRemaining > 0f)
        {
            GUILayout.Label($"Cambio: {latestTransition}", accentStyle);
        }

        GUILayout.FlexibleSpace();
        GUILayout.Label("F1: mostrar/ocultar diagnostico", labelStyle);
        GUILayout.EndArea();
    }

    private string GetTargetLabel()
    {
        BoidLife target = agent.CurrentTarget;
        if (target == null)
        {
            return "Objetivo: ninguno";
        }

        return target.IsAlive
            ? $"Objetivo: {target.name} ({target.CurrentLife:0}/{target.MaximumLife:0} HP)"
            : $"Objetivo: {target.name} (eliminado)";
    }

    private static string GetStateLabel(PoliceState state)
    {
        return state switch
        {
            PoliceState.Patrol => "PATROL",
            PoliceState.Pursuit => "PURSUIT",
            PoliceState.Attack => "ATTACK",
            PoliceState.Gather => "GATHER",
            _ => state.ToString().ToUpperInvariant()
        };
    }

    private static string GetActionLabel(PoliceState state)
    {
        return state switch
        {
            PoliceState.Patrol => "Recorriendo waypoints",
            PoliceState.Pursuit => "Persiguiendo al mismo objetivo",
            PoliceState.Attack => "Ejecutando un ataque",
            PoliceState.Gather => "Recolectando un agente eliminado",
            _ => "Sin accion"
        };
    }

    private void EnsureStyles()
    {
        if (panelStyle != null)
        {
            return;
        }

        panelStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(12, 12, 10, 10)
        };
        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            normal = { textColor = new Color(0.88f, 0.92f, 1f) }
        };
        accentStyle = new GUIStyle(labelStyle)
        {
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.2f, 0.9f, 1f) }
        };
    }

    private void OnValidate()
    {
        panelWidth = Mathf.Max(240f, panelWidth);
    }
}
