using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sprite cache for the cat art drop in Assets/Resources/Cat.
///
/// Two cats share the same poses:
///   cat_g_*  the grey-white cat you always start with
///   cat_o_*  the orange cat, the second cat obtained from the level-4 pickup
///
/// Every GIF in the drop runs at 200 ms per frame. All exported sprites use one
/// shared 64x64 canvas, so swapping clips never shifts the cat.
/// </summary>
public static class CatArt
{
    public const string Root = "Cat/";
    public const float FrameTime = 0.2f;

    static readonly Dictionary<string, Sprite> s_sprites = new Dictionary<string, Sprite>();
    static readonly Dictionary<string, Sprite[]> s_clips = new Dictionary<string, Sprite[]>();

    public static Sprite Get(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        Sprite s;
        if (s_sprites.TryGetValue(name, out s) && s != null) return s;
        s = Resources.Load<Sprite>(Root + name);
        s_sprites[name] = s;
        return s;
    }

    /// <summary>Load prefix_0 .. prefix_(count-1). Returns null if any frame is missing.</summary>
    public static Sprite[] Clip(string prefix, int count)
    {
        Sprite[] set;
        if (s_clips.TryGetValue(prefix, out set) && set != null) return set;

        var list = new List<Sprite>();
        for (int i = 0; i < count; i++)
        {
            var s = Get(prefix + "_" + i);
            if (s == null) return null;
            list.Add(s);
        }
        set = list.ToArray();
        s_clips[prefix] = set;
        return set;
    }

    /// <summary>Rolling / flying loop, 11 frames.</summary>
    public static Sprite[] Roll(bool orange)
    {
        return Clip(orange ? "cat_o_roll" : "cat_g_roll", 11);
    }

    /// <summary>Stuck on a slime block, 3-frame blink loop.</summary>
    public static Sprite[] Stuck(bool orange)
    {
        return Clip(orange ? "cat_o_stuck" : "cat_g_stuck", 3);
    }

    /// <summary>The second cat walking in place on the pickup tile, 5 frames.</summary>
    public static Sprite[] Walk(bool orange)
    {
        return Clip(orange ? "cat_o_walk" : "cat_g_walk", 5);
    }

    /// <summary>Standing -> curling into a ball, shown when the second cat is collected.</summary>
    public static Sprite[] Curl()
    {
        return Clip("cat_o_curl", 6);
    }

    /// <summary>Front-facing resting pose. The grey cat has its own placeholder idle.</summary>
    public static Sprite Idle(bool orange)
    {
        return orange ? Get("cat_o_roll_10") : Get("cat_g_idle");
    }
}
