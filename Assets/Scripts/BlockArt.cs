using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Every block / HUD sprite produced from the art drop lives in Resources/Blocks.
/// One place to change a name, one cache, so a tile never re-loads per frame.
/// A missing file simply returns null and the caller keeps its old flat-colour look.
/// </summary>
public static class BlockArt
{
    public const string Root = "Blocks/";

    static readonly Dictionary<string, Sprite> s_sprites = new Dictionary<string, Sprite>();
    static readonly Dictionary<string, Texture2D> s_textures = new Dictionary<string, Texture2D>();
    static readonly Dictionary<string, Sprite[]> s_frames = new Dictionary<string, Sprite[]>();

    public static Sprite Get(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        Sprite s;
        if (s_sprites.TryGetValue(name, out s) && s != null) return s;
        s = Resources.Load<Sprite>(Root + name);
        s_sprites[name] = s;
        return s;
    }

    public static Texture2D Texture(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        Texture2D t;
        if (s_textures.TryGetValue(name, out t) && t != null) return t;
        t = Resources.Load<Texture2D>(Root + name);
        s_textures[name] = t;
        return t;
    }

    /// <summary>The idle sprite of a tile type. Solid/stone/wood used to be flat colour only.</summary>
    public static Sprite ForTile(TileType t)
    {
        switch (t)
        {
            case TileType.Solid: return Get("ground");
            case TileType.Dirt: return Get("glass_1");
            case TileType.Wood: return Get("wood_1");
            case TileType.Stone: return Get("iron_1");
            case TileType.Sticky: return Get("sticky");
            case TileType.Slider: return Get("slider");
            case TileType.Falling: return Get("metal");
            case TileType.Switch: return Get("btn_off");
            case TileType.BallSwitch: return Get("btn_off");
            case TileType.Gate: return Get("gate");
            // The four wedge rotations live in RefractFacing; these two are the
            // defaults that match the legacy "/" and "\" mirror sprites.
            case TileType.RefractorDown: return Get("mirror_ru");
            case TileType.RefractorUp: return Get("mirror_lu");
        }
        return null;
    }

    /// <summary>Crates shatter through four damage frames; everything else has a single frame.</summary>
    public static Sprite[] CrateFrames(TileType t)
    {
        string prefix = t == TileType.Dirt ? "glass" : t == TileType.Wood ? "wood" : t == TileType.Stone ? "iron" : null;
        if (prefix == null) return null;

        Sprite[] set;
        if (s_frames.TryGetValue(prefix, out set) && set != null) return set;

        var list = new List<Sprite>();
        for (int i = 1; i <= 4; i++)
        {
            var s = Get(prefix + "_" + i);
            if (s == null) return null;
            list.Add(s);
        }
        set = list.ToArray();
        s_frames[prefix] = set;
        return set;
    }

    /// <summary>Every GIF in the art drop runs at 200 ms per frame.</summary>
    public const float GifFrameDuration = 0.2f;

    /// <summary>
    /// The actual GIF frames of a crate. These are separate from the idle PNGs so the
    /// animation really is the artist's GIF, not a re-ordered set of stills.
    /// </summary>
    public static Sprite[] CrateAnimation(TileType t)
    {
        string prefix = t == TileType.Dirt ? "anim_glass" : t == TileType.Wood ? "anim_wood" : t == TileType.Stone ? "anim_iron" : null;
        if (prefix == null) return null;

        Sprite[] set;
        if (s_frames.TryGetValue(prefix, out set) && set != null) return set;

        var list = new List<Sprite>();
        for (int i = 0; i < 4; i++)
        {
            var s = Get(prefix + "_" + i);
            if (s == null) return CrateFrames(t);
            list.Add(s);
        }
        set = list.ToArray();
        s_frames[prefix] = set;
        return set;
    }

    /// <summary>Charge bar frame: 0 = empty, 3 = full.</summary>
    public static Texture2D ChargeBar(int level)
    {
        return Texture("ui_charge_" + Mathf.Clamp(level, 0, 3));
    }

    /// <summary>The charge GIF, played by the HUD while the button is held.</summary>
    public static Texture2D ChargeAnimationFrame(int frame)
    {
        frame = Mathf.Clamp(frame, 0, 3);
        var t = Texture("anim_charge_" + frame);
        return t != null ? t : ChargeBar(frame);
    }
}
