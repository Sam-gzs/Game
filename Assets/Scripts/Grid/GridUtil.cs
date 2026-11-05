using UnityEngine;

/// <summary>One grid cell = one world unit. Cell (x, y) spans [x, x+1] x [y, y+1].</summary>
public static class GridUtil
{
    public const float Cell = 1f;

    public static Vector2 CellCenter(Vector2Int cell)
    {
        return new Vector2(cell.x + 0.5f, cell.y + 0.5f);
    }

    public static Vector2Int WorldToCell(Vector2 world)
    {
        return new Vector2Int(Mathf.FloorToInt(world.x / Cell), Mathf.FloorToInt(world.y / Cell));
    }

    public static Vector2 SnapToCellCenter(Vector2 world)
    {
        return CellCenter(WorldToCell(world));
    }
}
