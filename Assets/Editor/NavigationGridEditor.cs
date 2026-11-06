using CaughtGame.Navigation;
using System;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(NavigationGrid))]
public sealed class NavigationGridEditor : Editor
{
    private enum PaintTool { Brush, Rectangle }
    private enum PaintTarget { Walkability, Zone, Both }

    private NavigationGrid grid;
    private int columns;
    private int rows;
    private float cellSize;
    private bool paintMode;
    private PaintTool paintTool;
    private PaintTarget paintTarget = PaintTarget.Both;
    private bool walkable = true;
    private NavigationGridZone zone;
    private string zoneId = string.Empty;

    private void OnEnable()
    {
        grid = (NavigationGrid)target;
        columns = grid.Columns;
        rows = grid.Rows;
        cellSize = grid.CellSize;
    }

    public override void OnInspectorGUI()
    {
        EditorGUILayout.LabelField("Navigation Grid", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("The Transform position is the grid origin; use X=414, Z=569 for the reference layout. Cell size is measured in local units; use unit scale (1, 1, 1) for four world units per cell. Rotation and position are supported.", MessageType.Info);

        EditorGUI.BeginChangeCheck();
        columns = EditorGUILayout.IntField("Columns (draft)", columns);
        rows = EditorGUILayout.IntField("Rows (draft)", rows);
        cellSize = EditorGUILayout.FloatField("Cell Size (draft)", cellSize);
        if (EditorGUI.EndChangeCheck()) Repaint();

        using (new EditorGUI.DisabledScope(columns == grid.Columns && rows == grid.Rows && Mathf.Approximately(cellSize, grid.CellSize)))
        {
            if (GUILayout.Button("Apply Grid Size")) ApplyGridSize();
        }

        EditorGUILayout.Space();
        paintMode = EditorGUILayout.ToggleLeft("Paint Mode", paintMode);
        using (new EditorGUI.DisabledScope(!paintMode))
        {
            paintTool = (PaintTool)EditorGUILayout.EnumPopup("Tool", paintTool);
            paintTarget = (PaintTarget)EditorGUILayout.EnumPopup("Paint", paintTarget);
            if (paintTarget != PaintTarget.Zone)
            {
                WalkableLabel state = (WalkableLabel)EditorGUILayout.EnumPopup("Cell State", walkable ? WalkableLabel.Walkable : WalkableLabel.Blocked);
                walkable = state == WalkableLabel.Walkable;
            }
            if (paintTarget != PaintTarget.Walkability)
            {
                zone = (NavigationGridZone)EditorGUILayout.EnumPopup("Zone", zone);
                if (zone == NavigationGridZone.SafeZone)
                    zoneId = EditorGUILayout.TextField("Zone ID", zoneId);
            }
        }

        if (NavigationGridSceneTool.HasSelection(grid, out int selectedRow, out int selectedColumn))
            EditorGUILayout.LabelField("Selected Cell", $"Row {selectedRow}, Column {selectedColumn}");
        else
            EditorGUILayout.LabelField("Selected Cell", "None");

        if (GUI.changed) SceneView.RepaintAll();
    }

    private void OnSceneGUI()
    {
        var settings = new NavigationGridSceneTool.Settings(
            paintMode, (NavigationGridSceneTool.Tool)paintTool, (NavigationGridSceneTool.Target)paintTarget,
            walkable, zone, zoneId);
        NavigationGridSceneTool.DrawAndHandle(grid, settings);
    }

    private void ApplyGridSize()
    {
        if (columns < 1 || rows < 1 || cellSize <= 0f || float.IsNaN(cellSize) || float.IsInfinity(cellSize) || (long)columns * rows > 1000000)
        {
            EditorUtility.DisplayDialog("Invalid grid size", "Dimensions and cell size must be positive and within the supported cell limit.", "OK");
            return;
        }

        int discarded = grid.CountCellsToDiscard(columns, rows);
        if (discarded > 0 && !EditorUtility.DisplayDialog(
                "Discard grid cells?",
                $"This resize removes {discarded} cell records outside the new dimensions. Cells that remain in range will be preserved.",
                "Apply Resize", "Cancel")) return;

        Undo.RegisterCompleteObjectUndo(grid, "Resize Navigation Grid");
        if (!grid.TryApplyGridSize(columns, rows, cellSize, true, out _))
        {
            EditorUtility.DisplayDialog("Invalid grid size", "Dimensions and cell size must be positive and within the supported cell limit.", "OK");
            return;
        }

        EditorUtility.SetDirty(grid);
        PrefabUtility.RecordPrefabInstancePropertyModifications(grid);
        SceneView.RepaintAll();
    }

    private enum WalkableLabel { Walkable, Blocked }
}
