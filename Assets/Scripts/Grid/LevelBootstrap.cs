using UnityEngine;

/// <summary>Placed in every level scene: puts the player on the Spawn tile and fits the camera.</summary>
public class LevelBootstrap : MonoBehaviour
{
    public int levelIndex;

    void Start()
    {
        Time.timeScale = 1f;

        GridTile spawn = null;
        var tiles = Object.FindObjectsByType<GridTile>(FindObjectsSortMode.None);
        float minX = float.MaxValue, maxX = float.MinValue;

        for (int i = 0; i < tiles.Length; i++)
        {
            var t = tiles[i];
            if (t.type == TileType.Spawn) spawn = t;
            if (t.cell.x < minX) minX = t.cell.x;
            if (t.cell.x > maxX) maxX = t.cell.x;
        }

        var player = Object.FindFirstObjectByType<PlayerController>();
        if (spawn != null && player != null)
        {
            // the player is 2 cells tall: stand on top of the spawn cell
            player.transform.position = new Vector3(spawn.cell.x + 0.5f, spawn.cell.y + 1f, 0f);
            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;
        }

        var cam = Object.FindFirstObjectByType<CameraFollow>();
        if (cam != null)
        {
            cam.minX = minX + 9.5f;
            cam.maxX = maxX - 9.5f;
            if (cam.minX > cam.maxX)
            {
                float mid = (minX + maxX) * 0.5f;
                cam.minX = mid;
                cam.maxX = mid;
            }
            cam.maxY = 12f;
            if (player != null) cam.target = player.transform;
        }

        // The spawn tile is only a placement marker. It must not stay in the grid,
        // otherwise BoxProjectile.IsFree treats it as a solid and blocks throws.
        if (spawn != null) Destroy(spawn.gameObject);
    }
}
