using UnityEngine;

/// <summary>
/// Builds a whole grid level from a one-line ASCII map at startup.
/// Rows are separated by '|' (top row first). This keeps the demo levels small
/// and lets the new mechanics be authored without painting every tile by hand.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class RuntimeLevel : MonoBehaviour
{
    public GameObject tilePrefab;

    [TextArea(3, 10)]
    public string map;

    void Awake()
    {
        if (tilePrefab == null || string.IsNullOrEmpty(map)) return;

        string[] rows = map.Split('|');
        int height = rows.Length;

        for (int r = 0; r < height; r++)
        {
            string row = rows[r];
            int y = height - 1 - r;

            for (int x = 0; x < row.Length; x++)
            {
                char c = row[x];
                if (c == '.') continue;

                var go = Instantiate(tilePrefab);
                go.name = "GridTile";

                var tile = go.GetComponent<GridTile>();
                if (tile == null) tile = go.AddComponent<GridTile>();

                tile.cell = new Vector2Int(x, y);
                go.transform.position = new Vector3(x + 0.5f, y + 0.5f, 0f);

                ApplyChar(tile, c);
            }
        }
    }

    static void ApplyChar(GridTile t, char c)
    {
        TileType type;
        int channel = 1;
        bool invert = false;
        bool mirrorDown = true;

        switch (c)
        {
            case '#': type = TileType.Solid; break;
            case 'S': type = TileType.Stone; break;
            case 'D': type = TileType.Dirt; break;
            case 'P': type = TileType.Spawn; break;
            case 'E': type = TileType.Door; break;
            case 'F': type = TileType.Falling; break;
            case 'B': type = TileType.Bounce; break;
            case 'X': type = TileType.Pickup; break;
            case 'R': type = TileType.RefractorDown; break;
            case 'T': type = TileType.RefractorUp; break;

            case 'K': type = TileType.Switch; channel = 1; break;
            case 'L': type = TileType.Switch; channel = 2; break;
            case 'N': type = TileType.Switch; channel = 3; break;

            case 'H': type = TileType.Gate; channel = 1; break;
            case 'G': type = TileType.Gate; channel = 2; break;
            case 'J': type = TileType.Gate; channel = 3; break;

            case 'h': type = TileType.Gate; channel = 1; invert = true; break;
            case 'g': type = TileType.Gate; channel = 2; invert = true; break;
            case 'j': type = TileType.Gate; channel = 3; invert = true; break;

            case 'M': type = TileType.Mirror; channel = 1; mirrorDown = true; break;
            case 'Z': type = TileType.Mirror; channel = 1; mirrorDown = false; break;
            case 'm': type = TileType.Mirror; channel = 2; mirrorDown = true; break;
            case 'z': type = TileType.Mirror; channel = 2; mirrorDown = false; break;

            case 'O': type = TileType.Plate; channel = 1; break;
            case 'o': type = TileType.Plate; channel = 2; break;
            case 'Q': type = TileType.Plate; channel = 3; break;

            default: return;
        }

        t.channel = channel;
        t.invert = invert;
        t.mirrorDown = mirrorDown;
        t.SetType(type);
    }
}
