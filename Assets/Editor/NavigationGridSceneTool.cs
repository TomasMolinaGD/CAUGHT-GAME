using CaughtGame.Navigation;
using UnityEditor;
using UnityEngine;

public static class NavigationGridSceneTool
{
    public enum Tool { Brush, Rectangle }
    public enum Target { Walkability, Zone, Both }

    public readonly struct Settings
    {
        public readonly bool PaintMode;
        public readonly Tool ActiveTool;
        public readonly Target PaintTarget;
        public readonly bool Walkable;
        public readonly NavigationGridZone Zone;
        public readonly string ZoneId;

        public Settings(bool paintMode, Tool activeTool, Target paintTarget, bool walkable, NavigationGridZone zone, string zoneId)
        {
            PaintMode = paintMode;
            ActiveTool = activeTool;
            PaintTarget = paintTarget;
            Walkable = walkable;
            Zone = zone;
            ZoneId = zoneId;
        }
    }

    private static NavigationGrid selectedGrid;
    private static int selectedRow = -1;
    private static int selectedColumn = -1;
    private static bool isDragging;
    private static Vector2Int rectangleStart;
    private static Vector2Int lastBrushCell;
    private static bool undoRecorded;
    private static readonly Vector3[] cellCorners = new Vector3[4];

    public static bool HasSelection(NavigationGrid grid, out int row, out int column)
    {
        row = selectedRow;
        column = selectedColumn;
        return selectedGrid == grid && row >= 0 && column >= 0;
    }

    public static void DrawAndHandle(NavigationGrid grid, Settings settings)
    {
        if (grid == null) return;
        selectedGrid = grid;
        Event current = Event.current;
        if (current.type == EventType.Repaint) DrawGrid(grid);
        if (!settings.PaintMode)
        {
            if (isDragging) FinishStroke();
            else undoRecorded = false;
            return;
        }

        bool overGrid = TryGetMouseCell(grid, current.mousePosition, out int row, out int column);
        int controlId = GUIUtility.GetControlID(FocusType.Passive);
        if (current.type == EventType.Layout)
        {
            if (overGrid) HandleUtility.AddDefaultControl(controlId);
            return;
        }

        if (!overGrid)
        {
            if (current.type == EventType.MouseUp && isDragging) FinishStroke();
            return;
        }

        selectedRow = row;
        selectedColumn = column;
        if (current.type == EventType.Repaint)
        {
            DrawSelection(grid, row, column);
            if (isDragging && settings.ActiveTool == Tool.Rectangle)
                DrawRectanglePreview(grid, rectangleStart, new Vector2Int(row, column));
        }

        if (current.button != 0) return;
        if (current.type == EventType.MouseDown)
        {
            GUIUtility.hotControl = controlId;
            isDragging = true;
            undoRecorded = false;
            rectangleStart = new Vector2Int(row, column);
            lastBrushCell = rectangleStart;
            if (settings.ActiveTool == Tool.Brush) Paint(grid, row, column, settings);
            current.Use();
        }
        else if (current.type == EventType.MouseDrag && isDragging)
        {
            if (settings.ActiveTool == Tool.Brush)
            {
                PaintBrushLine(grid, lastBrushCell, new Vector2Int(row, column), settings);
                lastBrushCell = new Vector2Int(row, column);
            }
            current.Use();
        }
        else if (current.type == EventType.MouseUp && isDragging)
        {
            if (settings.ActiveTool == Tool.Rectangle)
                PaintRectangle(grid, rectangleStart, new Vector2Int(row, column), settings);
            FinishStroke();
            current.Use();
        }
    }

    private static void DrawGrid(NavigationGrid grid)
    {
        for (int row = 0; row < grid.Rows; row++)
        {
            for (int column = 0; column < grid.Columns; column++)
            {
                if (!grid.TryGetCell(row, column, out NavigationGridCell cell)) continue;
                Color zoneColor = GetZoneColor(cell.Zone);
                Color fill = cell.Walkable ? zoneColor : new Color(0.16f, 0.17f, 0.19f, 0.9f);
                FillCorners(grid, row, column, cellCorners);
                Vector3[] corners = cellCorners;
                Handles.DrawSolidRectangleWithOutline(corners, fill, cell.Walkable ? zoneColor * 1.15f : zoneColor);
                if (!cell.Walkable)
                {
                    Handles.color = zoneColor;
                    Handles.DrawLine(corners[0], corners[2]);
                    Handles.DrawLine(corners[1], corners[3]);
                }
            }
        }

        Handles.color = new Color(0f, 0f, 0f, 0.8f);
        for (int column = 0; column <= grid.Columns; column++)
        {
            Vector3 a = grid.transform.TransformPoint(new Vector3(column * grid.CellSize, 0f, 0f));
            Vector3 b = grid.transform.TransformPoint(new Vector3(column * grid.CellSize, 0f, grid.Rows * grid.CellSize));
            Handles.DrawLine(a, b);
        }
        for (int row = 0; row <= grid.Rows; row++)
        {
            Vector3 a = grid.transform.TransformPoint(new Vector3(0f, 0f, row * grid.CellSize));
            Vector3 b = grid.transform.TransformPoint(new Vector3(grid.Columns * grid.CellSize, 0f, row * grid.CellSize));
            Handles.DrawLine(a, b);
        }
    }

    private static bool TryGetMouseCell(NavigationGrid grid, Vector2 mousePosition, out int row, out int column)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
        Plane plane = new Plane(grid.transform.up, grid.transform.position);
        if (plane.Raycast(ray, out float distance) && grid.TryWorldToGrid(ray.GetPoint(distance), out row, out column)) return true;
        row = column = -1;
        return false;
    }

    private static void Paint(NavigationGrid grid, int row, int column, Settings settings)
    {
        if (!undoRecorded)
        {
            Undo.RegisterCompleteObjectUndo(grid, "Paint Navigation Grid");
            undoRecorded = true;
        }
        ApplyPaint(grid, row, column, settings);
        MarkChanged(grid);
    }

    private static void PaintRectangle(NavigationGrid grid, Vector2Int start, Vector2Int end, Settings settings)
    {
        Undo.RegisterCompleteObjectUndo(grid, "Paint Navigation Grid Rectangle");
        for (int row = Mathf.Min(start.x, end.x); row <= Mathf.Max(start.x, end.x); row++)
            for (int column = Mathf.Min(start.y, end.y); column <= Mathf.Max(start.y, end.y); column++)
                ApplyPaint(grid, row, column, settings);
        MarkChanged(grid);
    }

    private static void PaintBrushLine(NavigationGrid grid, Vector2Int start, Vector2Int end, Settings settings)
    {
        int x = start.y;
        int y = start.x;
        int targetX = end.y;
        int targetY = end.x;
        int deltaX = Mathf.Abs(targetX - x);
        int stepX = x < targetX ? 1 : -1;
        int deltaY = -Mathf.Abs(targetY - y);
        int stepY = y < targetY ? 1 : -1;
        int error = deltaX + deltaY;

        while (true)
        {
            Paint(grid, y, x, settings);
            if (x == targetX && y == targetY) break;
            int doubledError = 2 * error;
            if (doubledError >= deltaY)
            {
                error += deltaY;
                x += stepX;
            }
            if (doubledError <= deltaX)
            {
                error += deltaX;
                y += stepY;
            }
        }
    }

    private static void ApplyPaint(NavigationGrid grid, int row, int column, Settings settings)
    {
        if (settings.PaintTarget != Target.Zone) grid.SetWalkable(row, column, settings.Walkable);
        if (settings.PaintTarget != Target.Walkability)
            grid.SetZone(row, column, settings.Zone, settings.Zone == NavigationGridZone.SafeZone ? settings.ZoneId : null);
    }

    private static void MarkChanged(NavigationGrid grid)
    {
        EditorUtility.SetDirty(grid);
        PrefabUtility.RecordPrefabInstancePropertyModifications(grid);
        SceneView.RepaintAll();
    }

    private static void FinishStroke()
    {
        isDragging = false;
        undoRecorded = false;
        GUIUtility.hotControl = 0;
    }

    private static void DrawSelection(NavigationGrid grid, int row, int column)
    {
        Handles.color = Color.white;
        FillCorners(grid, row, column, cellCorners);
        Handles.DrawSolidRectangleWithOutline(cellCorners, new Color(1f, 1f, 1f, 0.12f), Color.white);
        Handles.Label(grid.GridToWorldPosition(row, column), $"Row {row}, Column {column}");
    }

    private static void DrawRectanglePreview(NavigationGrid grid, Vector2Int start, Vector2Int end)
    {
        int minRow = Mathf.Min(start.x, end.x);
        int maxRow = Mathf.Max(start.x, end.x);
        int minColumn = Mathf.Min(start.y, end.y);
        int maxColumn = Mathf.Max(start.y, end.y);
        Vector3 a = grid.transform.TransformPoint(new Vector3(minColumn * grid.CellSize, 0f, minRow * grid.CellSize));
        Vector3 b = grid.transform.TransformPoint(new Vector3((maxColumn + 1) * grid.CellSize, 0f, minRow * grid.CellSize));
        Vector3 c = grid.transform.TransformPoint(new Vector3((maxColumn + 1) * grid.CellSize, 0f, (maxRow + 1) * grid.CellSize));
        Vector3 d = grid.transform.TransformPoint(new Vector3(minColumn * grid.CellSize, 0f, (maxRow + 1) * grid.CellSize));
        cellCorners[0] = a;
        cellCorners[1] = b;
        cellCorners[2] = c;
        cellCorners[3] = d;
        Handles.DrawSolidRectangleWithOutline(cellCorners, new Color(1f, 1f, 1f, 0.15f), Color.white);
    }

    private static void FillCorners(NavigationGrid grid, int row, int column, Vector3[] corners)
    {
        float size = grid.CellSize;
        float x = column * size;
        float z = row * size;
        corners[0] = grid.transform.TransformPoint(new Vector3(x, 0f, z));
        corners[1] = grid.transform.TransformPoint(new Vector3(x + size, 0f, z));
        corners[2] = grid.transform.TransformPoint(new Vector3(x + size, 0f, z + size));
        corners[3] = grid.transform.TransformPoint(new Vector3(x, 0f, z + size));
    }

    private static Color GetZoneColor(NavigationGridZone zone)
    {
        switch (zone)
        {
            case NavigationGridZone.HuntingGround: return new Color(0.78f, 0.58f, 0.16f, 0.55f);
            case NavigationGridZone.SafeZone: return new Color(0.2f, 0.72f, 0.34f, 0.58f);
            case NavigationGridZone.Spawn: return new Color(0.16f, 0.48f, 0.9f, 0.58f);
            case NavigationGridZone.Exit: return new Color(0.62f, 0.3f, 0.78f, 0.58f);
            default: return new Color(0.36f, 0.47f, 0.56f, 0.48f);
        }
    }
}
