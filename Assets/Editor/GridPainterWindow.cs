using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Paint map cells in the Scene view.
///   Brush : click or drag to paint a free-hand line (no gaps, even on fast drags)
///   Line  : drag from A to B, release to fill the whole line
///   Rect  : drag from A to B, release to fill the whole rectangle
/// Right-click (or Shift+click) erases.
/// </summary>
public class GridPainterWindow : EditorWindow
{
    enum Mode { Brush, Line, Rect }

    static bool painting = true;
    static int brushSize = 1;
    static Mode mode = Mode.Brush;
    static TileType brush = TileType.Solid;
    static GameObject prefab;

    static bool dragging;
    static bool erasing;
    static Vector2Int dragStart;
    static Vector2Int dragCurrent;
    static Vector2Int lastCell;
    static bool hasLast;

    GameObject prefabField;

    [MenuItem("Tools/Grid Painter")]
    public static void Open()
    {
        var w = GetWindow<GridPainterWindow>("Grid Painter");
        w.minSize = new Vector2(280f, 260f);
    }

    void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Brush: click / drag to paint a line (release the mouse to stop).\n" +
            "Line / Rect: drag from A to B and release to fill.\n" +
            "Right-click or Shift+click erases.", MessageType.Info);

        painting = EditorGUILayout.ToggleLeft("Paint mode  (Esc in scene view exits)", painting);
        mode = (Mode)GUILayout.Toolbar((int)mode, new[] { "Brush", "Line", "Rect" });
        brushSize = EditorGUILayout.IntSlider("Brush size", brushSize, 1, 6);
        brush = (TileType)EditorGUILayout.EnumPopup("Tile", brush);

        prefabField = (GameObject)EditorGUILayout.ObjectField("Tile prefab", prefabField, typeof(GameObject), false);
        if (prefabField == null) prefabField = AssetDatabase.LoadAssetAtPath<GameObject>(GridSceneUtil.PrefabPath);
        prefab = prefabField;

        EditorGUILayout.Space();
        if (GUILayout.Button("Snap all tiles to grid")) GridSceneUtil.SnapAll();
        if (GUILayout.Button("Erase one tile")) { }
    }

    static GameObject ResolvePrefab()
    {
        if (prefab != null) return prefab;
        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GridSceneUtil.PrefabPath);
        return prefab;
    }

    /// <summary>Free-hand brush: paints an N x N block per step.</summary>
    static void PaintBrush(Vector2Int center, GameObject p)
    {
        int n = Mathf.Max(1, brushSize);
        int off = (n - 1) / 2;
        for (int x = 0; x < n; x++)
        {
            for (int y = 0; y < n; y++)
            {
                GridSceneUtil.Place(new Vector2Int(center.x - off + x, center.y - off + y), brush, p);
            }
        }
    }

    static void DrawHint(SceneView view)
    {
        Handles.BeginGUI();
        var rect = new Rect(10f, 10f, 320f, 22f);
        var old = GUI.color;
        GUI.color = new Color(0.11f, 0.80f, 0.74f, 0.95f);
        string size = brushSize > 1 ? "  " + brushSize + "x" + brushSize : "";
        GUI.Box(rect, "PAINT: " + brush + size + "   -   Esc to exit");
        GUI.color = old;
        Handles.EndGUI();
    }

    static void OnSceneGUI(SceneView view)
    {
        DrawGrid();

        Event e = Event.current;
        if (e == null) return;

        // Esc leaves paint mode so the scene view behaves normally again
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape && painting)
        {
            painting = false;
            view.Repaint();
            e.Use();
            return;
        }

        if (!painting) return;
        DrawHint(view);

        if (e.alt) return; // let Alt+drag orbit the scene view

        Vector2Int cell = CellUnderMouse(e.mousePosition);

        switch (e.type)
        {
            case EventType.MouseDown:
                if (e.button == 1 || (e.button == 0 && e.shift))
                {
                    erasing = true;
                    dragging = true;
                    dragStart = cell;
                    hasLast = false;
                    GridSceneUtil.Erase(cell);
                    SceneView.RepaintAll();
                    e.Use();
                }
                else if (e.button == 0)
                {
                    erasing = false;
                    dragging = true;
                    dragStart = cell;
                    hasLast = false;

                    if (mode == Mode.Brush)
                    {
                        var p = ResolvePrefab();
                        if (p == null) { Debug.LogWarning("[Grid Painter] No tile prefab."); return; }
                        PaintBrush(cell, p);
                        lastCell = cell;
                        hasLast = true;
                        SceneView.RepaintAll();
                    }
                    e.Use();
                }
                break;

            case EventType.MouseDrag:
                if (!dragging) return;
                dragCurrent = cell;

                if (erasing)
                {
                    GridSceneUtil.EraseLine(hasLast ? lastCell : cell, cell);
                    lastCell = cell;
                    hasLast = true;
                    SceneView.RepaintAll();
                }
                else if (mode == Mode.Brush)
                {
                    var p = ResolvePrefab();
                    if (p != null)
                    {
                        foreach (var c in GridSceneUtil.LineCells(hasLast ? lastCell : cell, cell)) PaintBrush(c, p);
                        lastCell = cell;
                        hasLast = true;
                        SceneView.RepaintAll();
                    }
                }
                e.Use();
                break;

            case EventType.MouseUp:
                if (dragging && !erasing && mode != Mode.Brush)
                {
                    var p = ResolvePrefab();
                    if (p != null)
                    {
                        if (mode == Mode.Line) GridSceneUtil.PlaceLine(dragStart, cell, brush, p);
                        else GridSceneUtil.PlaceRect(dragStart, cell, brush, p);
                        SceneView.RepaintAll();
                    }
                }
                dragging = false;
                erasing = false;
                hasLast = false;
                e.Use();
                break;
        }

        if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag || e.type == EventType.MouseDown)
        {
            DrawPreview(cell);
            view.Repaint();
        }
    }

    static Vector2Int CellUnderMouse(Vector2 guiPosition)
    {
        Vector3 world = ScenePoint(guiPosition);
        return GridUtil.WorldToCell(new Vector2(world.x, world.y));
    }

    static void DrawPreview(Vector2Int hover)
    {
        if (!dragging || erasing || mode == Mode.Brush)
        {
            DrawCell(hover, new Color(0.11f, 0.80f, 0.74f, 0.18f), new Color(0.11f, 0.80f, 0.74f, 0.85f));
            return;
        }

        var cells = mode == Mode.Line
            ? GridSceneUtil.LineCells(dragStart, hover)
            : null;

        if (cells != null)
        {
            int guard = 0;
            foreach (var c in cells)
            {
                DrawCell(c, new Color(0.11f, 0.80f, 0.74f, 0.22f), new Color(0.11f, 0.80f, 0.74f, 0.7f));
                if (++guard > 400) break;
            }
            return;
        }

        int x0 = Mathf.Min(dragStart.x, hover.x), x1 = Mathf.Max(dragStart.x, hover.x);
        int y0 = Mathf.Min(dragStart.y, hover.y), y1 = Mathf.Max(dragStart.y, hover.y);
        int count = 0;
        for (int x = x0; x <= x1; x++)
        {
            for (int y = y0; y <= y1; y++)
            {
                DrawCell(new Vector2Int(x, y), new Color(0.11f, 0.80f, 0.74f, 0.18f), new Color(0.11f, 0.80f, 0.74f, 0.55f));
                if (++count > 400) return;
            }
        }
    }

    static void DrawCell(Vector2Int cell, Color fill, Color outline)
    {
        var rect = new Vector3[]
        {
            new Vector3(cell.x, cell.y, 0f),
            new Vector3(cell.x + 1f, cell.y, 0f),
            new Vector3(cell.x + 1f, cell.y + 1f, 0f),
            new Vector3(cell.x, cell.y + 1f, 0f)
        };
        Handles.DrawSolidRectangleWithOutline(rect, fill, outline);
    }

    static Vector3 ScenePoint(Vector2 guiPosition)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(guiPosition);
        if (Mathf.Abs(ray.direction.z) < 0.0001f) return ray.origin;
        float t = -ray.origin.z / ray.direction.z;
        return ray.origin + ray.direction * t;
    }

    const int GridWidth = 40;
    const int GridHeight = 12;

    static void DrawGrid()
    {
        Handles.color = new Color(0.55f, 0.65f, 0.85f, 0.10f);
        for (int x = 0; x <= GridWidth; x++)
            Handles.DrawLine(new Vector3(x, 0f, 0f), new Vector3(x, GridHeight, 0f));
        for (int y = 0; y <= GridHeight; y++)
            Handles.DrawLine(new Vector3(0f, y, 0f), new Vector3(GridWidth, y, 0f));

        Handles.color = new Color(0.55f, 0.65f, 0.85f, 0.22f);
        for (int x = 0; x <= GridWidth; x += 5)
            Handles.DrawLine(new Vector3(x, 0f, 0f), new Vector3(x, GridHeight, 0f));
        for (int y = 0; y <= GridHeight; y += 5)
            Handles.DrawLine(new Vector3(0f, y, 0f), new Vector3(GridWidth, y, 0f));
    }
}
