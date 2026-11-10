using UnityEngine;

/// <summary>
/// The crack/shatter animation of a destructible block.
///
/// When a ball breaks dirt / wood / stone the tile itself is destroyed the same frame the ball
/// steps into the cell, so the animation cannot live on the tile. Instead this spawns a short
/// lived sprite at the same spot that walks the material's frame sequence from
/// <see cref="BrickArt"/>, holds the last frame for a moment and fades out.
///
/// It has no collider: the ball flies through it, it is purely decoration.
/// </summary>
public class BrickBreakFx : MonoBehaviour
{
    /// <summary>Playback speed of the crack frames.</summary>
    public static float FramesPerSecond = 24f;

    /// <summary>How long the final (most broken) frame stays fully visible.</summary>
    public static float HoldTime = 0.07f;

    /// <summary>Fade-out time after the hold.</summary>
    public static float FadeTime = 0.12f;

    /// <summary>Between the tiles (1) and the ball (6) so the ball stays readable.</summary>
    public static int SortingOrder = 5;

    Sprite[] frames;
    SpriteRenderer sr;
    float age;

    public static void Play(TileType type, Vector3 position)
    {
        if (!Application.isPlaying) return;

        var frames = BrickArt.Frames(type);
        if (frames.Length < 2) return;   // a single frame is not an animation

        var go = new GameObject("BrickBreak");
        go.transform.position = position;

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = SortingOrder;
        renderer.color = Color.white;

        var fx = go.AddComponent<BrickBreakFx>();
        fx.frames = frames;
        fx.sr = renderer;
        renderer.sprite = frames[1];     // frame 0 was already on screen as the block itself
    }

    void Update()
    {
        if (sr == null || frames == null || frames.Length == 0)
        {
            Destroy(gameObject);
            return;
        }

        age += Time.deltaTime;

        float step = 1f / Mathf.Max(1f, FramesPerSecond);
        int index = 1 + Mathf.FloorToInt(age / step);

        if (index >= frames.Length)
        {
            index = frames.Length - 1;

            // the last frame starts at (length - 2) * step, then hold, then fade
            float sinceLast = age - (frames.Length - 2) * step;
            if (sinceLast > HoldTime)
            {
                float a = 1f - Mathf.Clamp01((sinceLast - HoldTime) / Mathf.Max(0.01f, FadeTime));
                var c = sr.color;
                c.a = a;
                sr.color = c;
                if (a <= 0f)
                {
                    Destroy(gameObject);
                    return;
                }
            }
        }

        sr.sprite = frames[index];
    }
}
