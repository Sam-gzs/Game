using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Shared helpers for creating / editing grid tiles in a scene.</summary>
public static class GridSceneUtil
{
    public const string PrefabFolder = "Assets/Prefabs";
    public const string PrefabPath = PrefabFolder + "/GridTile.prefab";

    public static GameObject EnsurePrefab(Sprite sprite, Material material)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existing != null) return existing;

        if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets", "Prefabs");

        var go = new GameObject("GridTile");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        if (material != null) sr.sharedMaterial = material;
        sr.color = GridTile.SolidColor;
        go.AddComponent<BoxCollider2D>();
        go.AddComponent<GridTile>();

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();
        return prefab;
    }

    public static GridTile FindAt(Vector2Int cell)
    {
        var tiles = Object.FindObjectsByType<GridTile>(FindObjectsSortMode.None);
        for (int i = 0; i < tiles.Length; i++)
        {
            if (tiles[i].cell == cell) return tiles[i];
        }
        return null;
    }

    /// <summary>Fill every cell on the (Bresenham) line between two cells.</summary>
    public static int PlaceLine(Vector2Int a, Vector2Int b, TileType type, GameObject prefab)
    {
        int count = 0;
        foreach (var cell in LineCells(a, b))
        {
            Place(cell, type, prefab);
            count++;
        }
        return count;
    }

    /// <summary>Fill a rectangle of cells (inclusive).</summary>
    public static int PlaceRect(Vector2Int a, Vector2Int b, TileType type, GameObject prefab)
    {
        int count = 0;
        int x0 = Mathf.Min(a.x, b.x), x1 = Mathf.Max(a.x, b.x);
        int y0 = Mathf.Min(a.y, b.y), y1 = Mathf.Max(a.y, b.y);
        for (int x = x0; x <= x1; x++)
        {
            for (int y = y0; y <= y1; y++)
            {
                Place(new Vector2Int(x, y), type, prefab);
                count++;
            }
        }
        return count;
    }

    public static int EraseLine(Vector2Int a, Vector2Int b)
    {
        int count = 0;
        foreach (var cell in LineCells(a, b))
        {
            Erase(cell);
            count++;
        }
        return count;
    }

    public static System.Collections.Generic.IEnumerable<Vector2Int> LineCells(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(b.x - a.x), dy = -Mathf.Abs(b.y - a.y);
        int sx = a.x < b.x ? 1 : -1, sy = a.y < b.y ? 1 : -1;
        int err = dx + dy;
        int x = a.x, y = a.y;

        while (true)
        {
            yield return new Vector2Int(x, y);
            if (x == b.x && y == b.y) yield break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x += sx; }
            if (e2 <= dx) { err += dx; y += sy; }
        }
    }

    /// <summary>Create or retype the tile in a cell.</summary>
    public static GridTile Place(Vector2Int cell, TileType type, GameObject prefab)
    {
        var tile = FindAt(cell);
        if (tile == null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
            tile = go.GetComponent<GridTile>();
            if (tile == null) tile = go.AddComponent<GridTile>();
        }

        tile.cell = cell;
        tile.SetType(type);
        tile.transform.position = new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
        EditorUtility.SetDirty(tile);
        return tile;
    }

    public static void Erase(Vector2Int cell)
    {
        var tile = FindAt(cell);
        if (tile != null) Object.DestroyImmediate(tile.gameObject);
    }

    public static void SnapAll()
    {
        var tiles = Object.FindObjectsByType<GridTile>(FindObjectsSortMode.None);
        for (int i = 0; i < tiles.Length; i++)
        {
            var t = tiles[i];
            t.cell = GridUtil.WorldToCell(t.transform.position);
            t.transform.position = new Vector3(t.cell.x + 0.5f, t.cell.y + 0.5f, 0f);
            t.Apply();
            EditorUtility.SetDirty(t);
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }
}
