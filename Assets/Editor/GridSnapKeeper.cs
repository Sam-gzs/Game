using UnityEditor;
using UnityEngine;

/// <summary>
/// Grid snapping in edit mode.
///   - the tile you have selected is pulled to the nearest cell immediately (magnet while dragging)
///   - every tile is checked twice a second, so nothing can end up off-grid
/// Only objects that are actually off-grid are written, so the scene stays clean.
/// </summary>
[InitializeOnLoad]
public static class GridSnapKeeper
{
    static double nextSelected;
    static double nextSweep;

    static GridSnapKeeper()
    {
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        double now = EditorApplication.timeSinceStartup;

        // 1) the selected tile follows the mouse in cell steps (20 checks / second)
        if (now >= nextSelected)
        {
            nextSelected = now + 0.05;
            var selected = Selection.activeGameObject;
            if (selected != null)
            {
                var tile = selected.GetComponent<GridTile>();
                if (tile != null) tile.EnforceGrid();
            }
        }

        // 2) cheap safety sweep (2 checks / second) so nothing stays off-grid
        if (now >= nextSweep)
        {
            nextSweep = now + 0.5;
            var tiles = Object.FindObjectsByType<GridTile>(FindObjectsSortMode.None);
            for (int i = 0; i < tiles.Length; i++)
            {
                var t = tiles[i];
                if (t == null) continue;

                Vector2Int cell = GridUtil.WorldToCell(new Vector2(t.transform.position.x, t.transform.position.y));
                Vector2 center = GridUtil.CellCenter(cell);
                bool off = (t.transform.position - new Vector3(center.x, center.y, 0f)).sqrMagnitude > 0.000001f;
                bool scaled = t.transform.localScale != Vector3.one;

                if (off || scaled || t.cell != cell) t.EnforceGrid();
            }
        }
    }
}
