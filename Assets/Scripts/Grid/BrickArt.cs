using UnityEngine;

/// <summary>
/// Art for the three destructible blocks (dirt / wood / stone).
///
/// Each material is a short sequence of frames that crack apart: frame 0 is the block as it
/// stands in the level, the following frames are the shatter. Sprites are plain files in
/// Assets/Resources, so adding or reordering a frame is a data change, not a code change:
///
///   BlockDirt  BlockDirt_1  BlockDirt_2  BlockDirt_3
///   BlockWood  BlockWood_1  BlockWood_2
///   BlockStone BlockStone_1 BlockStone_2
///
/// The first file has no suffix for historical reasons - several scenes already point their
/// tiles' SpriteRenderer at it, and renaming it would silently drop those references.
/// </summary>
public static class BrickArt
{
    public const string DirtName = "BlockDirt";
    public const string WoodName = "BlockWood";
    public const string StoneName = "BlockStone";

    /// <summary>Frames per material, frame 0 included.</summary>
    public const int DirtFrames = 4;
    public const int WoodFrames = 3;
    public const int StoneFrames = 3;

    // 0 = dirt, 1 = wood, 2 = stone
    static readonly Sprite[][] cache = new Sprite[3][];

    public static bool IsBrick(TileType type)
    {
        return type == TileType.Dirt || type == TileType.Wood || type == TileType.Stone;
    }

    static int MaterialIndex(TileType type)
    {
        if (type == TileType.Dirt) return 0;
        if (type == TileType.Wood) return 1;
        if (type == TileType.Stone) return 2;
        return -1;
    }

    static string BaseName(TileType type)
    {
        if (type == TileType.Dirt) return DirtName;
        if (type == TileType.Wood) return WoodName;
        return StoneName;
    }

    static int FrameCount(TileType type)
    {
        if (type == TileType.Dirt) return DirtFrames;
        if (type == TileType.Wood) return WoodFrames;
        return StoneFrames;
    }

    /// <summary>The whole crack sequence, frame 0 first. Empty for anything that is not a brick.</summary>
    public static Sprite[] Frames(TileType type)
    {
        int m = MaterialIndex(type);
        if (m < 0) return new Sprite[0];

        if (cache[m] != null) return cache[m];

        string baseName = BaseName(type);
        int count = FrameCount(type);
        var frames = new Sprite[count];
        int found = 0;

        for (int i = 0; i < count; i++)
        {
            string path = i == 0 ? baseName : baseName + "_" + i;
            var s = Resources.Load<Sprite>(path);
            if (s == null) break;          // a shorter sequence is fine, the animation just ends sooner
            frames[i] = s;
            found = i + 1;
        }

        if (found < count)
        {
            var trimmed = new Sprite[found];
            System.Array.Copy(frames, trimmed, found);
            frames = trimmed;
        }

        cache[m] = frames;
        return frames;
    }

    /// <summary>Frame 0: the sprite a brick shows while it is still intact.</summary>
    public static Sprite Still(TileType type)
    {
        var frames = Frames(type);
        return frames.Length > 0 ? frames[0] : null;
    }

    /// <summary>Drops the cached sprites - used when entering play mode with domain reload disabled.</summary>
    public static void ClearCache()
    {
        cache[0] = cache[1] = cache[2] = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        ClearCache();
    }
}
